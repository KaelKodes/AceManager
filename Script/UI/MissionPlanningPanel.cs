using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using AceManager.Core;

namespace AceManager.UI
{
	public partial class MissionPlanningPanel : Control
	{
		private OptionButton _typeOption;
		private HSlider _distanceSlider;
		private Label _distanceValue;
		private OptionButton _riskOption;
		private Label _costPreview;
		private ItemList _pilotList;
		private ItemList _aircraftList;
		private VBoxContainer _assignmentsContainer;
		private Label _flightLeaderLabel;
		private Button _cancelButton;
		private Button _launchButton;
		private Button _trainingButton;

		private List<FlightAssignment> _pendingAssignments = new List<FlightAssignment>();
		private List<AircraftInstance> _availableAircraft = new List<AircraftInstance>();
		private List<CrewData> _availablePilots = new List<CrewData>();
		private FlightAssignment _flightLeader = null;
		private bool _isReady = false;

		// Map elements
		private Control _flightMapArea;
		private Label _targetLabel;
		private Control _targetMarker; // Custom marker for mission target

		// Refactored Components
		private RoutePlanner _routePlanner;
		private MapRenderer _renderer;
		private MapInputController _inputController;

		private enum SelectionMode
		{
			PickAircraft,
			PickPilot,
			PickObserver
		}

		private SelectionMode _currentMode = SelectionMode.PickAircraft;
		private FlightAssignment _tempAssignment = null;

		[Signal] public delegate void PanelClosedEventHandler();
		[Signal] public delegate void TrainingRequestedEventHandler();
		[Signal] public delegate void MissionSettingsChangedEventHandler(Godot.Collections.Array<Vector2> waypoints);

		public override void _Ready()
		{
			_typeOption = GetNode<OptionButton>("%TypeOption");
			_distanceSlider = GetNode<HSlider>("%DistanceSlider");

			// New KM Calibration
			_distanceSlider.MinValue = 10;
			_distanceSlider.MaxValue = 150;
			_distanceSlider.Step = 5;
			_distanceSlider.Value = 30; // Default 30km start

			_distanceValue = GetNode<Label>("%DistanceValue");
			_riskOption = GetNode<OptionButton>("%RiskOption");
			_costPreview = GetNode<Label>("%CostPreview");
			_pilotList = GetNode<ItemList>("%PilotList");
			_aircraftList = GetNode<ItemList>("%AircraftList");
			_assignmentsContainer = GetNode<VBoxContainer>("%AssignmentsContainer");
			_flightLeaderLabel = GetNode<Label>("%FlightLeaderLabel");
			_cancelButton = GetNode<Button>("%CancelButton");
			_launchButton = GetNode<Button>("%LaunchButton");
			_trainingButton = GetNode<Button>("%TrainingButton");
			_flightMapArea = GetNode<Control>("%FlightMapArea");

			// Initialize Helpers
			_routePlanner = new RoutePlanner();

			// Inject Home Location
			if (GameManager.Instance != null && GameManager.Instance.SectorMap != null)
			{
				var homeLoc = GameManager.Instance.SectorMap.Locations.FirstOrDefault(l => l.Id == "home_base");
				if (homeLoc != null)
				{
					_routePlanner.HomeWorldPos = homeLoc.WorldCoordinates;
				}
			}

			_routePlanner.OnPathUpdated += OnPathUpdated;

			SetupMapNodes();
			SetupOptions();
			ConnectSignals();

			_pilotList.TooltipText = "Right-click to view Pilot Details";
			_aircraftList.TooltipText = "Right-click to view Aircraft Details";

			_isReady = true;
			RefreshData();
		}

