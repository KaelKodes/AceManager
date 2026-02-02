using Godot;
using System;
using System.Collections.Generic;

namespace AceManager.Core.Strategy
{
    /// <summary>
    /// Represents a dynamic entity on the strategic map.
    /// Acts as a node in the logistics and command graph.
    /// </summary>
    public abstract partial class StrategicNode : Resource
    {
        [Export] public string Id { get; set; }
        [Export] public string Name { get; set; }
        [Export] public string RegionId { get; set; } // "North", "Mid", "South"
        [Export] public Vector2 WorldCoordinates { get; set; }
        [Export] public string OwningNation { get; set; }

        // Status
        [Export] public float CurrentIntegrity { get; set; } = 100f;
        [Export] public float MaxIntegrity { get; set; } = 100f;
        public bool IsDestroyed => CurrentIntegrity <= 0;

        // Intel / Fog of War
        public enum IntelLevel { Unknown, Rumored, Confirmed }
        [Export] public IntelLevel IntelStatus { get; set; } = IntelLevel.Unknown;

        // Logistics Graph
        public StrategicNode ParentNode { get; set; }
        public List<StrategicNode> ChildNodes { get; set; } = new List<StrategicNode>();

        // Supply State
        public bool IsStarved { get; set; } = false; // True if parent is cut off
        public float SupplyLevel { get; set; } = 100f; // 0-100% supply efficiency

        // Dynamic Logistics
        public StrategicNode OriginalParent { get; set; }
        public bool IsRerouted => ParentNode != null && ParentNode != OriginalParent;

        public virtual void TakeDamage(float amount)
        {
            CurrentIntegrity = Math.Max(0, CurrentIntegrity - amount);
            if (IsDestroyed)
            {
                OnDestroyed();
            }
        }

        public virtual void Repair(float amount)
        {
            CurrentIntegrity = Math.Min(MaxIntegrity, CurrentIntegrity + amount);
        }

        protected virtual void OnDestroyed()
        {
            // Logic for when node is disabled (e.g. notify parent/children)
            // Implementation handled by the Simulation Manager
        }
    }
}
