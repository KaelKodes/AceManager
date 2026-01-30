using Godot;
using System;
using AceManager.Core;

namespace AceManager.UI
{
    public partial class DailyBriefingPanel : Control
    {
        private Label _dateLabel;
        private Label _weatherLabel;
        private Label _visibilityLabel;
        private RichTextLabel _intelList;
        private RichTextLabel _newsList;
        private Label _commandLabel;
        private Button _dismissButton;

        private DailyBriefing _currentBriefing;

        [Signal] public delegate void BriefingDismissedEventHandler();

        public override void _Ready()
        {
            _dateLabel = GetNode<Label>("%DateLabel");
            _weatherLabel = GetNode<Label>("%WeatherLabel");
            _visibilityLabel = GetNode<Label>("%VisibilityLabel");
            _intelList = GetNode<RichTextLabel>("%IntelList");
            _newsList = GetNode<RichTextLabel>("%NewsList");
            _commandLabel = GetNode<Label>("%CommandLabel");
            _dismissButton = GetNode<Button>("%DismissButton");

            _dismissButton.Pressed += OnDismissPressed;
        }

        public void DisplayBriefing(DailyBriefing briefing)
        {
            _currentBriefing = briefing;

            _dateLabel.Text = briefing.Date.ToString("MMMM d, yyyy");

            // Weather
            _weatherLabel.Text = briefing.GetWeatherDescription();
            if (briefing.IsFlightGrounded())
            {
                _weatherLabel.Modulate = new Color(1, 0.4f, 0.4f);
            }
            else
            {
                _weatherLabel.Modulate = Colors.White;
            }

            string visDesc = briefing.Visibility switch
            {
                >= 8 => "Excellent",
                >= 6 => "Good",
                >= 4 => "Fair",
                >= 2 => "Poor",
                _ => "Very Poor"
            };
            _visibilityLabel.Text = $"Visibility: {visDesc} ({briefing.Visibility}/10)";

            // Intel reports
            _intelList.Text = "";
            foreach (var report in briefing.IntelReports)
            {
                _intelList.Text += $"• {report}\n";
            }

            // Allied news
            _newsList.Text = "";
            foreach (var news in briefing.AlliedNews)
            {
                _newsList.Text += $"• {news}\n";
            }

            // Command message
            _commandLabel.Text = briefing.CommandMessage;

            Show();
        }

        private void OnDismissPressed()
        {
            EmitSignal(SignalName.BriefingDismissed);
            Hide();
        }
    }
}
