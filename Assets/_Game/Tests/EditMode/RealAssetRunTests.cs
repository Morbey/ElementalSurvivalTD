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
            (8f, 50, 360f, "L2 S1 L2 S1 L2 S1 L1", 3, 7),
            (8f, 55, 680f, "S1 L1 S1 L1 S1 L1 S1 L1 S1 L1 S1", 6, 5),
            (8f, 60, 800f, "L1 S1 L1 S1 L1 S1 L1 S1 L1 S1 L1 S1", 6, 6),
        };

        // The 13 enemy assets of the document.
        private static readonly string[] DocEnemyAssets =
        {
            "Enemy_Shadow45", "Enemy_Light55", "Enemy_Light70", "Enemy_Shadow100", "Enemy_Light100", "Enemy_Shadow180", "Enemy_Light180",
            "Enemy_Shadow360", "Enemy_Light360", "Enemy_Shadow680", "Enemy_Light680", "Enemy_Shadow800", "Enemy_Light800",
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

        // ---- 4e-AC6 Start and building nothing (the reference order with its picks is ReferenceRunTests, 5b-AC9)

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
