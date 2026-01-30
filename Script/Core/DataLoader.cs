using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AceManager.Core
{
    public static class DataLoader
    {
        private const string AircraftDataPath = "res://Data/Aircraft/";
        private const string AirbaseDataPath = "res://Data/Airbases/";

        public static List<AircraftData> LoadAllAircraft()
        {
            var allAircraft = new List<AircraftData>();
            string[] fleetFiles = { "BritishFleet.txt", "FrenchFleet.txt", "GermanFleet.txt", "USA&ItalyFleet.txt" };

            foreach (var fileName in fleetFiles)
            {
                string fullPath = AircraftDataPath + fileName;
                allAircraft.AddRange(ParseAircraftCsv(fullPath));
            }

            return allAircraft;
        }

        private static List<AircraftData> ParseAircraftCsv(string path)
        {
            var list = new List<AircraftData>();
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr($"Could not open file: {path}");
                return list;
            }

            string headerLine = file.GetLine();
            string[] headers = headerLine.Split(',');

            while (!file.EofReached())
            {
                string line = file.GetLine().Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                string[] values = ParseCsvLine(line);
                if (values.Length < headers.Length) continue;

                var data = new AircraftData();
                try
                {
                    data.AircraftId = values[0];
                    data.Name = values[1];
                    data.Nation = values[2];
                    data.Manufacturer = values[3];
                    data.YearIntroduced = int.Parse(values[4]);
                    data.RolePrimary = values[5];
                    data.Variant = values[6];

                    data.SpeedRange = int.Parse(values[7]);
                    data.ClimbRange = int.Parse(values[8]);
                    data.TurnRange = int.Parse(values[9]);
                    data.StabilityRange = int.Parse(values[10]);
                    data.DiveSafetyRange = int.Parse(values[11]);
                    data.CeilingRange = int.Parse(values[12]);
                    data.DistanceRange = int.Parse(values[13]);

                    data.FighterRole = int.Parse(values[14]);
                    data.BomberRole = int.Parse(values[15]);
                    data.ReconRole = int.Parse(values[16]);

                    data.FirepowerRange = int.Parse(values[17]);
                    data.AccuracyRange = int.Parse(values[18]);
                    data.AmmoRange = int.Parse(values[19]);
                    data.WeaponType = values[20];
                    data.FiringArc = values[21];

                    data.AirframeStrengthRange = int.Parse(values[22]);
                    data.EngineDurabilityRange = int.Parse(values[23]);
                    data.PilotProtectionRange = int.Parse(values[24]);
                    data.FuelVulnerabilityRange = int.Parse(values[25]);

                    data.ReliabilityRange = int.Parse(values[26]);
                    data.MaintenanceCostRange = int.Parse(values[27]);
                    data.RepairTimeRange = int.Parse(values[28]);
                    data.SparePartsAvailabilityRange = int.Parse(values[29]);

                    data.TrainingDifficultyRange = int.Parse(values[30]);
                    data.SkillCeilingRange = int.Parse(values[31]);
                    data.AceSynergyRange = int.Parse(values[32]);

                    data.HangarSize = values[33];
                    data.RunwayRequirementRange = int.Parse(values[34]);
                    data.FuelConsumptionRange = int.Parse(values[35]);
                    data.SupplyStrainRange = int.Parse(values[36]);

                    data.BaseReputationRequired = int.Parse(values[37]);
                    data.CommandPriorityTier = int.Parse(values[38]);
                    data.ProductionScarcityRange = int.Parse(values[39]);

                    if (values.Length > 40)
                    {
                        data.CrewSeats = int.Parse(values[40]);
                    }
                    if (values.Length > 41)
                    {
                        data.FirepowerRear = int.Parse(values[41]);
                    }
                    if (values.Length > 42)
                    {
                        data.DoctrineTags = values[42].Trim('"').Split(',').Select(t => t.Trim()).ToArray();
                    }

                    list.Add(data);
                }
                catch (Exception e)
                {
                    GD.PrintErr($"Error parsing line in {path}: {line}. Error: {e.Message}");
                }
            }

            return list;
        }

        private static string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            string current = "";

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current);
                    current = "";
                }
                else
                {
                    current += c;
                }
            }
            result.Add(current);
            return result.ToArray();
        }

        public static List<AirbaseData> LoadAirbaseDatabase()
        {
            var bases = new List<AirbaseData>();
            string path = AirbaseDataPath + "AirbaseDatabase.txt";

            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null) return bases;

            AirbaseData currentBase = null;

            while (!file.EofReached())
            {
                string line = file.GetLine().Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("=") || line.StartsWith("Coordinate")) continue;

                if (char.IsDigit(line[0]) && line.Contains(")"))
                {
                    if (currentBase != null) bases.Add(currentBase);
                    currentBase = new AirbaseData();
                    currentBase.Name = line.Substring(line.IndexOf(")") + 1).Trim();
                }
                else if (currentBase != null)
                {
                    if (line.StartsWith("Nation:")) currentBase.Nation = line.Replace("Nation:", "").Trim();
                    else if (line.StartsWith("Location:")) currentBase.Location = line.Replace("Location:", "").Trim();
                    else if (line.StartsWith("Coordinates:"))
                    {
                        var coords = line.Replace("Coordinates:", "").Trim().Split(',');
                        if (coords.Length == 2)
                        {
                            float lat = float.Parse(coords[0].Replace("N", "").Replace("S", "").Trim());
                            float lon = float.Parse(coords[1].Replace("E", "").Replace("W", "").Trim());
                            currentBase.Coordinates = new Vector2(lon, lat); // X=Lon, Y=Lat
                        }
                    }
                    else if (line.StartsWith("Active:")) currentBase.ActiveYears = line.Replace("Active:", "").Trim();
                    else if (line.StartsWith("Notes:")) currentBase.Notes = line.Replace("Notes:", "").Trim();
                }
            }

            if (currentBase != null) bases.Add(currentBase);
            return bases;
        }
    }
}
