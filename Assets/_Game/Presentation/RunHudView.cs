using ElementalTD.Core;
using ElementalTD.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace ElementalTD.Presentation
{
    /// <summary>
    /// Shows the state of the run: wave, lives, the countdown to the next wave and, once it ends, the end screen.
    /// Polls the battle every frame and rewrites a label only when its number changes (ADR 0004).
    /// </summary>
    public sealed class RunHudView : MonoBehaviour
    {
        private const string WaveLabelName = "wave-label";
        private const string LivesLabelName = "lives-label";
        private const string NextWaveLabelName = "next-wave-label";
        private const string EndScreenName = "end-screen";
        private const string EndTitleName = "end-title";
        private const string RestartButtonName = "restart-button";

        [SerializeField] private UIDocument document;
        [SerializeField] private PrototypeBootstrap bootstrap;

        [Header("Texts")]
        [Tooltip("{0}: waves started, {1}: waves in the run.")]
        [SerializeField] private string waveText;
        [Tooltip("{0}: lives left.")]
        [SerializeField] private string livesText;
        [Tooltip("{0}: whole seconds left, rounded up.")]
        [SerializeField] private string nextWaveText;
        [SerializeField] private string victoryText;
        [SerializeField] private string defeatText;

        private Battle _battle;
        private Label _waveLabel;
        private Label _livesLabel;
        private Label _nextWaveLabel;
        private VisualElement _endScreen;
        private Label _endTitle;
        private Button _restartButton;

        // -1 forces the first frame to write every label.
        private int _shownWaves = -1;
        private int _shownLives = -1;
        private int _shownSeconds = -1;
        private int _shownOutcome = -1;
        private string _waveCountText;

        private void Start()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root != null)
            {
                _waveLabel = root.Q<Label>(WaveLabelName);
                _livesLabel = root.Q<Label>(LivesLabelName);
                _nextWaveLabel = root.Q<Label>(NextWaveLabelName);
                _endScreen = root.Q<VisualElement>(EndScreenName);
                _endTitle = root.Q<Label>(EndTitleName);
                _restartButton = root.Q<Button>(RestartButtonName);
            }

            if (_waveLabel == null || _livesLabel == null || _nextWaveLabel == null || _endScreen == null
                || _endTitle == null || _restartButton == null || bootstrap == null || bootstrap.Battle == null)
            {
                Debug.LogError($"RunHudView: assign the UI document (with {WaveLabelName}, {LivesLabelName}, {NextWaveLabelName}, " +
                    $"{EndScreenName}, {EndTitleName} and {RestartButtonName}) and the bootstrap; the battle must exist.", this);
                _restartButton = null;
                enabled = false;
                return;
            }

            _battle = bootstrap.Battle;
            _waveCountText = _battle.Waves.WaveCount.ToString();
            _restartButton.clicked += OnRestartClicked;
        }

        private void OnDestroy()
        {
            if (_restartButton != null) _restartButton.clicked -= OnRestartClicked;
        }

        private void Update()
        {
            WaveSchedule waves = _battle.Waves;

            int started = waves.StartedWaves;
            if (started != _shownWaves)
            {
                _shownWaves = started;
                _waveLabel.text = string.Format(waveText, started.ToString(), _waveCountText);
            }

            int lives = _battle.Lives;
            if (lives != _shownLives)
            {
                _shownLives = lives;
                _livesLabel.text = string.Format(livesText, lives.ToString());
            }

            float secondsLeft = waves.SecondsToNextWave;
            int seconds = secondsLeft > 0f ? Mathf.CeilToInt(secondsLeft) : 0; // 0: no delay is running
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                if (seconds > 0) _nextWaveLabel.text = string.Format(nextWaveText, seconds.ToString());
                _nextWaveLabel.style.display = seconds > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            RunOutcome outcome = _battle.Outcome;
            if ((int)outcome != _shownOutcome)
            {
                _shownOutcome = (int)outcome;
                if (outcome != RunOutcome.Running) _endTitle.text = outcome == RunOutcome.Won ? victoryText : defeatText;
                _endScreen.style.display = outcome == RunOutcome.Running ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        private void OnRestartClicked() => bootstrap.Restart();
    }
}