		private void SetupMapNodes()
		{
			// Clear any placeholders defined in the scene (e.g. the black ColorRect)
			foreach (Node child in _flightMapArea.GetChildren())
			{
				child.QueueFree();
			}

			// Initialize Renderer
			_renderer = new MapRenderer(_flightMapArea);
			// Default scale for Briefing Map (might be overridden)
			_renderer.MapScale = 6f;

			// Set bounds (copied from CommandMapPanel or MapData - reusing unified values)
			float lonScale = 71f;
			float latScale = 111f;
			_renderer.WorldMinKM = new Vector2(-1.5f * lonScale, -52.2f * latScale);
			_renderer.WorldMaxKM = new Vector2(7.8f * lonScale, -48.0f * latScale);
			_renderer.WorldSizeKM = _renderer.WorldMaxKM - _renderer.WorldMinKM;

			// Initialize Input Controller (Pan/Zoom enabled for better UX)
			_inputController = new MapInputController(
				_flightMapArea,
				(delta) => { _renderer.ViewOffset -= delta; UpdateMapVisuals(); }, // Pan
				(zoom) => { _renderer.MapScale = zoom; UpdateMapVisuals(); },   // Zoom
				OnMapCursorMove, // Cursor
				(active) => { } // SetActive state
			);
			_inputController.SetZoom(6f);

			// Custom Target Marker (independent of renderer's internal markers)
            _targetMarker = new Control { Name = "CalculatedTargetMarker" };
            var dot = new ColorRect
            {
                Size = new Vector2(10, 10),
                Position = new Vector2(-5, -5),
                Color = Colors.Red
            };
            _targetMarker.AddChild(dot);
            _flightMapArea.AddChild(_targetMarker); // Add on top

            _targetLabel = new Label { Text = "TARGET", HorizontalAlignment = HorizontalAlignment.Center };
            _targetLabel.AddThemeFontSizeOverride("font_size", 10);
            _flightMapArea.AddChild(_targetLabel);



            // Enable clipping to prevent map from drawing over other UI elements
            _flightMapArea.ClipContents = true;

            // Hook input for clicking (still needed for setting target manualy)
            _flightMapArea.GuiInput += OnMapInput;
            _flightMapArea.Resized += () =>
            {
                // Rebuild to show markers if initial size was 0
                _renderer?.RebuildMap();
                CallDeferred(nameof(UpdateMapVisuals));
            };
        }

        private void OnMapCursorMove(Vector2 localPos)
        {
            // Optional: Show coordinate tooltip?
        }

        private void SetupOptions()
        {
            foreach (MissionType type in Enum.GetValues(typeof(MissionType)))
            {
                _typeOption.AddItem(type.ToString());
            }
            _typeOption.Selected = 0;

            foreach (RiskPosture risk in Enum.GetValues(typeof(RiskPosture)))
            {
                _riskOption.AddItem(risk.ToString());
            }
            _riskOption.Selected = 1;
        }

        private void RefreshData()
        {
            if (!_isReady) return;

            var gm = GameManager.Instance;
            if (gm == null)
            {
                GD.PrintErr("GameManager not initialized!");
                return;
            }

            _availableAircraft = gm.GetAvailableAircraft();
            _availablePilots = gm.Roster.GetAvailablePilots();
            _pendingAssignments.Clear();
            _flightLeader = null;

            // Setup Map Data
            if (gm.SectorMap != null)
            {
                var hLoc = gm.SectorMap.Locations.FirstOrDefault(l => l.Id == "home_base");
                Vector2 homeCoords = hLoc?.WorldCoordinates ?? Vector2.Zero;

                _routePlanner.HomeWorldPos = homeCoords;
                _renderer.HomeWorldPos = homeCoords; // Important: center view on home
                _renderer.SetMapData(gm.SectorMap);
                GD.Print($"MissionPlanningPanel: Loaded map with {gm.SectorMap.Locations.Count} locations and {gm.SectorMap.FrontLinePoints?.Length ?? 0} frontline points.");
            }

            // Reset selection flow
            _currentMode = SelectionMode.PickAircraft;
            _tempAssignment = null;

            GD.Print($"MissionPlanningPanel: Found {_availableAircraft.Count} aircraft, {_availablePilots.Count} pilots");

            RefreshLists();

            // --- AI Integration: Load assigned mission details ---
            var assigned = gm.GetAssignedMission();
            if (assigned != null)
            {
                GD.Print($"MissionPlanningPanel: Loading AI-assigned {assigned.Type} mission against {assigned.TargetName}");

                // 1. Set Mission Type
                for (int i = 0; i < _typeOption.ItemCount; i++)
                {
                    if (_typeOption.GetItemText(i) == assigned.Type.ToString())
                    {
                        _typeOption.Selected = i;
                        break;
                    }
                }

                // 2. Set Target in RoutePlanner (must happen before slider update to avoid overriding ManualTargetPos)
                _routePlanner.SetTarget(assigned.TargetLocation, assigned.TargetName);

                // 3. Set Distance Slider (after SetTarget since SetTarget might clamp distance differently)
                _distanceSlider.Value = assigned.TargetDistance;
                int miles = (int)(assigned.TargetDistance * 0.621371f);
                _distanceValue.Text = $"{assigned.TargetDistance} km ({miles} mi)";
            }
            else if (gm.TodaysBriefing != null)
            {
                // Fallback to old recommendation logic if no specific mission is assigned
                var recommendedTypes = gm.TodaysBriefing.GetMatchingMissionTypes();
                if (recommendedTypes.Count > 0)
                {
                    var recommended = recommendedTypes[0];
                    for (int i = 0; i < _typeOption.ItemCount; i++)
                    {
                        if (_typeOption.GetItemText(i) == recommended.ToString())
                        {
                            _typeOption.Selected = i;
                            break;
                        }
                    }
                }
            }

            // Initial path refresh
            _routePlanner.RefreshPath();
        }

