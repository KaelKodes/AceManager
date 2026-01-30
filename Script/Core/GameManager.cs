using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AceManager.Core
{
    public partial class GameManager : Node
    {
        public static GameManager Instance { get; private set; }

        [Signal] public delegate void DayAdvancedEventHandler();
        [Signal] public delegate void MissionCompletedEventHandler();
        [Signal] public delegate void BriefingReadyEventHandler();

        public DateTime CurrentDate { get; private set; } = new DateTime(1917, 2, 8);
        public AirbaseData CurrentBase { get; private set; }
        public List<AircraftData> AircraftLibrary { get; private set; } = new List<AircraftData>();

        // Physical aircraft inventory (replaces AvailableAircraft)
        public List<AircraftInstance> AircraftInventory { get; private set; } = new List<AircraftInstance>();

        public RosterManager Roster { get; private set; }
        public MissionData CurrentMission { get; private set; }
        public MissionData LastCompletedMission { get; private set; }
        public DailyBriefing TodaysBriefing { get; private set; }
        public MapData SectorMap { get; private set; }
        public CaptainData PlayerCaptain { get; private set; }
        public bool MissionCompletedToday { get; private set; } = false;

        public override void _Ready()
        {
            Instance = this;

            // Create and add RosterManager as child
            Roster = new RosterManager();
            AddChild(Roster);

            LoadInitialData();
        }

        private void LoadInitialData()
        {
            AircraftLibrary = DataLoader.LoadAllAircraft();
            var bases = DataLoader.LoadAirbaseDatabase();

            if (bases.Count > 0)
            {
                CurrentBase = bases[0]; // Start at St-Omer by default
                CurrentBase.CurrentFuel = 1000;
                CurrentBase.CurrentAmmo = 500;
                CurrentBase.CurrentSpareParts = 50;
                CurrentBase.RunwayRating = 3;
                CurrentBase.MaintenanceRating = 2;
                CurrentBase.OperationsRating = 2;
                CurrentBase.TrainingFacilitiesRating = 2;
            }

            Roster.GenerateRoster(8);
            AssignStarterAircraft();

            // Initialize sector map with home base
            if (CurrentBase != null)
            {
                SectorMap = MapData.CreateStOmerSector(CurrentBase);
            }

            // Initialize player's commanding officer
            PlayerCaptain = new CaptainData();
        }

        private void AssignStarterAircraft()
        {
            // Find a suitable British aircraft that:
            // 1. Was available by the starting year
            // 2. Can operate from our current runway
            var starterType = AircraftLibrary
                .Where(a => a.Nation == "Britain"
                    && a.YearIntroduced <= CurrentDate.Year
                    && a.RunwayRequirementRange <= CurrentBase.RunwayRating)
                .OrderBy(a => a.CommandPriorityTier) // Lower tier = more common/cheaper
                .ThenBy(a => a.YearIntroduced) // Older = more available
                .FirstOrDefault();

            if (starterType == null)
            {
                // Fallback: just get any British aircraft that fits the runway
                starterType = AircraftLibrary
                    .Where(a => a.Nation == "Britain" && a.RunwayRequirementRange <= CurrentBase.RunwayRating)
                    .FirstOrDefault();

                if (starterType == null)
                {
                    GD.PrintErr("No suitable starter aircraft found for this runway!");
                    return;
                }
            }

            // Give the player 6 of the same aircraft type
            int starterCount = 6;
            for (int i = 0; i < starterCount; i++)
            {
                var instance = AircraftInstance.Create(starterType);
                AircraftInventory.Add(instance);
            }

            GD.Print($"Assigned {starterCount}x {starterType.Name} (Runway Req: {starterType.RunwayRequirementRange}) to squadron inventory.");
        }



        public List<AircraftInstance> GetAvailableAircraft()
        {
            return AircraftInventory.Where(a => a.IsAvailable()).ToList();
        }

        public void AdvanceDay()
        {
            CurrentDate = CurrentDate.AddDays(1);
            // Pass yesterday's mission for briefing context only if it happened today (before advancing)
            var prevMission = MissionCompletedToday ? LastCompletedMission : null;
            TodaysBriefing = DailyBriefing.Generate(CurrentDate, CurrentBase, prevMission);

            // Advance repairs
            foreach (var aircraft in AircraftInventory)
            {
                aircraft.AdvanceRepair();
            }

            MissionCompletedToday = false;

            EmitSignal(SignalName.DayAdvanced);
            EmitSignal(SignalName.BriefingReady);
            GD.Print($"Advanced to {CurrentDate.ToShortDateString()}");
        }

        public MissionData CreateMission(MissionType type, int distance, RiskPosture risk)
        {
            CurrentMission = new MissionData
            {
                Type = type,
                TargetDistance = distance,
                Risk = risk,
                Status = MissionStatus.Planned
            };
            return CurrentMission;
        }

        public void AddFlightAssignment(FlightAssignment assignment)
        {
            if (CurrentMission == null) return;

            if (assignment.IsValid())
            {
                // Mark aircraft as assigned
                if (assignment.Aircraft != null)
                {
                    assignment.Aircraft.Status = AircraftStatus.Assigned;
                }
                CurrentMission.AddAssignment(assignment);
            }
        }

        public void LaunchMission()
        {
            if (MissionCompletedToday)
            {
                GD.PrintErr("Cannot launch mission: Already completed a mission today.");
                return;
            }

            if (CurrentMission == null || CurrentMission.GetFlightCount() == 0)
            {
                GD.PrintErr("Cannot launch mission: No flights assigned.");
                return;
            }

            GD.Print($"Launching mission with {CurrentMission.GetFlightCount()} aircraft...");
            MissionResolver.ResolveMission(CurrentMission, CurrentBase);

            // Handle casualties and aircraft damage
            ProcessMissionResults();

            LastCompletedMission = CurrentMission;
            CurrentMission = null;
            MissionCompletedToday = true;
            EmitSignal(SignalName.MissionCompleted);
        }

        private void ProcessMissionResults()
        {
            foreach (var assignment in CurrentMission.Assignments)
            {
                // Return aircraft to ready (or damaged) status
                if (assignment.Aircraft != null && assignment.Aircraft.Status != AircraftStatus.Lost)
                {
                    assignment.Aircraft.Status = AircraftStatus.Ready;
                    assignment.Aircraft.MissionsSurvived++;
                    assignment.Aircraft.AddFlightTime(CurrentMission.GetEstimatedDuration() / 60);

                    // Check if aircraft was damaged
                    if (CurrentMission.MissionLog.Any(l => l.Contains(assignment.Aircraft.Definition.Name) && l.Contains("lost")))
                    {
                        assignment.Aircraft.Status = AircraftStatus.Lost;
                    }
                }

                // Process crew casualties
                ProcessCrewCasualties(assignment.Pilot);
                ProcessCrewCasualties(assignment.Gunner);
                ProcessCrewCasualties(assignment.Observer);
            }
        }

        private void ProcessCrewCasualties(CrewData crew)
        {
            if (crew == null) return;

            if (CurrentMission.MissionLog.Any(l => l.Contains(crew.Name) && l.Contains("killed")))
            {
                Roster.KillPilot(crew);
            }
            else if (CurrentMission.MissionLog.Any(l => l.Contains(crew.Name) && l.Contains("wounded")))
            {
                Roster.WoundPilot(crew, 3 + new Random().Next(5));
            }
        }
    }
}
