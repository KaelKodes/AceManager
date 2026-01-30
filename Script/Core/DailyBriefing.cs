using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AceManager.Core
{
    public enum WeatherCondition
    {
        Clear,
        PartlyCloudy,
        Overcast,
        Fog,
        Rain,
        Storm
    }

    public enum CommandPriority
    {
        Patrol,           // "increased patrol activity"
        Defensive,        // "defensive operations" → Interception
        Reconnaissance,   // "reconnaissance high priority"
        GroundSupport,    // "support ground offensive" → Strafing or Bombing
        Escort,           // "escort bombers to target"
        ConserveResources // Skip day preferred
    }

    public partial class DailyBriefing : Resource
    {
        public DateTime Date { get; set; }
        public WeatherCondition Weather { get; set; }
        public int Visibility { get; set; } // 1-10 scale
        public int EnemyActivityLevel { get; set; } // 1-10 scale
        public List<string> IntelReports { get; set; } = new List<string>();
        public List<string> AlliedNews { get; set; } = new List<string>();
        public string CommandMessage { get; set; }
        public CommandPriority TodaysPriority { get; set; }

        private static Random _rng = new Random();

        /// <summary>
        /// Returns mission types that match today's command priority.
        /// Empty list means skip day / conserve resources.
        /// </summary>
        public List<MissionType> GetMatchingMissionTypes()
        {
            return TodaysPriority switch
            {
                CommandPriority.Patrol => new() { MissionType.Patrol },
                CommandPriority.Defensive => new() { MissionType.Interception, MissionType.Patrol },
                CommandPriority.Reconnaissance => new() { MissionType.Reconnaissance },
                CommandPriority.GroundSupport => new() { MissionType.Bombing, MissionType.Strafing },
                CommandPriority.Escort => new() { MissionType.Escort },
                CommandPriority.ConserveResources => new() { }, // Empty = skip day
                _ => new() { }
            };
        }

        /// <summary>
        /// Check if the given mission type follows today's orders.
        /// </summary>
        public bool DoesMissionFollowOrders(MissionType type)
        {
            var matching = GetMatchingMissionTypes();
            return matching.Count == 0 || matching.Contains(type);
        }

        public static DailyBriefing Generate(DateTime date, AirbaseData baseData, MissionData lastMission = null)
        {
            var briefing = new DailyBriefing
            {
                Date = date,
                Weather = GenerateWeather(date),
                EnemyActivityLevel = _rng.Next(1, 11)
            };

            // Visibility based on weather
            briefing.Visibility = briefing.Weather switch
            {
                WeatherCondition.Clear => _rng.Next(8, 11),
                WeatherCondition.PartlyCloudy => _rng.Next(6, 9),
                WeatherCondition.Overcast => _rng.Next(4, 7),
                WeatherCondition.Fog => _rng.Next(1, 4),
                WeatherCondition.Rain => _rng.Next(3, 6),
                WeatherCondition.Storm => _rng.Next(1, 3),
                _ => 5
            };

            // Generate intel reports (quality based on Operations rating)
            GenerateIntelReports(briefing, baseData);

            // Generate allied news
            GenerateAlliedNews(briefing, date);

            // Add performance feedback
            if (lastMission != null)
            {
                GeneratePerformanceReport(briefing, lastMission);
            }

            // Command message
            GenerateCommandMessage(briefing, baseData);

            return briefing;
        }

        private static void GeneratePerformanceReport(DailyBriefing briefing, MissionData lastMission)
        {
            if (lastMission.ResultBand <= MissionResultBand.Success)
                briefing.AlliedNews.Insert(0, "Command is pleased with yesterday's mission results.");
            else if (lastMission.ResultBand >= MissionResultBand.Failure)
                briefing.AlliedNews.Insert(0, "Morale is low following yesterday's losses.");

            foreach (var assignment in lastMission.Assignments)
            {
                var pilot = assignment.Pilot;
                if (pilot == null || pilot.DailyImprovements.Count == 0) continue;

                var bestStat = pilot.DailyImprovements.OrderByDescending(kvp => kvp.Value).First();
                if (bestStat.Value >= 0.5f)
                {
                    string statName = bestStat.Key;
                    string msg = statName switch
                    {
                        "GUN" => $"{pilot.Name} has shown improved marksmanship.",
                        "CTL" => $"{pilot.Name} is handling the aircraft with more confidence.",
                        "OA" => $"{pilot.Name} is spotting targets more effectively.",
                        "DA" => $"{pilot.Name}'s defensive flying has improved.",
                        "LDR" => $"{pilot.Name} is showing promise as a flight leader.",
                        _ => $"{pilot.Name} learned valuable lessons yesterday."
                    };

                    if (_rng.Next(100) < 30) briefing.AlliedNews.Add(msg);
                }
                pilot.ClearDailyImprovements();
            }
        }

        private static WeatherCondition GenerateWeather(DateTime date)
        {
            // Seasonal variation - winter more likely fog/rain, summer clearer
            int month = date.Month;
            int roll = _rng.Next(100);

            if (month >= 11 || month <= 2) // Winter
            {
                if (roll < 15) return WeatherCondition.Clear;
                if (roll < 30) return WeatherCondition.PartlyCloudy;
                if (roll < 50) return WeatherCondition.Overcast;
                if (roll < 70) return WeatherCondition.Fog;
                if (roll < 90) return WeatherCondition.Rain;
                return WeatherCondition.Storm;
            }
            else if (month >= 5 && month <= 8) // Summer
            {
                if (roll < 40) return WeatherCondition.Clear;
                if (roll < 65) return WeatherCondition.PartlyCloudy;
                if (roll < 80) return WeatherCondition.Overcast;
                if (roll < 90) return WeatherCondition.Rain;
                return WeatherCondition.Storm;
            }
            else // Spring/Fall
            {
                if (roll < 25) return WeatherCondition.Clear;
                if (roll < 50) return WeatherCondition.PartlyCloudy;
                if (roll < 70) return WeatherCondition.Overcast;
                if (roll < 85) return WeatherCondition.Rain;
                if (roll < 95) return WeatherCondition.Fog;
                return WeatherCondition.Storm;
            }
        }

        private static void GenerateIntelReports(DailyBriefing briefing, AirbaseData baseData)
        {
            int opsRating = baseData?.OperationsRating ?? 1;
            int reportCount = Math.Min(opsRating, 3);

            string[] activityDescriptions = {
                "Enemy patrol spotted over sector 7",
                "Reconnaissance suggests increased enemy presence near the front",
                "Artillery observers report light air activity to the east",
                "Allied spotters confirm enemy bombers heading south",
                "No significant enemy air activity reported",
                "Heavy enemy fighter patrols expected near objective zones",
                "Anti-aircraft positions reinforced along supply routes",
                "Enemy observation balloons active near the trenches"
            };

            string[] inaccurateReports = {
                "Reports unclear - fog obscuring observation posts",
                "Intelligence unreliable - confirm with caution",
                "Conflicting reports from forward positions"
            };

            for (int i = 0; i < reportCount; i++)
            {
                // Higher ops rating = more accurate intel
                if (_rng.Next(10) < opsRating)
                {
                    briefing.IntelReports.Add(activityDescriptions[_rng.Next(activityDescriptions.Length)]);
                }
                else
                {
                    briefing.IntelReports.Add(inaccurateReports[_rng.Next(inaccurateReports.Length)]);
                }
            }

            // Always add enemy activity level estimate
            string activityDesc = briefing.EnemyActivityLevel switch
            {
                <= 3 => "low",
                <= 6 => "moderate",
                <= 8 => "high",
                _ => "very high"
            };
            briefing.IntelReports.Insert(0, $"Expected enemy activity: {activityDesc}");
        }

        private static void GenerateAlliedNews(DailyBriefing briefing, DateTime date)
        {
            string[] goodNews = {
                "Allied offensive making progress along the Somme",
                "RFC squadron reports successful bombing raid",
                "Enemy ace shot down over friendly lines",
                "Supply convoy arrived safely at forward depot",
                "Allied reinforcements arriving at sector HQ"
            };

            string[] neutralNews = {
                "No significant changes on the Western Front",
                "Trench lines holding steady",
                "Weather delays operations across the sector",
                "Routine supply operations continue"
            };

            string[] badNews = {
                "German counter-attack repulsed with losses",
                "Allied airfield damaged in enemy raid",
                "Supply shortage affecting neighboring squadrons",
                "Heavy losses reported in sector 4"
            };

            // 1-2 news items
            int newsCount = _rng.Next(1, 3);
            for (int i = 0; i < newsCount; i++)
            {
                int roll = _rng.Next(100);
                if (roll < 30)
                    briefing.AlliedNews.Add(goodNews[_rng.Next(goodNews.Length)]);
                else if (roll < 70)
                    briefing.AlliedNews.Add(neutralNews[_rng.Next(neutralNews.Length)]);
                else
                    briefing.AlliedNews.Add(badNews[_rng.Next(badNews.Length)]);
            }
        }

        private static void GenerateCommandMessage(DailyBriefing briefing, AirbaseData baseData)
        {
            // Each priority paired with its message
            var priorityMessages = new (CommandPriority priority, string message)[]
            {
                (CommandPriority.Patrol, "HQ requests increased patrol activity today."),
                (CommandPriority.Patrol, "Maintain air superiority over our sector."),
                (CommandPriority.Defensive, "Focus on defensive operations until further notice."),
                (CommandPriority.Defensive, "Be prepared to intercept enemy raids."),
                (CommandPriority.Reconnaissance, "Reconnaissance missions are high priority."),
                (CommandPriority.Reconnaissance, "Command needs eyes on enemy positions."),
                (CommandPriority.GroundSupport, "Stand ready to support ground offensive."),
                (CommandPriority.GroundSupport, "Attack enemy supply lines and reinforcements."),
                (CommandPriority.Escort, "Bomber squadron needs fighter escort today."),
                (CommandPriority.Escort, "Protect our reconnaissance flights."),
                (CommandPriority.ConserveResources, "Conserve resources - supply convoy delayed."),
                (CommandPriority.ConserveResources, "Stand down unless contact is made.")
            };

            var selected = priorityMessages[_rng.Next(priorityMessages.Length)];
            briefing.TodaysPriority = selected.priority;
            briefing.CommandMessage = selected.message;
        }

        public string GetWeatherDescription()
        {
            return Weather switch
            {
                WeatherCondition.Clear => "Clear skies",
                WeatherCondition.PartlyCloudy => "Partly cloudy",
                WeatherCondition.Overcast => "Overcast",
                WeatherCondition.Fog => "Heavy fog",
                WeatherCondition.Rain => "Rain",
                WeatherCondition.Storm => "Storm - grounded conditions",
                _ => "Unknown"
            };
        }

        public bool IsFlightGrounded()
        {
            return Weather == WeatherCondition.Storm || Visibility <= 2;
        }
    }
}
