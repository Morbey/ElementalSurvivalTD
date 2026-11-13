using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ElementalTD.Core;
using ElementalTD.Data;
using ElementalTD.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ElementalTD.PlayModeTests
{
    /// <summary>
    /// Step 4e on the scene, with the real run: the announcement of a mixed wave (4e-AC4), the views in the first mixed wave (4e-AC3)
    /// and the palette for every enemy asset (4e-AC5). The elements of each wave come from docs/balance/0001-prototype-economy.md,
    /// not from the helpers the other scene tests use. Plan: docs/plans/0010-prototype-slice-4e.md.
    /// </summary>
    public class MixedWaveSceneTests : FloatingSceneBase
    {
        private const string LightHex = "FFE27A", ShadowHex = "B48CFF";

        // The elements each wave announces, in order of first appearance (the groups of the document's wave table).
        private static readonly string[][] Announced =
        {
            new[] { "Shadow" }, new[] { "Light" }, new[] { "Light" },
            new[] { "Shadow", "Light" },   // wave 4  S3 L3
            new[] { "Light", "Shadow" },   // wave 5  L4 S3
            new[] { "Shadow", "Light" },   // wave 6  S1 L1 ...
            new[] { "Shadow", "Light" },   // wave 7  S2 L1 ...
            new[] { "Light", "Shadow" },   // wave 8  L2 S1 ...
            new[] { "Shadow", "Light" },   // wave 9  S1 L1 ... S1
            new[] { "Light", "Shadow" },   // wave 10 L1 S1 ...
        };

        private static string Rich(string[] names)
        {
            var parts = new List<string>();
            foreach (string n in names) parts.Add($"<color=#{(n == "Light" ? LightHex : ShadowHex)}>{n}</color>");
            return string.Join(" + ", parts);
        }

        private static string Plain(string richText) => Regex.Replace(richText, "<.*?>", "");

        private Label WaveLabel => Root.Q<Label>("wave-label");
        private Label NextLabel => Root.Q<Label>("next-wave-label");

        private static void AssertInside(Rect bar, Rect label, string what)
        {
            Assert.GreaterOrEqual(label.xMin, bar.xMin - Snap, what + ": left edge inside the top bar");
            Assert.LessOrEqual(label.xMax, bar.xMax + Snap, what + ": right edge inside the top bar");
            Assert.GreaterOrEqual(label.yMin, bar.yMin - Snap, what + ": top edge inside the top bar");
            Assert.LessOrEqual(label.yMax, bar.yMax + Snap, what + ": bottom edge inside the top bar");
        }

        /// <summary>Both labels inside the top bar, and neither overlapping the other (nor the lives label between them).</summary>
        private void AssertTopBar(string when)
        {
            Rect bar = Root.Q<VisualElement>("top-bar").worldBound;
            Rect wave = WaveLabel.worldBound, next = NextLabel.worldBound, lives = Root.Q<Label>("lives-label").worldBound;
            AssertInside(bar, wave, when + ": wave-label");
            AssertInside(bar, next, when + ": next-wave-label");
            Assert.IsFalse(wave.Overlaps(next), $"{when}: the wave line {wave} and the countdown {next} overlap");
            Assert.IsFalse(lives.Overlaps(next), $"{when}: the lives {lives} and the countdown {next} overlap");
            Assert.IsFalse(wave.Overlaps(lives), $"{when}: the wave line {wave} and the lives {lives} overlap");
        }

        private bool TopBarFits()
        {
            Rect bar = Root.Q<VisualElement>("top-bar").worldBound;
            Rect wave = WaveLabel.worldBound, next = NextLabel.worldBound, lives = Root.Q<Label>("lives-label").worldBound;
            bool Inside(Rect r) => r.xMin >= bar.xMin - Snap && r.xMax <= bar.xMax + Snap && r.yMin >= bar.yMin - Snap && r.yMax <= bar.yMax + Snap;
            return Inside(wave) && Inside(next) && Inside(lives) && !wave.Overlaps(next) && !lives.Overlaps(next) && !wave.Overlaps(lives);
        }

        private IEnumerator PlayReferenceUntil(Func<bool> stop, int maxTicks, int ticksPerFrame = 3)
        {
            int ticks = 0;
            while (!stop() && Battle.Outcome == RunOutcome.Running && ticks < maxTicks)
            {
                for (int i = 0; i < ticksPerFrame && !stop() && Battle.Outcome == RunOutcome.Running; i++, ticks++)
                {
                    RunReferenceActionsAt(Battle.TickCount);
                    Battle.Tick();
                }
                yield return null;
            }
            Assert.IsTrue(stop(), $"the state was never reached (tick {Battle.TickCount}, outcome {Battle.Outcome})");
        }

        // ---- 4e-AC4 Announcement

        [UnityTest]
        public IEnumerator S4e_AC4_On_Every_Frame_Of_The_Reference_Run_Both_Labels_Name_Each_Element_Once_In_Order_Of_First_Appearance_In_Its_Colour_And_Lie_Inside_The_Top_Bar()
        {
            yield return null;
            var countdownFrames = new int[10];
            var firstCountdown = new string[10];
            bool widest = false;
            int frames = 0;
            while (Battle.Outcome == RunOutcome.Running && Battle.TickCount < 12000)
            {
                for (int i = 0; i < 3 && Battle.Outcome == RunOutcome.Running; i++)
                {
                    RunReferenceActionsAt(Battle.TickCount);
                    Battle.Tick();
                }
                yield return null;
                if (Battle.Outcome != RunOutcome.Running) break;   // the end screen hides the countdown
                frames++;

                int started = Battle.Waves.StartedWaves;
                string expectedWave = started == 0 ? "Wave 0/10" : $"Wave {started}/10: {Rich(Announced[started - 1])}";
                Assert.AreEqual(expectedWave, WaveLabel.text, $"tick {Battle.TickCount}: the wave line");
                if (Battle.Waves.SecondsToNextWave > 0f)
                {
                    int seconds = Mathf.CeilToInt(Battle.Waves.SecondsToNextWave);
                    string expectedNext = $"Next wave in {seconds} s: {Rich(Announced[started])}";
                    Assert.AreEqual(DisplayStyle.Flex, NextLabel.resolvedStyle.display, $"tick {Battle.TickCount}: the countdown shows");
                    Assert.AreEqual(expectedNext, NextLabel.text, $"tick {Battle.TickCount}: the countdown");
                    countdownFrames[started]++;
                    firstCountdown[started] ??= Plain(NextLabel.text);
                    AssertTopBar($"tick {Battle.TickCount} ({Plain(WaveLabel.text)} | {Plain(NextLabel.text)})");
                    if (started == 9 && Plain(WaveLabel.text) == "Wave 9/10: Shadow + Light" && Plain(NextLabel.text).EndsWith(": Light + Shadow")) widest = true;
                }
                else
                {
                    Assert.AreEqual(DisplayStyle.None, NextLabel.resolvedStyle.display, $"tick {Battle.TickCount}: no countdown while a wave spawns");
                    if (started > 0) AssertTopBar($"tick {Battle.TickCount} ({Plain(WaveLabel.text)})");
                }
            }

            Assert.AreEqual(RunOutcome.Won, Battle.Outcome);
            for (int w = 0; w < 10; w++) Assert.Greater(countdownFrames[w], 0, $"a countdown showed before wave {w + 1}");
            // The first mixed countdown, frame by frame, and its first text.
            Assert.AreEqual("Next wave in 8 s: Shadow + Light", firstCountdown[3], "the first frame of the countdown before wave 4");
            Assert.AreEqual("Next wave in 8 s: Light + Shadow", firstCountdown[4]);
            Assert.AreEqual("Next wave in 8 s: Light + Shadow", firstCountdown[9]);
            Assert.IsTrue(widest, "a frame held the widest pair: Wave 9/10: Shadow + Light with Next wave in N s: Light + Shadow");
            Assert.Greater(frames, 3000);
            Assert.AreEqual("Wave 10/10: Light + Shadow", Plain(WaveLabel.text), "the last frame");
        }

        [UnityTest]
        public IEnumerator S4e_AC4_The_Widest_Pair_Fits_The_Top_Bar_At_Every_Aspect_The_Layout_Tests_Use_And_Reports_The_Narrowest_Width_That_Does([Values(0f, 1f, 1.7778f, 2.5f)] float aspect)
        {
            yield return PlayReferenceUntil(() => Battle.Waves.StartedWaves == 9 && Battle.Waves.SecondsToNextWave > 0f, 12000);
            yield return null;
            Assert.AreEqual("Wave 9/10: Shadow + Light", Plain(WaveLabel.text));
            StringAssert.StartsWith("Next wave in ", Plain(NextLabel.text));
            StringAssert.EndsWith(": Light + Shadow", Plain(NextLabel.text));
            Assert.AreEqual(DisplayStyle.Flex, NextLabel.resolvedStyle.display);

            if (aspect > 0f) _camera.aspect = aspect;
            yield return Frames(3);
            AssertTopBar($"aspect {(aspect > 0f ? aspect.ToString() : "of the game view")}");

            // The panel is laid out at the window's size, not the camera's aspect: shrink the bar to find the width the pair needs.
            VisualElement bar = Root.Q<VisualElement>("top-bar");
            float full = bar.resolvedStyle.width;
            float narrowest = full;
            for (float width = full; width >= 400f; width -= 10f)
            {
                bar.style.width = width;
                yield return null;
                if (!TopBarFits()) break;
                narrowest = width;
            }
            bar.style.width = StyleKeyword.Null;
            _camera.ResetAspect();
            string report = $"4e-AC4: the widest pair ('{Plain(WaveLabel.text)}' | '{Plain(NextLabel.text)}') fits the top bar down to {narrowest} panel units; the bar is {full} here (aspect {aspect})";
            Debug.Log(report);
            TestContext.Out.WriteLine(report);
            Assert.LessOrEqual(narrowest, 960f, "the pair fits a top bar 960 panel units wide (visual direction 1.3). " + report);
        }

        // ---- 4e-AC3 Mixed fire on the scene

        [UnityTest]
        public IEnumerator S4e_AC3_In_The_First_Mixed_Wave_Every_Enemy_View_Has_Its_Own_Elements_Material_And_A_Shadow_Enemy_Carries_The_Immune_Word_While_A_Light_Enemy_Is_On_The_Board()
        {
            Element shadow = ElementOfTower(1), light = ElementOfTower(0);
            yield return PlayReferenceUntil(() => Battle.Waves.StartedWaves == 4, 12000);
            yield return null;
            Assert.AreEqual(shadow, Battle.Waves.WaveAt(3).GroupAt(0).Enemy.Element, "document: wave 4 opens with its Shadow group");

            int framesInWave = 0, wordWhileOther = 0, lightWordWhileShadow = 0, bothOnBoard = 0;
            while (Battle.Outcome == RunOutcome.Running && Battle.Waves.StartedWaves == 4 && Battle.TickCount < 12000)
            {
                for (int i = 0; i < 2 && Battle.Waves.StartedWaves == 4; i++)
                {
                    RunReferenceActionsAt(Battle.TickCount);
                    Battle.Tick();
                }
                yield return null;
                if (Battle.Waves.StartedWaves != 4) break;
                framesInWave++;

                int onBoard = Board.EnemyCount, markedShadow = 0, markedLight = 0;
                bool hasShadow = false, hasLight = false;
                Transform enemies = FindByName("Enemies");
                for (int i = 0; i < enemies.childCount; i++) Assert.AreEqual(i < onBoard, enemies.GetChild(i).gameObject.activeSelf);
                for (int i = 0; i < onBoard; i++)
                {
                    Enemy e = Board.EnemyAt(i);
                    Assert.AreSame(LookOf(e.Element).Enemy, SharedMaterialOf(enemies.GetChild(i)), $"tick {Battle.TickCount}: enemy view {i} shows a {(e.Element == light ? "Light" : "Shadow")} enemy");
                    if (e.Element == shadow) hasShadow = true; else hasLight = true;
                    bool marked = e.ImmuneHitTick > 0 && Battle.TickCount - e.ImmuneHitTick < 60;
                    if (marked && e.Element == shadow) markedShadow++;
                    if (marked && e.Element == light) markedLight++;
                }
                Assert.AreEqual(markedShadow + markedLight, DrawnWords().Count, $"tick {Battle.TickCount}: one word per enemy that took an immune landing in the last 60 ticks");
                if (hasShadow && hasLight) bothOnBoard++;
                if (markedShadow > 0 && hasLight) wordWhileOther++;
                if (markedLight > 0 && hasShadow) lightWordWhileShadow++;
            }

            Debug.Log($"4e-AC3 scene, wave 4: {framesInWave} frames, both elements on the board on {bothOnBoard}, a Shadow enemy with its word while a Light enemy was on the board on {wordWhileOther}, the reverse on {lightWordWhileShadow}");
            Assert.Greater(framesInWave, 200);
            Assert.Greater(bothOnBoard, 0, "enemies of both elements were on the board together");
            Assert.Greater(wordWhileOther, 0, "a Shadow enemy carried the immune word while a Light enemy was on the board");
        }

        // ---- 4e-AC5 Data: the palette

        [Test]
        public void S4e_AC5_The_Palette_Has_A_Look_For_The_Element_Of_Every_Enemy_Asset()
        {
            int checkedAssets = 0;
#if UNITY_EDITOR
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:EnemyDefinition", new[] { "Assets/_Game/Data/Assets" }))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                EnemyStats stats = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path).ToStats();
                Assert.IsFalse(stats.Element.IsNone, path + " has an element");
                Assert.IsTrue(Palette.TryGetLook(stats.Element, out ElementPalette.Look look), path + ": a palette look for its element");
                Assert.IsNotNull(look.Enemy, path + ": an enemy material");
                checkedAssets++;
            }
#endif
            Assert.AreEqual(13, checkedAssets, "the 13 enemy assets of the document");
        }
    }
}
