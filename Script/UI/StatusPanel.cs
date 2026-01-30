using Godot;
using System;
using AceManager.Core;

namespace AceManager.UI
{
    public partial class StatusPanel : Control
    {
        private Label _dateLabel;
        private Label _baseNameLabel;
        private Label _fuelLabel;
        private Label _ammoLabel;
        private Label _pilotsLabel;
        private Label _aircraftLabel;
        private Label _captainNameLabel;
        private Label _captainRankLabel;
        private Label _captainMeritLabel;
        private GridContainer _ratingsContainer;
        private Button _advanceButton;
        private Button _planMissionButton;
        private Button _viewBriefingButton;
        private Button _viewDebriefButton;
        private Button _viewMapButton;
        private Button _viewRosterButton;

        // Panels (loaded dynamically)
        private Control _missionPlanningPanel;
        private Control _missionResultPanel;
        private Control _briefingPanel;
        private Control _commandMapPanel;
        private Control _rosterPanel;

        private PackedScene _planningPanelScene;
        private PackedScene _resultPanelScene;
        private PackedScene _briefingPanelScene;
        private PackedScene _commandMapScene;
        private PackedScene _rosterPanelScene;
        private PackedScene _infoPopupScene;

        public override void _Ready()
        {
            _dateLabel = GetNode<Label>("%DateLabel");
            _baseNameLabel = GetNode<Label>("%BaseNameLabel");
            _fuelLabel = GetNode<Label>("%FuelLabel");
            _ammoLabel = GetNode<Label>("%AmmoLabel");
            _pilotsLabel = GetNode<Label>("%PilotsLabel");
            _aircraftLabel = GetNode<Label>("%AircraftLabel");
            _captainNameLabel = GetNode<Label>("%CaptainNameLabel");
            _captainRankLabel = GetNode<Label>("%CaptainRankLabel");
            _captainMeritLabel = GetNode<Label>("%CaptainMeritLabel");
            _ratingsContainer = GetNode<GridContainer>("%RatingsContainer");
            _advanceButton = GetNode<Button>("%AdvanceButton");
            _planMissionButton = GetNode<Button>("%PlanMissionButton");
            _viewBriefingButton = GetNode<Button>("%ViewBriefingButton");
            _viewDebriefButton = GetNode<Button>("%ViewDebriefButton");
            _viewMapButton = GetNode<Button>("%ViewMapButton");
            _viewRosterButton = GetNode<Button>("%RosterButton");

            // Load panel scenes
            _planningPanelScene = GD.Load<PackedScene>("res://Scene/UI/MissionPlanningPanel.tscn");
            _resultPanelScene = GD.Load<PackedScene>("res://Scene/UI/MissionResultPanel.tscn");
            _briefingPanelScene = GD.Load<PackedScene>("res://Scene/UI/DailyBriefingPanel.tscn");
            _commandMapScene = GD.Load<PackedScene>("res://Scene/UI/CommandMapPanel.tscn");
            _rosterPanelScene = GD.Load<PackedScene>("res://Scene/UI/RosterPanel.tscn");
            _infoPopupScene = GD.Load<PackedScene>("res://Scene/UI/InfoPopup.tscn");

            // Enable clicks on Captain labels
            _captainNameLabel.MouseFilter = MouseFilterEnum.Stop;
            _captainNameLabel.GuiInput += (eventArgs) => OnCaptainClicked(eventArgs);
            _captainRankLabel.MouseFilter = MouseFilterEnum.Stop;
            _captainRankLabel.GuiInput += (eventArgs) => OnCaptainClicked(eventArgs);

            _briefingPanelScene = GD.Load<PackedScene>("res://Scene/UI/DailyBriefingPanel.tscn");
            _commandMapScene = GD.Load<PackedScene>("res://Scene/UI/CommandMapPanel.tscn");
            _rosterPanelScene = GD.Load<PackedScene>("res://Scene/UI/RosterPanel.tscn");
            _infoPopupScene = GD.Load<PackedScene>("res://Scene/UI/InfoPopup.tscn");

            // Enable clicks on Captain labels
            _captainNameLabel.MouseFilter = MouseFilterEnum.Stop;
            _captainNameLabel.GuiInput += (eventArgs) => OnCaptainClicked(eventArgs);
            _captainRankLabel.MouseFilter = MouseFilterEnum.Stop;
            _captainRankLabel.GuiInput += (eventArgs) => OnCaptainClicked(eventArgs);
            _advanceButton.Pressed += () => GameManager.Instance.AdvanceDay();
            _planMissionButton.Pressed += OnPlanMissionPressed;
            _viewBriefingButton.Pressed += OnViewBriefingPressed;
            _viewDebriefButton.Pressed += OnViewDebriefPressed;
            _viewMapButton.Pressed += OnViewMapPressed;
            _viewRosterButton.Pressed += OnViewRosterPressed;
            GameManager.Instance.DayAdvanced += OnDayAdvanced;
            GameManager.Instance.MissionCompleted += OnMissionCompleted;
            GameManager.Instance.BriefingReady += OnBriefingReady;

            UpdateUI();
            UpdateBriefingButtonStates();
        }

