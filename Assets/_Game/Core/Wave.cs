using System;

namespace ElementalTD.Core
{
    /// <summary>
    /// The unit of the wave count, the countdown and the survival bonus: a delay, a bonus and its groups in spawn order (ADR 0004).
    /// </summary>
    public readonly struct Wave
    {
        private readonly WaveGroup[] _groups;

        /// <summary>Counted from the start of the run for the first wave, otherwise from the entry of the previous wave's last enemy.</summary>
        public readonly float DelayBeforeSeconds;
        /// <summary>Gold paid once the wave is survived.</summary>
        public readonly int SurvivalBonus;

        /// <summary>0 for the default wave.</summary>
        public int GroupCount => _groups == null ? 0 : _groups.Length;

        /// <summary>In spawn order.</summary>
        public WaveGroup GroupAt(int index)
        {
            if ((uint)index >= (uint)GroupCount) throw new ArgumentOutOfRangeException(nameof(index), index, "No group at this index.");
            return _groups[index];
        }

        public Wave(WaveGroup[] groups, float delayBeforeSeconds, int survivalBonus = 0)
        {
            if (groups == null) throw new ArgumentNullException(nameof(groups));
            if (groups.Length == 0) throw new ArgumentException("A wave needs at least one group.", nameof(groups));
            // A default group bypasses the checks of its constructor.
            for (int i = 0; i < groups.Length; i++)
                if (groups[i].Count < 1) throw new ArgumentException($"Group {i + 1} has no enemy.", nameof(groups));
            if (delayBeforeSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(delayBeforeSeconds), delayBeforeSeconds, "The delay cannot be negative.");
            if (survivalBonus < 0) throw new ArgumentOutOfRangeException(nameof(survivalBonus), survivalBonus, "The survival bonus cannot be negative.");

            // A copy, so changing the caller's array cannot change the wave.
            _groups = (WaveGroup[])groups.Clone();
            DelayBeforeSeconds = delayBeforeSeconds;
            SurvivalBonus = survivalBonus;
        }

        /// <summary>A wave of one group.</summary>
        public Wave(in EnemyStats enemy, int count, float spawnIntervalSeconds, float delayBeforeSeconds, int survivalBonus = 0)
            : this(new[] { new WaveGroup(enemy, count, spawnIntervalSeconds) }, delayBeforeSeconds, survivalBonus)
        {
        }
    }
}
