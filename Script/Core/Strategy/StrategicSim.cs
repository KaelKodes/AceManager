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
        public static void ProcessTurn(MapData map)
        {
            if (map == null) return;
            GD.Print($"[StrategicSim] Processing Turn for {map.StrategicNodes.Count} nodes...");

            // 1. Production Phase
            foreach (var node in map.StrategicNodes.OfType<IndustrialNode>())
            {
                ProcessProduction(node);
            }

            // 2. Logistics Phase (Push Supply)
            // Process from top of hierarchy down: Hubs -> Depots -> Bases
            // We can approximate this by sorting by 'distance from front' or just multiple passes.
            // For now, simple iteration. Factories push to hubs instantly? 
            // Better: Factories added to stockpile. Supply push logic moves from Parent to Child.

            // We need to process parents before children to allow flow in one turn, 
            // or just accept 1-turn lag (simpler). Let's accept 1-turn lag for "Pro High" stability.

            foreach (var node in map.StrategicNodes)
            {
                if (node.ParentNode != null)
                {
                    PushSupply(node.ParentNode, node);
                }
            }

            // 3. Frontline Phase
            ProcessFrontline(map);
        }

        private static void ProcessProduction(IndustrialNode factory)
        {
            if (factory.IsDestroyed) return;

            // Simple production: factories have infinite raw materials for now
            // They just add to their own local stockpile, which parent logic pulls/pushes?
            // Actually, in our graph, Factories are ROOTS. They push to Hubs (Children).

            // Wait, generated logic was: Parent = Hub, Child = Factory? 
            // "ConnectLayers(map, factories, hubs)" -> Factories are Parents of Hubs?
            // Let's check StrategicWorldGenerator.ConnectLayers:
            // "parents" are the first arg.
            // ConnectLayers(factories, hubs) -> Factory is Parent of Hub.
            // ConnectLayers(hubs, depots) -> Hub is Parent of Depot.
            // ConnectLayers(depots, bases) -> Depot is Parent of Base.
            // Flow: Parent -> Child. Correct.

            // So Factory produces into its own stockpile.
            // Then PushSupply moves it to Child.

            // Current abstract StrategicNode doesn't have a generic "Resource" storage yet.
            // We might need to add a generic 'SupplyStockpile' float to StrategicNode based on Abstract.
            // For now, let's assume 'Integrity' affects 'effectiveness' and we simulate abstract "Supply" flow.

            // TODO: Add 'Stockpile' to StrategicNode if we want real resource tracking.
            // For this version (MVP+), we simulate "Throughput".
            // A node is "Supplied" if its parent has supply.
        }

        private static void PushSupply(StrategicNode parent, StrategicNode child)
        {
            // Calculate efficiency based on integrity and interdiction
            float efficiency = (parent.CurrentIntegrity / parent.MaxIntegrity) *
                               (child.CurrentIntegrity / child.MaxIntegrity);

            // If rail, higher throughput
            // If road (supply line), distance penalty?

            // For MVP: Transfer "Readiness" or "Fuel/Ammo"
            if (child is MilitaryNode milNode)
            {
                // Bases regain readiness if supplied
                if (efficiency > 0.5f)
                {
                    milNode.Readiness = Math.Min(100, milNode.Readiness + 5 * efficiency);
                }
                else
                {
                    milNode.Readiness = Math.Max(0, milNode.Readiness - 2); // Attrition
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
