using ElementalTD.Core;
using ElementalTD.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace ElementalTD.Presentation
{
    /// <summary>
    /// Shows one line of text, for a fixed time in unscaled seconds, when a purchase is refused or a wave is survived.
    /// A newer message replaces the one shown. Survived waves are polled from the battle (ADR 0004).
    /// </summary>
    public sealed class MessageView : MonoBehaviour
    {
        private const string LabelName = "message";

        [SerializeField] private UIDocument document;
        [SerializeField] private TowerPlacementInput placementInput;
        [SerializeField] private PrototypeBootstrap bootstrap;

        [Header("Texts")]
        [SerializeField] private string spawnOrExitText;
        [SerializeField] private string cellBlockedText;
        [SerializeField] private string enemyInTheWayText;
        [SerializeField] private string wouldBlockPathText;
        [SerializeField] private string notEnoughGoldText;
        [Tooltip("{0}: wave number, {1}: its survival bonus.")]
        [SerializeField] private string waveSurvivedText;

        [Tooltip("Seconds of unscaled time the message stays on screen.")]
        [SerializeField, Min(0f)] private float duration = 2f;

        private Battle _battle;
        private Label _label;
        private bool _shown;
        private float _hideAt;
        private int _seenSurvivedWaves;

        private void Start()
        {
            _label = document != null ? document.rootVisualElement.Q<Label>(LabelName) : null;
            if (_label == null || placementInput == null || bootstrap == null || bootstrap.Battle == null)
            {
                Debug.LogError($"MessageView: assign the UI document (with a label named {LabelName}), the placement input and the bootstrap; the battle must exist.", this);
                enabled = false;
                return;
            }

            _battle = bootstrap.Battle;
            _seenSurvivedWaves = _battle.SurvivedWaves;
            Hide();
            placementInput.PlacementRefused += OnPlacementRefused;
        }

        private void OnDestroy()
        {
            if (placementInput != null) placementInput.PlacementRefused -= OnPlacementRefused;
        }

        private void Update()
        {
            int survived = _battle.SurvivedWaves;
            if (survived != _seenSurvivedWaves)
            {
                _seenSurvivedWaves = survived;
                int number = _battle.LastSurvivedWave;
                int bonus = _battle.Waves.WaveAt(number - 1).SurvivalBonus;
                Show(string.Format(waveSurvivedText, number.ToString(), bonus.ToString()));
            }
            else if (_shown && Time.unscaledTime >= _hideAt)
            {
                Hide();
            }
        }

        private void OnPlacementRefused(PlacementResult reason) => Show(TextFor(reason));

        private void Show(string text)
        {
            _label.text = text;
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
                case PlacementResult.NotEnoughGold: return notEnoughGoldText;
                default: return wouldBlockPathText;
            }
        }
    }
}
