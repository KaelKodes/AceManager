using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AceManager.Core
{
    public partial class RosterManager : Node
    {
        private static readonly string[] FirstNames = {
            "James", "William", "George", "Charles", "Henry", "Edward", "Albert", "Frederick",
            "Arthur", "Thomas", "John", "Richard", "Robert", "Frank", "Harold", "Walter",
            "Ernest", "Alfred", "Percy", "Herbert", "Stanley", "Leonard", "Cecil", "Reginald"
        };

        private static readonly string[] LastNames = {
            "Smith", "Jones", "Brown", "Wilson", "Taylor", "Davies", "Evans", "Thomas",
            "Roberts", "Williams", "Thompson", "Walker", "Wright", "Robinson", "Hall", "Clarke",
            "Green", "Lewis", "Harris", "Martin", "Jackson", "Wood", "Turner", "Edwards"
        };

        public List<CrewData> Roster { get; private set; } = new List<CrewData>();

        private Random _rng = new Random();

        public void GenerateRoster(int count)
        {
            Roster.Clear();
            for (int i = 0; i < count; i++)
            {
                Roster.Add(GenerateRandomPilot());
            }
            GD.Print($"Generated roster of {count} pilots.");
        }

        public CrewData GenerateRandomPilot()
        {
            var pilot = new CrewData();
            pilot.Name = $"{FirstNames[_rng.Next(FirstNames.Length)]} {LastNames[_rng.Next(LastNames.Length)]}";
            pilot.Role = "Pilot";

            // Generate stats with some variance (30-70 for average pilots, with outliers)
            pilot.CTL = GenerateStat();
            pilot.GUN = GenerateStat();
            pilot.ENG = GenerateStat();
            pilot.RFX = GenerateStat();
            pilot.OA = GenerateStat();
            pilot.DA = GenerateStat();
            pilot.TA = GenerateStat();
            pilot.WI = GenerateStat();
            pilot.AGG = GenerateStat();
            pilot.DIS = GenerateStat();
            pilot.CMP = GenerateStat();
            pilot.ADP = GenerateStat();
            pilot.LDR = GenerateStat();
            pilot.LRN = GenerateStat();
            pilot.STA = GenerateStat();

            // Additional tracking fields (to be added to CrewData if needed)
            // pilot.Fatigue = 0;
            // pilot.WoundedDays = 0;

            return pilot;
        }

        private int GenerateStat()
        {
            // Bell curve centered around 50, range 20-80 for most, with rare outliers
            int sum = 0;
            for (int i = 0; i < 3; i++)
            {
                sum += _rng.Next(20, 80);
            }
            return Math.Clamp(sum / 3, 0, 100);
        }

        public List<CrewData> GetAvailablePilots()
        {
            // For now, return all pilots. Later, filter by fatigue/wounds.
            return Roster.ToList();
        }

        public CrewData GetPilotByName(string name)
        {
            return Roster.FirstOrDefault(p => p.Name == name);
        }

        public void AddFatigue(CrewData pilot, int amount)
        {
            // Placeholder for fatigue system - will need to extend CrewData
            GD.Print($"{pilot.Name} gained {amount} fatigue.");
        }

        public void WoundPilot(CrewData pilot, int recoveryDays)
        {
            GD.Print($"{pilot.Name} was wounded! Recovery: {recoveryDays} days.");
        }

        public void KillPilot(CrewData pilot)
        {
            Roster.Remove(pilot);
            GD.Print($"{pilot.Name} was killed in action.");
        }
    }
}
