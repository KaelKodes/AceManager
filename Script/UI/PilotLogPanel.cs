using Godot;
using System;
using AceManager.Core;

namespace AceManager.UI
{
    public partial class PilotLogPanel : Control
    {
        private CrewData _pilot;
        private VBoxContainer _logContainer;
        private Label _pilotNameLabel;
        private Button _closeButton;

        public override void _Ready()
        {
            // Dimming Background
            var dim = new ColorRect { Color = new Color(0, 0, 0, 0.7f) };
            dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(dim);

            // Center Container
            var center = new CenterContainer();
            center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(center);

            // Panel Container
            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(900, 700);
            center.AddChild(panel);

            // Style the panel (Vintage Logbook)
            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.12f, 0.10f, 0.08f), // Dark Slate
                BorderWidthLeft = 4,
                BorderWidthTop = 4,
                BorderWidthRight = 4,
                BorderWidthBottom = 4,
                BorderColor = new Color(0.4f, 0.35f, 0.2f), // Brass
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4,
                ShadowSize = 10,
                ShadowColor = new Color(0, 0, 0, 0.5f)
            };
            panel.AddThemeStyleboxOverride("panel", style);

            var mainVBox = new VBoxContainer();
            mainVBox.AddThemeConstantOverride("separation", 20);
            panel.AddChild(mainVBox);

            // Header
            var header = new HBoxContainer();
            header.CustomMinimumSize = new Vector2(0, 60);
            mainVBox.AddChild(header);

            _pilotNameLabel = new Label
            {
                Text = "PILOT'S MISSION LOG",
                HorizontalAlignment = HorizontalAlignment.Center,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            _pilotNameLabel.AddThemeFontSizeOverride("font_size", 28);
            _pilotNameLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.6f));
            header.AddChild(_pilotNameLabel);

            // Scroll for Entries
            var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            mainVBox.AddChild(scroll);

            _logContainer = new VBoxContainer();
            _logContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _logContainer.AddThemeConstantOverride("separation", 15);
            scroll.AddChild(_logContainer);

            // Footer / Close
            var footer = new CenterContainer();
            mainVBox.AddChild(footer);

            _closeButton = new Button { Text = "CLOSE LOGBOOK", CustomMinimumSize = new Vector2(250, 50) };
            _closeButton.Pressed += () => Hide();
            footer.AddChild(_closeButton);
        }

        public void DisplayLog(CrewData pilot)
        {
            _pilot = pilot;
            _pilotNameLabel.Text = $"{pilot.Name.ToUpper()} - MISSION LOG";

            // Clear old entries
            foreach (Node child in _logContainer.GetChildren())
            {
                child.QueueFree();
            }

            if (pilot.MissionHistory.Count == 0)
            {
                var emptyLabel = new Label
                {
                    Text = "\n\nNo combat entries recorded yet.\nNew flyers must earn their place in the logbook.",
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                emptyLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
                _logContainer.AddChild(emptyLabel);
            }
            else
            {
                foreach (var entry in pilot.MissionHistory)
                {
                    AddEntryUI(entry);
                }
            }

            Show();
        }

        private void AddEntryUI(PilotLogEntry entry)
        {
            var entryPanel = new PanelContainer();
            var entryStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.18f, 0.16f, 0.14f),
                ContentMarginLeft = 15,
                ContentMarginTop = 15,
                ContentMarginRight = 15,
                ContentMarginBottom = 15,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4
            };
            entryPanel.AddThemeStyleboxOverride("panel", entryStyle);
            _logContainer.AddChild(entryPanel);

            var vbox = new VBoxContainer();
            entryPanel.AddChild(vbox);

            // Date and Type
            var header = new HBoxContainer();
            vbox.AddChild(header);

            var dateLabel = new Label { Text = entry.Date };
            dateLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
            header.AddChild(dateLabel);

            var typeLabel = new Label { Text = $"|   {entry.MissionType.ToUpper()}", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            typeLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.8f, 1.0f));
            header.AddChild(typeLabel);

            // Outcome Icons/Stats
            if (entry.Kills > 0 || entry.WasWounded || entry.WasShotDown)
            {
                var icons = new HBoxContainer();
                vbox.AddChild(icons);

                if (entry.Kills > 0)
                {
                    var killLabel = new Label { Text = $"[ {entry.Kills} CONFIRMED KILL{(entry.Kills > 1 ? "S" : "")} ]" };
                    killLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.8f, 0.2f));
                    icons.AddChild(killLabel);
                }

                if (entry.WasWounded)
                {
                    var woundLabel = new Label { Text = "[ WOUNDED ]" };
                    woundLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.4f, 0.4f));
                    icons.AddChild(woundLabel);
                }

                if (entry.WasShotDown)
                {
                    var crashLabel = new Label { Text = "[ SHOT DOWN ]" };
                    crashLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.2f, 0.2f));
                    icons.AddChild(crashLabel);
                }
            }

            // Narrative Text
            var narrativeLabel = new Label
            {
                Text = $"\"{entry.Narrative}\"",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            narrativeLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.85f));
            vbox.AddChild(narrativeLabel);
        }
    }
}
