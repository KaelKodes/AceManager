using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

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
		public Vector2 Coordinates { get; set; }
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

		// Sector bounds
		[Export] public Vector2 SectorMin { get; set; }
		[Export] public Vector2 SectorMax { get; set; }
		[Export] public string SectorName { get; set; }

		public void AddDiscoveredLocation(MapLocation location)
		{
			var existing = Locations.FirstOrDefault(l => l.Id == location.Id);
			if (existing == null)
			{
				location.IsDiscovered = true;
				Locations.Add(location);
			}
		}

		public List<MapLocation> GetDiscoveredLocations()
		{
			return Locations.Where(l => l.IsDiscovered).ToList();
		}

		public List<MapLocation> GetLocationsByType(MapLocation.LocationType type)
		{
			return Locations.Where(l => l.IsDiscovered && l.Type == type).ToList();
		}

		/// <summary>
		/// Initialize a basic map for the St-Omer sector (starting area)
		/// </summary>
		public static MapData CreateStOmerSector(AirbaseData homeBase)
		{
			var map = new MapData
			{
				SectorName = "St-Omer Sector",
				SectorMin = new Vector2(-50, -50),  // Relative km from base
				SectorMax = new Vector2(50, 50),
				FrontLinePoints = new Vector2[]
				{
					new Vector2(50, -50),  // Front line runs roughly NE-SW
					new Vector2(40, -20),
					new Vector2(35, 0),
					new Vector2(30, 25),
					new Vector2(25, 50)
				}
			};

			// Add home base (always known)
			map.Locations.Add(new MapLocation
			{
				Id = "home_base",
				Name = homeBase.Name,
				Coordinates = Vector2.Zero,  // Home base is center of map
				Type = MapLocation.LocationType.HomeBase,
				Nation = "Britain",
				IsDiscovered = true,
				Notes = "Home airfield - RFC operational base"
			});

			// Add some initially known allied locations
			map.Locations.Add(new MapLocation
			{
				Id = "supply_depot_1",
				Name = "Hazebrouck Supply Depot",
				Coordinates = new Vector2(-15, -8),
				Type = MapLocation.LocationType.SupplyDepot,
				Nation = "Britain",
				IsDiscovered = true,
				Notes = "Main supply depot for the sector"
			});

			map.Locations.Add(new MapLocation
			{
				Id = "hospital_1",
				Name = "Bailleul Field Hospital",
				Coordinates = new Vector2(-10, 12),
				Type = MapLocation.LocationType.Hospital,
				Nation = "Britain",
				IsDiscovered = true,
				Notes = "Casualty treatment facility"
			});

			// Undiscovered enemy positions (will be revealed through recon)
			map.Locations.Add(new MapLocation
			{
				Id = "enemy_airfield_1",
				Name = "Lille-Lesquin Aerodrome",
				Coordinates = new Vector2(30, 5),
				Type = MapLocation.LocationType.EnemyAirfield,
				Nation = "Germany",
				IsDiscovered = false,
				Notes = "German Jasta operating base"
			});

			map.Locations.Add(new MapLocation
			{
				Id = "enemy_position_1",
				Name = "German Artillery Battery",
				Coordinates = new Vector2(32, -15),
				Type = MapLocation.LocationType.EnemyPosition,
				Nation = "Germany",
				IsDiscovered = false
			});

			map.Locations.Add(new MapLocation
			{
				Id = "bridge_1",
				Name = "Menin Road Bridge",
				Coordinates = new Vector2(28, 18),
				Type = MapLocation.LocationType.Bridge,
				Nation = "Germany",
				IsDiscovered = false,
				Notes = "Strategic crossing point"
			});

			return map;
		}
	}
}
