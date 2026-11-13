using System;
using System.Collections;
using System.Collections.Generic;
using ElementalTD.Core;
using ElementalTD.Gameplay;
using ElementalTD.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ElementalTD.PlayModeTests
{
    /// <summary>The immune word (4d-AC9), the refusal at the cell (4d-AC10), the end of a run (4d-AC11) and picking (4d-AC12).</summary>
    public class FloatingTextSceneTests : FloatingSceneBase
    {
        private const string SpawnOrExitText = "Cannot build on the spawn or the exit.";
        private const string WallText = "Cannot build on a wall.";
        private const string EnemyText = "An enemy is in the way.";
        private const string PathText = "That would block the path.";
        private const string GoldText = "Not enough gold.";

        private Element WaveElement => Battle.Waves.WaveAt(0).GroupAt(0).Enemy.Element;

        // ---- 4d-AC9 the immune word

        /// <summary>The words the plan calls for now: one per enemy on the board with a mark younger than the hold, with its opacity.</summary>
        private List<(Enemy enemy, Drawn word)> AssertWords(string when)
        {
            float alpha = _bootstrap.TickAlpha;
            var expected = new List<(Enemy e, Vector2 top, float opacity)>();
            for (int i = 0; i < Board.EnemyCount; i++)
            {
                Enemy e = Board.EnemyAt(i);
                float age = (Battle.TickCount - e.ImmuneHitTick + alpha) * Dt;
                if (e.ImmuneHitTick == 0 || !(age < 1f)) continue;
                expected.Add((e, PanelOf(DrawnWorld(e) - new Vector3(0f, 0f, 0.35f * _map.CellSize)), Mathf.Clamp01((1f - age) / 0.2f)));
            }
            List<Drawn> drawn = DrawnWords();
            Assert.AreEqual(expected.Count, drawn.Count, $"{when}: a word per marked enemy, and no other");
            var pairs = new List<(Enemy, Drawn)>();
            foreach (var (e, top, opacity) in expected)
            {
                int found = drawn.FindIndex(d => Vector2.Distance(d.Bottom, top) <= PositionTolerance && Mathf.Abs(d.Opacity - opacity) <= 0.002f);
                Assert.GreaterOrEqual(found, 0, $"{when}: a word hung by its top centre {top} with opacity {opacity:F3} for the enemy of entry {e.Entry}");
                Assert.AreEqual("immune", drawn[found].Text);
                pairs.Add((e, drawn[found]));
                drawn.RemoveAt(found);
            }
            return pairs;
        }

        [UnityTest]
        public IEnumerator S4d_AC9_A_Tower_Of_The_Waves_Own_Element_Gives_One_Word_Under_The_Enemy_Opaque_Through_Four_Landings_Also_With_A_Second_Tower_And_No_Number()
        {
            BuyFree(2, 3, WaveElement);
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f);
            yield return null;
            int guard = 0;
            while (enemy.ImmuneHitTick == 0 && guard++ < 200)
            {
                Battle.Tick();
                yield return null;
                if (enemy.ImmuneHitTick == 0) Assert.AreEqual(0, DrawnWords().Count, "no word before the first landing");
            }
            Assert.AreNotEqual(0, enemy.ImmuneHitTick);
            Assert.AreEqual(0, Battle.HitCount, "an immune landing adds no record");
            Assert.AreEqual(0, VisibleNumbersOnly(), "and shows no number");
            var words = AssertWords("the frame after the first landing");
            Assert.AreEqual(1, words.Count);
            Drawn word = words[0].word;
            Assert.IsTrue(word.Label.ClassListContains("immune-word"));
            Assert.AreEqual(14f, word.Label.resolvedStyle.fontSize);
            Assert.AreEqual(FontStyle.Bold, word.Label.resolvedStyle.unityFontStyleAndWeight);
            AssertColor(Hex("D9D4C8"), word.Label.resolvedStyle.color, "word colour");
            AssertColor(new Color(30f / 255f, 28f / 255f, 26f / 255f, 0.8f), word.Label.resolvedStyle.backgroundColor, "plate", 0.01f);
            Assert.AreEqual(4f, word.Label.resolvedStyle.paddingLeft, Snap);
            Assert.AreEqual(4f, word.Label.resolvedStyle.paddingRight, Snap);
            Assert.AreEqual(3f, word.Label.resolvedStyle.borderTopLeftRadius, Snap);
            Assert.AreEqual(word.Bottom.x, word.Rect.center.x, PositionTolerance, "hung by its centre");
            Assert.AreEqual(word.Bottom.y, word.Rect.yMin, PositionTolerance, "hung by its top");
            Assert.AreEqual(1f, word.Opacity, 1e-4f);

            // Through the next three landings: still one word, opaque, on the enemy.
            int landings = 0, last = enemy.ImmuneHitTick, second = 0;
            guard = 0;
            while (landings < 3 && guard++ < 400)
            {
                Battle.Tick();
                if (guard == 20) { BuyFree(3, 3, WaveElement); second = Battle.TickCount; }
                yield return null;
                if (enemy.ImmuneHitTick != last) { landings++; last = enemy.ImmuneHitTick; }
                var pairs = AssertWords($"tick {Battle.TickCount}");
                Assert.AreEqual(1, pairs.Count, "one word however many towers hit it");
                Assert.AreEqual(1f, pairs[0].word.Opacity, 1e-4f, "opaque: the tower lands inside the hold");
                Assert.AreEqual(0, Battle.HitCount);
            }
            Assert.AreEqual(3, landings);
            Assert.Greater(second, 0, "the second tower stood during the landings");
        }

        private int VisibleNumbersOnly() => DrawnNumbers().Count;

        [UnityTest]
        public IEnumerator S4d_AC9_After_The_Towers_Are_Sold_The_Word_Is_Opaque_At_45_Ticks_Half_At_54_And_Hidden_From_60()
        {
            BuyFree(2, 3, WaveElement);
            BuyFree(1, 5, WaveElement);
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f);
            yield return null;
            int guard = 0;
            while (enemy.ImmuneHitTick == 0 && guard++ < 200) { Battle.Tick(); yield return null; }
            for (int i = 0; i < 5; i++) { Battle.Tick(); yield return null; }
            Assert.IsTrue(Battle.TrySellTower(new GridPos(2, 3)));
            Assert.IsTrue(Battle.TrySellTower(new GridPos(1, 5)));
            guard = 0;
            while (Battle.ProjectileCount > 0 && guard++ < 100) { Battle.Tick(); yield return null; }
            Assert.AreEqual(0, Battle.ProjectileCount, "the shots in flight landed");
            int mark = enemy.ImmuneHitTick;
            Assert.Greater(mark, 0);
            while (Battle.TickCount - mark < 45) { Battle.Tick(); yield return null; }
            Assert.AreEqual(mark, enemy.ImmuneHitTick, "no more landings");
            for (int age = 45; age <= 62; age++)
            {
                Assert.AreEqual(age, Battle.TickCount - mark);
                var words = AssertWords($"{age} ticks after the mark");
                if (age == 45) Assert.AreEqual(1f, words[0].word.Opacity, 1e-4f, "opaque at 45");
                if (age == 54) Assert.AreEqual(0.5f, words[0].word.Opacity, 0.01f, "half at 54");
                if (age >= 60) Assert.AreEqual(0, words.Count, $"hidden from 60 (age {age})");
                Battle.Tick();
                yield return null;
            }
        }

        private IEnumerator TwoEnemiesOnePastTheTower(Action<Enemy, Enemy> done)
        {
            BuyFree(2, 3, WaveElement);
            Enemy a = SpawnWave1Enemy(health: 1_000_000f);
            for (int i = 0; i < 60; i++) Battle.Tick();
            Enemy b = SpawnWave1Enemy(health: 1_000_000f);
            yield return null;
            int guard = 0;
            // Until the first has left the tower's range and the second is being hit: A's word is fading while B's is opaque.
            while (guard++ < 900)
            {
                Battle.Tick();
                yield return null;
                var words = AssertWords($"tick {Battle.TickCount}");
                if (words.Count == 2 && words.Exists(w => w.enemy == a && w.word.Opacity < 0.99f) && words.Exists(w => w.enemy == b && w.word.Opacity > 0.99f))
                {
                    done(a, b);
                    yield break;
                }
            }
            Assert.Fail("never saw the first word fading while the second is opaque");
        }

        [UnityTest]
        public IEnumerator S4d_AC9_Two_Enemies_Carry_Their_Own_Words_And_The_First_Fades_On_Its_Own_Time()
        {
            Enemy a = null, b = null;
            yield return TwoEnemiesOnePastTheTower((x, y) => { a = x; b = y; });
            Assert.AreNotEqual(a.ImmuneHitTick, b.ImmuneHitTick);
            Assert.Less(a.ImmuneHitTick, b.ImmuneHitTick);
        }

        [UnityTest]
        public IEnumerator S4d_AC9_A_Word_Belongs_To_Its_Enemy_Not_To_A_Place_In_The_List()
        {
            Enemy a = null, b = null;
            yield return TwoEnemiesOnePastTheTower((x, y) => { a = x; b = y; });
            Board.RemoveEnemy(a); // B moves to index 0 of the board; A's fading state must not follow the index
            yield return null;
            var words = AssertWords("the frame after the first enemy left");
            Assert.AreEqual(1, words.Count);
            Assert.AreSame(b, words[0].enemy);
            Assert.AreEqual(1f, words[0].word.Opacity, 1e-4f, "B's word is opaque, not A's fade");
        }

        [UnityTest]
        public IEnumerator S4d_AC9_An_Enemy_That_Exits_While_Its_Word_Shows_Leaves_No_Word_Behind()
        {
            BuyFree(14, 3, WaveElement);
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f);
            while (enemy.DistanceToExit > 5f) Battle.Tick();
            yield return null;
            Vector2 lastWord = default;
            bool sawWord = false;
            int guard = 0;
            while (OnBoard(enemy) && guard++ < 300)
            {
                Battle.Tick();
                yield return null;
                var words = AssertWords($"tick {Battle.TickCount}");
                if (words.Count == 1) { lastWord = words[0].word.Bottom; sawWord = true; }
            }
            Assert.IsFalse(OnBoard(enemy));
            Assert.IsTrue(sawWord, "the word showed before the exit");
            Assert.AreNotEqual(0, enemy.ImmuneHitTick);
            yield return null;
            var left = AssertWords("the frame after the exit");
            Assert.AreEqual(0, left.Count);
            Assert.AreEqual(0, DrawnWords().FindAll(w => Vector2.Distance(w.Bottom, lastWord) < 1f).Count, "no word stands where it was");
            Assert.IsNotNull(lastWord);
        }

        [UnityTest]
        public IEnumerator S4d_AC9_A_Tower_Of_The_Other_Element_On_A_Marked_Enemy_Shows_Numbers_And_The_Word_Together()
        {
            BuyFree(2, 3, WaveElement);
            BuyFree(1, 5, OtherElement(WaveElement));
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f);
            yield return null;
            bool both = false;
            for (int i = 0; i < 200 && !both; i++)
            {
                Battle.Tick();
                yield return null;
                var words = AssertWords($"tick {Battle.TickCount}");
                both = words.Count == 1 && DrawnNumbers().Count > 0;
            }
            Assert.IsTrue(both, "a number and the word on one frame");
            Assert.AreNotEqual(0, enemy.ImmuneHitTick);
            Assert.Greater(Battle.HitCount, 0);
        }

        [UnityTest]
        public IEnumerator S4d_AC9_AC12_The_Word_Pool_Grows_Past_Its_Prewarm_And_Every_Label_Of_Both_Pools_Ignores_Picking()
        {
            BuyFree(1, 3, WaveElement);
            for (int i = 0; i < 45; i++)
            {
                SpawnWave1Enemy(health: 1_000_000f, speed: 0.1f);
                Battle.Tick();
            }
            for (int i = 0; i < 60; i++) Battle.Tick();
            yield return Frames(2);
            Assert.Greater(WordLayer.childCount, 32, "the pool grew past its prewarm of 32");
            for (int i = 0; i < 45; i++) NumberView.Show(HitAtCell(3, 3, 20f, HitKind.Strong, OffBoard + i));
            yield return null;

            int checkedElements = 0;
            Root.Q<VisualElement>("floating-texts").Query<VisualElement>().ForEach(e =>
            {
                Assert.AreEqual(PickingMode.Ignore, e.pickingMode, $"{e.name} {e.GetType().Name}");
                checkedElements++;
            });
            Assert.Greater(checkedElements, 40 * 2 + 33 * 2);
            Assert.GreaterOrEqual(DrawnWords().Count, 1);
            foreach (Drawn d in DrawnNumbers().GetRange(0, 5))
            {
                VisualElement hit = Root.panel.Pick(d.Rect.center);
                Assert.IsTrue(hit == null || !Root.Q<VisualElement>("floating-texts").Contains(hit), "picking finds nothing of the floating texts");
            }
            foreach (Drawn d in DrawnWords())
            {
                VisualElement hit = Root.panel.Pick(d.Rect.center);
                Assert.IsTrue(hit == null || !Root.Q<VisualElement>("floating-texts").Contains(hit));
            }
        }

        // ---- 4d-AC10 the refusal at the cell

        private Vector2 RefusalAnchor()
        {
            Vector3 t = RefusalLabel.parent.resolvedStyle.translate;
            return new Vector2(t.x, t.y);
        }

        private Rect BoardAreaRect => Root.Q<VisualElement>("board-area").worldBound;

        private IEnumerator ClickAndFrame(int x, int y)
        {
            ClickCell(x, y);
            yield return null;
        }

        [UnityTest]
        public IEnumerator S4d_AC10_Reasons_Show_Their_Text_At_The_Clicked_Cell_In_The_Directions_Look()
        {
            yield return null;
            var cases = new List<(string name, GridPos cell, string text)>
            {
                ("wall", new GridPos(5, 3), WallText),
                ("spawn", new GridPos(0, 4), SpawnOrExitText),
            };
            // An enemy in the way: the cell it is heading to.
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f);
            Battle.Tick();
            yield return null;
            cases.Add(("enemy", enemy.Follower.To, EnemyText));
            foreach (var (name, cell, text) in cases)
            {
                float t0 = Time.unscaledTime;
                ClickCell(cell.X, cell.Y);
                yield return null;
                Assert.IsTrue(RefusalShown(), name);
                Assert.AreEqual(text, RefusalText, name);
                Assert.IsFalse(MessageShown(), name + ": nothing on the message line");
                Assert.IsEmpty(_label.text);
                IResolvedStyle r = RefusalLabel.resolvedStyle;
                Assert.AreEqual(18f, r.fontSize);
                Assert.AreEqual(FontStyle.Bold, r.unityFontStyleAndWeight);
                AssertColor(Color.white, r.color, "refusal colour");
                Assert.AreEqual(1f, r.unityTextOutlineWidth);
                AssertColor(Color.black, r.unityTextOutlineColor, "outline colour");
                // Expected spot: 0.6 cells above the cell centre, raised by the rise so far, kept inside the area by 8.
                float age = Time.unscaledTime - t0;
                Vector2 start = PanelOfCell(cell.X, cell.Y, 0.6f);
                Vector2 expectedBottom = ExpectedRefusalBottom(start, age, RefusalLabel.worldBound.size);
                Vector2 actual = RefusalAnchor();
                Assert.AreEqual(expectedBottom.x, actual.x, 0.7f, name + " x");
                Assert.AreEqual(expectedBottom.y, actual.y, 0.7f, name + " y");
                yield return new WaitForSecondsRealtime(1.6f);
                Assert.IsFalse(RefusalShown(), name + ": hidden after its life");
            }
        }

        private Vector2 ExpectedRefusalBottom(Vector2 start, float age, Vector2 size)
        {
            Rect area = BoardAreaRect;
            Vector2 p = start;
            p.y -= 16f * age / 1.5f;
            p.x = Mathf.Max(area.xMin + 8f + size.x / 2f, Mathf.Min(p.x, area.xMax - 8f - size.x / 2f));
            p.y = Mathf.Max(area.yMin + 8f + size.y, Mathf.Min(p.y, area.yMax - 8f));
            return p;
        }

        [UnityTest]
        public IEnumerator S4d_AC10_Path_And_Gold_Reasons_Show_Their_Texts_And_The_Gold_Is_Checked_First()
        {
            yield return null;
            ClickCell(5, 7);
            yield return null;
            Assert.IsFalse(RefusalShown(), "a placement shows no refusal");
            ClickCell(5, 8);
            yield return null;
            Assert.AreEqual(PathText, RefusalText);
            Assert.IsTrue(RefusalShown());
            Assert.IsFalse(MessageShown());
            for (int x = 1; Battle.Gold >= _input.Tower.ToStats().Cost; x++) ClickCell(x, 1);
            ClickCell(7, 6);
            yield return null;
            Assert.AreEqual(GoldText, RefusalText);
            Assert.AreEqual(PanelOfCell(7, 6, 0.6f).x, RefusalAnchor().x, 0.7f, "at the cell of the click");
        }

        [UnityTest]
        public IEnumerator S4d_AC10_It_Rises_16_And_Fades_In_Real_Seconds_Is_Hidden_After_Its_Life_And_A_Newer_One_Restarts_It_At_Its_Own_Cell()
        {
            yield return null;
            Vector2 first = PanelOfCell(5, 3, 0.6f);
            float t0 = Time.unscaledTime;
            ClickCell(5, 3);
            yield return null;
            Assert.AreEqual(1f, RefusalLabel.parent.resolvedStyle.opacity, 0.01f);
            float y0 = RefusalAnchor().y;
            yield return new WaitForSecondsRealtime(1.0f);
            float age = Time.unscaledTime - t0;
            Assert.AreEqual(y0 - 16f * (age - 0.02f) / 1.5f, RefusalAnchor().y, 0.7f, "it rises 16 over 1.5 s");
            Assert.AreEqual(1f, RefusalLabel.parent.resolvedStyle.opacity, 0.01f, "opaque until the fade starts at 1.1 s");
            yield return new WaitForSecondsRealtime(0.3f);
            age = Time.unscaledTime - t0;
            Assert.AreEqual(Mathf.Clamp01((1.5f - age) / 0.4f), RefusalLabel.parent.resolvedStyle.opacity, 0.12f, "fading linearly over the last 0.4 s");
            Assert.Less(RefusalLabel.parent.resolvedStyle.opacity, 1f);

            // A newer refusal on another cell: its text, its cell, a full life.
            ClickCell(10, 6);
            yield return null;
            Assert.AreEqual(WallText, RefusalText);
            Assert.AreEqual(PanelOfCell(10, 6, 0.6f).x, RefusalAnchor().x, 0.7f);
            Assert.AreEqual(1f, RefusalLabel.parent.resolvedStyle.opacity, 0.01f, "restarted: opaque again");
            yield return new WaitForSecondsRealtime(1.0f);
            Assert.IsTrue(RefusalShown(), "a full life from the newer refusal");
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.IsFalse(RefusalShown(), "hidden after its life");
            Assert.AreNotEqual(first.x, PanelOfCell(10, 6, 0.6f).x);
        }

        [UnityTest]
        public IEnumerator S4d_AC10_It_Is_Kept_8_Inside_The_Board_Area_On_The_First_Frame_At_The_Edges_At_Every_Aspect(
            [Values(0f, 1f, 2.5f)] float aspect)
        {
            yield return null;
            if (aspect > 0f) _camera.aspect = aspect;
            yield return Frames(2);
            // A short text first, then a long one on the next click: the clamp must not read the old layout.
            for (int x = 1; Battle.Gold >= _input.Tower.ToStats().Cost; x++) ClickCell(x, 1);
            var cells = new List<(GridPos cell, string text)>
            {
                (new GridPos(15, 8), GoldText),            // top-right corner
                (new GridPos(15, 4), SpawnOrExitText),     // right column, longest text, straight after a short one
                (new GridPos(0, 0), GoldText),             // bottom-left corner
                (new GridPos(0, 4), SpawnOrExitText),      // left column
                (new GridPos(10, 8), WallText),            // top row wall (gold is checked first, so see below)
            };
            int pinned = 0;
            foreach (var (cell, text) in cells)
            {
                ClickCell(cell.X, cell.Y);
                yield return null;
                Assert.IsTrue(RefusalShown());
                Rect area = BoardAreaRect;
                Rect r = RefusalLabel.worldBound;
                string where = $"{cell} aspect {aspect}: {RefusalText}";
                Assert.GreaterOrEqual(r.xMin, area.xMin + 8f - 0.5f, where + " left");
                Assert.LessOrEqual(r.xMax, area.xMax - 8f + 0.5f, where + " right");
                Assert.GreaterOrEqual(r.yMin, area.yMin + 8f - 0.5f, where + " top");
                Assert.LessOrEqual(r.yMax, area.yMax - 8f + 0.5f, where + " bottom");
                Vector2 start = PanelOfCell(cell.X, cell.Y, 0.6f);
                if (start.x + r.width / 2f > area.xMax - 8f) { Assert.AreEqual(area.xMax - 8f, r.xMax, 0.5f, where + ": pinned at the right margin exactly"); pinned++; }
                if (start.x - r.width / 2f < area.xMin + 8f) { Assert.AreEqual(area.xMin + 8f, r.xMin, 0.5f, where + ": pinned at the left margin exactly"); pinned++; }
                if (start.y - r.height < area.yMin + 8f) { Assert.AreEqual(area.yMin + 8f, r.yMin, 1.0f, where + ": pinned at the top margin"); pinned++; }
                yield return null;
            }
            Assert.GreaterOrEqual(pinned, 1, "the unclamped point was outside on some of these cells");
            _camera.ResetAspect();
        }

        [UnityTest]
        public IEnumerator S4d_AC10_On_The_Top_Row_It_Does_Not_Rise_And_Still_Fades()
        {
            _camera.aspect = 2.5f; // the board then fills the area's height, so the top row lies under the top bar
            yield return Frames(2);
            for (int x = 1; Battle.Gold >= _input.Tower.ToStats().Cost; x++) ClickCell(x, 1);
            ClickCell(7, 8);
            yield return null;
            Assert.AreEqual(GoldText, RefusalText);
            float y0 = RefusalAnchor().y;
            yield return new WaitForSecondsRealtime(1.0f);
            Assert.AreEqual(y0, RefusalAnchor().y, 0.5f, "pinned over the upper half of the cell: it does not rise");
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.Less(RefusalLabel.parent.resolvedStyle.opacity, 1f, "it still fades");
            _camera.ResetAspect();
        }

        [UnityTest]
        public IEnumerator S4d_AC10_After_The_Aspect_Changes_It_Is_At_Its_Place_On_The_First_Frame()
        {
            yield return null;
            ClickCell(5, 3); // a wall
            yield return null;
            Assert.IsTrue(RefusalShown());
            _camera.aspect = 2.5f;
            yield return null;
            Rect area = BoardAreaRect;
            Rect r = RefusalLabel.worldBound;
            float age = 0.05f;
            Vector2 expected = ExpectedRefusalBottom(PanelOfCell(5, 3, 0.6f), age, r.size);
            Assert.AreEqual(expected.x, RefusalAnchor().x, 1f, "x under the new camera on the first frame");
            Assert.AreEqual(expected.y, RefusalAnchor().y, 1f, "y under the new camera on the first frame");
            Assert.IsTrue(area.Contains(r.center));
            _camera.ResetAspect();
        }

        // ---- 4d-AC16 (Known limits) the layer is clipped at the board area's right edge

        private VisualElement TextLayer => Root.Q<VisualElement>("floating-texts");

        /// <summary>The element's world clip (internal in UI Toolkit, read by reflection): where its contents are cut. It is the layer's own bounds when the layer has overflow: hidden, and wider otherwise.</summary>
        private static Rect ContentClip(VisualElement e)
        {
            var property = typeof(VisualElement).GetProperty("worldClip", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            Assert.IsNotNull(property, "VisualElement.worldClip");
            return (Rect)property.GetValue(e);
        }

        private static bool Inside(Rect inner, Rect outer, float t = 0.5f) =>
            inner.xMin >= outer.xMin - t && inner.xMax <= outer.xMax + t && inner.yMin >= outer.yMin - t && inner.yMax <= outer.yMax + t;

        [UnityTest]
        public IEnumerator S4d_AC16_The_Text_Layer_Clips_At_The_Board_Areas_Right_Edge_And_Keeps_The_Panels_Origin([Values(0f, 1f, 1.7778f, 2.5f)] float aspect)
        {
            if (aspect > 0f) _camera.aspect = aspect;
            yield return Frames(2);
            Rect panel = Root.worldBound, area = BoardAreaRect, layer = TextLayer.worldBound;
            string where = $"aspect {aspect}";
            Assert.AreEqual(area.xMax, layer.xMax, 0.5f, where + ": right edge = board-area's right edge");
            Assert.AreEqual(panel.xMin, layer.xMin, 0.5f, where + ": left = the panel's");
            Assert.AreEqual(panel.yMin, layer.yMin, 0.5f, where + ": top = the panel's");
            Assert.AreEqual(panel.yMax, layer.yMax, 0.5f, where + ": bottom = the panel's");
            Assert.Less(layer.xMax, panel.xMax - 100f, where + ": the side column lies outside the layer");
            // overflow: hidden resolves as a clip of the layer's contents at its own bounds (a style with no clip leaves worldClip wider than the layer).
            Rect clip = ContentClip(TextLayer);
            Assert.AreEqual(layer.xMax, clip.xMax, 0.5f, where + ": overflow hidden: the clip stops at the layer's right edge");
            Assert.LessOrEqual(clip.xMax, area.xMax + 0.5f, where + ": and so at the board area's");
            // The origin did not move: a number's bottom centre still equals its translate (grown about its bottom centre, so the scale does not shift it).
            NumberView.Show(HitAtCell(3, 3, 20f, HitKind.Strong, OffBoard + 1));
            yield return null;
            for (int i = 0; i < 6; i++) { Battle.Tick(); yield return null; }
            var numbers = DrawnNumbers();
            Assert.AreEqual(1, numbers.Count, where);
            Assert.AreEqual(numbers[0].Bottom.x, numbers[0].Rect.center.x, 0.5f, where + ": worldBound bottom centre x = translate x");
            Assert.AreEqual(numbers[0].Bottom.y, numbers[0].Rect.yMax, 0.5f, where + ": worldBound bottom = translate y");
            _camera.ResetAspect();
        }

        [UnityTest]
        public IEnumerator S4d_AC16_A_Refusal_On_The_Last_Column_Or_A_Corner_Cell_Lies_Inside_The_Clipped_Layer([Values(0f, 1f, 1.7778f, 2.5f)] float aspect)
        {
            yield return null;
            if (aspect > 0f) _camera.aspect = aspect;
            yield return Frames(2);
            for (int x = 1; Battle.Gold >= _input.Tower.ToStats().Cost; x++) ClickCell(x, 1);
            foreach (var cell in new[] { new GridPos(15, 8), new GridPos(15, 4), new GridPos(15, 0), new GridPos(0, 0), new GridPos(0, 8), new GridPos(14, 8) })
            {
                ClickCell(cell.X, cell.Y);
                yield return null;
                Assert.IsTrue(RefusalShown(), $"{cell}");
                Rect layer = TextLayer.worldBound, r = RefusalLabel.worldBound;
                Assert.IsTrue(Inside(r, layer), $"{cell} aspect {aspect}: refusal {r} inside the layer {layer} (nothing of it cut)");
                Assert.IsTrue(Inside(r, ContentClip(TextLayer)), $"{cell} aspect {aspect}: and inside the clip {ContentClip(TextLayer)}");
                yield return null;
            }
            _camera.ResetAspect();
        }

        [UnityTest]
        public IEnumerator S4d_AC16_An_Immune_Word_Under_An_Enemy_On_The_Bottom_Row_Lies_Inside_The_Clipped_Layer([Values(0f, 1f, 1.7778f, 2.5f)] float aspect)
        {
            yield return null;
            if (aspect > 0f) _camera.aspect = aspect;
            BuyFree(10, 1, WaveElement); // same element as the wave: immune, a word per landing; the tower closes row 1 of column 10, so the enemy passes it on row 0
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f, speed: 1.5f);
            int lowFramesWithWord = 0, guard = 0, anyWord = 0;
            float minY = 99f;
            while (OnBoard(enemy) && guard++ < 3000)
            {
                Battle.Tick();
                yield return null;
                var words = DrawnWords();
                minY = Mathf.Min(minY, enemy.Y);
                if (words.Count > 0) anyWord++;
                Rect layer = TextLayer.worldBound;
                foreach (Drawn w in words)
                {
                    Assert.IsTrue(Inside(w.Rect, layer), $"aspect {aspect}, enemy at ({enemy.X:F2}, {enemy.Y:F2}): word {w.Rect} inside the layer {layer}");
                    if (enemy.Y < 0.5f) lowFramesWithWord++;
                }
            }
            Assert.Greater(lowFramesWithWord, 0, $"aspect {aspect}: a word showed while the enemy was on the bottom row (lowest Y {minY:F2}, {anyWord} frames with a word, {guard} frames)");
            _camera.ResetAspect();
        }

        // ---- 4d-AC11 end of a run

        private void AssertNothingFloats(string when)
        {
            Assert.AreEqual(0, DrawnNumbers().Count, when + ": numbers");
            Assert.AreEqual(0, DrawnWords().Count, when + ": words");
            Assert.IsFalse(RefusalShown(), when + ": refusal");
            foreach (VisualElement layer in new[] { NumberLayer, WordLayer })
                for (int i = 0; i < layer.childCount; i++)
                    Assert.AreEqual(Visibility.Hidden, layer[i].resolvedStyle.visibility, $"{when}: anchor {i} of {layer.name}");
            Assert.AreEqual(Visibility.Hidden, RefusalLabel.parent.resolvedStyle.visibility, when + ": refusal anchor");
        }

        private IEnumerator RestartScene()
        {
            var old = _bootstrap;
            _bootstrap.Restart();
            int guard = 0;
            while (old != null && guard++ < 120) yield return null;
            Assert.IsTrue(old == null, "the scene never reloaded");
            yield return null;
            AcquireScene(disableInput: true);
            yield return Frames(3);
        }

        private IEnumerator AssertAllThreeShowAfterRestart()
        {
            yield return RestartScene();
            BuyFree(2, 3, WaveElement);
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f);
            ClickCell(5, 0);
            int guard = 0;
            while (enemy.ImmuneHitTick == 0 && guard++ < 200) { Battle.Tick(); yield return null; }
            NumberView.Show(HitOn(enemy, 20f, HitKind.Strong));
            yield return null;
            Assert.AreEqual(1, DrawnNumbers().Count, "a number shows again after a restart");
            Assert.AreEqual(1, DrawnWords().Count, "a word shows again");
            Assert.IsTrue(RefusalShown(), "a refusal shows again");
        }

        [UnityTest]
        public IEnumerator S4d_AC11_On_The_Frame_After_A_Defeat_No_Number_Word_Or_Refusal_Shows_And_All_Three_Work_After_A_Restart()
        {
            var fast = new EnemyStats(30f, 1_000_000f, 0, default);
            Board.SpawnEnemy(new EnemyStats(0.3f, 1_000_000f, 0, WaveElement));
            Battle.TryBuyTower(new GridPos(1, 3), new TowerStats(2.5f, 10f, 30f, 100f, 0, WaveElement));
            yield return null;
            int spawned = 0, guard = 0;
            while (Battle.Lives > 1 && guard++ < 2000)
            {
                if (guard % 3 == 0 && spawned < 10) { Board.SpawnEnemy(fast); spawned++; }
                Battle.Tick();
                if (guard % 5 == 0) yield return null;
            }
            Assert.AreEqual(1, Battle.Lives);
            Assert.AreEqual(RunOutcome.Running, Battle.Outcome);
            Enemy slow = Board.EnemyAt(0);
            Battle.Tick();
            ClickCell(5, 0);
            NumberView.Show(HitOn(slow, 20f, HitKind.Strong));
            yield return null;
            Assert.AreEqual(1, DrawnNumbers().Count, "a number is on screen before the end");
            Assert.IsTrue(DrawnWords().Count >= 1, "a word is on screen before the end");
            Assert.IsTrue(RefusalShown(), "a refusal is on screen before the end");

            guard = 0;
            while (Battle.Outcome == RunOutcome.Running && guard++ < 2000) Battle.Tick();
            Assert.AreEqual(RunOutcome.Lost, Battle.Outcome);
            yield return null;
            AssertNothingFloats("the frame after the defeat");
            yield return new WaitForSecondsRealtime(1.0f);
            AssertNothingFloats("a second later");
            yield return AssertAllThreeShowAfterRestart();
        }

        [UnityTest]
        public IEnumerator S4d_AC11_On_The_Frame_After_A_Win_No_Number_Word_Or_Refusal_Shows()
        {
            yield return null;
            int ticks = 0, frames = 0;
            bool allThree = false, lastHadSomething = false;
            while (Battle.Outcome == RunOutcome.Running && ticks < 14000)
            {
                bool lastWave = Battle.Waves.StartedWaves == Battle.Waves.WaveCount;
                int step = lastWave ? 1 : 30;
                for (int i = 0; i < step && Battle.Outcome == RunOutcome.Running; i++, ticks++)
                {
                    RunReferenceActionsAt(ticks);
                    Battle.Tick();
                }
                if (Battle.Outcome != RunOutcome.Running) break;
                if (lastWave && frames++ % 40 == 0) ClickCell(5, 0);
                yield return null;
                if (!lastWave) continue;
                bool numbers = DrawnNumbers().Count > 0, words = DrawnWords().Count > 0, refusal = RefusalShown();
                allThree |= numbers && words && refusal;
                lastHadSomething = refusal && (numbers || words);
            }
            Assert.AreEqual(RunOutcome.Won, Battle.Outcome);
            Assert.IsTrue(allThree, "a number, a word and a refusal were on one frame in the last wave");
            Assert.IsTrue(lastHadSomething, "and floating text was still on screen on the frame before the winning tick");
            yield return null;
            AssertNothingFloats("the frame after the win");
            yield return new WaitForSecondsRealtime(1.0f);
            AssertNothingFloats("a second later");
            yield return AssertAllThreeShowAfterRestart();
        }

        // ---- 4d-AC12 clicks

        [UnityTest]
        public IEnumerator S4d_AC12_A_Hover_Or_Click_On_A_Cell_Under_A_Number_A_Word_Or_A_Refusal_Does_What_It_Does_Without_It()
        {
            BuyFree(2, 3, WaveElement);
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f);
            int guard = 0;
            while (enemy.ImmuneHitTick == 0 && guard++ < 200) { Battle.Tick(); yield return null; }
            NumberView.Show(HitOn(enemy, 20f, HitKind.Strong));
            NumberView.Show(HitAtCell(8, 6, 20f, HitKind.Strong, OffBoard));
            ClickCell(10, 6); // a refusal at a wall cell
            yield return null;
            Battle.Tick();
            yield return null;
            var targets = new List<(string what, VisualElement element)>();
            foreach (Drawn d in DrawnNumbers()) targets.Add(("number", d.Label));
            foreach (Drawn d in DrawnWords()) targets.Add(("word", d.Label));
            targets.Add(("refusal", RefusalLabel));
            Assert.GreaterOrEqual(targets.Count, 3);

            var ground = new Plane(Vector3.up, Vector3.zero);
            int clicked = 0;
            foreach (var (what, element) in targets)
            {
                Vector2 screen = ScreenCentreOf(element);
                Ray ray = _camera.ScreenPointToRay(screen);
                Assert.IsTrue(ground.Raycast(ray, out float distance));
                GridPos expected = CellOf(ray.GetPoint(distance));
                _input.Hover(screen);
                Assert.IsTrue(_input.HasHoverCell == Board.Map.InBounds(expected), what);
                if (_input.HasHoverCell) Assert.AreEqual(expected, _input.HoverCell, $"{what}: the hover cell is the cell under the text");

                int towers = Board.TowerCount;
                var refused = new List<GridPos>();
                Action<PlacementResult, GridPos> record = (_, c) => refused.Add(c);
                _input.PlacementRefused += record;
                bool hadTower = Board.HasTower(expected);
                _input.Click(screen);
                _input.PlacementRefused -= record;
                if (!Board.Map.InBounds(expected)) { Assert.AreEqual(towers, Board.TowerCount); continue; }
                if (hadTower) Assert.AreSame(Board.TowerOn(expected), _input.SelectedTower, $"{what}: a click on a tower selects it");
                else if (_input.SelectedTower != null) Assert.AreEqual(0, refused.Count);
                else Assert.IsTrue(Board.HasTower(expected) || (refused.Count == 1 && refused[0] == expected), $"{what}: the purchase of that cell");
                _input.Deselect();
                clicked++;
            }
            Assert.Greater(clicked, 0);
            Root.Q<VisualElement>("floating-texts").Query<VisualElement>().ForEach(e => Assert.AreEqual(PickingMode.Ignore, e.pickingMode));
        }
    }
}
