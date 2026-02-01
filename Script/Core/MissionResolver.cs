using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AceManager.Core
{
    public static class MissionResolver
    {
        private static Random _rng = new Random();

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
            int contactIntensity = ResolveContact(mission, baseData, out specialEvent);
            bool hadContact = contactIntensity > 0 || !string.IsNullOrEmpty(specialEvent);

            // PHASE 3: Outcome Determination
            mission.ResultBand = CalculateOutcome(mission, baseData, contactIntensity, specialEvent);

            // PHASE 4: Consequences
            ApplyConsequences(mission, baseData, hadContact);

            // PHASE 5: Outcome Finalization (Adjust for losses)
            FinalizeResultBand(mission);

            // PHASE 6: Pilot Progression
            ApplyProgression(mission);

            // PHASE 7: Intel & Discovery
            CheckDiscovery(mission);

            mission.Status = MissionStatus.Resolved;
            LogEntry(mission, $"Mission complete. Result: {mission.ResultBand}");
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

        // ============================
        // PHASE 2: Contact & Engagement
        // ============================
        private static int ResolveContact(MissionData mission, AirbaseData baseData, out string specialEvent)
        {
            LogEntry(mission, "=== PHASE 2: Contact & Engagement ===");
            specialEvent = "";

            // Check for special events based on mission type and timeline
            if (mission.Type == MissionType.Interception || mission.Type == MissionType.Patrol)
            {
                if (_rng.Next(100) < 5) // 5% chance for Ace Encounter
                {
                    specialEvent = "AceEncounter";
                    LogEntry(mission, "[b][color=red]ALERT:[/color][/b] Intelligence reports a renowned enemy Ace has been spotted in the sector!");
                }
            }
            else if (mission.Type == MissionType.Reconnaissance || mission.Type == MissionType.Bombing)
            {
                if (_rng.Next(100) < 3) // 3% chance for Zeppelin
                {
                    specialEvent = "Zeppelin";
                    LogEntry(mission, "[b][color=yellow]SIGHTING:[/color][/b] A massive enemy Zeppelin has been spotted through the cloud layer!");
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
                LogEntry(mission, "No enemy contact encountered.");
                intensity = 0;
            }
            else if (roll < contactChance * 2 / 3)
            {
                LogEntry(mission, "Limited enemy contact. Skirmish ensues.");
                intensity = 1;
            }
            else if (roll < contactChance)
            {
                LogEntry(mission, "Full engagement! Heavy combat.");
                intensity = 2;
            }
            else
            {
                LogEntry(mission, "Enemy ambush! Caught off guard.");
                intensity = 3;
            }

            return intensity;
        }

        // ============================
        // PHASE 3: Outcome Determination
        // ============================
        private static MissionResultBand CalculateOutcome(MissionData mission, AirbaseData baseData, int contactIntensity, string specialEvent)
        {
            LogEntry(mission, "=== PHASE 3: Outcome Determination ===");

            if (contactIntensity == 0 && string.IsNullOrEmpty(specialEvent))
            {
                LogEntry(mission, "Mission completed without opposition.");
                return MissionResultBand.Success;
            }

            float friendlyScore = CalculateFriendlyEffectiveness(mission, baseData);
            float enemyScore = CalculateEnemyEffectiveness(mission, contactIntensity, specialEvent);

            LogEntry(mission, $"Friendly Score: {friendlyScore:F1} vs Enemy Score: {enemyScore:F1}");

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
                LogEntry(mission, "Decisive victory!");
            }
            else if (ratio > 1.5f)
            {
                result = MissionResultBand.Success;
                LogEntry(mission, "Mission successful.");
            }
            else if (ratio > 1.1f)
            {
                result = MissionResultBand.MarginalSuccess;
                LogEntry(mission, "Marginal success achieved.");
            }
            else if (ratio > 0.9f)
            {
                result = MissionResultBand.Stalemate;
                LogEntry(mission, "Stalemate. No clear winner.");
            }
            else if (ratio > 0.7f)
            {
                result = MissionResultBand.MarginalFailure;
                LogEntry(mission, "Mission fell short of objectives.");
            }
            else if (ratio > 0.4f)
            {
                result = MissionResultBand.Failure;
                LogEntry(mission, "Mission failed.");
            }
            else
            {
                result = MissionResultBand.Disaster;
                LogEntry(mission, "Disaster! Heavy losses sustained.");
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
                            LogEntry(mission, $"{assignment.Pilot.Name} (Sturdy) handles the twitchy airframe of {assignment.Aircraft.TailNumber} with ease.");
                        }
                        else if (stressPenalty > 1.0f)
                        {
                            LogEntry(mission, $"WARNING: {assignment.Pilot.Name} struggles with the severely warped airframe of {assignment.Aircraft.TailNumber}.");
                        }

                        score -= stressPenalty;
                    }
                }

                // Missing crew penalty for two-seaters
                if (aircraft != null && aircraft.CrewSeats >= 2 && !assignment.HasGunner() && !assignment.HasObserver())
                {
                    score -= 2; // Penalty for flying two-seater solo
                    LogEntry(mission, $"{assignment.Pilot?.Name ?? "Unknown"} flying two-seater solo - reduced effectiveness.");
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

        // ============================
        // PHASE 4: Consequences
        // ============================
        private static void ApplyConsequences(MissionData mission, AirbaseData baseData, bool hadContact)
        {
            LogEntry(mission, "=== PHASE 4: Consequences ===");

            // Resource consumption
            mission.FuelConsumed = mission.GetBaseFuelCost();
            mission.AmmoConsumed = mission.GetBaseAmmoCost();
            baseData.CurrentFuel -= mission.FuelConsumed;
            baseData.CurrentAmmo -= mission.AmmoConsumed;
            LogEntry(mission, $"Consumed: {mission.FuelConsumed} fuel, {mission.AmmoConsumed} ammo.");

            // Losses based on result band
            CalculateLosses(mission);

            // Enemy kills for successful missions IF contact was made
            if (hadContact && mission.ResultBand <= MissionResultBand.MarginalSuccess)
            {
                mission.EnemyKills = mission.ResultBand switch
                {
                    MissionResultBand.DecisiveSuccess => _rng.Next(2, 5),
                    MissionResultBand.Success => _rng.Next(1, 3),
                    MissionResultBand.MarginalSuccess => _rng.Next(0, 2),
                    _ => 0
                };

                // Distribute kills to pilots
                if (mission.EnemyKills > 0)
                {
                    LogEntry(mission, $"Confirmed enemy kills: {mission.EnemyKills}");
                    DistributeKills(mission, mission.EnemyKills);
                }
            }
        }

        private static void DistributeKills(MissionData mission, int killCount)
        {
            var validAssignments = mission.Assignments.Where(a => a.Pilot != null && a.Aircraft?.Status != AircraftStatus.Lost).ToList();
            if (validAssignments.Count == 0) return;

            for (int i = 0; i < killCount; i++)
            {
                var killer = validAssignments[_rng.Next(validAssignments.Count)];
                killer.Pilot.AerialVictories++;
                killer.KillsThisMission++;
                killer.Pilot.AddImprovement("GUN", 1.0f);
                killer.Pilot.AddImprovement("OA", 0.5f);
                killer.Pilot.AddImprovement("CMP", 0.5f);
                LogEntry(mission, $"CONFIRMED KILL: {killer.Pilot.Name} downed an enemy aircraft!");

                // Player captain merit gain if involved (mock logic for now or real if captain is a pilot)
                if (GameManager.Instance.PlayerCaptain != null)
                {
                    GameManager.Instance.PlayerCaptain.AddMerit(5);
                }
            }
        }

        private static void FinalizeResultBand(MissionData mission)
        {
            // Only adjust if it's currently a 'Success' band and we lost pilots
            if (mission.ResultBand <= MissionResultBand.MarginalSuccess && mission.CrewKilled > 0)
            {
                // Exchange Ratio: How many enemies did we down per friendly killed?
                float exchangeRatio = (mission.CrewKilled == 0) ? mission.EnemyKills : (float)mission.EnemyKills / mission.CrewKilled;

                var oldBand = mission.ResultBand;

                if (exchangeRatio < 1.0f) // Lost more than we killed
                {
                    // Downgrade by 2 steps
                    mission.ResultBand = (MissionResultBand)Math.Min((int)mission.ResultBand + 2, (int)MissionResultBand.MarginalFailure);
                }
                else if (exchangeRatio < 3.0f) // Killed some, but lost some (not a clean victory)
                {
                    // Downgrade by 1 step
                    mission.ResultBand = (MissionResultBand)Math.Min((int)mission.ResultBand + 1, (int)MissionResultBand.Stalemate);
                }

                if (oldBand != mission.ResultBand)
                {
                    LogEntry(mission, $"[b][color=orange]RESULT ADJUSTED:[/color][/b] Objectives were met, but heavy casualties ({mission.EnemyKills} vs {mission.CrewKilled}) have tempered the outcome.");
                }
            }
        }

        private static void ApplyProgression(MissionData mission)
        {
            foreach (var assignment in mission.Assignments)
            {
                var pilot = assignment.Pilot;
                if (pilot == null) continue;

                // 1. Basic Participation
                pilot.MissionsFlown++;
                pilot.AddImprovement("LRN", 0.1f); // Slightly slower than before
                pilot.AddImprovement("STA", 0.3f);

                // Merit and Fatigue (v2.0)
                int meritGain = 0;
                float fatigueGain = 10f + (mission.TargetDistance * 1.5f);

                // Risk posture affects fatigue
                if (mission.Risk == RiskPosture.Aggressive) fatigueGain *= 1.25f;
                else if (mission.Risk == RiskPosture.Conservative) fatigueGain *= 0.75f;

                pilot.Fatigue = Math.Min(100, pilot.Fatigue + fatigueGain);
                LogEntry(mission, $"{pilot.Name} fatigue increased by {fatigueGain:F1}.");

                // 2. Mission Type Bonuses
                switch (mission.Type)
                {
                    case MissionType.Patrol:
                    case MissionType.Interception:
                        pilot.AddImprovement("DA", 0.5f);
                        pilot.AddImprovement("CMP", 0.5f);
                        break;
                    case MissionType.Reconnaissance:
                        pilot.AddImprovement("ADP", 1.0f);
                        pilot.AddImprovement("DA", 1.0f);
                        pilot.AddImprovement("OA", 0.5f);
                        break;
                    case MissionType.Bombing:
                    case MissionType.Strafing:
                        pilot.AddImprovement("GUN", 0.8f);
                        pilot.AddImprovement("DIS", 0.5f);
                        if (mission.Type == MissionType.Strafing) pilot.GroundTargetsDestroyed++; // Abstracted
                        break;
                    case MissionType.Escort:
                        pilot.AddImprovement("TA", 0.8f);
                        pilot.AddImprovement("DIS", 0.5f);
                        break;
                }

                // 3. Survival Bonus (if not shot down)
                if (assignment.Aircraft?.Status != AircraftStatus.Lost)
                {
                    pilot.AddImprovement("CMP", 0.3f);
                }

                // 4. Order Compliance Bonus
                if (mission.FollowedOrders && mission.OrderBonus > 0)
                {
                    pilot.AddImprovement("DIS", 0.5f); // Discipline for following orders
                    meritGain += 5;

                    // Captain merit for good command decision
                    if (GameManager.Instance.PlayerCaptain != null)
                    {
                        GameManager.Instance.PlayerCaptain.AddMerit(2);
                    }
                }

                // 5. Outcome Merit (v2.0)
                meritGain += mission.ResultBand switch
                {
                    MissionResultBand.DecisiveSuccess => 15,
                    MissionResultBand.Success => 10,
                    MissionResultBand.MarginalSuccess => 5,
                    MissionResultBand.Stalemate => 2,
                    _ => 0
                };

                pilot.Merit += meritGain;
                if (meritGain > 0)
                {
                    LogEntry(mission, $"{pilot.Name} earned {meritGain} Merit for mission performance.");
                }

                // 6. Trait Breakout Logic (Rare)
                CheckForTraitBreakout(mission, pilot);

                // Apply the gains
                pilot.ApplyDailyImprovements();

                // 7. Generate Personal Log Entry
                string narrative = GeneratePersonalNarrative(assignment, mission);
                var entry = new PilotLogEntry(
                    GameManager.Instance.CurrentDate.ToString("MMM d, yyyy"),
                    mission.Type.ToString(),
                    narrative,
                    assignment.KillsThisMission,
                    mission.ResultBand.ToString(),
                    assignment.Pilot.Status == PilotStatus.Wounded,
                    assignment.Aircraft?.Status == AircraftStatus.Lost
                );

                pilot.AddLogEntry(entry);
            }
        }

        private static string GeneratePersonalNarrative(FlightAssignment assignment, MissionData mission)
        {
            var pilot = assignment.Pilot;
            bool wasCombat = mission.EnemyKills > 0 || mission.CrewWounded > 0 || mission.CrewKilled > 0;
            bool didKill = pilot.DailyImprovements.ContainsKey("GUN") && pilot.DailyImprovements["GUN"] > 0; // Simple check if they were the killer

            var templates = new List<string>();

            if (mission.ResultBand == MissionResultBand.Disaster)
            {
                templates.Add("A chaotic nightmare. The Huns were everywhere. I barely made it back through the clouds.");
                templates.Add("Absolute carnage. The formation broke early and it was every man for himself.");
                templates.Add("Lost sight of my wingman in the first pass. The sky was full of lead.");
            }
            else if (didKill)
            {
                templates.Add($"Caught a Fokker in my sights and didn't let go until he spiraled. My first confirmed kill.");
                templates.Add($"Closed the distance until I could see the pilot's face. One burst, and he was gone.");
                templates.Add($"Dived from the sun and surprised an enemy scout. He never saw me coming.");
            }
            else if (wasCombat)
            {
                templates.Add("Engaged a flight of enemy scouts over the lines. A wild scrap, but no clear result.");
                templates.Add("The Archie was thick today. Shrapnel peppered the wings, but the engine held true.");
                templates.Add("Traded shots with a bold German pilot. He knew his business, but I lived to tell the tale.");
            }
            else
            {
                templates.Add("A quiet patrol over the Amiens sector. Nothing but clouds and the occasional flash of artillery far below.");
                templates.Add("Visibility was poor. We scanned the horizon for hours but the Huns stayed home today.");
                templates.Add("Drifted over the trenches for an hour. The war looks very different from five thousand feet.");
            }

            return templates[_rng.Next(templates.Count)];
        }

        private static void CheckForTraitBreakout(MissionData mission, CrewData pilot)
        {
            // Base chance is very rare (5%)
            int chance = 5;

            // Modifiers
            if (pilot.Fatigue > 80) chance += 10;
            if (mission.ResultBand == MissionResultBand.Disaster) chance += 15;
            if (mission.ResultBand == MissionResultBand.DecisiveSuccess) chance += 10;

            if (_rng.Next(100) >= chance) return;

            bool isPositive = mission.ResultBand <= MissionResultBand.Success;

            // Randomly determine if it's positive or negative regardless of result, 
            // but success leans positive.
            if (mission.ResultBand == MissionResultBand.Disaster) isPositive = false;
            else if (mission.ResultBand == MissionResultBand.DecisiveSuccess) isPositive = true;
            else isPositive = _rng.Next(100) < 50;

            if (isPositive && pilot.PositiveTraits.Count < 3)
            {
                var trait = GeneratePositiveTrait(mission);
                if (!pilot.HasTrait(trait.TraitId))
                {
                    pilot.PositiveTraits.Add(trait);
                    LogEntry(mission, $"[b][color=green]BREAKOUT:[/color][/b] {pilot.Name} has developed a positive trait: [b]{trait.TraitName}[/b]!");
                }
            }
            else if (!isPositive && pilot.NegativeTraits.Count < 4)
            {
                var trait = GenerateNegativeTrait(mission);
                if (!pilot.HasTrait(trait.TraitId))
                {
                    pilot.NegativeTraits.Add(trait);
                    LogEntry(mission, $"[b][color=red]BREAKOUT:[/color][/b] {pilot.Name} has developed a negative trait: [b]{trait.TraitName}[/b]!");

                    if (pilot.NegativeTraits.Count >= 4)
                    {
                        // Breakdown!
                        GameManager.Instance.Roster.HospitalizePilot(pilot, 7 + _rng.Next(8));
                        LogEntry(mission, $"[b][color=red]BREAKDOWN:[/color][/b] The mental strain was too much. {pilot.Name} has been hospitalized.");
                    }
                }
            }
        }

        private static PilotTrait GeneratePositiveTrait(MissionData mission)
        {
            return _rng.Next(3) switch
            {
                0 => PilotTrait.Create("deadeye", "Dead-Eye", "+10 Gunnery after confirmed success.", true, ("GUN", 10)),
                1 => PilotTrait.Create("eagleeye", "Eagle-Eye", "+10 Offensive Awareness.", true, ("OA", 10)),
                _ => PilotTrait.Create("ace_nerve", "Ace Nerve", "+10 Composure under pressure.", true, ("CMP", 10))
            };
        }

        private static PilotTrait GenerateNegativeTrait(MissionData mission)
        {
            return _rng.Next(3) switch
            {
                0 => PilotTrait.Create("jittery", "Jittery", "-10 Gunnery and -10 Composure.", false, ("GUN", -10), ("CMP", -10)),
                1 => PilotTrait.Create("cloud_blind", "Cloud Blind", "-15 Offensive Awareness.", false, ("OA", -15)),
                _ => PilotTrait.Create("shell_shock", "Shell Shock", "-10 to all combat awareness.", false, ("OA", -10), ("DA", -10), ("TA", -10))
            };
        }

        private static void CalculateLosses(MissionData mission)
        {
            int lossChance = mission.ResultBand switch
            {
                MissionResultBand.DecisiveSuccess => 5,
                MissionResultBand.Success => 10,
                MissionResultBand.MarginalSuccess => 20,
                MissionResultBand.Stalemate => 30,
                MissionResultBand.MarginalFailure => 40,
                MissionResultBand.Failure => 60,
                MissionResultBand.Disaster => 80,
                _ => 20
            };

            foreach (var assignment in mission.Assignments)
            {
                // Aircraft damage/loss
                if (assignment.Aircraft != null)
                {
                    int roll = _rng.Next(100);
                    if (roll < lossChance / 3)
                    {
                        mission.AircraftLost++;
                        assignment.Aircraft.Status = AircraftStatus.Lost;
                        LogEntry(mission, $"{assignment.Aircraft.GetDisplayName()} was lost.");
                    }
                    else if (roll < lossChance)
                    {
                        int damage = _rng.Next(10, 40);
                        assignment.Aircraft.ApplyDamage(damage);
                        LogEntry(mission, $"{assignment.Aircraft.GetDisplayName()} took {damage}% damage.");
                    }
                }

                // Crew casualties - check each seat
                ProcessCrewLoss(mission, assignment.Pilot, "Pilot", lossChance);
                ProcessCrewLoss(mission, assignment.Gunner, "Gunner", lossChance);
                ProcessCrewLoss(mission, assignment.Observer, "Observer", lossChance);
            }
        }

        private static void ProcessCrewLoss(MissionData mission, CrewData crew, string role, int lossChance)
        {
            if (crew == null) return;

            int roll = _rng.Next(100);

            // Gunners/Observers are slightly safer than pilots
            int adjustedChance = role == "Pilot" ? lossChance : (int)(lossChance * 0.8f);

            if (roll < adjustedChance / 4)
            {
                mission.CrewKilled++;
                LogEntry(mission, $"{role} {crew.Name} was killed in action.");
            }
            else if (roll < adjustedChance / 2)
            {
                mission.CrewWounded++;
                LogEntry(mission, $"{role} {crew.Name} was wounded.");
            }
        }

        private static void LogEntry(MissionData mission, string message)
        {
            mission.MissionLog.Add(message);
            GD.Print($"[Mission] {message}");
        }

        private static float GetDistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            Vector2 ap = p - a;
            float lengthSq = ab.LengthSquared();
            if (lengthSq == 0) return ap.Length();

            float t = Math.Max(0, Math.Min(1, ap.Dot(ab) / lengthSq));
            Vector2 projection = a + t * ab;
            return p.DistanceTo(projection);
        }

        private static void CheckDiscovery(MissionData mission)
        {
            var gm = GameManager.Instance;
            if (gm?.SectorMap == null || mission.Waypoints == null || mission.Waypoints.Count < 2) return;

            // Only discover if someone returns home
            if (mission.Assignments.All(a => a.Aircraft.Status == AircraftStatus.Lost)) return;

            // Use the recon rating from the flight assignments
            float reconScore = 0;
            foreach (var a in mission.Assignments)
            {
                reconScore += a.GetCombinedReconRating();
            }

            int distanceUnits = mission.TargetDistance;

            // Filter hidden locations by proximity to flight segments
            var hidden = gm.SectorMap.Locations.Where(l => !l.IsDiscovered).ToList();
            var candidates = new List<MapLocation>();

            foreach (var loc in hidden)
            {
                float minDistance = float.MaxValue;
                for (int i = 0; i < mission.Waypoints.Count - 1; i++)
                {
                    float dist = GetDistanceToSegment(loc.WorldCoordinates, mission.Waypoints[i], mission.Waypoints[i + 1]);
                    if (dist < minDistance) minDistance = dist;
                }

                // If within 15 units of any segment, it's a candidate
                if (minDistance < 15f)
                {
                    candidates.Add(loc);
                }
            }

            if (candidates.Count == 0) return;

            // Base chance: 5% + distance scaling + recon bonus
            float baseChance = 5 + (distanceUnits / 5f);
            int roll = _rng.Next(100);

            if (roll < baseChance + (reconScore / 10f))
            {
                var found = candidates[_rng.Next(candidates.Count)];
                found.IsDiscovered = true;
                found.DiscoveredDate = gm.CurrentDate;

                LogEntry(mission, $"RECON REPORT: Pilots spotted and confirmed the location of [b]{found.Name}[/b] during the flight.");
                GD.Print($"Location discovered: {found.Name}");
            }
        }
    }
}
