using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AceManager.Core
{
    public enum MissionType
    {
        Patrol,
        Interception,
        Escort,
        Reconnaissance,
        Bombing,
        Strafing,  // Ground attack - trenches, convoys, artillery
        Training   // Skill improvement session
    }

    public enum RiskPosture
    {
        Conservative,
        Standard,
        Aggressive
    }

    public enum MissionStatus
    {
        Planned,
        Active,
        Resolved,
        Aborted
    }

    public enum MissionResultBand
    {
        DecisiveSuccess,
        Success,
        MarginalSuccess,
        Stalemate,
        MarginalFailure,
        Failure,
        Disaster
    }

    public partial class MissionData : Resource
    {
        [Export] public MissionType Type { get; set; } = MissionType.Patrol;
        [Export] public int TargetDistance { get; set; } = 1; // 1-10 scale
        [Export] public RiskPosture Risk { get; set; } = RiskPosture.Standard;
        [Export] public MissionStatus Status { get; set; } = MissionStatus.Planned;
        [Export] public Vector2 TargetLocation { get; set; }
        [Export] public string TargetName { get; set; }

        // Escort Mission Support
        public Vector2? RendezvousPoint { get; set; }
        public Vector2? DisengagePoint { get; set; }

        // Metadata for AI-assigned orders
        public string CommanderOrderContext { get; set; }

        // Flight segments / path
        public List<Vector2> Waypoints { get; set; } = new List<Vector2>();

        // Flight assignments (replaces old AssignedAircraft/AssignedPilots)
        public List<FlightAssignment> Assignments { get; set; } = new List<FlightAssignment>();

        // Legacy properties for compatibility during transition
        [Obsolete("Use Assignments instead")]
        public List<AircraftData> AssignedAircraft
        {
            get => Assignments.Where(a => a.Aircraft != null).Select(a => a.Aircraft.Definition).ToList();
            set { } // No-op for compatibility
        }

        [Obsolete("Use Assignments instead")]
        public List<CrewData> AssignedPilots
        {
            get => Assignments.Where(a => a.Pilot != null).Select(a => a.Pilot).ToList();
            set { } // No-op for compatibility
        }

        // Results (populated after resolution)
        public MissionResultBand ResultBand { get; set; }
        public List<string> MissionLog { get; set; } = new List<string>();
        public int FuelConsumed { get; set; }
        public int AmmoConsumed { get; set; }
        public int AircraftLost { get; set; }
        public int CrewWounded { get; set; }
        public int CrewKilled { get; set; }
        public int EnemyKills { get; set; }

        // Order compliance tracking
        public bool FollowedOrders { get; set; } = true;
        public int OrderBonus { get; set; } = 0;  // +/- prestige from order compliance
        public string OrderComplianceMessage { get; set; } = "";



        public void AddAssignment(FlightAssignment assignment)
        {
            if (assignment.IsValid())
            {
                Assignments.Add(assignment);
            }
        }

        public int GetFlightCount()
        {
            return Assignments.Count;
        }

        public int GetTotalCrewCount()
        {
            return Assignments.Sum(a => a.GetCrewCount());
        }

        // Mission duration estimates (in minutes, for flavor)
        public int GetEstimatedDuration()
        {
            // Base time (takeoff/landing/form-up) + Travel Time (approx 150km/h = 2.5km/min) + Loiter
            return Type switch
            {
                MissionType.Patrol => 30 + (int)(TargetDistance * 0.5f),
                MissionType.Interception => 20 + (int)(TargetDistance * 0.4f),
                MissionType.Escort => 30 + (int)(TargetDistance * 0.6f),
                MissionType.Reconnaissance => 40 + (int)(TargetDistance * 0.8f),
                MissionType.Bombing => 60 + (int)(TargetDistance * 1.0f), // Heavy load, slower
                MissionType.Strafing => 45 + (int)(TargetDistance * 0.7f),
                _ => 60
            };
        }

        // Calculate base fuel cost for the mission
        public int GetBaseFuelCost(float efficiencyModifier = 0f)
        {
            int baseCost = 0;
            foreach (var assignment in Assignments)
            {
                if (assignment.Aircraft?.Definition != null)
                {
                    // Fuel consumption scaled for KM (previously Distance * 5 where Dist was 1-10)
                    // New Dist is 10-150. Old Max was 10*5 = 50 factor. New Max 150*0.33 = 50 factor.
                    baseCost += (int)(assignment.Aircraft.Definition.FuelConsumptionRange * TargetDistance * 0.5f);
                }
            }
            // Apply efficiency reduction (clamped 0-1)
            baseCost = (int)(baseCost * (1.0f - Math.Clamp(efficiencyModifier, 0f, 0.9f)));

            return Math.Max(baseCost, 10); // Minimum fuel cost
        }

        // Calculate base ammo cost (higher for bombing missions)
        public int GetBaseAmmoCost(float efficiencyModifier = 0f)
        {
            int multiplier = Type == MissionType.Bombing ? 3 : 1;
            int baseCost = 0;
            foreach (var assignment in Assignments)
            {
                if (assignment.Aircraft?.Definition != null)
                {
                    baseCost += assignment.Aircraft.Definition.AmmoRange * multiplier * 5;
                }
            }
            // Apply efficiency reduction (clamped 0-1)
            baseCost = (int)(baseCost * (1.0f - Math.Clamp(efficiencyModifier, 0f, 0.9f)));

            return Math.Max(baseCost, 5); // Minimum ammo cost
        }

        public List<CrewData> GetAllCrew()
        {
            var crew = new List<CrewData>();
            foreach (var assignment in Assignments)
            {
                if (assignment.Pilot != null) crew.Add(assignment.Pilot);
                if (assignment.Gunner != null) crew.Add(assignment.Gunner);
                if (assignment.Observer != null) crew.Add(assignment.Observer);
            }
            return crew;
        }
    }
}
