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

		private List<FlightAssignment> _pendingAssignments = new List<FlightAssignment>();
		private List<AircraftInstance> _availableAircraft = new List<AircraftInstance>();
		private List<CrewData> _availablePilots = new List<CrewData>();
		private FlightAssignment _flightLeader = null;
		private bool _isReady = false;

		// Map elements
		private Control _flightMapArea;
		private Control _mapMarkerContainer;
		private Line2D _departureLine;
		private Line2D _returnLine;
		private Label _targetLabel;
		private Vector2? _manualTargetPos = null;
		private string _manualTargetName = "";
		private float _briefingMapScale = 6f; // KM to Pixels
		private Vector2 _mapCenter;

		private enum SelectionMode
		{
			PickAircraft,
			PickPilot,
			PickObserver
		}

		private SelectionMode _currentMode = SelectionMode.PickAircraft;
		private FlightAssignment _tempAssignment = null;

		[Signal] public delegate void PanelClosedEventHandler();
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
			_flightMapArea = GetNode<Control>("%FlightMapArea");

			SetupMapNodes();
			SetupOptions();
			ConnectSignals();
			_pilotList.TooltipText = "Double-click to view Pilot Details";
			_aircraftList.TooltipText = "Double-click to view Aircraft Details";
			_isReady = true;
			RefreshData();
		}

		private void SetupMapNodes()
		{
			_mapMarkerContainer = new Control { Name = "Markers" };
			_flightMapArea.AddChild(_mapMarkerContainer);

			_departureLine = new Line2D { Width = 3, DefaultColor = new Color(0.2f, 1.0f, 0.2f, 0.7f) }; // Green
			_flightMapArea.AddChild(_departureLine);

			_returnLine = new Line2D { Width = 3, DefaultColor = new Color(1.0f, 0.2f, 0.2f, 0.7f) }; // Red
			_flightMapArea.AddChild(_returnLine);

			_targetLabel = new Label { Text = "TARGET", HorizontalAlignment = HorizontalAlignment.Center };
			_targetLabel.AddThemeFontSizeOverride("font_size", 10);
			_flightMapArea.AddChild(_targetLabel);

			_flightMapArea.GuiInput += OnMapInput;
			_flightMapArea.Resized += () => CallDeferred(nameof(UpdateMapPreview));
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
			_manualTargetPos = null;
			_manualTargetName = "";

			// Reset selection flow
			_currentMode = SelectionMode.PickAircraft;
			_tempAssignment = null;

			GD.Print($"MissionPlanningPanel: Found {_availableAircraft.Count} aircraft, {_availablePilots.Count} pilots");

			RefreshLists();
			RefreshLists();

			// Auto-select Command's requested mission type
			if (gm.TodaysBriefing != null)
			{
				var recommendedTypes = gm.TodaysBriefing.GetMatchingMissionTypes();
				if (recommendedTypes.Count > 0)
				{
					var recommended = recommendedTypes[0];
					// Find index in dropdown
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

			UpdateMapPreview();
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

			// Also exclude pilot of current temp assignment
			if (_tempAssignment?.Pilot != null) assignedPilots.Add(_tempAssignment.Pilot);
			if (_tempAssignment?.Gunner != null) assignedGunners.Add(_tempAssignment.Gunner);

			foreach (var pilot in _availablePilots)
			{
				if (assignedPilots.Contains(pilot) || assignedGunners.Contains(pilot)) continue;

				string stats = GetPilotListStats(pilot);
				string info = $"{pilot.Name} {stats}";
				_pilotList.AddItem(info);
				_pilotList.SetItemMetadata(_pilotList.ItemCount - 1, pilot.Name);
			}
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
				UpdateCostPreview();
				_manualTargetPos = null; // Reset manual target if distance changes via slider
				UpdateMapPreview();
			};
			_typeOption.ItemSelected += (index) =>
			{
				RefreshLists();
				UpdateMapPreview();
			};
			_pilotList.ItemSelected += OnPilotSelected;
			_aircraftList.ItemSelected += OnAircraftSelected;

			_pilotList.ItemActivated += OnPilotDoubleClicked;
			_aircraftList.ItemActivated += OnAircraftDoubleClicked;

			_cancelButton.Pressed += OnCancelPressed;
			_launchButton.Pressed += OnLaunchPressed;

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
				_tempAssignment.Observer = pilot; // Or Gunner, depending on logic
												  // For now, let's just use Gunner as the default second seat
				_tempAssignment.Gunner = pilot;
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
			UpdateCostPreview();
			UpdateModeInstructions();
		}

		private void UpdateModeInstructions()
		{
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
				_flightLeaderLabel.Text = $"Assign Observer for the {(_tempAssignment?.Aircraft?.Definition?.Name ?? "Aircraft")} (Optional - Click here to skip)";
				_flightLeaderLabel.SelfModulate = new Color(0.5f, 1f, 0.5f);
			}
			else
			{
				_flightLeaderLabel.SelfModulate = Colors.White;
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
		}

		private void OnAssignmentClicked(int index)
		{
			if (index >= 0 && index < _pendingAssignments.Count)
			{
				_flightLeader = _pendingAssignments[index];
				GD.Print($"Flight leader: {_flightLeader.Pilot.Name}");
				UpdateAssignmentsDisplay();
			}
		}

		private void RemoveAssignment(int index)
		{
			if (index >= 0 && index < _pendingAssignments.Count)
			{
				var removed = _pendingAssignments[index];
				_pendingAssignments.RemoveAt(index);

				// Reset flight leader if removed
				if (_flightLeader == removed)
				{
					_flightLeader = _pendingAssignments.Count > 0 ? _pendingAssignments[0] : null;
				}

				// Refresh lists
				PopulatePilotList();
				PopulateAircraftList();
				UpdateAssignmentsDisplay();
				UpdateCostPreview();
			}
		}

		private void UpdateCostPreview()
		{
			Vector2 homeWorldPos = GetHomeWorldPos();
			Vector2 targetPos = _manualTargetPos ?? MapData.GenerateProceduralTarget(homeWorldPos, (int)_distanceSlider.Value, GameManager.Instance.SelectedNation);
			UpdateCostPreview(targetPos);
		}

		private void UpdateCostPreview(Vector2 targetPos)
		{
			int distance = (int)_distanceSlider.Value;
			var selectedType = (MissionType)_typeOption.Selected;

			int baseFuel = 0;
			int baseAmmo = 0;

			foreach (var assignment in _pendingAssignments)
			{
				if (assignment.Aircraft?.Definition != null)
				{
					var def = assignment.Aircraft.Definition;
					baseFuel += def.FuelConsumptionRange * distance * 5;
					baseAmmo += def.AmmoRange * (selectedType == MissionType.Bombing ? 15 : 5);
				}
			}

			int flightCount = _pendingAssignments.Count;
			int crewCount = _pendingAssignments.Sum(a => a.GetCrewCount());

			if (flightCount == 0)
			{
				baseFuel = distance * 50;
				baseAmmo = selectedType == MissionType.Bombing ? 60 : 20;
			}

			string gridRef = GridSystem.WorldToGrid(targetPos, GameManager.Instance.SectorMap);
			_costPreview.Text = $"Flights: {flightCount} | Crew: {crewCount} | Fuel: ~{baseFuel} | Ammo: ~{baseAmmo} | Sector: {gridRef}";
		}

		private void OnCancelPressed()
		{
			if (_tempAssignment != null)
			{
				GD.Print("Resetting current selection.");
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
				// Training flow
				var instructor = _flightLeader.Pilot;
				var trainees = _pendingAssignments.Where(a => a != _flightLeader).Select(a => a.Pilot).ToList();
				gm.ConductTraining(trainees, instructor);
			}
			else
			{
				var missionType = (MissionType)_typeOption.Selected;
				int distance = (int)_distanceSlider.Value;
				var risk = (RiskPosture)_riskOption.Selected;

				gm.CreateMission(missionType, distance, risk, _manualTargetPos, _manualTargetName);

				foreach (var assignment in _pendingAssignments)
				{
					gm.AddFlightAssignment(assignment);
				}

				if (_flightLeader != null)
				{
					GD.Print($"Mission launched with Flight Leader: {_flightLeader.Pilot.Name}");
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
			if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
			{
				Vector2 mapPos = mb.Position;
				Vector2 homeWorldPos = GetHomeWorldPos();
				Vector2 center = _flightMapArea.Size / 2;

				// Map to World: world = origin + (screen - center) / scale
				Vector2 worldPos = homeWorldPos + (mapPos - center) / _briefingMapScale;

				_manualTargetPos = worldPos;
				_manualTargetName = "Manual Target";

				// Update slider to match distance (World Distance is in KM since scale is 1 unit = 1 KM)
				float distKm = (worldPos - homeWorldPos).Length();
				int distClamped = (int)Math.Clamp(distKm, 10, 150);

				// Snap to step 5
				distClamped = (int)(Math.Round(distClamped / 5.0) * 5);

				_distanceSlider.Value = distClamped;
				// Label updates automatically via ValueChanged signal

				UpdateMapPreview();
			}
		}

		private void UpdateMapPreview()
		{
			if (!_isReady || _flightMapArea == null) return;

			Vector2 areaSize = _flightMapArea.Size;
			if (areaSize.X < 10) return;
			Vector2 center = areaSize / 2;

			Vector2 homeWorldPos = GetHomeWorldPos();
			Vector2 targetPos = _manualTargetPos ?? MapData.GenerateProceduralTarget(homeWorldPos, (int)_distanceSlider.Value, GameManager.Instance.SelectedNation);

			var waypoints = MapData.GenerateWaypoints(homeWorldPos, targetPos, (int)_distanceSlider.Value);

			_departureLine.ClearPoints();
			_returnLine.ClearPoints();

			// Departure: Start to segment before last
			for (int i = 0; i < waypoints.Count - 1; i++)
			{
				_departureLine.AddPoint(center + (waypoints[i] - homeWorldPos) * _briefingMapScale);
			}

			// Return: Last segment back to home
			if (waypoints.Count >= 2)
			{
				_returnLine.AddPoint(center + (waypoints[waypoints.Count - 2] - homeWorldPos) * _briefingMapScale);
				_returnLine.AddPoint(center + (waypoints[waypoints.Count - 1] - homeWorldPos) * _briefingMapScale);
			}

			UpdateMapMarkers(homeWorldPos, targetPos, center);

			// Notify central map
			var godotWaypoints = new Godot.Collections.Array<Vector2>();
			foreach (var wp in waypoints) godotWaypoints.Add(wp);
			EmitSignal(SignalName.MissionSettingsChanged, godotWaypoints);
		}

		private void UpdateMapMarkers(Vector2 homeWorldPos, Vector2 targetPos, Vector2 center)
		{
			foreach (Node child in _mapMarkerContainer.GetChildren()) child.QueueFree();

			var homeMarker = CreateMapMarker("HOME", Colors.Green);
			homeMarker.Position = center;
			_mapMarkerContainer.AddChild(homeMarker);

			var targetMarker = CreateMapMarker("TARGET", Colors.Red);
			targetMarker.Position = center + (targetPos - homeWorldPos) * _briefingMapScale;
			_mapMarkerContainer.AddChild(targetMarker);

			_targetLabel.Text = _manualTargetName != "" ? _manualTargetName : "PRIMARY OBJECTIVE";
			_targetLabel.Position = targetMarker.Position + new Vector2(-50, 10);
			_targetLabel.Size = new Vector2(100, 20);

			if (GameManager.Instance.SectorMap != null)
			{
				foreach (var loc in GameManager.Instance.SectorMap.GetDiscoveredLocations())
				{
					if (loc.Id == "home_base") continue;

					Vector2 screenPos = center + (loc.WorldCoordinates - homeWorldPos) * _briefingMapScale;
					if (screenPos.X > 0 && screenPos.X < _flightMapArea.Size.X && screenPos.Y > 0 && screenPos.Y < _flightMapArea.Size.Y)
					{
						var marker = CreateMapMarker(loc.Name, Colors.LightBlue, 6);
						marker.Position = screenPos;
						_mapMarkerContainer.AddChild(marker);
					}
				}
			}
		}

		private Control CreateMapMarker(string labelText, Color color, int size = 10)
		{
			var marker = new Control();
			var dot = new ColorRect
			{
				Size = new Vector2(size, size),
				Position = new Vector2(-size / 2, -size / 2),
				Color = color
			};
			marker.AddChild(dot);

			if (!string.IsNullOrEmpty(labelText))
			{
				var lbl = new Label
				{
					Text = labelText,
					Position = new Vector2(size, -size),
					Size = new Vector2(100, 20)
				};
				lbl.AddThemeFontSizeOverride("font_size", 8);
				marker.AddChild(lbl);
			}
			return marker;
		}

		private Vector2 GetHomeWorldPos()
		{
			var homeLoc = GameManager.Instance.SectorMap?.Locations.FirstOrDefault(l => l.Id == "home_base");
			return homeLoc?.WorldCoordinates ?? Vector2.Zero;
		}
	}
}
