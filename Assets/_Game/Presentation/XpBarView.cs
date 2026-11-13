using ElementalTD.Core;
using ElementalTD.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace ElementalTD.Presentation
{
    /// <summary>
    /// Shows the run's level and the share of the level's XP as a bar. Polls the progression after the frame's ticks and
    /// writes the label only when the level changes and the fill's scale only when it changes (ADR 0004). The looks, the
    /// ease and the pop are USS classes on the row.
    /// </summary>
    public sealed class XpBarView : MonoBehaviour
    {
        private const string RowName = "xp-row";
        private const string LabelName = "level-label";
        private const string FillName = "xp-fill";
        private const string PopClass = "xp-row--pop";
        private const string MaxClass = "xp-row--max";
        private const int NoLevel = -1;

        [SerializeField] private UIDocument document;
        [SerializeField] private PrototypeBootstrap bootstrap;

        [Header("Texts")]
        [Tooltip("{0}: the level.")]
        [SerializeField] private string levelText;
        [Tooltip("Once the level table has ended. {0}: the level.")]
        [SerializeField] private string maxLevelText;

        private Progression _progression;
        private VisualElement _row;
        private Label _label;
        private VisualElement _fill;

        private int _shownLevel = NoLevel;
        private float _shownScale = -1f; // below any share, so the first frame writes it
        private bool _popping;

        private void Start()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root != null)
            {
                _row = root.Q<VisualElement>(RowName);
                _label = root.Q<Label>(LabelName);
                _fill = root.Q<VisualElement>(FillName);
            }

            if (_row == null || _label == null || _fill == null || bootstrap == null || bootstrap.Battle == null)
            {
                Debug.LogError($"XpBarView: assign the UI document (with {RowName}, {LabelName} and {FillName}) and the bootstrap; " +
                    "the battle must exist.", this);
                enabled = false;
                return;
            }

            _progression = bootstrap.Battle.Progression;
            _label.usageHints = UsageHints.DynamicTransform;
            _fill.usageHints = UsageHints.DynamicTransform;
        }

        // LateUpdate, so the row shows the state after this frame's ticks.
        private void LateUpdate()
        {
            int level = _progression.Level;
            bool ended = _progression.TableEnded;

            // The pop look holds for one frame; removing it lets the base rules ease the label and the track back.
            if (level == _shownLevel && _popping)
            {
                _popping = false;
                _row.RemoveFromClassList(PopClass);
            }

            if (level != _shownLevel)
            {
                bool first = _shownLevel == NoLevel;
                _shownLevel = level;
                _label.text = string.Format(ended ? maxLevelText : levelText, level.ToString());
                _row.EnableInClassList(MaxClass, ended);
                if (!first)
                {
                    _row.AddToClassList(PopClass);
                    _popping = true;
                }
            }

            float scale = ended ? 1f : (float)_progression.XpIntoLevel / _progression.XpForLevel;
            if (scale != _shownScale)
            {
                _shownScale = scale;
                _fill.style.scale = new Scale(new Vector2(scale, 1f));
            }
        }
    }
}
