using Godot;
using System;
using AceManager.Core;

namespace AceManager.UI
{
    public partial class InfoPopup : Control
    {
        private Label _titleLabel;
        private RichTextLabel _contentLabel;
        private Button _closeButton;
        private Button _upgradeButton;
        private Label _costLabel;

        private string _currentFacility;

        public override void _Ready()
        {
            _titleLabel = GetNode<Label>("%TitleLabel");
            _contentLabel = GetNode<RichTextLabel>("%ContentLabel");
            _closeButton = GetNode<Button>("%CloseButton");
            _upgradeButton = GetNode<Button>("%UpgradeButton");
            _costLabel = GetNode<Label>("%CostLabel");

            _closeButton.Pressed += QueueFree;
            _upgradeButton.Pressed += OnUpgradePressed;

            ApplyThemeStyle();
        }

        private void ApplyThemeStyle()
        {
            var panel = GetNode<PanelContainer>("CenterContainer/PanelContainer");
            if (panel == null) return;

            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.12f, 0.10f, 0.08f), // Deep dark brown
                BorderWidthLeft = 4,
                BorderWidthRight = 4,
                BorderWidthTop = 4,
                BorderWidthBottom = 4,
                BorderColor = new Color(0.4f, 0.35f, 0.25f), // Brass/Bronze/Tan
                CornerRadiusTopLeft = 10,
                CornerRadiusTopRight = 10,
                CornerRadiusBottomLeft = 10,
                CornerRadiusBottomRight = 10,
                ShadowSize = 20,
                ShadowColor = new Color(0, 0, 0, 0.5f),
                ContentMarginLeft = 30,
                ContentMarginRight = 30,
                ContentMarginTop = 30,
                ContentMarginBottom = 30
            };
            panel.AddThemeStyleboxOverride("panel", style);
        }

        public void ShowInfo(string title, string content)
        {
            _titleLabel.Text = title;
            _contentLabel.BbcodeEnabled = true;
            _contentLabel.Text = $"[center]{content}[/center]";
            _upgradeButton.Hide();
            _costLabel.Hide();
        }

        public void ShowFacility(string name, int level, string description)
        {
            _currentFacility = name;
            _titleLabel.Text = $"{name} Facility (Level {ToRoman(level)})";
            _contentLabel.BbcodeEnabled = true;
            _contentLabel.Text = description;

            if (level < 5)
            {
                int cost = UpgradeProject.CalculateCost(level + 1);
                int duration = (level + 1) * 2;

                _costLabel.Text = $"Cost: {cost} Merit | Est: {duration} days";
                _costLabel.Show();
                _upgradeButton.Show();

                var active = GameManager.Instance.ActiveUpgrade;
                if (active != null)
                {
                    _upgradeButton.Disabled = true;
                    _upgradeButton.Text = active.FacilityName == name ? "Upgrading..." : "Project Active";
                }
                else
                {
                    _upgradeButton.Disabled = GameManager.Instance.PlayerCaptain.Merit < cost;
                    _upgradeButton.Text = "Start Upgrade";
                }
            }
            else
            {
                _costLabel.Hide();
                _upgradeButton.Hide();
            }
        }

        private void OnUpgradePressed()
        {
            if (string.IsNullOrEmpty(_currentFacility)) return;
            GameManager.Instance.StartUpgrade(_currentFacility);
            QueueFree();
        }

        private string ToRoman(int number)
        {
            return number switch
            {
                1 => "I",
                2 => "II",
                3 => "III",
                4 => "IV",
                5 => "V",
                _ => number.ToString()
            };
        }
    }
}
