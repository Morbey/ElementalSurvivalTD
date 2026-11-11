using System;
using ElementalTD.Core;
using UnityEngine;

namespace ElementalTD.Data
{
    /// <summary>A whole run: the lives, the gold and the waves in order, each with its groups, edited and balanced as one asset (ADR 0004).</summary>
    [CreateAssetMenu(fileName = "Run_", menuName = "ElementalTD/Run Definition")]
    public sealed class RunDefinition : ScriptableObject
    {
        [Serializable]
        private sealed class GroupEntry
        {
            [SerializeField] private EnemyDefinition enemy;
            [SerializeField, Min(1)] private int count = 1;
            [Tooltip("Must be above 0.")]
            [SerializeField, Min(0f)] private float spawnIntervalSeconds = 1f;

            public WaveGroup ToGroup(string runName, int waveNumber, int groupNumber)
            {
                if (enemy == null) throw new InvalidOperationException($"{runName}: wave {waveNumber}, group {groupNumber} has no enemy.");
                return new WaveGroup(enemy.ToStats(), count, spawnIntervalSeconds);
            }
        }

        [Serializable]
        private sealed class WaveEntry
        {
            [Tooltip("Wave 1: from the start of the run. Others: from the entry of the previous wave's last enemy.")]
            [SerializeField, Min(0f)] private float delayBeforeSeconds;
            [Tooltip("Gold paid once the wave's last enemy has left the board, to a player still alive.")]
            [SerializeField, Min(0)] private int survivalBonus;
            [Tooltip("In spawn order.")]
            [SerializeField] private GroupEntry[] groups = new GroupEntry[0];

            public Wave ToWave(string runName, int number)
            {
                if (groups == null || groups.Length == 0) throw new InvalidOperationException($"{runName}: wave {number} has no group.");

                var result = new WaveGroup[groups.Length];
                for (int i = 0; i < groups.Length; i++)
                    result[i] = groups[i].ToGroup(runName, number, i + 1);
                return new Wave(result, delayBeforeSeconds, survivalBonus);
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
