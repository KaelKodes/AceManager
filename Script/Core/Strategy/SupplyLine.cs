using Godot;
using System;

namespace AceManager.Core.Strategy
{
    /// <summary>
    /// Represents a logistics connection between two nodes.
    /// Could be a railway line, road network, or supply route.
    /// </summary>
    public partial class SupplyLine : Resource
    {
        [Export] public string FromNodeId { get; set; }
        [Export] public string ToNodeId { get; set; }

        [Export] public bool IsRail { get; set; } // Rail is high cap, Road is low cap
        [Export] public float LengthKM { get; set; }

        // Interdiction status (e.g. bombed bridge)
        [Export] public float Efficiency { get; set; } = 1.0f; // 0.0 to 1.0

        public SupplyLine() { }

        public SupplyLine(string from, string to, float length, bool isRail)
        {
            FromNodeId = from;
            ToNodeId = to;
            LengthKM = length;
            IsRail = isRail;
        }
    }
}