        public override void _Notification(int what)
        {
            if (what == NotificationVisibilityChanged && Visible && _isReady)
            {
                RefreshData();
            }
        }

        private void PopulatePilotList()
        {
            if (_pilotList == null) return;

            _pilotList.Clear();

            // Get pilots already assigned
            var assignedPilots = _pendingAssignments.Select(a => a.Pilot).ToHashSet();
            var assignedGunners = _pendingAssignments.Where(a => a.Gunner != null).Select(a => a.Gunner).ToHashSet();
            var assignedObservers = _pendingAssignments.Where(a => a.Observer != null).Select(a => a.Observer).ToHashSet();

            // Also exclude pilot of current temp assignment
            if (_tempAssignment?.Pilot != null) assignedPilots.Add(_tempAssignment.Pilot);
            if (_tempAssignment?.Gunner != null) assignedGunners.Add(_tempAssignment.Gunner);
            if (_tempAssignment?.Observer != null) assignedObservers.Add(_tempAssignment.Observer);

            var available = _availablePilots
                .Where(p => !assignedPilots.Contains(p) && !assignedGunners.Contains(p) && !assignedObservers.Contains(p))
                .OrderByDescending(p => GetPilotMissionScore(p))
                .ToList();

            foreach (var pilot in available)
            {
                string stats = GetPilotListStats(pilot);
                string info = $"{pilot.Name} {stats}";
                _pilotList.AddItem(info);
                _pilotList.SetItemMetadata(_pilotList.ItemCount - 1, pilot.Name);
            }
        }

        private float GetPilotMissionScore(CrewData pilot)
        {
            var type = (MissionType)_typeOption.Selected;

            if (_currentMode == SelectionMode.PickPilot)
            {
                return type switch
                {
                    MissionType.Patrol or MissionType.Interception or MissionType.Escort =>
                        pilot.GUN + pilot.CTL + pilot.OA,
                    MissionType.Reconnaissance =>
                        pilot.CTL + pilot.DA + pilot.STA,
                    MissionType.Bombing or MissionType.Strafing =>
                        pilot.GUN + pilot.CTL + pilot.DIS,
                    _ => pilot.GetDogfightRating()
                };
            }
            else if (_currentMode == SelectionMode.PickObserver)
            {
                return type switch
                {
                    MissionType.Patrol or MissionType.Interception or MissionType.Escort =>
                        pilot.GUN + pilot.DA + pilot.RFX,
                    MissionType.Reconnaissance =>
                        pilot.OA + pilot.TA + pilot.DIS,
                    MissionType.Bombing or MissionType.Strafing =>
                        pilot.OA + pilot.DIS + pilot.TA,
                    _ => pilot.GUN
                };
            }
            return 0;
        }

        private string GetPilotListStats(CrewData pilot)
        {
            var type = (MissionType)_typeOption.Selected;

            if (_currentMode == SelectionMode.PickPilot)
            {
                return type switch
                {
                    MissionType.Patrol or MissionType.Interception or MissionType.Escort =>
                        $"(GUN:{pilot.GUN} CTL:{pilot.CTL} OA:{pilot.OA})",
                    MissionType.Reconnaissance =>
                        $"(CTL:{pilot.CTL} DA:{pilot.DA} STA:{pilot.STA})",
                    MissionType.Bombing or MissionType.Strafing =>
                        $"(GUN:{pilot.GUN} CTL:{pilot.CTL} DIS:{pilot.DIS})",
                    _ => $"(DF:{pilot.GetDogfightRating():F0})"
                };
            }
            else if (_currentMode == SelectionMode.PickObserver)
            {
                return type switch
                {
                    MissionType.Patrol or MissionType.Interception or MissionType.Escort =>
                        $"(GUN:{pilot.GUN} DA:{pilot.DA} RFX:{pilot.RFX})",
                    MissionType.Reconnaissance =>
                        $"(OA:{pilot.OA} TA:{pilot.TA} DIS:{pilot.DIS})",
                    MissionType.Bombing or MissionType.Strafing =>
                        $"(OA:{pilot.OA} DIS:{pilot.DIS} TA:{pilot.TA})",
                    _ => $"(GUN:{pilot.GUN})"
                };
            }

            return "";
        }

