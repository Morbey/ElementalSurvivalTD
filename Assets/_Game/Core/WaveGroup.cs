using System;

namespace ElementalTD.Core
{
    /// <summary>
    /// Enemies of one type within a wave. The spawn interval is the time before each of them, the first included,
    /// except a wave's first enemy, which enters when the wave's delay ends (ADR 0004).
    /// </summary>
    public readonly struct WaveGroup
    {
        public readonly EnemyStats Enemy;
        public readonly int Count;
        public readonly float SpawnIntervalSeconds;

        public WaveGroup(in EnemyStats enemy, int count, float spawnIntervalSeconds)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, "A group needs at least one enemy.");
            if (spawnIntervalSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(spawnIntervalSeconds), spawnIntervalSeconds, "The spawn interval must be above 0.");
            Enemy = enemy;
            Count = count;
            SpawnIntervalSeconds = spawnIntervalSeconds;
        }
    }
}
