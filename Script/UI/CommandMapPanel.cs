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
        private Control _legendPanel;
        private List<Line2D> _frontLines = new();
        private Line2D _missionDepartureLine;
        private Line2D _missionReturnLine;
        private TextureRect _backdrop;

        // Calibration for the provided Western Front map image
        // Adjusted to match aspect ratio (1.34) and visual alignment.
        private const float MapMax_Lat = 52.2f; // Shifted North and expanded
        private const float MapMin_Lat = 48.0f; // Shifted South and expanded
        private const float MapMin_Lon = -1.5f; // Shifted West and expanded
        private const float MapMax_Lon = 7.8f;  // Shifted East and expanded

        private Vector2 _worldMinKM; // Top-Left (NW)
        private Vector2 _worldMaxKM; // Bottom-Right (SE)
        private Vector2 _worldSizeKM;

        private MapData _mapData;
        private float _mapScale = 8f; // Pixels per km (default)
        private Vector2 _viewOffset = Vector2.Zero; // Current pan offset in KM
        private bool _isDragging = false;
        private bool _isMapActive = false;
        private Vector2 _lastMousePos;
        private Vector2 _homeWorldPos = Vector2.Zero;

        // Debug Editor State
        // Debug Editor State
        private bool _debugEditMode = false; // Calibration complete

        // Base Calibration Variables
        private float _baseLonOffset = -0.5f; // Global shift (deg) (Final)
        private float _baseLatOffset = 0.2f;  // Global vertical shift (deg) (Final)
        private float _baseLonSpread = 1.0f;  // Lon Spread multiplier (Final)
        private float _baseLatSpread = 0.9f;  // Lat Spread multiplier (Final)
        private Label _calibrationLabel;

        private Control _debugContainer;
        private List<Control> _debugHandles = new();
        private int _draggedHandleIndex = -1;

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
            _windowBackground = GetNode<Control>("Background");
            _panelContainer = GetNode<Control>("Panel");
            _legendPanel = GetNodeOrNull<Control>("Panel/VBoxContainer/ContentHBox/LegendPanel");

            // Setup Backdrop
            _backdrop = new TextureRect();
            _backdrop.Texture = ResourceLoader.Load<Texture2D>("res://Assets/UI/Map/WesternFrontBackground.jpg");
            _backdrop.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _backdrop.StretchMode = TextureRect.StretchModeEnum.Scale;
            _backdrop.MouseFilter = MouseFilterEnum.Ignore;
            _backdrop.SelfModulate = new Color(1, 1, 1, 1);
            _mapArea.AddChild(_backdrop);
            _mapArea.MoveChild(_backdrop, 0); // Put it at the bottom

            // Disable clipping for now to see where the map goes if it fails
            _mapArea.ClipContents = false;

            // Calculate KM bounds
            float lonScale = 71f;
            float latScale = 111f;
            _worldMinKM = new Vector2(MapMin_Lon * lonScale, -MapMax_Lat * latScale); // NW corner
            _worldMaxKM = new Vector2(MapMax_Lon * lonScale, -MapMin_Lat * latScale); // SE corner
            _worldSizeKM = _worldMaxKM - _worldMinKM;

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

            // Debug container for handles
            _debugContainer = new Control();
            _debugContainer.Name = "DebugHandles";
            _debugContainer.MouseFilter = MouseFilterEnum.Pass;
            _mapArea.AddChild(_debugContainer);

            // Print Export Button (temporary UI hack, usually would be a separate Scene)
            if (_debugEditMode)
            {
                CreateCalibrationUI();
            }

            // Build legend
            UpdateLegend();

            GameManager.Instance.BriefingReady += OnBriefingReady;
            GameManager.Instance.MissionCompleted += UpdateVisualPositions;
            GameManager.Instance.DayAdvanced += OnDayAdvanced;

            // Initial Draw
            CallDeferred(nameof(UpdateVisualPositions));
        }

        private void OnBriefingReady()
        {
            UpdateVisualPositions();
        }
        private void CreateCalibrationUI()
        {
            var vbox = new VBoxContainer();
            vbox.Position = new Vector2(10, 50);
            AddChild(vbox);

            var hbox = new HBoxContainer();
            hbox.AddThemeConstantOverride("separation", 10);
            vbox.AddChild(hbox);

            // Shift Buttons X (Update calls)
            var btnShiftLeft = new Button { Text = "<< X" };
            btnShiftLeft.Pressed += () => AdjustCalibration(-0.05f, 0, 0, 0);
            hbox.AddChild(btnShiftLeft);

            var btnShiftRight = new Button { Text = "X >>" };
            btnShiftRight.Pressed += () => AdjustCalibration(0.05f, 0, 0, 0);
            hbox.AddChild(btnShiftRight);

            // Shift Buttons Y
            var btnShiftUp = new Button { Text = "^ Y" };
            btnShiftUp.Pressed += () => AdjustCalibration(0, 0, 0.05f, 0);
            hbox.AddChild(btnShiftUp);

            var btnShiftDown = new Button { Text = "v Y" };
            btnShiftDown.Pressed += () => AdjustCalibration(0, 0, -0.05f, 0);
            hbox.AddChild(btnShiftDown);

            // Spread Buttons
            var hbox2 = new HBoxContainer();
            hbox2.AddThemeConstantOverride("separation", 10);
            vbox.AddChild(hbox2);

            var btnSpreadIn = new Button { Text = ">< SQUISH X" };
            btnSpreadIn.Pressed += () => AdjustCalibration(0, -0.05f, 0, 0);
            hbox2.AddChild(btnSpreadIn);

            var btnSpreadOut = new Button { Text = "<> SPREAD X" };
            btnSpreadOut.Pressed += () => AdjustCalibration(0, 0.05f, 0, 0);
            hbox2.AddChild(btnSpreadOut);

            // Vertical Spread Buttons
            var hbox3 = new HBoxContainer();
            hbox3.AddThemeConstantOverride("separation", 10);
            vbox.AddChild(hbox3);

            var btnVSpreadIn = new Button { Text = ">< SQUISH Y" };
            btnVSpreadIn.Pressed += () => AdjustCalibration(0, 0, 0, -0.05f);
            hbox3.AddChild(btnVSpreadIn);

            var btnVSpreadOut = new Button { Text = "<> SPREAD Y" };
            btnVSpreadOut.Pressed += () => AdjustCalibration(0, 0, 0, 0.05f);
            hbox3.AddChild(btnVSpreadOut);

            // Print Button
            var btnPrint = new Button { Text = "PRINT VALUES" };
            btnPrint.Pressed += () => GD.Print($"OFFSET_X: {_baseLonOffset:F3}, OFFSET_Y: {_baseLatOffset:F3}, SPREAD_X: {_baseLonSpread:F3}, SPREAD_Y: {_baseLatSpread:F3}");
            vbox.AddChild(btnPrint);

            // Export Frontline Button
            var btnExport = new Button { Text = "EXPORT PRECISE COORDS" };
            btnExport.Pressed += ExportFrontlineCoords;
            btnExport.Modulate = Colors.GreenYellow;
            vbox.AddChild(btnExport);

            // Label
            _calibrationLabel = new Label();
            _calibrationLabel.Text = $"X: {_baseLonOffset:F2} | Y: {_baseLatOffset:F2} | SprX: {_baseLonSpread:F2} | SprY: {_baseLatSpread:F2}";
            vbox.AddChild(_calibrationLabel);
        }

        private void AdjustCalibration(float xDelta, float spreadXDelta, float yDelta, float spreadYDelta)
        {
            _baseLonOffset += xDelta;
            _baseLonSpread += spreadXDelta;
            _baseLatOffset += yDelta;
            _baseLatSpread += spreadYDelta;

            if (_calibrationLabel != null)
                _calibrationLabel.Text = $"X: {_baseLonOffset:F2} | Y: {_baseLatOffset:F2} | SprX: {_baseLonSpread:F2} | SprY: {_baseLatSpread:F2}";
            UpdateVisualPositions();
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
                        // Initial Draw
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

        public void ShowMap(MapData data)
        {
            _mapData = data;
            _homeWorldPos = Vector2.Zero; // Reset so it re-resolves on next draw
            if (data == null) return;

            _sectorLabel.Text = data.SectorName;
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
                // Always fully visible
                pc.Modulate = Colors.White;
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

                // Extra safety: manually ensure the legacy ColorRect is gone
                if (_windowBackground != null) _windowBackground.Visible = false;

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

            if (_homeWorldPos == Vector2.Zero)
            {
                var homeLoc = _mapData.Locations.FirstOrDefault(l => l.Id == "home_base");
                _homeWorldPos = homeLoc?.WorldCoordinates ?? Vector2.Zero;
            }

            Vector2 center = areaSize / 2;
            Vector2 currentOrigin = _homeWorldPos + _viewOffset;

            // Rebuild front lines
            if (_mapData.FrontLinePoints != null && _mapData.FrontLinePoints.Length > 1)
            {
                var line = new Line2D();
                line.Width = 4;
                line.DefaultColor = new Color(Colors.Red, 0.8f); // High visibility red
                line.Antialiased = true;
                _mapArea.AddChild(line);
                _mapArea.MoveChild(line, _backdrop != null ? 1 : 0); // Stay behind markers but over backdrop
                _frontLines.Add(line);
            }

            // Rebuild markers
            var discovered = _mapData.GetDiscoveredLocations();
            foreach (var loc in discovered)
            {
                // Visual Polish: Hide generic frontline towns
                if (loc.Type == MapLocation.LocationType.Town && loc.Name == "Frontline Town") continue;
                var marker = CreateMarker(loc);
                marker.SetMeta("LocationId", loc.Id);
                _markerContainer.AddChild(marker);
            }
        }

        private void UpdateVisualPositions()
        {
            if (_mapData == null) return;

            Vector2 areaSize = _mapArea.Size;
            Vector2 center = areaSize / 2;

            if (_homeWorldPos == Vector2.Zero)
            {
                var hLoc = _mapData.Locations.FirstOrDefault(l => l.Id == "home_base");
                _homeWorldPos = hLoc?.WorldCoordinates ?? Vector2.Zero;
            }

            Vector2 currentOrigin = _homeWorldPos + _viewOffset;

            // Update backdrop
            if (_backdrop != null)
            {
                // Ensure the backdrop follows the exact same transformation as markers and lines
                _backdrop.Size = _worldSizeKM * _mapScale;
                _backdrop.Position = center + (_worldMinKM - currentOrigin) * _mapScale;
                _backdrop.Visible = true;
            }

            // Update front line points
            if (_mapData.FrontLinePoints != null && _frontLines.Count > 0)
            {
                var line = _frontLines[0];
                line.ClearPoints();
                foreach (var p in _mapData.FrontLinePoints)
                {
                    // Use Unified Transform so Frontline disperses with bases
                    line.AddPoint(GetVisualPosition(p));
                }
            }

            // Update markers
            foreach (Control marker in _markerContainer.GetChildren())
            {
                string id = marker.GetMeta("LocationId").ToString();
                var loc = _mapData.Locations.FirstOrDefault(l => l.Id == id);
                if (loc == null) continue;

                Vector2 screenPos = GetVisualPosition(loc.WorldCoordinates);
                marker.Position = screenPos;

                // Culling + Label LOD
                bool isVisible = screenPos.X > -200 && screenPos.X < areaSize.X + 200 &&
                                 screenPos.Y > -200 && screenPos.Y < areaSize.Y + 200;
                marker.Visible = isVisible;

                if (isVisible && marker is Control c && c.GetChildCount() > 0 && c.GetChild(0) is Label lbl)
                {
                    lbl.Visible = _mapScale > 1.5f; // Show labels when zoomed in
                }
            }

            // Update Debug Handles
            if (_debugEditMode)
            {
                UpdateDebugHandles();

                // --- CALIBRATION ANCHOR (Nieuport) ---
                // Real World Nieuport Coordinates: 2.75 E, 51.13 N
                Vector2 nieuportReal = _mapData.GetWorldCoordinates(new Vector2(2.75f, 51.13f));
                Vector2 anchorScreen = GetVisualPosition(nieuportReal);

                var anchor = _debugContainer.GetNodeOrNull<ColorRect>("Anchor_Nieuport");
                if (anchor == null)
                {
                    anchor = new ColorRect();
                    anchor.Name = "Anchor_Nieuport";
                    anchor.Size = new Vector2(14, 14);
                    anchor.Color = Colors.Magenta; // Bright signal color
                    anchor.PivotOffset = new Vector2(7, 7);

                    var lbl = new Label();
                    lbl.Text = "NIEUPORT (ANCHOR)";
                    lbl.AddThemeColorOverride("font_color", Colors.Magenta);
                    lbl.Position = new Vector2(15, -10);
                    anchor.AddChild(lbl);

                    _debugContainer.AddChild(anchor);
                }
                anchor.Position = anchorScreen - new Vector2(7, 7);
                anchor.Rotation += (float)GetProcessDeltaTime() * 2f; // Spin it for visibility
            }

            UpdateMissionPath();
        }

        private Vector2 GetVisualPosition(Vector2 worldPosKM)
        {
            // Dispersion Logic
            float pivotLon = 3.5f;     // Center of the Front
            float pivotX = _mapData.GetWorldCoordinates(new Vector2(pivotLon, 50)).X;

            // 1. Spread around pivot
            float distFromPivotX = worldPosKM.X - pivotX;
            float spreadX = pivotX + (distFromPivotX * _baseLonSpread);

            // Vertical Dispersion (Pivot around 50N approx, converted to KM)
            // 50N is roughly middle of our map
            float pivotY = _mapData.GetWorldCoordinates(new Vector2(0, 50.0f)).Y;
            float distFromPivotY = worldPosKM.Y - pivotY;
            float spreadY = pivotY + (distFromPivotY * _baseLatSpread);

            // 2. Apply Global Offset (converted to km)
            Vector2 offsetKM = _mapData.GetWorldCoordinates(new Vector2(_baseLonOffset, _baseLatOffset));

            Vector2 adjustedWorld = new Vector2(spreadX + offsetKM.X, spreadY + offsetKM.Y);

            // 3. Screen Transform
            Vector2 center = _mapArea.Size / 2;
            Vector2 currentOrigin = _homeWorldPos + _viewOffset;

            return center + (adjustedWorld - currentOrigin) * _mapScale;
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

            // Generate arcs for better visualization
            // Departure: Home -> Waypoint1 -> ... -> Target
            var departurePoints = new List<Vector2>();
            for (int i = 0; i < waypoints.Count - 2; i++) // Excluding the return-to-home part
            {
                departurePoints.Add(waypoints[i]);
            }
            departurePoints.Add(waypoints[waypoints.Count - 2]); // The target (last waypoint before home)

            AddArcedPoints(_missionDepartureLine, departurePoints);

            // Return: Target -> Home
            var returnPoints = new List<Vector2> {
                waypoints[waypoints.Count - 2],
                waypoints[waypoints.Count - 1]
            };
            AddArcedPoints(_missionReturnLine, returnPoints);
        }

        private void AddArcedPoints(Line2D line, List<Vector2> waypoints)
        {
            if (waypoints.Count < 2) return;

            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                Vector2 start = GetVisualPosition(waypoints[i]);
                Vector2 end = GetVisualPosition(waypoints[i + 1]);

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

        private async void OnDayAdvanced()
        {
            // Clear previous day's flight path
            _previewWaypoints = null;
            UpdateMissionPath();

            // Wait a frame to let UI settle if hidden/resizing
            if (IsInsideTree()) await ToSignal(GetTree(), "process_frame");

            // Force a full rebuild
            RebuildMap();
            UpdateVisualPositions();
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

        private void UpdateDebugHandles()
        {
            if (_mapData.FrontLinePoints == null) return;

            // Rebuild handles if count mismatch
            if (_debugHandles.Count != _mapData.FrontLinePoints.Length)
            {
                foreach (var h in _debugHandles) h.QueueFree();
                _debugHandles.Clear();

                for (int i = 0; i < _mapData.FrontLinePoints.Length; i++)
                {
                    var handle = new ColorRect();
                    handle.Size = new Vector2(10, 10);
                    handle.Color = Colors.Yellow;
                    handle.MouseFilter = MouseFilterEnum.Stop; // Catch input
                    handle.SetMeta("Idx", i);

                    // Capture handle input
                    handle.GuiInput += (evt) => OnHandleInput(handle, evt);

                    _debugContainer.AddChild(handle);
                    _debugHandles.Add(handle);
                }
            }

            // Position handles
            Vector2 areaSize = _mapArea.Size;
            Vector2 center = areaSize / 2;

            for (int i = 0; i < _mapData.FrontLinePoints.Length; i++)
            {
                Vector2 worldPos = _mapData.FrontLinePoints[i];

                // Use Unified Transform
                Vector2 screenPos = GetVisualPosition(worldPos);

                if (i < _debugHandles.Count)
                    _debugHandles[i].Position = screenPos - new Vector2(5, 5); // Center the 10x10 handle
            }
        }

        private void OnHandleInput(Control handle, InputEvent @event)
        {
            if (@event is InputEventMouseButton mb)
            {
                if (mb.ButtonIndex == MouseButton.Left)
                {
                    if (mb.Pressed)
                    {
                        _draggedHandleIndex = handle.GetMeta("Idx").AsInt32();
                        _isMapActive = false; // Prevent map panning while dragging handle
                        _mapArea.GuiInput -= OnMapInput; // Temporarily block map input
                    }
                    else
                    {
                        if (_draggedHandleIndex != -1)
                        {
                            _draggedHandleIndex = -1;
                            _mapArea.GuiInput += OnMapInput; // Restore map input
                        }
                    }
                    GetViewport().SetInputAsHandled();
                }
            }
            else if (@event is InputEventMouseMotion mm && _draggedHandleIndex != -1)
            {
                // Update handle position
                int idx = _draggedHandleIndex;
                Vector2 deltaScreen = mm.Relative;

                // Convert screen delta to World KM delta
                // Account for Spread factor on both axes
                float spreadSafeX = Math.Max(0.1f, _baseLonSpread);
                float spreadSafeY = Math.Max(0.1f, _baseLatSpread);

                float dX = deltaScreen.X / (_mapScale * spreadSafeX);
                float dY = deltaScreen.Y / (_mapScale * spreadSafeY);

                // Update point
                if (idx >= 0 && idx < _mapData.FrontLinePoints.Length)
                {
                    _mapData.FrontLinePoints[idx] += new Vector2(dX, dY);
                    UpdateVisualPositions();
                }
            }
        }

        private void ExportFrontlineCoords()
        {
            GD.Print("=== EXPORTED FRONTLINE COORDS ===");
            GD.Print("var points = new List<Vector2> {");

            float lonScale = 71f;
            float latScale = 111f;

            foreach (var p in _mapData.FrontLinePoints)
            {
                // p is (x, y) in Global KM
                // x = lon * 71
                // y = -lat * 111
                float lon = p.X / lonScale;
                float lat = -p.Y / latScale;

                GD.Print($"    new Vector2({lon:F2}f, {lat:F2}f),");
            }
            GD.Print("};");
            GD.Print("=================================");
        }
    }
}
