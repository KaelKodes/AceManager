using Godot;
using System;

namespace AceManager.Core
{
    /// <summary>
    /// Player character data - the commanding officer of the squadron.
    /// </summary>
    public partial class CaptainData : Resource
    {
        [Export] public string Name { get; set; } = "James Whitmore";
        [Export] public string Rank { get; set; } = "Captain";
        [Export] public int Merit { get; set; } = 0;

        [Export] public int MissionsCommanded { get; set; } = 0;
        [Export] public int VictoriesUnderCommand { get; set; } = 0;
        [Export] public int LossesUnderCommand { get; set; } = 0;



        // Rank progression thresholds
        private static readonly (string rank, int meritRequired)[] RankProgression = new[]
        {
            ("Second Lieutenant", 0),
            ("Lieutenant", 50),
            ("Captain", 150),
            ("Major", 400),
            ("Lieutenant Colonel", 800),
            ("Colonel", 1500)
        };

        public string GetFullTitle()
        {
            string abbrev = Rank switch
            {
                "Second Lieutenant" => "2Lt.",
                "Lieutenant" => "Lt.",
                "Captain" => "Capt.",
                "Major" => "Maj.",
                "Lieutenant Colonel" => "Lt. Col.",
                "Colonel" => "Col.",
                _ => ""
            };
            return $"{abbrev} {Name}";
        }

        public void AddMerit(int amount)
        {
            Merit += amount;
            CheckPromotion();
        }

        private void CheckPromotion()
        {
            foreach (var (rank, meritRequired) in RankProgression)
            {
                if (Merit >= meritRequired && Rank != rank)
                {
                    // Only promote, never demote
                    int currentIndex = Array.FindIndex(RankProgression, r => r.rank == Rank);
                    int newIndex = Array.FindIndex(RankProgression, r => r.rank == rank);
                    if (newIndex > currentIndex)
                    {
                        Rank = rank;
                        GD.Print($"PROMOTION: {Name} promoted to {Rank}!");
                    }
                }
            }
        }

        public int GetMeritToNextRank()
        {
            int currentIndex = Array.FindIndex(RankProgression, r => r.rank == Rank);
            if (currentIndex < RankProgression.Length - 1)
            {
                return RankProgression[currentIndex + 1].meritRequired - Merit;
            }
            return 0; // Already max rank
        }


    }
}
