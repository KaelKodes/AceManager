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
        }

        private void SelectNation(string nation)
        {
            GD.Print($"Nation selected in UI: {nation}");
            GameManager.Instance.StartCampaign(nation);
            EmitSignal(SignalName.NationSelected, nation);
            Hide();
            QueueFree(); // Close and remove once started
        }
    }
}
