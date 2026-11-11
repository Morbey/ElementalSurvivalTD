using ElementalTD.Core;
using ElementalTD.Data;
using ElementalTD.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace ElementalTD.Presentation
{
    /// <summary>
    /// Builds one button per buyable tower in the bottom bar; a click selects that tower. Polls the selection and the gold,
    /// and switches the selected and short-of-gold classes only when they change. Holds no rule: the input selects.
    /// </summary>
    public sealed class TowerBarView : MonoBehaviour
    {
        private const string TowerBarName = "tower-bar";
        private const string ButtonClass = "tower-button";
        private const string KeyClass = "tower-button__key";
        private const string SwatchClass = "tower-button__swatch";
        private const string InfoClass = "tower-button__info";
        private const string NameClass = "tower-button__name";
        private const string PriceClass = "tower-button__price";
        private const string SelectedClass = "tower-button--selected";
        private const string ShortClass = "tower-button--short";

        [SerializeField] private UIDocument document;
        [SerializeField] private TowerPlacementInput placementInput;
        [SerializeField] private ElementPalette palette;
        [SerializeField] private PrototypeBootstrap bootstrap;

        [Header("Texts")]
        [Tooltip("{0}: the tower's cost.")]
        [SerializeField] private string priceText;

        private Battle _battle;
        private Button[] _buttons;
        private int[] _costs;
        private int _shownSelected = -1;
        private int _shownGold = int.MinValue; // forces the first update

        private void Start()
        {
            VisualElement bar = document != null ? document.rootVisualElement.Q<VisualElement>(TowerBarName) : null;
            if (bar == null || placementInput == null || palette == null || bootstrap == null || bootstrap.Battle == null)
            {
                Debug.LogError($"TowerBarView: assign the UI document (with {TowerBarName}), the placement input, the palette and the bootstrap; the battle must exist.", this);
                enabled = false;
                return;
            }

            _battle = bootstrap.Battle;
            int count = placementInput.TowerCount;
            _buttons = new Button[count];
            _costs = new int[count];
            for (int i = 0; i < count; i++)
            {
                TowerDefinition tower = placementInput.TowerAt(i);
                _costs[i] = tower.ToStats().Cost;
                _buttons[i] = BuildButton(i, tower);
                bar.Add(_buttons[i]);
            }
        }

        private Button BuildButton(int index, TowerDefinition tower)
        {
            var button = new Button(() => placementInput.Select(index)) { focusable = false };
            button.AddToClassList(ButtonClass);

            button.Add(NewLabel(KeyClass, (index + 1).ToString()));

            var swatch = new VisualElement { pickingMode = PickingMode.Ignore };
            swatch.AddToClassList(SwatchClass);
            Element element = tower.Element != null ? tower.Element.ToElement() : default;
            if (palette.TryGetLook(element, out ElementPalette.Look look) && look.Tower != null)
                swatch.style.backgroundColor = look.Tower.color;
            else
                Debug.LogError($"TowerBarView: the palette has no look with a tower material for {tower.name}'s element.", this);
            button.Add(swatch);

            var info = new VisualElement { pickingMode = PickingMode.Ignore };
            info.AddToClassList(InfoClass);
            info.Add(NewLabel(NameClass, tower.Element != null ? tower.Element.DisplayName : string.Empty));
            info.Add(NewLabel(PriceClass, string.Format(priceText, _costs[index].ToString())));
            button.Add(info);

            return button;
        }

        private static Label NewLabel(string className, string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        private void Update()
        {
            int selected = placementInput.SelectedIndex;
            if (selected != _shownSelected)
            {
                if ((uint)_shownSelected < (uint)_buttons.Length) _buttons[_shownSelected].RemoveFromClassList(SelectedClass);
                if ((uint)selected < (uint)_buttons.Length) _buttons[selected].AddToClassList(SelectedClass);
                _shownSelected = selected;
            }

            int gold = _battle.Gold;
            if (gold != _shownGold)
            {
                _shownGold = gold;
                for (int i = 0; i < _buttons.Length; i++) _buttons[i].EnableInClassList(ShortClass, _costs[i] > gold);
            }
        }
    }
}
