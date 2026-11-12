using System.Globalization;
using ElementalTD.Core;
using ElementalTD.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace ElementalTD.Presentation
{
    /// <summary>
    /// Shows the tower selected on the board in the side column, in place of the buy buttons: what it is, and on Sell what a
    /// sale pays now, polled from the battle after the frame's ticks (ADR 0004). Texts are built only when the selection or
    /// the value changes; the looks and the drop are USS classes, so the style sheet holds their colours and times.
    /// Holds no rule: Sell and the close button go through the placement input.
    /// </summary>
    public sealed class TowerPanelView : MonoBehaviour
    {
        private const string SideColumnName = "side-column";
        private const string TileName = "tower-panel-tile";
        private const string TitleName = "tower-panel-title";
        private const string ElementValueName = "stat-element";
        private const string DamageValueName = "stat-damage";
        private const string ShotsValueName = "stat-shots";
        private const string RangeValueName = "stat-range";
        private const string CloseButtonName = "panel-close";
        private const string SellButtonName = "sell-button";
        private const string SellValueName = "sell-value";
        private const string SellTailName = "sell-value-tail";
        private const string TowerShownClass = "side-column--tower";
        private const string RefundClass = "sell-button--refund";
        private const string PopClass = "sell-button--pop";
        private const string SettleClass = "sell-button--settle";
        private const int NoValue = int.MinValue;

        [SerializeField] private UIDocument document;
        [SerializeField] private TowerPlacementInput placementInput;
        [SerializeField] private ElementPalette palette;
        [SerializeField] private PrototypeBootstrap bootstrap;

        [Header("Texts")]
        [Tooltip("{0}: the element's display name.")]
        [SerializeField] private string titleText;
        [Tooltip("A .NET numeric format for Damage, Shots/s and Range, applied with the invariant culture.")]
        [SerializeField] private string statNumberFormat;
        [Tooltip("Sell while it pays the full cost. {0}: the amount.")]
        [SerializeField] private string fullValueText;
        [Tooltip("Sell while it pays less than the cost. {0}: the amount.")]
        [SerializeField] private string refundValueText;
        [Tooltip("After the refund amount. {0}: the tower's cost.")]
        [SerializeField] private string refundTailText;

        private Battle _battle;
        private VisualElement _column;
        private VisualElement _tile;
        private Label _title;
        private Label _elementValue;
        private Label _damageValue;
        private Label _shotsValue;
        private Label _rangeValue;
        private Button _closeButton;
        private Button _sellButton;
        private Label _sellValue;
        private Label _sellTail;

        private Tower _shownTower;
        private int _shownValue = NoValue;
        private bool _popping;

        private void Start()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            if (root != null)
            {
                _column = root.Q<VisualElement>(SideColumnName);
                _tile = root.Q<VisualElement>(TileName);
                _title = root.Q<Label>(TitleName);
                _elementValue = root.Q<Label>(ElementValueName);
                _damageValue = root.Q<Label>(DamageValueName);
                _shotsValue = root.Q<Label>(ShotsValueName);
                _rangeValue = root.Q<Label>(RangeValueName);
                _closeButton = root.Q<Button>(CloseButtonName);
                _sellButton = root.Q<Button>(SellButtonName);
                _sellValue = root.Q<Label>(SellValueName);
                _sellTail = root.Q<Label>(SellTailName);
            }

            if (_column == null || _tile == null || _title == null || _elementValue == null || _damageValue == null
                || _shotsValue == null || _rangeValue == null || _closeButton == null || _sellButton == null || _sellValue == null
                || _sellTail == null || placementInput == null || palette == null || bootstrap == null || bootstrap.Battle == null)
            {
                Debug.LogError($"TowerPanelView: assign the UI document (with {SideColumnName}, {TileName}, {TitleName}, {ElementValueName}, " +
                    $"{DamageValueName}, {ShotsValueName}, {RangeValueName}, {CloseButtonName}, {SellButtonName}, {SellValueName} and " +
                    $"{SellTailName}), the placement input, the palette and the bootstrap; the battle must exist.", this);
                _closeButton = null;
                _sellButton = null;
                enabled = false;
                return;
            }

            _battle = bootstrap.Battle;
            _closeButton.clicked += OnCloseClicked;
            _sellButton.clicked += OnSellClicked;
        }

        private void OnDestroy()
        {
            if (_closeButton != null) _closeButton.clicked -= OnCloseClicked;
            if (_sellButton != null) _sellButton.clicked -= OnSellClicked;
        }

        // LateUpdate, so the value is the one after this frame's ticks.
        private void LateUpdate()
        {
            Tower tower = placementInput.SelectedTower;
            if (!ReferenceEquals(tower, _shownTower)) ShowTower(tower);
            if (tower == null) return;

            // The pop look holds for one frame, then the settle look eases to the refund look.
            if (_popping)
            {
                _popping = false;
                _sellButton.RemoveFromClassList(PopClass);
                _sellButton.AddToClassList(SettleClass);
            }

            _battle.TryGetSellValue(tower.Cell, out int value);
            if (value != _shownValue) ShowValue(value, tower.Stats.Cost);
        }

        private void ShowTower(Tower tower)
        {
            _shownTower = tower;
            _shownValue = NoValue;
            _popping = false;
            _sellButton.RemoveFromClassList(PopClass);
            _sellButton.RemoveFromClassList(SettleClass);
            _column.EnableInClassList(TowerShownClass, tower != null);
            if (tower == null) return;

            TowerStats stats = tower.Stats;
            if (palette.TryGetLook(stats.Element, out ElementPalette.Look look) && look.Element != null)
            {
                _title.text = string.Format(titleText, look.Element.DisplayName);
                _elementValue.text = look.Element.DisplayName;
                _elementValue.style.color = look.UiColor;
                if (look.Tower != null) _tile.style.backgroundColor = look.Tower.color;
            }
            else
            {
                Debug.LogError("TowerPanelView: the palette has no look for the selected tower's element.", this);
            }

            _damageValue.text = stats.Damage.ToString(statNumberFormat, CultureInfo.InvariantCulture);
            _shotsValue.text = stats.ShotsPerSecond.ToString(statNumberFormat, CultureInfo.InvariantCulture);
            _rangeValue.text = stats.Range.ToString(statNumberFormat, CultureInfo.InvariantCulture);
            _sellTail.text = string.Format(refundTailText, stats.Cost.ToString());
        }

        private void ShowValue(int value, int cost)
        {
            bool drop = _shownValue != NoValue;
            _shownValue = value;

            bool refund = value < cost;
            _sellValue.text = string.Format(refund ? refundValueText : fullValueText, value.ToString());
            _sellButton.EnableInClassList(RefundClass, refund);
            if (!drop) return;

            _sellButton.RemoveFromClassList(SettleClass);
            _sellButton.AddToClassList(PopClass);
            _popping = true;
        }

        private void OnCloseClicked() => placementInput.Deselect();

        private void OnSellClicked() => placementInput.SellSelected();
    }
}
