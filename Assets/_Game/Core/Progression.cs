using System;

namespace ElementalTD.Core
{
    /// <summary>
    /// The run's XP and level (ADR 0004). XP is a whole running total; the level starts at 1 and rises each time the XP
    /// gathered since it began reaches its item of the table, the rest carrying into the next level. Past the table's end
    /// no level is gained and XP still counts.
    /// </summary>
    public sealed class Progression
    {
        private readonly int[] _xpPerLevel;

        public int Xp { get; private set; }
        /// <summary>From 1.</summary>
        public int Level { get; private set; } = 1;
        public bool TableEnded => Level > _xpPerLevel.Length;
        /// <summary>Gathered since the level began.</summary>
        public int XpIntoLevel { get; private set; }
        /// <summary>The level's item of the table; 0 once the table has ended.</summary>
        public int XpForLevel => TableEnded ? 0 : _xpPerLevel[Level - 1];

        /// <summary>Item n is the XP from level n to n + 1. Copied; null or empty: no level is ever gained.</summary>
        public Progression(int[] xpPerLevel)
        {
            if (xpPerLevel == null)
            {
                _xpPerLevel = Array.Empty<int>();
                return;
            }

            for (int i = 0; i < xpPerLevel.Length; i++)
                if (xpPerLevel[i] < 1)
                    throw new ArgumentOutOfRangeException(nameof(xpPerLevel), xpPerLevel[i], $"Item {i + 1} of the level table must be at least 1.");
            _xpPerLevel = (int[])xpPerLevel.Clone();
        }

        /// <summary>Public for tests and simulations; in a run only the battle's kill adds XP.</summary>
        public void AddXp(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), amount, "XP cannot be taken away.");

            Xp += amount;
            XpIntoLevel += amount;
            while (!TableEnded && XpIntoLevel >= _xpPerLevel[Level - 1])
            {
                XpIntoLevel -= _xpPerLevel[Level - 1];
                Level++;
            }
        }
    }
}
