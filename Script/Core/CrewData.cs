using Godot;
using System;
using System.Linq;

namespace AceManager.Core
{
    public partial class CrewData : Resource
    {
        [Export] public string Name { get; set; }
        [Export] public string Role { get; set; } // Pilot, Planner, Logistics, Maintenance

        // Core Stats (0-100)
        [Export] public int CTL { get; set; } // Control
        [Export] public int GUN { get; set; } // Gunnery
        [Export] public int ENG { get; set; } // Energy Management
        [Export] public int RFX { get; set; } // Reaction
        [Export] public int OA { get; set; }  // Offensive Awareness
        [Export] public int DA { get; set; }  // Defensive Awareness
        [Export] public int TA { get; set; }  // Team Awareness
        [Export] public int WI { get; set; }  // Wingman Instinct
        [Export] public int AGG { get; set; } // Aggression
        [Export] public int DIS { get; set; } // Discipline
        [Export] public int CMP { get; set; } // Composure
        [Export] public int ADP { get; set; } // Adaptability
        [Export] public int LDR { get; set; } // Leadership
        [Export] public int LRN { get; set; } // Learning Rate
        [Export] public int STA { get; set; } // Stamina

        // Career Stats
        [Export] public int MissionsFlown { get; set; }
        [Export] public int AerialVictories { get; set; }
        [Export] public int GroundTargetsDestroyed { get; set; }

        // v2.0 Advancement Stats
        [Export] public int Merit { get; set; } = 0; // Hidden Command Trust
        [Export] public float Fatigue { get; set; } = 0; // 0-100 exhaustion
        [Export] public string CurrentRank { get; set; } = "Flight Sergeant";
        [Export] public string PrimaryRole { get; set; } = "Rookie";
        [Export] public string SecondaryRole { get; set; } = "";

        // Progression Tracking
        public System.Collections.Generic.Dictionary<string, float> DailyImprovements { get; set; } = new System.Collections.Generic.Dictionary<string, float>();

        public void AddImprovement(string statName, float amount)
        {
            if (!DailyImprovements.ContainsKey(statName))
                DailyImprovements[statName] = 0;

            // Learning rate modifies gain (100 LRN = 100% gain, 50 LRN = 50% gain)
            float modifiedAmount = amount * (LRN / 100.0f);

            // Apply Diminishing Returns (v2.0)
            int currentVal = GetStatByName(statName);
            if (currentVal >= 90) modifiedAmount *= 0.25f;
            else if (currentVal >= 80) modifiedAmount *= 0.5f;

            DailyImprovements[statName] += modifiedAmount;
        }

        private int GetStatByName(string name)
        {
            return name switch
            {
                "CTL" => CTL,
                "GUN" => GUN,
                "ENG" => ENG,
                "RFX" => RFX,
                "OA" => OA,
                "DA" => DA,
                "TA" => TA,
                "WI" => WI,
                "AGG" => AGG,
                "DIS" => DIS,
                "CMP" => CMP,
                "ADP" => ADP,
                "LDR" => LDR,
                "LRN" => LRN,
                "STA" => STA,
                _ => 0
            };
        }

        public void ApplyDailyImprovements()
        {
            foreach (var kvp in DailyImprovements)
            {
                int gain = (int)Math.Floor(kvp.Value);
                if (gain > 0)
                {
                    switch (kvp.Key)
                    {
                        case "CTL": CTL = Math.Min(100, CTL + gain); break;
                        case "GUN": GUN = Math.Min(100, GUN + gain); break;
                        case "ENG": ENG = Math.Min(100, ENG + gain); break;
                        case "RFX": RFX = Math.Min(100, RFX + gain); break;
                        case "OA": OA = Math.Min(100, OA + gain); break;
                        case "DA": DA = Math.Min(100, DA + gain); break;
                        case "TA": TA = Math.Min(100, TA + gain); break;
                        case "WI": WI = Math.Min(100, WI + gain); break;
                        case "AGG": AGG = Math.Min(100, AGG + gain); break;
                        case "DIS": DIS = Math.Min(100, DIS + gain); break;
                        case "CMP": CMP = Math.Min(100, CMP + gain); break;
                        case "ADP": ADP = Math.Min(100, ADP + gain); break;
                        case "LDR": LDR = Math.Min(100, LDR + gain); break;
                        case "STA": STA = Math.Min(100, STA + gain); break;
                    }
                }
            }
            ClearDailyImprovements();

            // Re-evaluate roles if it's a milestone mission
            if (MissionsFlown % 5 == 0)
            {
                UpdateRoles();
            }
            CheckPromotion();
        }

