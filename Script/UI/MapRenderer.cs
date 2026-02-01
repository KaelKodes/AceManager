using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using AceManager.Core;

namespace AceManager.UI
{
    /// <summary>
    /// Handles the visual rendering of the command map, including grid, markers, and mission paths.
    /// </summary>
    public class MapRenderer
    {
        private readonly Control _mapArea;
        private readonly Control _markerContainer;
        private readonly Control _gridLayer;
        private readonly TextureRect _backdrop;
        private readonly Line2D _missionDepartureLine;
        private readonly Line2D _missionReturnLine;
        private readonly List<Line2D> _frontLines = new();

        private readonly Dictionary<MapLocation.LocationType, Color> _typeColors = new()
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

        public MapRenderer(Control mapArea)
        {
            _mapArea = mapArea;

            // Setup Backdrop
            _backdrop = new TextureRect();
            _backdrop.Texture = ResourceLoader.Load<Texture2D>("res://Assets/UI/Map/WesternFrontBackground.jpg");
            _backdrop.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _backdrop.StretchMode = TextureRect.StretchModeEnum.Scale;
            _backdrop.MouseFilter = Control.MouseFilterEnum.Ignore;
            _backdrop.SelfModulate = new Color(1, 1, 1, 1);
            _mapArea.AddChild(_backdrop);
            _mapArea.MoveChild(_backdrop, 0);

            _markerContainer = new Control { Name = "MarkerContainer", MouseFilter = Control.MouseFilterEnum.Ignore };
            _mapArea.AddChild(_markerContainer);

            _missionDepartureLine = new Line2D { Width = 2, DefaultColor = new Color(0.2f, 1.0f, 0.2f, 0.5f) };
            _missionReturnLine = new Line2D { Width = 2, DefaultColor = new Color(1.0f, 0.2f, 0.2f, 0.5f) };
            _mapArea.AddChild(_missionDepartureLine);
            _mapArea.AddChild(_missionReturnLine);

            _gridLayer = new Control { Name = "GridLayer", MouseFilter = Control.MouseFilterEnum.Ignore };
            _mapArea.AddChild(_gridLayer);
            _gridLayer.Draw += OnGridDraw;
        }

        public MapData MapData { get; private set; }
        public float MapScale { get; set; } = 8f;
        public Vector2 ViewOffset { get; set; } = Vector2.Zero;

        // Viewport bounds injected from the main panel
        public Vector2 HomeWorldPos { get; set; }
        public Vector2 WorldMinKM { get; set; }
        public Vector2 WorldMaxKM { get; set; }
        public Vector2 WorldSizeKM { get; set; }

        public void SetMapData(MapData data)
        {
            MapData = data;
            RebuildMap();
        }

        public void RebuildMap()
        {
            if (MapData == null) return;
            // Basic sanity check
            if (_mapArea.Size.X < 10) return;

            // Clear markers
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

            // Rebuild front lines
            if (MapData.FrontLinePoints != null && MapData.FrontLinePoints.Length > 1)
            {
                var line = new Line2D();
                line.Width = 4;
                line.DefaultColor = new Color(Colors.Red, 0.8f);
                line.Antialiased = true;
                _mapArea.AddChild(line);
                _mapArea.MoveChild(line, 1); // Above backdrop
                _frontLines.Add(line);
            }

            // Rebuild markers
            var discovered = MapData.GetDiscoveredLocations();
            foreach (var loc in discovered)
            {
                if (loc.Type == MapLocation.LocationType.Town && loc.Name == "Frontline Town") continue;
                var marker = CreateMarker(loc);
                marker.SetMeta("LocationId", loc.Id);
                _markerContainer.AddChild(marker);
            }

            UpdateVisuals();
        }

        public void UpdateVisuals(List<Vector2> previewWaypoints = null)
        {
            if (MapData == null) return;

            Vector2 areaSize = _mapArea.Size;
            Vector2 center = areaSize / 2;
            Vector2 currentOrigin = HomeWorldPos + ViewOffset;

            // Update Backdrop
            if (_backdrop != null)
            {
                _backdrop.Size = WorldSizeKM * MapScale;
                _backdrop.Position = center + (WorldMinKM - currentOrigin) * MapScale;
                _backdrop.Visible = true;
            }

            _gridLayer.QueueRedraw();

            // Update Frontlines
            if (MapData.FrontLinePoints != null && _frontLines.Count > 0)
            {
                var line = _frontLines[0];
                line.ClearPoints();
                foreach (var p in MapData.FrontLinePoints)
                {
                    line.AddPoint(GetVisualPosition(p));
                }
            }

            // Update Markers
            foreach (Control marker in _markerContainer.GetChildren())
            {
                string id = marker.GetMeta("LocationId").ToString();
                var loc = MapData.Locations.FirstOrDefault(l => l.Id == id);
                if (loc == null) continue;

                Vector2 screenPos = GetVisualPosition(loc.WorldCoordinates);
                marker.Position = screenPos;

                bool isVisible = screenPos.X > -200 && screenPos.X < areaSize.X + 200 &&
                                 screenPos.Y > -200 && screenPos.Y < areaSize.Y + 200;
                marker.Visible = isVisible;

                if (isVisible && marker.GetChildCount() > 0 && marker.GetChild(0) is Label lbl)
                {
                    lbl.Visible = MapScale > 1.5f;
                }
            }

            UpdateMissionPath(previewWaypoints);
        }

        public Vector2 GetVisualPosition(Vector2 worldPosKM)
        {
            if (MapData == null) return Vector2.Zero;

            Vector2 adjustedWorld = MapData.GetTacticalCoordinates(worldPosKM);
            Vector2 areaSize = _mapArea.Size;
            Vector2 center = areaSize / 2;
            Vector2 currentOrigin = HomeWorldPos + ViewOffset;

            return center + (adjustedWorld - currentOrigin) * MapScale;
        }

