using Godot;
using System;

namespace AceManager.Core
{
    public static class GridSystem
    {
        public const float GridSizeKM = 20.0f;

        // Use World Origin (0,0 KM) for a consistent global grid
        private static readonly Vector2 GridOriginKM = Vector2.Zero;

        // Requested tactical bounds
        public const int MinColIdx = -4;  // "X4"
        public const int MaxColIdx = 27;  // "AB"
        public const int MinRowIdx = -288; // Label "-287"
        public const int MaxRowIdx = -268; // Label "-267"

        /// <summary>
        /// Converts world coordinates in KM to a grid reference (e.g., "B-4").
        /// Uses MapData's tactical calibration to ensure visually synced results.
        /// </summary>
        public static string WorldToGrid(Vector2 worldPos, MapData map = null)
        {
            // Apply tactical calibration if context is available
            Vector2 tacticalPos = map != null ? map.GetTacticalCoordinates(worldPos) : worldPos;

            int gridX = (int)Math.Floor(tacticalPos.X / GridSizeKM);
            int gridY = (int)Math.Floor(tacticalPos.Y / GridSizeKM);

            // Clamp or return empty if outside specific tactical grid bounds
            if (gridX < MinColIdx || gridX > MaxColIdx || gridY < MinRowIdx || gridY > MaxRowIdx)
                return "OUT-OF-BOUNDS";

            string col = GetColumnLetter(gridX);
            string row = (gridY + 1).ToString();

            return $"{col}-{row}";
        }

        public static string GetColumnLetter(int index)
        {
            if (index < 0) return "X" + Math.Abs(index); // Out of bounds safety

            string name = "";
            while (index >= 0)
            {
                name = (char)('A' + (index % 26)) + name;
                index = (index / 26) - 1;
            }
            return name;
        }

        public static Vector2 GridToWorld(string gridRef)
        {
            // Simple parser for "B-4" -> World KM
            var parts = gridRef.Split('-');
            if (parts.Length != 2) return Vector2.Zero;

            string colStr = parts[0].ToUpper();
            int row = int.Parse(parts[1]) - 1;

            int col = 0;
            for (int i = 0; i < colStr.Length; i++)
            {
                col = col * 26 + (colStr[i] - 'A' + 1);
            }
            col -= 1;

            return GridOriginKM + new Vector2(col * GridSizeKM + GridSizeKM / 2, row * GridSizeKM + GridSizeKM / 2);
        }
    }
}