        private void OnDayAdvanced()
        {
            UpdateUI();
            UpdateMissionButtonState();
            UpdateBriefingButtonStates();
        }

        private void OnViewBriefingPressed()
        {
            var briefing = GameManager.Instance.TodaysBriefing;
            if (briefing == null) return;

            if (_briefingPanel == null)
            {
                _briefingPanel = _briefingPanelScene.Instantiate<Control>();
                AddChild(_briefingPanel);
            }

            var panel = _briefingPanel as DailyBriefingPanel;
            panel?.DisplayBriefing(briefing);
        }

        private void OnViewDebriefPressed()
        {
            var mission = GameManager.Instance.LastCompletedMission;
            if (mission == null) return;

            if (_missionResultPanel == null)
            {
                _missionResultPanel = _resultPanelScene.Instantiate<Control>();
                AddChild(_missionResultPanel);
            }

            var resultPanel = _missionResultPanel as MissionResultPanel;
            resultPanel?.DisplayResults(mission);
        }

        private void OnViewMapPressed()
        {
            var mapData = GameManager.Instance.SectorMap;
            if (mapData == null)
            {
                GD.Print("No sector map data available.");
                return;
            }

            if (_commandMapPanel == null)
            {
                _commandMapPanel = _commandMapScene.Instantiate<Control>();
                AddChild(_commandMapPanel);
            }

            var mapPanel = _commandMapPanel as CommandMapPanel;
            mapPanel?.ShowMap(mapData);
        }

        private void OnViewRosterPressed()
        {
            if (_rosterPanel == null)
            {
                _rosterPanel = _rosterPanelScene.Instantiate<Control>();
                AddChild(_rosterPanel);
            }
            _rosterPanel.MoveToFront();
        }

        private void UpdateBriefingButtonStates()
        {
            // Briefing button always available after first day
            _viewBriefingButton.Disabled = GameManager.Instance.TodaysBriefing == null;
            _viewBriefingButton.TooltipText = _viewBriefingButton.Disabled ? "No briefing yet" : "Re-read today's briefing";

            // Debrief button only available after a mission
            _viewDebriefButton.Disabled = GameManager.Instance.LastCompletedMission == null;
            _viewDebriefButton.TooltipText = _viewDebriefButton.Disabled ? "No mission completed yet" : "Re-read mission debrief";
        }


        private void OnBriefingReady()
        {
            var briefing = GameManager.Instance.TodaysBriefing;
            if (briefing == null) return;

            if (_briefingPanel == null)
            {
                _briefingPanel = _briefingPanelScene.Instantiate<Control>();
                AddChild(_briefingPanel);
            }

            var panel = _briefingPanel as DailyBriefingPanel;
            panel?.DisplayBriefing(briefing);
        }

