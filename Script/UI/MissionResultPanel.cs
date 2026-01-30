using Godot;
using System;
using AceManager.Core;

namespace AceManager.UI
{
    public partial class MissionResultPanel : Control
    {
        private Label _title;
        private Label _resultBand;
        private RichTextLabel _missionLog;
        private Label _killsLabel;
        private Label _lossesLabel;
        private Label _fuelLabel;
        private Label _ammoLabel;
        private Button _dismissButton;

        [Signal] public delegate void PanelClosedEventHandler();

        public override void _Ready()
        {
            _title = GetNode<Label>("%Title");
            _resultBand = GetNode<Label>("%ResultBand");
            _missionLog = GetNode<RichTextLabel>("%MissionLog");
            _killsLabel = GetNode<Label>("%KillsLabel");
            _lossesLabel = GetNode<Label>("%LossesLabel");
            _fuelLabel = GetNode<Label>("%FuelLabel");
            _ammoLabel = GetNode<Label>("%AmmoLabel");
            _dismissButton = GetNode<Button>("%DismissButton");

            _dismissButton.Pressed += OnDismissPressed;
        }

        public void DisplayResults(MissionData mission)
        {
            if (mission == null) return;

            // Title and result band
            _resultBand.Text = mission.ResultBand.ToString().ToUpper();
            _resultBand.Modulate = GetResultColor(mission.ResultBand);

            // Mission log
            _missionLog.Text = "";
            foreach (var entry in mission.MissionLog)
            {
                _missionLog.Text += entry + "\n";
            }

            // Order compliance
            if (!string.IsNullOrEmpty(mission.OrderComplianceMessage))
            {
                _missionLog.BbcodeEnabled = true;
                string colorHex = mission.OrderBonus >= 0 ? "#55ff55" : "#ffff55";
                _missionLog.Text += $"\n[color={colorHex}]◆ {mission.OrderComplianceMessage}[/color]\n";
            }

            // Summary stats
            _killsLabel.Text = $"Kills: {mission.EnemyKills}";
            int totalLosses = mission.AircraftLost + mission.CrewWounded + mission.CrewKilled;
            _lossesLabel.Text = $"Losses: {totalLosses}";
            _fuelLabel.Text = $"Fuel: -{mission.FuelConsumed}";
            _ammoLabel.Text = $"Ammo: -{mission.AmmoConsumed}";

            Show();
        }

        private Color GetResultColor(MissionResultBand band)
        {
            return band switch
            {
                MissionResultBand.DecisiveSuccess => new Color(0.2f, 1.0f, 0.4f),
                MissionResultBand.Success => new Color(0.4f, 0.9f, 0.4f),
                MissionResultBand.MarginalSuccess => new Color(0.7f, 0.9f, 0.4f),
                MissionResultBand.Stalemate => new Color(0.9f, 0.9f, 0.4f),
                MissionResultBand.MarginalFailure => new Color(0.9f, 0.7f, 0.3f),
                MissionResultBand.Failure => new Color(0.9f, 0.4f, 0.3f),
                MissionResultBand.Disaster => new Color(1.0f, 0.2f, 0.2f),
                _ => Colors.White
            };
        }

        private void OnDismissPressed()
        {
            EmitSignal(SignalName.PanelClosed);
            Hide();
        }
    }
}