        private void OnGridDraw()
        {
            if (MapData == null || MapScale < 1.0f) return;

            Vector2 areaSize = _mapArea.Size;
            Vector2 center = areaSize / 2;
            Vector2 currentOrigin = HomeWorldPos + ViewOffset;

            float spacing = GridSystem.GridSizeKM * MapScale;
            if (spacing < 5) return;

            Color gridColor = new Color(1, 1, 1, 0.15f);
            Color labelColor = new Color(1, 1, 1, 0.4f);

            Vector2 viewTopLeft = currentOrigin - (center / MapScale);
            Vector2 viewBottomRight = currentOrigin + (center / MapScale);

            float drawMinX = Math.Max(viewTopLeft.X, WorldMinKM.X);
            float drawMaxX = Math.Min(viewBottomRight.X, WorldMaxKM.X);
            float drawMinY = Math.Max(viewTopLeft.Y, WorldMinKM.Y);
            float drawMaxY = Math.Min(viewBottomRight.Y, WorldMaxKM.Y);

            if (drawMaxX <= drawMinX || drawMaxY <= drawMinY) return;

            // Draw Columns
            int startCol = (int)Math.Floor(drawMinX / GridSystem.GridSizeKM);
            int endCol = (int)Math.Floor(drawMaxX / GridSystem.GridSizeKM);

            for (int colIdx = startCol; colIdx <= endCol; colIdx++)
            {
                float x = colIdx * GridSystem.GridSizeKM;
                if (x < WorldMinKM.X || x > WorldMaxKM.X) continue;

                Vector2 p1 = center + (new Vector2(x, drawMinY) - currentOrigin) * MapScale;
                Vector2 p2 = center + (new Vector2(x, drawMaxY) - currentOrigin) * MapScale;
                _gridLayer.DrawLine(p1, p2, gridColor, 1.0f);

                string colName = GridSystem.GetColumnLetter(colIdx);
                _gridLayer.DrawString(ThemeDB.FallbackFont, p1 + new Vector2(5, 20), colName, HorizontalAlignment.Left, -1, 10, labelColor);
            }

            // Draw Rows
            int startRow = (int)Math.Floor(drawMinY / GridSystem.GridSizeKM);
            int endRow = (int)Math.Floor(drawMaxY / GridSystem.GridSizeKM);

            for (int rowIdx = startRow; rowIdx <= endRow; rowIdx++)
            {
                float y = rowIdx * GridSystem.GridSizeKM;
                if (y < WorldMinKM.Y || y > WorldMaxKM.Y) continue;

                Vector2 p1 = center + (new Vector2(drawMinX, y) - currentOrigin) * MapScale;
                Vector2 p2 = center + (new Vector2(drawMaxX, y) - currentOrigin) * MapScale;
                _gridLayer.DrawLine(p1, p2, gridColor, 1.0f);

                float labelTacY = y + GridSystem.GridSizeKM / 2;
                if (labelTacY <= WorldMaxKM.Y)
                {
                    Vector2 labelPos = center + (new Vector2(drawMinX, labelTacY) - currentOrigin) * MapScale;
                    string rowName = (rowIdx + 1).ToString();
                    _gridLayer.DrawString(ThemeDB.FallbackFont, labelPos + new Vector2(5, 5), rowName, HorizontalAlignment.Left, -1, 10, labelColor);
                }
            }
        }

        private void UpdateMissionPath(List<Vector2> waypoints)
        {
            if (waypoints == null || waypoints.Count == 0)
            {
                var mission = GameManager.Instance.CurrentMission;
                if (mission != null) waypoints = mission.Waypoints;
            }

            if (waypoints == null || waypoints.Count < 2 || MapData == null)
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

            var departurePoints = waypoints.Take(waypoints.Count - 1).ToList();
            AddArcedPoints(_missionDepartureLine, departurePoints);

            var returnPoints = new List<Vector2> { waypoints[waypoints.Count - 2], waypoints[waypoints.Count - 1] };
            AddArcedPoints(_missionReturnLine, returnPoints);
        }

        private void AddArcedPoints(Line2D line, List<Vector2> points)
        {
            if (points.Count < 2) return;
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 start = GetVisualPosition(points[i]);
                Vector2 end = GetVisualPosition(points[i + 1]);

                float dist = start.DistanceTo(end);
                if (dist < 10)
                {
                    line.AddPoint(start);
                    continue;
                }

                Vector2 mid = (start + end) / 2;
                Vector2 dir = (end - start).Normalized();
                Vector2 normal = new Vector2(-dir.Y, dir.X);
                float h = dist * 0.15f;
                Vector2 control = mid + normal * h;

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

            var symbolLabel = new Label { Text = symbol, Position = new Vector2(-11, -15) };
            symbolLabel.AddThemeColorOverride("font_color", color);
            symbolLabel.AddThemeFontSizeOverride("font_size", 22);
            container.AddChild(symbolLabel);

            var nameLabel = new Label { Name = "NameLabel", Text = location.Name, Position = new Vector2(12, -8) };
            nameLabel.AddThemeColorOverride("font_color", color);
            nameLabel.AddThemeFontSizeOverride("font_size", 12);
            container.AddChild(nameLabel);

            if (!string.IsNullOrEmpty(location.Notes))
                container.TooltipText = location.Notes;

            container.FocusMode = Control.FocusModeEnum.None;
            container.MouseFilter = Control.MouseFilterEnum.Ignore;
            return container;
        }
    }
}
