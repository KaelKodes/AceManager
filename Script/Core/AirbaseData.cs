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
    }
}
