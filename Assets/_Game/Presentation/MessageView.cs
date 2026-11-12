using ElementalTD.Core;
using ElementalTD.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace ElementalTD.Presentation
{
    /// <summary>
    /// Shows one line of text, for a time in unscaled seconds, when a wave is survived; a newer line replaces the one shown.
    /// Showing and hiding are a USS class, so the style sheet holds the look and the fade. Survived waves are polled from
    /// the battle (ADR 0004). A placement refusal shows at its cell (RefusalView), not here.
    /// </summary>
    public sealed class MessageView : MonoBehaviour
    {
        private const string LabelName = "message";
        private const string ShownClass = "message--shown";

        [SerializeField] private UIDocument document;
        [SerializeField] private PrototypeBootstrap bootstrap;

        [Tooltip("{0}: wave number, {1}: its survival bonus.")]
        [SerializeField] private string waveSurvivedText;
        [Tooltip("Seconds of unscaled time the survived line stays on screen.")]
        [SerializeField, Min(0f)] private float waveSurvivedDuration = 2f;

        private Battle _battle;
        private Label _label;
        private bool _shown;
        private float _hideAt;
        private int _seenSurvivedWaves;

        private void Start()
        {
            _label = document != null ? document.rootVisualElement.Q<Label>(LabelName) : null;
            if (_label == null || bootstrap == null || bootstrap.Battle == null)
            {
                Debug.LogError($"MessageView: assign the UI document (with a label named {LabelName}) and the bootstrap; the battle must exist.", this);
                enabled = false;
                return;
            }

            _battle = bootstrap.Battle;
            _seenSurvivedWaves = _battle.SurvivedWaves;
            Hide();
        }

        private void Update()
        {
            // Under the end screen the line stays hidden until the next run, also for the last wave's survived line.
            if (_battle.Outcome != RunOutcome.Running)
            {
                if (_shown) Hide();
                return;
            }

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

        private void Show(string text)
        {
            _label.text = text;
            _label.AddToClassList(ShownClass);
            _shown = true;
            _hideAt = Time.unscaledTime + waveSurvivedDuration;
        }

        private void Hide()
        {
            _label.RemoveFromClassList(ShownClass);
            _shown = false;
        }
    }
}
