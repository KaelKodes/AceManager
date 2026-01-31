using Godot;
using System;
using System.Collections.Generic;

namespace AceManager.Core
{
    public partial class PilotTrait : Resource
    {
        [Export] public string TraitId { get; set; }
        [Export] public string TraitName { get; set; }
        [Export] public string Description { get; set; }
        [Export] public bool IsPositive { get; set; }

        // Stat modifiers (Stat Name -> Adder)
        // e.g., "GUN" -> 10, "CTL" -> -5
        [Export] public Godot.Collections.Dictionary<string, int> StatModifiers { get; set; } = new Godot.Collections.Dictionary<string, int>();

        public PilotTrait() { }

        public PilotTrait(string id, string name, string desc, bool positive)
        {
            TraitId = id;
            TraitName = name;
            Description = desc;
            IsPositive = positive;
        }

        public static PilotTrait Create(string id, string name, string desc, bool positive, params (string stat, int val)[] mods)
        {
            var trait = new PilotTrait(id, name, desc, positive);
            foreach (var (stat, val) in mods)
            {
                trait.StatModifiers[stat] = val;
            }
            return trait;
        }
    }
}
