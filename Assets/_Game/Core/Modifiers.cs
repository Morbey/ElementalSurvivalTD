using System.Collections.Generic;

namespace ElementalTD.Core
{
    /// <summary>
    /// The run's modifiers in the order added (ADR 0007). An effective value is the base, then every addition on that stat,
    /// then every multiplication, each in the order added and rounded to float32 after each. Clamps would be the third stage;
    /// none holds a bound yet. Nothing removes a modifier.
    /// </summary>
    public sealed class Modifiers
    {
        private readonly List<Modifier> _modifiers = new List<Modifier>();

        /// <summary>Modifiers added since the run began; a view polls it.</summary>
        public int Count => _modifiers.Count;

        public Modifier At(int index) => _modifiers[index];

        public void Add(in Modifier modifier) => _modifiers.Add(modifier);

        /// <summary>The effective value of a stat for a tower of that element; for a stat of no tower, pass no element.</summary>
        public float Apply(Stat stat, Element element, float value)
        {
            for (int i = 0; i < _modifiers.Count; i++)
            {
                Modifier m = _modifiers[i];
                if (m.Operation == Operation.Add && Touches(m, stat, element)) value = (float)(value + m.Value);
            }
            for (int i = 0; i < _modifiers.Count; i++)
            {
                Modifier m = _modifiers[i];
                if (m.Operation == Operation.Multiply && Touches(m, stat, element)) value = (float)(value * m.Value);
            }
            return value;
        }

        /// <summary>A whole stat: the value plus its additions, which the modifier guarantees are whole.</summary>
        public int ApplyWhole(Stat stat, int value)
        {
            for (int i = 0; i < _modifiers.Count; i++)
            {
                Modifier m = _modifiers[i];
                if (m.Stat == stat && m.Operation == Operation.Add) value += (int)m.Value;
            }
            return value;
        }

        /// <summary>Damage, shots per second and range made effective for the stats' element; the rest as given. Changes nothing.</summary>
        public TowerStats Apply(in TowerStats stats) =>
            new TowerStats(
                Apply(Stat.TowerRange, stats.Element, stats.Range),
                Apply(Stat.TowerDamage, stats.Element, stats.Damage),
                Apply(Stat.TowerShotsPerSecond, stats.Element, stats.ShotsPerSecond),
                stats.ProjectileSpeed,
                stats.Cost,
                stats.Element);

        private static bool Touches(in Modifier modifier, Stat stat, Element element) =>
            modifier.Stat == stat && (modifier.Element.IsNone || modifier.Element == element);
    }
}
