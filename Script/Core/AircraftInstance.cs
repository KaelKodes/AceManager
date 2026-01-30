using Godot;
using System;

namespace AceManager.Core
{
    public enum AircraftStatus
    {
        Ready,
        Assigned,
        Repairing,
        Damaged,
        Lost
    }

    public partial class AircraftInstance : Resource
    {
        // Reference to the aircraft type definition
        public AircraftData Definition { get; set; }

        // Instance-specific properties
        [Export] public string TailNumber { get; set; }
        [Export] public int Condition { get; set; } = 100; // 0-100
        [Export] public int HoursFlown { get; set; } = 0;
        [Export] public int MissionsSurvived { get; set; } = 0;
        [Export] public int Kills { get; set; } = 0;
        public AircraftStatus Status { get; set; } = AircraftStatus.Ready;
        public int RepairDaysRemaining { get; set; } = 0;

        private static int _tailCounter = 100;
        private static Random _rng = new Random();

        public static AircraftInstance Create(AircraftData definition)
        {
            var instance = new AircraftInstance
            {
                Definition = definition,
                TailNumber = GenerateTailNumber(definition.Nation),
                Condition = 85 + _rng.Next(16), // 85-100 for new aircraft
                HoursFlown = 0,
                Status = AircraftStatus.Ready
            };
            return instance;
        }

        private static string GenerateTailNumber(string nation)
        {
            _tailCounter++;
            string prefix = nation switch
            {
                "Britain" => "RFC",
                "France" => "SPA",
                "Germany" => "JG",
                "Austria-Hungary" => "FLK",
                _ => "XX"
            };
            return $"{prefix}-{_tailCounter:D4}";
        }

        public string GetDisplayName()
        {
            return $"{Definition.Name} [{TailNumber}]";
        }

        public string GetStatusDisplay()
        {
            return Status switch
            {
                AircraftStatus.Ready => $"Ready ({Condition}%)",
                AircraftStatus.Assigned => "On Mission",
                AircraftStatus.Repairing => $"Repairing ({RepairDaysRemaining}d)",
                AircraftStatus.Damaged => $"Damaged ({Condition}%)",
                AircraftStatus.Lost => "Lost",
                _ => "Unknown"
            };
        }

        public bool IsAvailable()
        {
            return Status == AircraftStatus.Ready && Condition > 20;
        }

        public int GetCrewSeats()
        {
            // Default to 1 seat; need to check aircraft definition for two-seaters
            return Definition?.CrewSeats ?? 1;
        }

        public void ApplyDamage(int damage)
        {
            Condition = Math.Max(0, Condition - damage);
            
            if (Condition <= 0)
            {
                Status = AircraftStatus.Lost;
            }
            else if (Condition < 50)
            {
                Status = AircraftStatus.Damaged;
            }
        }

        public void StartRepair()
        {
            if (Status == AircraftStatus.Damaged)
            {
                int damageToRepair = 100 - Condition;
                RepairDaysRemaining = Math.Max(1, damageToRepair / 15); // ~15% per day
                Status = AircraftStatus.Repairing;
            }
        }

        public void AdvanceRepair()
        {
            if (Status == AircraftStatus.Repairing)
            {
                RepairDaysRemaining--;
                Condition = Math.Min(100, Condition + 15);
                
                if (RepairDaysRemaining <= 0)
                {
                    Status = AircraftStatus.Ready;
                }
            }
        }

        public void AddFlightTime(int hours)
        {
            HoursFlown += hours;
            // Slight wear from usage
            Condition = Math.Max(0, Condition - (hours / 2));
        }
    }
}
