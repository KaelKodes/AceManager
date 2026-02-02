using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AceManager.Core.Strategy
{
    public static class StrategicWorldGenerator
    {
        private static Random _rng = new Random();

        public static void GenerateSector(MapData map, AirbaseData homeBase)
        {
            GD.Print("[StrategicWorldGenerator] Beginning sector generation...");

            // 0. Cleanup Logic
            map.StrategicNodes.Clear();
            map.SupplyLines.Clear();
            map.FrontlineSegments.Clear();

            // 1. Generate Frontline Segments from existing points
            GenerateFrontlineSegments(map);

            // 2. Generate Nodes for both sides
            GenerateFactionNodes(map, "Allied", homeBase);
            GenerateFactionNodes(map, "Axis", homeBase);

            // 2b. Explicitly add Player Home Base
            CreatePlayerNode(map, homeBase);

            // 2c. Explicitly add Allied Hubs
            GenerateCentralHub(map);
            GenerateSoutheastHub(map);

            // 2d. Add Country Labels
            GenerateCountryLabels(map);

            // 3. Connect Logistics
            ConnectLogistics(map);

            GD.Print($"[StrategicWorldGenerator] Generation Complete. Nodes: {map.StrategicNodes.Count}, Supply Lines: {map.SupplyLines.Count}");
        }

        private static void GenerateCentralHub(MapData map)
        {
            // User Update: Le Bourget N. Hub
            // Pos: 125.9, -5488.6
            Vector2 pos = new Vector2(125.9f, -5488.6f);

            var node = new LogisticsNode
            {
                Id = "allied_hub_central_le_bourget",
                Name = "Le Bourget N. Hub", // "North of Le Bourget"
                RegionId = DetermineRegion(pos.Y),
                WorldCoordinates = pos,
                OwningNation = "Allied",
                IsRailHub = true,
                ThroughputCapacity = 150, // Major Hub
                IntelStatus = StrategicNode.IntelLevel.Confirmed
            };
            map.StrategicNodes.Add(node);
            GD.Print("[StrategicWorldGenerator] Created Central Hub: Le Bourget N. Hub");
        }

        private static void GenerateSoutheastHub(MapData map)
        {
            // User Update: Chalons Southeast Hub
            // Pos: 425.5, -5347.6
            Vector2 pos = new Vector2(425.5f, -5347.6f);

            var node = new LogisticsNode
            {
                Id = "allied_hub_southeast_chalons",
                Name = "Chalons Southeast Hub",
                RegionId = DetermineRegion(pos.Y),
                WorldCoordinates = pos,
                OwningNation = "Allied",
                IsRailHub = true,
                ThroughputCapacity = 120,
                IntelStatus = StrategicNode.IntelLevel.Confirmed
            };
            map.StrategicNodes.Add(node);
            GD.Print("[StrategicWorldGenerator] Created Southeast Hub: Chalons Southeast Hub");
        }

        private static void GenerateCountryLabels(MapData map)
        {
            // Great Britain (Northwest)
            // User: "north a pinch and west a double pinch" from B-284
            AddCountryLabel(map, "GREAT BRITAIN", "X1", -286);

            // Netherlands (Northeast area, but west of original T)
            // User: "the same" (north a pinch, west a double pinch) from T-284
            AddCountryLabel(map, "NETHERLANDS", "R", -286);

            // Belgium (North-Central)
            // User: "pretty good"
            AddCountryLabel(map, "BELGIUM", "O", -278);

            // Germany (East)
            // User: "good x, just need to go up a little higher than belgium"
            AddCountryLabel(map, "GERMANY", "Y", -281);

            // France (South)
            // User: "tiny bit lower so its font doesnt overlap Le Bourget"
            AddCountryLabel(map, "FRANCE", "F", -271);
        }

        private static void AddCountryLabel(MapData map, string name, string colLabel, int rowLabel)
        {
            int colIdx = 0;
            string colUpper = colLabel.ToUpper();

            if (colUpper.StartsWith("X"))
            {
                if (int.TryParse(colUpper.Substring(1), out int xVal))
                {
                    colIdx = -xVal;
                }
            }
            else
            {
                for (int i = 0; i < colUpper.Length; i++)
                {
                    colIdx = colIdx * 26 + (colUpper[i] - 'A' + 1);
                }
                colIdx -= 1;
            }

            float tactX = colIdx * 20.0f + 10.0f;
            float tactY = (rowLabel - 1) * 20.0f + 10.0f; // Label row to index is -1 approx

            Vector2 pivotKM = map.GetWorldCoordinates(new Vector2(map.PivotLon, map.PivotLat));
            Vector2 offsetKM = map.GetWorldCoordinates(new Vector2(map.LonOffset, map.LatOffset));

            float worldX = pivotKM.X + (tactX - pivotKM.X - offsetKM.X) / map.LonSpread;
            float worldY = pivotKM.Y + (tactY - pivotKM.Y - offsetKM.Y) / map.LatSpread;
            Vector2 pos = new Vector2(worldX, worldY);

            var node = new RegionLabelNode
            {
                Id = $"label_{name.ToLower().Replace(" ", "_")}",
                Name = name,
                WorldCoordinates = pos,
                IntelStatus = StrategicNode.IntelLevel.Confirmed // Always visible
            };
            map.StrategicNodes.Add(node);
        }

        private static void CreatePlayerNode(MapData map, AirbaseData homeBase)
        {
            if (homeBase == null) return;

            var node = new MilitaryNode
            {
                Id = "player_home_base",
                Name = homeBase.Name ?? "Home Base", // Fallback name
                RegionId = DetermineRegion(homeBase.Coordinates.Y),
                WorldCoordinates = map.GetWorldCoordinates(homeBase.Coordinates), // Convert Lat/Lon to WorldKM
                OwningNation = "Allied",
                Readiness = 100f,
                IntelStatus = StrategicNode.IntelLevel.Confirmed
            };
            map.StrategicNodes.Add(node);
        }

        private static void GenerateFrontlineSegments(MapData map)
        {
            if (map.FrontLinePoints == null || map.FrontLinePoints.Length < 2) return;

            // Create segments between each point
            for (int i = 0; i < map.FrontLinePoints.Length - 1; i++)
            {
                var segment = new FrontlineSegment
                {
                    SegmentId = i,
                    StartPoint = map.FrontLinePoints[i],
                    EndPoint = map.FrontLinePoints[i + 1],
                    RegionId = DetermineRegion(map.FrontLinePoints[i].Y), // Approx region based on Lat
                    DisplacementKM = 0,
                    AlliedPressure = 50f, // Balanced start
                    AxisPressure = 50f
                };
                map.FrontlineSegments.Add(segment);
            }
        }

        private static void GenerateFactionNodes(MapData map, string faction, AirbaseData homeBase)
        {
            // Special handling for Allied Presets
            if (faction == "Allied")
            {
                GenerateAlliedFixedNodes(map);

                // Still need to ensure player base creation
                CreatePlayerNode(map, homeBase);
                return;
            }

            // Faction direction multiplier (West vs East)
            // Assuming Allies are West (-X), Axis East (+X) generally, 
            // but this depends on the specific map pivot.
            // For now, let's use the 'FrontLine' as the divider.

            // We'll generate nodes relative to each Frontline Segment to ensure coverage

            foreach (var segment in map.FrontlineSegments)
            {
                // Only spawn heavy infrastructure every few segments to avoid clutter
                if (segment.SegmentId % 3 != 0) continue;

                Vector2 center = segment.GetCenterPoint();
                Vector2 normal = GetSegmentNormal(segment); // Points towards Axis

                // Direction: Allies = -Normal, Axis = +Normal
                Vector2 dir = faction == "Axis" ? normal : -normal;

                // 1. Forward Outpost (Near Front) - New "Lesser Base"
                CreateInfantryBase(map, faction, center + (dir * _rng.Next(4, 12)), segment.SegmentId, "Forward Outpost", 400);

                // 2. Div HQ (Back from front) - Pushed back
                CreateInfantryBase(map, faction, center + (dir * _rng.Next(25, 50)), segment.SegmentId, "Div. HQ", 1200);

                // 3. Supply Depot (Mid range)
                CreateLogisticsNode(map, faction, center + (dir * _rng.Next(30, 60)), false);

                // 4. Rail Hub (Rear)
                if (segment.SegmentId % 4 == 0) // Slightly more hubs to ensure connectivity
                {
                    CreateLogisticsNode(map, faction, center + (dir * _rng.Next(60, 90)), true);
                }

                // 5. Industry (Deep Rear)
                if (segment.SegmentId % 8 == 0) // Includes 0
                {
                    Vector2 indPos = center + (dir * _rng.Next(90, 150));
                    // FIX: Northern works (Seg 0) is hitting water/Sector B. Nudge East (Pos Y/X depending on rotation, here Normal points East).
                    // Normal is (dir.Y, -dir.X). If diff is (end-start), normal is East-ish.
                    // Actually, just pushing further "back" might help, or specifically shifting along the segment axis.

                    if (segment.SegmentId == 0)
                    {
                        // Shift "Down" the front line (Positive Y) or "East" (Positive X) 
                        // Map coordinates: X is Lon, Y is NegLat. 
                        // B is West of C. Needs to go East (Positive X).
                        indPos.X += 65; // Hard nudge East
                    }

                    CreateIndustrialNode(map, faction, indPos);
                }
            }
        }

        private static void GenerateAlliedFixedNodes(MapData map)
        {
            // User provided coordinates (Feb 2026)
            // We apply a small random offset (+/- 2km) to keep it organic ("deviate a little")

            // Format: (X, Y, NameSuffix, Type, Strength/Capacity)

            var definitions = new List<(Vector2 Pos, string Suffix, string Type, int Value)>
            {
                // -- Sector North (Flanders) --
                (new Vector2(198.7f, -5667.9f), "Forward Outpost", "Infantry", 400),
                (new Vector2(164.1f, -5659.3f), "Div. HQ", "Infantry", 1200),
                (new Vector2(176.2f, -5654.9f), "Supply Depot", "Logistics", 0),
                (new Vector2(142.8f, -5648.9f), "Rail Hub", "RailHub", 0),
                (new Vector2(147.2f, -5628.4f), "Munitions Works", "Industry", 0),
                
                // -- Sector North-Mid --
                (new Vector2(203.2f, -5611.6f), "Forward Outpost", "Infantry", 400),
                (new Vector2(180.5f, -5611.9f), "Div. HQ", "Infantry", 1200),
                (new Vector2(171.5f, -5599.2f), "Supply Depot", "Logistics", 0),
                
                // -- Sector Mid (Arras/Somme) --
                (new Vector2(206.9f, -5565.2f), "Forward Outpost", "Infantry", 400),
                (new Vector2(187.5f, -5560.2f), "Div. HQ", "Infantry", 1200),
                (new Vector2(168.4f, -5571.0f), "Supply Depot", "Logistics", 0),
                
                // -- Sector South-Mid (Aisne) --
                (new Vector2(235.4f, -5499.1f), "Forward Outpost", "Infantry", 400),
                (new Vector2(202.2f, -5491.7f), "Div. HQ", "Infantry", 1200),
                (new Vector2(189.8f, -5507.1f), "Supply Depot", "Logistics", 0),
                
                // -- Sector South (Champagne) --
                (new Vector2(291.0f, -5463.7f), "Forward Outpost", "Infantry", 400),
                (new Vector2(304.2f, -5449.3f), "Div. HQ", "Infantry", 1200),
                (new Vector2(297.7f, -5432.8f), "Supply Depot", "Logistics", 0),
                (new Vector2(314.6f, -5409.5f), "Rail Hub", "RailHub", 0),
                
                // -- Sector Deep South (Verdun/Meuse) --
                (new Vector2(370.8f, -5448.7f), "Forward Outpost", "Infantry", 400),
                (new Vector2(365.1f, -5419.9f), "Div. HQ", "Infantry", 1200),
                (new Vector2(362.1f, -5427.9f), "Supply Depot", "Logistics", 0),
                
                // -- Sector Far South East (Vosges) --
                (new Vector2(492.7f, -5346.9f), "Forward Outpost", "Infantry", 400),
                (new Vector2(467.5f, -5335.6f), "Div. HQ", "Infantry", 1200),
                (new Vector2(461.0f, -5355.5f), "Supply Depot", "Logistics", 0),
            };

            foreach (var def in definitions)
            {
                Vector2 variation = new Vector2((float)(_rng.NextDouble() * 4 - 2), (float)(_rng.NextDouble() * 4 - 2));
                Vector2 finalPos = def.Pos + variation;

                // Fallback RegionId calc (approximated since we don't have the segment ID handy easily here, 
                // but determine region by Y is robust enough)
                int regionHint = -1;
                // We could find the closest segment if needed for assignments, but the current logic assigns later or uses Y.

                switch (def.Type)
                {
                    case "Infantry":
                        CreateInfantryBase(map, "Allied", finalPos, regionHint, def.Suffix, def.Value);
                        break;
                    case "Logistics":
                        CreateLogisticsNode(map, "Allied", finalPos, false);
                        break;
                    case "RailHub":
                        CreateLogisticsNode(map, "Allied", finalPos, true);
                        break;
                    case "Industry":
                        CreateIndustrialNode(map, "Allied", finalPos);
                        break;
                }
            }

            GD.Print($"[StrategicWorldGenerator] Generated {definitions.Count} Fixed Allied Nodes.");
        }

        private static void CreateInfantryBase(MapData map, string faction, Vector2 pos, int segmentId, string nameSuffix, int strength)
        {
            var node = new InfantryBase
            {
                Id = $"{faction}_inf_{Guid.NewGuid().ToString().Substring(0, 4)}",
                Name = $"{faction} {nameSuffix}",
                RegionId = DetermineRegion(pos.Y),
                WorldCoordinates = pos,
                OwningNation = faction,
                AssignedFrontlineSegmentId = segmentId,
                GroundStrength = _rng.Next((int)(strength * 0.8), (int)(strength * 1.2)),
                // Allied bases are known. Axis bases on the front are Rumored (visible but vague).
                IntelStatus = (faction == "Allied") ? StrategicNode.IntelLevel.Confirmed : StrategicNode.IntelLevel.Rumored
            };
            map.StrategicNodes.Add(node);
        }

        private static void CreateLogisticsNode(MapData map, string faction, Vector2 pos, bool isRailHub)
        {
            string shortId = Guid.NewGuid().ToString().Substring(0, 4);
            var node = new LogisticsNode
            {
                Id = $"{faction}_{(isRailHub ? "hub" : "depot")}_{shortId}",
                Name = isRailHub ? $"{faction} Rail Hub ({shortId})" : $"{faction} Supply Depot ({shortId})",
                RegionId = DetermineRegion(pos.Y),
                WorldCoordinates = pos,
                OwningNation = faction,
                IsRailHub = isRailHub,
                ThroughputCapacity = isRailHub ? 100 : 50,
                // Logistics in rear are Unknown for Axis, Confirmed for Allies
                IntelStatus = (faction == "Allied") ? StrategicNode.IntelLevel.Confirmed : StrategicNode.IntelLevel.Unknown
            };
            map.StrategicNodes.Add(node);
        }

        private static void CreateIndustrialNode(MapData map, string faction, Vector2 pos)
        {
            var type = (IndustryType)_rng.Next(0, 3);
            string shortId = Guid.NewGuid().ToString().Substring(0, 4);
            var node = new IndustrialNode
            {
                Id = $"{faction}_ind_{shortId}",
                Name = $"{faction} {type} Works ({shortId})",
                RegionId = DetermineRegion(pos.Y),
                WorldCoordinates = pos,
                OwningNation = faction,
                ProductionType = type,
                ProductionRate = _rng.Next(10, 20),
                IntelStatus = (faction == "Allied") ? StrategicNode.IntelLevel.Confirmed : StrategicNode.IntelLevel.Unknown
            };
            map.StrategicNodes.Add(node);
        }

        private static void ConnectLogistics(MapData map)
        {
            // Simple logic: Connect Children to nearest Parent
            // Factories -> Hubs -> Depots -> Infantry Bases

            var factories = map.StrategicNodes.OfType<IndustrialNode>().ToList();
            var hubs = map.StrategicNodes.OfType<LogisticsNode>().Where(n => n.IsRailHub).ToList();
            var depots = map.StrategicNodes.OfType<LogisticsNode>().Where(n => !n.IsRailHub).ToList();

            // Connect ALL military nodes (Infantry Bases AND Player Airfield)
            var bases = map.StrategicNodes.OfType<MilitaryNode>().ToList();
            GD.Print($"[StrategicWorldGenerator] Connecting Layers. Factories: {factories.Count}, Hubs: {hubs.Count}, Depots: {depots.Count}, Bases: {bases.Count}");

            // 1. Connect Rail Network (Hub <-> Hub) - RAIL ONLY
            ConnectRailNetwork(map, hubs);

            // 2. Connect Layers (Roads)
            // Factories connect to Hubs (Road)
            ConnectLayers(map, hubs, factories);


            // Hubs -> Depots (Road)
            ConnectLayers(map, hubs, depots);
            // Depots -> Bases (Road)
            ConnectLayers(map, depots, bases);

            // --- FAILSAFE: Player Base Connection ---
            var playerNode = bases.FirstOrDefault(n => n.Id == "player_home_base");
            if (playerNode != null)
            {
                if (playerNode.ParentNode == null)
                {
                    GD.PrintErr("[StrategicWorldGenerator] Player Base failed standard connection! Force-linking to nearest Allied Depot.");
                    var alliedDepots = depots.Where(d => d.OwningNation == "Allied").ToList();
                    if (alliedDepots.Count > 0)
                    {
                        var nearest = alliedDepots.OrderBy(d => d.WorldCoordinates.DistanceTo(playerNode.WorldCoordinates)).First();
                        playerNode.ParentNode = nearest;
                        playerNode.OriginalParent = nearest;
                        nearest.ChildNodes.Add(playerNode);
                        float dist = nearest.WorldCoordinates.DistanceTo(playerNode.WorldCoordinates);
                        map.SupplyLines.Add(new SupplyLine(nearest.Id, playerNode.Id, dist, false));
                        GD.Print($"[StrategicWorldGenerator] Failsafe successful. Connected Player to {nearest.Name} (Dist: {dist:F1}km).");
                    }
                    else
                    {
                        GD.PrintErr("[StrategicWorldGenerator] CRITICAL: No Allied Depots found for player connection!");
                    }
                }
                else
                {
                    GD.Print($"[StrategicWorldGenerator] Player Base successfully connected to {playerNode.ParentNode.Name}.");
                }
            }
            else
            {
                GD.PrintErr("[StrategicWorldGenerator] CRITICAL: Player Node 'player_home_base' not found in bases list!");
            }
            // ----------------------------------------
        }

        private static void ConnectRailNetwork(MapData map, List<LogisticsNode> hubs)
        {
            // Connect Allied hubs in a line
            // Sort by Y to create a north-south line or similar
            var alliedHubs = hubs.Where(h => h.OwningNation == "Allied").OrderBy(h => h.WorldCoordinates.Y).ToList();
            if (alliedHubs.Count > 1)
            {
                for (int i = 0; i < alliedHubs.Count - 1; i++)
                {
                    var h1 = alliedHubs[i];
                    var h2 = alliedHubs[i + 1];
                    float dist = (h1.WorldCoordinates - h2.WorldCoordinates).Length();
                    // Hub <-> Hub is RAIL
                    map.SupplyLines.Add(new SupplyLine(h1.Id, h2.Id, dist, true));
                }
            }

            // Connect Axis hubs
            var axisHubs = hubs.Where(h => h.OwningNation == "Axis").OrderBy(h => h.WorldCoordinates.Y).ToList();
            if (axisHubs.Count > 1)
            {
                for (int i = 0; i < axisHubs.Count - 1; i++)
                {
                    var h1 = axisHubs[i];
                    var h2 = axisHubs[i + 1];
                    float dist = (h1.WorldCoordinates - h2.WorldCoordinates).Length();
                    map.SupplyLines.Add(new SupplyLine(h1.Id, h2.Id, dist, true));
                }
            }
        }

        private static void ConnectLayers<T, U>(MapData map, List<T> parents, List<U> children)
            where T : StrategicNode where U : StrategicNode
        {
            foreach (var child in children)
            {
                // Enforce Single Parent: if already connected, skip.
                if (child.ParentNode != null) continue;

                // Find nearest parent of same faction
                var parent = parents
                    .Where(p => p.OwningNation == child.OwningNation)
                    .OrderBy(p => (p.WorldCoordinates - child.WorldCoordinates).Length())
                    .FirstOrDefault();

                if (parent != null)
                {
                    child.ParentNode = parent;
                    child.OriginalParent = parent;
                    parent.ChildNodes.Add(child);

                    // Logic: ConnectLayers is for Roads (Factories->Hubs, Hubs->Depots, Depots->Bases)
                    // Hub<->Hub connection is handled separately in ConnectRailNetwork
                    bool isRail = false;
                    float dist = (parent.WorldCoordinates - child.WorldCoordinates).Length();

                    map.SupplyLines.Add(new SupplyLine(parent.Id, child.Id, dist, isRail));
                }
            }
        }

        private static Vector2 GetSegmentNormal(FrontlineSegment segment)
        {
            Vector2 dir = (segment.EndPoint - segment.StartPoint).Normalized();
            return new Vector2(dir.Y, -dir.X); // Rotated 90 deg, pointing roughly East
        }

        private static string DetermineRegion(float latKM) // Raw World Y
        {
            // Y decreases North (more negative), increases South (less negative)
            // North: < -5550 (Lat > 50.0)
            // Mid: -5550 to -5440 (Lat 49.0 - 50.0)
            // South: > -5440 (Lat < 49.0)

            if (latKM < -5550) return "North";
            if (latKM > -5440) return "South";
            return "Mid";
        }
    }
}
