using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AceManager.Core.Strategy
{
    /// <summary>
    /// The core simulation engine for the strategic layer.
    /// Handles resource production, supply distribution, and frontline movement.
    /// Executed once per day.
    /// </summary>
    public static class StrategicSim
    {
        private static MainCommander _alliedMC;
        private static MainCommander _axisMC;
        private static List<SubCommander> _alliedSCs;
        private static List<SubCommander> _axisSCs;

        public static void ProcessTurn(MapData map)
        {
            if (map == null) return;
            GD.Print($"[StrategicSim] Processing Turn for {map.StrategicNodes.Count} nodes...");

            // 0. AI Commander Phase
            RunAICommanderPhase(map);

            // 1. Logistical Rerouting Phase
            // If parents are destroyed, try to find alternatives
            HandleLogisticalRerouting(map);

            // 2. Production Phase
            foreach (var node in map.StrategicNodes.OfType<IndustrialNode>())
            {
                ProcessProduction(node);
            }

            // 3. Logistics Workload Phase
            // Calculate how many nodes each depot is supporting
            CalculateLogisticsWorkload(map);

            // 4. Supply Distribution Phase (Push Supply)
            foreach (var node in map.StrategicNodes)
            {
                if (node.ParentNode != null)
                {
                    PushSupply(node.ParentNode, node);
                }
            }

            // 5. Repair Phase
            // Damaged facilities slowly recover if still supplied
            ProcessRepairs(map);

            // 6. Frontline Phase
            ProcessFrontline(map);
        }

        private static void RunAICommanderPhase(MapData map)
        {
            // Lazy initialization
            if (_alliedMC == null)
            {
                _alliedMC = new MainCommander("Allied");
                _alliedSCs = new List<SubCommander> {
                    new SubCommander("Allied", "North"),
                    new SubCommander("Allied", "Mid"),
                    new SubCommander("Allied", "South")
                };
            }
            if (_axisMC == null)
            {
                _axisMC = new MainCommander("Axis");
                _axisSCs = new List<SubCommander> {
                    new SubCommander("Axis", "North"),
                    new SubCommander("Axis", "Mid"),
                    new SubCommander("Axis", "South")
                };
            }

            // Update Global Intent
            _alliedMC.Update(map);
            _axisMC.Update(map);

            // Regional Assignment
            foreach (var sc in _alliedSCs) sc.AssignMissions(map, _alliedMC);
            foreach (var sc in _axisSCs) sc.AssignMissions(map, _axisMC);
        }

        private static void HandleLogisticalRerouting(MapData map)
        {
            foreach (var node in map.StrategicNodes)
            {
                // Skip decorative/non-functional nodes
                if (node is RegionLabelNode || node is IndustrialNode) continue;

                // If current parent is destroyed or missing, or if we haven't initialized parent yet, try to reroute
                if (node.ParentNode == null || node.ParentNode.IsDestroyed)
                {
                    // Clean up broken connection
                    if (node.ParentNode != null)
                    {
                        node.ParentNode.ChildNodes.Remove(node);
                        node.ParentNode = null;
                        node.OriginalParent = null; // Clean up original if it's dead
                    }

                    // Attempt to find a new parent
                    // Enforce hierarchy: 
                    // Hubs can only reroute to IndustrialNodes or other Hubs.
                    // Depots/Bases can reroute to Hubs or Depots.
                    bool isHub = node is LogisticsNode log && log.IsRailHub;

                    var possibleParents = map.StrategicNodes
                        .Where(p => p != node && p.OwningNation == node.OwningNation && !p.IsDestroyed)
                        .Where(p =>
                        {
                            if (isHub) return p is IndustrialNode || (p is LogisticsNode hub && hub.IsRailHub);
                            return p is LogisticsNode; // Depots/Bases can connect to any hub/depot
                        })
                        .OrderBy(p => (p.WorldCoordinates - node.WorldCoordinates).Length())
                        .ToList();

                    // Radius limit: Hubs have longer reach (200km), Depots/Bases (100km)
                    float maxRadius = isHub ? 200f : 100f;
                    var nearest = possibleParents.FirstOrDefault();

                    if (nearest != null && (nearest.WorldCoordinates - node.WorldCoordinates).Length() < maxRadius)
                    {
                        node.ParentNode = nearest;
                        if (!nearest.ChildNodes.Contains(node))
                            nearest.ChildNodes.Add(node);

                        node.IsStarved = false;
                        GD.Print($"[Logistics] {node.Name} rerouted to {nearest.Name}.");
                    }
                    else
                    {
                        node.IsStarved = true;
                        GD.Print($"[Logistics] {node.Name} is SEVERED (No valid parent).");
                    }
                }
                else
                {
                    // Parent exists and is alive
                    node.IsStarved = false;
                }
            }
        }

        private static void CalculateLogisticsWorkload(MapData map)
        {
            // Reset workloads
            foreach (var node in map.StrategicNodes.OfType<LogisticsNode>())
            {
                node.CurrentWorkload = 0;
            }

            // Calculate current workload
            foreach (var node in map.StrategicNodes)
            {
                if (node.ParentNode is LogisticsNode parentLogistics)
                {
                    parentLogistics.CurrentWorkload += 1f;
                }
            }
        }

        private static void ProcessProduction(IndustrialNode factory)
        {
            if (factory.IsDestroyed)
            {
                factory.SupplyLevel = 0;
                return;
            }

            // Factories (Hearts) are always at 100% supply if not destroyed
            factory.SupplyLevel = (factory.CurrentIntegrity / factory.MaxIntegrity) * 100f;
        }

        private static void PushSupply(StrategicNode parent, StrategicNode child)
        {
            if (parent.IsDestroyed)
            {
                child.SupplyLevel = 0;
                return;
            }

            // Base efficiency is inherited from parent
            float efficiency = parent.SupplyLevel / 100f;

            // Apply throttling if parent is overloaded
            if (parent is LogisticsNode logParent)
            {
                // Max capacity = 3 standard nodes
                float capacity = 3.0f;
                if (logParent.CurrentWorkload > capacity)
                {
                    float bottleneck = capacity / logParent.CurrentWorkload;
                    efficiency *= bottleneck;
                }
            }

            // Apply integrity degradation
            efficiency *= (parent.CurrentIntegrity / parent.MaxIntegrity);

            child.SupplyLevel = Math.Clamp(efficiency * 100f, 0f, 100f);

            // Apply result to Readiness if Military
            if (child is MilitaryNode milNode)
            {
                if (child.SupplyLevel > 50f)
                {
                    milNode.Readiness = Math.Min(100, milNode.Readiness + 5 * (child.SupplyLevel / 100f));
                }
                else
                {
                    milNode.Readiness = Math.Max(0, milNode.Readiness - 2); // Attrition
                }
            }
        }

        private static void ProcessRepairs(MapData map)
        {
            foreach (var node in map.StrategicNodes)
            {
                if (node.IsDestroyed) continue;

                if (node.CurrentIntegrity < node.MaxIntegrity)
                {
                    // Repair only if sufficiently supplied
                    if (node.SupplyLevel > 50f)
                    {
                        node.Repair(2.0f); // 2% per day
                    }
                }
            }
        }

        private static void ProcessFrontline(MapData map)
        {
            // 1. Reset Pressure
            foreach (var segment in map.FrontlineSegments)
            {
                segment.AlliedPressure = 0;
                segment.AxisPressure = 0;
            }

            // 2. Aggregate Pressure from Bases
            foreach (var node in map.StrategicNodes.OfType<MilitaryNode>())
            {
                if (node.IsDestroyed) continue;

                // Only InfantryBases have "AssignedFrontlineSegmentId" usually?
                // MilitaryNode property?

                // We need to cast to InfantryBase to get the SegmentID specifically, 
                // OR add SegmentID to MilitaryNode.
                // Looking at classes: InfantryBase has AssignedFrontlineSegmentId.
                // MilitaryNode does not. 

                if (node is InfantryBase inf)
                {
                    var segment = map.FrontlineSegments.FirstOrDefault(s => s.SegmentId == inf.AssignedFrontlineSegmentId);
                    if (segment != null)
                    {
                        float power = inf.GroundStrength * (inf.Readiness / 100f);
                        if (inf.OwningNation == "Allied") segment.AlliedPressure += power;
                        else segment.AxisPressure += power;
                    }
                }
            }

            // 3. Move the Line
            foreach (var segment in map.FrontlineSegments)
            {
                const float THRESHOLD = 1000f; // Force required to move line significantly
                float netDiff = segment.AlliedPressure - segment.AxisPressure;

                // Displacement logic
                // Positive = Push Axis (Move East/North depending on vector)
                // Negative = Push Allied

                float moveCheck = netDiff / THRESHOLD; // e.g. 2000 diff / 1000 = +2km push?

                // Clamping movement per turn to avoid wild swings
                float moveKM = Math.Clamp(moveCheck, -0.5f, 0.5f);

                segment.DisplacementKM += moveKM;

                // We should probably clamp total displacement too (e.g. max 50km deep)
                segment.DisplacementKM = Math.Clamp(segment.DisplacementKM, -30, 30);
            }

            // 4. Update Visuals? 
            // FrontlineSegment only stores "DisplacementKM". 
            // MapRenderer or MapData needs to Recalculate the actual Vector2 points based on this displacement.
            // That's a complex geometry task. 
            // For now, we update the data. Visuals might need a "UpdateFrontlinePoints" method in MapData.
        }
    }
}
