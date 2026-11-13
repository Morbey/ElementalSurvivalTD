using System;

namespace ElementalTD.Core
{
    /// <summary>
    /// The run's XP, level, perk pool and choices (ADR 0004, ADR 0007). XP is a whole running total; the level starts at 1
    /// and rises each time the XP gathered since it began reaches its item of the table, the rest carrying into the next
    /// level. Past the table's end no level is gained and XP still counts.
    /// Every level gained is owed a choice of perks, answered in rising order; a progression with no pool owes none.
    /// An offer is drawn with the progression's own generator when its choice opens (rules 20 to 23 of plan 0012).
    /// </summary>
    public sealed class Progression
    {
        public const int OfferSize = 3;

        private readonly int[] _xpPerLevel;
        private readonly Modifier[] _perks;
        private readonly int[] _timesTaken;
        private readonly int[] _order;
        private readonly int[] _offer = new int[OfferSize];
        private readonly Rng _rng;
        private int _levelsAnswered;

        public int Xp { get; private set; }
        /// <summary>From 1.</summary>
        public int Level { get; private set; } = 1;
        public bool TableEnded => Level > _xpPerLevel.Length;
        /// <summary>Gathered since the level began.</summary>
        public int XpIntoLevel { get; private set; }
        /// <summary>The level's item of the table; 0 once the table has ended.</summary>
        public int XpForLevel => TableEnded ? 0 : _xpPerLevel[Level - 1];

        public int PerkCount => _perks.Length;
        public bool ChoiceOpen { get; private set; }
        /// <summary>Choices opened since the run began; a view polls it.</summary>
        public int ChoicesOpened { get; private set; }
        /// <summary>The level the open choice answers; 0 with none open.</summary>
        public int ChoiceLevel => ChoiceOpen ? _levelsAnswered + 2 : 0;
        /// <summary>From 1, among the choices opened in a row; 0 with none open.</summary>
        public int ChoiceNumber { get; private set; }
        /// <summary>How many choices the open one's row holds; 0 with none open.</summary>
        public int ChoicesInARow { get; private set; }

        private int LevelsOwed => _perks.Length == 0 ? 0 : Level - 1 - _levelsAnswered;

        /// <summary>
        /// Item n of the table is the XP from level n to n + 1. Copied; null or empty: no level is ever gained.
        /// The perks are copied in order, each given its place as source; null or empty: no choice ever opens.
        /// A pool of one or two is refused, as an offer holds three distinct perks.
        /// </summary>
        public Progression(int[] xpPerLevel, Modifier[] perks = null, uint seed = 0)
        {
            if (xpPerLevel == null) _xpPerLevel = Array.Empty<int>();
            else
            {
                for (int i = 0; i < xpPerLevel.Length; i++)
                    if (xpPerLevel[i] < 1)
                        throw new ArgumentOutOfRangeException(nameof(xpPerLevel), xpPerLevel[i], $"Item {i + 1} of the level table must be at least 1.");
                _xpPerLevel = (int[])xpPerLevel.Clone();
            }

            int perkCount = perks == null ? 0 : perks.Length;
            if (perkCount > 0 && perkCount < OfferSize)
                throw new ArgumentException($"A pool needs at least {OfferSize} perks, or none; it has {perkCount}.", nameof(perks));
            _perks = new Modifier[perkCount];
            for (int i = 0; i < perkCount; i++)
                _perks[i] = new Modifier(perks[i].Stat, perks[i].Operation, perks[i].Value, perks[i].Element, i);
            _timesTaken = new int[perkCount];
            _order = new int[perkCount];
            _rng = new Rng(seed);
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

        /// <summary>The perk on that place of the pool, with its place as source.</summary>
        public Modifier PerkAt(int perk) => _perks[perk];

        public int TimesTaken(int perk) => _timesTaken[perk];

        /// <summary>The perk's place on that slot of the open offer, slots 0 to 2 left to right.</summary>
        public int OfferAt(int slot)
        {
            if (!ChoiceOpen) throw new InvalidOperationException("No choice is open.");
            if (slot < 0 || slot >= OfferSize) throw new ArgumentOutOfRangeException(nameof(slot), slot, $"A slot is 0 to {OfferSize - 1}.");
            return _offer[slot];
        }

        /// <summary>Opens a choice and draws its offer; nothing when none is owed or one is open. Called by the battle at the end of a tick.</summary>
        internal void OpenChoice()
        {
            if (ChoiceOpen || LevelsOwed == 0) return;

            ChoiceNumber = 1;
            ChoicesInARow = LevelsOwed;
            Open();
        }

        /// <summary>Answers the open choice with its slot and returns the perk taken; opens the next choice when one is owed.</summary>
        internal int Pick(int slot)
        {
            int perk = OfferAt(slot);
            _timesTaken[perk]++;
            _levelsAnswered++;
            ChoiceOpen = false;

            if (LevelsOwed == 0)
            {
                ChoiceNumber = 0;
                ChoicesInARow = 0;
                return perk;
            }

            ChoiceNumber++;
            // Recounted, in case a level was gained while the row was open.
            ChoicesInARow = ChoiceNumber - 1 + LevelsOwed;
            Open();
            return perk;
        }

        private void Open()
        {
            Draw();
            ChoiceOpen = true;
            ChoicesOpened++;
        }

        /// <summary>Rule 21: a partial shuffle of the pool's places, set afresh for every draw; three numbers taken.</summary>
        private void Draw()
        {
            int n = _order.Length;
            for (int i = 0; i < n; i++) _order[i] = i;
            for (int i = 0; i < OfferSize; i++)
            {
                int j = i + _rng.Below(n - i);
                int swap = _order[i];
                _order[i] = _order[j];
                _order[j] = swap;
                _offer[i] = _order[i];
            }
        }
    }
}
