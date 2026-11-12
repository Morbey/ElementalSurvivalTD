using System.Collections.Generic;
using System.Globalization;
using ElementalTD.Core;
using ElementalTD.Data;
using ElementalTD.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace ElementalTD.Presentation
{
    /// <summary>
    /// A number over the board for every hit in the battle's log, and the immune word under every enemy that towers have
    /// just failed to hurt (ADR 0006). Polls the log by its count and each enemy's mark; ages everything in battle time.
    /// Every label is made at start, except words when more enemies are on the board than ever before.
    /// </summary>
    // After BoardCameraFramer, so a frame that reframes the camera places the labels with the new one.
    [DefaultExecutionOrder(100)]
    public sealed class DamageNumberView : MonoBehaviour
    {
        private const string NumbersName = "damage-numbers";
        private const string WordsName = "immune-words";
        private const string WordClass = "immune-word";
        private const string HangTopClass = "floating-text--hang-top";

        // By HitKind; an immune hit has no number.
        private static readonly string[] KindClasses =
            { null, "damage-number--weak", "damage-number--normal", "damage-number--strong" };

        [SerializeField] private UIDocument document;
        [SerializeField] private PrototypeBootstrap bootstrap;
        [SerializeField] private Camera viewCamera;

        [Header("Numbers")]
        [Tooltip("Seconds of battle time a weak number lives.")]
        [SerializeField, Min(0f)] private float weakLifeSeconds;
        [Tooltip("Seconds of battle time a normal number lives.")]
        [SerializeField, Min(0f)] private float normalLifeSeconds;
        [Tooltip("Seconds of battle time a strong number lives.")]
        [SerializeField, Min(0f)] private float strongLifeSeconds;
        [Tooltip("Cells above the hit point, towards the top of the screen, where a number's bottom centre starts.")]
        [SerializeField] private float heightCells;
        [Tooltip("Panel pixels a number rises over its life, eased out.")]
        [SerializeField] private float risePixels;
        [Tooltip("Share of its life a number stays fully opaque before it fades linearly to 0.")]
        [SerializeField, Range(0f, 1f)] private float opaqueShare;
        [Tooltip("Panel pixels from the panel's top above which a number's bottom centre never goes.")]
        [SerializeField, Min(0f)] private float topLimitPixels;
        [Tooltip("Panel offsets (x, y; a negative y is up) given to new numbers in turn, one counter for all.")]
        [SerializeField] private Vector2[] spawnOffsets = new Vector2[0];
        [Tooltip("Numbers alive at most; the labels are made at start. At the cap a new number takes the oldest's.")]
        [SerializeField, Min(0)] private int maxNumbers;
        [Tooltip("From this many numbers alive, a weak hit shows none.")]
        [SerializeField, Min(0)] private int weakCutoff;

        [Header("Immune word")]
        [SerializeField] private string immuneText;
        [Tooltip("Cells below the enemy's centre, towards the bottom of the screen, where the word's top centre hangs.")]
        [SerializeField] private float wordDepthCells;
        [Tooltip("Seconds of battle time the word stays after the last shot that could not hurt the enemy.")]
        [SerializeField, Min(0f)] private float immuneHoldSeconds;
        [Tooltip("The last seconds of the hold, over which the word fades linearly to 0.")]
        [SerializeField, Min(0f)] private float immuneFadeSeconds;
        [Tooltip("Words made at start; the pool grows only when more enemies are on the board.")]
        [SerializeField, Min(0)] private int prewarmWords;

        private struct Number
        {
            public bool Alive;
            public int Tick;
            public float Life;
            public float X, Y;      // the raised board point, in cells
            public Vector2 Offset;
            public int Order;       // when it was shown: the lowest is the oldest
        }

        private Battle _battle;
        private MapDefinition _map;
        private VisualElement _root;
        private VisualElement _wordParent;
        private FloatingLabel[] _numberLabels;
        private Number[] _numbers;
        private int _alive;
        private int _nextOrder;
        private int _nextOffset;
        private int _readHits;
        private readonly List<FloatingLabel> _words = new List<FloatingLabel>();
        private readonly Dictionary<int, string> _amountTexts = new Dictionary<int, string>();

        private void Start()
        {
            _root = document != null ? document.rootVisualElement : null;
            VisualElement numberParent = _root?.Q<VisualElement>(NumbersName);
            _wordParent = _root?.Q<VisualElement>(WordsName);
            if (numberParent == null || _wordParent == null || bootstrap == null || bootstrap.Battle == null || viewCamera == null)
            {
                Debug.LogError($"DamageNumberView: assign the UI document (with {NumbersName} and {WordsName}), the bootstrap and the camera; the battle must exist.", this);
                enabled = false;
                return;
            }

            _battle = bootstrap.Battle;
            _map = bootstrap.Map;
            _readHits = _battle.HitCount;
            _numbers = new Number[maxNumbers];
            _numberLabels = new FloatingLabel[maxNumbers];
            for (int i = 0; i < maxNumbers; i++) _numberLabels[i] = FloatingLabel.Create(numberParent);
            for (int i = 0; i < prewarmWords; i++) AddWord();
        }

        private void LateUpdate()
        {
            if (_battle.Outcome != RunOutcome.Running)
            {
                HideAll();
                return;
            }

            int count = _battle.HitCount;
            for (int i = Mathf.Max(_readHits, count - Battle.HitLogCapacity); i < count; i++)
            {
                Hit hit = _battle.HitAt(i);
                Show(in hit);
            }
            _readHits = count;

            IPanel panel = _root.panel;
            if (panel == null) return;
            var toPanel = new BoardToPanel(viewCamera, panel, _map);
            float alpha = bootstrap.TickAlpha;
            float now = _battle.TickCount + alpha;
            DrawNumbers(toPanel, now);
            DrawWords(toPanel, now, alpha);
        }

        /// <summary>What the poll calls for each new record; public for tests.</summary>
        public void Show(in Hit hit)
        {
            float life = LifeOf(hit.Kind);
            float age = (_battle.TickCount - hit.Tick + bootstrap.TickAlpha) * Battle.TickSeconds;
            if (!(age < life) || _numbers.Length == 0) return;
            if (hit.Kind == HitKind.Weak && _alive >= weakCutoff) return;

            int slot = TakeSlot();
            Vector2 offset = Vector2.zero;
            if (spawnOffsets.Length > 0)
            {
                offset = spawnOffsets[_nextOffset];
                _nextOffset = (_nextOffset + 1) % spawnOffsets.Length;
            }
            _numbers[slot] = new Number
            {
                Alive = true, Tick = hit.Tick, Life = life, X = hit.X, Y = hit.Y + heightCells, Offset = offset, Order = _nextOrder++
            };

            FloatingLabel label = _numberLabels[slot];
            label.SetText(TextOf(hit.Amount));
            label.SetClass(KindClasses[(int)hit.Kind]);
        }

        private void DrawNumbers(in BoardToPanel toPanel, float now)
        {
            for (int i = 0; i < _numbers.Length; i++)
            {
                ref Number number = ref _numbers[i];
                if (!number.Alive) continue;

                FloatingLabel label = _numberLabels[i];
                float t = (now - number.Tick) * Battle.TickSeconds / number.Life;
                if (!(t < 1f))
                {
                    number.Alive = false;
                    _alive--;
                    label.Hide();
                    continue;
                }

                float eased = 1f - (1f - t) * (1f - t);
                Vector2 position = toPanel.Point(number.X, number.Y) + number.Offset;
                position.y = Mathf.Max(position.y - risePixels * eased, topLimitPixels);
                label.MoveTo(position);
                label.SetOpacity(t <= opaqueShare ? 1f : (1f - t) / (1f - opaqueShare));
                label.Show();
            }
        }

        private void DrawWords(in BoardToPanel toPanel, float now, float alpha)
        {
            Board board = _battle.Board;
            int enemies = board.EnemyCount;
            while (_words.Count < enemies) AddWord();

            for (int i = 0; i < enemies; i++)
            {
                Enemy enemy = board.EnemyAt(i);
                FloatingLabel word = _words[i];
                int mark = enemy.ImmuneHitTick;
                float age = (now - mark) * Battle.TickSeconds;
                if (mark == 0 || !(age < immuneHoldSeconds))
                {
                    word.Hide();
                    continue;
                }

                // As CombatView draws the enemy: between its last two ticks.
                float x = Mathf.Lerp(enemy.PreviousX, enemy.X, alpha);
                float y = Mathf.Lerp(enemy.PreviousY, enemy.Y, alpha);
                word.MoveTo(toPanel.Point(x, y - wordDepthCells));
                word.SetOpacity(Mathf.Clamp01((immuneHoldSeconds - age) / immuneFadeSeconds));
                word.Show();
            }
            for (int i = enemies; i < _words.Count; i++) _words[i].Hide();
        }

        private void HideAll()
        {
            for (int i = 0; i < _numbers.Length; i++)
            {
                if (!_numbers[i].Alive) continue;
                _numbers[i].Alive = false;
                _numberLabels[i].Hide();
            }
            _alive = 0;
            for (int i = 0; i < _words.Count; i++) _words[i].Hide();
        }

        /// <summary>A free label, or the oldest number's.</summary>
        private int TakeSlot()
        {
            int oldest = 0;
            for (int i = 0; i < _numbers.Length; i++)
            {
                if (!_numbers[i].Alive)
                {
                    _alive++;
                    return i;
                }
                if (_numbers[i].Order < _numbers[oldest].Order) oldest = i;
            }
            return oldest;
        }

        private float LifeOf(HitKind kind)
        {
            switch (kind)
            {
                case HitKind.Weak: return weakLifeSeconds;
                case HitKind.Normal: return normalLifeSeconds;
                case HitKind.Strong: return strongLifeSeconds;
                default: return 0f;
            }
        }

        /// <summary>Built the first time an amount occurs, then reused.</summary>
        private string TextOf(float amount)
        {
            int whole = Mathf.Max(1, Mathf.FloorToInt(amount + 0.5f));
            if (!_amountTexts.TryGetValue(whole, out string text))
            {
                text = whole.ToString(CultureInfo.InvariantCulture);
                _amountTexts.Add(whole, text);
            }
            return text;
        }

        private void AddWord()
        {
            FloatingLabel word = FloatingLabel.Create(_wordParent);
            word.Label.AddToClassList(HangTopClass);
            word.Label.AddToClassList(WordClass);
            word.SetText(immuneText);
            _words.Add(word);
        }
    }
}
