using Godot;
using System;
using AceManager.Core;

namespace AceManager.UI
{
	public partial class PilotCard : Control
	{
		private Label _nameLabel;
		private Label _roleLabel;
		private Label _statsLabel;
		private Label _ratingsLabel;
		private Label _recordLabel;
		private Button _closeButton;

		private CrewData _pilot;

		[Signal] public delegate void CardClosedEventHandler();

		public override void _Ready()
		{
			_nameLabel = GetNode<Label>("%NameLabel");
			_roleLabel = GetNode<Label>("%RoleLabel");
			_statsLabel = GetNode<Label>("%StatsLabel");
			_ratingsLabel = GetNode<Label>("%RatingsLabel");
			_recordLabel = GetNode<Label>("%RecordLabel");
			_closeButton = GetNode<Button>("%CloseButton");

			_closeButton.Pressed += OnClosePressed;
		}

		public void ShowPilot(CrewData pilot)
		{
			_pilot = pilot;
			if (pilot == null) return;

			_nameLabel.Text = pilot.Name;
			_roleLabel.Text = pilot.Role;

			// Core Stats
			_statsLabel.Text = $@"CORE STATS
Control (CTL): {pilot.CTL}
Gunnery (GUN): {pilot.GUN}
Energy Mgmt (ENG): {pilot.ENG}
Reactions (RFX): {pilot.RFX}
Off. Awareness (OA): {pilot.OA}
Def. Awareness (DA): {pilot.DA}
Team Awareness (TA): {pilot.TA}
Wingman (WI): {pilot.WI}
Aggression (AGG): {pilot.AGG}
Discipline (DIS): {pilot.DIS}
Composure (CMP): {pilot.CMP}
Adaptability (ADP): {pilot.ADP}
Leadership (LDR): {pilot.LDR}
Learning (LRN): {pilot.LRN}
Stamina (STA): {pilot.STA}";

			// Derived Ratings
			_ratingsLabel.Text = $@"COMBAT RATINGS
Dogfight: {pilot.GetDogfightRating():F1}
Energy Fighter: {pilot.GetEnergyFighterRating():F1}
Ground Attack: {pilot.GetGroundAttackRating():F1}
Recon Survival: {pilot.GetReconSurvivalRating():F1}

SPECIAL SKILLS
{(pilot.HasSkill("ace") ? "★ Ace Pilot" : "")}
{(pilot.HasSkill("wingman") ? "★ Expert Wingman" : "")}
{(pilot.HasSkill("steady") ? "★ Steady Under Fire" : "")}
{(pilot.HasSkill("survivor") ? "★ Natural Survivor" : "")}".Trim();

			// TODO: Add mission/kill tracking when implemented
			_recordLabel.Text = "SERVICE RECORD\nMissions: --\nKills: --\nWounds: --";

			Show();
		}

		private void OnClosePressed()
		{
			EmitSignal(SignalName.CardClosed);
			Hide();
		}
	}
}
