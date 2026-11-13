using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ElementalTD.Core;
using ElementalTD.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ElementalTD.PlayModeTests
{
    /// <summary>
    /// Step 4c (plan 0008): selecting a tower, ending the selection, the frame and the disc, the side column, the panel, Sell and its
    /// live value with the drop. M1 to M21 name the wrong implementations of the tech-lead's list that a test below cannot pass.
    /// A real mouse click on a button needs a focused window, so a button is clicked with the event its handler listens to, and
    /// what a press on the board does is tested through Click at the button's centre and through Pick.
    /// </summary>
    public class SelectionSceneTests : SceneTestBase
    {
        private static readonly Color32 FullGold = new Color32(255, 194, 61, 255);
        private static readonly Color32 RefundGold = new Color32(201, 160, 74, 255);
        private static readonly Color32 DropRed = new Color32(255, 107, 94, 255);
        private static readonly Color32 RefundBorder = new Color32(107, 101, 90, 255);

        private static readonly GridPos A = new GridPos(1, 3);     // 1.41 cells from the spawn: in range of an enemy standing there
        private static readonly GridPos B = new GridPos(1, 5);     // also in range
        private static readonly GridPos Quiet = new GridPos(6, 6); // far from the spawn: never fires while the enemy stands still
        private static readonly GridPos Quiet2 = new GridPos(8, 1);
        private static readonly GridPos Free = new GridPos(4, 4);

        // ---- helpers

        private VisualElement E(string name)
        {
            VisualElement element = Root.Q<VisualElement>(name);
            Assert.IsNotNull(element, name);
            return element;
        }

        private Label L(string name) => (Label)E(name);
        private Button Btn(string name) => (Button)E(name);
        private bool Displayed(string name) => E(name).resolvedStyle.display == DisplayStyle.Flex;
        private bool SellHas(string cls) => E("sell-button").ClassListContains(cls);
        private string SellClasses() => string.Join(" ", E("sell-button").GetClasses());
        private int ShownNumber()
        {
            string text = L("sell-value").text;
            Assert.IsTrue(text.StartsWith("+"), $"the number reads '+n', not '{text}'");
            return int.Parse(text.Substring(1), CultureInfo.InvariantCulture);
        }

        private int QueryValue(GridPos cell)
        {
            Assert.IsTrue(Battle.TryGetSellValue(cell, out int value), $"a tower on {cell}");
            return value;
        }

        private Tower Buy(GridPos cell, int index = 0)
        {
            _input.Select(index);
            ClickCell(cell.X, cell.Y);
            Tower tower = Board.TowerOn(cell);
            Assert.IsNotNull(tower, $"bought on {cell}");
            return tower;
        }

        /// <summary>A tower of the asset's numbers at its own cost, bought through the battle (the starting gold buys only two real ones).</summary>
        private Tower BuyCheap(GridPos cell, int cost, int index = 0)
        {
            TowerStats real = _input.TowerAt(index).ToStats();
            Assert.AreEqual(PlacementResult.Placed, Battle.TryBuyTower(cell, new TowerStats(real.Range, real.Damage, real.ShotsPerSecond, real.ProjectileSpeed, cost, real.Element)));
            return Board.TowerOn(cell);
        }

        /// <summary>An enemy that stands next to the spawn for ever: every tower within its range launches on the next tick.</summary>
        private void Wake() => Board.SpawnEnemy(new EnemyStats(0f, 1_000_000f, 0, ElementOfTower(1)));

        private IEnumerator KeyWithInput(Key key)
        {
            _input.enabled = true;
            yield return PressKey(key);
            _input.enabled = false;
        }

        private static string Hud(string name)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "_Game", "Presentation", "UI", name));
            return File.ReadAllText(path);
        }

        private static void AssertSize(VisualElement element, float width, float height)
        {
            Assert.AreEqual(width, element.resolvedStyle.width, Snap, element.name + " width");
            Assert.AreEqual(height, element.resolvedStyle.height, Snap, element.name + " height");
        }

        private void AssertLook(Color32 number, Color32 border, float scale, string when)
        {
            Label value = L("sell-value");
            AssertColor(number, value.resolvedStyle.color, when + ": the number's colour");
            AssertColor(border, E("sell-button").resolvedStyle.borderTopColor, when + ": the border's colour");
            Assert.AreEqual(scale, value.resolvedStyle.scale.value.x, 0.005f, when + ": the number's scale");
        }

        private void AssertFullLook(int cost, string when)
        {
            Assert.AreEqual($"+{cost}", L("sell-value").text, when);
            Assert.AreEqual(DisplayStyle.None, L("sell-value-tail").resolvedStyle.display, when + ": no tail");
            Assert.IsFalse(SellHas("sell-button--refund"), when + ": not refund");
            Assert.IsFalse(SellHas("sell-button--pop") || SellHas("sell-button--settle"), when + ": no drop class");
            AssertLook(FullGold, FullGold, 1f, when);
        }

        private void AssertRefundLook(int value, int cost, string when)
        {
            Assert.AreEqual($"+{value}", L("sell-value").text, when);
            Assert.AreEqual($" of {cost}", L("sell-value-tail").text, when + ": the tail, behind a no-break space");
            Assert.AreEqual(DisplayStyle.Flex, L("sell-value-tail").resolvedStyle.display, when + ": the tail shows");
            Assert.IsTrue(SellHas("sell-button--refund"), when + ": refund");
            Assert.IsFalse(SellHas("sell-button--pop") || SellHas("sell-button--settle"), when + ": no drop class");
            AssertLook(RefundGold, RefundBorder, 1f, when);
        }

        // ---- 4c-AC2 selecting

        [UnityTest]
        public IEnumerator S4c_AC2_Nothing_Is_Selected_At_The_Start_A_Click_On_A_Tower_Selects_It_Another_Takes_It_The_Same_Keeps_It()
        {
            yield return null;
            Assert.IsNull(_input.SelectedTower, "none at the start of a run");
            Tower a = Buy(A), b = Buy(B);
            Assert.IsNull(_input.SelectedTower, "a purchase selects nothing");
            int gold = Battle.Gold, towers = Board.TowerCount;
            int refusals = 0;
            _input.PlacementRefused += (_, _) => refusals++;

            ClickCell(A.X, A.Y);
            Assert.AreSame(a, _input.SelectedTower);
            Assert.AreEqual((gold, towers), (Battle.Gold, Board.TowerCount), "a click on a tower neither sells nor buys");
            ClickCell(B.X, B.Y);
            Assert.AreSame(b, _input.SelectedTower, "another tower takes the selection");
            ClickCell(B.X, B.Y);
            Assert.AreSame(b, _input.SelectedTower, "the selected tower keeps it");
            ClickCell(A.X, A.Y);
            ClickCell(A.X, A.Y);
            Assert.AreSame(a, _input.SelectedTower);
            yield return null;
            Assert.AreEqual((gold, towers, 0), (Battle.Gold, Board.TowerCount, refusals));
            Assert.IsFalse(MessageShown());
            Assert.IsFalse(RefusalShown());
        }

        // ---- 4c-AC3 ending

        [UnityTest]
        public IEnumerator S4c_AC3_M7_M8_Every_Ending_Ends_The_Selection_Leaves_The_Tower_To_Buy_As_It_Was_And_The_Next_Click_Buys(
            [Values("close", "escape", "free", "wall", "spawn", "exit", "key1", "key2", "select0", "select1", "sale")] string how)
        {
            yield return null;
            Tower tower = Buy(A, 1);                       // index 1 chosen: 0 equals 0 would hide a reset
            Assert.AreEqual(1, _input.SelectedIndex);
            int refusals = 0;
            _input.PlacementRefused += (_, _) => refusals++;
            ClickCell(A.X, A.Y);
            Assert.AreSame(tower, _input.SelectedTower, "selected before the ending");
            yield return Frames(2);
            Assert.IsTrue(Displayed("tower-panel"));
            int gold = Battle.Gold, towers = Board.TowerCount;
            int expectedIndex = 1;

            switch (how)
            {
                case "close": ClickButton(Btn("panel-close")); break;
                case "escape": yield return KeyWithInput(Key.Escape); break;
                case "free": ClickCell(Free.X, Free.Y); break;
                case "wall": ClickCell(5, 0); break;
                case "spawn": ClickCell(_map.Spawn.X, _map.Spawn.Y); break;
                case "exit": ClickCell(_map.Exit.X, _map.Exit.Y); break;
                case "key1": expectedIndex = 0; yield return KeyWithInput(Key.Digit1); break;
                case "key2": yield return KeyWithInput(Key.Digit2); break;      // the one already chosen
                case "select0": expectedIndex = 0; _input.Select(0); break;
                case "select1": _input.Select(1); break;                         // M8: the index already chosen still ends it
                case "sale": ClickButton(Btn("sell-button")); break;
            }

            Assert.IsNull(_input.SelectedTower, how);
            Assert.AreEqual(expectedIndex, _input.SelectedIndex, $"{how}: the tower to buy");
            Assert.AreEqual(0, refusals, $"{how}: no refusal");
            if (how == "sale") Assert.AreEqual((gold + 40, towers - 1), (Battle.Gold, Board.TowerCount), "the sale paid the full cost");
            else Assert.AreEqual((gold, towers), (Battle.Gold, Board.TowerCount), $"{how}: nothing bought, nothing sold");
            _input.Hover(OffGrid);
            yield return Frames(2);
            Assert.IsFalse(MessageShown(), $"{how}: no message");
            Assert.IsFalse(RefusalShown(), $"{how}: no refusal");
            Assert.IsTrue(Displayed("tower-bar"), $"{how}: the buy buttons are back");
            Assert.IsFalse(Displayed("tower-panel"), $"{how}: the panel is gone");
            Assert.IsFalse(FindByName("SelectionFrame").gameObject.activeSelf, $"{how}: no frame");
            Assert.IsFalse(FindByName("RangeDisc").gameObject.activeSelf, $"{how}: no disc");
            Button[] buttons = TowerButtons();
            Assert.IsTrue(buttons[expectedIndex].ClassListContains("tower-button--selected"), $"{how}: the button of the tower to buy is marked");
            Assert.IsFalse(buttons[1 - expectedIndex].ClassListContains("tower-button--selected"));

            // The next click buys, with the tower to buy as it was.
            GridPos target = how == "sale" ? A : new GridPos(6, 6);
            int goldBefore = Battle.Gold;
            ClickCell(target.X, target.Y);
            Assert.IsNotNull(Board.TowerOn(target), $"{how}: the next click buys");
            Assert.AreEqual(ElementOfTower(expectedIndex), Board.TowerOn(target).Stats.Element, $"{how}: the tower to buy");
            Assert.AreEqual(goldBefore - 40, Battle.Gold);
            Assert.IsNull(_input.SelectedTower, $"{how}: a purchase selects nothing");
        }

        [UnityTest]
        public IEnumerator S4c_AC3_M7_Selecting_A_Light_Tower_With_Shadow_To_Buy_Keeps_Shadow_And_Escape_Keeps_It_Too()
        {
            Tower light = Buy(A, 0);
            _input.Select(1);
            ClickCell(A.X, A.Y);
            Assert.AreSame(light, _input.SelectedTower);
            Assert.AreEqual(1, _input.SelectedIndex, "selecting a Light tower keeps Shadow as the tower to buy");
            yield return KeyWithInput(Key.Escape);
            Assert.IsNull(_input.SelectedTower);
            Assert.AreEqual(1, _input.SelectedIndex, "so does Escape");
            yield return Frames(2);
            Assert.IsTrue(TowerButtons()[1].ClassListContains("tower-button--selected"));
        }

        [UnityTest]
        public IEnumerator S4c_AC3_A_Click_Off_The_Grid_A_Key_Outside_The_List_An_Index_Outside_And_A_Wave_Starting_Change_Nothing()
        {
            Tower tower = Buy(Quiet);
            ClickCell(Quiet.X, Quiet.Y);
            yield return Frames(2);
            int gold = Battle.Gold;

            _input.Click(OffGrid);
            Assert.AreSame(tower, _input.SelectedTower, "a click off the grid");
            _input.Click(ScreenCentreOf(E("side-column")));
            Assert.AreSame(tower, _input.SelectedTower, "a click on the column");
            yield return KeyWithInput(Key.Digit3);
            yield return KeyWithInput(Key.Digit9);
            yield return KeyWithInput(Key.Digit0);
            Assert.AreSame(tower, _input.SelectedTower, "keys 3, 9 and 0 with two towers in the list");
            _input.Select(2);
            _input.Select(-1);
            Assert.AreSame(tower, _input.SelectedTower, "an index outside the list");
            Assert.AreEqual(0, _input.SelectedIndex);

            yield return TickUntil(() => Battle.Waves.StartedWaves == 1, 1500, 100);
            yield return Frames(2);
            Assert.AreSame(tower, _input.SelectedTower, "a wave starting");
            Assert.IsTrue(Displayed("tower-panel"));
            Assert.AreEqual(gold, Battle.Gold);
        }

        [UnityTest]
        public IEnumerator S4c_AC3_M10_A_Tower_Sold_Behind_The_Selections_Back_Ends_It_And_A_New_Tower_On_Its_Cell_Is_Not_Selected()
        {
            Buy(A);
            ClickCell(A.X, A.Y);
            Assert.IsNotNull(_input.SelectedTower);
            Assert.IsTrue(Battle.TrySellTower(A));
            Assert.IsNull(_input.SelectedTower, "the sold tower is no longer selected");
            Assert.IsFalse(_input.SellSelected());

            int gold = Battle.Gold;
            ClickCell(Free.X, Free.Y);
            Assert.IsTrue(Board.HasTower(Free), "the next ground click buys at once");
            Assert.AreEqual(gold - 40, Battle.Gold);

            ClickCell(A.X, A.Y);
            Assert.IsTrue(Board.HasTower(A), "a new tower on the cell");
            Assert.IsNull(_input.SelectedTower, "is not selected");
            yield return Frames(2);
            Assert.IsFalse(Displayed("tower-panel"));
        }

        // ---- 4c-AC4 Sell and Upgrade

        [UnityTest]
        public IEnumerator S4c_AC4_Sell_Sells_The_Selected_Tower_Pays_Ends_The_Selection_And_Brings_The_Buy_Buttons_Back()
        {
            Tower tower = Buy(A);
            Buy(Quiet);
            int gold = Battle.Gold;
            ClickCell(A.X, A.Y);
            yield return Frames(2);
            Assert.AreEqual("+40", L("sell-value").text);

            ClickButton(Btn("sell-button"));
            Assert.AreEqual(gold + 40, Battle.Gold, "the sale paid the number shown");
            Assert.IsFalse(Board.HasTower(A));
            Assert.IsTrue(Board.HasTower(Quiet), "only the selected tower was sold");
            Assert.IsNull(_input.SelectedTower);
            yield return Frames(2);
            Assert.IsNull(GameObject.Find("Tower 1,3"), "its view is gone");
            Assert.IsTrue(Displayed("tower-bar"));
            Assert.IsFalse(Displayed("tower-panel"));
            Assert.IsFalse(FindByName("SelectionFrame").gameObject.activeSelf);
            Assert.AreEqual(tower.Cell, A);

            ClickButton(Btn("sell-button")); // the panel is closed and nothing is selected: nothing sells
            Assert.AreEqual(gold + 40, Battle.Gold);
            Assert.IsTrue(Board.HasTower(Quiet));
        }

        [UnityTest]
        public IEnumerator S4c_AC4_Nothing_Else_Sells_A_Click_On_A_Tower_A_Key_And_A_Click_On_Ground_Never_Sell()
        {
            Buy(A);
            Buy(B);
            int gold = Battle.Gold;
            foreach (GridPos cell in new[] { A, B, A, B, A })
                ClickCell(cell.X, cell.Y);
            ClickCell(Free.X, Free.Y);
            yield return KeyWithInput(Key.Escape);
            yield return KeyWithInput(Key.Digit1);
            ClickCell(A.X, A.Y);
            _input.Deselect();
            Assert.AreEqual(2, Board.TowerCount);
            Assert.AreEqual(gold, Battle.Gold);
        }

        [UnityTest]
        public IEnumerator S4c_AC4_M5_M19_A_Click_Off_The_Grid_At_Sell_Close_And_Upgrade_Keeps_The_Selection_And_Sell_Then_Still_Sells_Only_Sell_Close_And_The_Buttons_Take_Picking()
        {
            Tower tower = Buy(A);
            int gold = Battle.Gold;
            ClickCell(A.X, A.Y);
            yield return Frames(3);
            IPanel panel = Root.panel;

            foreach (string name in new[] { "sell-button", "panel-close", "upgrade-button" })
            {
                Vector2 centre = ScreenCentreOf(E(name));
                _input.Hover(centre);
                _input.Click(centre);
                Assert.AreSame(tower, _input.SelectedTower, $"a press on {name} reaches the board and must not end the selection");
                Assert.AreEqual((gold, 1), (Battle.Gold, Board.TowerCount), name);
            }

            // Pick, not a submit event: who takes the pointer.
            VisualElement upgrade = E("upgrade-button");
            Assert.AreSame(E("sell-button"), panel.Pick(E("sell-button").worldBound.center), "Sell takes the pointer");
            Assert.AreSame(E("panel-close"), panel.Pick(E("panel-close").worldBound.center), "the close button takes the pointer");
            VisualElement atUpgrade = panel.Pick(upgrade.worldBound.center);
            Assert.IsTrue(atUpgrade == null || (atUpgrade != upgrade && !upgrade.Contains(atUpgrade)), "Upgrade ignores picking");
            Assert.IsFalse(upgrade is Button, "Upgrade is no Button");
            Assert.AreEqual(PickingMode.Ignore, upgrade.pickingMode);
            foreach (VisualElement child in upgrade.Children()) Assert.AreEqual(PickingMode.Ignore, child.pickingMode, "its labels too");
            foreach (string name in new[] { "top-bar", "bottom-bar", "side-column", "tower-panel", "tower-stats", "synergies", "tower-actions" })
                Assert.AreNotSame(E(name), panel.Pick(E(name).worldBound.center), $"{name} takes no pointer");

            // Only the buy buttons, the close button, Sell and the end screen (with its button) take picking.
            var takers = new List<string>();
            Root.Query<VisualElement>().ForEach(e =>
            {
                if (e.pickingMode == PickingMode.Position && e != Root && e.parent != null) takers.Add(string.IsNullOrEmpty(e.name) ? e.GetType().Name + "." + string.Join(".", e.GetClasses()) : e.name);
            });
            foreach (string taker in takers)
            {
                bool allowed = taker == "panel-close" || taker == "sell-button" || taker == "end-screen" || taker == "restart-button" || taker.Contains("tower-button");
                Assert.IsTrue(allowed, $"'{taker}' takes picking");
            }
            Assert.Contains("panel-close", takers);
            Assert.Contains("sell-button", takers);

            ClickButton(Btn("sell-button"));
            Assert.AreEqual(gold + 40, Battle.Gold, "Sell still sells after those presses");
            Assert.IsFalse(Board.HasTower(A));
        }

        [UnityTest]
        public IEnumerator S4c_AC4_Upgrade_Takes_No_Click_And_Has_No_Hover_Look_And_Changes_Nothing()
        {
            Buy(A);
            int gold = Battle.Gold;
            ClickCell(A.X, A.Y);
            yield return Frames(2);
            VisualElement upgrade = E("upgrade-button");
            Color before = upgrade.resolvedStyle.backgroundColor;
            foreach (EventBase e in new EventBase[] { NavigationSubmitEvent.GetPooled(), ClickEvent.GetPooled() })
            {
                e.target = upgrade;
                upgrade.SendEvent(e);
                e.Dispose();
            }
            yield return Frames(2);
            Assert.IsNotNull(_input.SelectedTower);
            Assert.AreEqual((gold, 1), (Battle.Gold, Board.TowerCount));
            Assert.AreEqual(before, upgrade.resolvedStyle.backgroundColor);
            string uss = Hud("Hud.uss");
            StringAssert.DoesNotContain("upgrade-button:hover", uss);
            StringAssert.DoesNotContain("upgrade-button:active", uss);
            StringAssert.DoesNotContain("upgrade-button:focus", uss);
        }

        // ---- 4c-AC5 after the end

        [UnityTest]
        public IEnumerator S4c_AC5_M9_With_A_Tower_Selected_A_Defeat_Takes_The_Panel_The_Frame_And_The_Disc_On_The_Next_Frame()
        {
            ClickCell(0, 0); // far from the route: the run is lost
            Buy(new GridPos(1, 0));
            ClickCell(0, 0);
            Assert.IsNotNull(_input.SelectedTower);
            yield return Frames(2);
            Assert.IsTrue(Displayed("tower-panel"));
            Assert.IsTrue(FindByName("SelectionFrame").gameObject.activeSelf);

            int ticks = 0;
            while (true)
            {
                Battle.Tick();
                if (Battle.Outcome != RunOutcome.Running) break;
                Assert.IsNotNull(_input.SelectedTower, "selected while the run runs");
                if (++ticks % 50 == 0) yield return null;
                Assert.Less(ticks, 8000);
            }
            Assert.AreEqual(RunOutcome.Lost, Battle.Outcome);
            Assert.IsNull(_input.SelectedTower, "the same tick");
            yield return null;
            Assert.IsTrue(Displayed("tower-bar"), "the column shows the buy buttons, under the end screen");
            Assert.IsFalse(Displayed("tower-panel"));
            Assert.IsTrue(Displayed("end-screen"));
            Assert.IsFalse(FindByName("SelectionFrame").gameObject.activeSelf, "no frame");
            Assert.IsFalse(FindByName("RangeDisc").gameObject.activeSelf, $"no disc (hover cell {_input.HasHoverCell}, input enabled {_input.enabled}, selected {_input.SelectedTower != null})");

            int gold = Battle.Gold, towers = Board.TowerCount;
            Assert.IsFalse(_input.SellSelected(), "M9: nothing to sell");
            ClickCell(0, 0);
            Assert.IsNull(_input.SelectedTower, "a click on a tower selects nothing");
            Assert.IsFalse(_input.SellSelected());
            Assert.AreEqual((gold, towers), (Battle.Gold, Board.TowerCount));

            var old = _bootstrap;
            _bootstrap.Restart();
            int guard = 0;
            while (old != null && guard++ < 120) yield return null;
            Assert.IsTrue(old == null, "the scene never reloaded");
            yield return null;
            AcquireScene(disableInput: true);
            yield return Frames(3);
            Assert.IsNull(_input.SelectedTower, "after a restart nothing is selected");
            Assert.IsTrue(Displayed("tower-bar"));
            Assert.IsFalse(Displayed("tower-panel"));
            Assert.IsFalse(Displayed("end-screen"));
        }

        [UnityTest]
        public IEnumerator S4c_AC5_M9_With_A_Tower_Selected_A_Win_Ends_The_Selection_And_Sells_Nothing()
        {
            int ticks = 0;
            Tower selected = null;
            while (Battle.Outcome == RunOutcome.Running)
            {
                RunReferenceActionsAt(ticks);
                if (ticks == 9900) selected = SelectByClick(3, 1); // the last purchase of the order; the run wins 265 ticks later
                Battle.Tick();
                ticks++;
                Assert.Less(ticks, 12000, "the run never ended");
                if (Battle.Outcome == RunOutcome.Running)
                {
                    if (ticks > 9900) Assert.AreSame(selected, _input.SelectedTower, $"tick {ticks}: still selected");
                    if (ticks % 100 == 0) yield return null;
                }
            }
            Assert.AreEqual(RunOutcome.Won, Battle.Outcome);
            Assert.IsNotNull(selected);
            Assert.IsNull(_input.SelectedTower, "the tick of the win");
            int gold = Battle.Gold, towers = Board.TowerCount;
            Assert.IsFalse(_input.SellSelected());
            yield return null;
            Assert.IsFalse(Displayed("tower-panel"), "win: panel");
            Assert.IsTrue(Displayed("tower-bar"), "win: bar");
            Assert.IsFalse(FindByName("SelectionFrame").gameObject.activeSelf, "win: frame");
            Assert.IsFalse(FindByName("RangeDisc").gameObject.activeSelf, $"win: disc (hover cell {_input.HasHoverCell}, input enabled {_input.enabled})");
            ClickCell(3, 1);
            Assert.IsNull(_input.SelectedTower);
            Assert.AreEqual((gold, towers), (Battle.Gold, Board.TowerCount));
        }

        // ---- 4c-AC6 on the board

        private void AssertFrameAt(GridPos cell, string when)
        {
            Transform frame = FindByName("SelectionFrame");
            Assert.IsTrue(frame.gameObject.activeSelf, when + ": the frame is active");
            Vector3 centre = _map.CellToWorld(cell);
            float size = _map.CellSize;
            Renderer[] bars = frame.GetComponentsInChildren<Renderer>();
            Assert.AreEqual(4, bars.Length, "four bars");
            Bounds all = bars[0].bounds;
            foreach (Renderer bar in bars)
            {
                all.Encapsulate(bar.bounds);
                Assert.AreEqual(0.07f * size, Mathf.Min(bar.bounds.size.x, bar.bounds.size.z), 0.005f * size, when + ": a bar is 0.07 cells wide");
                Assert.AreEqual(0.03f, bar.bounds.center.y, 0.005f, when + ": at height 0.03");
                Assert.AreEqual(ShadowCastingMode.Off, bar.shadowCastingMode, "no shadow");
            }
            Assert.AreEqual(1.1f * size, all.size.x, 0.01f * size, when + ": 1.1 cells outer");
            Assert.AreEqual(1.1f * size, all.size.z, 0.01f * size, when + ": 1.1 cells outer");
            Assert.AreEqual(centre.x, all.center.x, 0.01f, when + ": on the cell's centre (x)");
            Assert.AreEqual(centre.z, all.center.z, 0.01f, when + ": on the cell's centre (z)");
        }

        private void AssertDisc(Tower tower, string when)
        {
            Transform disc = FindByName("RangeDisc");
            Assert.IsTrue(disc.gameObject.activeSelf, when + ": the disc is active");
            Renderer renderer = disc.GetComponentInChildren<Renderer>();
            Assert.AreSame(LookOf(tower.Stats.Element).RangeDisc, renderer.sharedMaterial, when + ": the tower's element");
            Assert.AreEqual(tower.Stats.Range * _map.CellSize, renderer.bounds.extents.x, 0.05f, when + ": the tower's range");
            Vector3 centre = _map.CellToWorld(tower.Cell);
            Assert.Less(Vector3.Distance(centre, new Vector3(disc.position.x, 0f, disc.position.z)), 0.01f, when + ": on the tower");
        }

        [UnityTest]
        public IEnumerator S4c_AC6_The_Frame_Has_The_Visual_Directions_Size_Height_And_Unlit_White_Material_And_The_Disc_Is_The_Selected_Towers_Wherever_The_Pointer_Is()
        {
            Tower light = Buy(A, 0);
            Tower shadow = Buy(B, 1);
            _input.Select(1); // Shadow to buy
            yield return null;
            Assert.IsFalse(FindByName("SelectionFrame").gameObject.activeSelf, "no frame while nothing is selected");

            ClickCell(A.X, A.Y);
            _input.Hover(ScreenOf(Free));
            yield return null;
            AssertFrameAt(A, "pointer on a free cell");
            AssertDisc(light, "pointer on a free cell with Shadow to buy");

            Material mat = FindByName("SelectionFrame").GetComponentInChildren<Renderer>().sharedMaterial;
            Assert.AreEqual(MaterialDir + "SelectionFrame.mat", PathOf(mat));
            StringAssert.Contains("Unlit", mat.shader.name);
            AssertColor(Color.white, mat.GetColor("_BaseColor"), "frame colour");
            if (mat.HasProperty("_Surface")) Assert.AreEqual(0f, mat.GetFloat("_Surface"), "opaque");
            Assert.Less(mat.renderQueue, 2500, "opaque queue");

            foreach (var (pointer, what) in new[]
            {
                (ScreenOf(B), "on another tower"), (OffGrid, "off the grid"), (ScreenCentreOf(E("side-column")), "over the column"),
                (ScreenCentreOf(E("top-bar")), "over the top bar"),
            })
            {
                _input.Hover(pointer);
                yield return null;
                AssertFrameAt(A, what);
                AssertDisc(light, what);
            }

            _input.Hover(ScreenOf(Free));
            yield return null;
            Assert.IsTrue(FindByName("Hover").gameObject.activeSelf, "the hover shows on the hovered cell");
            Assert.Less(Vector3.Distance(_map.CellToWorld(Free), new Vector3(FindByName("Hover").position.x, 0f, FindByName("Hover").position.z)), 0.01f);

            // After the selection ends the disc follows the hover again, with the tower to buy (4b-AC17).
            _input.Deselect();
            yield return null;
            Assert.IsFalse(FindByName("SelectionFrame").gameObject.activeSelf);
            Renderer disc = FindByName("RangeDisc").GetComponentInChildren<Renderer>();
            Assert.AreSame(LookOf(ElementOfTower(1)).RangeDisc, disc.sharedMaterial, "Shadow to buy");
            Assert.Less(Vector3.Distance(_map.CellToWorld(Free), FindByName("RangeDisc").position), 0.01f, "on the hovered cell");
            _input.Hover(OffGrid);
            yield return null;
            Assert.IsFalse(FindByName("RangeDisc").gameObject.activeSelf);
            Assert.IsNotNull(shadow);
        }

        [UnityTest]
        public IEnumerator S4c_AC6_M11_M13_Selecting_A_Second_Tower_Moves_The_Frame_And_The_Disc_And_The_Hover_Still_Follows_The_Pointer()
        {
            Tower a = Buy(A, 0), b = Buy(B, 1);
            ClickCell(A.X, A.Y);
            yield return null;
            AssertFrameAt(A, "first");
            ClickCell(B.X, B.Y); // directly, with no frame in between
            yield return null;
            AssertFrameAt(B, "second, selected directly");
            AssertDisc(b, "second");
            ClickCell(A.X, A.Y);
            yield return null;
            AssertFrameAt(A, "back");
            AssertDisc(a, "back");

            Transform hover = FindByName("Hover");
            foreach (GridPos cell in new[] { Free, Quiet, Quiet2, Free })
            {
                _input.Hover(ScreenOf(cell));
                yield return null;
                Assert.IsTrue(hover.gameObject.activeSelf, $"M13: the hover shows at {cell} while a tower is selected");
                Assert.Less(Vector3.Distance(_map.CellToWorld(cell), new Vector3(hover.position.x, 0f, hover.position.z)), 0.01f, $"M13: and follows to {cell}");
                AssertDisc(a, "the disc stays on the selected tower, not the hover");
            }
            _input.Hover(OffGrid);
            yield return null;
            Assert.IsFalse(hover.gameObject.activeSelf, "off the grid the hover goes");
            AssertFrameAt(A, "and the frame stays");
        }

        [UnityTest]
        public IEnumerator S4c_AC6_M12_Escape_With_The_Pointer_Over_The_Column_Or_Off_The_Grid_Takes_The_Frame_And_The_Disc_At_Once()
        {
            Buy(A);
            foreach (Vector2 pointer in new[] { ScreenCentreOf(E("side-column")), OffGrid })
            {
                ClickCell(A.X, A.Y);
                _input.Hover(pointer);
                yield return null;
                Assert.IsTrue(FindByName("SelectionFrame").gameObject.activeSelf);
                Assert.IsTrue(FindByName("RangeDisc").gameObject.activeSelf);
                yield return KeyWithInput(Key.Escape);
                Assert.IsNull(_input.SelectedTower);
                yield return null;
                Assert.IsFalse(_input.HasHoverCell, "no hover cell");
                Assert.IsFalse(FindByName("SelectionFrame").gameObject.activeSelf, "the frame is gone");
                Assert.IsFalse(FindByName("RangeDisc").gameObject.activeSelf, "the disc is gone");
                Assert.IsFalse(FindByName("Hover").gameObject.activeSelf, "no hover");
            }
        }

        // ---- 4c-AC7 the side column

        [UnityTest]
        public IEnumerator S4c_AC7_M20_The_Column_Holds_Exactly_One_Of_The_Buttons_And_The_Panel_And_The_Layout_Holds_At_Every_Aspect(
            [Values(0f, 1f, 2.5f)] float aspect)
        {
            if (aspect > 0f) _camera.aspect = aspect;
            Buy(A);
            yield return Frames(3);
            AssertLayout("nothing selected");
            Assert.IsTrue(Displayed("tower-bar"));
            Assert.IsFalse(Displayed("tower-panel"));
            Assert.IsFalse(E("side-column").ClassListContains("side-column--tower"));

            Vector3 position = _camera.transform.position;
            float size = _camera.orthographicSize;

            ClickCell(A.X, A.Y);
            yield return Frames(3);
            AssertLayout("a tower selected");
            Assert.IsFalse(Displayed("tower-bar"));
            Assert.IsTrue(Displayed("tower-panel"));
            Assert.AreEqual(position, _camera.transform.position, "M20: the camera does not move when the panel opens");
            Assert.AreEqual(size, _camera.orthographicSize, "M20: nor change its size");

            // The column's rectangle lies between the bars and misses the board's.
            Rect col = E("side-column").worldBound, top = E("top-bar").worldBound, bottom = E("bottom-bar").worldBound;
            Assert.AreEqual(216f, col.width, Snap);
            Assert.GreaterOrEqual(col.yMin, top.yMax - Snap);
            Assert.LessOrEqual(col.yMax, bottom.yMin + Snap);

            Wake();
            Battle.Tick();
            yield return Frames(3);
            AssertLayout("after the drop");
            Assert.AreEqual(position, _camera.transform.position, "M20: after the drop");
            Assert.AreEqual(size, _camera.orthographicSize, "M20: after the drop");

            _input.Deselect();
            yield return Frames(3);
            AssertLayout("closed");
            Assert.AreEqual(position, _camera.transform.position, "M20: after closing");
            Assert.AreEqual(size, _camera.orthographicSize, "M20: after closing");
            Assert.IsTrue(Displayed("tower-bar"));
            Assert.IsFalse(Displayed("tower-panel"));
        }

        // ---- 4c-AC8 the panel

        private void AssertPanelOf(Tower tower, string title)
        {
            ElementPalette.Look look = LookOf(tower.Stats.Element);
            Assert.AreEqual(title, L("tower-panel-title").text, "title");
            Assert.AreEqual(look.Element.DisplayName, L("stat-element").text, "element name");
            AssertColor(look.UiColor, L("stat-element").resolvedStyle.color, "the element label's colour");
            AssertColor(look.Tower.color, E("tower-panel-tile").resolvedStyle.backgroundColor, "the tile");
            AssertColor(Color.white, L("tower-panel-title").resolvedStyle.color, "title colour");
            string Format(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
            Assert.AreEqual(Format(tower.Stats.Damage), L("stat-damage").text, "damage");
            Assert.AreEqual(Format(tower.Stats.ShotsPerSecond), L("stat-shots").text, "shots per second");
            Assert.AreEqual(Format(tower.Stats.Range), L("stat-range").text, "range");
        }

        [UnityTest]
        public IEnumerator S4c_AC8_M21_The_Panel_Shows_The_Tower_Its_Element_Tile_And_Colour_And_Nothing_Is_Left_From_The_Previous_One()
        {
            Tower light = Buy(A, 0), shadow = Buy(B, 1);
            Renderer disc = FindByName("RangeDisc").GetComponentInChildren<Renderer>();
            foreach (var (tower, name, cell) in new[] { (light, "Light", A), (shadow, "Shadow", B), (light, "Light", A) })
            {
                ClickCell(cell.X, cell.Y);
                yield return Frames(2);
                AssertPanelOf(tower, $"{name} tower");
                AssertColor(Hex(name == "Light" ? "FFE27A" : "3B1F5C"), E("tower-panel-tile").resolvedStyle.backgroundColor, $"{name}: the tile is the tower material's colour");
                Assert.AreEqual(name, L("stat-element").text);
                Assert.AreSame(LookOf(tower.Stats.Element).RangeDisc, disc.sharedMaterial, $"{name}: the disc");
                Assert.AreNotEqual(LookOf(ElementOfTower(name == "Light" ? 1 : 0)).Tower.color, E("tower-panel-tile").resolvedStyle.backgroundColor, $"{name}: not the other tile");
            }
            // The numbers of the asset, as the direction writes them.
            Assert.AreEqual("10", L("stat-damage").text);
            Assert.AreEqual("1.5", L("stat-shots").text);
            Assert.AreEqual("2.5", L("stat-range").text);
            AssertColor(Hex("FFE27A"), L("stat-element").resolvedStyle.color, "Light's UI colour");
            ClickCell(B.X, B.Y);
            yield return Frames(2);
            AssertColor(Hex("B48CFF"), L("stat-element").resolvedStyle.color, "Shadow's UI colour");
        }

        [UnityTest]
        public IEnumerator S4c_AC8_The_Static_Texts_And_Every_Style_Of_The_Panel_Are_The_Visual_Directions()
        {
            Buy(A);
            ClickCell(A.X, A.Y);
            yield return Frames(3);

            var found = new List<Label>();
            Root.Query<Label>(className: "stat-row__caption").ForEach(found.Add);
            Assert.AreEqual(new[] { "Element", "Damage", "Shots/s", "Range" }, found.ConvertAll(l => l.text).ToArray());
            foreach (Label caption in found)
            {
                Assert.AreEqual(16f, caption.resolvedStyle.fontSize, "caption size");
                AssertColor(Hex("B5AFA3"), caption.resolvedStyle.color, "caption colour");
                Assert.AreEqual(FontStyle.Normal, caption.resolvedStyle.unityFontStyleAndWeight, "caption regular");
            }
            foreach (string name in new[] { "stat-element", "stat-damage", "stat-shots", "stat-range" })
            {
                Assert.AreEqual(16f, L(name).resolvedStyle.fontSize, name);
                Assert.AreEqual(FontStyle.Bold, L(name).resolvedStyle.unityFontStyleAndWeight, name);
                Assert.AreEqual(TextAnchor.MiddleRight, L(name).resolvedStyle.unityTextAlign, name);
                Assert.AreEqual(22f, L(name).parent.resolvedStyle.height, Snap, name + " row height");
            }
            AssertColor(Color.white, L("stat-damage").resolvedStyle.color, "value colour");

            Assert.AreEqual("Synergies", L("synergies-heading").text);
            Assert.AreEqual(14f, L("synergies-heading").resolvedStyle.fontSize);
            Assert.AreEqual(FontStyle.Bold, L("synergies-heading").resolvedStyle.unityFontStyleAndWeight);
            AssertColor(Hex("B5AFA3"), L("synergies-heading").resolvedStyle.color, "heading");
            Assert.AreEqual(18f, L("synergies-heading").resolvedStyle.height, Snap);
            Assert.AreEqual("None found yet", L("synergies-empty").text);
            Assert.AreEqual(14f, L("synergies-empty").resolvedStyle.fontSize);
            Assert.AreEqual(FontStyle.Normal, L("synergies-empty").resolvedStyle.unityFontStyleAndWeight);
            AssertColor(Hex("8A8478"), L("synergies-empty").resolvedStyle.color, "empty line");
            Assert.AreEqual(28f, L("synergies-empty").resolvedStyle.height, Snap);

            Assert.AreEqual(18f, L("tower-panel-title").resolvedStyle.fontSize);
            Assert.AreEqual(FontStyle.Bold, L("tower-panel-title").resolvedStyle.unityFontStyleAndWeight);
            VisualElement tile = E("tower-panel-tile");
            AssertSize(tile, 28f, 28f);
            Assert.AreEqual(8f, tile.resolvedStyle.marginRight, Snap, "8 to its right");
            AssertColor(Hex("D9D4C8"), tile.resolvedStyle.borderTopColor, "tile border");
            Button close = Btn("panel-close");
            Assert.AreEqual("×", close.text);
            AssertSize(close, 28f, 28f);
            Assert.AreEqual(24f, close.resolvedStyle.fontSize);
            Assert.AreEqual(FontStyle.Bold, close.resolvedStyle.unityFontStyleAndWeight);
            AssertColor(Hex("B5AFA3"), close.resolvedStyle.color, "close colour");
            Assert.AreEqual(0f, close.resolvedStyle.backgroundColor.a, "no background");
            Assert.AreEqual(0f, close.resolvedStyle.borderTopWidth, "no border");
            Assert.IsFalse(close.focusable);
            Assert.AreEqual(close.worldBound.xMax, E("tower-panel-header").worldBound.xMax, Snap, "at the row's right");

            VisualElement upgrade = E("upgrade-button"), sell = E("sell-button");
            AssertSize(upgrade, 80f, 56f);
            AssertColor(Hex("3A3733"), upgrade.resolvedStyle.backgroundColor, "upgrade background");
            AssertColor(Hex("4F4A43"), upgrade.resolvedStyle.borderTopColor, "upgrade border");
            Assert.AreEqual(2f, upgrade.resolvedStyle.borderTopWidth, Snap);
            Assert.AreEqual(6f, upgrade.resolvedStyle.borderTopLeftRadius, Snap);
            Label caption1 = upgrade.Q<Label>(className: "upgrade-button__caption"), note = upgrade.Q<Label>(className: "upgrade-button__note");
            Assert.AreEqual(("Upgrade", "Soon"), (caption1.text, note.text));
            Assert.AreEqual((16f, 14f), (caption1.resolvedStyle.fontSize, note.resolvedStyle.fontSize));
            Assert.AreEqual(FontStyle.Bold, caption1.resolvedStyle.unityFontStyleAndWeight);
            Assert.AreEqual(FontStyle.Normal, note.resolvedStyle.unityFontStyleAndWeight);
            AssertColor(Hex("8A8478"), caption1.resolvedStyle.color, "upgrade caption");
            AssertColor(Hex("8A8478"), note.resolvedStyle.color, "upgrade note");

            AssertSize(sell, 100f, 56f);
            Assert.AreEqual(12f, sell.worldBound.xMin - upgrade.worldBound.xMax, Snap, "Sell sits 12 to the right of Upgrade");
            AssertColor(Hex("45413B"), sell.resolvedStyle.backgroundColor, "sell background");
            Assert.AreEqual(6f, sell.resolvedStyle.borderTopLeftRadius, Snap);
            Assert.AreEqual(2f, sell.resolvedStyle.borderTopWidth, Snap);
            Assert.IsFalse(sell.focusable);
            Label sellCaption = sell.Q<Label>(className: "sell-button__caption");
            Assert.AreEqual("Sell", sellCaption.text);
            Assert.AreEqual(18f, sellCaption.resolvedStyle.fontSize);
            Assert.AreEqual(FontStyle.Bold, sellCaption.resolvedStyle.unityFontStyleAndWeight);
            AssertColor(Color.white, sellCaption.resolvedStyle.color, "sell caption");
            Assert.AreEqual(16f, L("sell-value").resolvedStyle.fontSize);
            Assert.AreEqual(FontStyle.Bold, L("sell-value").resolvedStyle.unityFontStyleAndWeight);
            Assert.AreEqual(14f, L("sell-value-tail").resolvedStyle.fontSize);
            Assert.AreEqual(FontStyle.Normal, L("sell-value-tail").resolvedStyle.unityFontStyleAndWeight);
            AssertColor(Hex("B5AFA3"), L("sell-value-tail").resolvedStyle.color, "tail colour");
            Assert.AreEqual(upgrade.worldBound.yMin, sell.worldBound.yMin, Snap, "side by side");
            // The actions sit at the bottom of the column.
            Assert.Greater(sell.worldBound.yMin, L("synergies-empty").worldBound.yMax - Snap);
            Assert.LessOrEqual(sell.worldBound.yMax, E("side-column").worldBound.yMax - 12f + Snap);
        }

        [UnityTest]
        public IEnumerator S4c_AC8_The_Numbers_Are_Written_With_A_Point_Whatever_The_Machines_Culture_Is()
        {
            CultureInfo saved = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo comma = null;
                foreach (string name in new[] { "pt-PT", "de-DE", "fr-FR" })
                {
                    CultureInfo candidate = new CultureInfo(name);
                    if (1.5f.ToString(candidate) == "1,5") { comma = candidate; break; }
                }
                Assert.IsNotNull(comma, "the test needs a culture with a decimal comma (1.5 reads 1,5)");
                CultureInfo.CurrentCulture = comma;
                Buy(A);
                ClickCell(A.X, A.Y);
                yield return Frames(2);
                Assert.AreEqual("1,5", 1.5f.ToString(), "the thread's culture writes a comma");
                Assert.AreEqual("1.5", L("stat-shots").text);
                Assert.AreEqual("2.5", L("stat-range").text);
                Assert.AreEqual("10", L("stat-damage").text);
                Assert.AreEqual("+40", L("sell-value").text);
            }
            finally
            {
                CultureInfo.CurrentCulture = saved;
            }
        }

        [UnityTest]
        public IEnumerator S4c_AC8_M18_The_Numbers_And_The_Tail_Come_From_The_Selected_Tower_Not_The_Buy_List_Or_The_Previous_Tower()
        {
            Element light = ElementOfTower(0);
            Assert.AreEqual(PlacementResult.Placed, Battle.TryBuyTower(A, new TowerStats(3.5f, 7f, 0.5f, 5f, 17, light)));
            Buy(B);
            ClickCell(A.X, A.Y);
            yield return Frames(2);
            Assert.AreEqual("Light tower", L("tower-panel-title").text);
            Assert.AreEqual("7", L("stat-damage").text);
            Assert.AreEqual("0.5", L("stat-shots").text);
            Assert.AreEqual("3.5", L("stat-range").text);
            Assert.AreEqual("+17", L("sell-value").text);
            Assert.AreEqual(DisplayStyle.None, L("sell-value-tail").resolvedStyle.display);

            Wake();
            Battle.Tick();
            yield return null;
            Assert.AreEqual("+8", L("sell-value").text, "17 at 50 percent, rounded down");
            Assert.AreEqual(" of 17", L("sell-value-tail").text, "the tail names this tower's cost");

            ClickCell(B.X, B.Y); // a normal tower, directly
            yield return Frames(2);
            Assert.AreEqual("10", L("stat-damage").text);
            Assert.AreEqual("1.5", L("stat-shots").text);
            Assert.AreEqual("2.5", L("stat-range").text);
            Assert.AreEqual("+20", L("sell-value").text, "B fired too: 40 at 50 percent");
            Assert.AreEqual(" of 40", L("sell-value-tail").text, "the previous tower's tail is gone");

            ClickCell(A.X, A.Y);
            yield return Frames(2);
            Assert.AreEqual("0.5", L("stat-shots").text, "and back");
            Assert.AreEqual(" of 17", L("sell-value-tail").text);
        }

        // ---- 4c-AC9 the live value and the drop

        [UnityTest]
        public IEnumerator S4c_AC9_Eduardos_Case_The_Pointer_Rests_On_Sell_And_The_Price_Drops_And_The_Click_Pays_What_Is_Shown()
        {
            Tower tower = Buy(A);
            int cost = tower.Stats.Cost;
            ClickCell(A.X, A.Y);
            Assert.AreSame(tower, _input.SelectedTower);
            _input.enabled = true; // the real pointer drives Hover from here on
            yield return Frames(3);
            Button sell = Btn("sell-button");
            Vector2 centre = ScreenCentreOf(sell);
            yield return MovePointer(centre);   // once, and never again
            yield return Frames(2);
            Debug.Log($"4c Eduardo: the pointer was put at {centre}; the mouse reads {Mouse.current.position.ReadValue()}; hover cell {_input.HasHoverCell}");
            Assert.IsFalse(_input.HasHoverCell, "the pointer is over the column, not a cell");
            AssertFullLook(cost, "before the first launch");

            var log = new List<string>();
            int frame = 0, dropFrame = -1;
            while (true)
            {
                Battle.Tick();
                bool lowered = Battle.TryGetSellValue(A, out int after) && after < cost;
                yield return null;
                frame++;
                int expected = QueryValue(A);
                int shown = ShownNumber();
                Assert.AreEqual(expected, shown, $"frame {frame}: the number equals what Core answers");
                if (lowered) { dropFrame = frame; break; }
                Assert.AreEqual($"+{cost}", L("sell-value").text, $"frame {frame}");
                Assert.IsFalse(SellHas("sell-button--refund") || SellHas("sell-button--pop") || SellHas("sell-button--settle"), $"frame {frame}: the full look has no class");
                Assert.IsFalse(_input.HasHoverCell, $"frame {frame}: the pointer never left Sell");
                if (frame % 200 == 1) AssertFullLook(cost, $"frame {frame}");
                Assert.Less(frame, 4000, "the tower never fired");
            }
            float tPop = Time.realtimeSinceStartup;
            Assert.Greater(Battle.ProjectileCount, 0, "the tick that lowered the value launched a shot");

            // The first frame after the tick that lowers the value.
            Assert.AreEqual("+20", L("sell-value").text, "the refund text (the asset's 20)");
            Assert.AreEqual(" of 40", L("sell-value-tail").text);
            Assert.AreEqual(DisplayStyle.Flex, L("sell-value-tail").resolvedStyle.display);
            Assert.IsTrue(SellHas("sell-button--refund") && SellHas("sell-button--pop"), $"classes: {SellClasses()}");
            Assert.IsFalse(SellHas("sell-button--settle"));
            AssertLook(DropRed, DropRed, 1.4f, "the first frame: the drop's colour and scale");
            log.Add($"drop frame {dropFrame}: '{L("sell-value").text}{L("sell-value-tail").text}' [{SellClasses()}]");

            // The next frame: the pop gives way to the settle.
            Battle.Tick();
            yield return null;
            frame++;
            Assert.AreEqual(20, ShownNumber());
            Assert.IsFalse(SellHas("sell-button--pop"), $"classes: {SellClasses()}");
            Assert.IsTrue(SellHas("sell-button--settle") && SellHas("sell-button--refund"), $"classes: {SellClasses()}");
            float tSettle = Time.realtimeSinceStartup;
            log.Add($"settle frame {frame}: [{SellClasses()}] scale {L("sell-value").resolvedStyle.scale.value.x:F3}");

            // M17: it eases. One sample before 0.25 s: the scale is between 1 and 1.4, the colour is still the pop's.
            bool sampled = false, sampledMid = false;
            var trace = new List<string>();
            string lastHud = null;
            while (Time.realtimeSinceStartup - tSettle < 0.95f)
            {
                Battle.Tick();
                yield return new WaitForSecondsRealtime(1f / 60f); // a batch frame takes 0.4 ms: pace the run like a 60 Hz game, or 0.95 s plays a minute of waves
                frame++;
                Assert.AreEqual(QueryValue(A), ShownNumber(), $"frame {frame}");
                Assert.IsFalse(SellHas("sell-button--pop"), $"frame {frame}: no pop again");
                float t = Time.realtimeSinceStartup - tSettle;
                float scale = L("sell-value").resolvedStyle.scale.value.x;
                string hud = $"{L("wave-label").text}|{L("next-wave-label").text}|{L("lives-label").text}|{GoldLabel.text}|{_label.text}|{string.Join(" ", _label.GetClasses())}|enemies {Board.EnemyCount}|towerFired {Battle.ProjectileCount}";
                if (hud != lastHud) { trace.Add($"t={t:F3} HUD CHANGED: {hud}"); lastHud = hud; }
                trace.Add($"t={t:F3} scale {scale:F3} colour {(Color32)L("sell-value").resolvedStyle.color} border {(Color32)E("sell-button").resolvedStyle.borderTopColor}");
                if (!sampled && t >= 0.08f && t < 0.2f)
                {
                    sampled = true;
                    Assert.Greater(scale, 1.005f, $"t={t:F3}: not yet at 1");
                    Assert.Less(scale, 1.395f, $"t={t:F3}: already moving");
                    AssertColor(DropRed, L("sell-value").resolvedStyle.color, $"t={t:F3}: the number's colour holds for 0.25 s");
                    AssertColor(DropRed, E("sell-button").resolvedStyle.borderTopColor, $"t={t:F3}: the border's colour holds");
                    log.Add($"t={t:F3}: scale {scale:F3}, colour {(Color32)L("sell-value").resolvedStyle.color}");
                }
                if (!sampledMid && t >= 0.45f && t < 0.6f)
                {
                    sampledMid = true;
                    Color mid = L("sell-value").resolvedStyle.color;
                    if (Vector4.Distance(mid, (Color)RefundGold) < 0.01f) Debug.Log("4c Eduardo (mid sample failed): every frame of the settle\n" + string.Join("\n", trace));
                    Assert.AreNotEqual((Color)DropRed, mid, $"t={t:F3}: the colour has left the pop's");
                    Assert.AreNotEqual((Color)RefundGold, mid, $"t={t:F3}: and is not at the refund's yet");
                    Assert.AreEqual(1f, scale, 0.005f, $"t={t:F3}: the scale has settled");
                    log.Add($"t={t:F3}: colour {(Color32)mid}");
                }
            }
            Debug.Log("4c Eduardo: every frame of the settle\n" + string.Join("\n", trace));
            Assert.IsTrue(sampled, "a frame fell in the 0.08 to 0.2 s window");
            AssertLook(RefundGold, RefundBorder, 1f, $"{Time.realtimeSinceStartup - tPop:F2} s after the drop");
            log.Add($"resolved after {Time.realtimeSinceStartup - tPop:F2} s: refund look");
            Assert.AreEqual(1, new List<string>(SellClasses().Split(' ')).FindAll(c => c == "sell-button--settle").Count);
            Debug.Log("4c Eduardo: frames and values\n" + string.Join("\n", log) + $"\nframes run: {frame}");

            // A click on Sell pays what is shown, not the cost.
            int shownLast = ShownNumber();
            int gold = Battle.Gold;
            ClickButton(sell);
            Assert.AreEqual(20, shownLast);
            Assert.AreEqual(gold + shownLast, Battle.Gold, "the gold rose by the number shown");
            Assert.AreNotEqual(gold + cost, Battle.Gold, "not by the cost");
            Assert.IsFalse(Board.HasTower(A), "the tower is gone");
            Assert.IsNull(_input.SelectedTower);
            yield return Frames(2);
            Assert.IsTrue(Displayed("tower-bar"), "the buy buttons are back");
            Assert.IsFalse(Displayed("tower-panel"));
        }

        [UnityTest]
        public IEnumerator S4c_AC9_The_Same_Drop_Plays_With_The_Pointer_Off_The_UI_And_A_Frame_Without_A_Change_Writes_Nothing()
        {
            Buy(A);
            ClickCell(A.X, A.Y);
            _input.Hover(OffGrid);
            yield return Frames(3);
            AssertFullLook(40, "before");

            // No frame without a change writes a class or a text (the order of the classes is a snapshot of every write).
            string classes = SellClasses();
            string text = L("sell-value").text + "|" + L("sell-value-tail").text + "|" + L("tower-panel-title").text;
            for (int i = 0; i < 10; i++)
            {
                Battle.Tick(); // nothing in range: no launch
                yield return null;
                Assert.AreEqual(classes, SellClasses(), $"frame {i}");
                Assert.AreEqual(text, L("sell-value").text + "|" + L("sell-value-tail").text + "|" + L("tower-panel-title").text);
            }

            Wake();
            Battle.Tick();
            yield return null;
            Assert.AreEqual("+20", L("sell-value").text);
            Assert.IsTrue(SellHas("sell-button--refund") && SellHas("sell-button--pop"), SellClasses());
            AssertLook(DropRed, DropRed, 1.4f, "the first frame");
            Assert.AreEqual(DisplayStyle.Flex, L("sell-value-tail").resolvedStyle.display);

            Battle.Tick();
            yield return null;
            Assert.IsFalse(SellHas("sell-button--pop"));
            Assert.IsTrue(SellHas("sell-button--settle"));
            string settled = SellClasses();
            for (int i = 0; i < 20; i++)
            {
                Battle.Tick();
                yield return null;
                Assert.AreEqual(settled, SellClasses(), $"frame {i} after the settle began: nothing is written");
                Assert.AreEqual("+20", L("sell-value").text);
            }
            yield return new WaitForSecondsRealtime(0.9f);
            AssertLook(RefundGold, RefundBorder, 1f, "after the drop");
        }

        [UnityTest]
        public IEnumerator S4c_AC9_The_Enlarged_Number_On_The_Pop_Frame_Grows_Leftwards_And_Does_Not_Overlap_The_Tail_And_Is_Back_At_Scale_1_After_The_Settle()
        {
            Buy(A);
            ClickCell(A.X, A.Y);
            _input.Hover(OffGrid);
            yield return Frames(3);
            AssertFullLook(40, "before");

            Wake();
            Battle.Tick();
            yield return null;
            Assert.IsTrue(SellHas("sell-button--pop"), SellClasses());
            Label number = L("sell-value"), tail = L("sell-value-tail");
            Assert.AreEqual(1.4f, number.resolvedStyle.scale.value.x, 0.005f, "the pop frame is at scale 1.4");
            float numberRight = number.worldBound.xMax, tailLeft = tail.worldBound.xMin;
            Debug.Log($"4c pop frame: number [{number.worldBound.xMin:F2}, {numberRight:F2}], tail left {tailLeft:F2}, scale {number.resolvedStyle.scale.value.x:F2}");
            Assert.LessOrEqual(numberRight, tailLeft + Snap, $"the enlarged number's right edge ({numberRight:F2}) is at or left of the tail's left edge ({tailLeft:F2})");

            yield return new WaitForSecondsRealtime(1.0f);
            Battle.Tick();
            yield return Frames(2);
            Assert.AreEqual(1f, number.resolvedStyle.scale.value.x, 0.005f, "back at scale 1 after the settle");
            Assert.LessOrEqual(number.worldBound.xMax, tail.worldBound.xMin + Snap, "and still not over the tail");
        }

        [UnityTest]
        public IEnumerator S4c_AC9_M15_A_Tower_Selected_After_It_Fired_Shows_The_Refund_With_No_Drop_And_The_Reverse_Shows_The_Full_Look()
        {
            Tower fired = Buy(A);
            Tower fresh = BuyCheap(Quiet, 30);
            Wake();
            Battle.Tick();
            Assert.AreEqual(20, QueryValue(A));
            Assert.AreEqual(30, QueryValue(Quiet));

            ClickCell(Quiet.X, Quiet.Y);
            yield return Frames(2);
            AssertFullLook(30, "a never-fired tower");
            ClickCell(A.X, A.Y); // directly, with no frame in between
            yield return null;
            Assert.AreEqual("+20", L("sell-value").text);
            Assert.IsFalse(SellHas("sell-button--pop") || SellHas("sell-button--settle"), $"no drop for a tower selected after it fired: {SellClasses()}");
            Assert.IsTrue(SellHas("sell-button--refund"));
            AssertLook(RefundGold, RefundBorder, 1f, "the refund look at once");
            yield return Frames(3);
            AssertRefundLook(20, 40, "and it stays");

            // The reverse: the full look after the refund, with the tail hidden and no class left.
            ClickCell(Quiet.X, Quiet.Y);
            yield return null;
            AssertFullLook(30, "after a fired tower, directly");
            Assert.IsNotNull(fired);
            Assert.IsNotNull(fresh);
        }

        [UnityTest]
        public IEnumerator S4c_AC9_M15_A_Never_Fired_Tower_Selected_While_The_Settle_Runs_Shows_The_Full_Look_And_Clears_The_Settle()
        {
            Buy(A);
            BuyCheap(Quiet, 30);
            ClickCell(A.X, A.Y);
            yield return Frames(2);
            Wake();
            Battle.Tick();
            yield return Frames(2);
            Assert.IsTrue(SellHas("sell-button--settle"), "the settle runs");

            ClickCell(Quiet.X, Quiet.Y);
            yield return null;
            AssertFullLook(30, "a never-fired tower after a settle");
            yield return Frames(2);
            AssertFullLook(30, "and it stays");
        }

        [UnityTest]
        public IEnumerator S4c_AC9_M16_The_Drop_Classes_Do_Not_Survive_A_Change_Of_Selection_On_The_Pop_Frame_Or_Mid_Settle(
            [Values("pop", "settle")] string when, [Values("fresh", "fired")] string target)
        {
            Buy(A);
            BuyCheap(B, 10);
            BuyCheap(Quiet, 30);
            ClickCell(A.X, A.Y);
            yield return Frames(2);
            Wake();
            Battle.Tick(); // A and B launch on this tick
            yield return null;
            Assert.IsTrue(SellHas("sell-button--pop"), "the pop frame");
            if (when == "settle")
            {
                yield return null;
                yield return new WaitForSecondsRealtime(0.1f);
                Assert.IsTrue(SellHas("sell-button--settle"), "mid-settle");
                Assert.Greater(L("sell-value").resolvedStyle.scale.value.x, 1.005f, "the scale is still on its way down");
            }

            GridPos next = target == "fresh" ? Quiet : B;
            ClickCell(next.X, next.Y);
            yield return null;
            Assert.IsFalse(SellHas("sell-button--pop") || SellHas("sell-button--settle"), $"the new tower has neither class: {SellClasses()}");
            if (target == "fresh") AssertFullLook(30, "the new tower");
            else AssertRefundLook(5, 10, "the new tower");
            yield return Frames(2);
            if (target == "fresh") AssertFullLook(30, "the new tower, later");
            else AssertRefundLook(5, 10, "the new tower, later");
        }

        [UnityTest]
        public IEnumerator S4c_AC9_M14_The_Panel_Has_The_Value_After_The_Frames_Ticks_In_LateUpdate_Not_The_Value_Before_Them()
        {
            Buy(A);
            ClickCell(A.X, A.Y);
            yield return Frames(3);

            var probe = new GameObject("LateProbe").AddComponent<LateProbe>();
            int samples = 0;
            var mismatches = new List<string>();
            probe.Sample = () =>
            {
                samples++;
                if (!Battle.TryGetSellValue(A, out int value)) return;
                int shown = ShownNumber();
                if (shown != value) mismatches.Add($"frame {Time.frameCount}: shows {shown}, Core answers {value}");
            };
            for (int i = 0; i < 6; i++)
            {
                if (i == 3) Wake();
                Battle.Tick();
                yield return null;
            }
            Object.Destroy(probe.gameObject);
            Assert.GreaterOrEqual(samples, 6);
            Assert.IsEmpty(mismatches, string.Join("\n", mismatches));
            Assert.AreEqual("+20", L("sell-value").text);
        }

        [UnityTest]
        public IEnumerator S4c_AC9_A_Click_On_Sell_Pays_What_Core_Answers_At_That_Moment_Even_When_The_Last_Drawn_Number_Was_The_Cost()
        {
            Buy(A);
            ClickCell(A.X, A.Y);
            yield return Frames(3);
            Assert.AreEqual("+40", L("sell-value").text);
            Wake();
            Battle.Tick(); // the tower fires in the frame of the click: no frame has drawn the new number yet
            int gold = Battle.Gold;
            Assert.AreEqual("+40", L("sell-value").text, "the last drawn number was the cost");
            ClickButton(Btn("sell-button"));
            Assert.AreEqual(gold + 20, Battle.Gold, "the sale always pays Core's value (the documented gap of the plan's risks)");
            yield return null;
        }

        // ---- 4c-AC11 allocation, with a tower selected and the panel open

        [UnityTest]
        public IEnumerator S4c_AC11_The_Input_The_Board_View_And_The_Panel_Allocate_Nothing_Per_Frame_With_A_Tower_Selected()
        {
            Buy(A);
            Buy(B, 1);
            Wake();
            Battle.Tick();
            ClickCell(A.X, A.Y);
            yield return Frames(3);
            _input.enabled = true;
            yield return MovePointer(ScreenCentreOf(Btn("sell-button")));
            yield return Frames(3);
            Assert.IsNotNull(_input.SelectedTower);
            Assert.IsTrue(Displayed("tower-panel"));
            Assert.AreEqual("+20", L("sell-value").text);

            var views = new MonoBehaviour[] { _input, Object.FindAnyObjectByType<BoardView>(), Object.FindAnyObjectByType<TowerPanelView>() };
            foreach (MonoBehaviour view in views)
            {
                System.Action frame = FrameMethod(view);
                frame();
                frame();
                int count = Allocations.Of(frame);
                Assert.AreEqual(0, count, $"{view.GetType().Name} allocated {count} time(s) with a tower selected");
            }

            // The input again with the pointer over a cell, and the selection read.
            yield return MovePointer(ScreenOf(Free));
            yield return Frames(2);
            Assert.IsTrue(_input.HasHoverCell);
            System.Action update = FrameMethod(_input);
            update();
            Assert.AreEqual(0, Allocations.Of(update), "the input with the pointer on a cell");
            System.Action select = () => { Tower t = _input.SelectedTower; if (t == null) Assert.Fail(); };
            select();
            Assert.AreEqual(0, Allocations.Of(select), "reading the selection");
            System.Action ask = () => { Battle.TryGetSellValue(A, out int v); if (v < 0) Assert.Fail(); };
            ask();
            Assert.AreEqual(0, Allocations.Of(ask), "the query");

            // Across ticks, with the board view and the panel drawing every frame.
            System.Action board = FrameMethod(views[1]), panel = FrameMethod(views[2]);
            for (int tick = 0; tick < 60; tick++)
            {
                Battle.Tick();
                Assert.AreEqual(0, Allocations.Of(board), $"board view, tick {tick}");
                Assert.AreEqual(0, Allocations.Of(panel), $"panel view, tick {tick}");
            }
        }

        [UnityTest]
        public IEnumerator S4c_AC11_The_Panel_Does_Not_Rewrite_Its_Texts_On_A_Frame_Where_Nothing_Changed()
        {
            Buy(A);
            ClickCell(A.X, A.Y);
            yield return Frames(3);
            System.Action panel = FrameMethod(Object.FindAnyObjectByType<TowerPanelView>());
            panel();
            for (int i = 0; i < 5; i++) Assert.AreEqual(0, Allocations.Of(panel), "an open panel, an unchanged value");
            // A change allocates (a text is built), and only then: the next frame is silent again.
            Wake();
            Battle.Tick();
            Assert.Greater(Allocations.Of(panel), 0, "the value changed: a text is built");
            panel(); // the frame after the drop: the pop gives way to the settle (a class swap, no text)
            for (int i = 0; i < 3; i++) Assert.AreEqual(0, Allocations.Of(panel), $"settled, frame {i}");
        }

        // ---- the scene: one HUD

        [UnityTest]
        public IEnumerator S4c_AC7_The_Hud_Object_Has_No_PanelRenderer_And_The_Panels_Visual_Tree_Holds_One_Hud_Root()
        {
            yield return Frames(2);
            Assert.AreEqual(0, Object.FindObjectsByType<PanelRenderer>(FindObjectsInactive.Include).Length, "a PanelRenderer draws a second, unwired HUD");
            Assert.AreEqual(1, Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include).Length);
            Assert.IsNull(_document.GetComponent<PanelRenderer>());
            VisualElement tree = Root.panel.visualTree;
            foreach (string name in new[] { "top-bar", "board-row", "board-area", "side-column", "tower-bar", "tower-panel", "bottom-bar", "gold-label", "message", "end-screen", "sell-button", "panel-close" })
                Assert.AreEqual(1, tree.Query<VisualElement>(name).ToList().Count, $"exactly one '{name}' in the panel");
            int roots = 0;
            foreach (VisualElement child in tree.Children())
                if (child.Q<VisualElement>("top-bar") != null) roots++;
            Assert.AreEqual(1, roots, "one HUD root under the panel's tree");
            Assert.AreEqual(2, TowerButtons().Length);
        }
    }

    /// <summary>Runs after every other script's LateUpdate, so it sees what the frame is about to draw.</summary>
    [DefaultExecutionOrder(32000)]
    public sealed class LateProbe : MonoBehaviour
    {
        public System.Action Sample;

        private void LateUpdate() => Sample?.Invoke();
    }
}
