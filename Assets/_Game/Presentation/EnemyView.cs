using UnityEngine;

namespace ElementalTD.Presentation
{
    /// <summary>Draws one enemy: its position and a health bar whose fill pivot is scaled on x.</summary>
    public sealed class EnemyView : MonoBehaviour
    {
        [Tooltip("Pivot at the bar's left end; its x scale is the health fraction.")]
        [SerializeField] private Transform healthFill;

        private Transform _transform;
        private float _shownFraction = -1f; // forces the first update

        private void Awake()
        {
            _transform = transform;
            if (healthFill == null) Debug.LogError("EnemyView: assign the health fill.", this);
        }

        public void Show(Vector3 position, float healthFraction)
        {
            _transform.position = position;

            if (healthFraction == _shownFraction || healthFill == null) return;
            _shownFraction = healthFraction;
            Vector3 scale = healthFill.localScale;
            scale.x = healthFraction;
            healthFill.localScale = scale;
        }
    }
}
