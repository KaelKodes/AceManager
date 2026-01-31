using Godot;
using System;

namespace AceManager.Core
{
    public partial class UpgradeProject : Resource
    {
        [Export] public string FacilityName { get; set; }
        [Export] public int TargetLevel { get; set; }
        [Export] public int DaysRemaining { get; set; }
        [Export] public int MeritCost { get; set; }

        public static UpgradeProject Create(string name, int targetLevel)
        {
            return new UpgradeProject
            {
                FacilityName = name,
                TargetLevel = targetLevel,
                DaysRemaining = targetLevel * 2, // 2 days for level 1, 4 for level 2, etc.
                MeritCost = CalculateCost(targetLevel)
            };
        }

        public static int CalculateCost(int targetLevel)
        {
            // Level 2: 50, Level 3: 150, Level 4: 300, Level 5: 500
            return targetLevel switch
            {
                2 => 50,
                3 => 150,
                4 => 300,
                5 => 500,
                _ => 25 * targetLevel
            };
        }
    }
}