        private void OnPlanMissionPressed()
        {
            var gm = GameManager.Instance;

            // Check if already flew today
            if (gm.MissionCompletedToday)
            {
                GD.Print("Mission already completed today. Advance to next day.");
                return;
            }

            // Check if grounded by weather
            var briefing = gm.TodaysBriefing;
            if (briefing != null && briefing.IsFlightGrounded())
            {
                GD.Print("Cannot launch missions - weather conditions prevent flight.");
                return;
            }

            if (_missionPlanningPanel == null)
            {
                _missionPlanningPanel = _planningPanelScene.Instantiate<Control>();
                AddChild(_missionPlanningPanel);
                _missionPlanningPanel.Connect("PanelClosed", Callable.From(OnPlanningPanelClosed));
            }
            else
            {
                _missionPlanningPanel.Show();
            }
        }


        private void OnPlanningPanelClosed()
        {
            // Panel hides itself
        }

        private void OnMissionCompleted()
        {
            UpdateUI();
            UpdateMissionButtonState();
            UpdateBriefingButtonStates();

            var mission = GameManager.Instance.LastCompletedMission;
            if (mission == null) return;

            if (_missionResultPanel == null)
            {
                _missionResultPanel = _resultPanelScene.Instantiate<Control>();
                AddChild(_missionResultPanel);
            }

            var resultPanel = _missionResultPanel as MissionResultPanel;
            resultPanel?.DisplayResults(mission);
        }

        private void UpdateMissionButtonState()
        {
            var gm = GameManager.Instance;

            bool canFly = true;
            string tooltip = "Plan and launch a mission";

            // Check if already flew today
            if (gm.MissionCompletedToday)
            {
                canFly = false;
                tooltip = "Mission already completed today";
            }
            // Check weather
            else if (gm.TodaysBriefing != null && gm.TodaysBriefing.IsFlightGrounded())
            {
                canFly = false;
                tooltip = "Grounded - weather too severe";
            }

            _planMissionButton.Disabled = !canFly;
            _planMissionButton.TooltipText = tooltip;
        }


        public void UpdateUI()
        {
            var gm = GameManager.Instance;
            _dateLabel.Text = $"Date: {gm.CurrentDate.ToString("MMM d, yyyy")}";

            if (gm.CurrentBase != null)
            {
                _baseNameLabel.Text = $"Base: {gm.CurrentBase.Name} ({ToRoman(gm.CurrentBase.BaseLevel)})";
                _fuelLabel.Text = $"Fuel: {gm.CurrentBase.CurrentFuel:F0}";
                _ammoLabel.Text = $"Ammo: {gm.CurrentBase.CurrentAmmo:F0}";

                UpdateRatings(gm.CurrentBase);
            }

            // Personnel counts
            UpdatePersonnelDisplay(gm);

            // Captain info
            UpdateCaptainDisplay(gm);
        }

        private void UpdatePersonnelDisplay(GameManager gm)
        {
            int available = gm.Roster?.GetAvailablePilots().Count ?? 0;
            int total = gm.Roster?.Roster.Count ?? 0;
            _pilotsLabel.Text = $"Pilots: {available}/{total}";

            // Group aircraft by type
            var aircraftCounts = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var aircraft in gm.AircraftInventory)
            {
                string name = aircraft.Definition.Name;
                if (!aircraftCounts.ContainsKey(name))
                    aircraftCounts[name] = 0;
                aircraftCounts[name]++;
            }

            // Format: "Aircraft: 4x BE.2c, 2x DH.2"
            var parts = new System.Collections.Generic.List<string>();
            foreach (var kvp in aircraftCounts)
            {
                parts.Add($"{kvp.Value}x {kvp.Key}");
            }
            _aircraftLabel.Text = parts.Count > 0 ? string.Join(", ", parts) : "None";
        }

        private void UpdateCaptainDisplay(GameManager gm)
        {
            if (gm.PlayerCaptain != null)
            {
                _captainNameLabel.Text = gm.PlayerCaptain.GetFullTitle();
                _captainRankLabel.Text = $"Rank: {gm.PlayerCaptain.Rank}";
                _captainMeritLabel.Text = $"Merit: {gm.PlayerCaptain.Merit} pts";
            }
        }

