using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AceManager.Core
{
    public static class MissionResolver
    {
        public static void ResolveMission(MissionData mission, AirbaseData baseData)
        {
            mission.MissionLog.Clear();
            mission.Status = MissionStatus.Active;

            // PHASE 0: Order Compliance Check
            CheckOrderCompliance(mission);

            // PHASE 1: Readiness Check
            bool readinessOk = CheckReadiness(mission, baseData);
            if (!readinessOk)
            {
                mission.Status = MissionStatus.Aborted;
                return;
            }

            // PHASE 2: Contact & Engagement
            string specialEvent = "";
            int contactIntensity = CombatResolver.ResolveContact(mission, baseData, out specialEvent);
            bool hadContact = contactIntensity > 0 || !string.IsNullOrEmpty(specialEvent);

            // PHASE 3: Outcome Determination
            mission.ResultBand = CombatResolver.CalculateOutcome(mission, baseData, contactIntensity, specialEvent);

            // PHASE 4: Consequences
            PostMissionLogic.ApplyConsequences(mission, baseData, hadContact);

            // PHASE 5: Outcome Finalization (Adjust for losses)
            PostMissionLogic.FinalizeResultBand(mission);

            // PHASE 6: Pilot Progression
            PostMissionLogic.ApplyProgression(mission);

            // PHASE 7: Intel & Discovery
            PostMissionLogic.CheckDiscovery(mission);

            mission.Status = MissionStatus.Resolved;
            LogEntry(mission, $"Mission complete. Result: {mission.ResultBand}");
        }

        private static void LogEntry(MissionData mission, string message)
        {
            mission.MissionLog.Add(message);
            GD.Print($"[Mission] {message}");
        }

        private static void CheckOrderCompliance(MissionData mission)
        {
            var briefing = GameManager.Instance?.TodaysBriefing;
            if (briefing == null)
            {
                mission.FollowedOrders = true;
                mission.OrderBonus = 0;
                mission.OrderComplianceMessage = "";
                return;
            }

            bool followedOrders = briefing.DoesMissionFollowOrders(mission.Type);
            mission.FollowedOrders = followedOrders;

            if (briefing.TodaysPriority == CommandPriority.ConserveResources)
            {
                // Flying on a "conserve resources" day - minor demerit unless great success
                mission.OrderBonus = -5;
                mission.OrderComplianceMessage = "Command advised conservation - mission flown anyway.";
                LogEntry(mission, "Note: Command requested we conserve resources today.");
            }
            else if (followedOrders)
            {
                // Following orders = bonus
                int bonus = mission.Type switch
                {
                    MissionType.Reconnaissance => 15,  // Intel is valuable
                    MissionType.Escort => 15,          // Protecting allies
                    MissionType.Bombing or MissionType.Strafing => 20,  // Direct support
                    _ => 10
                };
                mission.OrderBonus = bonus;
                mission.OrderComplianceMessage = $"Orders followed - HQ commends the squadron. (+{bonus} prestige)";
                LogEntry(mission, $"Mission matches today's priority: {briefing.TodaysPriority}");
            }
            else
            {
                // Ignoring orders = penalty
                mission.OrderBonus = -5;
                mission.OrderComplianceMessage = "Mission type did not match command priority.";
                LogEntry(mission, $"Warning: Command requested {briefing.TodaysPriority} operations.");
            }
        }

        // ============================
        // PHASE 1: Readiness Check
        // ============================
        private static bool CheckReadiness(MissionData mission, AirbaseData baseData)
        {
            LogEntry(mission, "=== PHASE 1: Readiness Check ===");

            int fuelNeeded = mission.GetBaseFuelCost();
            if (baseData.CurrentFuel < fuelNeeded)
            {
                LogEntry(mission, $"ABORT: Insufficient fuel. Need {fuelNeeded}, have {baseData.CurrentFuel}.");
                return false;
            }

            int ammoNeeded = mission.GetBaseAmmoCost();
            if (baseData.CurrentAmmo < ammoNeeded)
            {
                LogEntry(mission, $"ABORT: Insufficient ammo. Need {ammoNeeded}, have {baseData.CurrentAmmo}.");
                return false;
            }

            // Check runway for aircraft requirements
            foreach (var assignment in mission.Assignments)
            {
                var aircraft = assignment.Aircraft?.Definition;
                if (aircraft != null && aircraft.RunwayRequirementRange > baseData.RunwayRating)
                {
                    LogEntry(mission, $"ABORT: {aircraft.Name} requires Runway Level {aircraft.RunwayRequirementRange}, but base is Level {baseData.RunwayRating}.");
                    return false;
                }
            }

            // Check maintenance capacity
            if (mission.GetFlightCount() > baseData.MaintenanceRating * 2)
            {
                LogEntry(mission, "WARNING: Maintenance strained. May affect readiness.");
            }

            LogEntry(mission, "Readiness: GREEN. All checks passed.");
            return true;
        }
    }
}
