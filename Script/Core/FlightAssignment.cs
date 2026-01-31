using Godot;
using System;

namespace AceManager.Core
{
    public enum CrewSeat
    {
        Pilot,
        Gunner,
        Observer
    }

    public partial class FlightAssignment : Resource
    {
        public AircraftInstance Aircraft { get; set; }
        public CrewData Pilot { get; set; }
        public CrewData Gunner { get; set; } // Optional - for two-seaters
        public CrewData Observer { get; set; } // Optional - for recon two-seaters

        public FlightAssignment() { }

        public FlightAssignment(AircraftInstance aircraft, CrewData pilot)
        {
            Aircraft = aircraft;
            Pilot = pilot;
        }

        public bool IsValid()
        {
            // Must have aircraft and pilot at minimum
            if (Aircraft == null || Pilot == null)
                return false;

            // Two-seater aircraft should ideally have a second crew member
            // but it's not strictly required (can fly solo with penalty)
            return true;
        }

        public bool IsTwoSeater()
        {
            return Aircraft?.GetCrewSeats() >= 2;
        }

        public bool HasGunner()
        {
            return Gunner != null;
        }

        public bool HasObserver()
        {
            return Observer != null;
        }

        public int GetCrewCount()
        {
            var crew = new System.Collections.Generic.HashSet<CrewData>();
            if (Pilot != null) crew.Add(Pilot);
            if (Gunner != null) crew.Add(Gunner);
            if (Observer != null) crew.Add(Observer);
            return crew.Count;
        }

        public string GetDisplayName()
        {
            string display = $"{Aircraft?.GetDisplayName() ?? "No Aircraft"}";
            display += $" - {Pilot?.Name ?? "No Pilot"}";

            if (Gunner != null)
                display += $" / {Gunner.Name} (Gunner)";
            if (Observer != null)
                display += $" / {Observer.Name} (Observer)";

            return display;
        }

        // Calculate combined crew effectiveness for this flight
        public float GetCombinedDogfightRating()
        {
            float rating = Pilot?.GetDogfightRating() ?? 0;

            // Gunner contributes to defensive capability
            if (Gunner != null)
            {
                rating += Gunner.GUN * 0.3f; // Gunner's shooting helps
                rating += Gunner.DA * 0.2f;  // Defensive awareness
            }

            return rating;
        }

        public float GetCombinedReconRating()
        {
            float rating = Pilot?.GetReconSurvivalRating() ?? 0;

            // Observer significantly boosts recon
            if (Observer != null)
            {
                rating += Observer.OA * 0.5f;  // Offensive awareness for spotting
                rating += Observer.TA * 0.3f;  // Team awareness for reporting
            }
            else if (Gunner != null)
            {
                // Gunner can observe but not as effectively
                rating += Gunner.OA * 0.2f;
            }

            return rating;
        }

        public float GetDefensiveRating()
        {
            float rating = Pilot?.DA ?? 0;

            // Rear gunner is crucial for two-seater defense
            if (Gunner != null)
            {
                rating += Gunner.GUN * 0.5f;
                rating += Gunner.RFX * 0.3f;
                rating += Gunner.DA * 0.2f;
            }

            return rating;
        }

        public float GetBombingRating()
        {
            float rating = Pilot?.GetGroundAttackRating() ?? 0;

            // Observer/Gunner helps with bomb aiming and defense
            if (Observer != null)
            {
                rating += Observer.OA * 0.3f;
                rating += Observer.DIS * 0.2f; // Discipline for accurate drops
            }
            else if (Gunner != null)
            {
                rating += Gunner.DIS * 0.1f;
            }

            return rating;
        }
    }
}
