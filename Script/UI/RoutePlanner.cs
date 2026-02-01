using Godot;
using System;
using System.Collections.Generic;
using AceManager.Core;

namespace AceManager.UI
{
    public class RoutePlanner
    {
        public Vector2 HomeWorldPos { get; set; }
        public Vector2? ManualTargetPos { get; private set; }
        public string TargetName { get; private set; } = "Primary Objective";
        public int DistanceKM { get; private set; } = 30;

        public List<Vector2> Waypoints { get; private set; } = new();

        public event Action OnPathUpdated;

        public void SetTarget(Vector2 worldPos, string name = null)
        {
            ManualTargetPos = worldPos;
            if (name != null) TargetName = name;

            // Auto-calc distance
            float dist = (worldPos - HomeWorldPos).Length();
            DistanceKM = (int)Math.Clamp(dist, 10, 150);
            DistanceKM = (int)(Math.Round(DistanceKM / 5.0) * 5); // Snap to 5

            RefreshPath();
        }

        public void SetDistance(int km)
        {
            // Only update if changed
            if (km == DistanceKM && ManualTargetPos == null) return;

            DistanceKM = km;
            // Reset manual target to allow procedural generation at new distance
            ManualTargetPos = null;
            TargetName = "Primary Objective";

            RefreshPath();
        }

        public void RefreshPath()
        {
            Vector2 target = ManualTargetPos ?? MapData.GenerateProceduralTarget(HomeWorldPos, DistanceKM, GameManager.Instance.SelectedNation);
            Waypoints = MapData.GenerateWaypoints(HomeWorldPos, target, DistanceKM);
            OnPathUpdated?.Invoke();
        }

        public Vector2 GetTargetPosition()
        {
            if (Waypoints != null && Waypoints.Count >= 2)
            {
                // Target is second to last point (Home -> ... -> Target -> Home)
                return Waypoints[Waypoints.Count - 2];
            }
            return Vector2.Zero;
        }
    }
}
