using Godot;
using System;
using AceManager.Core;
using System.Linq;

namespace AceManager.UI
{
    public partial class RosterPanel : Control
    {
        private Tree _pilotTree;
        private Tree _aircraftTree;
        private Label _legend;
        private TabContainer _tabs;
        private Button _closeButton;

        private PackedScene _infoPopupScene;

        private int _pilotSortColumn = 2; // Default to OVR
        private bool _pilotSortAscending = false;
        private int _aircraftSortColumn = 3; // Default to COND
        private bool _aircraftSortAscending = false;

        public override void _Ready()
        {
            _pilotTree = GetNode<Tree>("%PilotList");
            _aircraftTree = GetNode<Tree>("%AircraftList");
            _legend = GetNode<Label>("%Legend");
            _tabs = GetNode<TabContainer>("%TabContainer");
            _closeButton = GetNode<Button>("%CloseButton");

            _infoPopupScene = GD.Load<PackedScene>("res://Scene/UI/InfoPopup.tscn");

            _closeButton.Pressed += QueueFree;
            _pilotTree.ItemActivated += OnPilotActivated;
            _aircraftTree.ItemActivated += OnAircraftActivated;

            _pilotTree.ColumnTitleClicked += (col, btn) => OnPilotSortPressed(col);
            _aircraftTree.ColumnTitleClicked += (col, btn) => OnAircraftSortPressed(col);

            _tabs.TabChanged += (tab) => UpdateLegend();

            SetupHeaders();
            PopulateLists();
            UpdateLegend();
        }

        private void UpdateLegend()
        {
            if (_tabs.CurrentTab == 0) // Pilots
            {
                _legend.Text = "OVR: Overall Rating | GUN: Gunnery | CTL: Control | OA: Offense | DIS: Discipline | CMP: Composure | STA: Stamina | MSS: Missions | VIC: Victories";
            }
            else // Aircraft
            {
                _legend.Text = "COND: Condition | SPD: Max Speed | CEL: Max Ceiling | RNG: Max Range | MSS: Sorties Survived | Kills: Enemy Aircraft Destroyed";
            }
        }

        private void SetupHeaders()
        {
            // Pilot Headers
            string[] pilotHeaders = { "Name", "Rank", "ROLE", "OVR", "GUN", "CTL", "OA", "DIS", "CMP", "STA", "MSS", "VIC" };
            for (int i = 0; i < pilotHeaders.Length; i++)
            {
                _pilotTree.SetColumnTitle(i, pilotHeaders[i]);
                _pilotTree.SetColumnExpand(i, i == 0 || i == 2); // Expand name and role
                if (i > 2) _pilotTree.SetColumnCustomMinimumWidth(i, 45);
            }

            // Aircraft Headers
            string[] aircraftHeaders = { "Tail #", "Type", "Status", "COND", "SPD", "CEL", "RNG", "MSS", "Kills" };
            for (int i = 0; i < aircraftHeaders.Length; i++)
            {
                _aircraftTree.SetColumnTitle(i, aircraftHeaders[i]);
                _aircraftTree.SetColumnExpand(i, i == 0 || i == 1);
                if (i > 2) _aircraftTree.SetColumnCustomMinimumWidth(i, 50);
            }
        }

        private void PopulateLists()
        {
            PopulatePilots();
            PopulateAircraft();
        }

        private void PopulatePilots()
        {
            _pilotTree.Clear();
            var root = _pilotTree.CreateItem();

            var pilots = GameManager.Instance.Roster.Roster.ToList();

            // Sort
            pilots.Sort((a, b) =>
            {
                int result = _pilotSortColumn switch
                {
                    0 => string.Compare(a.Name, b.Name),
                    1 => string.Compare(a.CurrentRank, b.CurrentRank),
                    2 => string.Compare(a.PrimaryRole, b.PrimaryRole),
                    3 => a.GetOverallRating().CompareTo(b.GetOverallRating()),
                    4 => a.GUN.CompareTo(b.GUN),
                    5 => a.CTL.CompareTo(b.CTL),
                    6 => a.OA.CompareTo(b.OA),
                    7 => a.DIS.CompareTo(b.DIS),
                    8 => a.CMP.CompareTo(b.CMP),
                    9 => a.STA.CompareTo(b.STA),
                    10 => a.MissionsFlown.CompareTo(b.MissionsFlown),
                    11 => a.AerialVictories.CompareTo(b.AerialVictories),
                    _ => 0
                };
                return _pilotSortAscending ? result : -result;
            });

            foreach (var pilot in pilots)
            {
                var item = _pilotTree.CreateItem(root);
                item.SetText(0, pilot.Name);
                item.SetText(1, pilot.CurrentRank);
                item.SetText(2, pilot.PrimaryRole);
                item.SetText(3, pilot.GetOverallRating().ToString());
                item.SetText(4, pilot.GUN.ToString());
                item.SetText(5, pilot.CTL.ToString());
                item.SetText(6, pilot.OA.ToString());
                item.SetText(7, pilot.DIS.ToString());
                item.SetText(8, pilot.CMP.ToString());
                item.SetText(9, pilot.STA.ToString());
                item.SetText(10, pilot.MissionsFlown.ToString());
                item.SetText(11, pilot.AerialVictories.ToString());

                item.SetMetadata(0, pilot.Name);

                // Color code by fatigue (v2.0)
                if (pilot.Fatigue > 75) item.SetCustomBgColor(0, new Color(0.8f, 0.4f, 0, 0.2f));
                else if (pilot.Fatigue > 50) item.SetCustomBgColor(0, new Color(0.8f, 0.8f, 0, 0.1f));

                // Colors
                for (int i = 0; i < 12; i++) item.SetTextAlignment(i, i == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Center);
                if (pilot.GetOverallRating() > 70) item.SetCustomColor(3, new Color(1, 0.84f, 0)); // Gold for high rating
            }
        }

