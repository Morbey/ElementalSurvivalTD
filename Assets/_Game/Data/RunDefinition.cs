using System;
using ElementalTD.Core;
using UnityEngine;

namespace ElementalTD.Data
{
    /// <summary>A whole run: the lives, the gold and the waves in order, edited and balanced as one asset (ADR 0004).</summary>
    [CreateAssetMenu(fileName = "Run_", menuName = "ElementalTD/Run Definition")]
    public sealed class RunDefinition : ScriptableObject
    {
        [Serializable]
        private sealed class WaveEntry
        {
            [SerializeField] private EnemyDefinition enemy;
            [SerializeField, Min(1)] private int count = 1;
            [Tooltip("Must be above 0.")]
            [SerializeField, Min(0f)] private float spawnIntervalSeconds = 1f;
            [Tooltip("Wave 1: from the start of the run. Others: from the entry of the previous wave's last enemy.")]
            [SerializeField, Min(0f)] private float delayBeforeSeconds;

            public Wave ToWave(string runName, int number)
            {
                if (enemy == null) throw new InvalidOperationException($"{runName}: wave {number} has no enemy.");
                return new Wave(enemy.ToStats(), count, spawnIntervalSeconds, delayBeforeSeconds);
            }
        }

        [SerializeField, Min(1)] private int lives = 1;
        [SerializeField, Min(0)] private int startingGold;
        [Tooltip("Percent of its cost a tower refunds once it has fired; rounded down.")]
        [SerializeField, Range(0, 100)] private int sellRefundPercent = 100;
        [SerializeField] private WaveEntry[] waves = new WaveEntry[0];

        public int Lives => lives;
        public int StartingGold => startingGold;
        public int SellRefundPercent => sellRefundPercent;

        public Wave[] ToWaves()
        {
            if (waves == null || waves.Length == 0) throw new InvalidOperationException($"{name}: the run has no wave.");

            var result = new Wave[waves.Length];
            for (int i = 0; i < waves.Length; i++)
                result[i] = waves[i].ToWave(name, i + 1);
            return result;
        }
    }
}
