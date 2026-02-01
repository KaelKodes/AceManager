using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using AceManager.Core.Strategy;

namespace AceManager.Core
{
    /// <summary>
    /// Represents a point of interest on the command map.
    /// Can be bases, targets, enemy positions, etc.
    /// </summary>
    public class MapLocation
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Vector2 WorldCoordinates { get; set; } // Global KM relative to (0 Lat, 0 Lon)
        public Vector2 LocalCoordinates { get; set; } // Relative to sector center/home base (for legacy UI compat if needed)
        public LocationType Type { get; set; }
        public string Nation { get; set; }
        public bool IsDiscovered { get; set; }
        public DateTime DiscoveredDate { get; set; }
        public string Notes { get; set; }

        public enum LocationType
        {
            HomeBase,
            AlliedBase,
            SupplyDepot,
            Hospital,
            EnemyAirfield,
            EnemyPosition,
            FrontLine,
            Town,
            Bridge,
            RailYard,
            Factory,
            Unknown
        }
    }

    /// <summary>
    /// Holds all map data for the player's current base sector.
    /// Persistent data that survives between sessions.
    /// </summary>
    public partial class MapData : Resource
    {
        // Known locations - discovered through missions and intel
        public List<MapLocation> Locations { get; set; } = new List<MapLocation>();

        // Front line segments as coordinate pairs
        [Export] public Vector2[] FrontLinePoints { get; set; }

        // --- Strategic World Data ---
        [Export] public Godot.Collections.Array<StrategicNode> StrategicNodes { get; set; } = new Godot.Collections.Array<StrategicNode>();
        [Export] public Godot.Collections.Array<SupplyLine> SupplyLines { get; set; } = new Godot.Collections.Array<SupplyLine>();
        [Export] public Godot.Collections.Array<FrontlineSegment> FrontlineSegments { get; set; } = new Godot.Collections.Array<FrontlineSegment>();
        // -----------------------------

        // Sector bounds
        [Export] public Vector2 SectorMin { get; set; }
        [Export] public Vector2 SectorMax { get; set; }
        [Export] public string SectorName { get; set; }

        // Tactical Calibration (Synced with UI for Western Front)
        [Export] public float LonOffset { get; set; } = -0.5f;
        [Export] public float LatOffset { get; set; } = 0.2f;
        [Export] public float LonSpread { get; set; } = 1.0f;
        [Export] public float LatSpread { get; set; } = 0.9f;
        [Export] public float PivotLon { get; set; } = 3.5f;
        [Export] public float PivotLat { get; set; } = 50.0f;

        public Vector2 GetWorldCoordinates(Vector2 latLon)
        {
            // 1 degree Latitude = ~111km
            // 1 degree Longitude at 50°N = ~71km
            float lonScale = 71f;
            float latScale = 111f;

            float x = latLon.X * lonScale;
            float y = -latLon.Y * latScale; // Lat increases North, Godot Y increases South

            return new Vector2(x, y);
        }

        /// <summary>
        /// Converts raw World KM to "Tactical KM" matching the visual map calibration.
        /// </summary>
        public Vector2 GetTacticalCoordinates(Vector2 worldPosKM)
        {
            // Center of the Front (Nieuport to Belfort approx)
            Vector2 pivotKM = GetWorldCoordinates(new Vector2(PivotLon, PivotLat));
            Vector2 offsetKM = GetWorldCoordinates(new Vector2(LonOffset, LatOffset));

            // 1. Spread around pivot
            float distFromPivotX = worldPosKM.X - pivotKM.X;
            float spreadX = pivotKM.X + (distFromPivotX * LonSpread);

            float distFromPivotY = worldPosKM.Y - pivotKM.Y;
            float spreadY = pivotKM.Y + (distFromPivotY * LatSpread);

            // 2. Apply Global Offset
            return new Vector2(spreadX + offsetKM.X, spreadY + offsetKM.Y);
        }

        public List<MapLocation> GetDiscoveredLocations()
        {
            return Locations.Where(l => l.IsDiscovered).ToList();
        }

        /// <summary>
        /// Generates a historical map centered on the current airbase.
        /// Loads other airbases from the database.
        /// </summary>
        public static MapData GenerateHistoricalMap(AirbaseData homeBase)
        {
            var map = new MapData
            {
                SectorName = $"{homeBase.Name} Sector",
                SectorMin = new Vector2(-100, -100),
                SectorMax = new Vector2(100, 100)
            };

            Vector2 homeCoords = homeBase.Coordinates;
            // Emergency Fallback: If parsing failed and we are at 0,0, default to St. Omer (approximate RFC HQ)
            if (homeCoords.Length() < 0.01f)
            {
                GD.PrintErr($"WARNING: Home base {homeBase.Name} has invalid coordinates (0,0). Using RFC HQ St. Omer fallback.");
                homeCoords = new Vector2(2.2610f, 50.7510f); // Lon, Lat
            }

            Vector2 homeWorldPos = map.GetWorldCoordinates(homeCoords);
            int currentYear = GameManager.Instance.CurrentDate.Year;

            // 1. Add Home Base
            map.Locations.Add(new MapLocation
            {
                Id = "home_base",
                Name = homeBase.Name,
                WorldCoordinates = homeWorldPos,
                Type = MapLocation.LocationType.HomeBase,
                Nation = homeBase.Nation,
                IsDiscovered = true,
                Notes = "Primary operational base."
            });

            // 2. Load all other airbases from Database
            var allBases = DataLoader.LoadAirbaseDatabase();
            GD.Print($"[MapData] Loaded {allBases.Count} airbases from database.");
            var nearbyAllied = new List<(MapLocation Location, float Distance)>();

            foreach (var b in allBases)
            {
                if (b.Name == homeBase.Name) continue;

                // Date filtering
                if (!IsBaseActive(b.ActiveYears, currentYear)) continue;

                Vector2 worldPos = map.GetWorldCoordinates(b.Coordinates);

                // Safety: Skip invalid/unparsed coordinates
                if (b.Coordinates.Length() < 0.01f) continue;

                // Filter out obviously remote theatres (e.g. Italy/Palestine) based on Latitude
                // Keep Western Front (Lat > 48.0 approx)
                if (b.Coordinates.Y < 48.0f) continue;

                // Load ALL Western Front bases for visual calibration
                // (Removed the 'dist < 60' check)

                bool isAllied = (b.Nation == homeBase.Nation) || (homeBase.Nation != "Germany" && b.Nation != "Germany");
                var type = isAllied ? MapLocation.LocationType.AlliedBase : MapLocation.LocationType.EnemyAirfield;

                // Add and Auto-Discover everything
                var loc = AddLocation(map, $"base_{b.Name.ToLower().Replace(" ", "_")}", b.Name, worldPos, type, b.Nation, true);
                if (isAllied && loc != null)
                {
                    nearbyAllied.Add((loc, (worldPos - homeWorldPos).Length()));
                }
            }

            // Discover 2 nearest allied bases
            if (nearbyAllied.Count > 0)
            {
                var sorted = nearbyAllied.OrderBy(x => x.Distance).ToList();
                for (int i = 0; i < Math.Min(2, sorted.Count); i++)
                {
                    sorted[i].Location.IsDiscovered = true;
                    sorted[i].Location.DiscoveredDate = GameManager.Instance.CurrentDate;

                    // Make one of them a supply depot for gameplay variety
                    if (i == 0) sorted[i].Location.Type = MapLocation.LocationType.SupplyDepot;
                }
            }

            // 3. Define Historical Front Line (Approximation)
            // Focused on Western Front only for now
            map.FrontLinePoints = GenerateWesternFront(homeBase.Coordinates, map);

            // 4. Add procedural points of interest (Towns, Bridges, Factories)
            GeneratePointsOfInterest(map, homeBase);

            // 5. Generate Strategic World (Nodes, Supply Lines)
            StrategicWorldGenerator.GenerateSector(map, homeBase);

            return map;
        }

        public static Vector2 GenerateProceduralTarget(Vector2 startPos, int distanceKM, string nation)
        {
            Random rng = new Random();
            float range = distanceKM; // Input is now directly in KM

            // Basic logic: Enemy is generally East (+X) on Western Front
            float angle = (float)(rng.NextDouble() * Math.PI / 2) - (float)(Math.PI / 4);

            Vector2 targetOffset = new Vector2(
                (float)Math.Cos(angle) * range,
                (float)Math.Sin(angle) * range
            );

            targetOffset.X = Math.Abs(targetOffset.X); // Ensure East
            targetOffset.Y = -Math.Abs(targetOffset.Y); // Ensure North-ish

            return startPos + targetOffset;
        }

        public static List<Vector2> GenerateWaypoints(Vector2 startPos, Vector2 targetPos, int distanceKM)
        {
            var waypoints = new List<Vector2>();
            waypoints.Add(startPos);

            // Add a "dogleg" if distance is significant (> 30km)
            if (distanceKM > 30)
            {
                Random rng = new Random();
                Vector2 midPoint = (startPos + targetPos) / 2;
                Vector2 targetOffset = targetPos - startPos;
                Vector2 perpendicular = new Vector2(-targetOffset.Y, targetOffset.X).Normalized();

                // Dogleg offset relative to distance (smaller relative deviation for longer flights)
                float offset = (float)(rng.NextDouble() - 0.5) * targetOffset.Length() * 0.25f;
                waypoints.Add(midPoint + perpendicular * offset);
            }

            waypoints.Add(targetPos);

            // Return to start
            waypoints.Add(startPos);

            return waypoints;
        }

        private static bool IsBaseActive(string activeYears, int currentYear)
        {
            if (string.IsNullOrEmpty(activeYears)) return true;

            // Expected formats: "1914", "1914 – 1918", "1917 - 1918"
            string[] parts = activeYears.Split(new[] { '–', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                if (int.TryParse(parts[0].Trim(), out int start))
                    return currentYear >= start;
            }
            else if (parts.Length >= 2)
            {
                if (int.TryParse(parts[0].Trim(), out int start) && int.TryParse(parts[1].Trim(), out int end))
                    return currentYear >= start && currentYear <= (end + 1); // Allow small buffer
            }

            return true;
        }

        private static Vector2[] GenerateWesternFront(Vector2 homeCoords, MapData map)
        {
            // Simplified Western Front line points (Lon, Lat)
            // Accurate 1916 Trench Line (Final User Calibration)
            var points = new List<Vector2> {
                new Vector2(2.86f, 51.17f),
                new Vector2(2.95f, 51.00f),
                new Vector2(2.98f, 50.81f),
                new Vector2(2.90f, 50.64f),
                new Vector2(2.98f, 50.51f),
                new Vector2(2.91f, 50.35f),
                new Vector2(3.06f, 50.21f),
                new Vector2(3.02f, 50.04f),
                new Vector2(3.02f, 49.77f),
                new Vector2(3.44f, 49.65f),
                new Vector2(3.47f, 49.45f),
                new Vector2(3.56f, 49.28f),
                new Vector2(3.88f, 49.23f),
                new Vector2(4.24f, 49.33f),
                new Vector2(4.56f, 49.31f),
                new Vector2(4.80f, 49.17f),
                new Vector2(5.66f, 49.13f),
                new Vector2(6.60f, 48.84f),
                new Vector2(7.08f, 48.46f),
                new Vector2(7.08f, 47.88f),
            };

            return points.Select(p => map.GetWorldCoordinates(p)).ToArray();
        }

        private static Vector2[] GenerateItalianFront(Vector2 homeCoords, MapData map)
        {
            // Simplified Italian Front line points (Lon, Lat)
            var points = new List<Vector2> {
                new Vector2(10.5f, 46.0f), // Lake Garda
                new Vector2(11.5f, 45.8f), // Asiago
                new Vector2(12.2f, 45.8f), // Piave River
                new Vector2(13.5f, 46.0f)  // Isonzo
            };

            return points.Select(p => map.GetWorldCoordinates(p)).ToArray();
        }

        private static void GeneratePointsOfInterest(MapData map, AirbaseData homeBase)
        {
            var rng = new Random(homeBase.Name.GetHashCode()); // Seeded for consistency per base
            Vector2 homeWorldPos = map.GetWorldCoordinates(homeBase.Coordinates);

            // Add a few targets near the front line for each segment
            if (map.FrontLinePoints == null) return;

            for (int i = 0; i < map.FrontLinePoints.Length - 1; i++)
            {
                Vector2 a = map.FrontLinePoints[i];
                Vector2 b = map.FrontLinePoints[i + 1];
                Vector2 mid = (a + b) / 2;
                Vector2 dir = (b - a).Normalized();
                Vector2 normal = new Vector2(-dir.Y, dir.X); // Points "Eastish" or "Westish"

                // Enemy side target (East of line in West, North of line in Italy?)
                // For simplicity, offset by normal
                float enemySideMultiplier = (homeBase.Nation == "Germany") ? -1 : 1;

                Vector2 targetPos = mid + (normal * 15 * enemySideMultiplier) + (dir * (float)(rng.NextDouble() - 0.5) * 10);
                AddLocation(map, $"bridge_{i}", "River Bridge", targetPos, MapLocation.LocationType.Bridge, "Enemy", false);

                // Vector2 townPos = mid - (normal * 20 * enemySideMultiplier) + (dir * (float)(rng.NextDouble() - 0.5) * 20);
                // AddLocation(map, $"town_{i}", "Frontline Town", townPos, MapLocation.LocationType.Town, homeBase.Nation, true);
            }
        }

        private static MapLocation AddLocation(MapData map, string id, string name, Vector2 worldCoords, MapLocation.LocationType type, string nation, bool startDiscovered)
        {
            // Avoid duplicates
            var existing = map.Locations.FirstOrDefault(l => l.Name == name && (l.WorldCoordinates - worldCoords).Length() < 0.1f);
            if (existing != null)
                return existing;

            var loc = new MapLocation
            {
                Id = id,
                Name = name,
                WorldCoordinates = worldCoords,
                Type = type,
                Nation = nation,
                IsDiscovered = startDiscovered,
                DiscoveredDate = startDiscovered ? new DateTime(1917, 1, 1) : DateTime.MinValue,
                Notes = type.ToString()
            };

            map.Locations.Add(loc);
            return loc;
        }
    }
}
