using System;
using System.Collections.Generic;
using System.IO;
using ElementalTD.Data;
using NUnit.Framework;
using UnityEditor;

namespace ElementalTD.Core.Tests
{
    /// <summary>
    /// The shipped assets and the design targets of docs/balance/0001-prototype-economy.md (step 4e) built on them:
    /// 4e-AC5 (the assets hold the document, group by group) and 4e-AC6 (the runs of the document). The wave entry ticks stay a 3a-AC13 test.
    /// </summary>
    public class RealAssetRunTests
    {
        private const string Dir = "Assets/_Game/Data/Assets/";

        private static T Load<T>(string name) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Dir + name);
            Assert.IsNotNull(asset, name);
            return asset;
        }

        private static ElementChart Chart() => Load<ElementChartDefinition>("ElementChart.asset").ToChart();
        private static Element LightElement => Load<ElementDefinition>("Element_Light.asset").ToElement();
        private static Element ShadowElement => Load<ElementDefinition>("Element_Shadow.asset").ToElement();

        private static Battle Build(out Board board, int? lives = null)
        {
            var map = Load<MapDefinition>("Map_Prototype.asset");
            var run = Load<RunDefinition>("Run_Prototype.asset");
            board = new Board(map.ToGridMap(), map.Spawn, map.Exit);
            return new Battle(board, run.ToWaves(), lives ?? run.Lives, run.StartingGold, run.SellRefundPercent, Chart());
        }

        private static TowerStats Tower(bool light) => Load<TowerDefinition>(light ? "Tower_Light.asset" : "Tower_Shadow.asset").ToStats();

        // ---- 4e-AC5 Data: the values of the balance document

        // The wave table of the document: delay, bonus, health of every enemy of the wave, the groups in spawn order
        // (S: Shadow, L: Light, then the count; every group's interval is 1 s), and the enemies of each element.
        private static readonly (float delay, int bonus, float health, string groups, int shadow, int light)[] DocWaves =
        {
            (15f, 40, 45f, "S3", 3, 0),
            (8f, 20, 55f, "L4", 0, 4),
            (8f, 25, 70f, "L5", 0, 5),
            (8f, 30, 100f, "S3 L3", 3, 3),
            (8f, 35, 100f, "L4 S3", 3, 4),
            (8f, 40, 180f, "S1 L1 S1 L1 S1 L1 S1 L1", 4, 4),
            (8f, 45, 180f, "S2 L1 S2 L1 S2 L1", 6, 3),
            (8f, 50, 260f, "L2 S1 L2 S1 L2 S1 L1", 3, 7),
            (8f, 55, 360f, "S1 L1 S1 L1 S1 L1 S1 L1 S1 L1 S1", 6, 5),
            (8f, 60, 420f, "L1 S1 L1 S1 L1 S1 L1 S1 L1 S1 L1 S1", 6, 6),
        };

        // The 13 enemy assets of the document.
        private static readonly string[] DocEnemyAssets =
        {
            "Enemy_Shadow45", "Enemy_Light55", "Enemy_Light70", "Enemy_Shadow100", "Enemy_Light100", "Enemy_Shadow180", "Enemy_Light180",
            "Enemy_Shadow260", "Enemy_Light260", "Enemy_Shadow360", "Enemy_Light360", "Enemy_Shadow420", "Enemy_Light420",
        };

        private static (string element, int count)[] ParseGroups(string spec)
        {
            string[] parts = spec.Split(' ');
            var groups = new (string, int)[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                groups[i] = (parts[i][0] == 'S' ? "Shadow" : "Light", int.Parse(parts[i].Substring(1)));
            return groups;
        }

        [Test]
        public void S4b_AC11_The_Elements_And_The_Chart_Hold_The_Document()
        {
            var light = Load<ElementDefinition>("Element_Light.asset");
            var shadow = Load<ElementDefinition>("Element_Shadow.asset");
            Assert.AreEqual("Light", light.DisplayName);
            Assert.AreEqual("Shadow", shadow.DisplayName);
            Assert.AreNotEqual(light.ToElement(), shadow.ToElement());

            ElementChart chart = Chart();
            Assert.AreEqual(2f, chart.Multiplier(LightElement, ShadowElement), "Light against Shadow");
            Assert.AreEqual(2f, chart.Multiplier(ShadowElement, LightElement), "Shadow against Light");
            Assert.AreEqual(0f, chart.Multiplier(LightElement, LightElement));
            Assert.AreEqual(0f, chart.Multiplier(ShadowElement, ShadowElement));

            var serialized = new SerializedObject(Load<ElementChartDefinition>("ElementChart.asset"));
            Assert.AreEqual(2, serialized.FindProperty("elements").arraySize, "two elements");
            Assert.AreEqual(2, serialized.FindProperty("relations").arraySize, "two rows");
        }

        [Test]
        public void S4b_AC11_The_Two_Towers_Differ_Only_In_Their_Element()
        {
            TowerStats light = Tower(true), shadow = Tower(false);

            Assert.AreEqual(LightElement, light.Element);
            Assert.AreEqual(ShadowElement, shadow.Element);
            Assert.AreEqual((40, 2.5f, 10f, 1.5f, 10f), (light.Cost, light.Range, light.Damage, light.ShotsPerSecond, light.ProjectileSpeed));
            Assert.AreEqual((light.Cost, light.Range, light.Damage, light.ShotsPerSecond, light.ProjectileSpeed),
                (shadow.Cost, shadow.Range, shadow.Damage, shadow.ShotsPerSecond, shadow.ProjectileSpeed));
        }

        [Test]
        public void S4e_AC5_The_Run_Holds_The_Document_Group_By_Group()
        {
            var runAsset = Load<RunDefinition>("Run_Prototype.asset");
            Assert.AreEqual(115, runAsset.StartingGold);
            Assert.AreEqual(50, runAsset.SellRefundPercent);
            Assert.AreEqual(10, runAsset.Lives);

            Wave[] waves = runAsset.ToWaves();
            SerializedProperty serializedWaves = new SerializedObject(runAsset).FindProperty("waves");
            Assert.AreEqual(DocWaves.Length, waves.Length);
            Assert.AreEqual(DocWaves.Length, serializedWaves.arraySize);
            int total = 0;
            for (int i = 0; i < waves.Length; i++)
            {
                string n = $"wave {i + 1}";
                var groups = ParseGroups(DocWaves[i].groups);
                Assert.AreEqual(groups.Length, waves[i].GroupCount, n + ": groups");
                Assert.AreEqual(DocWaves[i].bonus, waves[i].SurvivalBonus, n + " bonus");
                Assert.AreEqual(DocWaves[i].delay, waves[i].DelayBeforeSeconds, n + " delay");

                int shadow = 0, light = 0, enemies = 0;
                for (int g = 0; g < groups.Length; g++)
                {
                    string where = $"{n}, group {g + 1} ({groups[g].element} x{groups[g].count})";
                    string assetName = $"Enemy_{groups[g].element}{DocWaves[i].health}.asset";
                    EnemyStats asset = Load<EnemyDefinition>(assetName).ToStats();
                    WaveGroup group = waves[i].GroupAt(g);
                    Assert.AreEqual(groups[g].count, group.Count, where + " count");
                    Assert.AreEqual(1f, group.SpawnIntervalSeconds, where + " interval");
                    Assert.AreEqual((2f, DocWaves[i].health, 10), (group.Enemy.Speed, group.Enemy.MaxHealth, group.Enemy.GoldReward), where + " enemy");
                    Assert.AreEqual(groups[g].element == "Light" ? LightElement : ShadowElement, group.Enemy.Element, where + " element");
                    Assert.AreEqual((asset.Speed, asset.MaxHealth, asset.GoldReward, asset.Element),
                        (group.Enemy.Speed, group.Enemy.MaxHealth, group.Enemy.GoldReward, group.Enemy.Element), $"{where} uses {assetName}");

                    SerializedProperty serializedGroup = serializedWaves.GetArrayElementAtIndex(i).FindPropertyRelative("groups").GetArrayElementAtIndex(g);
                    Assert.AreEqual(Dir + assetName, AssetDatabase.GetAssetPath(serializedGroup.FindPropertyRelative("enemy").objectReferenceValue), where + ": the asset it references");

                    enemies += group.Count;
                    if (groups[g].element == "Light") light += group.Count; else shadow += group.Count;
                }
                Assert.AreEqual((DocWaves[i].shadow, DocWaves[i].light), (shadow, light), n + ": Shadow and Light enemies");
                Assert.AreEqual(3 + i, enemies, n + ": enemies");
                total += enemies;
            }
            Assert.AreEqual(75, total, "75 enemies in the run");
        }

        [Test]
        public void S4e_AC5_At_Least_One_Wave_Has_Groups_Of_Both_Elements_And_Waves_4_To_10_All_Do()
        {
            Wave[] waves = Load<RunDefinition>("Run_Prototype.asset").ToWaves();
            int mixed = 0;
            for (int i = 0; i < waves.Length; i++)
            {
                var elements = new HashSet<Element>();
                for (int g = 0; g < waves[i].GroupCount; g++) elements.Add(waves[i].GroupAt(g).Enemy.Element);
                bool both = elements.Contains(LightElement) && elements.Contains(ShadowElement);
                if (both) mixed++;
                Assert.AreEqual(i >= 3, both, $"wave {i + 1}: waves 1 to 3 teach one element, 4 to 10 hold both");
            }
            Assert.AreEqual(7, mixed);
        }

        [Test]
        public void S4e_AC5_Every_Tower_And_Enemy_Asset_Has_An_Element_The_Chart_Lists()
        {
            var listed = new HashSet<UnityEngine.Object>();
            SerializedProperty elements = new SerializedObject(Load<ElementChartDefinition>("ElementChart.asset")).FindProperty("elements");
            for (int i = 0; i < elements.arraySize; i++) listed.Add(elements.GetArrayElementAtIndex(i).objectReferenceValue);

            string[] towers = AssetDatabase.FindAssets("t:TowerDefinition", new[] { Dir.TrimEnd('/') });
            string[] enemies = AssetDatabase.FindAssets("t:EnemyDefinition", new[] { Dir.TrimEnd('/') });
            Assert.AreEqual(2, towers.Length, "the two towers");
            Assert.AreEqual(DocEnemyAssets.Length, enemies.Length, "the enemy assets of the document");
            foreach (string guid in towers)
                AssertListedElement(new SerializedObject(AssetDatabase.LoadAssetAtPath<TowerDefinition>(AssetDatabase.GUIDToAssetPath(guid))), listed, guid);
            foreach (string guid in enemies)
                AssertListedElement(new SerializedObject(AssetDatabase.LoadAssetAtPath<EnemyDefinition>(AssetDatabase.GUIDToAssetPath(guid))), listed, guid);
        }

        [Test]
        public void S4e_AC5_The_Enemy_Assets_Are_Those_Of_The_Document_Named_By_Element_And_Health_And_Every_One_Is_Used_By_A_Group()
        {
            string[] guids = AssetDatabase.FindAssets("t:EnemyDefinition", new[] { Dir.TrimEnd('/') });
            var onDisk = new List<string>();
            foreach (string guid in guids) onDisk.Add(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)));
            CollectionAssert.AreEquivalent(DocEnemyAssets, onDisk, "13 enemy assets, none more, none less");
            foreach (string name in DocEnemyAssets) Assert.IsTrue(File.Exists(Dir + name + ".asset"), name + " on disk");

            var used = new HashSet<string>();
            SerializedProperty waves = new SerializedObject(Load<RunDefinition>("Run_Prototype.asset")).FindProperty("waves");
            for (int i = 0; i < waves.arraySize; i++)
            {
                SerializedProperty groups = waves.GetArrayElementAtIndex(i).FindPropertyRelative("groups");
                for (int g = 0; g < groups.arraySize; g++)
                    used.Add(Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(groups.GetArrayElementAtIndex(g).FindPropertyRelative("enemy").objectReferenceValue)));
            }

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                Assert.IsTrue(used.Contains(name), name + ": no group uses it");
                var match = System.Text.RegularExpressions.Regex.Match(name, @"^Enemy_(Light|Shadow)(\d+)$");
                Assert.IsTrue(match.Success, name + ": named Enemy_<Element><Health>");
                var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
                var serialized = new SerializedObject(def);
                Assert.AreEqual(match.Groups[1].Value, ((ElementDefinition)serialized.FindProperty("element").objectReferenceValue).DisplayName, name + ": the element of its name");
                Assert.AreEqual(float.Parse(match.Groups[2].Value), serialized.FindProperty("maxHealth").floatValue, name + ": the health of its name");
                Assert.AreEqual((2f, 10), (def.ToStats().Speed, def.ToStats().GoldReward), name + ": speed 2, reward 10");
            }
            Assert.AreEqual(used.Count, guids.Length, "every enemy asset is used, and a group uses nothing else");
        }

        private static void AssertListedElement(SerializedObject asset, HashSet<UnityEngine.Object> listed, string guid)
        {
            UnityEngine.Object element = asset.FindProperty("element").objectReferenceValue;
            Assert.IsNotNull(element, AssetDatabase.GUIDToAssetPath(guid) + " has no element");
            Assert.IsTrue(listed.Contains(element), AssetDatabase.GUIDToAssetPath(guid) + " has an element the chart does not list");
        }

        [Test]
        public void S4b_AC11_The_Neutral_Prototype_Assets_Are_Gone()
        {
            foreach (string name in new[] { "Tower_Prototype.asset", "Enemy_Prototype.asset" })
            {
                Assert.IsNull(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Dir + name), name);
                Assert.IsFalse(File.Exists(Dir + name), name + " on disk");
            }
            Assert.AreEqual(0, AssetDatabase.FindAssets("Enemy_Health", new[] { Dir.TrimEnd('/') }).Length, "the old health assets");
        }

        [Test]
        public void S3a_AC13_The_Waves_Enter_At_The_Planned_Ticks_And_The_Last_Enemy_Enters_152s_In()
        {
            Battle battle = Build(out _, lives: 100_000); // enough lives for the run to stay open
            var expectedFirst = new[] { 901, 1501, 2160, 2880, 3660, 4500, 5400, 6360, 7380, 8460 };
            var firstTicks = new List<int>();
            int started = 0, last = -1;
            for (int tick = 1; tick <= 12000 && last < 0; tick++)
            {
                battle.Tick();
                if (battle.Waves.StartedWaves > started)
                {
                    started = battle.Waves.StartedWaves;
                    firstTicks.Add(tick);
                }
                if (battle.Waves.IsFinished) last = tick;
            }

            Assert.AreEqual(expectedFirst.Length, firstTicks.Count);
            for (int i = 0; i < expectedFirst.Length; i++) Assert.AreEqual(expectedFirst[i], firstTicks[i], 1, $"wave {i + 1}");
            Assert.AreEqual(9120, last, 1, "the last enemy enters 152 s in");
        }

        // ---- 4e-AC6 Design target (provisional, from the balance document)

        // The reference build order of the document. Tick (Tick() calls done before the action), cell, tower, buy or sell, gold after.
        private static readonly (int tick, int x, int y, bool light, bool buy, int goldAfter)[] ReferenceOrder = ReferenceRows();

        private static (int, int, int, bool, bool, int)[] ReferenceRows()
        {
            (int, int, int, ElementName, bool, int)[] rows =
            {
            (0, 0, 8, ElementName.Light, true, 75),
            (0, 0, 8, ElementName.Light, false, 115),
            (0, 11, 2, ElementName.Light, true, 75),
            (0, 11, 3, ElementName.Light, true, 35),
            (1290, 14, 2, ElementName.Shadow, true, 5),
            (1470, 13, 2, ElementName.Shadow, true, 25),
            (2130, 12, 0, ElementName.Shadow, true, 5),
            (2220, 10, 1, ElementName.Shadow, true, 5),
            (2910, 4, 7, ElementName.Light, true, 5),
            (3360, 3, 7, ElementName.Light, true, 10),
            (3630, 4, 6, ElementName.Shadow, true, 0),
            (3840, 6, 8, ElementName.Shadow, true, 10),
            (4110, 1, 6, ElementName.Light, true, 0),
            (4470, 0, 6, ElementName.Light, true, 35),
            (4620, 3, 4, ElementName.Light, true, 5),
            (5310, 2, 5, ElementName.Light, true, 5),
            (5580, 1, 2, ElementName.Shadow, true, 45),
            (5580, 3, 2, ElementName.Shadow, true, 5),
            (5970, 1, 3, ElementName.Shadow, true, 5),
            (6390, 3, 3, ElementName.Light, true, 50),
            (6390, 6, 5, ElementName.Light, true, 10),
            (6930, 7, 7, ElementName.Shadow, true, 0),
            (7410, 7, 5, ElementName.Light, true, 0),
            (7860, 8, 3, ElementName.Shadow, true, 50),
            (7860, 9, 2, ElementName.Light, true, 10),
            (8310, 8, 5, ElementName.Shadow, true, 0),
            (8790, 6, 1, ElementName.Light, true, 0),
            (9000, 7, 3, ElementName.Shadow, true, 65),
            (9000, 7, 1, ElementName.Light, true, 25),
            (9450, 9, 1, ElementName.Shadow, true, 5),
            (9900, 0, 3, ElementName.Shadow, true, 5),
            };
            var result = new (int, int, int, bool, bool, int)[rows.Length];
            for (int i = 0; i < rows.Length; i++)
                result[i] = (rows[i].Item1, rows[i].Item2, rows[i].Item3, rows[i].Item4 == ElementName.Light, rows[i].Item5, rows[i].Item6);
            return result;
        }

        private enum ElementName { Light, Shadow }

        [Test]
        public void S4e_AC6_At_The_Start_The_Gold_Buys_Exactly_Two_Towers_And_Refuses_The_Third()
        {
            foreach ((bool first, bool second) in new[] { (true, true), (true, false), (false, true), (false, false) })
            {
                Battle battle = Build(out _);
                Assert.AreEqual(115, battle.Gold);
                Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(new GridPos(2, 5), Tower(first)));
                Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(new GridPos(3, 5), Tower(second)));
                Assert.AreEqual(35, battle.Gold);
                Assert.AreEqual(PlacementResult.NotEnoughGold, battle.TryBuyTower(new GridPos(6, 6), Tower(true)));
                Assert.AreEqual(PlacementResult.NotEnoughGold, battle.TryBuyTower(new GridPos(6, 6), Tower(false)));
                Assert.AreEqual(2, battle.Board.TowerCount);
            }
        }

        [Test]
        public void S4e_AC6_DesignTarget_Building_Nothing_Is_Defeated_At_Tick_2931_With_175_Gold_And_Two_Bonuses()
        {
            Battle battle = Build(out _);
            int ticks = 0, wave1 = -1, wave2 = -1;
            while (battle.Outcome == RunOutcome.Running && ticks < 6000)
            {
                battle.Tick();
                ticks++;
                if (wave1 < 0 && battle.SurvivedWaves >= 1) { wave1 = ticks; Assert.AreEqual(155, battle.Gold, "wave 1 pays 40"); }
                if (wave2 < 0 && battle.SurvivedWaves >= 2) { wave2 = ticks; Assert.AreEqual(175, battle.Gold, "wave 2 pays 20"); }
            }

            Assert.AreEqual(RunOutcome.Lost, battle.Outcome);
            Assert.AreEqual(2931, ticks, 0, "document: lost on the 2931st Tick()");
            Assert.AreEqual(1672, wave1, 0, "document: wave 1 pays on tick 1672");
            Assert.AreEqual(2332, wave2, 0, "document: wave 2 pays on tick 2332");
            Assert.AreEqual(2, battle.SurvivedWaves);
            Assert.AreEqual(0, battle.Lives);
            Assert.AreEqual(175, battle.Gold, "115 + 40 + 20");
            Assert.AreEqual(0, battle.Board.TowerCount);
        }

        [Test]
        public void S4e_AC6_DesignTarget_The_Reference_Build_Order_Wins_On_Tick_10317_With_All_Lives_75_Kills_And_105_Gold_Left()
        {
            var run = Load<RunDefinition>("Run_Prototype.asset");
            Battle battle = Build(out Board board);
            int next = 0, ticks = 0, firstKillTick = -1, firstKillPay = -1;
            while (battle.Outcome == RunOutcome.Running && ticks < 12000)
            {
                while (next < ReferenceOrder.Length && ReferenceOrder[next].tick == ticks)
                {
                    var a = ReferenceOrder[next++];
                    var cell = new GridPos(a.x, a.y);
                    string where = $"tick {a.tick} ({a.x},{a.y}) {(a.buy ? "buy" : "sell")}";
                    if (a.buy) Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(cell, Tower(a.light)), where);
                    else Assert.IsTrue(battle.TrySellTower(cell), where);
                    Assert.AreEqual(a.goldAfter, battle.Gold, where + ": gold after");
                }

                int gold = battle.Gold;
                battle.Tick();
                ticks++;
                if (firstKillTick < 0 && battle.Gold > gold) { firstKillTick = ticks; firstKillPay = battle.Gold - gold; }
            }

            int lightTowers = 0, shadowTowers = 0;
            for (int i = 0; i < board.TowerCount; i++)
            {
                if (board.TowerAt(i).Stats.Element == LightElement) lightTowers++;
                else if (board.TowerAt(i).Stats.Element == ShadowElement) shadowTowers++;
            }

            Assert.AreEqual(ReferenceOrder.Length, next, "every action ran");
            Assert.AreEqual(RunOutcome.Won, battle.Outcome);
            Assert.AreEqual(10, battle.Lives);
            Assert.AreEqual(105, battle.Gold, "document: 105 left");
            Assert.AreEqual(10317, ticks, 0, "document: won on the 10317th Tick()");
            Assert.AreEqual(1288, firstKillTick, 0, "document: the first kill pays on tick 1288");
            Assert.AreEqual(10, firstKillPay);
            Assert.AreEqual((14, 15), (lightTowers, shadowTowers), "document: 29 towers, 14 Light and 15 Shadow");

            // Every enemy was killed and none leaked (10 lives), so the gold tells the kills: start + 10 a kill + the bonuses - 40 a tower kept.
            int bonuses = 0;
            for (int w = 0; w < battle.Waves.WaveCount; w++) bonuses += battle.Waves.WaveAt(w).SurvivalBonus;
            Assert.AreEqual(75, (battle.Gold - run.StartingGold - bonuses + Tower(true).Cost * board.TowerCount) / 10, "document: 75 kills");
            Assert.AreEqual(400, bonuses, "document: 400 of bonuses");
        }

        /// <summary>Replays the reference order tick by tick, as the document does: each action runs after the given number of ticks, before the next.</summary>
        private static void Replay(Battle battle, Board board, Action<int> beforeTick, Action<int> afterTick, int maxTicks = 12000)
        {
            int next = 0, ticks = 0;
            while (battle.Outcome == RunOutcome.Running && ticks < maxTicks)
            {
                while (next < ReferenceOrder.Length && ReferenceOrder[next].tick == ticks)
                {
                    var a = ReferenceOrder[next++];
                    var cell = new GridPos(a.x, a.y);
                    if (a.buy) Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(cell, Tower(a.light)));
                    else Assert.IsTrue(battle.TrySellTower(cell));
                }
                beforeTick?.Invoke(ticks);
                battle.Tick();
                ticks++;
                afterTick?.Invoke(ticks);
            }
        }

        private static readonly System.Reflection.PropertyInfo ProjectileTarget =
            typeof(Projectile).GetProperty("Target", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        // Per wave of the document ("Reference order" column of the per-wave table): recoverable and wasted launches in percent (counted on the wave
        // of the enemy shot at), and the share of the wave's busy ticks with both elements on the board.
        private static readonly (int recoverable, int wasted, int both)[] DocShares =
        {
            (0, 0, 0), (0, 61, 0), (0, 50, 9), (0, 45, 52), (11, 50, 53), (28, 50, 92), (29, 48, 89), (28, 58, 92), (36, 51, 100), (35, 52, 97),
        };

        [Test]
        public void S4e_AC6_DesignTarget_On_The_Reference_Order_52_Percent_Of_The_Launches_Are_Wasted_29_Percent_With_A_Damageable_Enemy_In_Range_And_Each_Wave_Matches_The_Documents_Table()
        {
            Battle battle = Build(out Board board);
            Assert.IsNotNull(ProjectileTarget, "Projectile.Target (internal) is read by reflection to name the enemy a launch was aimed at");
            var before = new List<Enemy>();
            int[] shots = new int[10], wasted = new int[10], recoverable = new int[10], busy = new int[10], mixedTicks = new int[10];
            Replay(battle, board, _ =>
            {
                before.Clear();
                for (int i = 0; i < board.EnemyCount; i++) before.Add(board.EnemyAt(i));
            }, _ =>
            {
                // Enemies alive when the towers fired: those still on the board, and those killed later in this tick (health 0, not removed by the exit).
                var candidates = new List<Enemy>();
                for (int i = 0; i < board.EnemyCount; i++) candidates.Add(board.EnemyAt(i));
                foreach (Enemy e in before) if (!e.IsOnBoard && e.Health <= 0f) candidates.Add(e);

                for (int i = 0; i < battle.ProjectileCount; i++)
                {
                    Projectile shot = battle.ProjectileAt(i);
                    var origin = new GridPos((int)shot.PreviousX, (int)shot.PreviousY);
                    Tower tower = board.TowerOn(origin);
                    if (tower == null || shot.PreviousX != origin.X || shot.PreviousY != origin.Y) continue;   // not launched this tick
                    var target = (Enemy)ProjectileTarget.GetValue(shot);
                    int wave = target.WaveIndex;
                    shots[wave]++;
                    if (target.Element != shot.Element) continue;
                    wasted[wave]++;
                    foreach (Enemy e in candidates)
                    {
                        float dx = e.X - origin.X, dy = e.Y - origin.Y;
                        if (e.Element != shot.Element && dx * dx + dy * dy <= tower.Stats.Range * tower.Stats.Range) { recoverable[wave]++; break; }
                    }
                }

                if (board.EnemyCount == 0) return;
                bool light = false, shadow = false;
                var waves = new HashSet<int>();
                for (int i = 0; i < board.EnemyCount; i++)
                {
                    if (board.EnemyAt(i).Element == LightElement) light = true; else shadow = true;
                    waves.Add(board.EnemyAt(i).WaveIndex);
                }
                foreach (int w in waves)
                {
                    busy[w]++;
                    if (light && shadow) mixedTicks[w]++;
                }
            });

            Assert.AreEqual(RunOutcome.Won, battle.Outcome);
            int allShots = 0, allWasted = 0, allRecoverable = 0;
            var lines = new List<string>();
            for (int w = 0; w < 10; w++)
            {
                allShots += shots[w]; allWasted += wasted[w]; allRecoverable += recoverable[w];
                lines.Add($"wave {w + 1}: {shots[w]} launches, recoverable {100f * recoverable[w] / Math.Max(1, shots[w]):F1} %, wasted {100f * wasted[w] / Math.Max(1, shots[w]):F1} %, both {100f * mixedTicks[w] / Math.Max(1, busy[w]):F1} % of {busy[w]} busy ticks");
            }
            string report = $"4e-AC6 shares: {allShots} launches, wasted {100f * allWasted / allShots:F1} %, recoverable {100f * allRecoverable / allShots:F1} %\n" + string.Join("\n", lines);
            UnityEngine.Debug.Log(report);
            Assert.AreEqual(52, UnityEngine.Mathf.RoundToInt(100f * allWasted / allShots), "document: 52 % wasted. " + report);
            Assert.AreEqual(29, UnityEngine.Mathf.RoundToInt(100f * allRecoverable / allShots), "document: 29 % recoverable. " + report);
            for (int w = 0; w < 10; w++)
            {
                // The launches counted here miss the few shots that land on the tick they leave; one point of tolerance on those.
                Assert.AreEqual(DocShares[w].recoverable, 100f * recoverable[w] / Math.Max(1, shots[w]), 1f, $"wave {w + 1}: recoverable. " + report);
                Assert.AreEqual(DocShares[w].wasted, 100f * wasted[w] / Math.Max(1, shots[w]), 1f, $"wave {w + 1}: wasted. " + report);
                Assert.AreEqual(DocShares[w].both, UnityEngine.Mathf.RoundToInt(100f * mixedTicks[w] / Math.Max(1, busy[w])), $"wave {w + 1}: busy ticks with both elements. " + report);
            }
        }

        [Test]
        public void S4e_AC7_Two_Replays_Of_The_Reference_Order_On_The_Real_Assets_Are_Identical_Tick_For_Tick()
        {
            List<(int, int, int, int, int, int, float)> Snapshots(out Battle battle)
            {
                battle = Build(out Board board);
                var list = new List<(int, int, int, int, int, int, float)>();
                Battle b = battle;
                Replay(b, board, null, _ =>
                {
                    float sum = 0f;
                    for (int e = 0; e < board.EnemyCount; e++) sum += board.EnemyAt(e).X * 3f + board.EnemyAt(e).Y + board.EnemyAt(e).Health + board.EnemyAt(e).WaveIndex;
                    list.Add((b.Gold, b.Lives, board.EnemyCount, b.HitCount, b.ProjectileCount, b.SurvivedWaves, sum));
                });
                return list;
            }

            var first = Snapshots(out Battle a);
            var second = Snapshots(out Battle b2);
            Assert.AreEqual(10317, first.Count);
            Assert.AreEqual(first, second);
            Assert.AreEqual((RunOutcome.Won, 105), (a.Outcome, a.Gold));
            Assert.AreEqual((a.Outcome, a.Gold, a.HitCount), (b2.Outcome, b2.Gold, b2.HitCount));
        }

        [Test]
        public void S4e_AC7_Ticking_The_Real_Run_Allocates_Only_To_Grow_A_Pool_And_Nothing_Else_Including_The_Ticks_Where_The_Group_Changes()
        {
            // Every tick of the reference run is measured. A pooled enemy or shot object is created the first time the pool is too small, and a shot
            // can be launched and landed inside one tick, so the end-of-tick counts cannot say when that happens: instead, a tick may allocate only
            // while there is a pool object still to create, and no more often than there are objects ever on the board at once.
            Battle battle = Build(out Board board);
            var lockstep = new WaveSchedule(Load<RunDefinition>("Run_Prototype.asset").ToWaves());
            var allocating = new List<(int tick, int count)>();
            int peakEnemies = 0, peakShots = 0, quiet = 0, quietChanges = 0, bestQuiet = 0, bestQuietChanges = 0;
            Element previous = default;
            int tickNo = 0, next = 0;
            while (battle.Outcome == RunOutcome.Running && tickNo < 12000)
            {
                while (next < ReferenceOrder.Length && ReferenceOrder[next].tick == tickNo)
                {
                    var a = ReferenceOrder[next++];
                    var cell = new GridPos(a.x, a.y);
                    if (a.buy) Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(cell, Tower(a.light)));
                    else Assert.IsTrue(battle.TrySellTower(cell));
                }
                int count = TickAllocation.AllocationsOf(battle.Tick);
                tickNo++;
                if (count != 0) { allocating.Add((tickNo, count)); quiet = 0; quietChanges = 0; }
                else quiet++;
                peakEnemies = Math.Max(peakEnemies, board.EnemyCount);
                peakShots = Math.Max(peakShots, battle.ProjectileCount);
                if (lockstep.Step(Battle.TickSeconds, out EnemyStats entered))
                {
                    if (!previous.IsNone && entered.Element != previous) quietChanges++;
                    previous = entered.Element;
                }
                if (quiet > bestQuiet) { bestQuiet = quiet; bestQuietChanges = quietChanges; }
            }

            Assert.AreEqual(RunOutcome.Won, battle.Outcome);
            string report = $"4e-AC7: {allocating.Count} allocating ticks of {tickNo}: {string.Join(", ", allocating)}; peaks {peakEnemies} enemies and {peakShots} shots at the end of a tick; the longest quiet stretch is {bestQuiet} ticks with {bestQuietChanges} changes of group";
            UnityEngine.Debug.Log(report);
            foreach ((int tick, int count) in allocating) Assert.LessOrEqual(count, 4, $"tick {tick}: one new pooled object (an enemy with its follower) and the list that holds it. " + report);
            Assert.LessOrEqual(allocating.Count, peakEnemies + 2 + peakShots + board.TowerCount, "growing the pools explains every allocation: " + report);
            Assert.AreEqual(10317, tickNo);
            Assert.GreaterOrEqual(bestQuiet, 1500, "once the pools have their size, a long stretch of the run (waves 9 and 10) allocates nothing: " + report);
            Assert.GreaterOrEqual(bestQuietChanges, 10, "and in it the enemies of wave 10 (L1 S1 six times) change element 11 times: " + report);
        }

        [Test]
        public void S4e_AC6_Rule_10_A_Shadow_Tower_Takes_A_Light_Enemy_Of_45_To_25_5_And_Kills_It_Third_And_Light_Cannot_Hurt_It()
        {
            var enemy = new EnemyStats(0f, 45f, 0, LightElement); // speed 0: it stays at the spawn

            Battle shadowBattle = Build(out Board shadowBoard);
            Assert.AreEqual(PlacementResult.Placed, shadowBattle.TryBuyTower(new GridPos(1, 3), Tower(false)));
            Enemy target = shadowBoard.SpawnEnemy(enemy);
            Assert.AreEqual(25f, HitHealth(shadowBattle, target));
            Assert.AreEqual(5f, HitHealth(shadowBattle, target));
            Assert.IsTrue(target.IsOnBoard);
            HitHealth(shadowBattle, target);
            Assert.IsFalse(target.IsOnBoard, "the third hit kills");

            Battle lightBattle = Build(out Board lightBoard);
            Assert.AreEqual(PlacementResult.Placed, lightBattle.TryBuyTower(new GridPos(1, 3), Tower(true)));
            Enemy immune = lightBoard.SpawnEnemy(enemy);
            for (int i = 0; i < 400; i++) lightBattle.Tick(); // 10 shots; the first wave enters at tick 901
            Assert.AreEqual(45f, immune.Health);
            Assert.IsTrue(immune.IsOnBoard);
            Assert.AreEqual(75, lightBattle.Gold, "no kill, no pay");
            Assert.IsTrue(lightBattle.TrySellTower(new GridPos(1, 3)));
            Assert.AreEqual(95, lightBattle.Gold, "it has fired: 50 percent of 40, not the full cost");
        }

        private static float HitHealth(Battle battle, Enemy enemy)
        {
            float before = enemy.Health;
            for (int i = 0; i < 200; i++)
            {
                battle.Tick();
                if (enemy.Health != before) return enemy.Health;
            }
            Assert.Fail($"no shot landed within 200 ticks, health stayed {before}");
            return before;
        }
    }
}
