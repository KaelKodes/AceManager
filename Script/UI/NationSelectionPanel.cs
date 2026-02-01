using Godot;
using System;
using AceManager.Core;

namespace AceManager.UI
{
    public partial class NationSelectionPanel : Control
    {
        [Signal] public delegate void NationSelectedEventHandler(string nation);

        private Button _britainButton;
        private Button _franceButton;
        private Button _germanyButton;
        private Button _italyButton;
        private Button _usaButton;

        public override void _Ready()
        {
            _britainButton = GetNode<Button>("%BritainButton");
            _franceButton = GetNode<Button>("%FranceButton");
            _germanyButton = GetNode<Button>("%GermanyButton");
            _italyButton = GetNode<Button>("%ItalyButton");
            _usaButton = GetNode<Button>("%USAButton");

            _britainButton.Pressed += () => SelectNation("Britain");
            _franceButton.Pressed += () => SelectNation("France");
            _germanyButton.Pressed += () => SelectNation("Germany");
            _italyButton.Pressed += () => SelectNation("Italy");
            _usaButton.Pressed += () => SelectNation("USA");

            ApplyThemeStyle(_britainButton.GetParent<Control>());
        }

        private void SelectNation(string nation)
        {
            GD.Print($"Nation selected in UI: {nation}");
            GameManager.Instance.StartCampaign(nation);
            EmitSignal(SignalName.NationSelected, nation);
            Hide();
            QueueFree(); // Close and remove once started
        }

        private void ApplyThemeStyle(Control target)
        {
            if (target == null) return;
            var parent = target.GetParent<Control>();
            if (parent == null) return;

            // Capture index to keep order
            int index = target.GetIndex();

            parent.RemoveChild(target);

            var wrapper = new PanelContainer();

            // Style matching IntroductionPanel
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
                ShadowColor = new Color(0, 0, 0, 0.5f),
                // Add padding for content
                ContentMarginLeft = 20,
                ContentMarginRight = 20,
                ContentMarginTop = 20,
                ContentMarginBottom = 20
            };
            wrapper.AddThemeStyleboxOverride("panel", style);

            // Copy layout flags
            wrapper.SizeFlagsHorizontal = target.SizeFlagsHorizontal;
            wrapper.SizeFlagsVertical = target.SizeFlagsVertical;
            wrapper.LayoutMode = 2; // AnchorsContainer

            parent.AddChild(wrapper);
            parent.MoveChild(wrapper, index);

            wrapper.AddChild(target);
        }
    }
}