        private void UpdateRatings(AirbaseData baseData)
        {
            foreach (Node child in _ratingsContainer.GetChildren())
            {
                child.QueueFree();
            }

            AddRatingRow("Runway", baseData.RunwayRating);
            AddRatingRow("Lodging", baseData.LodgingRating);
            AddRatingRow("Maintenance", baseData.MaintenanceRating);
            AddRatingRow("Fuel Storage", baseData.FuelStorageRating);
            AddRatingRow("Ammo Storage", baseData.AmmunitionStorageRating);
            AddRatingRow("Operations", baseData.OperationsRating);
            AddRatingRow("Medical", baseData.MedicalRating);
            AddRatingRow("Transport", baseData.TransportAccessRating);
            AddRatingRow("Training", baseData.TrainingFacilitiesRating);
        }

        private void AddRatingRow(string name, int rating)
        {
            var nameLabel = new Label { Text = name, MouseFilter = MouseFilterEnum.Stop };
            var ratingLabel = new Label { Text = ToRoman(rating), HorizontalAlignment = HorizontalAlignment.Right, MouseFilter = MouseFilterEnum.Stop };

            // Connect click events
            nameLabel.GuiInput += (args) => OnFacilityClicked(args, name, rating);
            ratingLabel.GuiInput += (args) => OnFacilityClicked(args, name, rating);

            _ratingsContainer.AddChild(nameLabel);
            _ratingsContainer.AddChild(ratingLabel);
        }

        private void OnCaptainClicked(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                var gm = GameManager.Instance;
                if (gm.PlayerCaptain != null)
                {
                    string details = $"Name: {gm.PlayerCaptain.Name}\nRank: {gm.PlayerCaptain.Rank}\nMerit: {gm.PlayerCaptain.Merit}\n\n" +
                                     $"Missions Commanded: {gm.PlayerCaptain.MissionsCommanded}\n" +
                                     $"Victories: {gm.PlayerCaptain.VictoriesUnderCommand}\n" +
                                     $"Losses: {gm.PlayerCaptain.LossesUnderCommand}\n\n" +
                                     $"Next Promotion: {gm.PlayerCaptain.GetMeritToNextRank()} merit needed";
                    ShowInfo("Commanding Officer", details);
                }
            }
        }

        private void OnFacilityClicked(InputEvent @event, string facilityName, int rating)
        {
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                string desc = GetFacilityDescription(facilityName, rating);
                ShowInfo($"{facilityName} Facility (Level {ToRoman(rating)})", desc);
            }
        }

        private void ShowInfo(string title, string content)
        {
            var popup = _infoPopupScene.Instantiate<AceManager.UI.InfoPopup>();
            AddChild(popup);
            popup.ShowInfo(title, content);
        }

        private string GetFacilityDescription(string name, int rating)
        {
            return name switch
            {
                "Runway" => $"Determines maximum aircraft weight and takeoff safety.\n\nCurrent Level ({ToRoman(rating)}): Supports standard fighters and light bombers.",
                "Lodging" => $"Impacts pilot fatigue recovery and morale.\n\nCurrent Level ({ToRoman(rating)}): Basic bunks, minimal comfort.",
                "Maintenance" => $"affects repair speed and aircraft reliability.\n\nCurrent Level ({ToRoman(rating)}): Field workshops, basic tools.",
                "Fuel Storage" => $"Maximum fuel capacity.\n\nCurrent Level ({ToRoman(rating)}): 1000 Gallons.",
                "Ammo Storage" => $"Maximum ammunition capacity.\n\nCurrent Level ({ToRoman(rating)}): 500 Rounds.",
                "Operations" => $"Intelligence quality and mission planning efficiency.\n\nCurrent Level ({ToRoman(rating)}): Basic maps and telephone link.",
                "Medical" => $"Wound recovery speed and survival chance.\n\nCurrent Level ({ToRoman(rating)}): Field dressing station.",
                "Transport" => $"Supply delivery speed and reinforcement arrival.\n\nCurrent Level ({ToRoman(rating)}): Mud roads, occasional trucks.",
                "Training" => $"Experience gain for pilots and quality of replacements.\n\nCurrent Level ({ToRoman(rating)}): Ad-hoc lectures.",
                _ => "Standard base facility."
            };
        }

        private string ToRoman(int number)
        {
            return number switch
            {
                1 => "I",
                2 => "II",
                3 => "III",
                4 => "IV",
                5 => "V",
                _ => number.ToString()
            };
        }
    }
}
