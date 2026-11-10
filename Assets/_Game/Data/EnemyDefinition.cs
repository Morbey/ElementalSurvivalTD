using ElementalTD.Core;
using UnityEngine;

namespace ElementalTD.Data
{
    [CreateAssetMenu(fileName = "Enemy_", menuName = "ElementalTD/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [SerializeField, Min(0f)] private float speedCellsPerSecond = 1f;
        [SerializeField, Min(1f)] private float maxHealth = 1f;

        public float SpeedCellsPerSecond => speedCellsPerSecond;

        public EnemyStats ToStats() => new EnemyStats(speedCellsPerSecond, maxHealth);
    }
}
