using Godot;
using System;
using System.Collections.Generic;

namespace AceManager.Core
{
    public enum TrainingLessonType
    {
        DeflectionShooting,
        AdvancedFlight,
        TacticalBriefing,
        SquadronDrill,
        CommandPost
    }

    public class TrainingLesson
    {
        public TrainingLessonType Type { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string[] PrimaryStats { get; set; }
        public Texture2D Icon { get; set; }

        public static List<TrainingLesson> GetAll()
        {
            return new List<TrainingLesson>
            {
                new TrainingLesson {
                    Type = TrainingLessonType.DeflectionShooting,
                    Name = "Deflection Shooting",
                    Description = "Live-fire practice against towed targets. Focuses on lead and aggression.",
                    PrimaryStats = new[] { "GUN", "AGG" }
                },
                new TrainingLesson {
                    Type = TrainingLessonType.AdvancedFlight,
                    Name = "Advanced Flight",
                    Description = "Practicing high-G maneuvers and energy conservation.",
                    PrimaryStats = new[] { "CTL", "ENG" }
                },
                new TrainingLesson {
                    Type = TrainingLessonType.TacticalBriefing,
                    Name = "Tactical Briefing",
                    Description = "Studying enemy formations and defensive break-turns.",
                    PrimaryStats = new[] { "OA", "DA" }
                },
                new TrainingLesson {
                    Type = TrainingLessonType.SquadronDrill,
                    Name = "Squadron Drill",
                    Description = "Coordinating wingman coverage and blind-spot checks.",
                    PrimaryStats = new[] { "TA", "WI" }
                },
                new TrainingLesson {
                    Type = TrainingLessonType.CommandPost,
                    Name = "Command Post",
                    Description = "Mental discipline and composure under chaotic conditions.",
                    PrimaryStats = new[] { "DIS", "CMP" }
                }
            };
        }
    }

    public partial class TrainingSession : Resource
    {
        [Export] public TrainingLessonType LessonType { get; set; }
        [Export] public string CoInstructorPilotId { get; set; }
        [Export] public Godot.Collections.Array<string> AttendeePilotIds { get; set; } = new();

        public float CalculateBaseXP(int trainingFacilityRating)
        {
            // Base gain between 2-5 points depending on facility level
            return 1.5f + (trainingFacilityRating * 0.75f);
        }

        public float GetInstructorBonus(CrewData coInstructor)
        {
            if (coInstructor == null) return 1.0f;

            // If the co-instructor is an expert (skill level > 70) in the lesson's primary stats, they give a bonus
            var lesson = TrainingLesson.GetAll().Find(l => l.Type == LessonType);
            float bonus = 1.0f;

            foreach (var stat in lesson.PrimaryStats)
            {
                int statVal = coInstructor.GetEffectiveStat(stat);
                if (statVal >= 80) bonus += 0.25f; // Big bonus for masters
                else if (statVal >= 60) bonus += 0.10f; // Small bonus for experts
            }

            return bonus;
        }
    }
}
