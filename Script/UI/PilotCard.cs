using Godot;
using System;
using AceManager.Core;

namespace AceManager.UI
{
    public partial class PilotCard : Control
    {
        private Label _nameLabel;
        private Label _roleLabel;
        private RichTextLabel _statsLabel;
        private RichTextLabel _ratingsLabel;
        private RichTextLabel _recordLabel;
        private Button _closeButton;
        private Button _viewLogButton;
        private PilotCard _pilotCard; // Self reference not needed but I'll use it for structure if needed
        private PilotLogPanel _logPanel;

        private CrewData _pilot;

        [Signal] public delegate void CardClosedEventHandler();

        public override void _Ready()
        {
            _nameLabel = GetNode<Label>("%NameLabel");
            _roleLabel = GetNode<Label>("%RoleLabel");
            _statsLabel = GetNode<RichTextLabel>("%StatsLabel");
            _ratingsLabel = GetNode<RichTextLabel>("%RatingsLabel");
            _recordLabel = GetNode<RichTextLabel>("%RecordLabel");
            _closeButton = GetNode<Button>("%CloseButton");

            // Add View Log Button dynamically if it doesn't exist in the scene
            // Or assume it will be added to the scene later. For now, I'll add it code-side.
            var footer = _closeButton.GetParent() as BoxContainer;
            if (footer != null)
            {
                _viewLogButton = new Button { Text = "VIEW MISSION LOG", CustomMinimumSize = new Vector2(180, 40) };
                footer.AddChild(_viewLogButton);
                footer.MoveChild(_viewLogButton, 0); // Put it before the close button
                _viewLogButton.Pressed += OnViewLogPressed;
            }

            _closeButton.Pressed += OnClosePressed;

            ApplyThemeStyle();
        }

        private void ApplyThemeStyle()
        {
            var panel = GetNode<PanelContainer>("Panel");
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
                ContentMarginLeft = 20,
                ContentMarginRight = 20,
                ContentMarginTop = 20,
                ContentMarginBottom = 20
            };
            panel.AddThemeStyleboxOverride("panel", style);
        }

        private void OnViewLogPressed()
        {
            if (_logPanel == null)
            {
                _logPanel = new PilotLogPanel();
                AddChild(_logPanel);
            }
            _logPanel.DisplayLog(_pilot);
        }

        public void ShowPilot(CrewData pilot)
        {
            _pilot = pilot;
            if (pilot == null) return;

            _nameLabel.Text = pilot.Name;
            _roleLabel.Text = pilot.Role;

            // Core Stats with Tooltips
            _statsLabel.Text = "[b]CORE STATS[/b]\n" +
                $"[hint=Handling and maneuverability. Affects recovery from stalls.]CTL: {pilot.CTL}[/hint]\n" +
                $"[hint=Marksmanship and aim. Affects kill probability.]GUN: {pilot.GUN}[/hint]\n" +
                $"[hint=Speed and altitude management.]ENG: {pilot.ENG}[/hint]\n" +
                $"[hint=Split-second response time. Affects initiative.]RFX: {pilot.RFX}[/hint]\n" +
                $"[hint=Spotting and tracking targets.]OA: {pilot.OA}[/hint]\n" +
                $"[hint=Defensive alertness and threat detection.]DA: {pilot.DA}[/hint]\n" +
                $"[hint=Wingman and formation awareness.]TA: {pilot.TA}[/hint]\n" +
                $"[hint=Protection and support of flight leader.]WI: {pilot.WI}[/hint]\n" +
                $"[hint=Offensive drive and persistence.]AGG: {pilot.AGG}[/hint]\n" +
                $"[hint=Following orders and discipline.]DIS: {pilot.DIS}[/hint]\n" +
                $"[hint=Performance under stress and fire.]CMP: {pilot.CMP}[/hint]\n" +
                $"[hint=Learning speed for new mission roles.]ADP: {pilot.ADP}[/hint]\n" +
                $"[hint=Command presence and inspiration.]LDR: {pilot.LDR}[/hint]\n" +
                $"[hint=Rate of experience gain.]LRN: {pilot.LRN}[/hint]\n" +
                $"[hint=Fatigue resistance and physical endurance.]STA: {pilot.STA}[/hint]";

            // Derived Ratings with Tooltips
            _ratingsLabel.Text = "[b]COMBAT RATINGS[/b]\n" +
                $"[hint=Close-in maneuvering combat (CTL, GUN, OA, RFX, ENG)]Dogfight: {pilot.GetDogfightRating():F1}[/hint]\n" +
                $"[hint=High-speed diving and climbing attacks (ENG, CTL, OA, DIS, CMP)]Energy Fighter: {pilot.GetEnergyFighterRating():F1}[/hint]\n" +
                $"[hint=Strafing and bombing surface targets (DIS, GUN, CTL, CMP, STA)]Ground Attack: {pilot.GetGroundAttackRating():F1}[/hint]\n" +
                $"[hint=Spanning avoidance and return safety (DA, OA, ADP, CMP)]Recon Survival: {pilot.GetReconSurvivalRating():F1}[/hint]\n\n" +
                "[b]SPECIAL SKILLS[/b]\n" +
                (pilot.HasSkill("ace") ? "[hint=Expert in finding and engaging enemy aircraft.]★ Ace Pilot[/hint]\n" : "") +
                (pilot.HasSkill("wingman") ? "[hint=Specialized in protecting the flight leader.]★ Expert Wingman[/hint]\n" : "") +
                (pilot.HasSkill("steady") ? "[hint=Remains cool even in high-threat situations.]★ Steady Under Fire[/hint]\n" : "") +
                (pilot.HasSkill("survivor") ? "[hint=Higher probability of surviving crashes.]★ Natural Survivor[/hint]\n" : "").Trim();

            // Status & Service Record
            string statusText = pilot.Status == PilotStatus.Active ? "[color=green]Active[/color]" :
                             pilot.Status == PilotStatus.Wounded ? $"[color=yellow]Wounded ({pilot.RecoveryDays} days)[/color]" :
                             pilot.Status == PilotStatus.Hospitalized ? $"[color=orange]Hospitalized ({pilot.RecoveryDays} days)[/color]" :
                             "[color=red]KIA[/color]";

            _recordLabel.Text = $"STATUS: {statusText}\n\n[b]SERVICE RECORD[/b]\nMissions: {pilot.MissionsFlown}\nKills: {pilot.AerialVictories}\nGround: {pilot.GroundTargetsDestroyed}";

            // Traits with Tooltips
            string traitsText = "\n[b]TRAITS[/b]\n";
            foreach (var t in pilot.PositiveTraits)
                traitsText += $"[color=green][hint={t.Description}]★ {t.TraitName}[/hint][/color]\n";
            foreach (var t in pilot.NegativeTraits)
                traitsText += $"[color=red][hint={t.Description}]✖ {t.TraitName}[/hint][/color]\n";

            _ratingsLabel.Text += traitsText;

            Show();
        }

        private void OnClosePressed()
        {
            EmitSignal(SignalName.CardClosed);
            Hide();
        }
    }
}
