using Godot;
using Godot.Collections;
using System;
using System.Linq;
using System.Collections.Generic;

namespace AceManager.Core
{
    public enum PilotStatus
    {
        Active,
        Wounded,
        Hospitalized,
        KIA
    }

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

        // Status & Recovery
        [Export] public PilotStatus Status { get; set; } = PilotStatus.Active;
        [Export] public int RecoveryDays { get; set; } = 0;

        // Traits (Rare Breakouts)
        [Export] public Godot.Collections.Array<PilotTrait> PositiveTraits { get; set; } = new Godot.Collections.Array<PilotTrait>();
        [Export] public Godot.Collections.Array<PilotTrait> NegativeTraits { get; set; } = new Godot.Collections.Array<PilotTrait>();

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

        public int GetEffectiveStat(string statName)
        {
            return Math.Clamp(GetStatByName(statName) + GetTraitModifier(statName), 0, 100);
        }

        public int GetTraitModifier(string statName)
        {
            int mod = 0;
            foreach (var trait in PositiveTraits)
            {
                if (trait != null && trait.StatModifiers.ContainsKey(statName))
                    mod += (int)trait.StatModifiers[statName];
            }
            foreach (var trait in NegativeTraits)
            {
                if (trait != null && trait.StatModifiers.ContainsKey(statName))
                    mod += (int)trait.StatModifiers[statName];
            }
            return mod;
        }

        public bool HasTrait(string traitId)
        {
            return PositiveTraits.Any(t => t?.TraitId == traitId) || NegativeTraits.Any(t => t?.TraitId == traitId);
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
        public float GetDogfightRating() => (0.25f * GetEffectiveStat("CTL")) + (0.25f * GetEffectiveStat("GUN")) + (0.20f * GetEffectiveStat("OA")) + (0.15f * GetEffectiveStat("RFX")) + (0.15f * GetEffectiveStat("ENG"));
        public float GetEnergyFighterRating() => (0.30f * GetEffectiveStat("ENG")) + (0.25f * GetEffectiveStat("CTL")) + (0.20f * GetEffectiveStat("OA")) + (0.15f * GetEffectiveStat("DIS")) + (0.10f * GetEffectiveStat("CMP"));
        public float GetGroundAttackRating() => (0.30f * GetEffectiveStat("DIS")) + (0.25f * GetEffectiveStat("GUN")) + (0.20f * GetEffectiveStat("CTL")) + (0.15f * GetEffectiveStat("CMP")) + (0.10f * GetEffectiveStat("STA"));
        public float GetReconSurvivalRating() => (0.35f * GetEffectiveStat("DA")) + (0.25f * GetEffectiveStat("OA")) + (0.20f * GetEffectiveStat("ADP")) + (0.20f * GetEffectiveStat("CMP"));

        public int GetOverallRating()
        {
            // Simple average of 5 core combat stats for the roster OVR display
            return (int)Math.Round((GetEffectiveStat("CTL") + GetEffectiveStat("GUN") + GetEffectiveStat("OA") + GetEffectiveStat("DA") + GetEffectiveStat("CMP")) / 5.0f);
        }

        public void UpdateRoles()
        {
            var roleScores = new System.Collections.Generic.Dictionary<string, float>();

            // 1) Fighter: (Dogfight Rating >= 70)
            float fightScore = GetDogfightRating();
            if (fightScore >= 70 && GetEffectiveStat("OA") >= 65 && GetEffectiveStat("CTL") >= 65 && GetEffectiveStat("CMP") >= 45)
                roleScores["Fighter"] = fightScore;

            // 2) Bomber: (Ground Attack Rating >= 65)
            float bombScore = GetGroundAttackRating();
            if (bombScore >= 65 && GetEffectiveStat("DIS") >= 65 && GetEffectiveStat("CMP") >= 60)
                roleScores["Bomber"] = bombScore;

            // 3) Recon: (Recon Survival Rating >= 65)
            float reconScore = GetReconSurvivalRating();
            if (reconScore >= 65 && GetEffectiveStat("DA") >= 70 && GetEffectiveStat("ADP") >= 60)
                roleScores["Recon"] = reconScore;

            // 4) Gunner: (GUN >= 70, RFX >= 65)
            if (GetEffectiveStat("GUN") >= 70 && GetEffectiveStat("RFX") >= 65 && GetEffectiveStat("DA") >= 60)
                roleScores["Gunner"] = (GetEffectiveStat("GUN") + GetEffectiveStat("RFX") + GetEffectiveStat("DA")) / 3f;

            // 5) Mentor: (TA >= 70, DIS >= 65, CMP >= 65)
            if (GetEffectiveStat("TA") >= 70 && GetEffectiveStat("DIS") >= 65 && GetEffectiveStat("CMP") >= 65)
                roleScores["Mentor"] = (GetEffectiveStat("TA") + GetEffectiveStat("DIS") + GetEffectiveStat("CMP")) / 3f;

            // 6) Anti-Personnel: (GUN >= 65, AGG >= 70)
            if (GetEffectiveStat("GUN") >= 65 && GetEffectiveStat("AGG") >= 70 && GetEffectiveStat("DIS") >= 45)
                roleScores["Strafing Specialist"] = (GetEffectiveStat("GUN") + GetEffectiveStat("AGG")) / 2f;

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

            int currentIndex = System.Array.IndexOf(ranks, CurrentRank);
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
                case "ace": return GetEffectiveStat("OA") >= 70 && GetEffectiveStat("GUN") >= 70 && GetEffectiveStat("ENG") >= 70;
                case "wingman": return GetEffectiveStat("TA") >= 70 && GetEffectiveStat("WI") >= 70 && GetEffectiveStat("DA") >= 70;
                case "steady": return GetEffectiveStat("CMP") >= 70 && GetEffectiveStat("DA") >= 70;
                case "survivor": return GetEffectiveStat("DA") >= 70 && GetEffectiveStat("ADP") >= 70 && GetEffectiveStat("CMP") >= 70;
                case "overwatch": return GetEffectiveStat("DA") >= 70 && GetEffectiveStat("TA") >= 70 && GetEffectiveStat("CMP") >= 70;
                case "pack hunter": return GetEffectiveStat("OA") >= 70 && GetEffectiveStat("TA") >= 70 && GetEffectiveStat("DIS") >= 70;
                case "flight leader": return GetEffectiveStat("LDR") >= 70 && GetEffectiveStat("TA") >= 70;
                case "instructor": return GetEffectiveStat("LDR") >= 70 && GetEffectiveStat("DIS") >= 70 && GetEffectiveStat("LRN") >= 70;
                case "sturdy": return GetEffectiveStat("STA") >= 75 && GetEffectiveStat("CMP") >= 60; // Resistant to airframe stress
                default: return false;
            }
        }
    }
}
