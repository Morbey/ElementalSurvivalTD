using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ElementalTD.Core;
using ElementalTD.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ElementalTD.PlayModeTests
{
    /// <summary>
    /// Step 5a on the scene: the XP row of docs/art/visual-direction.md section 1.6 and how it follows the battle. The table is the balance
    /// document's (30, 40, 70 ...); every test reads the row on the frame after its ticks, with the time scale at 0 and the battle ticked by hand.
    /// </summary>
    public class XpBarSceneTests : SceneTestBase
    {
        private const string PopClass = "xp-row--pop";
        private const string MaxClass = "xp-row--max";
        private static readonly int[] Table = { 30, 40, 70, 90, 110, 130, 150, 170, 190, 210 };

        private Progression P => Battle.Progression;
        private VisualElement XpRow => Root.Q<VisualElement>("xp-row");
        private Label LevelLabel => Root.Q<Label>("level-label");
        private VisualElement Track => Root.Q<VisualElement>("xp-track");
        private VisualElement Fill => Root.Q<VisualElement>("xp-fill");
        private VisualElement BottomBar => Root.Q<VisualElement>("bottom-bar");
        private bool Popping => XpRow.ClassListContains(PopClass);
        private float WrittenScale => Fill.style.scale.value.value.x;
        private float ResolvedScale => Fill.resolvedStyle.scale.value.x;

        /// <summary>Kills one enemy of the given XP with a free tower beside the spawn, ticking by hand and drawing no frame.</summary>
        private void KillOne(int xp = 10)
        {
            if (Board.TowerOn(new GridPos(1, 3)) == null)
            {
                TowerStats s = _input.TowerAt(0).ToStats();
                Assert.AreEqual(PlacementResult.Placed, Battle.TryBuyTower(new GridPos(1, 3), new TowerStats(s.Range, s.Damage, s.ShotsPerSecond, s.ProjectileSpeed, 0, s.Element)));
            }
            Board.SpawnEnemy(new EnemyStats(0f, 1f, 0, default, xp));
            int before = P.Xp, guard = 0;
            while (P.Xp == before && guard++ < 400) Battle.Tick();
            Assert.AreEqual(before + xp, P.Xp, "the enemy was killed");
        }

        private static void AssertDurations(VisualElement element, float seconds, string what)
        {
            List<TimeValue> durations = element.resolvedStyle.transitionDuration.ToList();
            Assert.AreEqual(1, durations.Count, what + ": one transition");
            Assert.AreEqual(TimeUnit.Second, durations[0].unit, what);
            Assert.AreEqual(seconds, durations[0].value, 1e-4f, what);
        }

        private static string LevelText(int level) => $"Level {level}";

        // ---- 5a-AC4 The scene's battle

        [UnityTest]
        public IEnumerator S5a_AC4_The_Scenes_Battle_Has_The_Documents_Table_And_Starts_At_Level_1_With_No_XP()
        {
            yield return Frames(2);
            Assert.AreEqual((0, 1, 0, 30, false), (P.Xp, P.Level, P.XpIntoLevel, P.XpForLevel, P.TableEnded));
            for (int i = 0; i < Table.Length; i++)
            {
                Assert.AreEqual(Table[i], P.XpForLevel, $"item {i + 1}");
                Assert.AreEqual(i + 1, P.Level);
                P.AddXp(Table[i]);
            }
            Assert.AreEqual((11, true, 1190), (P.Level, P.TableEnded, P.Xp));
            Assert.IsNotNull(Object.FindAnyObjectByType<XpBarView>());
            Assert.IsTrue(Object.FindAnyObjectByType<XpBarView>().enabled, "the view found its document, row and battle");
        }

        // ---- 5a-AC5 The row

        [UnityTest]
        public IEnumerator S5a_AC5_The_Row_Has_The_Sizes_Colours_Min_Width_And_Origin_Of_Section_1_6()
        {
            yield return Frames(3);
            VisualElement bar = BottomBar, row = XpRow, track = Track, fill = Fill;
            Label label = LevelLabel;

            Assert.AreEqual(72f, bar.resolvedStyle.height, Snap, "the bottom bar keeps its height");
            Assert.AreEqual(FlexDirection.Column, bar.resolvedStyle.flexDirection);
            Assert.AreEqual(FlexDirection.Row, row.resolvedStyle.flexDirection);
            Assert.AreEqual(20f, row.resolvedStyle.height, Snap, "xp row height");
            Assert.AreEqual(8f, row.resolvedStyle.marginTop, Snap, "xp row margin-top");
            Assert.AreEqual(8f, row.worldBound.yMin - bar.worldBound.yMin, Snap, "the row sits 8 below the bar's top");
            Assert.AreEqual(bar.worldBound.xMin + 16f, row.worldBound.xMin, Snap, "across the bar's content width, left");
            Assert.AreEqual(bar.worldBound.xMax - 16f, row.worldBound.xMax, Snap, "and right");

            Assert.AreEqual(16f, label.resolvedStyle.fontSize);
            Assert.AreEqual(FontStyle.Bold, label.resolvedStyle.unityFontStyleAndWeight);
            AssertColor(Color.white, label.resolvedStyle.color, "label colour");
            Assert.AreEqual(108f, label.layout.width, Snap, "the label holds its minimum width");
            Assert.AreEqual(0f, label.resolvedStyle.transformOrigin.x, 0.01f, "the label scales about its left");
            Assert.AreEqual(label.layout.height / 2f, label.resolvedStyle.transformOrigin.y, 0.5f, "and its centre");
            Assert.AreEqual(label.layout.xMin + 108f, track.layout.xMin, Snap, "the track starts after the label's width");

            Assert.AreEqual(10f, track.resolvedStyle.height, Snap, "track height");
            AssertColor(Hex("45413B"), track.resolvedStyle.backgroundColor, "track colour");
            Assert.AreEqual(0f, track.resolvedStyle.borderTopWidth + track.resolvedStyle.borderLeftWidth + track.resolvedStyle.borderRightWidth + track.resolvedStyle.borderBottomWidth, "no border");
            Assert.AreEqual(0f, track.resolvedStyle.borderTopLeftRadius, "radius 0");
            Assert.AreEqual(row.worldBound.xMax, track.worldBound.xMax, Snap, "the track takes the rest of the row");
            Assert.Greater(track.layout.width, 400f);

            Assert.AreEqual(Position.Absolute, fill.resolvedStyle.position);
            Assert.AreEqual((track.layout.width, track.layout.height), (fill.layout.width, fill.layout.height), "the fill covers the whole track");
            AssertColor(Hex("F4F1EA"), fill.resolvedStyle.backgroundColor, "fill colour");
            Assert.AreEqual(0f, fill.resolvedStyle.borderTopLeftRadius, "fill radius 0");
            Assert.AreEqual(0f, fill.resolvedStyle.transformOrigin.x, 0.01f, "the fill grows from its left");
            Assert.AreEqual(fill.layout.height / 2f, fill.resolvedStyle.transformOrigin.y, 0.5f);
            AssertColor(Hex("2E2C29"), bar.resolvedStyle.backgroundColor, "the bar's colour is unchanged");
        }

        [UnityTest]
        public IEnumerator S5a_AC5_The_Gold_Is_50_Below_The_Bars_Top_And_The_Message_Is_Beside_It_And_The_Rows_Lie_Inside_The_Bar_Without_Overlap()
        {
            yield return Frames(3);
            _label.text = "Wave 10 survived +60"; // the longest line the run can show; the label is empty until a message comes
            yield return null;
            Rect bar = BottomBar.worldBound, xp = XpRow.worldBound, economy = Root.Q<VisualElement>("economy-row").worldBound;
            Rect gold = GoldLabel.worldBound, message = _label.worldBound;
            float messageText = _label.MeasureTextSize(_label.text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined).x;
            Assert.LessOrEqual(messageText, _label.layout.width, "the longest message fits beside the gold");

            Assert.AreEqual(50f, gold.center.y - bar.yMin, Snap, "the gold's centre");
            Assert.AreEqual(24f, GoldLabel.resolvedStyle.fontSize);
            AssertColor(Hex("FFC23D"), GoldLabel.resolvedStyle.color, "gold colour");
            Assert.AreEqual(20f, _label.resolvedStyle.fontSize);

            Assert.GreaterOrEqual(message.xMin, gold.xMax - 0.01f, "the message line is beside the gold, to its right");
            Assert.Greater(Mathf.Min(message.yMax, gold.yMax) - Mathf.Max(message.yMin, gold.yMin), 5f, "on the same line");
            foreach ((string name, Rect rect) in new[] { ("xp row", xp), ("economy row", economy), ("gold", gold), ("message", message) })
            {
                Assert.GreaterOrEqual(rect.xMin, bar.xMin - 0.01f, name + " left");
                Assert.LessOrEqual(rect.xMax, bar.xMax + 0.01f, name + " right");
                Assert.GreaterOrEqual(rect.yMin, bar.yMin - 0.01f, name + " top");
                Assert.LessOrEqual(rect.yMax, bar.yMax + 0.01f, name + " bottom");
            }
            Assert.LessOrEqual(xp.yMax, economy.yMin + 0.01f, "the XP row is above the economy row");
            Assert.LessOrEqual(xp.yMax, gold.yMin + 0.01f, "and above the gold");
            Assert.LessOrEqual(xp.yMax, message.yMin + 0.01f, "and above the message line");
            Assert.LessOrEqual(Mathf.Max(gold.yMax, message.yMax), bar.yMax + 0.01f);
        }

        [UnityTest]
        public IEnumerator S5a_AC5_The_Board_Area_Is_What_The_Bars_Leave_The_Bottom_Bar_Is_72_And_The_Rest_Of_The_HUD_Is_Where_It_Was()
        {
            yield return Frames(3);
            Rect root = Root.worldBound, area = Root.Q<VisualElement>("board-area").worldBound;
            Rect top = Root.Q<VisualElement>("top-bar").worldBound, bottom = BottomBar.worldBound, side = Root.Q<VisualElement>("side-column").worldBound;

            Assert.AreEqual(44f, top.height, Snap, "the top bar");
            Assert.AreEqual(72f, bottom.height, Snap, "the bottom bar");
            Assert.AreEqual(216f, side.width, Snap, "the side column");
            Assert.AreEqual(bottom.yMin, area.yMax, 0.01f, "the board area ends where the bottom bar starts");
            Assert.AreEqual(top.yMax, area.yMin, 0.01f, "and starts under the top bar");
            Assert.AreEqual(root.yMax, bottom.yMax, 0.01f);
            Assert.AreEqual(side.xMin, area.xMax, 0.01f);
            Assert.AreEqual(root.xMin, area.xMin, 0.01f);
        }

        [UnityTest]
        public IEnumerator S5a_AC5_The_Board_Is_Framed_Inside_The_Board_Area_At_The_Games_Aspect_And_At_1_And_2_5_Also_Through_A_Pop(
            [Values(0f, 1f, 2.5f)] float aspect)
        {
            if (aspect > 0f) _camera.aspect = aspect;
            yield return Frames(3);
            string at = $"aspect {(aspect > 0f ? aspect.ToString() : "of the game view")}";
            AssertLayout(at);

            P.AddXp(30);
            yield return null;
            Assert.IsTrue(Popping);
            AssertLayout(at + ", on the pop frame");
            yield return null;
            AssertLayout(at + ", on the frame after the pop");
            yield return new WaitForSecondsRealtime(0.6f);
            AssertLayout(at + ", settled");
        }

        [UnityTest]
        public IEnumerator S5a_AC5_The_Label_Never_Reaches_The_Track_At_Its_Popped_Scale_For_Level_2_To_10_And_The_Track_Never_Moves()
        {
            yield return Frames(3);
            float trackX = Track.layout.xMin, trackWidth = Track.layout.width;
            for (int level = 2; level <= 10; level++)
            {
                P.AddXp(Table[level - 2]);
                yield return null;
                Assert.IsTrue(Popping, $"level {level}: popped");
                Assert.AreEqual(LevelText(level), LevelLabel.text);
                Assert.AreEqual(1.5f, LevelLabel.resolvedStyle.scale.value.x, 1e-3f, $"level {level}: popped scale");
                float text = LevelLabel.MeasureTextSize(LevelLabel.text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined).x;
                float right = LevelLabel.worldBound.xMin + text * 1.5f;
                Assert.LessOrEqual(right, Track.worldBound.xMin + 0.5f, $"level {level}: the popped text ({text * 1.5f:F1} wide) ends before the track starts");
                Assert.AreEqual(trackX, Track.layout.xMin, 0.01f, $"level {level}: the track did not move");
                Assert.AreEqual(trackWidth, Track.layout.width, 0.01f, $"level {level}: nor change its width");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator S5a_AC5_The_Max_Level_Text_Fits_The_Labels_Width_Unpopped()
        {
            yield return Frames(3);
            P.AddXp(1190);
            yield return null;
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(1f, LevelLabel.resolvedStyle.scale.value.x, 1e-3f);
            float text = LevelLabel.MeasureTextSize(LevelLabel.text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined).x;
            Assert.LessOrEqual(text, 108f, "Max level 11 fits");
            Assert.AreEqual(108f, LevelLabel.layout.width, Snap);
        }

        // ---- 5a-AC6 The row follows the battle

        [UnityTest]
        public IEnumerator S5a_AC6_On_The_First_Frame_The_Label_Reads_Level_1_And_The_Fill_Is_At_0_With_No_Pop()
        {
            // After an advanced run, a restart: the first frames of the new scene show Level 1 and an empty track.
            P.AddXp(140);
            yield return null;
            Assert.AreEqual(LevelText(4), LevelLabel.text);

            var old = _bootstrap;
            _bootstrap.Restart();
            int guard = 0;
            while (old != null && guard++ < 120) yield return null;
            Assert.IsTrue(old == null, "the scene never reloaded");
            yield return null;
            AcquireScene(disableInput: true);

            for (int frame = 1; frame <= 6; frame++)
            {
                Assert.AreEqual(LevelText(1), LevelLabel.text, $"frame {frame}");
                Assert.IsFalse(Popping, $"frame {frame}: no pop on the first frame, nor after");
                Assert.IsFalse(XpRow.ClassListContains(MaxClass), $"frame {frame}");
                Assert.AreEqual(0f, WrittenScale, $"frame {frame}: written 0");
                Assert.AreEqual(0f, ResolvedScale, $"frame {frame}: resolved 0, no ease from anywhere");
                Assert.AreEqual((0, 1), (Battle.Progression.Xp, Battle.Progression.Level));
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator S5a_AC6_After_Each_Kill_The_Fill_Is_The_Share_Of_The_Levels_XP_And_The_Label_Is_The_Same_String_Object()
        {
            yield return Frames(2);
            string text = LevelLabel.text;
            Assert.AreEqual(LevelText(1), text);
            Assert.AreEqual(0f, WrittenScale);

            KillOne();
            yield return null;
            Assert.AreEqual(10f / 30f, WrittenScale, 1e-6f, "a third after the first kill");
            Assert.AreSame(text, LevelLabel.text, "the label is not rebuilt for XP");
            Assert.IsFalse(Popping);

            KillOne();
            yield return null;
            Assert.AreEqual(20f / 30f, WrittenScale, 1e-6f, "two thirds after the second");
            Assert.AreSame(text, LevelLabel.text);
            Assert.IsFalse(Popping);

            yield return Frames(3);
            Assert.AreEqual(20f / 30f, WrittenScale, 1e-6f, "frames with no tick change nothing");
            Assert.IsFalse(Popping);
        }

        [UnityTest]
        public IEnumerator S5a_AC6_Ticks_With_No_Frame_Show_The_Last_State_On_The_Next_Frame()
        {
            yield return Frames(2);
            KillOne();
            KillOne();
            Assert.AreEqual(0f, WrittenScale, "no frame has run");
            yield return null;
            Assert.AreEqual(20f / 30f, WrittenScale, 1e-6f);
            Assert.IsFalse(Popping);
        }

        [UnityTest]
        public IEnumerator S5a_AC6_Under_The_End_Screen_The_Row_Stays_As_The_Last_Tick_Left_It_And_After_A_Restart_It_Starts_Again()
        {
            yield return Frames(2);
            KillOne();
            Assert.IsTrue(Battle.TrySellTower(new GridPos(1, 3)));
            int guard = 0;
            while (Battle.Outcome == RunOutcome.Running && guard++ < 6000) Battle.Tick();
            Assert.AreEqual(RunOutcome.Lost, Battle.Outcome);
            yield return null;
            Assert.AreEqual(DisplayStyle.Flex, Root.Q<VisualElement>("end-screen").resolvedStyle.display);

            int xp = P.Xp, level = P.Level;
            Assert.Greater(xp, 0);
            string text = LevelLabel.text;
            float written = WrittenScale;
            Assert.AreEqual((float)P.XpIntoLevel / P.XpForLevel, written, 1e-6f);
            bool pop = Popping;
            yield return new WaitForSecondsRealtime(1f);
            Assert.AreEqual((xp, level), (P.Xp, P.Level));
            Assert.AreSame(text, LevelLabel.text);
            Assert.AreEqual(written, WrittenScale);
            Assert.AreEqual(written, ResolvedScale, 1e-4f, "the ease ended where the last tick left it");
            Assert.IsFalse(Popping);
            Assert.IsFalse(pop);
            Assert.AreEqual(1f, LevelLabel.resolvedStyle.scale.value.x, 1e-3f);

            var old = _bootstrap;
            _bootstrap.Restart();
            guard = 0;
            while (old != null && guard++ < 120) yield return null;
            Assert.IsTrue(old == null, "the scene never reloaded");
            yield return null;
            AcquireScene(disableInput: true);
            yield return Frames(2);
            Assert.AreEqual((0, 1), (P.Xp, P.Level));
            Assert.AreEqual(LevelText(1), LevelLabel.text);
            Assert.AreEqual(0f, WrittenScale);
            Assert.AreEqual(0f, ResolvedScale);
            Assert.IsFalse(Popping);
        }

        // ---- 5a-AC7 The ease and the pop

        [UnityTest]
        public IEnumerator S5a_AC7_A_Gain_Of_XP_Eases_The_Fill_Over_0_15_s_And_Changes_No_Text_And_Pops_Nothing()
        {
            yield return Frames(2);
            KillOne();
            yield return null;
            AssertDurations(Fill, 0.15f, "the fill's ease");
            Assert.AreEqual(10f / 30f, WrittenScale, 1e-6f);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(10f / 30f, ResolvedScale, 1e-4f, "the ease ended on the share");
            Assert.IsFalse(Popping);
        }

        [UnityTest]
        public IEnumerator S5a_AC7_On_The_Frame_After_The_Third_Kill_The_Row_Pops_With_No_Transition_And_The_Next_Frame_Settles()
        {
            yield return Frames(2);
            for (int kill = 1; kill <= 2; kill++)
            {
                KillOne();
                yield return null;
                Assert.IsFalse(Popping, $"no pop class after kill {kill}");
                yield return null;
                Assert.IsFalse(Popping);
            }
            Assert.AreEqual(LevelText(1), LevelLabel.text);

            KillOne();
            Assert.AreEqual(2, P.Level, "the level rose on the kill's tick");
            Assert.AreEqual(LevelText(1), LevelLabel.text, "and the label waits for the frame");
            yield return null;

            Assert.IsTrue(Popping, "the pop class on the frame after the kill");
            Assert.AreEqual(LevelText(2), LevelLabel.text);
            Assert.AreEqual(0f, WrittenScale, "the fill jumps to the new share, 0");
            Assert.AreEqual(0f, ResolvedScale, "and is there at once, not on its way from two thirds");
            AssertDurations(LevelLabel, 0f, "label, pop");
            AssertDurations(Track, 0f, "track, pop");
            AssertDurations(Fill, 0f, "fill, pop");
            Assert.AreEqual(1.5f, LevelLabel.resolvedStyle.scale.value.x, 1e-3f, "the label at 1.5");
            Assert.AreEqual(1.5f, LevelLabel.resolvedStyle.scale.value.y, 1e-3f);
            AssertColor(Hex("F4F1EA"), Track.resolvedStyle.backgroundColor, "the track flashes full on the pop frame");

            yield return null;
            Assert.IsFalse(Popping, "the class goes on the next frame");
            Assert.AreEqual(LevelText(2), LevelLabel.text);
            AssertDurations(LevelLabel, 0.3f, "label, settle");
            AssertDurations(Track, 0.5f, "track, settle");
            AssertDurations(Fill, 0.15f, "fill, ease");

            yield return new WaitForSecondsRealtime(0.7f);
            Assert.AreEqual(1f, LevelLabel.resolvedStyle.scale.value.x, 1e-3f, "the label settled");
            AssertColor(Hex("45413B"), Track.resolvedStyle.backgroundColor, "the track settled");
            Assert.IsFalse(Popping);
        }

        [UnityTest]
        public IEnumerator S5a_AC7_The_Fill_Takes_Its_New_Share_At_Once_On_The_Pop_Frame_Even_Coming_From_A_Full_Looking_Bar()
        {
            yield return Frames(2);
            P.AddXp(20);
            yield return null;
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(20f / 30f, ResolvedScale, 1e-4f, "two thirds, settled");

            P.AddXp(15); // 35 in all: level 2 with 5 of 40
            yield return null;
            Assert.IsTrue(Popping);
            Assert.AreEqual(5f / 40f, WrittenScale, 1e-6f);
            Assert.AreEqual(5f / 40f, ResolvedScale, 1e-6f, "at its new share on the pop frame itself: not eased from two thirds");
            yield return null;
            Assert.AreEqual(5f / 40f, ResolvedScale, 1e-3f, "and still there a frame later");
        }

        [UnityTest]
        public IEnumerator S5a_AC7_A_Gain_On_The_Frame_After_A_Pop_Jumps_Because_The_Pop_Style_Is_Still_Resolved_And_A_Gain_On_The_Frame_After_That_Eases()
        {
            yield return Frames(2);
            P.AddXp(30);
            yield return null;
            Assert.IsTrue(Popping);
            P.AddXp(10); // XP only, no level: the frame removes the class and writes the scale
            yield return null;
            Assert.IsFalse(Popping);
            Assert.AreEqual(10f / 40f, WrittenScale, 1e-6f);
            Assert.AreEqual(WrittenScale, ResolvedScale, 1e-6f, "accepted: the gain on the settle frame jumps, the resolved scale is the written one");

            P.AddXp(10);
            yield return null;
            AssertDurations(Fill, 0.15f, "the next gain eases");
            Assert.AreEqual(20f / 40f, WrittenScale, 1e-6f);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(20f / 40f, ResolvedScale, 1e-4f);
        }

        private static void AssertTransition(VisualElement element, string property, EasingMode easing, string what)
        {
            List<StylePropertyName> properties = element.resolvedStyle.transitionProperty.ToList();
            Assert.AreEqual(1, properties.Count, what);
            Assert.AreEqual(new StylePropertyName(property), properties[0], what + ": property");
            List<EasingFunction> functions = element.resolvedStyle.transitionTimingFunction.ToList();
            Assert.AreEqual(1, functions.Count, what);
            Assert.AreEqual(easing, functions[0].mode, what + ": timing function");
        }

        [UnityTest]
        public IEnumerator S5a_AC7_The_Transitions_Are_On_The_Direction_Properties_With_The_Direction_Timing_Functions()
        {
            yield return Frames(3);
            AssertTransition(LevelLabel, "scale", EasingMode.EaseOut, "label");
            AssertTransition(Track, "background-color", EasingMode.Ease, "track");
            AssertTransition(Fill, "scale", EasingMode.EaseOut, "fill");
            AssertDurations(LevelLabel, 0.3f, "label");
            AssertDurations(Track, 0.5f, "track");
            AssertDurations(Fill, 0.15f, "fill");

            P.AddXp(30);
            yield return null;
            AssertTransition(LevelLabel, "scale", EasingMode.EaseOut, "label, pop");
            AssertTransition(Track, "background-color", EasingMode.Ease, "track, pop");
            AssertTransition(Fill, "scale", EasingMode.EaseOut, "fill, pop");
        }

        [UnityTest]
        public IEnumerator S5a_AC5_The_New_Elements_Ignore_Picking_And_A_Pick_Over_The_Row_Reaches_None_Of_Them()
        {
            yield return Frames(3);
            foreach (string name in new[] { "xp-row", "level-label", "xp-track", "xp-fill", "economy-row" })
                Assert.AreEqual(PickingMode.Ignore, Root.Q<VisualElement>(name).pickingMode, name);
            foreach (VisualElement e in new[] { XpRow, LevelLabel, Track, Fill, Root.Q<VisualElement>("economy-row") })
            {
                VisualElement picked = Root.panel.Pick(e.worldBound.center);
                Assert.IsTrue(picked == null || (picked != XpRow && picked != LevelLabel && picked != Track && picked != Fill && picked.name != "economy-row"),
                    $"a pick over {e.name} found {picked?.name}");
            }
        }

        [UnityTest]
        public IEnumerator S5a_AC7_Three_Levels_Fed_Before_One_Frame_Play_One_Pop_With_The_Last_Level()
        {
            yield return Frames(2);
            P.AddXp(30 + 40 + 70 + 15);
            Assert.AreEqual(4, P.Level);
            yield return null;
            Assert.IsTrue(Popping);
            Assert.AreEqual(LevelText(4), LevelLabel.text, "the last level, not 2 or 3");
            Assert.AreEqual(15f / 90f, WrittenScale, 1e-6f);
            yield return null;
            Assert.IsFalse(Popping, "one pop of one frame");
            Assert.AreEqual(LevelText(4), LevelLabel.text);
            yield return Frames(3);
            Assert.IsFalse(Popping);
        }

        [UnityTest]
        public IEnumerator S5a_AC7_A_Level_Fed_On_The_Pop_Frame_Keeps_The_Class_One_Frame_More()
        {
            yield return Frames(2);
            P.AddXp(30);
            yield return null;
            Assert.IsTrue(Popping);
            Assert.AreEqual(LevelText(2), LevelLabel.text);

            P.AddXp(40);
            yield return null;
            Assert.IsTrue(Popping, "a level on the frame after a pop pops again");
            Assert.AreEqual(LevelText(3), LevelLabel.text);
            AssertDurations(LevelLabel, 0f, "still no transition");

            yield return null;
            Assert.IsFalse(Popping);
            yield return null;
            Assert.IsFalse(Popping);
        }

        [UnityTest]
        public IEnumerator S5a_AC7_Levels_On_Every_Frame_Hold_The_Class_Every_Frame_And_It_Goes_One_Frame_After_The_Last()
        {
            yield return Frames(2);
            for (int level = 2; level <= 6; level++)
            {
                P.AddXp(Table[level - 2]);
                yield return null;
                Assert.IsTrue(Popping, $"level {level}");
                Assert.AreEqual(LevelText(level), LevelLabel.text);
            }
            yield return null;
            Assert.IsFalse(Popping);
        }

        // ---- 5a-AC8 The table ended

        [UnityTest]
        public IEnumerator S5a_AC8_When_The_Table_Ends_The_Row_Reads_Max_Level_11_Holds_A_Full_Grey_Fill_And_Never_Changes_Again()
        {
            yield return Frames(2);
            P.AddXp(1190 - 210);
            yield return null;
            Assert.AreEqual((10, false), (P.Level, P.TableEnded));
            Assert.AreEqual(LevelText(10), LevelLabel.text);
            Assert.IsFalse(XpRow.ClassListContains(MaxClass), "not yet");
            yield return null;

            P.AddXp(210);
            Assert.IsTrue(P.TableEnded);
            yield return null;
            Assert.AreEqual("Max level 11", LevelLabel.text);
            Assert.IsTrue(XpRow.ClassListContains(MaxClass));
            Assert.AreEqual(1f, WrittenScale);
            Assert.AreEqual(1f, ResolvedScale, 1e-6f);
            AssertColor(Hex("8A8478"), Fill.resolvedStyle.backgroundColor, "the ended fill");
            Assert.IsTrue(Popping, "the level that ends the table pops like any other");
            yield return null;
            Assert.IsFalse(Popping);
            Assert.AreEqual("Max level 11", LevelLabel.text);
            AssertColor(Hex("8A8478"), Fill.resolvedStyle.backgroundColor, "still grey after the pop");

            // Sentinels: whatever the view would write, it overwrites these.
            string text = LevelLabel.text;
            Fill.style.scale = new Scale(new Vector2(0.5f, 1f));
            P.AddXp(500);
            yield return null;
            yield return null;
            Assert.AreSame(text, LevelLabel.text, "the same string object");
            Assert.AreEqual(0.5f, WrittenScale, "the view did not write the scale again");
            Assert.IsFalse(Popping, "no more pops");
            Assert.IsTrue(XpRow.ClassListContains(MaxClass));
            Assert.AreEqual(11, P.Level);
            Assert.AreEqual(1190 + 500, P.Xp);
        }

        [UnityTest]
        public IEnumerator S5a_AC8_The_Table_Ended_In_One_Jump_From_Level_1_Shows_The_Same_Look()
        {
            yield return Frames(2);
            P.AddXp(100000);
            yield return null;
            Assert.AreEqual("Max level 11", LevelLabel.text);
            Assert.AreEqual(1f, WrittenScale);
            Assert.IsTrue(XpRow.ClassListContains(MaxClass));
            Assert.IsTrue(Popping);
            yield return null;
            Assert.IsFalse(Popping);
        }

        // ---- 5a-AC9 Cost

        [UnityTest]
        public IEnumerator S5a_AC9_The_Label_Is_Written_Only_On_A_Level_And_The_Fill_Only_When_Its_Share_Changes()
        {
            yield return Frames(2);
            // A sentinel text and scale: only the view's own write can replace them.
            LevelLabel.text = "sentinel";
            string text = LevelLabel.text;
            Fill.style.scale = new Scale(new Vector2(0.9f, 1f));
            yield return Frames(3);
            Assert.AreSame(text, LevelLabel.text, "no level change, no rewrite of the text");
            Assert.AreEqual(0.9f, WrittenScale, "no XP change, no rewrite of the scale");

            P.AddXp(5);
            yield return null;
            Assert.AreSame(text, LevelLabel.text, "XP alone does not rewrite the text");
            Assert.AreEqual(5f / 30f, WrittenScale, 1e-6f, "XP rewrites the scale");

            Fill.style.scale = new Scale(new Vector2(0.9f, 1f));
            yield return Frames(3);
            Assert.AreEqual(0.9f, WrittenScale, "and the same XP does not rewrite it again");

            P.AddXp(25);
            yield return null;
            Assert.AreEqual(LevelText(2), LevelLabel.text, "a level rewrites the text");
            Assert.AreNotSame(text, LevelLabel.text);
            Assert.AreEqual(0f, WrittenScale);
        }

        [UnityTest]
        public IEnumerator S5a_AC9_The_Views_Frame_Allocates_Nothing_Unless_The_Level_Changes_And_No_Layout_Under_The_Bottom_Bar_Changes_On_Any_Frame()
        {
            yield return Frames(3);
            TowerStats s = _input.TowerAt(0).ToStats();
            var free = new TowerStats(s.Range, s.Damage, s.ShotsPerSecond, s.ProjectileSpeed, 0, s.Element);
            foreach (GridPos cell in new[] { new GridPos(1, 3), new GridPos(1, 5), new GridPos(2, 4) })
                Assert.AreEqual(PlacementResult.Placed, Battle.TryBuyTower(cell, free));

            XpBarView view = Object.FindAnyObjectByType<XpBarView>();
            System.Action frame = FrameMethod(view);
            frame(); // warm-up of the code path
            view.enabled = false; // the test calls the frame itself, once per drawn frame, right after the tick

            var elements = new List<VisualElement>();
            BottomBar.Query<VisualElement>().ForEach(elements.Add);
            Assert.GreaterOrEqual(elements.Count, 8, "the bar's elements");
            int geometryEvents = 0;
            foreach (VisualElement element in elements) element.RegisterCallback<GeometryChangedEvent>(_ => geometryEvents++);
            var layouts = elements.ConvertAll(e => e.layout);

            int levelFrames = 0, xpOnlyFrames = 0, quietFrames = 0, popFrames = 0, maxLevelAllocs = 0;
            var violations = new List<string>();
            int xpBefore = P.Xp;
            for (int tick = 0; tick < 450; tick++)
            {
                if (tick % 15 == 0) Board.SpawnEnemy(new EnemyStats(0f, 1f, 0, default, 10));
                int level = P.Level, xp = P.Xp;
                Battle.Tick();
                int count = Allocations.Of(frame); // the view's frame, called by hand right after the tick
                if (P.Level != level)
                {
                    levelFrames++;
                    maxLevelAllocs = Mathf.Max(maxLevelAllocs, count);
                }
                else
                {
                    if (P.Xp != xp) xpOnlyFrames++; else quietFrames++;
                    if (count != 0) violations.Add($"tick {tick} (level {P.Level}, XP {P.Xp}): {count} allocation(s) on a frame where the level is unchanged");
                }
                yield return null;
                if (Popping) popFrames++;
            }

            Assert.AreEqual(0, violations.Count, string.Join("; ", violations));
            Assert.GreaterOrEqual(P.Xp - xpBefore, 60, "many kills happened");
            Assert.GreaterOrEqual(levelFrames, 1, "a level was gained");
            Assert.GreaterOrEqual(xpOnlyFrames, 5, "and several frames changed XP only");
            Assert.Greater(quietFrames, 100);
            Assert.GreaterOrEqual(popFrames, 1);
            Assert.LessOrEqual(maxLevelAllocs, 6, "a level builds its text (the format and the number) and nothing else");
            Assert.AreEqual(0, geometryEvents, "no element under the bottom bar got a GeometryChangedEvent");
            for (int i = 0; i < elements.Count; i++) Assert.AreEqual(layouts[i], elements[i].layout, $"layout of {elements[i].name}");
            Debug.Log($"5a-AC9: {levelFrames} level frame(s) (at most {maxLevelAllocs} allocations), {xpOnlyFrames} XP-only frames and {quietFrames} quiet frames with none; {popFrames} frames with the pop class");
        }

        [UnityTest]
        public IEnumerator S5a_AC9_The_Pop_Frame_And_The_Settle_Change_No_Layout_Either()
        {
            yield return Frames(3);
            var elements = new List<VisualElement>();
            BottomBar.Query<VisualElement>().ForEach(elements.Add);
            int geometryEvents = 0;
            foreach (VisualElement element in elements) element.RegisterCallback<GeometryChangedEvent>(_ => geometryEvents++);
            var layouts = elements.ConvertAll(e => e.layout);

            for (int level = 2; level <= 11; level++)
            {
                P.AddXp(Table[level - 2]);
                yield return null;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.7f);

            Assert.AreEqual(0, geometryEvents, "no GeometryChangedEvent through ten levels, the end and the settle");
            for (int i = 0; i < elements.Count; i++) Assert.AreEqual(layouts[i], elements[i].layout, $"layout of {elements[i].name}");
        }
    }
}
