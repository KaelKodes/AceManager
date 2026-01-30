using Godot;
using System;
using System.Collections.Generic;
using AceManager.Core;

namespace AceManager.UI
{
	public partial class CommandMapPanel : Control
	{
		private Control _mapArea;
		private Label _sectorLabel;
		private Label _legendLabel;
		private Button _closeButton;

		private MapData _mapData;
		private float _mapScale = 5f; // Pixels per km

		private Dictionary<MapLocation.LocationType, Color> _typeColors = new()
		{
			{ MapLocation.LocationType.HomeBase, Colors.Green },
			{ MapLocation.LocationType.AlliedBase, Colors.LightGreen },
			{ MapLocation.LocationType.SupplyDepot, Colors.Cyan },
			{ MapLocation.LocationType.Hospital, Colors.White },
			{ MapLocation.LocationType.EnemyAirfield, Colors.Red },
			{ MapLocation.LocationType.EnemyPosition, Colors.OrangeRed },
			{ MapLocation.LocationType.Bridge, Colors.Yellow },
			{ MapLocation.LocationType.RailYard, Colors.Purple },
			{ MapLocation.LocationType.Factory, Colors.Magenta },
			{ MapLocation.LocationType.Town, Colors.Gray },
			{ MapLocation.LocationType.FrontLine, Colors.Orange },
			{ MapLocation.LocationType.Unknown, Colors.DarkGray }
		};

		[Signal] public delegate void PanelClosedEventHandler();

		public override void _Ready()
		{
			_mapArea = GetNode<Control>("%MapArea");
			_sectorLabel = GetNode<Label>("%SectorLabel");
			_legendLabel = GetNode<Label>("%LegendLabel");
			_closeButton = GetNode<Button>("%CloseButton");

			_closeButton.Pressed += OnClosePressed;

			// Build legend
			UpdateLegend();
		}

		public void ShowMap(MapData mapData)
		{
			_mapData = mapData;
			if (mapData == null) return;

			_sectorLabel.Text = mapData.SectorName;
			
			// Clear existing markers
			foreach (Node child in _mapArea.GetChildren())
			{
				if (child.Name.ToString().StartsWith("Marker_") || child.Name.ToString().StartsWith("Line_"))
				{
					child.QueueFree();
				}
			}

			// After frame, draw the map
			CallDeferred(nameof(DrawMap));
			Show();
		}

		private void DrawMap()
		{
			if (_mapData == null) return;

			Vector2 center = _mapArea.Size / 2;

			// Draw front line
			if (_mapData.FrontLinePoints != null && _mapData.FrontLinePoints.Length > 1)
			{
				for (int i = 0; i < _mapData.FrontLinePoints.Length - 1; i++)
				{
					var line = new Line2D();
					line.Name = $"Line_Front_{i}";
					line.AddPoint(center + _mapData.FrontLinePoints[i] * _mapScale);
					line.AddPoint(center + _mapData.FrontLinePoints[i + 1] * _mapScale);
					line.Width = 3;
					line.DefaultColor = Colors.Orange;
					_mapArea.AddChild(line);
				}

				// Add "FRONT" label
				var frontLabel = new Label();
				frontLabel.Name = "Marker_FrontLabel";
				frontLabel.Text = "═══ FRONT LINE ═══";
				frontLabel.Position = center + new Vector2(38 * _mapScale, 0);
				frontLabel.Rotation = -0.5f;
				frontLabel.AddThemeColorOverride("font_color", Colors.Orange);
				_mapArea.AddChild(frontLabel);
			}

			// Draw discovered locations
			foreach (var loc in _mapData.GetDiscoveredLocations())
			{
				var marker = CreateMarker(loc);
				marker.Position = center + loc.Coordinates * _mapScale;
				_mapArea.AddChild(marker);
			}
		}

		private Control CreateMarker(MapLocation location)
		{
			var container = new Control();
			container.Name = $"Marker_{location.Id}";

			var color = _typeColors.GetValueOrDefault(location.Type, Colors.Gray);
			
			// Symbol based on type
			string symbol = location.Type switch
			{
				MapLocation.LocationType.HomeBase => "★",
				MapLocation.LocationType.AlliedBase => "✦",
				MapLocation.LocationType.SupplyDepot => "◆",
				MapLocation.LocationType.Hospital => "✚",
				MapLocation.LocationType.EnemyAirfield => "✈",
				MapLocation.LocationType.EnemyPosition => "⚔",
				MapLocation.LocationType.Bridge => "⌇",
				MapLocation.LocationType.RailYard => "═",
				MapLocation.LocationType.Factory => "▣",
				MapLocation.LocationType.Town => "●",
				_ => "?"
			};

			var symbolLabel = new Label();
			symbolLabel.Text = symbol;
			symbolLabel.AddThemeColorOverride("font_color", color);
			symbolLabel.AddThemeFontSizeOverride("font_size", 20);
			symbolLabel.Position = new Vector2(-8, -12);
			container.AddChild(symbolLabel);

			var nameLabel = new Label();
			nameLabel.Text = location.Name;
			nameLabel.AddThemeColorOverride("font_color", color);
			nameLabel.AddThemeFontSizeOverride("font_size", 10);
			nameLabel.Position = new Vector2(10, -6);
			container.AddChild(nameLabel);

			// Tooltip with notes
			if (!string.IsNullOrEmpty(location.Notes))
			{
				container.TooltipText = location.Notes;
			}

			return container;
		}

		private void UpdateLegend()
		{
			_legendLabel.Text = @"LEGEND
★ Home Base
✦ Allied Base
◆ Supply Depot
✚ Hospital
✈ Enemy Airfield
⚔ Enemy Position
═══ Front Line";
		}

		private void OnClosePressed()
		{
			EmitSignal(SignalName.PanelClosed);
			Hide();
		}
	}
}
