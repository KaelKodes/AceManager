using Godot;
using System;
using System.Collections.Generic;

namespace AceManager.Core
{
    public partial class AirbaseData : Resource
    {
        [Export] public string Name { get; set; }
        [Export] public string Nation { get; set; }
        [Export] public string Location { get; set; }
        [Export] public Vector2 Coordinates { get; set; }
        [Export] public string ActiveYears { get; set; }
        [Export] public string Notes { get; set; }

        public enum Archetype { GrassStrip, ForwardBase, RegionalHub }
        [Export] public Archetype BaseArchetype { get; set; } = Archetype.GrassStrip;
        [Export] public int BaseLevel { get; set; } = 1; // 1-5

        // Ratings (1-5)
        [Export] public int RunwayRating { get; set; } = 1;
        [Export] public int LodgingRating { get; set; } = 1;
        [Export] public int MaintenanceRating { get; set; } = 1;
        [Export] public int FuelStorageRating { get; set; } = 1;
        [Export] public int AmmunitionStorageRating { get; set; } = 1;
        [Export] public int OperationsRating { get; set; } = 1;
        [Export] public int MedicalRating { get; set; } = 1;
        [Export] public int TransportAccessRating { get; set; } = 1;
        [Export] public int TrainingFacilitiesRating { get; set; } = 1;

        // Current resources
        [Export] public float CurrentFuel { get; set; }
        [Export] public float CurrentAmmo { get; set; }
        [Export] public int CurrentSpareParts { get; set; }

        public int GetMaxPilotCapacity()
        {
            // Level 1 = 8 pilots, Level 2 = 16, etc.
            return LodgingRating * 8;
        }

        public int GetMaxAircraftCapacity()
        {
            // Derived from Maintenance, Fuel, Ammo, and Training
            // Average of these four ratings * 6 (starter count)
            float avg = (MaintenanceRating + FuelStorageRating + AmmunitionStorageRating + TrainingFacilitiesRating) / 4.0f;
            return (int)Math.Max(4, Math.Floor(avg * 6));
        }

        public float GetEfficiencyBonus()
        {
            // Operations Center improves logistics planning
            // Level 1: 0%
            // Level 2: 5%
            // Level 3: 10%
            // Level 4: 15%
            // Level 5: 20%
            return (OperationsRating - 1) * 0.05f;
        }

        public int GetRating(string facilityName)
        {
            return facilityName switch
            {
                "Runway" => RunwayRating,
                "Lodging" => LodgingRating,
                "Maintenance" => MaintenanceRating,
                "Fuel Storage" => FuelStorageRating,
                "Ammo Storage" => AmmunitionStorageRating,
                "Operations" => OperationsRating,
                "Medical" => MedicalRating,
                "Transport" => TransportAccessRating,
                "Training" => TrainingFacilitiesRating,
                _ => 0
            };
        }

        public void SetRating(string facilityName, int rating)
        {
            switch (facilityName)
            {
                case "Runway": RunwayRating = rating; break;
                case "Lodging": LodgingRating = rating; break;
                case "Maintenance": MaintenanceRating = rating; break;
                case "Fuel Storage": FuelStorageRating = rating; break;
                case "Ammo Storage": AmmunitionStorageRating = rating; break;
                case "Operations": OperationsRating = rating; break;
                case "Medical": MedicalRating = rating; break;
                case "Transport": TransportAccessRating = rating; break;
                case "Training": TrainingFacilitiesRating = rating; break;
            }
        }
    }
}
