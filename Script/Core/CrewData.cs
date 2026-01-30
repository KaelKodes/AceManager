using Godot;
using System;

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

        // Progression Tracking
        public System.Collections.Generic.Dictionary<string, float> DailyImprovements { get; set; } = new System.Collections.Generic.Dictionary<string, float>();

        public void AddImprovement(string statName, float amount)
        {
            if (!DailyImprovements.ContainsKey(statName))
                DailyImprovements[statName] = 0;

            // Learning rate modifies gain (100 LRN = 100% gain, 50 LRN = 50% gain)
            float modifiedAmount = amount * (LRN / 100.0f);
            DailyImprovements[statName] += modifiedAmount;
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

        public bool HasSkill(string skillName)
        {
            switch (skillName.ToLower())
            {
                case "ace": return OA > 70 && GUN > 70 && ENG > 70;
                case "wingman": return TA > 70 && WI > 70 && DA > 70;
                case "steady": return CMP > 70 && DA > 70;
                case "survivor": return DA > 70 && ADP > 70 && CMP > 70;
                // Add more skills as defined in CrewTemplate.txt
                default: return false;
            }
        }
    }
}
