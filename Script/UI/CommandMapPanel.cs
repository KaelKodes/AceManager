using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using AceManager.Core;

namespace AceManager.UI
{
    public partial class CommandMapPanel : Control
    {
        private Control _mapArea;
        private Label _sectorLabel;
        private Label _legendLabel;
        private Button _closeButton;
        private Control _markerContainer;
        private Control _windowBackground;
        private Control _panelContainer;
        private Control _mapBackground;
        private Control _legendPanel;
        private List<Line2D> _frontLines = new();
        private Line2D _missionDepartureLine;
        private Line2D _missionReturnLine;

        private MapData _mapData;
        private float _mapScale = 8f; // Pixels per km (default)
        private Vector2 _viewOffset = Vector2.Zero; // Current pan offset in KM
        private bool _isDragging = false;
        private bool _isMapActive = false;
        private Vector2 _lastMousePos;

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
            var bg = _mapArea.GetNode<Control>("MapBackground");
            if (bg != null) bg.MouseFilter = MouseFilterEnum.Ignore;

            _sectorLabel = GetNode<Label>("%SectorLabel");
            _legendLabel = GetNode<Label>("%LegendLabel");
            _closeButton = GetNode<Button>("%CloseButton");
            _windowBackground = GetNode<Control>("Background");
            _panelContainer = GetNode<Control>("Panel");
            _mapBackground = GetNode<Control>("%MapArea/MapBackground");
            _legendPanel = GetNodeOrNull<Control>("Panel/VBoxContainer/ContentHBox/LegendPanel");

            // Block input to background and allow focus
            MouseFilter = MouseFilterEnum.Stop;
            FocusMode = FocusModeEnum.All;
            _mapArea.FocusMode = FocusModeEnum.All;

            _closeButton.Pressed += OnClosePressed;
            _mapArea.Resized += () => CallDeferred(nameof(RebuildMap));

            _mapArea.GuiInput += OnMapInput;
            _mapArea.MouseDefaultCursorShape = CursorShape.Drag;
            _mapArea.FocusExited += () => SetMapActive(false);

            _markerContainer = new Control();
            _markerContainer.Name = "MarkerContainer";
            _markerContainer.MouseFilter = MouseFilterEnum.Ignore;
            _mapArea.AddChild(_markerContainer);

            // Mission Path lines
            _missionDepartureLine = new Line2D { Width = 2, DefaultColor = new Color(0.2f, 1.0f, 0.2f, 0.5f) };
            _missionReturnLine = new Line2D { Width = 2, DefaultColor = new Color(1.0f, 0.2f, 0.2f, 0.5f) };
            _mapArea.AddChild(_missionDepartureLine);
            _mapArea.AddChild(_missionReturnLine);

            // Build legend
            UpdateLegend();
        }

        public override void _Input(InputEvent @event)
        {
            if (!IsVisibleInTree()) return;

            // Consume all keyboard shortcuts/arrow keys while map is open
            if (@event is InputEventKey ek)
            {
                if (ek.Pressed && ek.Keycode == Key.Escape)
                {
                    OnClosePressed();
                }
                GetViewport().SetInputAsHandled();
            }
        }

