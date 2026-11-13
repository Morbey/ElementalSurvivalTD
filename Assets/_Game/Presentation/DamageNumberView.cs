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
    /// The numbers of one enemy are its stack: those alive that carry its entry number. A number enters at slot 0 above
    /// its enemy; each newer one lifts it by its own height over its own entry, and the lifts add up, so where a number is
    /// drawn depends on the records, the clock and where its enemy is drawn, never on which frames were drawn.
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
        [Tooltip("Panel pixels a weak number takes in its stack: its label's height.")]
        [SerializeField, Min(0f)] private float weakHeight;
        [Tooltip("Panel pixels a normal number takes in its stack: its label's height.")]
        [SerializeField, Min(0f)] private float normalHeight;
        [Tooltip("Panel pixels a strong number takes in its stack: its label's height.")]
        [SerializeField, Min(0f)] private float strongHeight;
        [Tooltip("Cells above the enemy's centre, towards the top of the screen, where slot 0's bottom centre hangs.")]
        [SerializeField] private float heightCells;
        [Tooltip("Seconds of battle time a number grows in (ease-in), lifts the older ones (ease-out), and fades out when it leaves.")]
        [SerializeField, Min(0.001f)] private float pushSeconds;
        [Tooltip("Slots a stack holds; a number pushed beyond them leaves.")]
        [SerializeField, Min(1)] private int budgetSlots;
        [Tooltip("Opacity at most of a number in the last slot of the budget or beyond it.")]
        [SerializeField, Range(0f, 1f)] private float topSlotOpacity;
        [Tooltip("Share of its life a number stays fully opaque before it fades linearly to 0.")]
        [SerializeField, Range(0f, 1f)] private float opaqueShare;
        [Tooltip("Panel pixels from the panel's top above which a number's top never goes.")]
        [SerializeField, Min(0f)] private float topLimitPixels;
        [Tooltip("Panel pixels to the right per slot of a number held on the top line.")]
        [SerializeField] private float lineStepPixels;
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
            public int Entry;       // of the enemy hit: its stack
            public float Life;
            public float Height;
            public float X, Y;      // where its enemy was last, in cells
            public int Slot;        // newer numbers shown for its enemy
            public float Lift;      // panel pixels: the heights of those newer numbers, once each has fully entered
            public bool Leaving;
            public int LeaveTick;
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
        private int[] _entering;   // this frame's numbers younger than the push time
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
            _entering = new int[maxNumbers];
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

            float alpha = bootstrap.TickAlpha;
            // Before the poll, so this frame's hits see the alive count and the free labels as they are now.
            ExpireNumbers(alpha);

            int count = _battle.HitCount;
            for (int i = Mathf.Max(_readHits, count - Battle.HitLogCapacity); i < count; i++)
            {
                Hit hit = _battle.HitAt(i);
                Show(in hit);
            }
            _readHits = count;
            // Again: a hit read late may have pushed a number beyond the budget longer ago than the push time.
            ExpireNumbers(alpha);

            IPanel panel = _root.panel;
            if (panel == null) return;
            var toPanel = new BoardToPanel(viewCamera, panel, _map);
            DrawNumbers(toPanel, alpha);
            DrawWords(toPanel, alpha);
        }

        /// <summary>What the poll calls for each new record; public for tests.</summary>
        public void Show(in Hit hit)
        {
            float life = LifeOf(hit.Kind);
            if (!(AgeOf(hit.Tick, bootstrap.TickAlpha) < life) || _numbers.Length == 0) return;
            if (hit.Kind == HitKind.Weak && _alive >= weakCutoff) return;

            float height = HeightOf(hit.Kind);
            for (int i = 0; i < _numbers.Length; i++)
            {
                ref Number older = ref _numbers[i];
                if (!older.Alive || older.Entry != hit.TargetEntry) continue;
                older.X = hit.X;
                older.Y = hit.Y;
                older.Slot++;
                older.Lift += height;
                if (older.Slot >= budgetSlots && !older.Leaving)
                {
                    older.Leaving = true;
                    older.LeaveTick = hit.Tick;
                }
            }

            int slot = TakeSlot();
            _numbers[slot] = new Number
            {
                Alive = true, Tick = hit.Tick, Entry = hit.TargetEntry, Life = life, Height = height, X = hit.X, Y = hit.Y,
                Order = _nextOrder++
            };

            FloatingLabel label = _numberLabels[slot];
            label.SetText(TextOf(hit.Amount));
            label.SetClass(KindClasses[(int)hit.Kind]);
        }

        /// <summary>Battle seconds since the tick, with the fraction of a tick left over.</summary>
        private float AgeOf(int tick, float alpha) => (_battle.TickCount - tick + alpha) * Battle.TickSeconds;

        /// <summary>Gone at the end of its life, or the push time after it began to leave; its label is then free.</summary>
        private void ExpireNumbers(float alpha)
        {
            for (int i = 0; i < _numbers.Length; i++)
            {
                ref Number number = ref _numbers[i];
                if (!number.Alive) continue;
                if (AgeOf(number.Tick, alpha) < number.Life
                    && !(number.Leaving && AgeOf(number.LeaveTick, alpha) >= pushSeconds)) continue;
                number.Alive = false;
                _alive--;
                _numberLabels[i].Hide();
            }
        }

        private void DrawNumbers(in BoardToPanel toPanel, float alpha)
        {
            Board board = _battle.Board;
            int entering = 0;
            for (int i = 0; i < _numbers.Length; i++)
                if (_numbers[i].Alive && AgeOf(_numbers[i].Tick, alpha) < pushSeconds) _entering[entering++] = i;

            for (int i = 0; i < _numbers.Length; i++)
            {
                ref Number number = ref _numbers[i];
                if (!number.Alive) continue;

                // Follows its enemy while it is on the board; after that it keeps the point where it was last.
                for (int e = 0; e < board.EnemyCount; e++)
                {
                    Enemy enemy = board.EnemyAt(e);
                    if (enemy.Entry != number.Entry) continue;
                    // As CombatView draws the enemy: between its last two ticks.
                    number.X = Mathf.Lerp(enemy.PreviousX, enemy.X, alpha);
                    number.Y = Mathf.Lerp(enemy.PreviousY, enemy.Y, alpha);
                    break;
                }

                // The whole lift and slot, less the part of each newer number's push not yet done.
                float lift = number.Lift;
                float slot = number.Slot;
                for (int k = 0; k < entering; k++)
                {
                    ref Number newer = ref _numbers[_entering[k]];
                    if (newer.Entry != number.Entry || newer.Order <= number.Order) continue;
                    float left = 1f - EaseOut(AgeOf(newer.Tick, alpha) / pushSeconds);
                    lift -= newer.Height * left;
                    slot -= left;
                }

                Vector2 position = toPanel.Point(number.X, number.Y + heightCells);
                position.y -= lift;
                if (position.y - number.Height < topLimitPixels)
                {
                    position.y = topLimitPixels + number.Height;
                    position.x += lineStepPixels * slot;
                }

                float age = AgeOf(number.Tick, alpha);
                float grown = Mathf.Min(1f, age / pushSeconds);
                float t = age / number.Life;
                float opacity = t <= opaqueShare ? 1f : (1f - t) / (1f - opaqueShare);
                if (number.Slot >= budgetSlots - 1) opacity = Mathf.Min(opacity, topSlotOpacity);
                if (number.Leaving) opacity *= Mathf.Max(0f, 1f - AgeOf(number.LeaveTick, alpha) / pushSeconds);

                FloatingLabel label = _numberLabels[i];
                label.MoveTo(position);
                label.SetScale(grown * grown);
                label.SetOpacity(opacity);
                label.Show();
            }
        }

        /// <summary>The direction's ease-out, 1 − (1 − t)², with t clamped to 0..1.</summary>
        private static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        private void DrawWords(in BoardToPanel toPanel, float alpha)
        {
            Board board = _battle.Board;
            int enemies = board.EnemyCount;
            while (_words.Count < enemies) AddWord();

            for (int i = 0; i < enemies; i++)
            {
                Enemy enemy = board.EnemyAt(i);
                FloatingLabel word = _words[i];
                int mark = enemy.ImmuneHitTick;
                float age = AgeOf(mark, alpha);
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

        private float HeightOf(HitKind kind)
        {
            switch (kind)
            {
                case HitKind.Weak: return weakHeight;
                case HitKind.Normal: return normalHeight;
                case HitKind.Strong: return strongHeight;
                default: return 0f;
            }
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
