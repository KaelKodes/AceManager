using Godot;
using System;
using AceManager.Core;

namespace AceManager.UI
{
    public partial class AircraftCard : Control
    {
        private Label _nameLabel;
        private Label _tailLabel;
        private Label _statsLabel;
        private Label _performanceLabel;
        private Label _conditionLabel;
        private Button _closeButton;

        private AircraftInstance _aircraft;

        [Signal] public delegate void CardClosedEventHandler();

        public override void _Ready()
        {
            _nameLabel = GetNode<Label>("%NameLabel");
            _tailLabel = GetNode<Label>("%TailLabel");
            _statsLabel = GetNode<Label>("%StatsLabel");
            _performanceLabel = GetNode<Label>("%PerformanceLabel");
            _conditionLabel = GetNode<Label>("%ConditionLabel");
            _closeButton = GetNode<Button>("%CloseButton");

            _closeButton.Pressed += OnClosePressed;
        }

        public void ShowAircraft(AircraftInstance aircraft)
        {
            _aircraft = aircraft;
            if (aircraft == null || aircraft.Definition == null) return;

            var def = aircraft.Definition;

            _nameLabel.Text = def.Name;
            _tailLabel.Text = $"Tail: {aircraft.TailNumber} | Status: {aircraft.GetStatusDisplay()}";

            // Aircraft specs
            _statsLabel.Text = $@"SPECIFICATIONS
Nation: {def.Nation}
Manufacturer: {def.Manufacturer}
Year: {def.YearIntroduced}
Role: {def.RolePrimary}
Crew Seats: {def.CrewSeats}
Variant: {def.Variant}

ARMAMENT
Firepower: {def.FirepowerRange}
Accuracy: {def.AccuracyRange}
Ammo: {def.AmmoRange}
Rear Gunner: {def.FirepowerRear}
Weapon: {def.WeaponType}
Arc: {def.FiringArc}

REQUIREMENTS
Runway Level: {def.RunwayRequirementRange}
Priority Tier: {def.CommandPriorityTier}";

            // Performance ratings
            _performanceLabel.Text = $@"FLIGHT PERFORMANCE
Speed: {def.SpeedRange}/10
Climb: {def.ClimbRange}/10
Turn: {def.TurnRange}/10
Stability: {def.StabilityRange}/10
Dive Safety: {def.DiveSafetyRange}/10
Ceiling: {def.CeilingRange}/10
Range: {def.DistanceRange}/10

EFFECTIVENESS
Fighter: {def.GetFighterEffectiveness():F1}
Bomber: {def.GetBomberEffectiveness():F1}
Recon: {def.GetReconEffectiveness():F1}
Durability: {def.GetDurabilityScore():F1}";

            // Current condition
            string conditionColor = aircraft.Condition >= 80 ? "Good" :
                                    aircraft.Condition >= 50 ? "Fair" : "Poor";
            _conditionLabel.Text = $@"AIRCRAFT STATUS
Condition: {aircraft.Condition}% ({conditionColor})
Hours Flown: {aircraft.HoursFlown:F1}
Missions Survived: {aircraft.MissionsSurvived}
Kills: {aircraft.Kills}
{(aircraft.RepairDaysRemaining > 0 ? $"Repair Days Left: {aircraft.RepairDaysRemaining}" : "")}";

            Show();
        }

        private void OnClosePressed()
        {
            EmitSignal(SignalName.CardClosed);
            Hide();
        }
    }
}