        private void OnMapInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb)
            {
                if (mb.Pressed)
                {
                    if (mb.ButtonIndex == MouseButton.Left)
                    {
                        if (!_isMapActive)
                        {
                            _mapArea.GrabFocus();
                            SetMapActive(true);
                        }
                        _isDragging = true;
                        _lastMousePos = mb.Position;
                    }
                    else if (mb.ButtonIndex == MouseButton.WheelUp)
                    {
                        if (!_isMapActive) SetMapActive(true);
                        _mapScale = Math.Min(30f, _mapScale * 1.15f);
                        UpdateVisualPositions();
                        AcceptEvent();
                    }
                    else if (mb.ButtonIndex == MouseButton.WheelDown)
                    {
                        if (!_isMapActive) SetMapActive(true);
                        _mapScale = Math.Max(2f, _mapScale / 1.15f);
                        UpdateVisualPositions();
                        AcceptEvent();
                    }
                }
                else
                {
                    if (mb.ButtonIndex == MouseButton.Left)
                        _isDragging = false;
                }
            }
            else if (_isDragging && @event is InputEventMouseMotion mm)
            {
                if (!_isMapActive) return;

                Vector2 delta = mm.Relative / _mapScale;
                _viewOffset -= delta;
                UpdateVisualPositions();
                AcceptEvent();
            }
        }

        public void ShowMap(MapData mapData)
        {
            _mapData = mapData;
            if (mapData == null) return;

            _sectorLabel.Text = mapData.SectorName;
            _viewOffset = Vector2.Zero;
            _mapScale = 12f;

            Show();

            // Full rebuild on show
            RebuildMap();
        }

        private void SetMapActive(bool active)
        {
            _isMapActive = active;

            // Minimal feedback for full-screen background to avoid distracting from UI
            if (_panelContainer is PanelContainer pc)
            {
                pc.Modulate = active ? Colors.White : new Color(0.8f, 0.8f, 0.8f, 0.7f);
            }

            if (!active) _isDragging = false;
        }

        public void SetAsBackground(bool isBackground)
        {
            if (isBackground)
            {
                _closeButton.Hide();
                _sectorLabel.Hide();
                _legendLabel.Hide();
                _legendPanel?.Hide();
                _windowBackground?.Hide();
                _mapBackground?.Hide();

                // Set the panel to fill the background and be transparent
                SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                if (_panelContainer is PanelContainer pc)
                {
                    pc.SelfModulate = new Color(1, 1, 1, 0); // Transparent container
                    pc.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                    pc.AddThemeStyleboxOverride("panel", new StyleBoxEmpty()); // Remove padding

                    // Also make sure internal hierarchy doesn't squash the map
                    var vBox = pc.GetNodeOrNull<VBoxContainer>("VBoxContainer");
                    if (vBox != null)
                    {
                        vBox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                        vBox.AddThemeConstantOverride("separation", 0);
                    }

                    var contentHBox = pc.GetNodeOrNull<HBoxContainer>("VBoxContainer/ContentHBox");
                    if (contentHBox != null)
                    {
                        contentHBox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                        contentHBox.AddThemeConstantOverride("separation", 0);
                    }

                    if (_mapArea != null) _mapArea.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                }

                MouseFilter = MouseFilterEnum.Pass;
                if (_mapArea != null) _mapArea.MouseFilter = MouseFilterEnum.Stop;
                SetMapActive(false); // Start inactive

                // Set a tactical dark background color for the map area itself
                if (_mapArea != null)
                {
                    // If we have a dedicated background color Rect
                    var bg = _mapArea.GetNodeOrNull<ColorRect>("MapBackground");
                    if (bg != null)
                    {
                        bg.Color = new Color(0.05f, 0.08f, 0.05f, 1.0f);
                        bg.Show();
                    }
                }

                CallDeferred(nameof(RebuildMap));
            }
        }

        private void RebuildMap()
        {
            if (_mapData == null) return;
            // If size is still 0, we'll wait for the next Resized event which is already connected
            if (_mapArea.Size.X < 10) return;
            foreach (Node child in _markerContainer.GetChildren())
            {
                _markerContainer.RemoveChild(child);
                child.QueueFree();
            }

            // Clear front lines
            foreach (var line in _frontLines)
            {
                if (GodotObject.IsInstanceValid(line))
                {
                    line.GetParent()?.RemoveChild(line);
                    line.QueueFree();
                }
            }
            _frontLines.Clear();

            Vector2 areaSize = _mapArea.Size;
            if (areaSize.X < 10 || areaSize.Y < 10) return;

            Vector2 center = areaSize / 2;
            var homeLoc = _mapData.Locations.FirstOrDefault(l => l.Id == "home_base");
            Vector2 homeWorldPos = homeLoc?.WorldCoordinates ?? Vector2.Zero;
            Vector2 currentOrigin = homeWorldPos + _viewOffset;

            // Rebuild front lines
            if (_mapData.FrontLinePoints != null && _mapData.FrontLinePoints.Length > 1)
            {
                var line = new Line2D();
                line.Width = 4;
                line.DefaultColor = new Color(Colors.Orange, 0.6f); // More visible
                line.Antialiased = true;
                _mapArea.AddChild(line);
                _mapArea.MoveChild(line, 1); // Stay behind markers
                _frontLines.Add(line);
            }

            // Rebuild markers
            var discovered = _mapData.GetDiscoveredLocations();
            foreach (var loc in discovered)
            {
                var marker = CreateMarker(loc);
                marker.SetMeta("LocationId", loc.Id);
                _markerContainer.AddChild(marker);
            }

            UpdateVisualPositions();
        }

        private void UpdateVisualPositions()
        {
            if (_mapData == null) return;

            Vector2 areaSize = _mapArea.Size;
            Vector2 center = areaSize / 2;
            var homeLoc = _mapData.Locations.FirstOrDefault(l => l.Id == "home_base");
            Vector2 homeWorldPos = homeLoc?.WorldCoordinates ?? Vector2.Zero;
            Vector2 currentOrigin = homeWorldPos + _viewOffset;

            // Update front line points
            if (_mapData.FrontLinePoints != null && _frontLines.Count > 0)
            {
                var line = _frontLines[0];
                line.ClearPoints();
                foreach (var p in _mapData.FrontLinePoints)
                {
                    line.AddPoint(center + (p - currentOrigin) * _mapScale);
                }
            }

            // Update markers
            foreach (Control marker in _markerContainer.GetChildren())
            {
                string id = marker.GetMeta("LocationId").ToString();
                var loc = _mapData.Locations.FirstOrDefault(l => l.Id == id);
                if (loc == null) continue;

                Vector2 screenPos = center + (loc.WorldCoordinates - currentOrigin) * _mapScale;
                marker.Position = screenPos;

                // Culling + Label LOD
                bool isVisible = screenPos.X > -50 && screenPos.X < areaSize.X + 50 &&
                                 screenPos.Y > -50 && screenPos.Y < areaSize.Y + 50;
                marker.Visible = isVisible;

                if (isVisible)
                {
                    var label = marker.GetNodeOrNull<Label>("NameLabel");
                    if (label != null)
                    {
                        label.Visible = (_mapScale >= 10 || loc.Type == MapLocation.LocationType.HomeBase);
                    }
                }
            }

            UpdateMissionPath();
        }

        private List<Vector2> _previewWaypoints;

        public void DrawPreviewPath(List<Vector2> waypoints)
        {
            _previewWaypoints = waypoints;
            UpdateMissionPath();
        }

        private void UpdateMissionPath()
        {
            var waypoints = _previewWaypoints;

            // Fallback to current mission if no preview
            if (waypoints == null || waypoints.Count == 0)
            {
                var mission = GameManager.Instance.CurrentMission;
                if (mission != null) waypoints = mission.Waypoints;
            }

            if (waypoints == null || waypoints.Count < 2 || _mapData == null)
            {
                _missionDepartureLine.Visible = false;
                _missionReturnLine.Visible = false;
                return;
            }

            _missionDepartureLine.Visible = true;
            _missionReturnLine.Visible = true;
            _missionDepartureLine.Width = 3;
            _missionReturnLine.Width = 3;
            _missionDepartureLine.ClearPoints();
            _missionReturnLine.ClearPoints();

            Vector2 areaSize = _mapArea.Size;
            Vector2 center = areaSize / 2;
            var homeLoc = _mapData.Locations.FirstOrDefault(l => l.Id == "home_base");
            Vector2 homeWorldPos = homeLoc?.WorldCoordinates ?? Vector2.Zero;
            Vector2 currentOrigin = homeWorldPos + _viewOffset;

            // Generate arcs for better visualization
            // Departure: Home -> Waypoint1 -> ... -> Target
            var departurePoints = new List<Vector2>();
            for (int i = 0; i < waypoints.Count - 2; i++) // Excluding the return-to-home part
            {
                departurePoints.Add(waypoints[i]);
            }
            departurePoints.Add(waypoints[waypoints.Count - 2]); // The target (last waypoint before home)

            AddArcedPoints(_missionDepartureLine, departurePoints, currentOrigin, center);

            // Return: Target -> Home
            var returnPoints = new List<Vector2> {
                waypoints[waypoints.Count - 2],
                waypoints[waypoints.Count - 1]
            };
            AddArcedPoints(_missionReturnLine, returnPoints, currentOrigin, center);
        }

        private void AddArcedPoints(Line2D line, List<Vector2> waypoints, Vector2 origin, Vector2 center)
        {
            if (waypoints.Count < 2) return;

            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                Vector2 start = center + (waypoints[i] - origin) * _mapScale;
                Vector2 end = center + (waypoints[i + 1] - origin) * _mapScale;

                // Add an arc between points
                float dist = start.DistanceTo(end);
                if (dist < 10)
                {
                    line.AddPoint(start);
                    continue;
                }

                Vector2 mid = (start + end) / 2;
                Vector2 dir = (end - start).Normalized();
                Vector2 normal = new Vector2(-dir.Y, dir.X);

                // Arc height proportional to distance
                float h = dist * 0.15f;
                Vector2 control = mid + normal * h;

                // Simple Quadratic Bezier
                for (int t = 0; t <= 10; t++)
                {
                    float f = t / 10f;
                    Vector2 p = (1 - f) * (1 - f) * start + 2 * (1 - f) * f * control + f * f * end;
                    line.AddPoint(p);
                }
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
            symbolLabel.AddThemeFontSizeOverride("font_size", 22); // Larger symbol
            symbolLabel.Position = new Vector2(-11, -15);
            container.AddChild(symbolLabel);

            var nameLabel = new Label();
            nameLabel.Name = "NameLabel";
            nameLabel.Text = location.Name;
            nameLabel.AddThemeColorOverride("font_color", color);
            nameLabel.AddThemeFontSizeOverride("font_size", 12); // Larger font
            nameLabel.Position = new Vector2(12, -8);
            container.AddChild(nameLabel);

            // Tooltip with notes
            if (!string.IsNullOrEmpty(location.Notes))
            {
                container.TooltipText = location.Notes;
            }

            // Ensure markers don't catch focus or block map input
            container.FocusMode = FocusModeEnum.None;
            container.MouseFilter = MouseFilterEnum.Ignore;

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
