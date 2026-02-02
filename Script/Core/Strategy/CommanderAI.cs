using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AceManager.Core.Strategy
{
    public enum StrategicIntent
    {
        Maintain,     // Hold current lines, repair nodes
        Push,         // Aggressive offensive, prioritize frontline targets
        Consolidate,  // Shorten supply lines, focus on secondary hubs
        Withdraw      // Abandon forward depots to save hubs (extreme)
    }

    /// <summary>
    /// High-level AI that sets global goals for a faction.
    /// </summary>
    public class MainCommander
    {
        public string Faction { get; set; }
        public StrategicIntent CurrentIntent { get; set; } = StrategicIntent.Maintain;
        public Dictionary<string, StrategicIntent> SectorIntents { get; set; } = new();

        public MainCommander(string faction)
        {
            Faction = faction;
        }

        public void Update(MapData map)
        {
            // Simple logic for now: If we have high overall supply/readiness, PUSH.
            // In a real version, this would analyze frontline health vs enemy health.
            CurrentIntent = StrategicIntent.Push;

            // For now, all sectors share the global intent
            foreach (var region in new[] { "North", "Mid", "South" })
            {
                SectorIntents[region] = CurrentIntent;
            }

            GD.Print($"[MainCommander] {Faction} is now in {CurrentIntent} mode.");
        }
    }

    /// <summary>
    /// Regional AI that translates MainCommander intent into specific base orders.
    /// </summary>
    public class SubCommander
    {
        public string RegionId { get; set; } // "North", "Mid", "South"
        public string Faction { get; set; }

        public SubCommander(string faction, string regionId)
        {
            Faction = faction;
            RegionId = regionId;
        }

        public void AssignMissions(MapData map, MainCommander mc)
        {
            StrategicIntent sectorIntent = mc.SectorIntents.GetValueOrDefault(RegionId, StrategicIntent.Maintain);

            // 1. Find all bases in this region
            var regionalNodes = map.StrategicNodes
                .OfType<MilitaryNode>()
                .Where(n => n.RegionId == RegionId && n.OwningNation == Faction)
                .ToList();

            if (regionalNodes.Count == 0) return;

            // 2. Intelligence Assessment: Check discovery ratio for enemy nodes
            var allPotentialTargets = map.StrategicNodes
                .Where(n => n.OwningNation != Faction && !(n is RegionLabelNode))
                .ToList();

            int knownCount = allPotentialTargets.Count(n => n.IntelStatus != StrategicNode.IntelLevel.Unknown);
            float discoveryRatio = allPotentialTargets.Count > 0 ? (float)knownCount / allPotentialTargets.Count : 1f;

            // 3. Assign Diverse Mission Roles
            for (int i = 0; i < regionalNodes.Count; i++)
            {
                var milNode = regionalNodes[i];
                MissionType assignedType = MissionType.Patrol;

                // Mission Mix Weights based on index
                float roll = (float)i / regionalNodes.Count;

                if (sectorIntent == StrategicIntent.Push)
                {
                    // Offensive Mix: 60% Attack, 20% Intel, 20% Support
                    if (roll < 0.6f) assignedType = MissionType.Strafing;
                    else if (roll < 0.8f) assignedType = MissionType.Reconnaissance;
                    else assignedType = MissionType.Patrol;

                    // Emergency Recon: If we are blind, prioritize identification
                    if (discoveryRatio < 0.4f && roll < 0.4f) assignedType = MissionType.Reconnaissance;
                }
                else
                {
                    // Defensive/Maintain Mix: 40% Intel, 40% Support, 20% Harassment
                    if (roll < 0.4f) assignedType = MissionType.Reconnaissance;
                    else if (roll < 0.8f) assignedType = MissionType.Patrol;
                    else assignedType = MissionType.Strafing;
                }

                GenerateOrderForBase(map, milNode, sectorIntent, assignedType);
            }
        }

        private void GenerateOrderForBase(MapData map, MilitaryNode baseNode, StrategicIntent intent, MissionType type)
        {
            float searchRadius = 150f;
            StrategicNode target = null;

            // 1. Select Target based on Mission Type
            if (type == MissionType.Reconnaissance)
            {
                // Prioritize Unknown nodes for recon
                target = map.StrategicNodes
                    .Where(n => n.OwningNation != Faction && n.IntelStatus == StrategicNode.IntelLevel.Unknown && !(n is RegionLabelNode))
                    .OrderBy(n => (n.WorldCoordinates - baseNode.WorldCoordinates).Length())
                    .FirstOrDefault();

                // Fallback to Known nodes if everything is discovered
                if (target == null)
                {
                    target = map.StrategicNodes
                        .Where(n => n.OwningNation != Faction && !(n is RegionLabelNode))
                        .OrderBy(n => (n.WorldCoordinates - baseNode.WorldCoordinates).Length())
                        .FirstOrDefault();
                }
            }
            else if (type == MissionType.Strafing || type == MissionType.Bombing)
            {
                // Offensive missions need Known targets
                var candidates = map.StrategicNodes
                    .Where(n => n.OwningNation != Faction && n.IntelStatus != StrategicNode.IntelLevel.Unknown && !(n is RegionLabelNode))
                    .Where(n => (n.WorldCoordinates - baseNode.WorldCoordinates).Length() < searchRadius)
                    .ToList();

                if (candidates.Count > 0)
                {
                    // Prioritize Military over Logistics for Strafing, Logistics for Bombing
                    if (type == MissionType.Strafing)
                        target = candidates.OfType<MilitaryNode>().OrderBy(n => (n.WorldCoordinates - baseNode.WorldCoordinates).Length()).FirstOrDefault();
                    else
                        target = candidates.OfType<LogisticsNode>().OrderBy(n => (n.WorldCoordinates - baseNode.WorldCoordinates).Length()).FirstOrDefault();

                    // Absolute fallback
                    if (target == null) target = candidates[0];
                }
            }

            // 2. Validation / Fallback
            if (target == null)
            {
                AssignDefaultPatrol(baseNode, intent);
                return;
            }

            // 3. Construct the Mission Order
            var distanceKM = (target.WorldCoordinates - baseNode.WorldCoordinates).Length();
            var mission = new MissionData
            {
                Type = type,
                TargetLocation = target.WorldCoordinates,
                TargetName = target.Name,
                TargetDistance = (int)Math.Max(10, Math.Min(150, distanceKM)),
                Status = MissionStatus.Planned,
                CommanderOrderContext = $"Strategic Objective: {intent}.\nRole: {type}.\nTarget Priority: {target.Name}."
            };

            baseNode.CurrentOrder = mission;
            GD.Print($"[SubCommander] {RegionId} assigned {type} mission to {baseNode.Name} -> Target: {target.Name}");
        }

        private void AssignDefaultPatrol(MilitaryNode baseNode, StrategicIntent intent)
        {
            baseNode.CurrentOrder = new MissionData
            {
                Type = MissionType.Patrol,
                TargetName = "Regional Skies",
                CommanderOrderContext = $"Strategic Intent: {intent}. Maintaining CAP over sector.",
                TargetDistance = 30
            };
        }
    }
}
