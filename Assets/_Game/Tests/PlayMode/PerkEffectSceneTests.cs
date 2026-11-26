using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ElementalTD.Core;
using ElementalTD.Gameplay;
using ElementalTD.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ElementalTD.PlayModeTests
{
    /// <summary>
    /// Step 5b on the scene, after the picks: the panel's values and tails (5b-AC12), the discs and Sell's tail (5b-AC13), the level on the end screen
    /// (5b-AC14), the survived line across a pause (5b-AC15) and the views' cost (5b-AC16). Picks are answered by the Tick() helper from
    /// the perks the test names; the real seed's first seven offers (1,0,2) (1,4,2) (3,4,2) (4,1,2) (1,3,2) (2,1,0) (2,3,0) hold every perk named here.
    /// </summary>
    public class PerkEffectSceneTests : SceneTestBase
    {
        private static readonly int[] Table = { 30, 40, 70, 90, 110, 130, 150 };
        private static readonly GridPos A = new GridPos(1, 3);
        private static readonly GridPos B = new GridPos(1, 5);

        private int _levelsGained;

        [SetUp]
        public void ResetLevels() => _levelsGained = 0;

        private Label L(string name) => Root.Q<Label>(name);
        private bool TailShown(string name) => L(name).ClassListContains("stat-row__tail--shown");

        /// <summary>Gains the next level and opens its choice; the tick helper answers it with the perk named before.</summary>
        private void NextLevel()
        {
            Battle.Progression.AddXp(Table[_levelsGained++]);
            Tick();
        }

        private void BuyLightAndShadow()
        {
            Assert.AreEqual(PlacementResult.Placed, Battle.TryBuyTower(A, _input.TowerAt(0).ToStats()));
            Assert.AreEqual(PlacementResult.Placed, Battle.TryBuyTower(B, _input.TowerAt(1).ToStats()));
        }

        private void AssertRow(string value, string tail, string expectedValue, string expectedTail, string what)
        {
            Assert.AreEqual(expectedValue, L(value).text, what + ": value");
            if (expectedTail == null) Assert.IsFalse(TailShown(tail), what + ": no tail");
            else
            {
                Assert.IsTrue(TailShown(tail), what + ": a tail shows");
                Assert.AreEqual(expectedTail, L(tail).text, what + ": tail");
            }
        }

        // ---- 5b-AC12 The tail

        [UnityTest]
        public IEnumerator S5b_AC12_The_Panel_Reads_The_Effective_Values_With_The_Directions_Tails_After_Each_Pick_Under_A_Portuguese_Culture()
        {
            CultureInfo before = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("pt-PT");
            try
            {
                BuyLightAndShadow();
                ClickCell(A.X, A.Y);
                yield return Frames(3);
                AssertRow("stat-damage", "stat-damage-tail", "10", null, "before any pick");
                AssertRow("stat-shots", "stat-shots-tail", "1.5", null, "before any pick");
                AssertRow("stat-range", "stat-range-tail", "2.5", null, "before any pick");

                // The perks taken, in order: the choices' offers hold each (1,0,2) (1,4,2) (3,4,2) (4,1,2) (1,3,2) (2,1,0) (2,3,0).
                NamePicks(0, 2, 3, 2, 1, 0, 2);
                // After each pick: the Light tower's damage, shots and range rows, then the Shadow tower's damage row.
                var expected = new (string dmg, string dmgTail, string shots, string shotsTail, string range, string rangeTail, string shadow, string shadowTail)[]
                {
                    ("12.5", "+2.5", "1.5", null, "2.5", null, "10", null),
                    ("12.5", "+2.5", "1.65", "+0.15", "2.5", null, "10", null),
                    ("12.5", "+2.5", "1.65", "+0.15", "3.5", "+1", "10", null),
                    ("12.5", "+2.5", "1.82", "+0.32", "3.5", "+1", "10", null),
                    ("12.5", "+2.5", "1.82", "+0.32", "3.5", "+1", "12.5", "+2.5"),
                    ("15.6", "+5.6", "1.82", "+0.32", "3.5", "+1", "12.5", "+2.5"),
                    ("15.6", "+5.6", "2", "+0.5", "3.5", "+1", "12.5", "+2.5"),
                };
                for (int pick = 0; pick < expected.Length; pick++)
                {
                    var e = expected[pick];
                    string where = $"after pick {pick + 1}";
                    NextLevel();
                    Assert.AreEqual(pick == 0 ? "10" : expected[pick - 1].dmg, L("stat-damage").text, where + ": nothing is rewritten before a frame");
                    yield return null;
                    ClickCell(A.X, A.Y);
                    yield return null;
                    AssertRow("stat-damage", "stat-damage-tail", e.dmg, e.dmgTail, where + ", Light damage");
                    AssertRow("stat-shots", "stat-shots-tail", e.shots, e.shotsTail, where + ", Light shots");
                    AssertRow("stat-range", "stat-range-tail", e.range, e.rangeTail, where + ", Light range");
                    ClickCell(B.X, B.Y);
                    yield return null;
                    AssertRow("stat-damage", "stat-damage-tail", e.shadow, e.shadowTail, where + ", Shadow damage (a tail does not stay from the other tower)");
                    ClickCell(A.X, A.Y);
                    yield return null;
                }
            }
            finally { CultureInfo.CurrentCulture = before; }
        }

        [UnityTest]
        public IEnumerator S5b_AC13_An_Open_Panel_Is_Rewritten_On_The_Frame_After_A_Pick_Once_And_Builds_No_String_On_The_Next()
        {
            BuyLightAndShadow();
            ClickCell(A.X, A.Y);
            yield return Frames(3);
            Assert.AreEqual("10", L("stat-damage").text);
            NamePicks(0);
            NextLevel();
            Assert.AreEqual("10", L("stat-damage").text, "not before the frame");
            yield return null;
            Assert.AreEqual("12.5", L("stat-damage").text, "the first frame after the pick");
            Assert.AreEqual("+2.5", L("stat-damage-tail").text);

            var view = Object.FindAnyObjectByType<TowerPanelView>();
            System.Action frame = FrameMethod(view);
            view.enabled = false;
            frame();
            string text = L("stat-damage").text, tail = L("stat-damage-tail").text;
            Assert.AreEqual(0, Allocations.Of(frame), "the next frame builds no string: nothing changed");
            Assert.AreSame(text, L("stat-damage").text);
            Assert.AreSame(tail, L("stat-damage-tail").text);
            view.enabled = true;
        }

        [UnityTest]
        public IEnumerator S5b_AC13_Sells_Tail_Shows_The_Base_Cost_After_Every_Perk_And_The_Sale_Pays_From_The_Base_Cost()
        {
            BuyLightAndShadow();
            ClickCell(A.X, A.Y);
            yield return Frames(3);
            string tailBefore = L("sell-value-tail").text;
            StringAssert.Contains("40", tailBefore);
            NamePicks(0, 2, 3);
            NextLevel(); NextLevel(); NextLevel();
            yield return Frames(2);
            Assert.AreEqual(tailBefore, L("sell-value-tail").text, "the tail still names the cost 40 the tower was bought for");
            Assert.IsTrue(Battle.TryGetSellValue(A, out int value));
            Assert.AreEqual(40, value, "never fired: the full base cost");
            Assert.AreEqual("+40", L("sell-value").text);
        }

        // ---- 5b-AC13 The discs

        [UnityTest]
        public IEnumerator S5b_AC13_The_Disc_Of_The_Selected_Tower_The_Hovered_Tower_And_The_Tower_To_Buy_Grow_With_A_Range_Pick()
        {
            BuyLightAndShadow();
            Transform disc = FindByName("RangeDisc");
            Renderer discRenderer = disc.GetComponentInChildren<Renderer>();
            float cell = _map.CellSize;
            var free = new GridPos(8, 6);

            ClickCell(A.X, A.Y);
            yield return Frames(2);
            Assert.AreEqual(2.5f * cell, discRenderer.bounds.extents.x, 0.05f, "selected, before the pick");

            NamePicks(0, 2, 3);
            NextLevel(); NextLevel(); NextLevel();    // damage, rate, range +1
            yield return null;
            Assert.AreEqual(3.5f * cell, discRenderer.bounds.extents.x, 0.05f, "the selected tower's disc grows on the frame after a range pick");

            _input.Deselect();
            _input.Hover(ScreenOf(B));
            yield return null;
            Assert.IsTrue(disc.gameObject.activeSelf);
            Assert.AreEqual(3.5f * cell, discRenderer.bounds.extents.x, 0.05f, "a hovered tower's disc is its effective range");
            _input.Hover(ScreenOf(free));
            yield return null;
            Assert.AreEqual(3.5f * cell, discRenderer.bounds.extents.x, 0.05f, "the disc of the tower to buy, on a free cell, reads the range it would have");
            _input.Select(1);
            yield return null;
            Assert.AreEqual(3.5f * cell, discRenderer.bounds.extents.x, 0.05f, "also for the other tower to buy");
        }

        [UnityTest]
        public IEnumerator S5b_AC13_Under_The_Overlay_A_Selected_Tower_Keeps_Its_Frame_Panel_And_Disc_And_The_Hover_And_Its_Disc_Hide()
        {
            BuyLightAndShadow();
            ClickCell(A.X, A.Y);
            yield return Frames(3);
            Assert.IsTrue(FindByName("SelectionFrame").gameObject.activeSelf);

            LeaveChoicesOpen();
            Battle.Progression.AddXp(30);
            Tick();
            yield return Frames(3);
            Assert.AreSame(Board.TowerOn(A), _input.SelectedTower);
            Assert.IsTrue(FindByName("SelectionFrame").gameObject.activeSelf, "the frame stays");
            Assert.IsTrue(FindByName("RangeDisc").gameObject.activeSelf, "so does the disc of the selected tower");
            Assert.AreEqual(DisplayStyle.Flex, Root.Q<VisualElement>("tower-panel").resolvedStyle.display, "and the panel");

            _input.Deselect();
            _input.Hover(ScreenOf(B));
            Assert.IsFalse(_input.HasHoverCell, "no hover cell during a choice");
            yield return null;
            Assert.IsFalse(FindByName("Hover").gameObject.activeSelf, "the hover hides");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return null;
            var overlay = Root.Q<VisualElement>("level-up");
            var cards = overlay.Query<Button>().ToList();
            ClickButton(cards[0]);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return null;
            Assert.IsFalse(Battle.AwaitingChoice);
        }

        [UnityTest]
        public IEnumerator S5b_AC13_A_Kill_Pays_13_With_The_Gold_Perk_And_The_Reward_Alone_Before_It()
        {
            BuyLightAndShadow();
            Battle.Board.SpawnEnemy(new EnemyStats(0f, 1f, 10, default, 0));
            int gold = Battle.Gold;
            while (Battle.Gold == gold) Tick();
            Assert.AreEqual(10, Battle.Gold - gold, "no perk: the reward");

            NamePicks(0, 4);
            NextLevel(); NextLevel();
            yield return null;
            gold = Battle.Gold;
            Battle.Board.SpawnEnemy(new EnemyStats(0f, 1f, 10, default, 0));
            int guard = 0;
            while (Battle.Gold == gold && guard++ < 200) Tick();
            Assert.AreEqual(13, Battle.Gold - gold, "reward 10 and gold per kill +3");
        }

        // ---- 5b-AC14 The end screen

        [UnityTest]
        public IEnumerator S5b_AC14_A_Lost_Run_Shows_Its_Level_And_Counts_A_Level_Gained_On_The_Tick_That_Loses()
        {
            Label endLevel = Root.Q<Label>("end-level");
            bool gained = false;
            int guard = 0;
            while (Battle.Outcome == RunOutcome.Running && guard++ < 6000)
            {
                if (!gained && Battle.Lives == 1 && Board.EnemyCount > 0)
                {
                    for (int i = 0; i < Board.EnemyCount; i++)
                    {
                        if (Board.EnemyAt(i).DistanceToExit > 0.0334f) continue;
                        Battle.Progression.AddXp(30);   // the level comes on the tick that takes the last life
                        gained = true;
                        break;
                    }
                }
                Tick();
                if (guard % 100 == 0) yield return null;
            }
            Assert.AreEqual(RunOutcome.Lost, Battle.Outcome);
            Assert.IsTrue(gained, "the level was gained on the last tick");
            Assert.AreEqual(2, Battle.Progression.Level);
            Assert.IsFalse(Battle.AwaitingChoice, "a level on the tick that ends the run opens no choice");
            yield return Frames(2);
            Assert.AreEqual("Level 2", endLevel.text, "the end level counts the level gained on the last tick, not one behind");
            StringAssert.DoesNotContain("Max", endLevel.text);
            Assert.AreEqual(DisplayStyle.Flex, endLevel.resolvedStyle.display);
        }

        // ---- 5b-AC15 The survived line across a pause

        [UnityTest]
        public IEnumerator S5b_AC15_The_Survived_Line_Does_Not_Run_Out_While_A_Choice_Is_Open_Is_Seen_After_The_Pick_And_Hides_On_The_180th_Battle_Tick()
        {
            while (Battle.SurvivedWaves < 1) Tick();
            yield return null;
            Assert.IsTrue(MessageShown());
            int shownAt = Battle.TickCount;   // the frame that shows it: no tick has run since the paying tick
            Assert.AreEqual("Wave 1 survived +40", _label.text);

            LeaveChoicesOpen();
            Battle.Progression.AddXp(30);
            Tick();
            Assert.IsTrue(Battle.AwaitingChoice);
            yield return null;
            yield return new WaitForSecondsRealtime(3.6f);   // longer than its 3 s duration, in real time
            yield return null;
            Assert.IsTrue(MessageShown(), "the line does not run out while the choice is open: it counts battle ticks");
            var cards = Root.Q<VisualElement>("level-up").Query<Button>().ToList();
            ClickButton(cards[0]);
            yield return new WaitForSecondsRealtime(0.55f);
            yield return null;
            Assert.IsFalse(Battle.AwaitingChoice);
            Assert.IsTrue(MessageShown(), "and it is still seen after the pick: the pick does not restart or end it");

            int toGo = 179 - (Battle.TickCount - shownAt);
            Assert.GreaterOrEqual(toGo, 0);
            yield return TickFor(toGo);
            Assert.AreEqual(179, Battle.TickCount - shownAt);
            Assert.IsTrue(MessageShown(), "shown at 179 battle ticks");
            yield return TickFor(1);
            Assert.AreEqual(180, Battle.TickCount - shownAt);
            Assert.IsFalse(MessageShown(), "hidden at 180: 3 s of battle time, with >=");
        }

        // ---- 5b-AC16 Cost

        [UnityTest]
        public IEnumerator S5b_AC16_No_View_Allocates_Or_Moves_A_Layout_On_A_Quiet_Frame_With_The_Overlay_Closed_And_Open()
        {
            BuyLightAndShadow();
            ClickCell(A.X, A.Y);
            yield return Frames(3);
            var views = new MonoBehaviour[]
            {
                Object.FindAnyObjectByType<LevelUpView>(), Object.FindAnyObjectByType<TowerPanelView>(), Object.FindAnyObjectByType<BoardView>(),
                Object.FindAnyObjectByType<MessageView>(), Object.FindAnyObjectByType<RunHudView>(), Object.FindAnyObjectByType<XpBarView>(),
                Object.FindAnyObjectByType<DamageNumberView>(), Object.FindAnyObjectByType<RefusalView>(),
            };
            foreach (MonoBehaviour view in views) Assert.IsNotNull(view);
            var frames = views.Select(FrameMethod).ToArray();
            for (int i = 0; i < views.Length; i++) views[i].enabled = false;
            foreach (System.Action f in frames) f();   // warm-up of each code path
            for (int i = 0; i < views.Length; i++)
                Assert.AreEqual(0, Allocations.Of(frames[i]), $"{views[i].GetType().Name}, overlay closed");

            for (int i = 0; i < views.Length; i++) views[i].enabled = true;
            LeaveChoicesOpen();
            Battle.Progression.AddXp(30);
            Tick();
            yield return null;
            yield return new WaitForSecondsRealtime(0.75f);
            yield return Frames(2);
            Assert.AreEqual(PickingMode.Position, Root.Q<VisualElement>("level-up").Query<Button>().ToList()[0].pickingMode);

            var elements = Root.Q<VisualElement>("level-up").Query<VisualElement>().ToList();
            var layouts = elements.ConvertAll(e => e.layout);
            int geometryEvents = 0;
            foreach (VisualElement element in elements) element.RegisterCallback<GeometryChangedEvent>(_ => geometryEvents++);
            for (int i = 0; i < views.Length; i++) views[i].enabled = false;
            foreach (System.Action f in frames) f();
            for (int i = 0; i < views.Length; i++)
                Assert.AreEqual(0, Allocations.Of(frames[i]), $"{views[i].GetType().Name}, overlay open and quiet");
            for (int i = 0; i < views.Length; i++) views[i].enabled = true;
            yield return Frames(5);
            Assert.AreEqual(0, geometryEvents, "no element of the overlay changes geometry on a quiet frame");
            for (int i = 0; i < elements.Count; i++) Assert.AreEqual(layouts[i], elements[i].layout, $"layout of {elements[i].name}");

            var cards = Root.Q<VisualElement>("level-up").Query<Button>().ToList();
            ClickButton(cards[0]);
            yield return new WaitForSecondsRealtime(0.55f);
            yield return null;
            Assert.IsFalse(Battle.AwaitingChoice);
        }
    }
}