        private void PopulateAircraftList()
        {
            if (_aircraftList == null) return;

            _aircraftList.Clear();

            // Get aircraft already assigned
            var assignedAircraft = _pendingAssignments.Select(a => a.Aircraft).ToHashSet();

            bool isGrounded = GameManager.Instance.TodaysBriefing?.IsFlightGrounded() ?? false;
            foreach (var aircraft in _availableAircraft)
            {
                if (assignedAircraft.Contains(aircraft)) continue;
				if (isGrounded) continue; // Don't show aircraft if training

				string seats = aircraft.GetCrewSeats() >= 2 ? " [2-Seater]" : "";
				string condition = $"{aircraft.Condition}%";
				string info = $"{aircraft.GetDisplayName()} ({condition}){seats}";
				_aircraftList.AddItem(info);
				_aircraftList.SetItemMetadata(_aircraftList.ItemCount - 1, aircraft.TailNumber);
			}
		}

		private void ConnectSignals()
		{
			_distanceSlider.ValueChanged += (value) =>
			{
				int km = (int)value;
				int miles = (int)(km * 0.621371f);
				_distanceValue.Text = $"{km} km ({miles} mi)";

				// Update Planner
				_routePlanner.SetDistance(km);
				// Call OnPathUpdated automatically
			};

			_typeOption.ItemSelected += (index) =>
			{
				RefreshLists();
				_routePlanner.RefreshPath(); // Type might affect generator RNG seed or logic eventually
			};

			_pilotList.ItemSelected += OnPilotSelected;
			_aircraftList.ItemSelected += OnAircraftSelected;

			// Right-click to inspect
			_pilotList.ItemClicked += (index, pos, btn) =>
			{
				if (btn == (long)MouseButton.Right) OnPilotDoubleClicked(index);
			};
			_aircraftList.ItemClicked += (index, pos, btn) =>
			{
				if (btn == (long)MouseButton.Right) OnAircraftDoubleClicked(index);
			};

			_cancelButton.Pressed += OnCancelPressed;
			_launchButton.Pressed += OnLaunchPressed;
			_trainingButton.Pressed += OnTrainingPressed;

			// Optional skip for observer seat
			_flightLeaderLabel.GuiInput += (ev) =>
			{
				if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
				{
					if (_currentMode == SelectionMode.PickObserver)
					{
						GD.Print("Skipping observer seat.");
						FinalizeAssignment();
						RefreshLists();
					}
				}
			};
			_flightLeaderLabel.MouseDefaultCursorShape = CursorShape.PointingHand;
		}

		private void OnPathUpdated()
		{
			UpdateMapVisuals();
			UpdateCostPreview();
		}

		private void OnPilotDoubleClicked(long index)
		{
			string pilotName = _pilotList.GetItemMetadata((int)index).AsString();
			var pilot = _availablePilots.FirstOrDefault(p => p.Name == pilotName);
			if (pilot != null) ShowPilotCard(pilot);
		}

		private void OnAircraftDoubleClicked(long index)
		{
			string tailNumber = _aircraftList.GetItemMetadata((int)index).AsString();
			var aircraft = _availableAircraft.FirstOrDefault(a => a.TailNumber == tailNumber);
			if (aircraft != null) ShowAircraftCard(aircraft);
		}

		private PilotCard _pilotCard;
		private AircraftCard _aircraftCard;
		private PackedScene _pilotCardScene;
		private PackedScene _aircraftCardScene;

		private void ShowPilotCard(CrewData pilot)
		{
			if (_pilotCardScene == null)
				_pilotCardScene = GD.Load<PackedScene>("res://Scene/UI/PilotCard.tscn");

			if (_pilotCard == null)
			{
				_pilotCard = _pilotCardScene.Instantiate<PilotCard>();
				AddChild(_pilotCard);
			}
			_pilotCard.ShowPilot(pilot);
		}

