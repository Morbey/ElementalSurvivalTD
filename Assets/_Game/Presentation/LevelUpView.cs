using System.Globalization;
using ElementalTD.Core;
using ElementalTD.Data;
using ElementalTD.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace ElementalTD.Presentation
{
    /// <summary>
    /// The level-up screen (ADR 0007, visual direction 1.7): polls the progression after the frame's ticks and, when a choice
    /// opens, shows its level and one card per offered perk. A click on a card, once the lock has run, holds the picked look
    /// and only then sends the pick to the battle. The lock and the hold are real time; the looks and the dealing are USS.
    /// Every perk's card words are built once at start; a choice writes the texts and classes, nothing else writes per frame.
    /// </summary>
    public sealed class LevelUpView : MonoBehaviour
    {
        private const string OverlayName = "level-up";
        private const string TitleName = "level-up-title";
        private const string HintName = "level-up-hint";
        private const string CardRowName = "perk-cards";
        private const string ShownClass = "level-up--shown";
        private const string EnterClass = "level-up--enter";
        private const string CardClass = "perk-card";
        private const string SlotClassPrefix = "perk-card--slot-";
        private const string PickedClass = "perk-card--picked";
        private const string PassedClass = "perk-card--passed";
        private const string TileClass = "perk-card__tile";
        private const string TileAllClass = "perk-card__tile--all";
        private const string TileGoldClass = "perk-card__tile--gold";
        private const string CardTitleClass = "perk-card__title";
        private const string NumberClass = "perk-card__number";
        private const string AttributeClass = "perk-card__attribute";
        private const string StackClass = "perk-card__stack";

        private enum TileKind { Element, All, Gold }

        private sealed class Card
        {
            public Button Button;
            public Label Tile, Title, Number, Attribute, Stack;
        }

        [SerializeField] private UIDocument document;
        [SerializeField] private PrototypeBootstrap bootstrap;
        [SerializeField] private ElementPalette palette;

        [Header("Times, in real seconds")]
        [Tooltip("After a choice appears, the cards take no hover and no click for this long.")]
        [SerializeField, Min(0f)] private float lockSeconds;
        [Tooltip("After a pick, the picked look holds this long before the pick reaches the battle.")]
        [SerializeField, Min(0f)] private float holdSeconds;

        [Header("Texts")]
        [Tooltip("{0}: the level the choice answers.")]
        [SerializeField] private string titleText = "";
        [Tooltip("Under the title, for a choice alone.")]
        [SerializeField] private string hintText = "";
        [Tooltip("Under the title, for choices in a row. {0}: this choice's number, {1}: how many are in the row.")]
        [SerializeField] private string hintInARowText = "";
        [Tooltip("A multiplication. {0}: the whole percent it adds.")]
        [SerializeField] private string percentText = "";
        [Tooltip("An addition. {0}: the amount.")]
        [SerializeField] private string amountText = "";
        [Tooltip("For a perk taken before. {0}: the total after this pick, as the percent or the amount text writes it.")]
        [SerializeField] private string stackText = "";
        [Tooltip("The tile of a perk on every tower.")]
        [SerializeField] private string allTileText = "";
        [Tooltip("The tile of a gold perk.")]
        [SerializeField] private string goldTileText = "";
        [Tooltip("An element's name in a card title. {0}: its display name, {1}: its UI colour as hex, RRGGBB.")]
        [SerializeField] private string elementNameText = "";

        private Battle _battle;
        private Progression _progression;
        private VisualElement _overlay;
        private Label _title;
        private Label _hint;
        private Card[] _cards;

        // By the perk's place in the pool.
        private string[] _perkTitles;
        private string[] _perkNumbers;
        private string[] _perkAttributes;
        private string[] _perkTileTexts;
        private TileKind[] _perkTiles;
        private Color[] _perkTileColors;
        private Color[] _perkTileTextColors;

        private int _shownChoices;
        private bool _shown;
        private bool _entering;
        private bool _locked;
        private float _lockEndsAt;
        private bool _holding;
        private float _holdEndsAt;
        private int _pickedSlot;

        private void Start()
        {
            VisualElement root = document != null ? document.rootVisualElement : null;
            VisualElement cardRow = null;
            if (root != null)
            {
                _overlay = root.Q<VisualElement>(OverlayName);
                _title = root.Q<Label>(TitleName);
                _hint = root.Q<Label>(HintName);
                cardRow = root.Q<VisualElement>(CardRowName);
            }

            if (_overlay == null || _title == null || _hint == null || cardRow == null || palette == null || bootstrap == null
                || bootstrap.Battle == null || bootstrap.Run == null)
            {
                Debug.LogError($"LevelUpView: assign the UI document (with {OverlayName}, {TitleName}, {HintName} and {CardRowName}), " +
                    "the palette and the bootstrap; the battle must exist.", this);
                enabled = false;
                return;
            }

            _battle = bootstrap.Battle;
            _progression = _battle.Progression;
            BuildPerkWords(bootstrap.Run);

            _cards = new Card[Progression.OfferSize];
            for (int slot = 0; slot < _cards.Length; slot++)
            {
                _cards[slot] = BuildCard(slot);
                cardRow.Add(_cards[slot].Button);
            }
        }

        // LateUpdate, so a choice opened by this frame's ticks shows on this frame.
        private void LateUpdate()
        {
            float now = Time.unscaledTime;

            if (_holding && now >= _holdEndsAt)
            {
                _holding = false;
                _battle.PickPerk(_pickedSlot);
            }

            // The enter look holds for one frame; removing it lets the base rules deal the cards and settle the title.
            // The lock counts from here, not from the show frame, whose layout and glyph work may be long (plan 0012, 5b-AC11).
            if (_entering)
            {
                _entering = false;
                _overlay.RemoveFromClassList(EnterClass);
                _lockEndsAt = now + lockSeconds;
            }

            bool open = _progression.ChoiceOpen;
            int opened = _progression.ChoicesOpened;
            if (open && opened != _shownChoices) ShowChoice(opened);
            else if (!open && _shown) Hide();

            if (_locked && now >= _lockEndsAt)
            {
                _locked = false;
                SetCardsPicking(PickingMode.Position);
            }
        }

        private void ShowChoice(int opened)
        {
            _shownChoices = opened;
            _title.text = string.Format(titleText, _progression.ChoiceLevel.ToString());
            int inARow = _progression.ChoicesInARow;
            _hint.text = inARow > 1
                ? string.Format(hintInARowText, _progression.ChoiceNumber.ToString(), inARow.ToString())
                : hintText;

            for (int slot = 0; slot < _cards.Length; slot++) WriteCard(_cards[slot], _progression.OfferAt(slot));

            _overlay.AddToClassList(ShownClass);
            _overlay.AddToClassList(EnterClass);
            _shown = true;
            _entering = true;
            _locked = true;
            // Until the next frame sets it, so the previous choice's end cannot open this lock early.
            _lockEndsAt = float.PositiveInfinity;
            SetCardsPicking(PickingMode.Ignore);
        }

        private void WriteCard(Card card, int perk)
        {
            card.Button.RemoveFromClassList(PickedClass);
            card.Button.RemoveFromClassList(PassedClass);

            card.Tile.text = _perkTileTexts[perk];
            TileKind tile = _perkTiles[perk];
            card.Tile.EnableInClassList(TileAllClass, tile == TileKind.All);
            card.Tile.EnableInClassList(TileGoldClass, tile == TileKind.Gold);
            if (tile == TileKind.Element)
            {
                card.Tile.style.backgroundColor = _perkTileColors[perk];
                card.Tile.style.color = _perkTileTextColors[perk];
            }
            else
            {
                card.Tile.style.backgroundColor = StyleKeyword.Null;
                card.Tile.style.color = StyleKeyword.Null;
            }

            card.Title.text = _perkTitles[perk];
            card.Number.text = _perkNumbers[perk];
            card.Attribute.text = _perkAttributes[perk];

            int taken = _progression.TimesTaken(perk);
            if (taken == 0)
            {
                card.Stack.text = string.Empty;
                return;
            }
            Modifier modifier = _progression.PerkAt(perk);
            card.Stack.text = string.Format(stackText, NumberText(modifier.Operation, modifier.Stacked(taken + 1)));
        }

        private void Hide()
        {
            _shown = false;
            _locked = false;
            _overlay.RemoveFromClassList(ShownClass);
        }

        /// <summary>Does nothing during the lock or a hold, whatever the cards' picking mode.</summary>
        private void OnCardClicked(int slot)
        {
            if (_locked || _holding || !_shown || !_progression.ChoiceOpen) return;

            for (int i = 0; i < _cards.Length; i++)
            {
                _cards[i].Button.EnableInClassList(PickedClass, i == slot);
                _cards[i].Button.EnableInClassList(PassedClass, i != slot);
            }
            // The hold takes no input, so no passed card lights up under the pointer.
            SetCardsPicking(PickingMode.Ignore);
            _pickedSlot = slot;
            _holding = true;
            _holdEndsAt = Time.unscaledTime + holdSeconds;
        }

        private void SetCardsPicking(PickingMode mode)
        {
            for (int i = 0; i < _cards.Length; i++) _cards[i].Button.pickingMode = mode;
        }

        private Card BuildCard(int slot)
        {
            var card = new Card
            {
                Button = new Button(() => OnCardClicked(slot)) { focusable = false, pickingMode = PickingMode.Ignore },
                Tile = NewLabel(TileClass),
                Title = NewLabel(CardTitleClass),
                Number = NewLabel(NumberClass),
                Attribute = NewLabel(AttributeClass),
                Stack = NewLabel(StackClass)
            };
            card.Button.AddToClassList(CardClass);
            card.Button.AddToClassList(SlotClassPrefix + slot.ToString(CultureInfo.InvariantCulture));
            card.Button.Add(card.Tile);
            card.Button.Add(card.Title);
            card.Button.Add(card.Number);
            card.Button.Add(card.Attribute);
            card.Button.Add(card.Stack);
            return card;
        }

        private static Label NewLabel(string className)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        /// <summary>The words, number and tile of every perk of the pool, once.</summary>
        private void BuildPerkWords(RunDefinition run)
        {
            int count = _progression.PerkCount;
            _perkTitles = new string[count];
            _perkNumbers = new string[count];
            _perkAttributes = new string[count];
            _perkTileTexts = new string[count];
            _perkTiles = new TileKind[count];
            _perkTileColors = new Color[count];
            _perkTileTextColors = new Color[count];

            for (int perk = 0; perk < count; perk++)
            {
                PerkDefinition definition = run.PerkAt(perk);
                Modifier modifier = _progression.PerkAt(perk);
                string elementName = string.Empty;

                if (!modifier.Element.IsNone)
                {
                    _perkTiles[perk] = TileKind.Element;
                    _perkTileTexts[perk] = string.Empty;
                    if (palette.TryGetLook(modifier.Element, out ElementPalette.Look look) && look.Element != null && look.Tower != null)
                    {
                        string displayName = look.Element.DisplayName;
                        elementName = string.Format(elementNameText, displayName, ColorUtility.ToHtmlStringRGB(look.UiColor));
                        // The element's initial is the first letter of its display name (plan 0012, reading 2).
                        if (displayName.Length > 0) _perkTileTexts[perk] = displayName.Substring(0, 1);
                        _perkTileColors[perk] = look.Tower.color;
                        _perkTileTextColors[perk] = look.TileTextColor;
                    }
                    else Debug.LogError($"LevelUpView: the palette has no look with a tower material for {definition.name}'s element.", this);
                }
                else if (Modifier.IsTowerStat(modifier.Stat))
                {
                    _perkTiles[perk] = TileKind.All;
                    _perkTileTexts[perk] = allTileText;
                }
                else
                {
                    _perkTiles[perk] = TileKind.Gold;
                    _perkTileTexts[perk] = goldTileText;
                }

                _perkTitles[perk] = string.Format(definition.Title, elementName);
                _perkNumbers[perk] = NumberText(modifier.Operation, modifier.Value);
                _perkAttributes[perk] = definition.AttributeText;
            }
        }

        /// <summary>A multiplication as the percent it adds; an addition as its amount.</summary>
        private string NumberText(Operation operation, float value) =>
            operation == Operation.Multiply
                ? string.Format(CultureInfo.InvariantCulture, percentText, StatText.Percent(value))
                : string.Format(CultureInfo.InvariantCulture, amountText, StatText.Shown(value));
    }
}
