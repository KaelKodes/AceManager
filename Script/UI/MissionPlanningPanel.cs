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

		private enum SelectionMode
		{
			PickAircraft,
			PickPilot,
			PickObserver
		}

		private SelectionMode _currentMode = SelectionMode.PickAircraft;
		private FlightAssignment _tempAssignment = null;

		[Signal] public delegate void PanelClosedEventHandler();

		public override void _Ready()
		{
			_typeOption = GetNode<OptionButton>("%TypeOption");
			_distanceSlider = GetNode<HSlider>("%DistanceSlider");
			_distanceValue = GetNode<Label>("%DistanceValue");
			_riskOption = GetNode<OptionButton>("%RiskOption");
			_costPreview = GetNode<Label>("%CostPreview");
			_pilotList = GetNode<ItemList>("%PilotList");
			_aircraftList = GetNode<ItemList>("%AircraftList");
			_assignmentsContainer = GetNode<VBoxContainer>("%AssignmentsContainer");
			_flightLeaderLabel = GetNode<Label>("%FlightLeaderLabel");
			_cancelButton = GetNode<Button>("%CancelButton");
			_launchButton = GetNode<Button>("%LaunchButton");

			SetupOptions();
			ConnectSignals();
			_isReady = true;
			RefreshData();
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

			// Reset selection flow
			_currentMode = SelectionMode.PickAircraft;
			_tempAssignment = null;

			GD.Print($"MissionPlanningPanel: Found {_availableAircraft.Count} aircraft, {_availablePilots.Count} pilots");

			RefreshLists();
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

				string info = $"{pilot.Name} (DF: {pilot.GetDogfightRating():F0})";
				_pilotList.AddItem(info);
				_pilotList.SetItemMetadata(_pilotList.ItemCount - 1, pilot.Name);
			}
		}

		private void PopulateAircraftList()
		{
			if (_aircraftList == null) return;

			_aircraftList.Clear();

			// Get aircraft already assigned
			var assignedAircraft = _pendingAssignments.Select(a => a.Aircraft).ToHashSet();

			foreach (var aircraft in _availableAircraft)
			{
				if (assignedAircraft.Contains(aircraft)) continue;

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
				_distanceValue.Text = ((int)value).ToString();
				UpdateCostPreview();
			};
			_typeOption.ItemSelected += (index) => UpdateCostPreview();
			_pilotList.ItemSelected += OnPilotSelected;
			_aircraftList.ItemSelected += OnAircraftSelected;

			// Use the same lists but filter/disable based on mode
			_pilotList.ItemActivated += (idx) => OnPilotSelected(idx);
			_aircraftList.ItemActivated += (idx) => OnAircraftSelected(idx);

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
			if (_currentMode == SelectionMode.PickAircraft)
			{
				SetListEnabled(_pilotList, false);
				SetListEnabled(_aircraftList, true);
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

				var button = new Button
				{
					Text = $"{leaderMark}{assignment.Pilot.Name} → {assignment.Aircraft.Definition.Name}",
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
			if (_flightLeader != null)
			{
				_flightLeaderLabel.Text = $"Flight Leader: {_flightLeader.Pilot.Name}";
			}
			else
			{
				_flightLeaderLabel.Text = "Flight Leader: (select an assignment)";
			}
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

			_costPreview.Text = $"Flights: {flightCount} | Crew: {crewCount} | Fuel: ~{baseFuel} | Ammo: ~{baseAmmo}";
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

			var missionType = (MissionType)_typeOption.Selected;
			int distance = (int)_distanceSlider.Value;
			var risk = (RiskPosture)_riskOption.Selected;

			gm.CreateMission(missionType, distance, risk);

			foreach (var assignment in _pendingAssignments)
			{
				gm.AddFlightAssignment(assignment);
			}

			// TODO: Pass flight leader info to mission
			if (_flightLeader != null)
			{
				GD.Print($"Mission launched with Flight Leader: {_flightLeader.Pilot.Name}");
			}

			gm.LaunchMission();

			_pendingAssignments.Clear();
			_flightLeader = null;
			EmitSignal(SignalName.PanelClosed);
			Hide();
		}
	}
}
