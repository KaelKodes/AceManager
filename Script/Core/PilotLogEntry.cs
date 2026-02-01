using Godot;
using System;

namespace AceManager.Core
{
    public partial class PilotLogEntry : Resource
    {
        [Export] public string Date { get; set; } // Historical date string
        [Export] public string MissionType { get; set; }
        [Export] public string Narrative { get; set; }
        [Export] public int Kills { get; set; }
        [Export] public string Result { get; set; }
        [Export] public bool WasWounded { get; set; }
        [Export] public bool WasShotDown { get; set; }

        public PilotLogEntry() { }

        public PilotLogEntry(string date, string type, string narrative, int kills, string result, bool wounded = false, bool shotDown = false)
        {
            Date = date;
            MissionType = type;
            Narrative = narrative;
            Kills = kills;
            Result = result;
            WasWounded = wounded;
            WasShotDown = shotDown;
        }
    }
}
