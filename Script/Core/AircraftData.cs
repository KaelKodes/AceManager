using Godot;
using System;

namespace AceManager.Core
{
    public partial class AircraftData : Resource
    {
        [Export] public string AircraftId { get; set; }
        [Export] public string Name { get; set; }
        [Export] public string Nation { get; set; }
        [Export] public string Manufacturer { get; set; }
        [Export] public int YearIntroduced { get; set; }
        [Export] public string RolePrimary { get; set; }
        [Export] public string Variant { get; set; }

        // Flight Performance (1-10)
        [Export] public int SpeedRange { get; set; }
        [Export] public int ClimbRange { get; set; }
        [Export] public int TurnRange { get; set; }
        [Export] public int StabilityRange { get; set; }
        [Export] public int DiveSafetyRange { get; set; }
        [Export] public int CeilingRange { get; set; }
        [Export] public int DistanceRange { get; set; }

        // Role Skill Ratings (0-10)
        [Export] public int FighterRole { get; set; }
        [Export] public int BomberRole { get; set; }
        [Export] public int ReconRole { get; set; }

        // Combat Capability (1-10 + enums)
        [Export] public int FirepowerRange { get; set; }
        [Export] public int AccuracyRange { get; set; }
        [Export] public int AmmoRange { get; set; }
        [Export] public string WeaponType { get; set; }
        [Export] public string FiringArc { get; set; }
        [Export] public int CrewSeats { get; set; } = 1; // 1 = single-seater, 2 = two-seater
        [Export] public int FirepowerRear { get; set; } = 0; // Rear gunner firepower for two-seaters

        // Survivability (1-10)
        [Export] public int AirframeStrengthRange { get; set; }
        [Export] public int EngineDurabilityRange { get; set; }
        [Export] public int PilotProtectionRange { get; set; }
        [Export] public int FuelVulnerabilityRange { get; set; }

        // Maintenance & Reliability (1-10)
        [Export] public int ReliabilityRange { get; set; }
        [Export] public int MaintenanceCostRange { get; set; }
        [Export] public int RepairTimeRange { get; set; }
        [Export] public int SparePartsAvailabilityRange { get; set; }

        // Training & Pilot Interaction (1-10)
        [Export] public int TrainingDifficultyRange { get; set; }
        [Export] public int SkillCeilingRange { get; set; }
        [Export] public int AceSynergyRange { get; set; }

        // Logistics
        [Export] public string HangarSize { get; set; }
        [Export] public int RunwayRequirementRange { get; set; }
        [Export] public int FuelConsumptionRange { get; set; }
        [Export] public int SupplyStrainRange { get; set; }

        // Strategic Meta
        [Export] public int BaseReputationRequired { get; set; }
        [Export] public int CommandPriorityTier { get; set; }
        [Export] public int ProductionScarcityRange { get; set; }
        [Export] public string[] DoctrineTags { get; set; }

        // Calculation methods (Derived values)
        public float GetFighterEffectiveness()
        {
            return FighterRole 
                + (SpeedRange * 0.6f) 
                + (ClimbRange * 0.6f) 
                + (TurnRange * 0.6f) 
                + (FirepowerRange * 0.7f) 
                + (AccuracyRange * 0.5f) 
                + (StabilityRange * 0.3f) 
                + (DiveSafetyRange * 0.3f);
        }

        public float GetBomberEffectiveness()
        {
            return BomberRole 
                + (DistanceRange * 0.7f) 
                + (StabilityRange * 0.6f) 
                + (ReliabilityRange * 0.6f) 
                + (AmmoRange * 0.3f) 
                + (AirframeStrengthRange * 0.3f);
        }

        public float GetReconEffectiveness()
        {
            return ReconRole 
                + (DistanceRange * 0.7f) 
                + (CeilingRange * 0.6f) 
                + (SpeedRange * 0.4f) 
                + (StabilityRange * 0.6f) 
                + (ReliabilityRange * 0.5f);
        }

        public float GetDurabilityScore()
        {
            return (AirframeStrengthRange * 0.6f) 
                + (EngineDurabilityRange * 0.5f) 
                + (PilotProtectionRange * 0.4f) 
                + (ReliabilityRange * 0.2f) 
                - (FuelVulnerabilityRange * 0.6f);
        }

        public float GetOpsBurdenScore()
        {
            return (MaintenanceCostRange * 0.7f) 
                + (RepairTimeRange * 0.6f) 
                + (FuelConsumptionRange * 0.5f) 
                + (SupplyStrainRange * 0.8f) 
                - (SparePartsAvailabilityRange * 0.7f);
        }
    }
}
