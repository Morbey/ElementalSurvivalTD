using UnityEngine;

namespace ElementalTD.Data
{
    [CreateAssetMenu(fileName = "Enemy_", menuName = "ElementalTD/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [SerializeField, Min(0f)] private float speedCellsPerSecond = 1f;

        public float SpeedCellsPerSecond => speedCellsPerSecond;
    }
}
