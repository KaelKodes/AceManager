using Godot;
using System;
using System.Linq;

namespace AceManager.Core
{
    public static class CombatResolver
    {
        private static Random _rng = new Random();

        public static int ResolveContact(MissionData mission, AirbaseData baseData, out string specialEvent)
        {
            Log(mission, "=== PHASE 2: Contact & Engagement ===");
            specialEvent = "";

            // Check for special events based on mission type and timeline
            if (mission.Type == MissionType.Interception || mission.Type == MissionType.Patrol)
            {
                if (_rng.Next(100) < 5) // 5% chance for Ace Encounter
                {
                    specialEvent = "AceEncounter";
                    Log(mission, "[b][color=red]ALERT:[/color][/b] Intelligence reports a renowned enemy Ace has been spotted in the sector!");
                }
            }
            else if (mission.Type == MissionType.Reconnaissance || mission.Type == MissionType.Bombing)
            {
                if (_rng.Next(100) < 3) // 3% chance for Zeppelin
                {
                    specialEvent = "Zeppelin";
                    Log(mission, "[b][color=yellow]SIGHTING:[/color][/b] A massive enemy Zeppelin has been spotted through the cloud layer!");
                }
            }

            int contactChance = mission.Type switch
            {
                MissionType.Patrol => 40 + (int)(mission.TargetDistance * 0.3f),
                MissionType.Interception => 60 + (int)(mission.TargetDistance * 0.2f),
                MissionType.Escort => 50 + (int)(mission.TargetDistance * 0.4f),
                MissionType.Reconnaissance => 30 + (int)(mission.TargetDistance * 0.5f),
                MissionType.Bombing => 50 + (int)(mission.TargetDistance * 0.5f),
                _ => 50
            };

            int opsBonus = baseData.OperationsRating * 5;
            contactChance = Math.Clamp(contactChance - opsBonus, 10, 95);

            int roll = _rng.Next(100);
            int intensity = 0;

            if (roll < contactChance / 3)
            {
                Log(mission, "No enemy contact encountered.");
                intensity = 0;
            }
            else if (roll < contactChance * 2 / 3)
            {
                Log(mission, "Limited enemy contact. Skirmish ensues.");
                intensity = 1;
            }
            else if (roll < contactChance)
            {
                Log(mission, "Full engagement! Heavy combat.");
                intensity = 2;
            }
            else
            {
                Log(mission, "Enemy ambush! Caught off guard.");
                intensity = 3;
            }

            return intensity;
        }

        public static MissionResultBand CalculateOutcome(MissionData mission, AirbaseData baseData, int contactIntensity, string specialEvent)
        {
            Log(mission, "=== PHASE 3: Outcome Determination ===");

            if (contactIntensity == 0 && string.IsNullOrEmpty(specialEvent))
            {
                Log(mission, "Mission completed without opposition.");
                return MissionResultBand.Success;
            }

            float friendlyScore = CalculateFriendlyEffectiveness(mission, baseData);
            float enemyScore = CalculateEnemyEffectiveness(mission, contactIntensity, specialEvent);

            Log(mission, $"Friendly Score: {friendlyScore:F1} vs Enemy Score: {enemyScore:F1}");

            float ratio = friendlyScore / Math.Max(enemyScore, 1);

            ratio *= mission.Risk switch
            {
                RiskPosture.Conservative => 0.9f,
                RiskPosture.Standard => 1.0f,
                RiskPosture.Aggressive => 1.15f,
                _ => 1.0f
            };

            MissionResultBand result;
            if (ratio > 2.0f)
            {
                result = MissionResultBand.DecisiveSuccess;
                Log(mission, "Decisive victory!");
            }
            else if (ratio > 1.5f)
            {
                result = MissionResultBand.Success;
                Log(mission, "Mission successful.");
            }
            else if (ratio > 1.1f)
            {
                result = MissionResultBand.MarginalSuccess;
                Log(mission, "Marginal success achieved.");
            }
            else if (ratio > 0.9f)
            {
                result = MissionResultBand.Stalemate;
                Log(mission, "Stalemate. No clear winner.");
            }
            else if (ratio > 0.7f)
            {
                result = MissionResultBand.MarginalFailure;
                Log(mission, "Mission fell short of objectives.");
            }
            else if (ratio > 0.4f)
            {
                result = MissionResultBand.Failure;
                Log(mission, "Mission failed.");
            }
            else
            {
                result = MissionResultBand.Disaster;
                Log(mission, "Disaster! Heavy losses sustained.");
            }

            return result;
        }

