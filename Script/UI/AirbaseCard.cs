using Godot;
using System;
using AceManager.Core;

namespace AceManager.UI
{
	public partial class AirbaseCard : Control
	{
		private Label _nameLabel;
		private Label _locationLabel;
		private Label _ratingsLabel;
		private Label _resourcesLabel;
		private Label _notesLabel;
		private Button _closeButton;

		private AirbaseData _base;

		[Signal] public delegate void CardClosedEventHandler();

		public override void _Ready()
		{
			_nameLabel = GetNode<Label>("%NameLabel");
			_locationLabel = GetNode<Label>("%LocationLabel");
			_ratingsLabel = GetNode<Label>("%RatingsLabel");
			_resourcesLabel = GetNode<Label>("%ResourcesLabel");
			_notesLabel = GetNode<Label>("%NotesLabel");
			_closeButton = GetNode<Button>("%CloseButton");

			_closeButton.Pressed += OnClosePressed;
		}

		public void ShowBase(AirbaseData airbase)
		{
			_base = airbase;
			if (airbase == null) return;

			_nameLabel.Text = airbase.Name;
			_locationLabel.Text = $"{airbase.Location} | {airbase.Nation} | Active: {airbase.ActiveYears}";

			// Base ratings with visual bars
			_ratingsLabel.Text = $@"FACILITY RATINGS
{GetRatingBar("Runway", airbase.RunwayRating)}
{GetRatingBar("Lodging", airbase.LodgingRating)}
{GetRatingBar("Maintenance", airbase.MaintenanceRating)}
{GetRatingBar("Fuel Storage", airbase.FuelStorageRating)}
{GetRatingBar("Ammo Storage", airbase.AmmunitionStorageRating)}
{GetRatingBar("Operations", airbase.OperationsRating)}
{GetRatingBar("Medical", airbase.MedicalRating)}
{GetRatingBar("Transport", airbase.TransportAccessRating)}
{GetRatingBar("Training", airbase.TrainingFacilitiesRating)}";

			// Current resources
			int maxFuel = airbase.FuelStorageRating * 500;
			int maxAmmo = airbase.AmmunitionStorageRating * 300;
			_resourcesLabel.Text = $@"CURRENT RESOURCES
Fuel: {airbase.CurrentFuel:F0} / {maxFuel}
Ammo: {airbase.CurrentAmmo:F0} / {maxAmmo}
Spare Parts: {airbase.CurrentSpareParts}

BASE INFO
Archetype: {airbase.BaseArchetype}
Base Level: {airbase.BaseLevel}
Coordinates: {airbase.Coordinates.X:F2}, {airbase.Coordinates.Y:F2}";

			_notesLabel.Text = !string.IsNullOrEmpty(airbase.Notes) ? $"NOTES\n{airbase.Notes}" : "";

			Show();
		}

		private string GetRatingBar(string name, int rating)
		{
			string bar = new string('█', rating) + new string('░', 5 - rating);
			return $"{name,-14}: [{bar}] {rating}/5";
		}

		private void OnClosePressed()
		{
			EmitSignal(SignalName.CardClosed);
			Hide();
		}
	}
}