        public void ClearDailyImprovements()
        {
            DailyImprovements.Clear();
        }

        // Derived Ratings
        public float GetDogfightRating() => (0.25f * CTL) + (0.25f * GUN) + (0.20f * OA) + (0.15f * RFX) + (0.15f * ENG);
        public float GetEnergyFighterRating() => (0.30f * ENG) + (0.25f * CTL) + (0.20f * OA) + (0.15f * DIS) + (0.10f * CMP);
        public float GetGroundAttackRating() => (0.30f * DIS) + (0.25f * GUN) + (0.20f * CTL) + (0.15f * CMP) + (0.10f * STA);
        public float GetReconSurvivalRating() => (0.35f * DA) + (0.25f * OA) + (0.20f * ADP) + (0.20f * CMP);

        public int GetOverallRating()
        {
            // Simple average of 5 core combat stats for the roster OVR display
            return (int)Math.Round((CTL + GUN + OA + DA + CMP) / 5.0f);
        }

        public void UpdateRoles()
        {
            var roleScores = new System.Collections.Generic.Dictionary<string, float>();

            // 1) Fighter: (Dogfight Rating >= 70)
            float fightScore = GetDogfightRating();
            if (fightScore >= 70 && OA >= 65 && CTL >= 65 && CMP >= 45)
                roleScores["Fighter"] = fightScore;

            // 2) Bomber: (Ground Attack Rating >= 65)
            float bombScore = GetGroundAttackRating();
            if (bombScore >= 65 && DIS >= 65 && CMP >= 60)
                roleScores["Bomber"] = bombScore;

            // 3) Recon: (Recon Survival Rating >= 65)
            float reconScore = GetReconSurvivalRating();
            if (reconScore >= 65 && DA >= 70 && ADP >= 60)
                roleScores["Recon"] = reconScore;

            // 4) Gunner: (GUN >= 70, RFX >= 65)
            if (GUN >= 70 && RFX >= 65 && DA >= 60)
                roleScores["Gunner"] = (GUN + RFX + DA) / 3f;

            // 5) Mentor: (TA >= 70, DIS >= 65, CMP >= 65)
            if (TA >= 70 && DIS >= 65 && CMP >= 65)
                roleScores["Mentor"] = (TA + DIS + CMP) / 3f;

            // 6) Anti-Personnel: (GUN >= 65, AGG >= 70)
            if (GUN >= 65 && AGG >= 70 && DIS >= 45)
                roleScores["Strafing Specialist"] = (GUN + AGG) / 2f;

            // Sort by score
            var sortedRoles = roleScores.OrderByDescending(x => x.Value).ToList();

            if (sortedRoles.Count > 0)
            {
                PrimaryRole = sortedRoles[0].Key;
                if (sortedRoles.Count > 1 && sortedRoles[1].Value > sortedRoles[0].Value * 0.85f)
                    SecondaryRole = sortedRoles[1].Key;
                else
                    SecondaryRole = "";
            }
            else if (MissionsFlown < 10)
            {
                PrimaryRole = "Rookie";
                SecondaryRole = "";
            }
        }

        public void CheckPromotion()
        {
            string[] ranks = { "Flight Sergeant", "2nd Lieutenant", "1st Lieutenant", "Captain", "Major" };
            int[] meritThresholds = { 0, 50, 150, 400, 1000 };
            int[] missionThresholds = { 0, 10, 30, 75, 150 };

            int currentIndex = Array.IndexOf(ranks, CurrentRank);
            if (currentIndex == -1) currentIndex = 0;

            for (int i = ranks.Length - 1; i > currentIndex; i--)
            {
                if (Merit >= meritThresholds[i] && MissionsFlown >= missionThresholds[i])
                {
                    CurrentRank = ranks[i];
                    GD.Print($"PROMOTION: {Name} promoted to {CurrentRank}!");
                    break;
                }
            }
        }

        public bool HasSkill(string skillName)
        {
            switch (skillName.ToLower())
            {
                case "ace": return OA >= 70 && GUN >= 70 && ENG >= 70;
                case "wingman": return TA >= 70 && WI >= 70 && DA >= 70;
                case "steady": return CMP >= 70 && DA >= 70;
                case "survivor": return DA >= 70 && ADP >= 70 && CMP >= 70;
                case "overwatch": return DA >= 70 && TA >= 70 && CMP >= 70;
                case "pack hunter": return OA >= 70 && TA >= 70 && DIS >= 70;
                case "flight leader": return LDR >= 70 && TA >= 70;
                case "instructor": return LDR >= 70 && DIS >= 70 && LRN >= 70;
                default: return false;
            }
        }
    }
}
