using System;
using System.Collections.Generic;
using System.IO;
using ElementalTD.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ElementalTD.Core.Tests
{
    /// <summary>
    /// 5a on the shipped assets: the data (5a-AC4), the pins of docs/balance/0001-prototype-economy.md "Step 5a" (5a-AC4), the real run with
    /// and without the table (5a-AC3), determinism and cost (5a-AC9) and the source rules (5a-AC9). The numbers are the document's, not what the game produced.
    /// </summary>
    public class XpRealRunTests
    {
        private const string Dir = "Assets/_Game/Data/Assets/";

        private static readonly int[] DocTable = { 30, 40, 70, 90, 110, 130, 150, 170, 190, 210 };
        private static readonly int[] DocLevelTicks = { 1452, 2209, 3369, 4175, 5446, 6980, 8808 };
        private static readonly int[] DocLevelXp = { 30, 70, 140, 230, 340, 470, 620 };

        private static T Load<T>(string name) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Dir + name);
            Assert.IsNotNull(asset, name);
            return asset;
        }

        /// <summary>The real run: with the table (and the pool and seed of 5b, unless <paramref name="pool"/> is false) or with no progression at all.</summary>
        private static Battle Build(bool withTable, out Board board, bool pool = true) => Reference5b.Build(out board, pool, withTable);

        private static TowerStats Tower(bool light) => Reference5b.Tower(light);

        /// <summary>
        /// Replays the 5b reference order with its picks; <paramref name="afterTick"/> gets the number of Tick() calls done, with a choice that
        /// tick opened still open. With <paramref name="picks"/> false the run has no pool, so no choice can open, and its purchases are tried
        /// without checking the document's gold (the document's order was made for the run with perks).
        /// </summary>
        private static void Replay(Battle battle, Action<int> afterTick, bool build = true, bool picks = true) =>
            Reference5b.Replay(battle, afterTick, build: build, picks: picks, checkGold: picks);

        // ---- 5a-AC4 Data

        [Test]
        public void S5a_AC4_Every_Enemy_Asset_Holds_10_XP_And_There_Are_13()
        {
            string[] guids = AssetDatabase.FindAssets("t:EnemyDefinition", new[] { Dir.TrimEnd('/') });
            Assert.AreEqual(13, guids.Length);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
                Assert.AreEqual(10, new SerializedObject(asset).FindProperty("xpReward").intValue, path);
                Assert.AreEqual(10, asset.ToStats().XpReward, path + ": passed on to the stats");
                Assert.AreEqual(10, asset.ToStats().GoldReward, path + ": the gold reward is untouched");
            }
        }

        [Test]
        public void S5a_AC4_The_Run_Asset_Holds_The_Documents_Table_And_Hands_Out_A_Copy()
        {
            var run = Load<RunDefinition>("Run_Prototype.asset");
            SerializedProperty serialized = new SerializedObject(run).FindProperty("xpPerLevel");
            Assert.AreEqual(DocTable.Length, serialized.arraySize);
            for (int i = 0; i < DocTable.Length; i++) Assert.AreEqual(DocTable[i], serialized.GetArrayElementAtIndex(i).intValue, $"item {i + 1}");

            int[] table = run.ToLevelTable();
            Assert.AreEqual(DocTable, table);
            Assert.AreEqual(1190, Sum(table));
            table[0] = 999;
            Array.Clear(table, 1, table.Length - 1);
            Assert.AreEqual(DocTable, run.ToLevelTable(), "the caller's changes did not reach the asset");
            Assert.AreNotSame(run.ToLevelTable(), run.ToLevelTable(), "a new array every call");
            Assert.AreEqual(DocTable, ReadArray(new SerializedObject(run).FindProperty("xpPerLevel")), "and the asset itself still holds the table");
        }

        private static int Sum(int[] items)
        {
            int sum = 0;
            foreach (int i in items) sum += i;
            return sum;
        }

        private static int[] ReadArray(SerializedProperty array)
        {
            var items = new int[array.arraySize];
            for (int i = 0; i < items.Length; i++) items[i] = array.GetArrayElementAtIndex(i).intValue;
            return items;
        }

        private static RunDefinition RunWithTable(string name, params int[] table)
        {
            var run = ScriptableObject.CreateInstance<RunDefinition>();
            run.name = name;
            var so = new SerializedObject(run);
            SerializedProperty array = so.FindProperty("xpPerLevel");
            array.arraySize = table.Length;
            for (int i = 0; i < table.Length; i++) array.GetArrayElementAtIndex(i).intValue = table[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return run;
        }

        [TestCase(new[] { 0 }, 1, 0)]
        [TestCase(new[] { 5, 0, 3 }, 2, 0)]
        [TestCase(new[] { 5, 4, -2 }, 3, -2)]
        [TestCase(new[] { int.MinValue }, 1, int.MinValue)]
        public void S5a_AC4_ToLevelTable_Refuses_An_Item_Below_1_And_Names_The_Run_And_The_Item(int[] table, int item, int value)
        {
            RunDefinition run = RunWithTable("Run_Broken", table);
            try
            {
                var failure = Assert.Throws<InvalidOperationException>(() => run.ToLevelTable());
                StringAssert.Contains("Run_Broken", failure.Message);
                StringAssert.Contains($"item {item}", failure.Message);
                StringAssert.Contains(value.ToString(), failure.Message);
            }
            finally { UnityEngine.Object.DestroyImmediate(run); }
        }

        [Test]
        public void S5a_AC4_A_Run_With_No_Table_Or_A_Table_Of_1s_Is_Accepted()
        {
            RunDefinition none = RunWithTable("Run_None"), ones = RunWithTable("Run_Ones", 1, 1);
            try
            {
                Assert.AreEqual(0, none.ToLevelTable().Length);
                Assert.AreEqual(new[] { 1, 1 }, ones.ToLevelTable());
                Assert.IsTrue(new Progression(none.ToLevelTable()).TableEnded);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(none);
                UnityEngine.Object.DestroyImmediate(ones);
            }
        }

        [Test]
        public void S5a_AC4_An_Enemy_Asset_With_A_Negative_XP_Reward_Is_Refused_When_It_Becomes_Stats()
        {
            var enemy = ScriptableObject.CreateInstance<EnemyDefinition>();
            try
            {
                var so = new SerializedObject(enemy);
                so.FindProperty("xpReward").intValue = -1;
                so.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<ArgumentOutOfRangeException>(() => enemy.ToStats());
            }
            finally { UnityEngine.Object.DestroyImmediate(enemy); }
        }

        // ---- 5a-AC4 The pins

        [Test]
        public void S5a_AC4_Pin_The_Reference_Order_Gains_Levels_2_To_8_On_The_Documents_Ticks_With_The_Documents_XP_And_Wins_On_10026_At_Level_8_With_750_XP()
        {
            Battle battle = Build(true, out Board board);
            Progression p = battle.Progression;
            int previousLevel = 1, previousXp = 0, levelUps = 0, wonAt = -1;
            var kills = new List<int>();
            Replay(battle, tick =>
            {
                if (p.Xp != previousXp)
                {
                    Assert.AreEqual(10, p.Xp - previousXp, $"tick {tick}: one kill is 10 XP");
                    kills.Add(tick);
                }
                if (p.Level != previousLevel)
                {
                    Assert.AreEqual(previousLevel + 1, p.Level, $"tick {tick}: one level at a time on the real run");
                    Assert.Less(levelUps, DocLevelTicks.Length, $"tick {tick}: a level beyond the document's seven");
                    Assert.AreEqual(DocLevelTicks[levelUps], tick, $"level {p.Level}");
                    Assert.AreEqual(DocLevelXp[levelUps], p.Xp, $"XP after level {p.Level}");
                    Assert.AreEqual(0, p.XpIntoLevel, $"level {p.Level} starts empty: the levels land on a multiple of 10 on an item's edge");
                    levelUps++;
                }
                previousLevel = p.Level;
                previousXp = p.Xp;
                if (battle.Outcome == RunOutcome.Won) wonAt = tick;
            });

            Assert.AreEqual(7, levelUps);
            Assert.AreEqual(10026, wonAt);
            Assert.AreEqual((RunOutcome.Won, 10, 189), (battle.Outcome, battle.Lives, battle.Gold));
            Assert.AreEqual((750, 8, false), (p.Xp, p.Level, p.TableEnded));
            Assert.AreEqual(75, kills.Count, "75 kills");
            Assert.AreEqual(1288, kills[0], "the first kill is where 4e put it");
            Assert.AreEqual(1452, kills[2], "the third kill gains level 2");
            Assert.AreEqual((130, 170), (p.XpIntoLevel, p.XpForLevel), "130 of the 170 XP to level 9");
        }

        [Test]
        public void S5a_AC4_Pin_The_Tick_Before_Each_Level_Is_Still_The_Old_Level_With_The_Last_Kill_Short_Of_The_Item()
        {
            Battle battle = Build(true, out _);
            var levelAt = new int[10400];
            var xpAt = new int[10400];
            Replay(battle, tick => { levelAt[tick] = battle.Progression.Level; xpAt[tick] = battle.Progression.Xp; });
            for (int i = 0; i < DocLevelTicks.Length; i++)
            {
                int tick = DocLevelTicks[i];
                Assert.AreEqual(i + 1, levelAt[tick - 1], $"tick {tick - 1}: still level {i + 1}");
                Assert.AreEqual(DocLevelXp[i] - 10, xpAt[tick - 1], $"tick {tick - 1}: ten XP short");
                Assert.AreEqual(i + 2, levelAt[tick], $"tick {tick}");
                Assert.AreEqual(DocLevelXp[i], xpAt[tick], $"tick {tick}");
            }
        }

        [Test]
        public void S5a_AC4_Pin_Building_Nothing_Loses_On_Tick_2931_With_No_XP_And_Level_1()
        {
            Battle battle = Build(true, out _);
            Replay(battle, null, build: false);
            Assert.AreEqual((RunOutcome.Lost, 2931), (battle.Outcome, battle.TickCount));
            Assert.AreEqual((0, 1, 0), (battle.Progression.Xp, battle.Progression.Level, battle.Board.TowerCount));
            Assert.AreEqual(175, battle.Gold, "4e's pin holds with XP and the pool on");
            Assert.AreEqual(0, battle.Progression.ChoicesOpened);
        }

        [Test]
        public void S5a_AC4_The_Share_The_Bar_Shows_Is_The_Levels_Own_XP_Over_The_Levels_Item_At_Every_Tick_Of_The_Real_Run()
        {
            Battle battle = Build(true, out _);
            Progression p = battle.Progression;
            int checkedTicks = 0;
            float maxShare = 0f;
            Replay(battle, tick =>
            {
                // The model: levels come from the running total and the document's table alone.
                int level = 1, start = 0;
                while (level <= DocTable.Length && p.Xp >= start + DocTable[level - 1]) start += DocTable[level++ - 1];
                Assert.AreEqual(level, p.Level, $"tick {tick}");
                Assert.AreEqual(p.Xp - start, p.XpIntoLevel, $"tick {tick}");
                Assert.AreEqual(DocTable[level - 1], p.XpForLevel, $"tick {tick}");
                float share = (float)p.XpIntoLevel / p.XpForLevel;
                Assert.That(share, Is.InRange(0f, 1f).And.LessThan(1f), $"tick {tick}");
                if (tick > 1288 && tick < 1452) Assert.AreEqual(p.Xp / 30f, share, 1e-6f, $"tick {tick}: level 1's share");
                maxShare = Mathf.Max(maxShare, share);
                checkedTicks++;
            });
            Assert.AreEqual(10026, checkedTicks);
            Assert.AreEqual(130f / 170f, (float)battle.Progression.XpIntoLevel / battle.Progression.XpForLevel, 1e-6f, "the bar ends three quarters full");
        }

        // ---- 5a-AC3 Nothing else changes

        private static List<(int, int, int, int, int, int, RunOutcome, int, float)> Snapshots(bool withTable, out Battle battle)
        {
            battle = Build(withTable, out Board board, pool: false);
            Battle b = battle;
            var list = new List<(int, int, int, int, int, int, RunOutcome, int, float)>();
            Replay(b, _ =>
            {
                float sum = 0f;
                for (int e = 0; e < board.EnemyCount; e++) sum += board.EnemyAt(e).X * 3f + board.EnemyAt(e).Y + board.EnemyAt(e).Health + board.EnemyAt(e).WaveIndex;
                list.Add((b.Gold, b.Lives, board.EnemyCount, b.HitCount, b.ProjectileCount, b.SurvivedWaves, b.Outcome, b.TickCount, sum));
            }, picks: false);
            return list;
        }

        [Test]
        public void S5b_AC4_The_Real_Run_With_The_Table_And_No_Pool_Equals_The_Run_Without_A_Table_On_Every_Tick_In_Everything_But_The_Level()
        {
            var with = Snapshots(true, out Battle a);
            var without = Snapshots(false, out Battle b);

            Assert.Greater(with.Count, 3000, "a real run");
            Assert.AreEqual(without, with, "gold, lives, enemies, hits, shots, survived waves, outcome and enemy positions after every tick");
            Assert.AreEqual(b.Progression.Xp, a.Progression.Xp);
            Assert.AreEqual((1, true), (b.Progression.Level, b.Progression.TableEnded), "without a table the XP counts and the level stays 1");
            Assert.AreEqual(0, a.Progression.ChoicesOpened, "a pool of no perk never opens a choice, whatever the level");
            Assert.AreEqual(0, a.Modifiers.Count);
            int level = 1, start = 0;
            while (level <= DocTable.Length && a.Progression.Xp >= start + DocTable[level - 1]) start += DocTable[level++ - 1];
            Assert.AreEqual(level, a.Progression.Level, "the level the table gives that XP");
            Assert.AreEqual((b.Outcome, b.Lives, b.Gold, b.TickCount, b.HitCount), (a.Outcome, a.Lives, a.Gold, a.TickCount, a.HitCount));
        }

        // ---- 5a-AC9 Determinism and cost

        [Test]
        public void S5a_AC9_Two_Replays_Of_The_Real_Run_Are_Identical_Tick_For_Tick_With_The_XP_And_The_Level_Compared_After_Every_Tick()
        {
            List<(int, int, int, int, int)> Run()
            {
                Battle battle = Build(true, out _);
                var list = new List<(int, int, int, int, int)>();
                Replay(battle, _ => list.Add((battle.Gold, battle.Lives, battle.Progression.Xp, battle.Progression.Level, battle.Progression.XpIntoLevel)));
                return list;
            }

            var first = Run();
            var second = Run();
            Assert.AreEqual(10026, first.Count);
            Assert.AreEqual(first, second);
        }

        [Test]
        public void S5a_AC9_No_Tick_Allocates_That_Did_Not_Allocate_Without_The_Table_The_Seven_Level_Ticks_And_The_Kill_Ticks_Included()
        {
            // The pool-growing ticks of 4e allocate; a tick with a kill or a level must not add to them. Compared tick by tick with the run without a table.
            var levelAt = new int[10400];
            int[] Measure(bool withTable)
            {
                Battle battle = Build(withTable, out _, pool: false);
                var counts = new int[10400];
                var order = Reference5b.Actions;
                int next = 0, ticks = 0;
                while (battle.Outcome == RunOutcome.Running && ticks < 12000)
                {
                    while (next < order.Length && order[next].tick == ticks)
                    {
                        var a = order[next++];
                        if (a.buy) battle.TryBuyTower(new GridPos(a.x, a.y), Tower(a.light));
                        else battle.TrySellTower(new GridPos(a.x, a.y));
                    }
                    counts[ticks + 1] = TickAllocation.AllocationsOf(battle.Tick);
                    levelAt[ticks + 1] = battle.Progression.Level;
                    ticks++;
                }
                return counts;
            }

            Measure(true); // warm-up: the first run of the process may allocate for the first use of a code path
            int[] control = Measure(false);
            int[] withTable = Measure(true);
            for (int i = 1; i < control.Length; i++) Assert.AreEqual(control[i], withTable[i], $"tick {i}: allocations with the table and without");
            int levelTicks = 0;
            for (int i = 2; i < levelAt.Length; i++)
                if (levelAt[i] > levelAt[i - 1]) { levelTicks++; Assert.LessOrEqual(withTable[i], control[i], $"level tick {i}"); }
            Assert.Greater(levelTicks, 0, "the run gained a level");
        }

        // ---- 5a-AC9 Source rules

        [Test]
        public void S5a_AC9_No_File_Outside_Core_And_The_Tests_Calls_AddXp_And_In_Core_Only_The_Battle_Does()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "_Game"));
            string tests = Path.Combine(root, "Tests") + Path.DirectorySeparatorChar;
            string core = Path.Combine(root, "Core") + Path.DirectorySeparatorChar;
            int scanned = 0, callers = 0;
            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(file);
                if ((extension != ".cs" && extension != ".uxml" && extension != ".uss") || file.StartsWith(tests)) continue;
                scanned++;
                string text = File.ReadAllText(file);
                if (!text.Contains("AddXp(")) continue;
                callers++;
                Assert.IsTrue(file.StartsWith(core), file + ": outside Core");
                Assert.IsTrue(Path.GetFileName(file) == "Battle.cs" || Path.GetFileName(file) == "Progression.cs", file + ": in Core, but neither the battle nor the progression");
            }
            Assert.Greater(scanned, 20);
            Assert.AreEqual(2, callers, "Progression defines it and Battle calls it");
            string battleText = File.ReadAllText(Path.Combine(root, "Core", "Battle.cs"));
            Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(battleText, @"AddXp\(").Count, "the battle adds XP in one place, on a kill");
        }

        [Test]
        public void S5a_AC9_The_Xp_Bar_View_Holds_No_Number_Colour_Or_Wording_Of_Its_Own()
        {
            string text = File.ReadAllText(Path.Combine(Application.dataPath, "_Game", "Presentation", "XpBarView.cs"));
            // Comments and the developer's error message are not part of what the view shows.
            text = System.Text.RegularExpressions.Regex.Replace(text, @"//[^\n]*", "");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"/\*.*?\*/", "", System.Text.RegularExpressions.RegexOptions.Singleline);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"Debug\.LogError\(.*?\);", "", System.Text.RegularExpressions.RegexOptions.Singleline);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\[Tooltip\(.*?\)\]", "", System.Text.RegularExpressions.RegexOptions.Singleline);

            var allowedStrings = new HashSet<string> { "xp-row", "level-label", "xp-fill", "xp-row--pop", "xp-row--max", "Texts" };
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, "\"((?:[^\"\\\\]|\\\\.)*)\""))
                Assert.Contains(m.Groups[1].Value, new List<string>(allowedStrings), "a string the view invents");
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, @"(?<![\w.])\d+(\.\d+)?f?\b"))
                Assert.Contains(m.Value, new[] { "0", "1", "0f", "1f" }, "a number the view invents: " + m.Value);
            foreach (string forbidden in new[] { "Color", "rgb", "#", "0.15", "0.3", "1.5", "transition" })
                StringAssert.DoesNotContain(forbidden, text.Replace("Debug", ""), "the view holds a colour, number or text of the direction");
        }
    }
}
