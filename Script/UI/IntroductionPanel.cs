using Godot;
using System;
using System.Collections.Generic;
using AceManager.Core;

namespace AceManager.UI
{
    public partial class IntroductionPanel : Control
    {
        private string _nation;
        private LineEdit _nameEdit;
        private Label _messageLabel;
        private Button _acceptButton;
        private TextureRect _bg;

        public override void _Ready()
        {
            // Dimming Background
            var dim = new ColorRect { Color = new Color(0, 0, 0, 0.8f) };
            dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(dim);

            // Center Container for the panel
            var center = new CenterContainer();
            center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(center);

            // Panel Wrapper
            var wrapper = new PanelContainer { CustomMinimumSize = new Vector2(800, 600) };
            center.AddChild(wrapper);

            // Style the panel (Vintage aesthetic)
            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.12f, 0.10f, 0.08f), // Deep dark
                BorderWidthLeft = 4,
                BorderWidthRight = 4,
                BorderWidthTop = 4,
                BorderWidthBottom = 4,
                BorderColor = new Color(0.4f, 0.35f, 0.25f), // Brass/Bronze
                CornerRadiusTopLeft = 10,
                CornerRadiusTopRight = 10,
                CornerRadiusBottomLeft = 10,
                CornerRadiusBottomRight = 10,
                ShadowSize = 20,
                ShadowColor = new Color(0, 0, 0, 0.5f)
            };
            wrapper.AddThemeStyleboxOverride("panel", style);

            var mainVBox = new VBoxContainer { CustomMinimumSize = new Vector2(750, 550) };
            mainVBox.AddThemeConstantOverride("separation", 20);
            wrapper.AddChild(mainVBox);

            // Title
            var title = new Label
            {
                Text = "COMMISSION ORDERS",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            title.AddThemeFontSizeOverride("font_size", 32);
            title.AddThemeColorOverride("font_color", new Color(0.8f, 0.7f, 0.3f));
            mainVBox.AddChild(title);

            // Content Area (Scrollable text)
            var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            mainVBox.AddChild(scroll);

            var contentVBox = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                Alignment = BoxContainer.AlignmentMode.Center
            };
            scroll.AddChild(contentVBox);

            _messageLabel = new Label
            {
                Text = "Loading briefing...",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            _messageLabel.AddThemeFontSizeOverride("font_size", 20);
            _messageLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.8f));
            contentVBox.AddChild(_messageLabel);

            // Spacer
            contentVBox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 30) });

            // Name Input Section
            var nameHBox = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            mainVBox.AddChild(nameHBox);

            var nameLabel = new Label { Text = "CAPTAIN'S NAME: " };
            nameLabel.AddThemeFontSizeOverride("font_size", 22);
            nameHBox.AddChild(nameLabel);

            _nameEdit = new LineEdit
            {
                PlaceholderText = "Enter Name...",
                CustomMinimumSize = new Vector2(300, 40),
                Alignment = HorizontalAlignment.Center,
                MaxLength = 24
            };
            _nameEdit.AddThemeFontSizeOverride("font_size", 20);
            nameHBox.AddChild(_nameEdit);

            // Accept Button
            var footer = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, CustomMinimumSize = new Vector2(0, 80) };
            mainVBox.AddChild(footer);

            _acceptButton = new Button
            {
                Text = "ACCEPT COMMISSION",
                CustomMinimumSize = new Vector2(300, 50),
                Disabled = true
            };
            _acceptButton.Pressed += OnAcceptPressed;
            footer.AddChild(_acceptButton);

            _nameEdit.TextChanged += (txt) => _acceptButton.Disabled = string.IsNullOrWhiteSpace(txt);
        }

        public void Setup(string nation)
        {
            _nation = nation;
            _messageLabel.Text = GetBriefingText(nation);

            // Set default name based on nation for fun
            _nameEdit.Text = nation switch
            {
                "Britain" => "James Whitmore",
                "France" => "Jean-Luc Picard",
                "Germany" => "Hans Müller",
                "Italy" => "Marco Rossini",
                "USA" => "Chuck Yeager",
                _ => "Unknown Captain"
            };
            _acceptButton.Disabled = false;
        }

        private string GetBriefingText(string nation)
        {
            return nation switch
            {
                "Britain" => "To the newly appointed Commanding Officer,\nRoyal Flying Corps, Western Front.\n\nSir, the situation on the continent has grown dire. The British Expeditionary Force has landed in France, and our scouting pilots are the eyes of the Empire. You are hereby ordered to establish a frontline airbase near the Amiens sector.\n\nYour objective is clear: maintain aerial reconnaissance and protect our ground forces from German incursions.\n\nToday, for you, the war begins. God save the King.",
                "France" => "Citoyen Capitaine,\nAéronautique Militaire.\n\nThe soil of France is being desecrated. The German war machine is pushing towards the heart of our nation. You have been appointed to lead an Escadrille in this desperate struggle. Every flight you command is a blow for liberty and a defense of our homes.\n\nAchieve mastery of the skies by any means necessary. The people of France look to you. Your facilities are minimal, but your resolve must be iron.\n\nToday, the battle for our sacred soil begins. Pour la Patrie!",
                "Germany" => "An den Kommandanten,\nFliegertruppe des deutschen Kaiserreiches.\n\nThe mobilization is complete. Our technical superiority must now be proven in the field of battle. You are commanded to establish an airfield in the Flanders region and ensure that no enemy aircraft dares to trespass over our lines.\n\nDiscipline and precision are your sharpest weapons. See that you do not disappoint the High Command.\n\nFor the Fatherland and for Victory.",
                "Italy" => "Al Comandante,\nCorpo Aeronautico Militare.\n\nThe Alpine front is calling. Our brave infantry are facing the Austrians in the high peaks, and we must provide the wings they need. You are appointed to lead a tactical squadron in the Venetian sector to support our offensive along the Isonzo.\n\nThe terrain is treacherous, and the winds are as dangerous as the enemy, but the skies of the Mediterranean must remain ours.\n\nFor the House of Savoy and for Italy! Today, your duty begins.",
                "USA" => "Captain,\nUnited States Air Service.\n\nThe Yanks are here. We're late to the show, but we're here to stay. Your assignment is to set up shop on the Verdun front and show the Germans what American grit is all about. The Huns are seasoned and won't make it easy.\n\nLet’s send them a clear message. The facilities at your new aerodrome are being brought up to spec as we speak.\n\nToday, the world finds out what the USAS can do. Let's get to work.",
                _ => "You are an aspiring captain tasked with establishing an airbase.\n\nGood luck."
            };
        }

        private void OnAcceptPressed()
        {
            GameManager.Instance.FinalizeCampaignStart(_nameEdit.Text);
            Hide();
            QueueFree();
        }
    }
}
