using ElementalTD.Core;
using ElementalTD.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace ElementalTD.Presentation
{
    /// <summary>
    /// Shows one line of text, for a time in battle seconds, when a wave is survived; a newer line replaces the one shown.
    /// Battle time stands still while a choice of perks is open, so the line is still seen after the pick (ADR 0007).
    /// Showing and hiding are a USS class, so the style sheet holds the look and the fade. Survived waves are polled from
    /// the battle (ADR 0004).
    /// </summary>
    public sealed class MessageView : MonoBehaviour
    {
        private const string LabelName = "message";
        private const string ShownClass = "message--shown";

        [SerializeField] private UIDocument document;
        [SerializeField] private PrototypeBootstrap bootstrap;

        [Tooltip("{0}: wave number, {1}: its survival bonus.")]
        [SerializeField] private string waveSurvivedText;
        [Tooltip("Seconds of battle time the survived line stays on screen, counted in ticks from the frame that shows it.")]
        [SerializeField, Min(0f)] private float waveSurvivedDuration = 2f;

        private Battle _battle;
        private Label _label;
        private bool _shown;
        private int _shownAtTick;
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
            else if (_shown && (_battle.TickCount - _shownAtTick) * Battle.TickSeconds >= waveSurvivedDuration)
            {
                Hide();
            }
        }

        private void Show(string text)
        {
            _label.text = text;
            _label.AddToClassList(ShownClass);
            _shown = true;
            _shownAtTick = _battle.TickCount;
        }

        private void Hide()
        {
            _label.RemoveFromClassList(ShownClass);
            _shown = false;
        }
    }
}
