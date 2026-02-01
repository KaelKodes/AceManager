using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using AceManager.Core;

namespace AceManager.UI
{
    public partial class DebriefingPanel : Control
    {
        private MissionData _mission;
        private VBoxContainer _leftColumn;
        private VBoxContainer _rightColumn;
        private Button _continueButton;
        private ScrollContainer _logScroll;
        private PanelContainer _scoreCard;

        [Signal] public delegate void DebriefCompletedEventHandler();

        public void Setup(MissionData mission)
        {
            _mission = mission;
            BuildUI();
            AnimateStatGains();
        }

        private void BuildUI()
        {
            // 1. Black/Dark Background
            var bg = new ColorRect { Color = new Color(0.05f, 0.05f, 0.05f, 1.0f) };
            bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(bg);

            // 2. Main Layout Container
            var margin = new MarginContainer();
            margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_left", 40);
            margin.AddThemeConstantOverride("margin_right", 40);
            margin.AddThemeConstantOverride("margin_top", 40);
            margin.AddThemeConstantOverride("margin_bottom", 40);
            AddChild(margin);

            var mainHBox = new HBoxContainer();
            margin.AddChild(mainHBox);

            // --- Left Column (Stats) ---
            _leftColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.Fill };
            mainHBox.AddChild(_leftColumn);

            // --- Center Column (Image + Content) ---
            var centerVBox = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsStretchRatio = 2.5f,
                Alignment = BoxContainer.AlignmentMode.Center
            };
            centerVBox.AddThemeConstantOverride("separation", 20);
            mainHBox.AddChild(centerVBox);

            // Image
            var texture = GetDebriefImage(_mission);
            var centerImg = new TextureRect
            {
                Texture = texture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Modulate = new Color(0.9f, 0.9f, 0.9f, 1.0f),
                CustomMinimumSize = new Vector2(800, 450),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
                SizeFlagsVertical = SizeFlags.ShrinkCenter
            };
            centerVBox.AddChild(centerImg);

            // Result Text
            var resultLabel = new Label
            {
                Text = _mission.ResultBand.ToString().Replace("Success", " SUCCESS").Replace("Failure", " FAILURE").ToUpper(),
                HorizontalAlignment = HorizontalAlignment.Center,
                ThemeTypeVariation = "HeaderLarge"
            };
            resultLabel.AddThemeFontSizeOverride("font_size", 48);
            resultLabel.AddThemeColorOverride("font_color", GetResultColor(_mission.ResultBand));
            centerVBox.AddChild(resultLabel);

            // --- Score Card ---
            _scoreCard = BuildScoreCard();
            _scoreCard.Modulate = new Color(1, 1, 1, 0); // Hide for animation
            centerVBox.AddChild(_scoreCard);

            // Summary Text (Scrollable)
            _logScroll = new ScrollContainer
            {
                CustomMinimumSize = new Vector2(600, 150), // Give it some height
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
                VerticalScrollMode = ScrollContainer.ScrollMode.Auto
            };
            centerVBox.AddChild(_logScroll);

