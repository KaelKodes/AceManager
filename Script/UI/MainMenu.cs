using Godot;
using System;
using AceManager.Core;

namespace AceManager.UI
{
	public partial class MainMenu : Control
	{
		private Button _newGameButton;
		private Button _loadGameButton;
		private Button _quitButton;
		private Control _menuContainer;
		private Control _overlayContainer;

		public override void _Ready()
		{
			_menuContainer = GetNode<Control>("MenuContainer");
			_overlayContainer = GetNode<Control>("%OverlayContainer");
			_newGameButton = GetNode<Button>("%NewGameButton");
			_loadGameButton = GetNode<Button>("%LoadGameButton");
			_quitButton = GetNode<Button>("%QuitButton");

			_newGameButton.Pressed += OnNewGamePressed;
			_loadGameButton.Pressed += OnLoadGamePressed;
			_quitButton.Pressed += OnQuitPressed;
		}

		private void OnNewGamePressed()
		{
			GD.Print("Main Menu: New Game requested.");
			_menuContainer.Visible = false; // Hide menu buttons

			var nationSelectScene = GD.Load<PackedScene>("res://Scene/UI/NationSelectionPanel.tscn");
			var nationSelect = nationSelectScene.Instantiate<NationSelectionPanel>();
			_overlayContainer.AddChild(nationSelect);

			nationSelect.NationSelected += OnNationSelected;
		}

		private void OnNationSelected(string nation)
		{
			GD.Print($"Main Menu: Nation selected: {nation}. Transitioning to Introduction.");
			// Nation panel closes itself (queuefree) triggers automatically or logic in panel.
			// NationSelectionPanel hides and queues free on select.

			var introScene = GD.Load<PackedScene>("res://Scene/UI/IntroductionPanel.tscn");
			var intro = introScene.Instantiate<IntroductionPanel>();
			_overlayContainer.AddChild(intro);

			intro.Setup(nation);
			intro.CommissionAccepted += OnCommissionAccepted;
		}

		private void OnCommissionAccepted(string captainName)
		{
			GD.Print($"Main Menu: Commission accepted by {captainName}. Starting Game.");

			GameManager.Instance.FinalizeCampaignStart(captainName);

			// Transition to Main HUD
			GetTree().ChangeSceneToFile("res://Scene/MainHUD.tscn");
		}

		private void OnLoadGamePressed()
		{
			GD.Print("Main Menu: Load Game requested (Placeholder).");
			// TODO: Open Load Dialog
		}

		private void OnQuitPressed()
		{
			GD.Print("Main Menu: Quitting.");
			GetTree().Quit();
		}
	}
}
