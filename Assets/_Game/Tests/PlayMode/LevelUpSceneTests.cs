using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using ElementalTD.Core;
using ElementalTD.Data;
using ElementalTD.Gameplay;
using ElementalTD.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ElementalTD.PlayModeTests
{
    /// <summary>
    /// Step 5b on the scene, with the real pool and the document's seed: the level-up screen (5b-AC10, AC11), the driver and the input
    /// during a choice (5b-AC6), the seed (5b-AC7). A level is gained with AddXp and the choice opened by one hand tick that leaves it open;
    /// the lock and the hold are real time (the scene's 0.6 s and 0.35 s). A click is the event a button's handler listens to; who takes the
    /// pointer is asked of the panel with Pick. A test never calls Battle.PickPerk while the view holds a pick (it would answer the next choice
    /// of a row with its stale slot).
    /// </summary>
    public class LevelUpSceneTests : SceneTestBase
    {
        private const float LockSeconds = 0.6f, HoldSeconds = 0.35f;

        // The document's pool by place: tile text (element perks: the initial), the title's words after the element name, number, attribute.
        private static readonly (string tile, string title, string number, string attribute)[] Words =
        {
            ("L", " towers", "+25 %", "damage"),
            ("S", " towers", "+25 %", "damage"),
            ("All", "All towers", "+10 %", "fire rate"),
            ("All", "All towers", "+1", "range"),
            ("Gold", "Gold", "+3", "per kill"),
        };

        private VisualElement Overlay => Root.Q<VisualElement>("level-up");
        private Label Title => Root.Q<Label>("level-up-title");
        private Label Hint => Root.Q<Label>("level-up-hint");
        private VisualElement CardRow => Root.Q<VisualElement>("perk-cards");
        private List<Button> Cards => CardRow.Query<Button>().ToList();
        private bool OverlayShown => Overlay.resolvedStyle.display == DisplayStyle.Flex;
        private static Label Part(Button card, string cls) => card.Q<Label>(className: cls);

        /// <summary>Gains the XP of one or more levels, opens the choice by one hand tick and lets the first frame show it.</summary>
        private IEnumerator Show(int xp = 30)
        {
            LeaveChoicesOpen();
            Battle.Progression.AddXp(xp);
            Tick();
            Assert.IsTrue(Battle.AwaitingChoice, "the tick opened a choice");
            yield return null;
        }

        /// <summary>The frame after the show frame starts the lock; this waits the lock out in real time and gives the view the frame that ends it.</summary>
        private IEnumerator WaitOutTheLock()
        {
            yield return null;
            yield return new WaitForSecondsRealtime(LockSeconds + 0.15f);
            yield return null;
        }

        private IEnumerator WaitOutTheHold()
        {
            yield return new WaitForSecondsRealtime(HoldSeconds + 0.15f);
            yield return null;
        }

        private static string StackText(string total) => $"Total <b><color=#FFFFFF>{total}</color></b>";

        private string ExpectedTitle(int place)
        {
            Element element = Battle.Progression.PerkAt(place).Element;
            return element.IsNone ? Words[place].title : Markup(element) + Words[place].title;
        }

        // ---- Scene values the tests rely on

        [Test]
        public void S5b_AC11_The_Scene_Holds_The_Directions_Lock_And_Hold_And_The_Seed_Is_The_Reference_Seed()
        {
            var view = Object.FindAnyObjectByType<LevelUpView>();
            Assert.IsNotNull(view, "LevelUpView is on the scene");
            BindingFlags f = BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.AreEqual(LockSeconds, (float)typeof(LevelUpView).GetField("lockSeconds", f).GetValue(view), "1.7: the lock is 0.6 s");
            Assert.AreEqual(HoldSeconds, (float)typeof(LevelUpView).GetField("holdSeconds", f).GetValue(view), "1.7: the hold is 0.35 s");
            Assert.AreEqual(5, Battle.Progression.PerkCount);
            Assert.IsNull(PrototypeBootstrap.SeedForNextRun, "the seed was taken and cleared by the load (5b-AC7)");
        }

        // ---- 5b-AC10 The screen

        [UnityTest]
        public IEnumerator S5b_AC10_Hidden_Before_The_First_Level_And_Three_Cards_Are_Built_At_Start()
        {
            yield return Frames(2);
            Assert.AreEqual(DisplayStyle.None, Overlay.resolvedStyle.display);
            Assert.AreEqual(3, Cards.Count);
            foreach (Button card in Cards) Assert.IsFalse(card.focusable, "a card takes no focus: no key reaches it");
            Assert.IsEmpty(Title.text);
        }

        [UnityTest]
        public IEnumerator S5b_AC10_On_The_Frame_After_The_Tick_The_Overlay_Shows_The_Title_The_Hint_And_Three_Cards_In_Slot_Order()
        {
            yield return Frames(2);
            LeaveChoicesOpen();
            Battle.Progression.AddXp(30);
            Tick();
            Assert.AreEqual(DisplayStyle.None, Overlay.resolvedStyle.display, "nothing shows before a frame");
            yield return null;

            Assert.IsTrue(OverlayShown, "shown on the first frame after the tick");
            Assert.AreEqual("Level 2", Title.text);
            Assert.AreEqual("Choose one", Hint.text, "a choice alone: no (1 of 1)");
            Assert.IsTrue(Overlay.ClassListContains("level-up--enter"), "the enter look on the show frame");
            int[] offer = { 1, 0, 2 };   // the first draw of seed 4, pinned by the document
            for (int slot = 0; slot < 3; slot++) Assert.AreEqual(offer[slot], Battle.Progression.OfferAt(slot));

            List<Button> cards = Cards;
            for (int slot = 0; slot < 3; slot++)
            {
                int place = offer[slot];
                Button card = cards[slot];
                string where = $"slot {slot}, perk {place}";
                Assert.AreEqual(ExpectedTitle(place), Part(card, "perk-card__title").text, where + ": title");
                Assert.AreEqual(Words[place].number, Part(card, "perk-card__number").text, where + ": number");
                Assert.AreEqual(Words[place].attribute, Part(card, "perk-card__attribute").text, where + ": attribute");
                Assert.IsEmpty(Part(card, "perk-card__stack").text, where + ": a perk taken for the first time shows no stack line");
                Assert.IsFalse(card.ClassListContains("perk-card--picked") || card.ClassListContains("perk-card--passed"), where);
            }

            yield return null;
            Assert.IsFalse(Overlay.ClassListContains("level-up--enter"), "the enter look lasts exactly one frame");
            yield return null;
            Assert.IsFalse(Overlay.ClassListContains("level-up--enter"));
        }

        [UnityTest]
        public IEnumerator S5b_AC10_Each_Card_Shows_Its_Tile_In_The_Palettes_Colours_And_The_Directions_Text_Colours()
        {
            yield return Show();
            List<Button> cards = Cards;
            // Offer (1, 0, 2): Shadow damage, Light damage, fire rate (every tower).
            ElementPalette.Look shadow = LookOf(ElementOfTower(1)), light = LookOf(ElementOfTower(0));
            Label t0 = Part(cards[0], "perk-card__tile"), t1 = Part(cards[1], "perk-card__tile"), t2 = Part(cards[2], "perk-card__tile");

            Assert.AreEqual("S", t0.text);
            AssertColor(shadow.Tower.color, t0.resolvedStyle.backgroundColor, "Shadow tile");
            AssertColor(Hex("FFFFFF"), t0.resolvedStyle.color, "1.3: the Shadow initial is white (the direction's value, not only the palette's)");
            AssertColor(shadow.TileTextColor, t0.resolvedStyle.color, "and the palette's");
            Assert.AreEqual("L", t1.text);
            AssertColor(light.Tower.color, t1.resolvedStyle.backgroundColor, "Light tile");
            AssertColor(Hex("1A1918"), t1.resolvedStyle.color, "1.3: the Light initial is dark");
            AssertColor(light.TileTextColor, t1.resolvedStyle.color, "and the palette's");
            Assert.AreNotEqual(light.Tower.color, shadow.Tower.color);
            foreach (Label tile in new[] { t0, t1 }) Assert.IsFalse(tile.ClassListContains("perk-card__tile--all") || tile.ClassListContains("perk-card__tile--gold"));

            Assert.AreEqual("All", t2.text);
            Assert.IsTrue(t2.ClassListContains("perk-card__tile--all"));
            AssertColor(Hex("2E2C29"), t2.resolvedStyle.backgroundColor, "All tile");
            AssertColor(Hex("FFFFFF"), t2.resolvedStyle.color, "All text");
            Assert.AreEqual(16f, t2.resolvedStyle.fontSize);
            AssertColor(Hex("D9D4C8"), t2.resolvedStyle.borderTopColor, "tile border");
        }

        [UnityTest]
        public IEnumerator S5b_AC10_The_Gold_Tile_And_A_Slot_That_Changes_From_A_Gold_Or_All_Tile_To_An_Element_Tile_Leave_No_Stale_Class_Or_Colour()
        {
            // Offers of seed 4: (1,0,2) then (1,4,2) then (3,4,2): slot 1 goes element, gold, gold; slot 0 goes element, element, All.
            yield return Show(30 + 40 + 70);
            yield return WaitOutTheLock();
            ClickButton(Cards[0]);      // answers choice 1 with slot 0 (perk 1)
            yield return WaitOutTheHold();
            Assert.IsTrue(Battle.AwaitingChoice, "choice 2 of 3 is open");
            Label slot1 = Part(Cards[1], "perk-card__tile");
            Assert.AreEqual("Gold", slot1.text);
            Assert.IsTrue(slot1.ClassListContains("perk-card__tile--gold"));
            AssertColor(Hex("FFC23D"), slot1.resolvedStyle.borderTopColor, "the gold tile's border");
            AssertColor(Hex("FFC23D"), slot1.resolvedStyle.color, "and its text");
            Assert.AreEqual(14f, slot1.resolvedStyle.fontSize);

            yield return WaitOutTheLock();
            ClickButton(Cards[2]);      // answers choice 2 with slot 2 (perk 2)
            yield return WaitOutTheHold();
            Assert.IsTrue(Battle.AwaitingChoice, "choice 3 of 3 is open");
            // Slot 0 was an element tile (Shadow) and is now All (perk 3); slot 1 stays gold.
            Label slot0 = Part(Cards[0], "perk-card__tile");
            Assert.AreEqual("All", slot0.text);
            Assert.IsTrue(slot0.ClassListContains("perk-card__tile--all"));
            AssertColor(Hex("2E2C29"), slot0.resolvedStyle.backgroundColor, "no inline element colour is left on the All tile");
            AssertColor(Hex("FFFFFF"), slot0.resolvedStyle.color, "nor a dark text colour");
            Assert.IsTrue(Part(Cards[1], "perk-card__tile").ClassListContains("perk-card__tile--gold"));
            yield return WaitOutTheLock();
            ClickButton(Cards[0]);
            yield return WaitOutTheHold();
            Assert.IsFalse(Battle.AwaitingChoice);
        }

        [UnityTest]
        public IEnumerator S5b_AC10_Sizes_Colours_And_Places_Of_The_Directions_Section_1_7()
        {
            yield return Show();
            yield return WaitOutTheLock();
            AssertColor(new Color(20f / 255f, 19f / 255f, 17f / 255f, 0.7f), Overlay.resolvedStyle.backgroundColor, "overlay", 0.005f);
            Assert.AreEqual(Root.worldBound.size, Overlay.worldBound.size, "over the whole panel");
            Assert.AreEqual(40f, Title.resolvedStyle.fontSize);
            AssertColor(Hex("FFFFFF"), Title.resolvedStyle.color, "title");
            Assert.AreEqual(FontStyle.Bold, Title.resolvedStyle.unityFontStyleAndWeight);
            Assert.AreEqual(18f, Hint.resolvedStyle.fontSize);
            AssertColor(Hex("B5AFA3"), Hint.resolvedStyle.color, "hint");
            Assert.AreEqual(4f, Hint.resolvedStyle.marginTop, Snap, "4 below the title");

            List<Button> cards = Cards;
            for (int i = 0; i < 3; i++)
            {
                Button c = cards[i];
                Assert.AreEqual(220f, c.worldBound.width, Snap, $"card {i} width");
                Assert.AreEqual(240f, c.worldBound.height, Snap, $"card {i} height");
                Assert.AreEqual(16f, c.resolvedStyle.paddingTop, Snap);
                Assert.AreEqual(2f, c.resolvedStyle.borderTopWidth, Snap);
                Assert.AreEqual(6f, c.resolvedStyle.borderTopLeftRadius, Snap);
                AssertColor(Hex("45413B"), c.resolvedStyle.backgroundColor, $"card {i}", 0.005f);
                AssertColor(Hex("6B655A"), c.resolvedStyle.borderTopColor, $"card {i} border", 0.005f);
                Assert.AreEqual(1f, c.resolvedStyle.opacity, 0.01f, "dealt: opaque");
                Assert.AreEqual(0f, c.resolvedStyle.translate.y, 0.5f, "dealt: in place");
                Assert.AreEqual(48f, Part(c, "perk-card__tile").worldBound.width, Snap);
                Assert.AreEqual(48f, Part(c, "perk-card__tile").worldBound.height, Snap);
                Assert.AreEqual(2f, Part(c, "perk-card__tile").resolvedStyle.borderTopWidth, Snap);
                Assert.AreEqual(20f, Part(c, "perk-card__title").resolvedStyle.fontSize);
                Assert.AreEqual(36f, Part(c, "perk-card__number").resolvedStyle.fontSize);
                Assert.AreEqual(FontStyle.Bold, Part(c, "perk-card__number").resolvedStyle.unityFontStyleAndWeight);
                AssertColor(Hex("FFFFFF"), Part(c, "perk-card__number").resolvedStyle.color, "number");
                Assert.AreEqual(8f, Part(c, "perk-card__number").resolvedStyle.marginTop, Snap, "8 below the title");
                Assert.AreEqual(16f, Part(c, "perk-card__attribute").resolvedStyle.fontSize);
                AssertColor(Hex("D9D4C8"), Part(c, "perk-card__attribute").resolvedStyle.color, "attribute");
                Assert.AreEqual(14f, Part(c, "perk-card__stack").resolvedStyle.fontSize);
                Assert.AreEqual(18f, Part(c, "perk-card__stack").resolvedStyle.height, Snap);
            }
            Assert.AreEqual(24f, cards[1].worldBound.xMin - cards[0].worldBound.xMax, Snap, "24 apart");
            Assert.AreEqual(24f, cards[2].worldBound.xMin - cards[1].worldBound.xMax, Snap);
            Assert.AreEqual(708f, CardRow.worldBound.width, Snap, "the row is 708 wide");
            Assert.AreEqual(Overlay.worldBound.center.x, CardRow.worldBound.center.x, Snap, "centred");
            Assert.AreEqual(24f, CardRow.worldBound.yMin - Hint.worldBound.yMax, Snap, "24 below the hint");
            float top = Title.worldBound.yMin, bottom = CardRow.worldBound.yMax;
            Assert.AreEqual(Overlay.worldBound.center.y, (top + bottom) * 0.5f, 3f, "the block is centred both ways");
            Assert.AreEqual(338f, bottom - top, 8f, "the block is about 338 high");
            AssertLayout("the level-up screen shows");
        }

        [UnityTest]
        public IEnumerator S5b_AC10_The_Overlay_Lies_Above_The_Floating_Texts_And_Under_The_End_Screen_In_The_Document()
        {
            yield return Frames(1);
            VisualElement overlay = Overlay, floating = Root.Q<VisualElement>("floating-texts"), end = Root.Q<VisualElement>("end-screen");
            Assert.AreSame(overlay.parent, floating.parent);
            Assert.AreSame(overlay.parent, end.parent);
            int o = overlay.parent.IndexOf(overlay);
            Assert.Less(overlay.parent.IndexOf(floating), o, "after the floating texts");
            Assert.Less(o, overlay.parent.IndexOf(end), "before the end screen");
            Assert.AreEqual(floating.parent.childCount - 1 - 1, o, "directly under the end screen, the last but one");
        }

        [UnityTest]
        public IEnumerator S5b_AC10_Two_Levels_Fed_At_Once_Read_1_Of_2_Then_2_Of_2_Each_Titled_With_Its_Own_Level()
        {
            yield return Show(30 + 40);
            Assert.AreEqual("Level 2", Title.text);
            Assert.AreEqual("Choose one (1 of 2)", Hint.text);
            yield return WaitOutTheLock();
            ClickButton(Cards[0]);
            yield return WaitOutTheHold();
            Assert.IsTrue(OverlayShown, "the next choice deals in at once");
            Assert.AreEqual("Level 3", Title.text, "its own level, not the progression's (also 3) and not the first's");
            Assert.AreEqual("Choose one (2 of 2)", Hint.text);
            yield return WaitOutTheLock();
            ClickButton(Cards[0]);
            yield return WaitOutTheHold();
            Assert.IsFalse(Battle.AwaitingChoice);
            Assert.AreEqual(2, Battle.Modifiers.Count);
        }

        [UnityTest]
        public IEnumerator S5b_AC10_Three_Levels_At_Once_Read_Level_2_3_And_4_And_The_Title_Follows_The_Choice_Not_The_Progression()
        {
            yield return Show(30 + 40 + 70);
            Assert.AreEqual(4, Battle.Progression.Level, "the progression is already at level 4");
            var titles = new List<string> { Title.text };
            var hints = new List<string> { Hint.text };
            for (int i = 0; i < 3; i++)
            {
                yield return WaitOutTheLock();
                ClickButton(Cards[1]);
                yield return WaitOutTheHold();
                if (Battle.AwaitingChoice) { titles.Add(Title.text); hints.Add(Hint.text); }
            }
            CollectionAssert.AreEqual(new[] { "Level 2", "Level 3", "Level 4" }, titles);
            CollectionAssert.AreEqual(new[] { "Choose one (1 of 3)", "Choose one (2 of 3)", "Choose one (3 of 3)" }, hints);
        }

        // ---- 5b-AC11 Lock, pick and hold

        [UnityTest]
        public IEnumerator S5b_AC11_During_The_Lock_A_Click_On_A_Card_Does_Nothing_And_The_Cards_Ignore_Picking_Then_They_Take_It()
        {
            yield return Show();
            foreach (Button card in Cards) Assert.AreEqual(PickingMode.Ignore, card.pickingMode, "locked on the show frame");
            ClickButton(Cards[0]);
            Assert.IsTrue(Battle.AwaitingChoice);
            Assert.AreEqual(0, Cards.Count(c => c.ClassListContains("perk-card--picked")), "a click on the show frame picks nothing");

            yield return null;   // the dealing starts and the lock's time with it
            yield return new WaitForSecondsRealtime(LockSeconds * 0.7f);
            ClickButton(Cards[1]);
            foreach (Button card in Cards) Assert.AreEqual(PickingMode.Ignore, card.pickingMode, "still locked at 0.42 s");
            Assert.AreEqual(0, Cards.Count(c => c.ClassListContains("perk-card--picked")), "a click at 0.42 s picks nothing");
            yield return null;
            Assert.IsTrue(Battle.AwaitingChoice);

            yield return new WaitForSecondsRealtime(LockSeconds * 0.3f + 0.15f);
            yield return null;
            foreach (Button card in Cards) Assert.AreEqual(PickingMode.Position, card.pickingMode, "unlocked after 0.6 s");
        }

        [UnityTest]
        public IEnumerator S5b_AC11_A_Click_After_The_Lock_Picks_Holds_The_Look_Sends_The_Pick_Once_When_The_Hold_Ends_And_Hides_The_Overlay()
        {
            yield return Show();
            yield return WaitOutTheLock();
            ClickButton(Cards[0]);   // slot 0: perk 1 (Shadow damage)

            Assert.IsTrue(Cards[0].ClassListContains("perk-card--picked"));
            Assert.IsTrue(Cards[1].ClassListContains("perk-card--passed"));
            Assert.IsTrue(Cards[2].ClassListContains("perk-card--passed"));
            Assert.IsTrue(Battle.AwaitingChoice, "the choice stays open in Core during the hold");
            Assert.AreEqual(0, Battle.Modifiers.Count, "the pick is not sent on the click");
            foreach (Button card in Cards) Assert.AreEqual(PickingMode.Ignore, card.pickingMode, "no input during the hold");

            ClickButton(Cards[2]);   // a second click during the hold counts for nothing
            Assert.IsTrue(Cards[0].ClassListContains("perk-card--picked"), "the picked card did not change");
            Assert.IsFalse(Cards[2].ClassListContains("perk-card--picked"));
            yield return new WaitForSecondsRealtime(HoldSeconds * 0.5f);
            yield return null;
            Assert.IsTrue(Battle.AwaitingChoice, "still open halfway through the hold");
            Assert.AreEqual(0, Battle.Modifiers.Count);
            Assert.IsTrue(OverlayShown);

            yield return new WaitForSecondsRealtime(HoldSeconds * 0.5f + 0.15f);
            yield return null;
            Assert.IsFalse(Battle.AwaitingChoice, "the hold ended");
            Assert.AreEqual(1, Battle.Modifiers.Count, "exactly one pick");
            Assert.AreEqual(1, Battle.Modifiers.At(0).Source, "slot 0 of the offer (1, 0, 2) is perk 1: the modifier's source is the place, not the slot");
            Assert.AreEqual(Battle.Progression.TimesTaken(1), 1);
            Assert.IsFalse(Overlay.ClassListContains("level-up--shown"), "the overlay hides on the frame the last choice is answered");
            yield return Frames(30);
            Assert.AreEqual(1, Battle.Modifiers.Count, "never sent again");
            Assert.AreEqual(DisplayStyle.None, Overlay.resolvedStyle.display);
        }

        [UnityTest]
        public IEnumerator S5b_AC11_Clicking_The_Third_Card_Sends_Slot_2_Not_0_And_Not_The_Perks_Place()
        {
            yield return Show(30 + 40);   // offers (1,0,2) then (1,4,2)
            yield return WaitOutTheLock();
            ClickButton(Cards[2]);        // slot 2 = perk 2
            yield return WaitOutTheHold();
            Assert.AreEqual(2, Battle.Modifiers.At(0).Source);
            yield return WaitOutTheLock();
            ClickButton(Cards[1]);        // slot 1 of (1, 4, 2) = perk 4: neither the slot (1) nor 0 nor the first offer's perk
            yield return WaitOutTheHold();
            Assert.AreEqual(4, Battle.Modifiers.At(1).Source);
            Assert.AreEqual((Stat.GoldPerKill, 3f), (Battle.Modifiers.At(1).Stat, Battle.Modifiers.At(1).Value));
        }

        [UnityTest]
        public IEnumerator S5b_AC11_Five_In_A_Row_Each_Has_Its_Own_Offer_Hint_Title_Stack_Line_And_Full_Lock_And_The_Overlay_Never_Hides_Between_Them()
        {
            CultureInfo before = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("pt-PT");   // no number may follow the machine's culture
            try
            {
                yield return Show(30 + 40 + 70 + 90 + 110);
                int[][] offers = { new[] { 1, 0, 2 }, new[] { 1, 4, 2 }, new[] { 3, 4, 2 }, new[] { 4, 1, 2 }, new[] { 1, 3, 2 } };
                int[] slots = { 0, 0, 2, 1, 2 };
                // Perk 1 is taken at choices 1, 2 and 4 (its totals: +56 % on the second, +95 % on the third, +144 % offered on the fifth); perk 2 at 3 and 5 (+21 % offered on the fourth and the fifth).
                string[][] stacks =
                {
                    new[] { "", "", "" },
                    new[] { StackText("+56 %"), "", "" },
                    new[] { "", "", "" },
                    new[] { "", StackText("+95 %"), StackText("+21 %") },
                    new[] { StackText("+144 %"), "", StackText("+21 %") },
                };
                var sources = new List<int>();
                bool everyFrameShown = true;
                for (int choice = 0; choice < 5; choice++)
                {
                    string where = $"choice {choice + 1}";
                    Assert.IsTrue(Battle.AwaitingChoice, where);
                    Assert.AreEqual($"Level {choice + 2}", Title.text, where);
                    Assert.AreEqual($"Choose one ({choice + 1} of 5)", Hint.text, where);
                    for (int slot = 0; slot < 3; slot++)
                    {
                        int place = offers[choice][slot];
                        Assert.AreEqual(place, Battle.Progression.OfferAt(slot), $"{where}, slot {slot}: the offer");
                        Assert.AreEqual(Words[place].number, Part(Cards[slot], "perk-card__number").text, $"{where}, slot {slot}: the card shows this choice's offer");
                        Assert.AreEqual(stacks[choice][slot], Part(Cards[slot], "perk-card__stack").text, $"{where}, slot {slot}: the stack line");
                        AssertTile(Part(Cards[slot], "perk-card__tile"), place, $"{where}, slot {slot}");
                        Assert.IsFalse(Cards[slot].ClassListContains("perk-card--picked") || Cards[slot].ClassListContains("perk-card--passed"), $"{where}: no look kept from the last pick");
                    }
                    // A full lock of its own: cards ignore picking and a click does nothing on the show frame and the next one.
                    foreach (Button card in Cards) Assert.AreEqual(PickingMode.Ignore, card.pickingMode, $"{where}: locked");
                    ClickButton(Cards[0]);
                    Assert.AreEqual(0, Cards.Count(c => c.ClassListContains("perk-card--picked")), $"{where}: a click on the show frame does nothing");
                    yield return null;
                    foreach (Button card in Cards) Assert.AreEqual(PickingMode.Ignore, card.pickingMode, $"{where}: still locked on the frame the dealing starts");
                    ClickButton(Cards[0]);
                    Assert.AreEqual(0, Cards.Count(c => c.ClassListContains("perk-card--picked")), $"{where}: nor on the next");
                    yield return new WaitForSecondsRealtime(LockSeconds + 0.15f);
                    yield return null;
                    foreach (Button card in Cards) Assert.AreEqual(PickingMode.Position, card.pickingMode, $"{where}: unlocked");

                    int opened = Battle.Progression.ChoicesOpened;
                    ClickButton(Cards[slots[choice]]);
                    // Sampled on every frame until the hold has ended, so a frame with the overlay hidden would show.
                    // The loop ends on the frame the pick was sent (the next choice shows on that same frame), so the next choice's lock is
                    // measured from its start and a slow frame cannot eat it.
                    // A host that stalls for seconds moves real time without running frames: the wait ends only after 40 frames as well.
                    float end = Time.realtimeSinceStartup + HoldSeconds + 2f;
                    int waited = 0;
                    while (Battle.Modifiers.Count < choice + 1 && (Time.realtimeSinceStartup < end || waited < 40))
                    {
                        waited++;
                        yield return null;
                        if (choice < 4 && !Overlay.ClassListContains("level-up--shown")) everyFrameShown = false;
                    }
                    if (choice == 4) yield return null;
                    sources.Add(Battle.Modifiers.At(choice).Source);
                    Assert.AreEqual(choice + 1, Battle.Modifiers.Count, $"{where}: exactly one choice answered");
                    if (choice < 4)
                    {
                        Assert.IsTrue(Battle.AwaitingChoice, $"{where}: the next choice is open");
                        Assert.AreEqual(opened + 1, Battle.Progression.ChoicesOpened);
                    }
                }
                CollectionAssert.AreEqual(new[] { 1, 1, 2, 1, 2 }, sources, "the places of the perks taken");
                Assert.IsTrue(everyFrameShown, "the overlay never hid between two choices in a row");
                Assert.IsFalse(Battle.AwaitingChoice);
                Assert.AreEqual(3, Battle.Progression.TimesTaken(1));
                Assert.AreEqual(2, Battle.Progression.TimesTaken(2));
            }
            finally { CultureInfo.CurrentCulture = before; }
        }

        /// <summary>The tile of a perk by its place: an element tile with the palette's colours and no class, All or Gold with its own and no inline colour left.</summary>
        private void AssertTile(Label tile, int place, string where)
        {
            bool isAll = place == 2 || place == 3, isGold = place == 4;
            Assert.AreEqual(isAll, tile.ClassListContains("perk-card__tile--all"), where + ": the All class");
            Assert.AreEqual(isGold, tile.ClassListContains("perk-card__tile--gold"), where + ": the Gold class");
            Assert.AreEqual(Words[place].tile, tile.text, where + ": the tile's text");
            if (isAll || isGold) AssertColor(Hex("2E2C29"), tile.resolvedStyle.backgroundColor, where + ": no element colour left on a non-element tile");
            else
            {
                ElementPalette.Look look = LookOf(place == 0 ? ElementOfTower(0) : ElementOfTower(1));
                AssertColor(look.Tower.color, tile.resolvedStyle.backgroundColor, where + ": the element's colour");
                AssertColor(place == 0 ? Hex("1A1918") : Hex("FFFFFF"), tile.resolvedStyle.color, where + ": the initial's colour");
            }
            if (isGold) AssertColor(Hex("FFC23D"), tile.resolvedStyle.color, where + ": gold text");
        }

        [UnityTest]
        public IEnumerator S5b_AC11_With_Two_In_A_Row_The_Second_Stays_Open_Through_Another_Hold_Time_Because_The_First_Pick_Is_Sent_Once()
        {
            yield return Show(30 + 40);
            yield return WaitOutTheLock();
            ClickButton(Cards[0]);
            yield return WaitOutTheHold();
            Assert.AreEqual(1, Battle.Modifiers.Count);
            Assert.IsTrue(Battle.AwaitingChoice, "the second choice is open");
            yield return new WaitForSecondsRealtime(HoldSeconds + 0.4f);
            yield return null;
            Assert.AreEqual(1, Battle.Modifiers.Count, "a pick sent twice, or every frame, would have answered it");
            Assert.IsTrue(Battle.AwaitingChoice);
            Assert.IsTrue(OverlayShown);
        }

        [UnityTest]
        public IEnumerator S5b_AC11_The_Perk_Takes_Effect_On_The_Towers_When_The_Hold_Ends_Not_On_The_Click()
        {
            Battle.TryBuyTower(new GridPos(3, 3), _input.TowerAt(0).ToStats());
            Tower light = Board.TowerOn(new GridPos(3, 3));
            yield return Show();                     // offer (1, 0, 2): slot 1 is Light damage
            yield return WaitOutTheLock();
            ClickButton(Cards[1]);
            Assert.AreEqual(10f, light.Stats.Damage, "not on the click");
            yield return WaitOutTheHold();
            Assert.AreEqual(12.5f, light.Stats.Damage, 0f, "once the hold has ended");
        }

        [UnityTest]
        public IEnumerator S5b_AC11_The_Picked_And_Passed_Looks_Resolve_To_The_Directions_Values_And_Hold_For_The_Hold()
        {
            yield return Show();
            yield return WaitOutTheLock();
            ClickButton(Cards[1]);
            float clicked = Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(0.25f);
            yield return Frames(2);   // the 0.15 s fade advances with frames: a slow frame must not leave it half-way at the read
            Button picked = Cards[1];
            Assert.AreEqual(3f, picked.resolvedStyle.borderTopWidth, Snap, "picked: border 3");
            Assert.AreEqual(15f, picked.resolvedStyle.paddingTop, Snap, "picked: padding 15");
            AssertColor(Hex("FFFFFF"), picked.resolvedStyle.borderTopColor, "picked border", 0.005f);
            AssertColor(Hex("6B655A"), picked.resolvedStyle.backgroundColor, "picked background", 0.005f);
            Assert.AreEqual(1f, picked.resolvedStyle.opacity, 0.01f);
            foreach (int passed in new[] { 0, 2 }) Assert.AreEqual(0.35f, Cards[passed].resolvedStyle.opacity, 0.02f, $"card {passed} passed: 0.35");
            if (Time.realtimeSinceStartup - clicked < HoldSeconds - 0.05f) Assert.IsTrue(Battle.AwaitingChoice, "still held");
            yield return WaitOutTheHold();
        }

        [UnityTest]
        public IEnumerator S5b_AC11_The_Resolved_Transitions_Of_The_Base_Rules_The_Enter_Look_And_The_Picked_And_Passed_Classes()
        {
            yield return Show();
            // The show frame has the enter class: everything starts at once at its start look.
            Assert.IsTrue(Overlay.ClassListContains("level-up--enter"));
            Assert.AreEqual(0f, Cards[0].resolvedStyle.opacity, 0.01f, "enter: cards start transparent");
            Assert.AreEqual(24f, Cards[0].resolvedStyle.translate.y, 0.5f, "and 24 below");
            Assert.AreEqual(1.3f, Title.resolvedStyle.scale.value.x, 0.01f, "the title enters at 1.3");
            Assert.AreEqual(0f, Seconds(Title.resolvedStyle.transitionDuration), 1e-4f, "enter: no transition on the title");
            Assert.AreEqual(0f, Seconds(Cards[0].resolvedStyle.transitionDuration), 1e-4f, "enter: none on the cards");
            yield return null;

            Assert.AreEqual(0.25f, Seconds(Title.resolvedStyle.transitionDuration), 1e-4f, "the title eases over 0.25 s");
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(0.25f, Seconds(Cards[i].resolvedStyle.transitionDuration), 1e-4f, $"card {i}: 0.25 s");
                Assert.AreEqual(new[] { 0f, 0.1f, 0.2f }[i], Seconds(Cards[i].resolvedStyle.transitionDelay), 1e-4f, $"card {i}: the dealing delay");
                CollectionAssert.AreEquivalent(new[] { "opacity", "translate" }, Cards[i].resolvedStyle.transitionProperty.Select(p => p.ToString()).ToList(), $"card {i}: opacity and translate only");
            }
            CollectionAssert.AreEquivalent(new[] { "scale" }, Title.resolvedStyle.transitionProperty.Select(p => p.ToString()).ToList());

            yield return WaitOutTheLock();
            ClickButton(Cards[0]);
            yield return null;   // the classes are resolved on the next frame
            Assert.AreEqual(0f, Seconds(Cards[0].resolvedStyle.transitionDelay), 1e-4f, "picked: no delay");
            Assert.AreEqual(0.15f, Seconds(Cards[1].resolvedStyle.transitionDuration), 1e-4f, "passed: 0.15 s");
            Assert.AreEqual(0f, Seconds(Cards[2].resolvedStyle.transitionDelay), 1e-4f, "passed: no delay, also on slot 2 that carries 0.2 s at rest");
            yield return WaitOutTheHold();
        }

        private static float Seconds(IEnumerable<TimeValue> values)
        {
            List<TimeValue> list = values.ToList();
            Assert.GreaterOrEqual(list.Count, 1);
            Assert.AreEqual(TimeUnit.Second, list[0].unit);
            return list[0].value;
        }

        // ---- R2: the lock counts from the frame the cards start dealing

        /// <summary>Sleeps once in its LateUpdate, after every view's: a frame that takes 700 ms.</summary>
        [DefaultExecutionOrder(10000)]
        public sealed class SlowFrame : MonoBehaviour
        {
            public static bool Armed;

            private void LateUpdate()
            {
                if (!Armed) return;
                Armed = false;
                Thread.Sleep(700);
            }
        }

        [UnityTest]
        public IEnumerator S5b_AC11_A_Long_Show_Frame_Leaves_The_Cards_Locked_For_The_Full_Lock_After_They_Start_Dealing()
        {
            var host = new GameObject("SlowFrame", typeof(SlowFrame));
            try
            {
                LeaveChoicesOpen();
                Battle.Progression.AddXp(30);
                Tick();
                SlowFrame.Armed = true;
                yield return null;               // the show frame: it takes 0.7 s
                Assert.IsFalse(SlowFrame.Armed, "the long frame happened");
                Assert.IsTrue(OverlayShown);
                yield return null;               // the frame the dealing and the lock start on
                foreach (Button card in Cards) Assert.AreEqual(PickingMode.Ignore, card.pickingMode, "locked although 0.7 s passed since the show frame");
                ClickButton(Cards[0]);
                Assert.AreEqual(0, Cards.Count(c => c.ClassListContains("perk-card--picked")));
                yield return new WaitForSecondsRealtime(LockSeconds * 0.7f);
                yield return null;
                foreach (Button card in Cards) Assert.AreEqual(PickingMode.Ignore, card.pickingMode, "still locked 0.42 s after the dealing started");
                yield return new WaitForSecondsRealtime(LockSeconds * 0.3f + 0.15f);
                yield return null;
                foreach (Button card in Cards) Assert.AreEqual(PickingMode.Position, card.pickingMode, "unlocked after the full 0.6 s");
                ClickButton(Cards[0]);
                yield return WaitOutTheHold();
            }
            finally { Object.Destroy(host); SlowFrame.Armed = false; }
        }

        // ---- Who takes the pointer while the overlay shows

        [UnityTest]
        public IEnumerator S5b_AC11_While_The_Overlay_Shows_The_Pointer_At_Sell_Close_A_Buy_Button_And_A_Card_During_The_Lock_And_The_Hold_Lands_On_The_Overlay()
        {
            // A tower is selected: the panel shows Sell and the close button.
            Battle.TryBuyTower(new GridPos(3, 3), _input.TowerAt(0).ToStats());
            ClickCell(3, 3);
            yield return Frames(3);
            Assert.IsNotNull(_input.SelectedTower);
            IPanel panel = Root.panel;
            Vector2 sell = Root.Q<Button>("sell-button").worldBound.center, close = Root.Q<Button>("panel-close").worldBound.center;
            Assert.AreSame(Root.Q<Button>("sell-button"), panel.Pick(sell), "control: without the overlay Sell takes the pointer");

            yield return Show();
            foreach (Vector2 point in new[] { sell, close, Cards[1].worldBound.center })
                Assert.AreSame(Overlay, panel.Pick(point), "during the lock the overlay takes it");
            yield return WaitOutTheLock();
            Assert.AreSame(Overlay, panel.Pick(sell), "Sell");
            Assert.AreSame(Overlay, panel.Pick(close), "the close button");
            Assert.AreSame(Cards[1], panel.Pick(Cards[1].worldBound.center), "after the lock a card takes it");
            ClickButton(Cards[1]);
            Assert.AreSame(Overlay, panel.Pick(Cards[1].worldBound.center), "during the hold the card ignores it again: the overlay");
            yield return WaitOutTheHold();
            Assert.AreSame(Root.Q<Button>("sell-button"), panel.Pick(sell), "after the last choice Sell takes it again");
        }

        [UnityTest]
        public IEnumerator S5b_AC11_With_Nothing_Selected_A_Buy_Button_Is_Covered_By_The_Overlay()
        {
            yield return Frames(3);
            IPanel panel = Root.panel;
            Button[] buttons = TowerButtons();
            Assert.GreaterOrEqual(buttons.Length, 2);
            foreach (Button b in buttons) Assert.AreSame(b, panel.Pick(b.worldBound.center), "control: a buy button takes the pointer");
            yield return Show();
            yield return WaitOutTheLock();
            foreach (Button b in buttons) Assert.AreSame(Overlay, panel.Pick(b.worldBound.center), "covered while the overlay shows");
            ClickButton(Cards[0]);
            yield return WaitOutTheHold();
        }

        [UnityTest]
        public IEnumerator S5b_AC11_No_Key_Picks_And_No_Focused_Element_Takes_A_Tab_And_An_Enter_During_A_Choice()
        {
            _input.enabled = true;   // the keys are read by the input's Update
            yield return Show();
            yield return WaitOutTheLock();
            foreach (Key key in new[] { Key.Digit1, Key.Digit2, Key.Digit3, Key.Enter, Key.Space, Key.Escape, Key.Numpad1 })
                yield return PressKey(key);
            Assert.IsTrue(Battle.AwaitingChoice, "no key picked");
            Assert.AreEqual(0, Cards.Count(c => c.ClassListContains("perk-card--picked")));

            IPanel panel = Root.panel;
            using (KeyDownEvent tab = KeyDownEvent.GetPooled('\t', KeyCode.Tab, EventModifiers.None)) panel.visualTree.SendEvent(tab);
            using (KeyDownEvent enter = KeyDownEvent.GetPooled('\n', KeyCode.Return, EventModifiers.None)) panel.visualTree.SendEvent(enter);
            using (KeyUpEvent up = KeyUpEvent.GetPooled('\n', KeyCode.Return, EventModifiers.None)) panel.visualTree.SendEvent(up);
            yield return null;
            Assert.IsFalse(Cards.Contains(panel.focusController.focusedElement as Button), "a card is never the focused element");
            Assert.IsTrue(Battle.AwaitingChoice, "a Tab then an Enter pick nothing");
            Assert.AreEqual(0, Cards.Count(c => c.ClassListContains("perk-card--picked")));
            ClickButton(Cards[0]);
            yield return WaitOutTheHold();
        }

        // ---- 5b-AC6 Driver and input

        private Tower BuyAndSelect(GridPos cell, int index)
        {
            Assert.AreEqual(PlacementResult.Placed, Battle.TryBuyTower(cell, _input.TowerAt(index).ToStats()));
            ClickCell(cell.X, cell.Y);
            Tower tower = Board.TowerOn(cell);
            Assert.AreSame(tower, _input.SelectedTower);
            return tower;
        }

        [UnityTest]
        public IEnumerator S5b_AC6_On_The_Frame_The_Tick_Opens_The_Choice_Select_Deselect_SellSelected_And_The_Buttons_Do_Nothing_Before_Any_Frame_Passes()
        {
            Tower selected = BuyAndSelect(new GridPos(3, 3), 0);
            Battle.TryBuyTower(new GridPos(6, 6), _input.TowerAt(0).ToStats());
            yield return Frames(3);
            int gold = Battle.Gold, towers = Board.TowerCount, index = _input.SelectedIndex;

            LeaveChoicesOpen();
            Battle.Progression.AddXp(30);
            Tick();                       // the tick that opens the choice; no frame has run, so no overlay shows yet
            Assert.IsTrue(Battle.AwaitingChoice);
            Assert.AreEqual(DisplayStyle.None, Overlay.resolvedStyle.display, "the overlay cannot guard this frame");

            _input.Select(1);
            Assert.AreEqual(index, _input.SelectedIndex, "Select does nothing");
            Assert.AreSame(selected, _input.SelectedTower);
            _input.Deselect();
            Assert.AreSame(selected, _input.SelectedTower, "Deselect does nothing");
            Assert.IsFalse(_input.SellSelected(), "SellSelected answers false");
            ClickButton(Root.Q<Button>("sell-button"));
            ClickButton(Root.Q<Button>("panel-close"));
            Assert.AreSame(selected, _input.SelectedTower, "a click on Sell and on close changes no selection");
            Assert.AreEqual((gold, towers), (Battle.Gold, Board.TowerCount), "and sells nothing");
            Assert.IsTrue(Board.HasTower(new GridPos(3, 3)));

            yield return null;
            yield return WaitOutTheLock();
            ClickButton(Cards[0]);
            yield return WaitOutTheHold();
            Assert.AreSame(selected, _input.SelectedTower, "the selection made before is still there after the pick");
            Assert.IsTrue(_input.SellSelected(), "and Sell works again after it");
        }

        [UnityTest]
        public IEnumerator S5b_AC6_With_Nothing_Selected_A_Click_On_A_Buy_Button_On_The_Opening_Frame_Changes_The_Tower_To_Buy_Not()
        {
            yield return Frames(3);
            Button[] buttons = TowerButtons();
            Assert.GreaterOrEqual(buttons.Length, 2);
            Assert.AreEqual(0, _input.SelectedIndex);
            LeaveChoicesOpen();
            Battle.Progression.AddXp(30);
            Tick();
            ClickButton(buttons[1]);
            Assert.AreEqual(0, _input.SelectedIndex, "the tower to buy did not change");
            yield return null;
            yield return WaitOutTheLock();
            ClickButton(Cards[0]);
            yield return WaitOutTheHold();
            ClickButton(buttons[1]);
            Assert.AreEqual(1, _input.SelectedIndex, "control: the button works once the choice is answered");
        }

        [UnityTest]
        public IEnumerator S5b_AC6_During_A_Choice_Hover_Click_And_The_Keys_1_2_And_Escape_Change_Nothing_And_The_Selection_Stays()
        {
            Tower selected = BuyAndSelect(new GridPos(3, 3), 0);
            Battle.TryBuyTower(new GridPos(6, 6), _input.TowerAt(0).ToStats());
            _input.enabled = true;
            yield return Frames(3);
            int gold = Battle.Gold, towers = Board.TowerCount;
            yield return Show();

            _input.Hover(ScreenOf(new GridPos(9, 6)));
            Assert.IsFalse(_input.HasHoverCell, "no hover cell during a choice");
            _input.Click(ScreenOf(new GridPos(9, 6)));
            Assert.AreEqual((gold, towers), (Battle.Gold, Board.TowerCount), "a click on a free cell buys nothing");
            _input.Click(ScreenOf(new GridPos(6, 6)));
            Assert.AreSame(selected, _input.SelectedTower, "a click on another tower selects nothing");
            yield return PressKey(Key.Digit2);
            Assert.AreEqual(0, _input.SelectedIndex, "key 2 does nothing");
            yield return PressKey(Key.Escape);
            Assert.AreSame(selected, _input.SelectedTower, "Escape does nothing");
            yield return PressKey(Key.Digit1);
            Assert.AreSame(selected, _input.SelectedTower, "key 1 does nothing");

            yield return WaitOutTheLock();
            ClickButton(Cards[0]);
            yield return WaitOutTheHold();
            Assert.AreSame(selected, _input.SelectedTower, "the selection made before is still there after the pick");
            yield return PressKey(Key.Digit2);
            Assert.AreEqual(1, _input.SelectedIndex, "control: the keys work again after the pick");
        }

        [UnityTest]
        public IEnumerator S5b_AC6_With_The_Real_Driver_A_Choice_Open_For_A_Second_Moves_No_Tick_Gathers_No_Time_And_The_First_Frame_After_The_Pick_Runs_Only_Its_Own_Ticks()
        {
            yield return Frames(2);
            LeaveChoicesOpen();
            Battle.Progression.AddXp(30);
            Tick();
            yield return null;
            int ticks = Battle.TickCount, gold = Battle.Gold;
            float countdown = Battle.Waves.SecondsToNextWave;

            Time.timeScale = 1f;
            float end = Time.realtimeSinceStartup + 1.0f;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                Assert.AreEqual(ticks, Battle.TickCount, "the driver runs no tick during the choice");
                Assert.AreEqual(0f, _bootstrap.TickAlpha, "the tick fraction is 0");
            }
            Assert.AreEqual((gold, countdown), (Battle.Gold, Battle.Waves.SecondsToNextWave));

            // Not through the view: no hold is running, so a direct pick is allowed.
            Battle.PickPerk(0);
            yield return null;
            int ran = Battle.TickCount - ticks;
            int allowed = Mathf.CeilToInt(Time.deltaTime / Battle.TickSeconds) + 1;
            Assert.LessOrEqual(ran, allowed, $"the first frame after the pick ran {ran} ticks for a frame of {Time.deltaTime:F3} s: no burst");
            Time.timeScale = 0f;
        }

        [UnityTest]
        public IEnumerator S5b_AC6_A_Tick_Fraction_Left_Over_Is_Cleared_When_A_Hand_Tick_Opens_The_Choice()
        {
            yield return Frames(2);
            Time.timeScale = 1f;
            int guard = 0;
            while (!(_bootstrap.TickAlpha > 0.1f && _bootstrap.TickAlpha < 0.9f) && guard++ < 100) yield return null;
            Time.timeScale = 0f;
            Assert.Greater(_bootstrap.TickAlpha, 0.1f, "a fraction of a tick is left over");

            LeaveChoicesOpen();
            Battle.Progression.AddXp(30);
            Tick();
            yield return null;
            Assert.AreEqual(0f, _bootstrap.TickAlpha, "cleared on the first frame of the choice");
            yield return WaitOutTheLock();
            ClickButton(Cards[0]);
            yield return WaitOutTheHold();
        }

        // ---- 5b-AC7 The seed

        private IEnumerator Reload(uint? seed)
        {
            PrototypeBootstrap.SeedForNextRun = seed;
            var old = _bootstrap;
            _bootstrap.Restart();
            int guard = 0;
            while (old != null && guard++ < 120) yield return null;
            Assert.IsTrue(old == null, "the scene never reloaded");
            yield return null;
            AcquireScene(disableInput: true);
            yield return Frames(2);
            Assert.IsNull(PrototypeBootstrap.SeedForNextRun, "the static is cleared by the load");
        }

        private int[] FirstOffer()
        {
            LeaveChoicesOpen();
            Battle.Progression.AddXp(30);
            Tick();
            Assert.IsTrue(Battle.AwaitingChoice);
            return new[] { Battle.Progression.OfferAt(0), Battle.Progression.OfferAt(1), Battle.Progression.OfferAt(2) };
        }

        [UnityTest]
        public IEnumerator S5b_AC7_Two_Loads_On_One_Seed_Show_The_Same_First_Offer_The_Static_Is_Cleared_And_The_Reference_Seed_Gives_The_Documents()
        {
            CollectionAssert.AreEqual(new[] { 1, 0, 2 }, FirstOffer(), "seed 4: the document's first offer");
            yield return Reload(77);
            int[] a = FirstOffer();
            yield return Reload(77);
            int[] b = FirstOffer();
            CollectionAssert.AreEqual(a, b, "two loads on seed 77");
            yield return Reload(4);
            CollectionAssert.AreEqual(new[] { 1, 0, 2 }, FirstOffer());
        }

        [UnityTest]
        public IEnumerator S5b_AC7_A_Load_With_No_Seed_Starts_A_Run_From_The_Clock_And_A_Restart_Draws_From_A_Fresh_Seed()
        {
            var offers = new List<string> { string.Join(",", FirstOffer()) };
            for (int i = 0; i < 3; i++)
            {
                yield return Reload(null);
                Assert.IsNotNull(Battle);
                Assert.AreEqual(RunOutcome.Running, Battle.Outcome);
                Assert.AreEqual(0, Battle.TickCount);
                offers.Add(string.Join(",", FirstOffer()));
                yield return null;   // the new tick of the clock moves between loads
            }
            Assert.Greater(offers.Distinct().Count(), 1, "four runs, not one and the same first offer: " + string.Join(" | ", offers));
        }
    }
}
