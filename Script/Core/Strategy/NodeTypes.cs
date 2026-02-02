using Godot;
using System;

namespace AceManager.Core.Strategy
{
    public enum IndustryType
    {
        Munitions,
        Steel,
        Chemicals,
        Fuel
    }

    public partial class IndustrialNode : StrategicNode
    {
        [Export] public IndustryType ProductionType { get; set; }
        [Export] public float ProductionRate { get; set; } = 10f; // Units per day

        public IndustrialNode()
        {
            MaxIntegrity = 400f;
            CurrentIntegrity = 400f;
        }
    }

    public partial class LogisticsNode : StrategicNode
    {
        [Export] public float ThroughputCapacity { get; set; } = 100f;

        // Dynamic workload from rerouted connections
        public float CurrentWorkload { get; set; } = 0f;

        // Rail Hubs, Ports, Supply Depots
        public bool IsRailHub { get; set; } // If true, losing this kills child supply

        public LogisticsNode()
        {
            MaxIntegrity = 250f;
            CurrentIntegrity = 250f;
        }
    }

    public partial class MilitaryNode : StrategicNode
    {
        [Export] public float Readiness { get; set; } = 100f; // Combat efficiency
        [Export] public float ResourceConsumption { get; set; } = 5f; // Daily upkeep

        // The specific order assigned by the SubCommander for today
        public MissionData CurrentOrder { get; set; }

        // Airfields, Infantry Bases
    }

    public partial class InfantryBase : MilitaryNode
    {
        [Export] public float TroopMorale { get; set; } = 100f;
        [Export] public int GroundStrength { get; set; } = 1000;

        // Assigned frontline segment to push
        [Export] public int AssignedFrontlineSegmentId { get; set; }
    }

    public partial class RegionLabelNode : StrategicNode
    {
        // Decorative node for geographical labels (France, Belgium, etc.)
    }
}
