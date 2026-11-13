using ElementalTD.Core;
using UnityEngine;

namespace ElementalTD.Data
{
    [CreateAssetMenu(fileName = "Enemy_", menuName = "ElementalTD/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [SerializeField, Min(0f)] private float speedCellsPerSecond = 1f;
        [SerializeField, Min(1f)] private float maxHealth = 1f;
        [Tooltip("Gold paid when it is killed.")]
        [SerializeField, Min(0)] private int goldReward;
        [Tooltip("XP added when it is killed.")]
        [SerializeField, Min(0)] private int xpReward;
        [Tooltip("While unassigned, no element: it takes x1.")]
        [SerializeField] private ElementDefinition element;

        public float SpeedCellsPerSecond => speedCellsPerSecond;

        public EnemyStats ToStats() =>
            new EnemyStats(speedCellsPerSecond, maxHealth, goldReward, element != null ? element.ToElement() : default, xpReward);
    }
}
