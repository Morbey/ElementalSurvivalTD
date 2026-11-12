using System.Collections;
using System.Collections.Generic;
using ElementalTD.Core;
using ElementalTD.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ElementalTD.PlayModeTests
{
    /// <summary>
    /// Light and Shadow on the scene (plan 0007, step 4b): choosing, the tower bar, clicks on the UI, colours, the announcement,
    /// the layout, hover and range, the message line, the end screen and the allocation of the views.
    /// Real mouse clicks on buttons need a focused window, so a button is clicked with the UI Toolkit event its handler listens to.
    /// </summary>
    public class ElementSceneTests : SceneTestBase
    {
        // ---- 4b-AC6 choosing

        [UnityTest]
        public IEnumerator S4b_AC6_The_Input_Holds_Light_Then_Shadow_And_Starts_On_The_First()
        {
            yield return null;
            Assert.AreEqual(2, _input.TowerCount);
            Assert.AreEqual("Light", _input.TowerAt(0).Element.DisplayName);
            Assert.AreEqual("Shadow", _input.TowerAt(1).Element.DisplayName);
            Assert.AreEqual(0, _input.SelectedIndex);
            Assert.AreSame(_input.TowerAt(0), _input.Tower);
        }

        [UnityTest]
        public IEnumerator S4b_AC6_Select_Ignores_An_Index_Outside_The_List_Whichever_Tower_Is_Selected()
        {
            yield return null;
            foreach (int start in new[] { 0, 1 })
            {
                _input.Select(start);
                foreach (int bad in new[] { -1, 2, 3, int.MaxValue, int.MinValue })
                {
                    _input.Select(bad);
                    Assert.AreEqual(start, _input.SelectedIndex, $"Select({bad}) with {start} selected");
                    yield return null; // a bad index must not break the next frame (the views read the selection)
                    Assert.AreSame(_input.TowerAt(start), _input.Tower);
                }
            }
        }

        [UnityTest]
        public IEnumerator S4b_AC6_Keys_Select_The_Nth_Tower_Key_3_Does_Nothing_And_Key_1_Returns()
        {
            _input.enabled = true;
            yield return PressKey(Key.Digit2);
            Assert.AreEqual(1, _input.SelectedIndex, "key 2");
            yield return PressKey(Key.Digit3);
            Assert.AreEqual(1, _input.SelectedIndex, "key 3 with two towers does nothing");
            yield return PressKey(Key.Digit9);
            yield return PressKey(Key.Digit0);
            Assert.AreEqual(1, _input.SelectedIndex, "keys 9 and 0 do nothing");
            yield return PressKey(Key.Digit1);
            Assert.AreEqual(0, _input.SelectedIndex, "key 1 returns from the second tower");
            yield return PressKey(Key.Digit1);
            Assert.AreEqual(0, _input.SelectedIndex, "key 1 on the first stays");
        }

        [UnityTest]
        public IEnumerator S4b_AC6_The_Selection_Survives_A_Purchase_And_A_Sale_And_A_Click_On_A_Button_Selects_Without_Buying()
        {
            yield return null;
            Button[] buttons = TowerButtons();
            ClickButton(buttons[1]);
            Assert.AreEqual(1, _input.SelectedIndex, "a click on the second button");
            Assert.AreEqual(0, Board.TowerCount, "a click on a button buys nothing");
            Assert.AreEqual(StartingGold(), Battle.Gold);

            ClickCell(2, 2); // buys the selected tower
            Assert.AreEqual(1, _input.SelectedIndex, "a purchase keeps the selection");
            Assert.AreEqual(ElementOfTower(1), Board.TowerOn(new GridPos(2, 2)).Stats.Element, "the click bought the selected tower");
            SellByClick(2, 2);
            Assert.AreEqual(1, _input.SelectedIndex, "a sale keeps the tower to buy");

            ClickButton(buttons[0]);
            Assert.AreEqual(0, _input.SelectedIndex);
            ClickCell(3, 2);
            Assert.AreEqual(ElementOfTower(0), Board.TowerOn(new GridPos(3, 2)).Stats.Element);
            yield return null;
            Assert.AreSame(LookOf(ElementOfTower(0)).Tower, SharedMaterialOf(GameObject.Find("Tower 3,2").transform));
        }

        [UnityTest]
        public IEnumerator S4b_AC6_After_A_Restart_The_First_Tower_Is_Selected_Again()
        {
            yield return null;
            _input.Select(1);
            yield return null;
            Assert.AreEqual(1, _input.SelectedIndex);

            var old = _bootstrap;
            _bootstrap.Restart();
            int guard = 0;
            while (old != null && guard++ < 120) yield return null;
            Assert.IsTrue(old == null, "the scene never reloaded");
            yield return null;
            AcquireScene(disableInput: true);
            yield return Frames(3);

            Assert.AreEqual(0, _input.SelectedIndex);
            Button[] buttons = TowerButtons();
            Assert.IsTrue(buttons[0].ClassListContains("tower-button--selected"));
            Assert.IsFalse(buttons[1].ClassListContains("tower-button--selected"));
            ClickCell(2, 2);
            Assert.AreEqual(ElementOfTower(0), Board.TowerOn(new GridPos(2, 2)).Stats.Element, "the next purchase is a Light tower");
        }

        // ---- 4b-AC7 the buy bar

        [UnityTest]
        public IEnumerator S4b_AC7_Each_Button_Has_Its_Key_Swatch_Name_And_Cost_In_The_Style_Of_The_Visual_Direction()
        {
            yield return Frames(2);
            Button[] buttons = TowerButtons();
            Assert.AreEqual(2, buttons.Length);
            for (int i = 0; i < 2; i++)
            {
                Button button = buttons[i];
                var tower = _input.TowerAt(i);
                Assert.AreEqual((i + 1).ToString(), button.Q<Label>(className: "tower-button__key").text, $"button {i} key");
                Assert.AreEqual(tower.Element.DisplayName, button.Q<Label>(className: "tower-button__name").text, $"button {i} name");
                Assert.AreEqual($"{tower.ToStats().Cost} gold", button.Q<Label>(className: "tower-button__price").text, $"button {i} cost");
                VisualElement swatch = button.Q<VisualElement>(className: "tower-button__swatch");
                AssertColor(LookOf(tower.ToStats().Element).Tower.color, swatch.resolvedStyle.backgroundColor, $"button {i} swatch");
                Assert.AreEqual(192f, button.resolvedStyle.width, Snap, $"button {i} width (the column's)");
                Assert.AreEqual(56f, button.resolvedStyle.height, Snap, $"button {i} height");
                Assert.AreEqual(22f, swatch.resolvedStyle.width, Snap, $"button {i} swatch width");
                Assert.AreEqual(22f, swatch.resolvedStyle.height, Snap, $"button {i} swatch height");
                Assert.IsFalse(button.focusable);
            }
            Assert.AreEqual(40, _input.TowerAt(0).ToStats().Cost, "document: cost 40");
            Assert.AreEqual(new Color32(255, 194, 61, 255), (Color32)buttons[0].Q<Label>(className: "tower-button__price").resolvedStyle.color);
        }

        [UnityTest]
        public IEnumerator S4b_AC7_Exactly_One_Button_Is_Marked_From_The_Frame_After_The_Selection_Changes()
        {
            yield return Frames(2);
            Button[] buttons = TowerButtons();
            foreach (int index in new[] { 0, 1, 0, 1, 1, 0 })
            {
                _input.Select(index);
                yield return null;
                int marked = 0;
                for (int i = 0; i < buttons.Length; i++)
                    if (buttons[i].ClassListContains("tower-button--selected")) { marked++; Assert.AreEqual(index, i, $"selected {index}"); }
                Assert.AreEqual(1, marked, $"after selecting {index}: exactly one button is marked");
            }
            yield return null;
            Assert.AreEqual(3f, buttons[0].resolvedStyle.borderTopWidth, Snap, "the selected button has a border of 3");
            Assert.AreEqual(new Color32(255, 255, 255, 255), (Color32)buttons[0].resolvedStyle.borderTopColor);
            Assert.AreEqual(2f, buttons[1].resolvedStyle.borderTopWidth, Snap, "the other keeps 2");
            Assert.Greater(buttons[0].resolvedStyle.borderTopWidth, buttons[1].resolvedStyle.borderTopWidth, "the selected border is the thicker");
            // Padding 9 and border 3 make the same 12 as padding 10 and border 2; each snaps to the pixel grid on its own, so the sizes agree to about a pixel.
            Assert.AreEqual(buttons[1].resolvedStyle.width, buttons[0].resolvedStyle.width, 1.5f, "the size holds when selected");
            Assert.AreEqual(buttons[1].resolvedStyle.height, buttons[0].resolvedStyle.height, 1.5f);
        }

        [UnityTest]
        public IEnumerator S4b_AC7_A_Button_Short_Of_Gold_Is_Marked_From_The_Frame_After_The_Gold_Changes_And_Still_Selects()
        {
            yield return Frames(2);
            Button[] buttons = TowerButtons();
            int cost = _input.TowerAt(0).ToStats().Cost;
            foreach (Button b in buttons) Assert.IsFalse(b.ClassListContains("tower-button--short"), "not short at the start");

            ClickCell(2, 2);
            ClickCell(3, 2);
            Assert.Less(Battle.Gold, cost, "the starting gold buys exactly two");
            foreach (Button b in buttons) Assert.IsFalse(b.ClassListContains("tower-button--short"), "no frame has run since the purchase");
            yield return Frames(2);
            foreach (Button b in buttons)
            {
                Assert.IsTrue(b.ClassListContains("tower-button--short"), "short from the frame after");
                Assert.AreEqual(new Color32(255, 107, 94, 255), (Color32)b.Q<Label>(className: "tower-button__price").resolvedStyle.color, "the price turns red");
            }

            ClickButton(buttons[1]); // short, and still selects
            yield return null;
            Assert.AreEqual(1, _input.SelectedIndex);
            Assert.IsTrue(buttons[1].ClassListContains("tower-button--selected"));

            SellByClick(3, 2); // a tower that never fired: the full cost back
            yield return Frames(2);
            foreach (Button b in buttons)
            {
                Assert.IsFalse(b.ClassListContains("tower-button--short"), "the sale brought the gold back");
                Assert.AreEqual(new Color32(255, 194, 61, 255), (Color32)b.Q<Label>(className: "tower-button__price").resolvedStyle.color);
            }
        }

        // ---- 4b-AC8 the UI takes its clicks

        [UnityTest]
        public IEnumerator S4b_AC8_A_Click_At_A_Buttons_Centre_Gives_No_Hover_Buys_Nothing_Sells_Nothing_At_Every_Aspect(
            [Values(0f, 1f, 2.5f)] float aspect)
        {
            if (aspect > 0f) _camera.aspect = aspect;
            yield return Frames(3);
            ClickCell(2, 2); // a tower to select or sell, if a click could reach the board
            int gold = Battle.Gold, towers = Board.TowerCount;
            Transform hover = FindByName("Hover");
            Transform disc = FindByName("RangeDisc");

            foreach (string barName in new[] { "top-bar", "bottom-bar", "side-column" })
            {
                _input.Hover(ScreenOf(new GridPos(4, 4))); // the pointer is on the board first
                yield return null;
                Assert.IsTrue(hover.gameObject.activeSelf && disc.gameObject.activeSelf);
                _input.Hover(ScreenCentreOf(Root.Q<VisualElement>(barName)));
                yield return null;
                Assert.IsFalse(_input.HasHoverCell, $"{barName}: no hover cell");
                Assert.IsFalse(hover.gameObject.activeSelf, $"{barName}: no hover");
                Assert.IsFalse(disc.gameObject.activeSelf, $"{barName}: no range disc");
            }

            foreach (Button button in TowerButtons())
            {
                Vector2 centre = ScreenCentreOf(button);
                _input.Hover(centre);
                _input.Click(centre);
                yield return null;
                Assert.IsFalse(_input.HasHoverCell, "no hover cell at a button's centre");
                Assert.IsFalse(hover.gameObject.activeSelf);
                Assert.IsFalse(disc.gameObject.activeSelf);
                Assert.AreEqual(towers, Board.TowerCount, "nothing bought or sold");
                Assert.AreEqual(gold, Battle.Gold);
                Assert.IsFalse(MessageShown(), "no refusal either");
            }

            ClickButton(TowerButtons()[1]);
            Assert.AreEqual(1, _input.SelectedIndex);
            Assert.AreEqual((towers, gold), (Board.TowerCount, Battle.Gold), "a click on a button changes the selection and nothing on the board");

            // 4c-AC4 and AC7: the panel's column, Sell, Upgrade and the close button take the pointer too, with a tower selected.
            Tower selected = SelectByClick(2, 2);
            yield return Frames(2);
            Assert.AreEqual(DisplayStyle.Flex, Root.Q<VisualElement>("tower-panel").resolvedStyle.display);
            foreach (string name in new[] { "side-column", "sell-button", "upgrade-button", "panel-close" })
            {
                Vector2 centre = ScreenCentreOf(Root.Q<VisualElement>(name));
                _input.Hover(centre);
                _input.Click(centre);
                yield return null;
                Assert.IsFalse(_input.HasHoverCell, $"{name}: no hover cell");
                Assert.IsFalse(hover.gameObject.activeSelf, $"{name}: no hover");
                Assert.AreSame(selected, _input.SelectedTower, $"{name}: a click there keeps the selection");
                Assert.IsTrue(disc.gameObject.activeSelf && FindByName("SelectionFrame").gameObject.activeSelf, $"{name}: the selected tower's frame and disc stay");
                Assert.AreEqual((towers, gold), (Board.TowerCount, Battle.Gold), $"{name}: nothing bought or sold");
                Assert.IsFalse(MessageShown(), $"{name}: no refusal");
            }
        }

        // ---- 4b-AC9 colours

        [Test]
        public void S4b_AC9_The_Body_Materials_Hold_The_Element_Colours()
        {
            foreach (string name in new[] { "Tower_Light", "Enemy_Light" }) AssertColor(Hex("FFE27A"), LoadMaterial(name).GetColor("_BaseColor"), name + " body");
            foreach (string name in new[] { "Tower_Shadow", "Enemy_Shadow" }) AssertColor(Hex("3B1F5C"), LoadMaterial(name).GetColor("_BaseColor"), name + " body");
        }

        [Test]
        public void S4b_AC9_The_Light_Bodies_Have_Their_Emission_On()
        {
            foreach (string name in new[] { "Tower_Light", "Enemy_Light" })
            {
                Material m = LoadMaterial(name);
                Assert.IsTrue(m.IsKeywordEnabled("_EMISSION"), name + " emission is on");
                AssertColor(Hex("5C4A12"), m.GetColor("_EmissionColor"), name + " emission");
            }
        }

        [Test]
        public void S4b_AC9_The_Bodies_Are_Matte_Smoothness_0()
        {
            foreach (string name in new[] { "Tower_Light", "Tower_Shadow", "Enemy_Light", "Enemy_Shadow" })
                Assert.AreEqual(0f, LoadMaterial(name).GetFloat("_Smoothness"), name + ": a specular highlight whitens Light into its own projectile colour");
        }

        [Test]
        public void S4b_AC9_The_Shot_Materials_Are_Unlit_In_The_Projectile_Colours()
        {
            AssertColor(Hex("FFF6CC"), LoadMaterial("Projectile_Light").GetColor("_BaseColor"), "Projectile_Light");
            AssertColor(Hex("2A1340"), LoadMaterial("Projectile_Shadow").GetColor("_BaseColor"), "Projectile_Shadow");
            StringAssert.Contains("Unlit", LoadMaterial("Projectile_Light").shader.name);
            StringAssert.Contains("Unlit", LoadMaterial("Projectile_Shadow").shader.name);
        }

        [Test]
        public void S4b_AC9_The_Range_Disc_Materials_Are_The_UI_Colours_At_Alpha_0_18()
        {
            Color light = Hex("FFE27A"), shadow = Hex("B48CFF");
            light.a = shadow.a = 0.18f;
            AssertColor(light, LoadMaterial("RangeDisc_Light").GetColor("_BaseColor"), "RangeDisc_Light");
            AssertColor(shadow, LoadMaterial("RangeDisc_Shadow").GetColor("_BaseColor"), "RangeDisc_Shadow");
        }

        [Test]
        public void S4b_AC9_The_Free_Cell_Is_Stone_968F7F()
        {
            AssertColor(Hex("968F7F"), LoadMaterial("Cell_Free").GetColor("_BaseColor"), "Cell_Free");
        }

        [Test]
        public void S4b_AC9_The_Wall_Is_Stone_4D4943()
        {
            AssertColor(Hex("4D4943"), LoadMaterial("Cell_Blocked").GetColor("_BaseColor"), "Cell_Blocked");
        }

        [UnityTest]
        public IEnumerator S4b_AC9_The_Camera_Background_And_The_Shot_Size_Hold_The_Visual_Direction()
        {
            yield return null;
            Assert.AreEqual(CameraClearFlags.SolidColor, _camera.clearFlags);
            AssertColor(Hex("2E2C29"), _camera.backgroundColor, "camera background");

            ClickCell(1, 3);
            yield return TickUntilFirstEnemy();
            yield return TickUntil(() => Battle.ProjectileCount > 0, 400, 1);
            Transform shot = FirstActiveChild("Projectiles");
            Assert.IsNotNull(shot);
            Assert.AreEqual(0.25f * _map.CellSize, shot.GetComponentInChildren<Renderer>().bounds.size.x, 0.005f, "the shot is a sphere of 0.25 cells");
        }

        [Test]
        public void S4b_AC9_The_Palette_Has_A_Look_For_Both_Elements_With_The_Colours_And_The_Materials_Of_The_Visual_Direction()
        {
            ElementPalette palette = Palette;
            Assert.AreEqual(2, palette.LookCount);
            for (int i = 0; i < 2; i++)
            {
                Element element = ElementOfTower(i);
                ElementPalette.Look look = LookOf(element);
                string name = look.Element.DisplayName;
                AssertColor(Hex(name == "Light" ? "FFE27A" : "B48CFF"), look.UiColor, name + " UI colour");
                Assert.AreSame(LoadMaterial("Tower_" + name), look.Tower, name);
                Assert.AreSame(LoadMaterial("Enemy_" + name), look.Enemy, name);
                Assert.AreSame(LoadMaterial("Projectile_" + name), look.Projectile, name);
                Assert.AreSame(LoadMaterial("RangeDisc_" + name), look.RangeDisc, name);
            }
            Assert.AreNotEqual(LookOf(ElementOfTower(0)).Tower, LookOf(ElementOfTower(1)).Tower);
        }

        [UnityTest]
        public IEnumerator S4b_AC9_A_Tower_Of_Each_Element_Is_Drawn_With_Its_Palette_Material()
        {
            _input.Select(0);
            ClickCell(2, 2);
            _input.Select(1);
            ClickCell(3, 2);
            yield return null;
            Assert.AreSame(LookOf(ElementOfTower(0)).Tower, SharedMaterialOf(GameObject.Find("Tower 2,2").transform));
            Assert.AreSame(LookOf(ElementOfTower(1)).Tower, SharedMaterialOf(GameObject.Find("Tower 3,2").transform));
        }

        [UnityTest]
        public IEnumerator S4b_AC9_A_Pooled_Enemy_View_Reused_By_The_Other_Element_Changes_Material_On_Its_First_Frame()
        {
            Element light = ElementOfTower(0), shadow = ElementOfTower(1);
            Enemy first = Board.SpawnEnemy(new EnemyStats(0f, 45f, 0, light));
            yield return null;
            Transform view = EnemyViewAt(0);
            Assert.AreSame(LookOf(light).Enemy, SharedMaterialOf(view));

            Board.RemoveEnemy(first);
            Enemy second = Board.SpawnEnemy(new EnemyStats(0f, 45f, 0, shadow));
            Assert.AreSame(first, second, "the board reuses the object");
            yield return null;
            Assert.AreSame(view, EnemyViewAt(0));
            Assert.AreSame(LookOf(shadow).Enemy, SharedMaterialOf(view), "its first frame with the other element");
            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
                Assert.AreSame(LookOf(shadow).Enemy, SharedMaterialOf(view), "and it stays: no instance of the material is made");
            }

            Board.RemoveEnemy(second);
            Board.SpawnEnemy(new EnemyStats(0f, 45f, 0, light));
            yield return null;
            Assert.AreSame(LookOf(light).Enemy, SharedMaterialOf(view), "and back");
        }

        [UnityTest]
        public IEnumerator S4b_AC9_A_Shot_View_Takes_The_Element_Of_The_Tower_That_Fired_It_Also_When_Reused()
        {
            _input.Select(1);
            ClickCell(1, 3);
            _input.Select(0);
            ClickCell(1, 5);
            Board.SpawnEnemy(new EnemyStats(0f, 1_000_000f, 0, ElementOfTower(1))); // a Shadow enemy: only the Light tower hurts it
            for (int tick = 0; tick < 200; tick++)
            {
                Battle.Tick();
                if (tick % 5 == 0)
                {
                    yield return null;
                    for (int i = 0; i < Battle.ProjectileCount; i++)
                        Assert.AreSame(LookOf(Battle.ProjectileAt(i).Element).Projectile, SharedMaterialOf(FindByName("Projectiles").GetChild(i)), $"tick {tick}, shot {i}");
                }
            }
        }

        // ---- 4b-AC10 the announcement

        [UnityTest]
        public IEnumerator S4b_AC10_The_Countdown_Names_The_Wave_To_Come_And_The_Wave_Line_The_Wave_That_Started_Last()
        {
            yield return null;
            Label wave = Root.Q<Label>("wave-label"), next = Root.Q<Label>("next-wave-label");
            Assert.AreEqual("Wave 0/10", wave.text, "before wave 1 the line names no element");
            Assert.AreEqual($"Next wave in 15 s: {Markup(ElementOfTower(1))}", next.text, "wave 1 is Shadow");
            StringAssert.Contains("#B48CFF", next.text, "in the element's UI colour");

            // Wave 1 (Shadow) has started and wave 2 (Light) is next: the two lines must differ.
            yield return TickUntil(() => Battle.Waves.StartedWaves == 1 && Battle.Waves.SecondsToNextWave > 0f, 3000, 20);
            Assert.AreEqual($"Wave 1/10: {Markup(ElementOfTower(1))}", wave.text);
            Assert.AreEqual($"Next wave in {Mathf.CeilToInt(Battle.Waves.SecondsToNextWave)} s: {Markup(ElementOfTower(0))}", next.text, "the countdown names Light, not the Shadow wave that started last");
            Assert.AreEqual(DisplayStyle.Flex, next.resolvedStyle.display);

            // While a wave spawns the countdown is hidden.
            yield return TickUntil(() => Battle.Waves.SecondsToNextWave <= 0f && Battle.Waves.StartedWaves == 2, 3000, 20);
            Assert.AreEqual(DisplayStyle.None, next.resolvedStyle.display, "hidden while a wave spawns");
            Assert.AreEqual($"Wave 2/10: {Markup(ElementOfTower(0))}", wave.text);
        }

        [UnityTest]
        public IEnumerator S4b_AC10_When_The_Last_Wave_Starts_No_Countdown_Shows_And_Nothing_Is_Thrown()
        {
            // The run ticked far with a huge life count would need a new battle; the reference order wins in 10165 ticks, and the last wave starts on tick 8460.
            int ticks = 0;
            while (Battle.Waves.StartedWaves < Battle.Waves.WaveCount && ticks < 12000 && Battle.Outcome == RunOutcome.Running)
            {
                RunReferenceActionsAt(ticks);
                Battle.Tick();
                if (++ticks % 30 == 0) yield return null;
            }
            yield return Frames(2);
            Assert.AreEqual(10, Battle.Waves.StartedWaves);
            Assert.AreEqual(DisplayStyle.None, Root.Q<Label>("next-wave-label").resolvedStyle.display, "no wave is left");
            Assert.AreEqual($"Wave 10/10: {Markup(ElementOfTower(1))}", Root.Q<Label>("wave-label").text);
        }

        // ---- 4b-AC16 layout

        [UnityTest]
        public IEnumerator S4b_AC16_The_Board_Is_Framed_Inside_The_Board_Area_At_The_Games_Aspect_And_At_1_And_2_5(
            [Values(0f, 1f, 2.5f)] float aspect)
        {
            if (aspect > 0f) _camera.aspect = aspect;
            yield return Frames(3);
            AssertLayout($"aspect {(aspect > 0f ? aspect.ToString() : "of the game view")}");
        }

        [UnityTest]
        public IEnumerator S4b_AC16_The_Framing_Follows_A_Change_Of_Aspect_And_Comes_Back()
        {
            yield return Frames(2);
            AssertLayout("start");
            foreach (float aspect in new[] { 1f, 2.5f, 1.6f })
            {
                _camera.aspect = aspect;
                yield return Frames(2);
                AssertLayout($"after a resize to {aspect}");
            }
        }

        [UnityTest]
        public IEnumerator S4b_AC16_The_Bars_And_Labels_Have_The_Sizes_And_Styles_Of_The_Visual_Direction()
        {
            yield return Frames(3);
            Assert.AreEqual(44f, Root.Q<VisualElement>("top-bar").resolvedStyle.height, Snap, "top bar");
            Assert.AreEqual(72f, Root.Q<VisualElement>("bottom-bar").resolvedStyle.height, Snap, "bottom bar");
            Assert.AreEqual(20f, Root.Q<Label>("wave-label").resolvedStyle.fontSize);
            Assert.AreEqual(20f, Root.Q<Label>("lives-label").resolvedStyle.fontSize);
            Assert.AreEqual(20f, Root.Q<Label>("next-wave-label").resolvedStyle.fontSize);
            Assert.AreEqual(24f, GoldLabel.resolvedStyle.fontSize);
            Assert.AreEqual(new Color32(255, 194, 61, 255), (Color32)GoldLabel.resolvedStyle.color, "gold");
            Assert.AreEqual(20f, _label.resolvedStyle.fontSize, "message line");
            foreach (string name in new[] { "wave-label", "lives-label", "next-wave-label" })
                Assert.AreEqual(new Color32(255, 255, 255, 255), (Color32)Root.Q<Label>(name).resolvedStyle.color, name);
            Assert.AreEqual(FontStyle.Bold, Root.Q<Label>("wave-label").resolvedStyle.unityFontStyleAndWeight);
            Assert.AreEqual(24f, Root.Q<Label>("lives-label").resolvedStyle.marginLeft, Snap, "lives sit 24 from the wave line");
            Assert.AreEqual(16f, _label.resolvedStyle.marginLeft, Snap, "the message line sits 16 from the gold");
            // The bars and the column have the camera's background colour (1.3, 1.5).
            foreach (string name in new[] { "top-bar", "bottom-bar", "side-column" })
                AssertColor(Hex("2E2C29"), Root.Q<VisualElement>(name).resolvedStyle.backgroundColor, name + " background");
            // The message line lies inside the bottom bar; from 4c the tower buttons are stacked in the side column.
            Rect bottom = Root.Q<VisualElement>("bottom-bar").worldBound;
            Assert.IsTrue(bottom.Contains(_label.worldBound.center));
            Assert.Greater(_label.worldBound.xMin, GoldLabel.worldBound.xMax);
            Rect root = Root.worldBound, top = Root.Q<VisualElement>("top-bar").worldBound;
            VisualElement column = Root.Q<VisualElement>("side-column");
            Rect col = column.worldBound;
            Assert.AreEqual(216f, col.width, Snap, "the column is 216 wide");
            Assert.AreEqual(root.xMax, col.xMax, Snap, "at the right edge");
            Assert.AreEqual(top.yMax, col.yMin, Snap, "under the top bar");
            Assert.AreEqual(bottom.yMin, col.yMax, Snap, "over the bottom bar");
            Assert.AreEqual(12f, column.resolvedStyle.paddingLeft, Snap, "padding 12");
            Button[] buttons = TowerButtons();
            Assert.AreEqual(2, buttons.Length);
            foreach (Button b in buttons)
            {
                Assert.AreEqual(192f, b.resolvedStyle.width, Snap, "192 wide");
                Assert.AreEqual(56f, b.resolvedStyle.height, Snap, "56 high");
                Assert.AreEqual(col.xMin + 12f, b.worldBound.xMin, Snap, "at the column's padding");
                Assert.IsTrue(col.Contains(b.worldBound.center), "inside the column");
            }
            Assert.AreEqual(col.yMin + 12f, buttons[0].worldBound.yMin, Snap, "the first starts at the top of the column");
            Assert.AreEqual(8f, buttons[1].worldBound.yMin - buttons[0].worldBound.yMax, Snap, "stacked 8 apart");
        }

        [UnityTest]
        public IEnumerator S4b_AC16_Frames_One_And_Two_After_A_Load_Are_Reported_And_From_The_Third_The_Layout_Holds()
        {
            var report = new List<string>();
            for (int round = 1; round <= 2; round++)
            {
                if (round == 1) SceneManager.LoadScene("Prototype");
                else _bootstrap.Restart();
                for (int frame = 1; frame <= 5; frame++)
                {
                    yield return null;
                    var document = Object.FindAnyObjectByType<UIDocument>();
                    var camera = Object.FindAnyObjectByType<Camera>();
                    if (document == null || camera == null || document.rootVisualElement == null) { report.Add($"load {round} frame {frame}: no document yet"); continue; }
                    _document = document;
                    _camera = camera;
                    _map = Object.FindAnyObjectByType<ElementalTD.Gameplay.PrototypeBootstrap>().Map;
                    VisualElement area = document.rootVisualElement.Q<VisualElement>("board-area");
                    if (area == null || !(area.worldBound.width > 0f)) { report.Add($"load {round} frame {frame}: no layout; camera size {camera.orthographicSize:F2}"); continue; }
                    Area a = BoardArea();
                    Vector3 centre = camera.WorldToViewportPoint(new Vector3(_map.Width * _map.CellSize * 0.5f, 0f, _map.Height * _map.CellSize * 0.5f));
                    report.Add($"load {round} frame {frame}: board centre at ({centre.x:F3}, {centre.y:F3}) of the screen, area centre ({(a.X0 + a.X1) * 0.5f:F3}, {(a.Y0 + a.Y1) * 0.5f:F3}), camera size {camera.orthographicSize:F3}");
                    if (frame >= 3) AssertLayout($"load {round} frame {frame}");
                }
                AcquireScene(disableInput: true);
            }
            Debug.Log("4b-AC16 first frames after a load:\n" + string.Join("\n", report));
        }

        // ---- 4b-AC17 hover and range

        [UnityTest]
        public IEnumerator S4b_AC17_The_Hover_Lies_On_The_Cell_Under_The_Tower_With_The_Same_Look_On_Every_Cell()
        {
            Transform hover = FindByName("Hover");
            Assert.IsFalse(hover.gameObject.activeSelf, "no hover before the pointer is on the grid");

            _input.Hover(ScreenOf(new GridPos(2, 2)));
            yield return null;
            Assert.IsTrue(hover.gameObject.activeSelf);
            Renderer quad = hover.GetComponentInChildren<Renderer>();
            Assert.Less(Vector3.Distance(_map.CellToWorld(new GridPos(2, 2)), new Vector3(hover.position.x, 0f, hover.position.z)), 0.01f);
            Assert.AreEqual(0.95f * _map.CellSize, quad.bounds.size.x, 0.005f, "the quad is 0.95 cells");
            Assert.AreEqual(0.02f, quad.bounds.center.y, 0.005f, "at height 0.02");
            Material free = quad.sharedMaterial;
            Assert.AreEqual(MaterialDir + "Hover.mat", PathOf(free));
            AssertColor(new Color(1f, 1f, 1f, 0.3f), free.GetColor("_BaseColor"), "hover");
            Assert.AreEqual(3001, free.renderQueue, "draws after the range disc");
            Assert.AreEqual(3000, FindByName("RangeDisc").GetComponentInChildren<Renderer>().sharedMaterial.renderQueue);

            // A cell that would refuse the purchase (wall, spawn, exit) looks like a free one.
            foreach (var refused in new[] { new GridPos(5, 0), _map.Spawn, _map.Exit })
            {
                _input.Hover(ScreenOf(refused));
                yield return null;
                Assert.IsTrue(hover.gameObject.activeSelf, $"{refused}");
                Assert.AreSame(free, quad.sharedMaterial, $"{refused} looks like a free cell");
            }

            ClickCell(3, 2);
            _input.Hover(ScreenOf(new GridPos(3, 2)));
            yield return null;
            Assert.IsTrue(hover.gameObject.activeSelf);
            Assert.Less(Vector3.Distance(_map.CellToWorld(new GridPos(3, 2)), new Vector3(hover.position.x, 0f, hover.position.z)), 0.01f);
            Assert.AreSame(free, quad.sharedMaterial, "the same look on a tower's cell");
            Renderer tower = GameObject.Find("Tower 3,2").GetComponentInChildren<Renderer>();
            Assert.Less(quad.bounds.max.y, tower.bounds.min.y + 0.1f * _map.CellSize, "the hover is under the tower");
            Assert.Less(quad.bounds.max.y, tower.bounds.max.y, "its top is below the tower's top");

            _input.Hover(OffGrid);
            yield return null;
            Assert.IsFalse(hover.gameObject.activeSelf);
            Assert.IsFalse(FindByName("RangeDisc").gameObject.activeSelf, "off the grid both are inactive");
        }

        [UnityTest]
        public IEnumerator S4b_AC17_The_Range_Disc_Follows_The_Tower_Under_The_Pointer_Or_Else_The_Selected_One_From_The_Frame_After_A_Change()
        {
            Transform disc = FindByName("RangeDisc");
            Renderer discRenderer = disc.GetComponentInChildren<Renderer>();
            Material lightDisc = LookOf(ElementOfTower(0)).RangeDisc, shadowDisc = LookOf(ElementOfTower(1)).RangeDisc;
            Assert.AreNotSame(lightDisc, shadowDisc);

            // A Light tower stands at (3,2); Light is selected.
            ClickCell(3, 2);
            var free = new GridPos(6, 6);
            _input.Hover(ScreenOf(free));
            yield return null;
            Assert.IsTrue(disc.gameObject.activeSelf);
            Assert.AreSame(lightDisc, discRenderer.sharedMaterial, "on a free cell: the selected tower's disc");
            Assert.AreEqual(_input.Tower.RangeCells * _map.CellSize, discRenderer.bounds.extents.x, 0.05f, "with its range");
            Assert.AreEqual(0.01f, discRenderer.bounds.center.y, 0.01f, "flat, at height 0.01");
            Assert.Less(Vector3.Distance(_map.CellToWorld(free), new Vector3(disc.position.x, 0f, disc.position.z)), 0.01f);

            _input.Select(1);
            yield return null;
            Assert.AreSame(shadowDisc, discRenderer.sharedMaterial, "key 2 changes the disc on the next frame");

            _input.Hover(ScreenOf(new GridPos(3, 2)));
            yield return null;
            Assert.AreSame(lightDisc, discRenderer.sharedMaterial, "on a Light tower with Shadow selected: the Light disc");
            Assert.AreEqual(2.5f * _map.CellSize, discRenderer.bounds.extents.x, 0.05f, "with that tower's range");

            _input.Hover(ScreenOf(free));
            yield return null;
            Assert.AreSame(shadowDisc, discRenderer.sharedMaterial, "back on a free cell: the selected tower's disc again");

            _input.Select(0);
            yield return null;
            Assert.AreSame(lightDisc, discRenderer.sharedMaterial);
            _input.Hover(OffGrid);
            yield return null;
            Assert.IsFalse(disc.gameObject.activeSelf);
        }

        // ---- 4b-AC18 the message line

        private Color32 LineColour => _label.resolvedStyle.color;

        [UnityTest]
        public IEnumerator S4b_AC18_A_Refusal_Shows_In_Its_Style_For_2_Seconds_And_Keeps_Its_Class_While_It_Fades()
        {
            yield return null;
            Assert.IsFalse(MessageShown());
            ClickCell(_map.Spawn.X, _map.Spawn.Y);
            yield return null;
            Assert.IsTrue(MessageShown());
            Assert.IsTrue(_label.ClassListContains("message--refusal"));
            Assert.AreEqual(new Color32(255, 255, 255, 255), LineColour, "a refusal is white");
            Assert.AreEqual(1f, _label.resolvedStyle.opacity, 0.01f);

            yield return new WaitForSecondsRealtime(1.7f);
            Assert.IsTrue(MessageShown(), "a refusal is still shown at 1.7 s");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsFalse(MessageShown(), "and hidden by 2.2 s: the refusal lasts 2 s, not the 3 of the survived line");
            Assert.IsTrue(_label.ClassListContains("message--refusal"), "the refusal class stays while the line fades");
            Assert.AreEqual(new Color32(255, 255, 255, 255), LineColour, "so it fades white");
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(0f, _label.resolvedStyle.opacity, 0.01f, "faded out after 0.3 s");
        }

        [UnityTest]
        public IEnumerator S4b_AC18_The_Survived_Line_Is_Gold_Lasts_3_Seconds_Replaces_A_Refusal_And_Drops_Its_Class()
        {
            ClickCell(_map.Spawn.X, _map.Spawn.Y);
            yield return null;
            Assert.IsTrue(_label.ClassListContains("message--refusal"));

            int ticks = 0;
            while (Battle.SurvivedWaves < 1 && ticks < 3000)
            {
                Battle.Tick();
                if (++ticks % 100 == 0) yield return null;
            }
            yield return Frames(3);
            Assert.IsTrue(MessageShown());
            Assert.AreEqual("Wave 1 survived +40", _label.text);
            Assert.IsFalse(_label.ClassListContains("message--refusal"), "the survived line is not a refusal");
            Assert.AreEqual(new Color32(255, 194, 61, 255), LineColour, "gold");

            yield return new WaitForSecondsRealtime(2.6f);
            Assert.IsTrue(MessageShown(), "the survived line is still shown at 2.6 s");
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.IsFalse(MessageShown(), "and hidden by 3.3 s");
            Assert.IsFalse(_label.ClassListContains("message--refusal"));
            Assert.AreEqual(new Color32(255, 194, 61, 255), LineColour, "it fades gold");

            // A refusal after it replaces it in its own style.
            ClickCell(5, 0);
            yield return null;
            Assert.IsTrue(MessageShown());
            Assert.IsTrue(_label.ClassListContains("message--refusal"));
            Assert.AreEqual("Cannot build on a wall.", _label.text);
            Assert.AreEqual(new Color32(255, 255, 255, 255), LineColour);
        }

        // ---- 4b-AC19 the end screen

        [UnityTest]
        public IEnumerator S4b_AC19_After_A_Defeat_The_End_Screen_Is_Not_Marked_Won_And_Has_The_Style_Of_The_Visual_Direction()
        {
            yield return TickUntil(() => Battle.Outcome == RunOutcome.Lost, 6000, 50);
            yield return Frames(3);

            VisualElement end = Root.Q<VisualElement>("end-screen");
            Label title = Root.Q<Label>("end-title");
            Button restart = Root.Q<Button>("restart-button");
            Assert.AreEqual(DisplayStyle.Flex, end.resolvedStyle.display);
            Assert.IsFalse(end.ClassListContains("end-screen--won"));
            Assert.AreEqual("Defeat", title.text);
            Assert.AreEqual(new Color32(255, 107, 94, 255), (Color32)title.resolvedStyle.color, "Defeat is red");
            Assert.AreEqual(48f, title.resolvedStyle.fontSize);
            AssertColor(new Color(20f / 255f, 19f / 255f, 17f / 255f, 0.8f), end.resolvedStyle.backgroundColor, "overlay", 0.005f);
            Assert.AreEqual(Root.worldBound.size, end.worldBound.size, "over everything");
            Assert.AreEqual(24f, restart.resolvedStyle.fontSize);
            Assert.AreEqual(new Color32(255, 255, 255, 255), (Color32)restart.resolvedStyle.borderTopColor);
            Assert.AreEqual(2f, restart.resolvedStyle.borderTopWidth, Snap);
            Assert.AreEqual(6f, restart.resolvedStyle.borderTopLeftRadius, Snap);
            Assert.AreEqual(10f, restart.resolvedStyle.paddingTop, Snap);
            Assert.AreEqual(32f, restart.resolvedStyle.paddingLeft, Snap);
            AssertColor(new Color(69f / 255f, 65f / 255f, 59f / 255f, 1f), restart.resolvedStyle.backgroundColor, "restart background", 0.005f);
            Assert.AreEqual(DisplayStyle.None, Root.Q<Label>("next-wave-label").resolvedStyle.display, "the countdown hides");
        }

        // ---- 4b-AC4 / 4b-AC11 pins through the scene

        [UnityTest]
        public IEnumerator S4b_AC11_Wave_1_Is_Shadow_With_45_Health_And_A_Light_Tower_Kills_Its_First_Enemy_On_The_Third_Hit()
        {
            ClickCell(1, 3);
            Assert.AreEqual(ElementOfTower(0), Board.TowerOn(new GridPos(1, 3)).Stats.Element, "Light is selected");
            yield return TickUntilFirstEnemy();
            Enemy enemy = Board.EnemyAt(0);
            Assert.AreEqual(ElementOfTower(1), enemy.Element, "wave 1 is Shadow");
            Assert.AreEqual(45f, enemy.MaxHealth);

            var healths = new List<float>();
            float last = enemy.Health;
            for (int tick = 0; tick < 600 && enemy.IsOnBoard; tick++)
            {
                Battle.Tick();
                if (enemy.Health != last) { healths.Add(enemy.Health); last = enemy.Health; }
                if (tick % 10 == 0) yield return null;
            }
            Assert.AreEqual(new[] { 25f, 5f, -15f }, healths.ToArray(), "45 - 20, 25 - 20, 5 - 20");
            Assert.IsFalse(enemy.IsOnBoard, "killed on the third hit");
            Assert.AreEqual(Battle.Lives, 10);
            Assert.AreEqual(StartingGold() - 40 + 10, Battle.Gold, "the kill pays 10");
        }

        [UnityTest]
        public IEnumerator S4b_AC11_The_Gold_Label_Follows_The_Tick_By_One_Frame_Through_A_Kill()
        {
            ClickCell(1, 3);
            ClickCell(1, 5);
            yield return null;
            int before = Battle.Gold;
            while (Battle.Gold == before) Battle.Tick(); // the first kill pays on the tick it lands
            Assert.AreEqual($"Gold {before}", GoldLabel.text, "no frame since the tick");
            yield return null;
            Assert.AreEqual($"Gold {Battle.Gold}", GoldLabel.text, "one frame later");
        }

        // ---- 4b-AC14 allocation of the views

        [UnityTest]
        public IEnumerator S4b_AC14_And_S4c_AC11_The_Views_Allocate_Nothing_On_A_Frame_Where_Nothing_Changes_With_A_Tower_Selected()
        {
            // A busy scene: towers of both elements, enemies of one wave, shots in flight, a hover on a cell.
            _input.Select(1);
            ClickCell(1, 3);
            _input.Select(0);
            ClickCell(1, 5);
            yield return TickUntil(() => Battle.ProjectileCount > 0 && Board.EnemyCount > 1, 3000, 20);
            ClickCell(1, 3); // 4c-AC11: a tower is selected and the panel is open, its Sell value already at the refund
            Assert.IsNotNull(_input.SelectedTower);
            _input.Hover(ScreenOf(new GridPos(8, 6)));
            yield return Frames(3);
            Assert.AreEqual(DisplayStyle.Flex, Root.Q<VisualElement>("tower-panel").resolvedStyle.display, "the panel is open");

            var views = new MonoBehaviour[]
            {
                Object.FindAnyObjectByType<CombatView>(),
                Object.FindAnyObjectByType<BoardView>(),
                Object.FindAnyObjectByType<RunHudView>(),
                Object.FindAnyObjectByType<TowerBarView>(),
                Object.FindAnyObjectByType<MessageView>(),
                Object.FindAnyObjectByType<BoardCameraFramer>(),
                Object.FindAnyObjectByType<TowerPanelView>(),
            };
            foreach (MonoBehaviour view in views) Assert.IsNotNull(view);

            foreach (MonoBehaviour view in views)
            {
                System.Action frame = FrameMethod(view);
                frame();
                frame(); // warm-up
                int count = Allocations.Of(frame);
                Assert.AreEqual(0, count, $"{view.GetType().Name} allocated {count} time(s) on a frame where nothing changed");
            }

            // The board view while the hover moves between cells.
            System.Action boardFrame = FrameMethod(views[1]);
            var a = ScreenOf(new GridPos(1, 3));
            var b = ScreenOf(new GridPos(8, 6));
            foreach (Vector2 position in new[] { b, a, b, a })
            {
                _input.Hover(position);
                int count = Allocations.Of(boardFrame);
                Assert.AreEqual(0, count, "BoardView allocated while the hover moved");
            }

            // The combat view across ticks that move enemies and shots.
            System.Action combatFrame = FrameMethod(views[0]);
            for (int tick = 0; tick < 120; tick++)
            {
                Battle.Tick();
                int count = Allocations.Of(combatFrame);
                Assert.AreEqual(0, count, $"CombatView allocated on tick {tick}");
            }
        }
    }
}
