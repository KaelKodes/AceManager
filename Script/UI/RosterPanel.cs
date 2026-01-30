using Godot;
using System;
using AceManager.Core;
using System.Linq;

namespace AceManager.UI
{
    public partial class RosterPanel : Control
    {
        private ItemList _pilotList;
        private ItemList _aircraftList;
        private Button _closeButton;

        private PackedScene _infoPopupScene;

        public override void _Ready()
        {
            _pilotList = GetNode<ItemList>("%PilotList");
            _aircraftList = GetNode<ItemList>("%AircraftList");
            _closeButton = GetNode<Button>("%CloseButton");

            _infoPopupScene = GD.Load<PackedScene>("res://Scene/UI/InfoPopup.tscn");

            _closeButton.Pressed += QueueFree;
            _pilotList.ItemActivated += OnPilotActivated;
            _aircraftList.ItemActivated += OnAircraftActivated;

            PopulateLists();
        }

        private void PopulateLists()
        {
            // Pilots
            _pilotList.Clear();
            var pilots = GameManager.Instance.Roster.Roster;
            foreach (var pilot in pilots)
            {
                int index = _pilotList.AddItem($"{pilot.Name} ({pilot.Role})");
                _pilotList.SetItemMetadata(index, pilot.Name);

                // Color code status (green healthy, red wounded)
                // Need to implement wounded check properly later, for now assume healthy
            }

            // Aircraft
            _aircraftList.Clear();
            var aircraft = GameManager.Instance.AircraftInventory;
            foreach (var plane in aircraft)
            {
                int index = _aircraftList.AddItem(plane.GetDisplayName());
                _aircraftList.SetItemMetadata(index, plane.TailNumber);

                if (plane.Status == AircraftStatus.Lost)
                    _pilotList.SetItemCustomBgColor(index, new Color(0.5f, 0, 0, 0.3f));
            }
        }

        private void OnPilotActivated(long index)
        {
            string name = (string)_pilotList.GetItemMetadata((int)index);
            var pilot = GameManager.Instance.Roster.GetPilotByName(name);
            if (pilot != null)
            {
                ShowInfo(pilot.Name, GetPilotDetails(pilot));
            }
        }

        private void OnAircraftActivated(long index)
        {
            string tail = (string)_aircraftList.GetItemMetadata((int)index);
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
            return $"Rank: {pilot.Role}\n" +
                   $"Missions: {pilot.MissionsFlown}\n" +
                   $"Victories: {pilot.AerialVictories}\n\n" +
                   $"--- Skills ---\n" +
                   $"Gunnery: {pilot.GUN}\n" +
                   $"Piloting: {pilot.CTL}\n" +
                   $"Awareness: {pilot.OA}\n" +
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
