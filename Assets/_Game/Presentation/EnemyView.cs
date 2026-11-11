using UnityEngine;

namespace ElementalTD.Presentation
{
    /// <summary>Draws one enemy: its position, its body's material and a health bar whose fill pivot is scaled on x.</summary>
    public sealed class EnemyView : MonoBehaviour
    {
        [SerializeField] private Renderer body;
        [Tooltip("Pivot at the bar's left end; its x scale is the health fraction.")]
        [SerializeField] private Transform healthFill;

        private Transform _transform;
        private float _shownFraction = -1f; // forces the first update
        private Material _shownMaterial;

        /// <summary>The body's own material on the prefab: what an enemy with no look is drawn with.</summary>
        public Material BodyMaterial => body != null ? body.sharedMaterial : null;

        private void Awake()
        {
            _transform = transform;
            if (body == null || healthFill == null) Debug.LogError("EnemyView: assign the body and the health fill.", this);
        }

        public void Show(Vector3 position, float healthFraction, Material material)
        {
            _transform.position = position;

            // By reference: Unity's == on objects costs a native check per frame.
            if (!ReferenceEquals(material, _shownMaterial) && body != null)
            {
                _shownMaterial = material;
                body.sharedMaterial = material;
            }

            if (healthFraction == _shownFraction || healthFill == null) return;
            _shownFraction = healthFraction;
            Vector3 scale = healthFill.localScale;
            scale.x = healthFraction;
            healthFill.localScale = scale;
        }
    }
}
