using System;

namespace ElementalTD.Core
{
    /// <summary>A group of one enemy type that enters one enemy per spawn interval, after a delay.</summary>
    public readonly struct Wave
    {
        public readonly EnemyStats Enemy;
        public readonly int Count;
        public readonly float SpawnIntervalSeconds;
        /// <summary>Counted from the start of the run for the first wave, otherwise from the entry of the previous wave's last enemy.</summary>
        public readonly float DelayBeforeSeconds;

        public Wave(in EnemyStats enemy, int count, float spawnIntervalSeconds, float delayBeforeSeconds)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, "A wave needs at least one enemy.");
            if (spawnIntervalSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(spawnIntervalSeconds), spawnIntervalSeconds, "The spawn interval must be above 0.");
            if (delayBeforeSeconds < 0f) throw new ArgumentOutOfRangeException(nameof(delayBeforeSeconds), delayBeforeSeconds, "The delay cannot be negative.");
            Enemy = enemy;
            Count = count;
            SpawnIntervalSeconds = spawnIntervalSeconds;
            DelayBeforeSeconds = delayBeforeSeconds;
        }
    }
}
