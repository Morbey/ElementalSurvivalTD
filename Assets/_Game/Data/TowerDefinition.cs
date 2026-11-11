using ElementalTD.Core;
using UnityEngine;

namespace ElementalTD.Data
{
    [CreateAssetMenu(fileName = "Tower_", menuName = "ElementalTD/Tower Definition")]
    public sealed class TowerDefinition : ScriptableObject
    {
        [Tooltip("Cells from the tower's cell centre to the enemy, inclusive.")]
        [SerializeField, Min(0f)] private float rangeCells;
        [SerializeField, Min(0f)] private float damage;
        [Tooltip("Must be above 0.")]
        [SerializeField, Min(0f)] private float shotsPerSecond;
        [Tooltip("Must be above 0.")]
        [SerializeField, Min(0f)] private float projectileSpeedCellsPerSecond;
        [Tooltip("Gold to buy it.")]
        [SerializeField, Min(0)] private int cost;
        [Tooltip("While unassigned, no element: it deals x1.")]
        [SerializeField] private ElementDefinition element;

        public float RangeCells => rangeCells;
        public ElementDefinition Element => element;

        public TowerStats ToStats() =>
            new TowerStats(rangeCells, damage, shotsPerSecond, projectileSpeedCellsPerSecond, cost, element != null ? element.ToElement() : default);
    }
}