        private static float CalculateFriendlyEffectiveness(MissionData mission, AirbaseData baseData)
        {
            float score = 0;

            foreach (var assignment in mission.Assignments)
            {
                // Aircraft contribution based on mission type
                var aircraft = assignment.Aircraft?.Definition;
                if (aircraft != null)
                {
                    score += mission.Type switch
                    {
                        MissionType.Patrol or MissionType.Interception => aircraft.GetFighterEffectiveness(),
                        MissionType.Bombing => aircraft.GetBomberEffectiveness(),
                        MissionType.Reconnaissance => aircraft.GetReconEffectiveness(),
                        MissionType.Escort => (aircraft.GetFighterEffectiveness() + aircraft.GetDurabilityScore()) / 2,
                        _ => aircraft.GetFighterEffectiveness()
                    };

                    // Two-seater defensive bonus
                    if (aircraft.CrewSeats >= 2 && assignment.HasGunner())
                    {
                        score += aircraft.FirepowerRear * 0.5f;
                    }
                }

                // Crew contribution using FlightAssignment methods
                score += mission.Type switch
                {
                    MissionType.Patrol or MissionType.Interception => assignment.GetCombinedDogfightRating() / 10f,
                    MissionType.Bombing => assignment.GetBombingRating() / 10f,
                    MissionType.Reconnaissance => assignment.GetCombinedReconRating() / 10f,
                    MissionType.Escort => (assignment.GetCombinedDogfightRating() + assignment.GetDefensiveRating()) / 20f,
                    _ => assignment.GetCombinedDogfightRating() / 10f
                };

                // Skill bonuses (pilot only)
                if (assignment.Pilot != null)
                {
                    if (assignment.Pilot.HasSkill("ace")) score += 3;
                    if (assignment.Pilot.HasSkill("wingman")) score += 1.5f;
                    if (assignment.Pilot.HasSkill("steady")) score += 1;

                    // v2.1 Airframe Stress & Sturdy Pilot Interaction
                    if (assignment.Aircraft != null && assignment.Aircraft.AirframeStress > 20)
                    {
                        float stressPenalty = (assignment.Aircraft.AirframeStress - 20) / 30f; // Every 3 points above 20 is -0.1 effectiveness

                        if (assignment.Pilot.HasSkill("sturdy"))
                        {
                            stressPenalty *= 0.25f; // 75% reduction in penalty for sturdy pilots
                            Log(mission, $"{assignment.Pilot.Name} (Sturdy) handles the twitchy airframe of {assignment.Aircraft.TailNumber} with ease.");
                        }
                        else if (stressPenalty > 1.0f)
                        {
                            Log(mission, $"WARNING: {assignment.Pilot.Name} struggles with the severely warped airframe of {assignment.Aircraft.TailNumber}.");
                        }

                        score -= stressPenalty;
                    }
                }

                // Missing crew penalty for two-seaters
                if (aircraft != null && aircraft.CrewSeats >= 2 && !assignment.HasGunner() && !assignment.HasObserver())
                {
                    score -= 2; // Penalty for flying two-seater solo
                    Log(mission, $"{assignment.Pilot?.Name ?? "Unknown"} flying two-seater solo - reduced effectiveness.");
                }
            }

            // Base rating bonuses
            score += baseData.TrainingFacilitiesRating * 0.5f;
            score += baseData.OperationsRating * 0.3f;

            return score;
        }

        private static float CalculateEnemyEffectiveness(MissionData mission, int contactIntensity, string specialEvent)
        {
            float baseEnemy = 5 + (mission.TargetDistance * 0.2f);
            baseEnemy *= Math.Max(contactIntensity, 1);

            if (specialEvent == "AceEncounter") baseEnemy *= 2.5f;
            else if (specialEvent == "Zeppelin") baseEnemy *= 3.0f;

            baseEnemy += _rng.Next(-3, 4);
            return Math.Max(baseEnemy, 1);
        }

        private static void Log(MissionData mission, string message)
        {
            mission.MissionLog.Add(message);
            GD.Print($"[Mission] {message}");
        }
    }
}
