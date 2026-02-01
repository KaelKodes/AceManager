using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AceManager.Core
{
    public static class PostMissionLogic
    {
        private static Random _rng = new Random();

        public static void ApplyConsequences(MissionData mission, AirbaseData baseData, bool hadContact)
        {
            Log(mission, "=== PHASE 4: Consequences ===");

            // Resource consumption
            // Resource consumption
            float efficiency = 0f;
            if (baseData != null)
            {
                efficiency = baseData.GetEfficiencyBonus();
            }

            mission.FuelConsumed = mission.GetBaseFuelCost(efficiency);
            mission.AmmoConsumed = mission.GetBaseAmmoCost(efficiency);
            baseData.CurrentFuel -= mission.FuelConsumed;
            baseData.CurrentAmmo -= mission.AmmoConsumed;
            Log(mission, $"Consumed: {mission.FuelConsumed} fuel, {mission.AmmoConsumed} ammo (Logistics: {(int)(efficiency * 100)}%).");

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
                    Log(mission, $"Confirmed enemy kills: {mission.EnemyKills}");
                    DistributeKills(mission, mission.EnemyKills);
                }
            }
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
                        Log(mission, $"{assignment.Aircraft.GetDisplayName()} was lost.");
                    }
                    else if (roll < lossChance)
                    {
                        int damage = _rng.Next(10, 40);
                        assignment.Aircraft.ApplyDamage(damage);
                        Log(mission, $"{assignment.Aircraft.GetDisplayName()} took {damage}% damage.");
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
                Log(mission, $"{role} {crew.Name} was killed in action.");
            }
            else if (roll < adjustedChance / 2)
            {
                mission.CrewWounded++;
                Log(mission, $"{role} {crew.Name} was wounded.");
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
                Log(mission, $"CONFIRMED KILL: {killer.Pilot.Name} downed an enemy aircraft!");

                // Player captain merit gain if involved (mock logic for now or real if captain is a pilot)
                if (GameManager.Instance.PlayerCaptain != null)
                {
                    GameManager.Instance.PlayerCaptain.AddMerit(5);
                }
            }
        }

        public static void FinalizeResultBand(MissionData mission)
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
                    Log(mission, $"[b][color=orange]RESULT ADJUSTED:[/color][/b] Objectives were met, but heavy casualties ({mission.EnemyKills} vs {mission.CrewKilled}) have tempered the outcome.");
                }
            }
        }

        public static void ApplyProgression(MissionData mission)
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
                Log(mission, $"{pilot.Name} fatigue increased by {fatigueGain:F1}.");

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
                    Log(mission, $"{pilot.Name} earned {meritGain} Merit for mission performance.");
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

        private static void CheckForTraitBreakout(MissionData mission, CrewData pilot)
        {
            // Base chance is very rare (5%)
            int chance = 5;

            if (pilot.Fatigue > 80) chance += 10;
            if (mission.ResultBand == MissionResultBand.Disaster) chance += 15;
            if (mission.ResultBand == MissionResultBand.DecisiveSuccess) chance += 10;

            if (_rng.Next(100) >= chance) return;

            bool isPositive = mission.ResultBand <= MissionResultBand.Success;

            if (mission.ResultBand == MissionResultBand.Disaster) isPositive = false;
            else if (mission.ResultBand == MissionResultBand.DecisiveSuccess) isPositive = true;
            else isPositive = _rng.Next(100) < 50;

            if (isPositive && pilot.PositiveTraits.Count < 3)
            {
                var trait = GeneratePositiveTrait(mission);
                if (!pilot.HasTrait(trait.TraitId))
                {
                    pilot.PositiveTraits.Add(trait);
                    Log(mission, $"[b][color=green]BREAKOUT:[/color][/b] {pilot.Name} has developed a positive trait: [b]{trait.TraitName}[/b]!");
                }
            }
            else if (!isPositive && pilot.NegativeTraits.Count < 4)
            {
                var trait = GenerateNegativeTrait(mission);
                if (!pilot.HasTrait(trait.TraitId))
                {
                    pilot.NegativeTraits.Add(trait);
                    Log(mission, $"[b][color=red]BREAKOUT:[/color][/b] {pilot.Name} has developed a negative trait: [b]{trait.TraitName}[/b]!");

                    if (pilot.NegativeTraits.Count >= 4)
                    {
                        GameManager.Instance.Roster.HospitalizePilot(pilot, 7 + _rng.Next(8));
                        Log(mission, $"[b][color=red]BREAKDOWN:[/color][/b] The mental strain was too much. {pilot.Name} has been hospitalized.");
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

        private static string GeneratePersonalNarrative(FlightAssignment assignment, MissionData mission)
        {
            var pilot = assignment.Pilot;
            bool wasCombat = mission.EnemyKills > 0 || mission.CrewWounded > 0 || mission.CrewKilled > 0;
            bool didKill = pilot.DailyImprovements.ContainsKey("GUN") && pilot.DailyImprovements["GUN"] > 0;

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

        public static void CheckDiscovery(MissionData mission)
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

                if (minDistance < 15f)
                {
                    candidates.Add(loc);
                }
            }

            if (candidates.Count == 0) return;

            float baseChance = 5 + (distanceUnits / 5f);
            int roll = _rng.Next(100);

            if (roll < baseChance + (reconScore / 10f))
            {
                var found = candidates[_rng.Next(candidates.Count)];
                found.IsDiscovered = true;
                found.DiscoveredDate = gm.CurrentDate;

                Log(mission, $"RECON REPORT: Pilots spotted and confirmed the location of [b]{found.Name}[/b] during the flight.");
                GD.Print($"Location discovered: {found.Name}");
            }
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

        private static void Log(MissionData mission, string message)
        {
            mission.MissionLog.Add(message);
            GD.Print($"[Mission] {message}");
        }
    }
}
