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

        // AI Processing UI
        private Control _aiProcessingOverlay;
        private ProgressBar _aiProgressBar;
        private Label _aiStatusLabel;

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

            // Setup AI Processing Overlay programmatically if not in scene
            SetupAIProcessingUI();
        }

        private void SetupAIProcessingUI()
        {
            // Use a PanelContainer with a subtle background for visibility
            _aiProcessingOverlay = new PanelContainer
            {
                Name = "AIProcessingOverlay",
                MouseFilter = MouseFilterEnum.Stop
            };
            AddChild(_aiProcessingOverlay);

            // Set dynamic style for the panel
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0, 0, 0, 0.4f);
            style.SetContentMarginAll(15);
            style.CornerRadiusBottomLeft = 5;
            style.CornerRadiusBottomRight = 5;
            style.CornerRadiusTopLeft = 5;
            style.CornerRadiusTopRight = 5;
            _aiProcessingOverlay.AddThemeStyleboxOverride("panel", style);

            // Anchor to Bottom-Left with explicit bounds
            _aiProcessingOverlay.SetAnchorsPreset(LayoutPreset.BottomLeft);
            _aiProcessingOverlay.OffsetLeft = 40;
            _aiProcessingOverlay.OffsetTop = -200; // 200px from bottom edge
            _aiProcessingOverlay.OffsetRight = 440; // 400px wide
            _aiProcessingOverlay.OffsetBottom = -120; // 120px from bottom edge

            var vbox = new VBoxContainer();
            _aiProcessingOverlay.AddChild(vbox);

            var title = new Label { Text = "STRATEGIC PLANNING", HorizontalAlignment = HorizontalAlignment.Left };
            title.AddThemeFontSizeOverride("font_size", 18);
            title.Modulate = new Color(0.9f, 0.9f, 0.9f);
            vbox.AddChild(title);

            _aiProgressBar = new ProgressBar { CustomMinimumSize = new Vector2(0, 12), ShowPercentage = false };
            vbox.AddChild(_aiProgressBar);

            _aiStatusLabel = new Label { Text = "Mapping Frontline...", HorizontalAlignment = HorizontalAlignment.Left };
            _aiStatusLabel.Modulate = new Color(0.7f, 0.7f, 0.7f);
            _aiStatusLabel.AddThemeFontSizeOverride("font_size", 13);
            vbox.AddChild(_aiStatusLabel);

            _aiProcessingOverlay.Hide();
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

            // Command message - Hide if processing is about to run
            if (!GameManager.Instance.AITurnProcessedToday)
            {
                _commandLabel.Text = "";
            }
            else
            {
                var assigned = GameManager.Instance.GetAssignedMission();
                _commandLabel.Text = assigned != null
                    ? $"{assigned.CommanderOrderContext}\n\n{briefing.CommandMessage}"
                    : briefing.CommandMessage;
            }

            Show();

            if (!GameManager.Instance.AITurnProcessedToday)
            {
                RunAIProcessingSequence();
            }
            else
            {
                _aiProcessingOverlay.Hide();
                _dismissButton.Disabled = false;
            }
        }

        private async void RunAIProcessingSequence()
        {
            _aiProcessingOverlay.Show();
            _dismissButton.Disabled = true;
            _aiProgressBar.Value = 0;

            string[] stages = {
                "Main Command assessing strategic situation...",
                "Main Command issuing daily objectives...",
                "Regional Commanders deploying resources...",
                "Coordinating with adjacent sectors...",
                "Finalizing daily flight orders..."
            };

            for (int i = 0; i < stages.Length; i++)
            {
                _aiStatusLabel.Text = stages[i];
                float targetValue = (i + 1) * (100f / stages.Length);

                // Tween or simple increment
                while (_aiProgressBar.Value < targetValue)
                {
                    _aiProgressBar.Value += 2;
                    await ToSignal(GetTree().CreateTimer(0.05f), "timeout");
                }

                if (i == 2) // Middle of the process, actually run the sim logic
                {
                    GameManager.Instance.ProcessAITurn();
                }
            }

            await ToSignal(GetTree().CreateTimer(0.5f), "timeout");
            _aiProcessingOverlay.Hide();
            _dismissButton.Disabled = false;

            // Reveal the finished orders
            var assigned = GameManager.Instance.GetAssignedMission();
            _commandLabel.Text = assigned != null
                ? $"{assigned.CommanderOrderContext}\n\n{_currentBriefing.CommandMessage}"
                : _currentBriefing.CommandMessage;
        }

        private void OnDismissPressed()
        {
            EmitSignal(SignalName.BriefingDismissed);
            Hide();
        }
    }
}
