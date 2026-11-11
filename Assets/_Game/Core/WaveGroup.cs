using System;

namespace ElementalTD.Core
{
    /// <summary>Enemies of one type within a wave, entering one per spawn interval.</summary>
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
