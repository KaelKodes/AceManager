using Godot;
using System;
using System.Collections.Generic;

namespace AceManager.Core
{
    public partial class SaveData : Resource
    {
        [Export] public string SaveName { get; set; }
        [Export] public string SaveDate { get; set; }
        [Export] public int GameDay { get; set; }
        [Export] public string PlayerNation { get; set; }

        // Core data structures
        [Export] public AirbaseData Airbase { get; set; }
        [Export] public Godot.Collections.Array<AircraftInstance> Inventory { get; set; } = new Godot.Collections.Array<AircraftInstance>();
        [Export] public MapData Map { get; set; }
        [Export] public CaptainData Captain { get; set; }

        // Roster is managed by RosterManager, but we save its pilots here
        [Export] public Godot.Collections.Array<CrewData> RosterPilots { get; set; } = new Godot.Collections.Array<CrewData>();

        public SaveData() { }
    }
}