		private void ShowAircraftCard(AircraftInstance aircraft)
		{
			if (_aircraftCardScene == null)
				_aircraftCardScene = GD.Load<PackedScene>("res://Scene/UI/AircraftCard.tscn");

			if (_aircraftCard == null)
			{
				_aircraftCard = _aircraftCardScene.Instantiate<AircraftCard>();
				AddChild(_aircraftCard);
			}
			_aircraftCard.ShowAircraft(aircraft);
		}

		private void OnPilotSelected(long index)
		{
			if (_currentMode == SelectionMode.PickAircraft) return;

			string pilotName = _pilotList.GetItemMetadata((int)index).AsString();
			var pilot = _availablePilots.FirstOrDefault(p => p.Name == pilotName);
			if (pilot == null) return;

			if (_currentMode == SelectionMode.PickPilot)
			{
				_tempAssignment.Pilot = pilot;
				if (_tempAssignment.IsTwoSeater())
				{
					_currentMode = SelectionMode.PickObserver;
					GD.Print($"Pilot assigned: {pilot.Name}. Now picking Observer.");
				}
				else
				{
					FinalizeAssignment();
				}
			}
			else if (_currentMode == SelectionMode.PickObserver)
			{
				_tempAssignment.Observer = pilot;
				_tempAssignment.Gunner = pilot; // Assign to both for compatibility for now
				FinalizeAssignment();
			}

			RefreshLists();
		}

		private void OnAircraftSelected(long index)
		{
			if (_currentMode != SelectionMode.PickAircraft) return;

			string tailNumber = _aircraftList.GetItemMetadata((int)index).AsString();
			var aircraft = _availableAircraft.FirstOrDefault(a => a.TailNumber == tailNumber);
			if (aircraft == null) return;

			_tempAssignment = new FlightAssignment(aircraft, null);
			_currentMode = SelectionMode.PickPilot;
			GD.Print($"Aircraft selected: {aircraft.GetDisplayName()}. Now picking Pilot.");

			RefreshLists();
		}

		private void FinalizeAssignment()
		{
			if (_tempAssignment == null || !_tempAssignment.IsValid()) return;

			_pendingAssignments.Add(_tempAssignment);
			if (_flightLeader == null) _flightLeader = _tempAssignment;

			GD.Print($"Assignment finalized: {_tempAssignment.GetDisplayName()}");

			_tempAssignment = null;
			_currentMode = SelectionMode.PickAircraft;
		}

		private void RefreshLists()
		{
			_pilotList.DeselectAll();
			_aircraftList.DeselectAll();
			PopulatePilotList();
			PopulateAircraftList();
			UpdateAssignmentsDisplay();
			// UpdateCostPreview called via OnPathUpdated or manually
			UpdateCostPreview();
			UpdateModeInstructions();
		}

		private void UpdateModeInstructions()
		{
			// Always green per user request
			_flightLeaderLabel.SelfModulate = new Color(0.5f, 1f, 0.5f);

			bool isGrounded = GameManager.Instance.TodaysBriefing?.IsFlightGrounded() ?? false;
			if (_currentMode == SelectionMode.PickAircraft)
			{
				SetListEnabled(_pilotList, isGrounded);
				SetListEnabled(_aircraftList, !isGrounded);

				if (isGrounded)
					_flightLeaderLabel.Text = _pendingAssignments.Count == 0 ? "Select an Instructor to start training" : "Select Trainees or Start Training";
				else
					_flightLeaderLabel.Text = _pendingAssignments.Count == 0 ? "Select an Aircraft to start your flight" : "Select another Aircraft or Launch";
			}
			else if (_currentMode == SelectionMode.PickPilot)
			{
				SetListEnabled(_pilotList, true);
				SetListEnabled(_aircraftList, false);
				_flightLeaderLabel.Text = $"Assign a Pilot for the {(_tempAssignment?.Aircraft?.Definition?.Name ?? "Aircraft")}";
			}
			else if (_currentMode == SelectionMode.PickObserver)
			{
				SetListEnabled(_pilotList, true);
				SetListEnabled(_aircraftList, false);
				_flightLeaderLabel.Text = $"Assign Observer for the {(_tempAssignment?.Aircraft?.Definition?.Name ?? "Aircraft")}";
			}
		}