            // Dismiss Button
            var summaryLabel = new RichTextLabel
            {
                Text = "[center]" + string.Join("\n", _mission.MissionLog) + "[/center]", // Join all log entries and center
                BbcodeEnabled = true,
                FitContent = true,
                ScrollActive = false, // Let ScrollContainer handle it
                CustomMinimumSize = new Vector2(600, 0),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            summaryLabel.AddThemeColorOverride("default_color", new Color(0.8f, 0.8f, 0.8f));
            _logScroll.AddChild(summaryLabel);

            // Dismiss Button
            _continueButton = new Button
            {
                Text = "DISMISS",
                CustomMinimumSize = new Vector2(200, 50),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter
            };
            _continueButton.Pressed += () => { EmitSignal(SignalName.DebriefCompleted); QueueFree(); };
            _continueButton.Hide(); // Show after animation
            centerVBox.AddChild(_continueButton);

            // --- Right Column (Stats) ---
            _rightColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.Fill };
            mainHBox.AddChild(_rightColumn);
        }

        private async void AnimateStatGains()
        {
            var pilots = _mission.Assignments
                .Where(a => a.Pilot != null && a.Pilot.DailyImprovements.Count > 0)
                .Select(a => a.Pilot)
                .Distinct()
                .ToList();

            bool leftinfo = true;

            foreach (var pilot in pilots)
            {
                var popup = CreateStatPopup(pilot);
                if (leftinfo) _leftColumn.AddChild(popup);
                else _rightColumn.AddChild(popup);

                leftinfo = !leftinfo;

                // Simple Fade In Animation (Simulated)
                var tween = CreateTween();
                popup.Modulate = new Color(1, 1, 1, 0);
                tween.TweenProperty(popup, "modulate", new Color(1, 1, 1, 1), 0.5f);

                await ToSignal(GetTree().CreateTimer(0.8f), "timeout");
            }

            // Start Cinematic Scroll
            AnimateLogScroll();

            // Show Score Card
            var scorecardTween = CreateTween();
            scorecardTween.TweenProperty(_scoreCard, "modulate", new Color(1, 1, 1, 1), 0.5f);
            await ToSignal(GetTree().CreateTimer(0.6f), "timeout");

            // Show Continue Button after all animations
            _continueButton.Show();
        }

        private async void AnimateLogScroll()
        {
            if (_logScroll == null) return;

            // Wait for layout to calculate sizes
            await ToSignal(GetTree(), "process_frame");
            await ToSignal(GetTree(), "process_frame");

            int totalHeight = (int)_logScroll.GetVScrollBar().MaxValue;
            int visibleHeight = (int)_logScroll.Size.Y;

            if (totalHeight > visibleHeight)
            {
                var tween = CreateTween();

                // Calculate duration based on length (e.g., 20 pixels per second, min 5 seconds)
                float pixelsToScroll = totalHeight - visibleHeight;
                float duration = Math.Max(5.0f, pixelsToScroll / 20.0f);

                // Wait a moment before scrolling
                tween.TweenInterval(1.0f);
                tween.TweenProperty(_logScroll, "scroll_vertical", (int)pixelsToScroll, duration)
                    .SetTrans(Tween.TransitionType.Linear)
                    .SetEase(Tween.EaseType.InOut);
            }
        }

        private Control CreateStatPopup(CrewData pilot)
        {
            var panel = new PanelContainer();
            var style = new StyleBoxFlat
            {
                BgColor = new Color(0, 0, 0, 0.7f),
                CornerRadiusTopLeft = 5,
                CornerRadiusTopRight = 5,
                CornerRadiusBottomLeft = 5,
                CornerRadiusBottomRight = 5,
                ContentMarginLeft = 10,
                ContentMarginRight = 10,
                ContentMarginTop = 5,
                ContentMarginBottom = 5
            };
            panel.AddThemeStyleboxOverride("panel", style);

            var vBox = new VBoxContainer();
            panel.AddChild(vBox);

            var nameLabel = new Label
            {
                Text = pilot.Name,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            nameLabel.AddThemeColorOverride("font_color", new Color(1f, 0.8f, 0.4f)); // Goldish name
            nameLabel.AddThemeFontSizeOverride("font_size", 16);
            vBox.AddChild(nameLabel);

            foreach (var kvp in pilot.DailyImprovements)
            {
                if (kvp.Value > 0)
                {
                    var statLabel = new Label
                    {
                        Text = $"+{kvp.Value:F1} {kvp.Key}"
                    };
                    statLabel.AddThemeColorOverride("font_color", new Color(0.6f, 1f, 0.6f)); // Green gain
                    statLabel.AddThemeFontSizeOverride("font_size", 14);
                    vBox.AddChild(statLabel);
                }
            }

            return panel;
        }

        private PanelContainer BuildScoreCard()
        {
            var panel = new PanelContainer();
            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.15f, 0.15f, 0.15f, 0.8f),
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                BorderWidthTop = 2,
                BorderWidthBottom = 2,
                BorderColor = new Color(0.3f, 0.3f, 0.3f, 1.0f),
                CornerRadiusTopLeft = 5,
                CornerRadiusTopRight = 5,
                CornerRadiusBottomLeft = 5,
                CornerRadiusBottomRight = 5,
                ContentMarginLeft = 20,
                ContentMarginRight = 20,
                ContentMarginTop = 10,
                ContentMarginBottom = 10
            };
            panel.AddThemeStyleboxOverride("panel", style);
            panel.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

            var hBox = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            hBox.AddThemeConstantOverride("separation", 30);
            panel.AddChild(hBox);

            hBox.AddChild(CreateScoreItem("KILLS", _mission.EnemyKills.ToString(), new Color(1, 0.8f, 0.2f)));
            hBox.AddChild(CreateScoreItem("LOSSES", _mission.AircraftLost.ToString(), _mission.AircraftLost > 0 ? new Color(1, 0.3f, 0.3f) : Colors.White));
            hBox.AddChild(CreateScoreItem("FUEL", _mission.FuelConsumed.ToString(), Colors.White));
            hBox.AddChild(CreateScoreItem("AMMO", _mission.AmmoConsumed.ToString(), Colors.White));

            return panel;
        }

        private VBoxContainer CreateScoreItem(string label, string value, Color valueColor)
        {
            var vBox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };

            var lbl = new Label
            {
                Text = label,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            lbl.AddThemeFontSizeOverride("font_size", 12);
            lbl.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            vBox.AddChild(lbl);

            var val = new Label
            {
                Text = value,
                HorizontalAlignment = HorizontalAlignment.Center,
                ThemeTypeVariation = "HeaderSmall"
            };
            val.AddThemeFontSizeOverride("font_size", 22);
            val.AddThemeColorOverride("font_color", valueColor);
            vBox.AddChild(val);

            return vBox;
        }

        private Color GetResultColor(MissionResultBand result)
        {
            return result switch
            {
                MissionResultBand.DecisiveSuccess => new Color(0.2f, 1.0f, 0.2f),
                MissionResultBand.Success => new Color(0.4f, 1.0f, 0.4f),
                MissionResultBand.MarginalSuccess => new Color(0.7f, 1.0f, 0.4f),
                MissionResultBand.Stalemate => new Color(1.0f, 1.0f, 0.2f),
                MissionResultBand.MarginalFailure => new Color(1.0f, 0.6f, 0.2f),
                MissionResultBand.Failure => new Color(1.0f, 0.4f, 0.2f),
                MissionResultBand.Disaster => new Color(1.0f, 0.2f, 0.2f),
                _ => Colors.White
            };
        }

        private Texture2D GetDebriefImage(MissionData mission)
        {
            // 1. Check for Disasters / Crashes
            if (mission.ResultBand == MissionResultBand.Disaster || mission.ResultBand == MissionResultBand.Failure)
            {
                // 50/50 chance for different crash images
                return new Random().Next(2) == 0
                    ? GD.Load<Texture2D>("res://Assets/UI/Debrief/DebriefCrashed.png")
                    : GD.Load<Texture2D>("res://Assets/UI/Debrief/DebriefCrashed2.png");
            }

            // 2. Check for heavy damage / wounded
            if (mission.CrewWounded > 0 || mission.AircraftLost > 0)
            {
                return GD.Load<Texture2D>("res://Assets/UI/Debrief/DebriefDamaged.png");
            }

            // 3. Success / Celebration (Lounge)
            if (mission.ResultBand <= MissionResultBand.Success)
            {
                string nation = GameManager.Instance.SelectedNation;
                bool isGermany = nation == "Germany";

                int variant = new Random().Next(1, 5); // 1-4 (Germany has 4, Allies have 5, safeguard to 4 for now)

                if (isGermany)
                {
                    return GD.Load<Texture2D>($"res://Assets/UI/PilotsLounge/Offiziersbar{variant}.png");
                }
                else
                {
                    variant = new Random().Next(1, 6); // 1-5 for Allies
                    return GD.Load<Texture2D>($"res://Assets/UI/PilotsLounge/PilotsLounge{variant}.png");
                }
            }

            // 4. Default by Mission Type
            return mission.Type switch
            {
                MissionType.Bombing or MissionType.Strafing => GD.Load<Texture2D>("res://Assets/UI/Debrief/debriefbombing.png"),
                MissionType.Reconnaissance => GD.Load<Texture2D>("res://Assets/UI/Debrief/debriefRecon.png"),
                MissionType.Training => GD.Load<Texture2D>("res://Assets/UI/Debrief/debriefRecon.png"), // Use Recon image (classroom/map) for now
                _ => GD.Load<Texture2D>("res://Assets/UI/Debrief/debriefScramble.png")
            };
        }
    }
}
