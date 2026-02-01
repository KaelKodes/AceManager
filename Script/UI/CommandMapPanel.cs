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
        private Control _windowBackground;
        private Control _panelContainer;
        private Control _legendPanel;

        private Label _coordLabel;

        private MapData _mapData;
        private float _mapScale = 8f; // Pixels per km (default)
        private Vector2 _viewOffset = Vector2.Zero; // Current pan offset in KM
        private bool _isMapActive = false;
        private Vector2 _homeWorldPos = Vector2.Zero;

        // Debug Editor State
        private bool _debugEditMode = false; // Calibration complete
        private Label _calibrationLabel;
        private Control _debugContainer;
        private List<Control> _debugHandles = new();
        private int _draggedHandleIndex = -1;

        [Signal] public delegate void PanelClosedEventHandler();

        private MapInputController _inputController;
        private MapRenderer _renderer;

        // Calibration Constants
        private const float MapMax_Lat = 52.2f; // Shifted North and expanded
        private const float MapMin_Lat = 48.0f; // Shifted South and expanded
        private const float MapMin_Lon = -1.5f; // Shifted West and expanded
        private const float MapMax_Lon = 7.8f;  // Shifted East and expanded

        public override void _Ready()
        {
            _mapArea = GetNode<Control>("%MapArea");

            _sectorLabel = GetNode<Label>("%SectorLabel");
            _legendLabel = GetNode<Label>("%LegendLabel");
            _closeButton = GetNode<Button>("%CloseButton");
            _windowBackground = GetNode<Control>("Background");
            _panelContainer = GetNode<Control>("Panel");
            _legendPanel = GetNodeOrNull<Control>("Panel/VBoxContainer/ContentHBox/LegendPanel");

            // Disable clipping for now to see where the map goes if it fails
            _mapArea.ClipContents = false;

            // Block input to background and allow focus
            MouseFilter = MouseFilterEnum.Stop;
            FocusMode = FocusModeEnum.All;
            _mapArea.FocusMode = FocusModeEnum.All;

            _closeButton.Pressed += OnClosePressed;
            _mapArea.Resized += () => CallDeferred(nameof(RebuildMap));

            // Setup Renderer
            _renderer = new MapRenderer(_mapArea);
            float lonScale = 71f;
            float latScale = 111f;
            _renderer.WorldMinKM = new Vector2(MapMin_Lon * lonScale, -MapMax_Lat * latScale); // NW corner
            _renderer.WorldMaxKM = new Vector2(MapMax_Lon * lonScale, -MapMin_Lat * latScale); // SE corner
            _renderer.WorldSizeKM = _renderer.WorldMaxKM - _renderer.WorldMinKM;

            // Initialize Input Controller
            _inputController = new MapInputController(
                _mapArea,
                OnPanMap,
                OnZoomMap,
                OnMapCursorMove,
                SetMapActive
            );
            _inputController.SetZoom(_mapScale);

            // Debug container for handles
            _debugContainer = new Control { Name = "DebugHandles", MouseFilter = MouseFilterEnum.Pass };
            _mapArea.AddChild(_debugContainer);

            _coordLabel = new Label { Name = "CoordLabel" };
            _coordLabel.AddThemeFontSizeOverride("font_size", 12);
            _coordLabel.SelfModulate = new Color(1, 1, 1, 0.8f);
            _mapArea.AddChild(_coordLabel);

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

        private void OnPanMap(Vector2 deltaKM)
        {
            _viewOffset -= deltaKM;
            UpdateVisualPositions();
        }

        private void OnZoomMap(float newZoom)
        {
            _mapScale = newZoom;
            UpdateVisualPositions();
        }

        private void OnMapCursorMove(Vector2 localMousePos)
        {
            UpdateCursorCoord(localMousePos);
        }

        private void UpdateCursorCoord(Vector2 mousePos)
        {
            Vector2 homeWorldPos = GetHomeWorldPos();
            Vector2 center = _mapArea.Size / 2;
            Vector2 currentOrigin = homeWorldPos + _viewOffset;

            // Screen to World (This worldPos is already Tactical if the map image is calibrated)
            Vector2 worldPos = currentOrigin + (mousePos - center) / _mapScale;
            // Pass null to WorldToGrid to avoid double-calibration
            string gridRef = GridSystem.WorldToGrid(worldPos, null);
            _coordLabel.Text = gridRef;
            _coordLabel.Position = mousePos + new Vector2(15, 15);
        }

        public void ShowMap(MapData data)
        {
            _mapData = data;
            _homeWorldPos = Vector2.Zero; // Reset so it re-resolves on next draw
            if (data == null) return;

            _sectorLabel.Text = data.SectorName;
            _viewOffset = Vector2.Zero;
            _mapScale = 12f;
            _inputController.SetZoom(_mapScale);

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

            if (!active)
            {
                // _inputController manages dragging state internally, 
                // but we can ensure consistency here if needed.
            }
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

            if (_homeWorldPos == Vector2.Zero)
            {
                var homeLoc = _mapData.Locations.FirstOrDefault(l => l.Id == "home_base");
                _homeWorldPos = homeLoc?.WorldCoordinates ?? Vector2.Zero;
            }

            _renderer.HomeWorldPos = _homeWorldPos;
            _renderer.SetMapData(_mapData);

            UpdateVisualPositions();
        }

        private void UpdateLegend()
        {
            if (_legendPanel == null) return;
            // Simplified for background mode
            _legendLabel.Text = _isMapActive ? "PAN: Left Drag | ZOOM: Wheel" : "CLICK TO INTERACT";
        }

        private void UpdateVisualPositions()
        {
            if (_mapData == null) return;

            if (_homeWorldPos == Vector2.Zero)
            {
                var hLoc = _mapData.Locations.FirstOrDefault(l => l.Id == "home_base");
                _homeWorldPos = hLoc?.WorldCoordinates ?? Vector2.Zero;
            }

            _renderer.HomeWorldPos = _homeWorldPos;
            _renderer.MapScale = _mapScale;
            _renderer.ViewOffset = _viewOffset;
            _renderer.UpdateVisuals(_previewWaypoints);

            // Update Debug Handles
            if (_debugEditMode)
            {
                UpdateDebugHandles();

                // --- CALIBRATION ANCHOR (Nieuport) ---
                // Real World Nieuport Coordinates: 2.75 E, 51.13 N
                Vector2 nieuportReal = _mapData.GetWorldCoordinates(new Vector2(2.75f, 51.13f));
                Vector2 anchorScreen = _renderer.GetVisualPosition(nieuportReal);

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
        }

        private List<Vector2> _previewWaypoints;

        public void DrawPreviewPath(List<Vector2> waypoints)
        {
            _previewWaypoints = waypoints;
            UpdateVisualPositions();
        }

        private async void OnDayAdvanced()
        {
            // Clear previous day's flight path
            _previewWaypoints = null;

            // Wait a frame to let UI settle if hidden/resizing
            if (IsInsideTree()) await ToSignal(GetTree(), "process_frame");

            // Force a full rebuild
            RebuildMap();
            UpdateVisualPositions();
        }

        private Vector2 GetHomeWorldPos()
        {
            if (_mapData == null) return Vector2.Zero;
            var homeLoc = _mapData.Locations.FirstOrDefault(l => l.Id == "home_base");
            return homeLoc?.WorldCoordinates ?? Vector2.Zero;
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
            for (int i = 0; i < _mapData.FrontLinePoints.Length; i++)
            {
                Vector2 worldPos = _mapData.FrontLinePoints[i];

                // Use Unified Transform
                Vector2 screenPos = _renderer.GetVisualPosition(worldPos);

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
                        _inputController.SetActive(false);
                    }
                    else
                    {
                        if (_draggedHandleIndex != -1)
                        {
                            _draggedHandleIndex = -1;
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

                // Simple drag factor approximation since we don't need pixel-perfect reverse projection for debug
                // Accessing internal spread seems hard unless we expose it from mapdata directly? MapData is public.
                float spreadSafeX = Math.Max(0.1f, _mapData?.LonSpread ?? 1.0f);
                float spreadSafeY = Math.Max(0.1f, _mapData?.LatSpread ?? 1.0f);

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
            btnPrint.Pressed += () =>
            {
                if (_mapData != null)
                    GD.Print($"OFFSET_X: {_mapData.LonOffset:F3}, OFFSET_Y: {_mapData.LatOffset:F3}, SPREAD_X: {_mapData.LonSpread:F3}, SPREAD_Y: {_mapData.LatSpread:F3}");
            };
            vbox.AddChild(btnPrint);

            // Export Frontline Button
            var btnExport = new Button { Text = "EXPORT PRECISE COORDS" };
            btnExport.Pressed += ExportFrontlineCoords;
            btnExport.Modulate = Colors.GreenYellow;
            vbox.AddChild(btnExport);

            // Label
            _calibrationLabel = new Label();
            if (_mapData != null)
                _calibrationLabel.Text = $"X: {_mapData.LonOffset:F2} | Y: {_mapData.LatOffset:F2} | SprX: {_mapData.LonSpread:F2} | SprY: {_mapData.LatSpread:F2}";
            vbox.AddChild(_calibrationLabel);
        }

        private void AdjustCalibration(float xDelta, float spreadXDelta, float yDelta, float spreadYDelta)
        {
            if (_mapData == null) return;
            _mapData.LonOffset += xDelta;
            _mapData.LonSpread += spreadXDelta;
            _mapData.LatOffset += yDelta;
            _mapData.LatSpread += spreadYDelta;

            if (_calibrationLabel != null)
                _calibrationLabel.Text = $"X: {_mapData.LonOffset:F2} | Y: {_mapData.LatOffset:F2} | SprX: {_mapData.LonSpread:F2} | SprY: {_mapData.LatSpread:F2}";
            UpdateVisualPositions();
        }
    }
}