		private void SetListEnabled(ItemList list, bool enabled)
		{
			list.FocusMode = enabled ? FocusModeEnum.All : FocusModeEnum.None;
			list.MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
			list.Modulate = enabled ? Colors.White : new Color(1, 1, 1, 0.5f);
		}

		private void UpdateAssignmentsDisplay()
		{
			// Clear existing
			foreach (Node child in _assignmentsContainer.GetChildren())
			{
				child.QueueFree();
			}

			// Add assignment buttons
			for (int i = 0; i < _pendingAssignments.Count; i++)
			{
				var assignment = _pendingAssignments[i];
				var hbox = new HBoxContainer();

				bool isLeader = assignment == _flightLeader;
				string leaderMark = isLeader ? "★ " : "   ";

				string crewInfo = assignment.Pilot.Name;
				if (assignment.Gunner != null) crewInfo += $" + {assignment.Gunner.Name}";
				else if (assignment.Observer != null) crewInfo += $" + {assignment.Observer.Name}";

				var button = new Button
				{
					Text = $"{leaderMark}{crewInfo} → {assignment.Aircraft.Definition.Name}",
					SizeFlagsHorizontal = SizeFlags.ExpandFill,
					TooltipText = isLeader ? "Flight Leader! Click another to change." : "Click to set as Flight Leader"
				};

				int idx = i;
				button.Pressed += () => OnAssignmentClicked(idx);

				var removeBtn = new Button
				{
					Text = "X",
					CustomMinimumSize = new Vector2(30, 0),
					TooltipText = "Remove assignment"
				};
				removeBtn.Pressed += () => RemoveAssignment(idx);

				hbox.AddChild(button);
				hbox.AddChild(removeBtn);
				_assignmentsContainer.AddChild(hbox);
			}

			// Update flight leader label
			bool isGrounded = GameManager.Instance.TodaysBriefing?.IsFlightGrounded() ?? false;
			if (_flightLeader != null)
			{
				_flightLeaderLabel.Text = (isGrounded ? "Instructor: " : "Flight Leader: ") + _flightLeader.Pilot.Name;
			}
			else
			{
				_flightLeaderLabel.Text = isGrounded ? "Instructor: (select an assignment)" : "Flight Leader: (select an assignment)";
			}

			_launchButton.Text = isGrounded ? "CONDUCT TRAINING" : "LAUNCH MISSION";
			_trainingButton.Visible = !isGrounded;
		}

		private void OnAssignmentClicked(int index)
		{
			if (index >= 0 && index < _pendingAssignments.Count)
			{
				_flightLeader = _pendingAssignments[index];
				UpdateAssignmentsDisplay();
			}
		}

		private void RemoveAssignment(int index)
		{
			if (index >= 0 && index < _pendingAssignments.Count)
			{
				var removed = _pendingAssignments[index];
				_pendingAssignments.RemoveAt(index);

				if (_flightLeader == removed)
				{
					_flightLeader = _pendingAssignments.Count > 0 ? _pendingAssignments[0] : null;
				}

				RefreshLists();
			}
		}

		private void UpdateCostPreview()
		{
			if (_routePlanner != null)
				UpdateCostPreview(_routePlanner.GetTargetPosition());
		}

		private void UpdateCostPreview(Vector2 targetPos)
		{
			int distance = (int)_distanceSlider.Value;
			var selectedType = (MissionType)_typeOption.Selected;

			// Get Logistics Efficiency from Airbase Operations Center
			float efficiency = 0f;
			if (GameManager.Instance.CurrentBase != null)
			{
				efficiency = GameManager.Instance.CurrentBase.GetEfficiencyBonus();
			}

			int baseFuel = 0;
			int baseAmmo = 0;

			int flightCount = _pendingAssignments.Count;
			int crewCount = _pendingAssignments.Sum(a => a.GetCrewCount());

			foreach (var assignment in _pendingAssignments)
			{
				if (assignment.Aircraft?.Definition != null)
				{
					var def = assignment.Aircraft.Definition;
					// Match MissionData logic: Cons * Dist * 0.5
					baseFuel += (int)(def.FuelConsumptionRange * distance * 0.5f);
					baseAmmo += def.AmmoRange * (selectedType == MissionType.Bombing ? 15 : 5);
				}
			}

			// Apply Efficiency
			baseFuel = (int)(baseFuel * (1.0f - efficiency));
			baseAmmo = (int)(baseAmmo * (1.0f - efficiency));

			// Minima
			if (flightCount > 0)
			{
				baseFuel = Math.Max(baseFuel, 10);
				baseAmmo = Math.Max(baseAmmo, 5);
			}
			else
			{
				// Estimation for no assignments yet (raw guess)
				baseFuel = distance * 50;
				if (efficiency > 0) baseFuel = (int)(baseFuel * (1.0f - efficiency));

				baseAmmo = selectedType == MissionType.Bombing ? 60 : 20;
			}

			string gridRef = GridSystem.WorldToGrid(targetPos, GameManager.Instance.SectorMap);
			string savings = efficiency > 0 ? $" (-{(int)(efficiency * 100)}%)" : "";

			_costPreview.Text = $"Flights: {flightCount} | Crew: {crewCount} | Fuel: ~{baseFuel}{savings} | Ammo: ~{baseAmmo} | Sector: {gridRef}";
		}

