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
        Strafing  // Ground attack - trenches, convoys, artillery
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

        // Legacy property
        [Obsolete("Use CrewWounded")]
        public int PilotsWounded { get => CrewWounded; set => CrewWounded = value; }
        [Obsolete("Use CrewKilled")]
        public int PilotsKilled { get => CrewKilled; set => CrewKilled = value; }

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
            return Type switch
            {
                MissionType.Patrol => 30 + (TargetDistance * 5),
                MissionType.Interception => 45 + (TargetDistance * 8),
                MissionType.Escort => 60 + (TargetDistance * 10),
                MissionType.Reconnaissance => 60 + (TargetDistance * 15),
                MissionType.Bombing => 120 + (TargetDistance * 20),
                MissionType.Strafing => 90 + (TargetDistance * 12),
                _ => 60
            };
        }

        // Calculate base fuel cost for the mission
        public int GetBaseFuelCost()
        {
            int baseCost = 0;
            foreach (var assignment in Assignments)
            {
                if (assignment.Aircraft?.Definition != null)
                {
                    baseCost += assignment.Aircraft.Definition.FuelConsumptionRange * TargetDistance * 5;
                }
            }
            return Math.Max(baseCost, 10); // Minimum fuel cost
        }

        // Calculate base ammo cost (higher for bombing missions)
        public int GetBaseAmmoCost()
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
