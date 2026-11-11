using System.Text;
using ElementalTD.Core;
using ElementalTD.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace ElementalTD.Presentation
{
    /// <summary>
    /// Shows the state of the run: wave and its elements, lives, gold, the countdown to the next wave with its elements and,
    /// once it ends, the end screen. Polls the battle every frame and rewrites a label only when what it shows changes (ADR 0004).
    /// </summary>
    public sealed class RunHudView : MonoBehaviour
    {
        private const string WaveLabelName = "wave-label";
        private const string LivesLabelName = "lives-label";
        private const string GoldLabelName = "gold-label";
        private const string NextWaveLabelName = "next-wave-label";
        private const string EndScreenName = "end-screen";
        private const string EndTitleName = "end-title";
        private const string RestartButtonName = "restart-button";
        private const string WonClass = "end-screen--won";

        [SerializeField] private UIDocument document;
        [SerializeField] private PrototypeBootstrap bootstrap;
        [SerializeField] private ElementPalette palette;

        [Header("Texts")]
        [Tooltip("Before the first wave. {0}: waves started, {1}: waves in the run.")]
        [SerializeField] private string waveText;
        [Tooltip("{0}: waves started, {1}: waves in the run, {2}: the element names of the wave that started last.")]
        [SerializeField] private string waveElementsText;
        [Tooltip("{0}: lives left.")]
        [SerializeField] private string livesText;
        [Tooltip("{0}: gold.")]
        [SerializeField] private string goldText;
        [Tooltip("{0}: whole seconds left, rounded up, {1}: the element names of the wave to come.")]
        [SerializeField] private string nextWaveText;
        [Tooltip("One element name. {0}: its display name, {1}: its UI colour as hex, RRGGBB.")]
        [SerializeField] private string elementNameText;
        [Tooltip("Between two element names.")]
        [SerializeField] private string elementSeparator;
        [SerializeField] private string victoryText;
        [SerializeField] private string defeatText;

        private Battle _battle;
        private Label _waveLabel;
        private Label _livesLabel;
        private Label _goldLabel;
        private Label _nextWaveLabel;
        private VisualElement _endScreen;
        private Label _endTitle;
        private Button _restartButton;

        // -1 forces the first frame to write every label.
        private int _shownWaves = -1;
        private int _shownLives = -1;
        private int _shownGold = -1;
        private int _shownSeconds = -1;
        private int _shownOutcome = -1;
        private string _waveCountText;
        private string _nextWaveNames = string.Empty;
        private string[] _elementNames; // by element id; null for an id with no look
        private readonly StringBuilder _names = new StringBuilder();

        private void Start()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root != null)
            {
                _waveLabel = root.Q<Label>(WaveLabelName);
                _livesLabel = root.Q<Label>(LivesLabelName);
                _goldLabel = root.Q<Label>(GoldLabelName);
                _nextWaveLabel = root.Q<Label>(NextWaveLabelName);
                _endScreen = root.Q<VisualElement>(EndScreenName);
                _endTitle = root.Q<Label>(EndTitleName);
                _restartButton = root.Q<Button>(RestartButtonName);
            }

            if (_waveLabel == null || _livesLabel == null || _goldLabel == null || _nextWaveLabel == null || _endScreen == null
                || _endTitle == null || _restartButton == null || bootstrap == null || bootstrap.Battle == null || palette == null)
            {
                Debug.LogError($"RunHudView: assign the UI document (with {WaveLabelName}, {LivesLabelName}, {GoldLabelName}, {NextWaveLabelName}, " +
                    $"{EndScreenName}, {EndTitleName} and {RestartButtonName}), the bootstrap and the palette; the battle must exist.", this);
                _restartButton = null;
                enabled = false;
                return;
            }

            _battle = bootstrap.Battle;
            _waveCountText = _battle.Waves.WaveCount.ToString();
            BuildElementNames();
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
            bool wavesChanged = started != _shownWaves;
            if (wavesChanged)
            {
                _shownWaves = started;
                _waveLabel.text = started > 0
                    ? string.Format(waveElementsText, started.ToString(), _waveCountText, NamesOf(waves.WaveAt(started - 1)))
                    : string.Format(waveText, started.ToString(), _waveCountText);
                _nextWaveNames = started < waves.WaveCount ? NamesOf(waves.WaveAt(started)) : string.Empty;
            }

            int lives = _battle.Lives;
            if (lives != _shownLives)
            {
                _shownLives = lives;
                _livesLabel.text = string.Format(livesText, lives.ToString());
            }

            int gold = _battle.Gold;
            if (gold != _shownGold)
            {
                _shownGold = gold;
                _goldLabel.text = string.Format(goldText, gold.ToString());
            }

            float secondsLeft = waves.SecondsToNextWave;
            int seconds = secondsLeft > 0f ? Mathf.CeilToInt(secondsLeft) : 0; // 0: no delay is running
            if (seconds != _shownSeconds || wavesChanged)
            {
                _shownSeconds = seconds;
                if (seconds > 0) _nextWaveLabel.text = string.Format(nextWaveText, seconds.ToString(), _nextWaveNames);
                _nextWaveLabel.style.display = seconds > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            RunOutcome outcome = _battle.Outcome;
            if ((int)outcome != _shownOutcome)
            {
                _shownOutcome = (int)outcome;
                if (outcome != RunOutcome.Running) _endTitle.text = outcome == RunOutcome.Won ? victoryText : defeatText;
                _endScreen.EnableInClassList(WonClass, outcome == RunOutcome.Won);
                _endScreen.style.display = outcome == RunOutcome.Running ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        /// <summary>Each palette element's name, formatted once with its UI colour.</summary>
        private void BuildElementNames()
        {
            _elementNames = new string[palette.MaxElementId() + 1];
            for (int i = 0; i < palette.LookCount; i++)
            {
                ElementPalette.Look look = palette.LookAt(i);
                if (look.Element == null) continue;
                _elementNames[look.Element.ToElement().Id] =
                    string.Format(elementNameText, look.Element.DisplayName, ColorUtility.ToHtmlStringRGB(look.UiColor));
            }
        }

        /// <summary>The names of the distinct elements of the wave's groups, in group order. Built only when a wave starts.</summary>
        private string NamesOf(in Wave wave)
        {
            _names.Clear();
            for (int g = 0; g < wave.GroupCount; g++)
            {
                int id = wave.GroupAt(g).Enemy.Element.Id;
                if ((uint)id >= (uint)_elementNames.Length || _elementNames[id] == null || SeenBefore(wave, g, id)) continue;
                if (_names.Length > 0) _names.Append(elementSeparator);
                _names.Append(_elementNames[id]);
            }
            return _names.ToString();
        }

        private static bool SeenBefore(in Wave wave, int group, int id)
        {
            for (int g = 0; g < group; g++)
                if (wave.GroupAt(g).Enemy.Element.Id == id) return true;
            return false;
        }

        private void OnRestartClicked() => bootstrap.Restart();
    }
}