		private void OnTrainingPressed()
		{
			EmitSignal(SignalName.TrainingRequested);
			OnCancelPressed();
		}

		private void OnCancelPressed()
		{
			if (_tempAssignment != null)
			{
				_tempAssignment = null;
				_currentMode = SelectionMode.PickAircraft;
				RefreshLists();
				return;
			}

			_pendingAssignments.Clear();
			_flightLeader = null;
			EmitSignal(SignalName.PanelClosed);
			Hide();
		}

		private void OnLaunchPressed()
		{
			if (_pendingAssignments.Count == 0)
			{
				GD.PrintErr("Must assign at least one aircraft with a pilot!");
				return;
			}

			var gm = GameManager.Instance;

			bool isGrounded = gm.TodaysBriefing?.IsFlightGrounded() ?? false;
			if (isGrounded)
			{
				var instructor = _flightLeader.Pilot;
				var trainees = _pendingAssignments.Where(a => a != _flightLeader).Select(a => a.Pilot).ToList();
				gm.ConductTraining(trainees, instructor);
			}
			else
			{
				var missionType = (MissionType)_typeOption.Selected;
				int distance = (int)_distanceSlider.Value;
				var risk = (RiskPosture)_riskOption.Selected;

				gm.CreateMission(missionType, distance, risk, _routePlanner.ManualTargetPos, _routePlanner.TargetName);

				foreach (var assignment in _pendingAssignments)
				{
					gm.AddFlightAssignment(assignment);
				}

				gm.LaunchMission();
			}

			_pendingAssignments.Clear();
			_flightLeader = null;
			EmitSignal(SignalName.PanelClosed);
			Hide();
		}

		private void OnMapInput(InputEvent @event)
		{
			// Handle clicking to set target
			// DISABLED for now per user request - Pan only
			/*
			if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				// ... (existing logic commented out) ...
				Vector2 mapPos = mb.Position;
				Vector2 center = _flightMapArea.Size / 2;
				Vector2 homeWorldPos = _routePlanner.HomeWorldPos;

				Vector2 currentOrigin = homeWorldPos + _renderer.ViewOffset;
				Vector2 worldPos = (mapPos - center) / _renderer.MapScale + currentOrigin;

				// Update Planner
				_routePlanner.SetTarget(worldPos, "Manual Target");

				// Update Slider to match
				_distanceSlider.SetValueNoSignal(_routePlanner.DistanceKM);
				_distanceValue.Text = $"{_routePlanner.DistanceKM} km";
			}
			*/
			// Let InputController handle the rest (pan/zoom)
		}

		private void UpdateMapVisuals()
		{
			if (_renderer == null || _routePlanner == null) return;

			_renderer.UpdateVisuals(_routePlanner.Waypoints);

			if (_targetMarker != null)
			{
				_targetMarker.Position = _renderer.GetVisualPosition(_routePlanner.GetTargetPosition());
				_targetMarker.Visible = true;
				if (_targetLabel != null)
				{
					_targetLabel.Position = _targetMarker.Position + new Vector2(-50, 10);
					_targetLabel.Text = _routePlanner.TargetName.ToUpper();
				}
			}

			// Notify central map
			var godotWaypoints = new Godot.Collections.Array<Vector2>();
			if (_routePlanner.Waypoints != null)
				foreach (var wp in _routePlanner.Waypoints) godotWaypoints.Add(wp);
			EmitSignal(SignalName.MissionSettingsChanged, godotWaypoints);
		}
	}
}
