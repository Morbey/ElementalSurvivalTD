using ElementalTD.Core;
using ElementalTD.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace ElementalTD.Presentation
{
    /// <summary>Shows one line of text when a placement is refused, for a fixed time in unscaled seconds.</summary>
    public sealed class PlacementMessageView : MonoBehaviour
    {
        private const string LabelName = "placement-message";

        [SerializeField] private UIDocument document;
        [SerializeField] private TowerPlacementInput placementInput;

        [Header("Texts")]
        [SerializeField] private string spawnOrExitText;
        [SerializeField] private string cellBlockedText;
        [SerializeField] private string enemyInTheWayText;
        [SerializeField] private string wouldBlockPathText;

        [Tooltip("Seconds of unscaled time the message stays on screen.")]
        [SerializeField, Min(0f)] private float duration = 2f;

        private Label _label;
        private bool _shown;
        private float _hideAt;

        private void Start()
        {
            _label = document != null ? document.rootVisualElement.Q<Label>(LabelName) : null;
            if (_label == null || placementInput == null)
            {
                Debug.LogError($"PlacementMessageView: assign the UI document (with a label named {LabelName}) and the placement input.", this);
                enabled = false;
                return;
            }

            Hide();
            placementInput.PlacementRefused += OnPlacementRefused;
        }

        private void OnDestroy()
        {
            if (placementInput != null) placementInput.PlacementRefused -= OnPlacementRefused;
        }

        private void Update()
        {
            if (_shown && Time.unscaledTime >= _hideAt) Hide();
        }

        private void OnPlacementRefused(PlacementResult reason)
        {
            _label.text = TextFor(reason);
            _label.style.display = DisplayStyle.Flex;
            _shown = true;
            _hideAt = Time.unscaledTime + duration;
        }

        private void Hide()
        {
            _label.style.display = DisplayStyle.None;
            _shown = false;
        }

        private string TextFor(PlacementResult reason)
        {
            switch (reason)
            {
                case PlacementResult.SpawnOrExit: return spawnOrExitText;
                case PlacementResult.CellBlocked: return cellBlockedText;
                case PlacementResult.EnemyInTheWay: return enemyInTheWayText;
                default: return wouldBlockPathText;
            }
        }
    }
}