        private void PopulateAircraft()
        {
            _aircraftTree.Clear();
            var root = _aircraftTree.CreateItem();

            var aircraft = GameManager.Instance.AircraftInventory.ToList();

            // Sort
            aircraft.Sort((a, b) =>
            {
                int result = _aircraftSortColumn switch
                {
                    0 => string.Compare(a.TailNumber, b.TailNumber),
                    1 => string.Compare(a.Definition.Name, b.Definition.Name),
                    2 => ((int)a.Status).CompareTo((int)b.Status),
                    3 => a.Condition.CompareTo(b.Condition),
                    4 => a.Definition.SpeedRange.CompareTo(b.Definition.SpeedRange),
                    5 => a.Definition.CeilingRange.CompareTo(b.Definition.CeilingRange),
                    6 => a.Definition.DistanceRange.CompareTo(b.Definition.DistanceRange),
                    7 => a.MissionsSurvived.CompareTo(b.MissionsSurvived),
                    8 => a.Kills.CompareTo(b.Kills),
                    _ => 0
                };
                return _aircraftSortAscending ? result : -result;
            });

            foreach (var plane in aircraft)
            {
                var item = _aircraftTree.CreateItem(root);
                item.SetText(0, plane.TailNumber);
                item.SetText(1, plane.Definition.Name);
                item.SetText(2, plane.Status.ToString());
                item.SetText(3, $"{plane.Condition}%");
                item.SetText(4, plane.Definition.SpeedRange.ToString());
                item.SetText(5, plane.Definition.CeilingRange.ToString());
                item.SetText(6, plane.Definition.DistanceRange.ToString());
                item.SetText(7, plane.MissionsSurvived.ToString());
                item.SetText(8, plane.Kills.ToString());

                item.SetMetadata(0, plane.TailNumber);

                for (int i = 0; i < 9; i++) item.SetTextAlignment(i, i < 2 ? HorizontalAlignment.Left : HorizontalAlignment.Center);
                if (plane.Status == AircraftStatus.Lost) item.SetCustomColor(2, new Color(1, 0.3f, 0.3f));
            }
        }

        private void OnPilotSortPressed(long column)
        {
            if (_pilotSortColumn == (int)column)
                _pilotSortAscending = !_pilotSortAscending;
            else
            {
                _pilotSortColumn = (int)column;
                _pilotSortAscending = false;
            }
            PopulatePilots();
        }

        private void OnAircraftSortPressed(long column)
        {
            if (_aircraftSortColumn == (int)column)
                _aircraftSortAscending = !_aircraftSortAscending;
            else
            {
                _aircraftSortColumn = (int)column;
                _aircraftSortAscending = false;
            }
            PopulateAircraft();
        }

        private void OnPilotActivated()
        {
            var item = _pilotTree.GetSelected();
            if (item == null) return;

            string name = (string)item.GetMetadata(0);
            var pilot = GameManager.Instance.Roster.GetPilotByName(name);
            if (pilot != null)
            {
                ShowInfo(pilot.Name, GetPilotDetails(pilot));
            }
        }

        private void OnAircraftActivated()
        {
            var item = _aircraftTree.GetSelected();
            if (item == null) return;

            string tail = (string)item.GetMetadata(0);
            var plane = GameManager.Instance.AircraftInventory.FirstOrDefault(a => a.TailNumber == tail);
            if (plane != null)
            {
                ShowInfo(plane.GetDisplayName(), GetAircraftDetails(plane));
            }
        }

        private void ShowInfo(string title, string content)
        {
            var popup = _infoPopupScene.Instantiate<InfoPopup>();
            AddChild(popup);
            popup.ShowInfo(title, content);
        }

        private string GetPilotDetails(CrewData pilot)
        {
            return $"Rank: {pilot.CurrentRank}\n" +
                   $"Role: {pilot.PrimaryRole} {(!string.IsNullOrEmpty(pilot.SecondaryRole) ? "/ " + pilot.SecondaryRole : "")}\n" +
                   $"Status: {(pilot.Fatigue > 75 ? "Exhausted" : pilot.Fatigue > 40 ? "Fatigued" : "Fit")}\n" +
                   $"Missions: {pilot.MissionsFlown}\n" +
                   $"Victories: {pilot.AerialVictories}\n\n" +
                   $"--- Stats ---\n" +
                   $"Gunnery: {pilot.GUN}\n" +
                   $"Control: {pilot.CTL}\n" +
                   $"Energy: {pilot.ENG}\n" +
                   $"Awareness: {pilot.OA} (O) / {pilot.DA} (D)\n" +
                   $"Discipline: {pilot.DIS}\n" +
                   $"Composure: {pilot.CMP}\n" +
                   $"Stamina: {pilot.STA}\n";
        }

        private string GetAircraftDetails(AircraftInstance plane)
        {
            return $"Type: {plane.Definition.Name}\n" +
                   $"Status: {plane.GetStatusDisplay()}\n" +
                   $"Condition: {plane.Condition}%\n" +
                   $"Hours Flown: {plane.HoursFlown}\n" +
                   $"Missions Survived: {plane.MissionsSurvived}\n" +
                   $"Kills: {plane.Kills}\n\n" +
                   $"--- Specs ---\n" +
                   $"Speed: {plane.Definition.SpeedRange}/10\n" +
                   $"Ceiling: {plane.Definition.CeilingRange}/10\n" +
                   $"Range: {plane.Definition.DistanceRange}/10";
        }
    }
}
