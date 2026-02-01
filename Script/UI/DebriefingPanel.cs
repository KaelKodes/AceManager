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

            // 2. Center "Lounge" Image (Placeholder for now)
            // We'll simulate a centered cinematic view
            var centerImg = new TextureRect
            {
                Texture = GD.Load<Texture2D>("res://Assets/UI/Training/Frame.jpg"), // Re-using frame for now as placeholder or board
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Modulate = new Color(0.5f, 0.5f, 0.5f, 0.5f) // Dimmed
            };
            centerImg.CustomMinimumSize = new Vector2(800, 600);
            centerImg.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
            AddChild(centerImg);

            // 3. Central Text (Mission Result)
            var centerContainer = new CenterContainer();
            centerContainer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(centerContainer);

            var vBox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            centerContainer.AddChild(vBox);

            var resultLabel = new Label
            {
                Text = _mission.ResultBand.ToString().Replace("Success", " SUCCESS").Replace("Failure", " FAILURE").ToUpper(),
                HorizontalAlignment = HorizontalAlignment.Center,
                ThemeTypeVariation = "HeaderLarge"
            };
            resultLabel.AddThemeFontSizeOverride("font_size", 48);
            resultLabel.AddThemeColorOverride("font_color", GetResultColor(_mission.ResultBand));
            vBox.AddChild(resultLabel);

            var summaryLabel = new Label
            {
                Text = _mission.MissionLog.LastOrDefault() ?? "Mission Complete",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            summaryLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
            vBox.AddChild(summaryLabel);

            // 4. Side Columns for Stat Popups
            var margin = new MarginContainer();
            margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_left", 50);
            margin.AddThemeConstantOverride("margin_right", 50);
            margin.AddThemeConstantOverride("margin_top", 100);
            margin.AddThemeConstantOverride("margin_bottom", 100);
            AddChild(margin);

            var hBox = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
            margin.AddChild(hBox);

            _leftColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
            hBox.AddChild(_leftColumn);

            // Spacer in middle
            hBox.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 2.0f });

            _rightColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
            hBox.AddChild(_rightColumn);

            // Continue Button (Initially Hidden)
            _continueButton = new Button
            {
                Text = "DISMISS",
                CustomMinimumSize = new Vector2(200, 50)
            };
            centerContainer.AddChild(_continueButton);
            _continueButton.Position = new Vector2(0, 300); // Rough placement, CenterContainer will override
            _continueButton.Pressed += () => { EmitSignal(SignalName.DebriefCompleted); QueueFree(); };
            _continueButton.Hide();
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

            // Show Continue Button after all animations
            await ToSignal(GetTree().CreateTimer(1.0f), "timeout");
            _continueButton.Show();
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
    }
}
