using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using AceManager.Core.Strategy;

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
        public string SelectedNation { get; private set; }

        public RosterManager Roster { get; private set; }
        public MissionData CurrentMission { get; private set; }
        public MissionData LastCompletedMission { get; private set; }
        public DailyBriefing TodaysBriefing { get; private set; }
        public MapData SectorMap { get; private set; }
        public CaptainData PlayerCaptain { get; private set; }
        public bool MissionCompletedToday { get; private set; } = false;

        public UpgradeProject ActiveUpgrade { get; private set; }

        public override void _Ready()
        {
            Instance = this;

            // Create and add RosterManager as child
            Roster = new RosterManager();
            AddChild(Roster);

            // Just load static library data, don't start session yet
            AircraftLibrary = DataLoader.LoadAllAircraft();
        }

        [Signal] public delegate void CampaignIntroRequestedEventHandler(string nation);

        public void StartCampaign(string nation)
        {
            SelectedNation = nation;
            GD.Print($"Campaign start requested for {nation}...");

            // Set historical starting date
            CurrentDate = nation switch
            {
                "Britain" => new DateTime(1914, 8, 13), // RFC deployment to France
                "France" => new DateTime(1914, 8, 3),   // French entry into WWI
                "Germany" => new DateTime(1914, 8, 1),  // Mobilization
                "USA" => new DateTime(1917, 4, 6),      // US Declaration of War
                _ => new DateTime(1914, 8, 1)
            };

            EmitSignal(SignalName.CampaignIntroRequested, nation);
        }

        public void FinalizeCampaignStart(string captainName)
        {
            GD.Print($"Finalizing campaign start for {SelectedNation} with Captain {captainName}...");
            PlayerCaptain = new CaptainData { Name = captainName };
            InitializeSession();
        }

        private void InitializeSession()
        {
            var bases = DataLoader.LoadAirbaseDatabase();

            // Pick starting base by nation
            CurrentBase = bases.FirstOrDefault(b => b.Nation == SelectedNation);

            if (CurrentBase == null)
            {
                GD.PrintErr($"No base found for {SelectedNation}! Falling back to first available.");
                CurrentBase = bases.FirstOrDefault();
            }

            if (CurrentBase != null)
            {
                // Set default basic starting stats
                CurrentBase.CurrentFuel = 1000;
                CurrentBase.CurrentAmmo = 500;
                CurrentBase.CurrentSpareParts = 50;
                CurrentBase.RunwayRating = 3; // Basic grass strip (Level 3)
                CurrentBase.MaintenanceRating = 1;
                CurrentBase.OperationsRating = 1;
                CurrentBase.TrainingFacilitiesRating = 1;
                CurrentBase.LodgingRating = 1; // Level I lodging (8 pilots)
            }

            int startingPilots = CurrentBase?.GetMaxPilotCapacity() ?? 8;
            Roster.GenerateRoster(startingPilots);
            AssignStarterAircraft(SelectedNation);

            // Initialize sector map
            if (CurrentBase != null)
            {
                SectorMap = MapData.GenerateHistoricalMap(CurrentBase);
            }

            EmitSignal(SignalName.DayAdvanced); // Trigger initial UI refresh
        }

        private void AssignStarterAircraft(string nation)
        {
            GD.Print($"Assigning starter aircraft for {nation}. Library size: {AircraftLibrary.Count}");

            // Find a suitable basic aircraft for the nation
            var starterType = AircraftLibrary
                .Where(a => a.Nation == nation
                    && a.YearIntroduced <= CurrentDate.Year
                    && a.RunwayRequirementRange <= CurrentBase.RunwayRating)
                .OrderBy(a => a.CommandPriorityTier) // Pick common/basic types first
                .FirstOrDefault();

            if (starterType == null)
            {
                // Fallback 1: Ignore year, respect runway
                starterType = AircraftLibrary
                    .Where(a => a.Nation == nation && a.RunwayRequirementRange <= CurrentBase.RunwayRating)
                    .FirstOrDefault();

                if (starterType == null)
                {
                    // Fallback 2: Ignore everything, just get any plane for this nation (prevent lock)
                    starterType = AircraftLibrary
                        .Where(a => a.Nation == nation)
                        .FirstOrDefault();

                    if (starterType == null)
                    {
                        GD.PrintErr($"No starter aircraft found for {nation} even with overrides!");
                        return;
                    }
                }
            }

            // Give the player 6 aircraft
            int starterCount = 6;
            for (int i = 0; i < starterCount; i++)
            {
                var instance = AircraftInstance.Create(starterType);
                AircraftInventory.Add(instance);
            }

            GD.Print($"Assigned {starterCount}x {starterType.Name} for starting {nation} squadron.");
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

            // Process pilot recovery and fatigue
            Roster.ProcessDailyRecovery();

            // Process facility upgrades
            if (ActiveUpgrade != null)
            {
                ActiveUpgrade.DaysRemaining--;
                if (ActiveUpgrade.DaysRemaining <= 0)
                {
                    CurrentBase.SetRating(ActiveUpgrade.FacilityName, ActiveUpgrade.TargetLevel);
                    GD.Print($"COMPLETE: {ActiveUpgrade.FacilityName} upgrade to Level {ActiveUpgrade.TargetLevel} finished!");
                    ActiveUpgrade = null;
                }
            }

            MissionCompletedToday = false;

            // --- Strategic Simulation ---
            if (SectorMap != null)
            {
                StrategicSim.ProcessTurn(SectorMap);
            }
            // ----------------------------

            EmitSignal(SignalName.DayAdvanced);
            EmitSignal(SignalName.BriefingReady);
            GD.Print($"Advanced to {CurrentDate.ToShortDateString()}");
        }

        public MissionData CreateMission(MissionType type, int distance, RiskPosture risk, Vector2? manualTarget = null, string targetName = "")
        {
            var mission = new MissionData
            {
                Type = type,
                TargetDistance = distance,
                Risk = risk,
                Status = MissionStatus.Planned,
                TargetName = targetName
            };

            var homeLoc = SectorMap?.Locations.FirstOrDefault(l => l.Id == "home_base");
            Vector2 startPos = homeLoc?.WorldCoordinates ?? Vector2.Zero;

            Vector2 targetPos = manualTarget ?? MapData.GenerateProceduralTarget(startPos, distance, SelectedNation);
            mission.TargetLocation = targetPos;

            // Use the shared waypoint generation logic
            mission.Waypoints = MapData.GenerateWaypoints(startPos, targetPos, distance);

            // Update TargetDistance based on actual distance if manual
            if (manualTarget.HasValue)
            {
                mission.TargetDistance = (int)Math.Max(1, Math.Min(10, (targetPos - startPos).Length() / 10f));
            }

            CurrentMission = mission;
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

        public void CompleteTraining(TrainingSession session)
        {
            if (MissionCompletedToday) return;

            var roster = Roster.Roster;
            var lesson = TrainingLesson.GetAll().Find(l => l.Type == session.LessonType);
            if (lesson == null) return;

            CrewData coInstructor = roster.FirstOrDefault(p => p.Name == session.CoInstructorPilotId);
            float baseXP = session.CalculateBaseXP(CurrentBase.TrainingFacilitiesRating);
            float instructorBonus = session.GetInstructorBonus(coInstructor);

            var attendeeNames = session.AttendeePilotIds.ToHashSet();

            // Create a MissionData object to represent this training for the Debrief UI
            var trainingReport = new MissionData
            {
                Type = MissionType.Training,
                Status = MissionStatus.Resolved,
                ResultBand = MissionResultBand.Success, // Training is always a "success" in this sense
                TargetName = "Training Area",
                MissionLog = new List<string>
                {
                    $"TRAINING REPORT: {CurrentDate.ToShortDateString()}",
                    $"MODULE: {lesson.Name.ToUpper()}",
                    $"--------------------------------------------------",
                    $"INSTRUCTOR: {(coInstructor != null ? coInstructor.Name : "Squadron Leader")}",
                    $"FOCUS: {string.Join(", ", lesson.PrimaryStats)}",
                    $"",
                    $"ATTENDEES:"
                }
            };

            foreach (var pilot in roster)
            {
                if (pilot.Status == PilotStatus.KIA) continue;

                if (attendeeNames.Contains(pilot.Name))
                {
                    // Attendees get XP
                    foreach (var stat in lesson.PrimaryStats)
                    {
                        pilot.AddImprovement(stat, baseXP * instructorBonus);
                    }
                    // Small fatigue recovery bonus (they are working, but it's safe)
                    pilot.Fatigue = Math.Max(0, pilot.Fatigue - 5);

                    trainingReport.MissionLog.Add($"- {pilot.Name}: COMPLETED");

                    // Add to assignments so we get popups in debrief
                    // We bypass AddAssignment validation since there's no aircraft
                    trainingReport.Assignments.Add(new FlightAssignment { Pilot = pilot });
                }
                else
                {
                    // Skippers get NO XP but MORE fatigue recovery
                    pilot.Fatigue = Math.Max(0, pilot.Fatigue - 15);
                }

                pilot.ApplyDailyImprovements();
            }

            trainingReport.MissionLog.Add("");
            trainingReport.MissionLog.Add("SESSION CONCLUDED.");

            MissionCompletedToday = true;
            LastCompletedMission = trainingReport; // Set this so StatusPanel picks it up
            EmitSignal(SignalName.MissionCompleted); // Trigger UI refresh (buttons etc)
            GD.Print($"Training session [{lesson.Name}] complete.");
        }

        private void ProcessMissionResults()
        {
            // Discover target location if successful
            var targetLoc = SectorMap?.Locations.FirstOrDefault(l =>
                (l.Name == CurrentMission.TargetName && !string.IsNullOrEmpty(l.Name)) ||
                (l.WorldCoordinates - CurrentMission.TargetLocation).Length() < 1.0f);

            if (targetLoc != null && !targetLoc.IsDiscovered)
            {
                // We'll mark it discovered if the mission wasn't an absolute failure
                if (CurrentMission.ResultBand >= MissionResultBand.Stalemate)
                {
                    targetLoc.IsDiscovered = true;
                    targetLoc.DiscoveredDate = CurrentDate;
                    GD.Print($"INTEL: Location {targetLoc.Name} permanently mapped after mission.");
                }
            }

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

        public void ScrapAircraft(AircraftInstance aircraft)
        {
            if (aircraft == null) return;

            // Calculate parts reward: base value (20) + 30% of remaining condition
            int reward = 20 + (int)(aircraft.Condition * 0.3f);

            if (CurrentBase != null)
            {
                CurrentBase.CurrentSpareParts += reward;
                GD.Print($"Scrapped {aircraft.GetDisplayName()} for {reward} spare parts.");
            }

            AircraftInventory.Remove(aircraft);
            EmitSignal(SignalName.DayAdvanced); // Force UI refresh
        }

        public void StartUpgrade(string facilityName)
        {
            if (ActiveUpgrade != null)
            {
                GD.Print("Already have an active upgrade project.");
                return;
            }

            int currentLevel = CurrentBase.GetRating(facilityName);
            if (currentLevel >= 5)
            {
                GD.Print("Facility already at maximum level.");
                return;
            }

            var upgrade = UpgradeProject.Create(facilityName, currentLevel + 1);

            if (PlayerCaptain.Merit < upgrade.MeritCost)
            {
                GD.Print("Insufficient Merit to start upgrade.");
                return;
            }

            PlayerCaptain.Merit -= upgrade.MeritCost;
            ActiveUpgrade = upgrade;
            GD.Print($"Started upgrade for {facilityName} to Level {upgrade.TargetLevel}. Cost: {upgrade.MeritCost} Merit.");
            EmitSignal(SignalName.DayAdvanced);
        }

        public void RepairAircraft(AircraftInstance aircraft)
        {
            if (aircraft == null) return;
            if (aircraft.Condition >= 100) return;

            aircraft.StartRepair();
            EmitSignal(SignalName.DayAdvanced);
        }

        public void ConductTraining(List<CrewData> trainees, CrewData instructor)
        {
            if (trainees == null || trainees.Count == 0 || instructor == null) return;

            GD.Print($"Conducting training session with {instructor.Name} and {trainees.Count} trainees.");

            foreach (var pilot in trainees)
            {
                // Small boost to core stats
                pilot.AddImprovement("OA", 2.0f);
                pilot.AddImprovement("CTL", 1.5f);
                pilot.ApplyDailyImprovements();
            }

            // Instructor gets fatigued
            instructor.Fatigue = Math.Min(100, instructor.Fatigue + 15);

            MissionCompletedToday = true; // Training counts as today's "mission"
            EmitSignal(SignalName.MissionCompleted); // Refresh UI buttons
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
