using Godot;
using System;

namespace AceManager.Core.Strategy
{
    /// <summary>
    /// A dynamic segment of the war front.
    /// Moves based on the 'Push Pressure' exerted by both sides.
    /// </summary>
    public partial class FrontlineSegment : Resource
    {
        [Export] public int SegmentId { get; set; }

        // Coordinates (World KM)
        [Export] public Vector2 StartPoint { get; set; }
        [Export] public Vector2 EndPoint { get; set; }

        // The current "Bulge" or displacement from the historical line
        // Positive = Allied Push (East), Negative = Axis Push (West)
        [Export] public float DisplacementKM { get; set; } = 0f;

        // Current Force Balance
        [Export] public float AlliedPressure { get; set; }
        [Export] public float AxisPressure { get; set; }

        // Regional assignment
        [Export] public string RegionId { get; set; }

        public Vector2 GetCenterPoint()
        {
            Vector2 mid = (StartPoint + EndPoint) / 2f;
            // Apply displacement logic (roughly East/West)
            return mid + new Vector2(DisplacementKM, 0);
        }

        public float GetNetPressure()
        {
            return AlliedPressure - AxisPressure;
        }
    }
}
