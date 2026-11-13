using System;

namespace ElementalTD.Core
{
    /// <summary>
    /// One change to a stat (ADR 0007): the stat, an element or none, the operation, the amount and who added it.
    /// With an element it applies to the towers of that element only; with none, to every tower.
    /// </summary>
    public readonly struct Modifier
    {
        public readonly Stat Stat;
        public readonly Element Element;
        public readonly Operation Operation;
        public readonly float Value;
        /// <summary>Who added it: a perk's place in the pool; -1 for none.</summary>
        public readonly int Source;

        /// <summary>
        /// Refuses an element on a stat that is not a tower's, and on a whole stat a multiplication or a fractional addition,
        /// which could round differently under Mono and IL2CPP (ADR 0007, decision 3).
        /// </summary>
        public Modifier(Stat stat, Operation operation, float value, Element element = default, int source = -1)
        {
            if (!element.IsNone && !IsTowerStat(stat))
                throw new ArgumentException($"Only a tower stat takes an element, not {stat}.", nameof(element));
            if (IsWholeStat(stat))
            {
                if (operation != Operation.Add)
                    throw new ArgumentException($"{stat} is whole: it takes additions only.", nameof(operation));
                if (value != MathF.Floor(value))
                    throw new ArgumentOutOfRangeException(nameof(value), value, $"{stat} is whole: it takes whole additions only.");
            }

            Stat = stat;
            Element = element;
            Operation = operation;
            Value = value;
            Source = source;
        }

        public static bool IsTowerStat(Stat stat) =>
            stat == Stat.TowerDamage || stat == Stat.TowerShotsPerSecond || stat == Stat.TowerRange;

        private static bool IsWholeStat(Stat stat) => stat == Stat.GoldPerKill;

        /// <summary>This modifier applied that many times to 0 (add) or 1 (multiply), rounded to float32 after each, as rule 17.</summary>
        public float Stacked(int times)
        {
            if (times < 0) throw new ArgumentOutOfRangeException(nameof(times), times, "A modifier cannot be applied fewer than 0 times.");

            float total = Operation == Operation.Add ? 0f : 1f;
            for (int i = 0; i < times; i++)
                total = Operation == Operation.Add ? (float)(total + Value) : (float)(total * Value);
            return total;
        }
    }
}
