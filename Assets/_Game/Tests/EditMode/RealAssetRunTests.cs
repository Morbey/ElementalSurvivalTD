using System.Collections.Generic;
using System.IO;
using ElementalTD.Data;
using NUnit.Framework;
using UnityEditor;

namespace ElementalTD.Core.Tests
{
    /// <summary>
    /// The shipped assets and the design targets of docs/balance/0001-prototype-economy.md (step 4b) built on them:
    /// 4b-AC11 (the assets hold the document) and 4b-AC12 (the runs of the document). The wave entry ticks stay a 3a-AC13 test.
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

        // ---- 4b-AC11 Data: the values of the balance document

        // Document order of the waves: element, enemy asset, health, bonus.
        private static readonly (string element, string asset, float health, int bonus)[] Waves =
        {
            ("Shadow", "Enemy_Shadow45.asset", 45f, 40), ("Light", "Enemy_Light55.asset", 55f, 20), ("Light", "Enemy_Light70.asset", 70f, 25),
            ("Shadow", "Enemy_Shadow85.asset", 85f, 30), ("Light", "Enemy_Light105.asset", 105f, 35), ("Shadow", "Enemy_Shadow135.asset", 135f, 40),
            ("Shadow", "Enemy_Shadow180.asset", 180f, 45), ("Light", "Enemy_Light245.asset", 245f, 50), ("Shadow", "Enemy_Shadow345.asset", 345f, 55),
            ("Shadow", "Enemy_Shadow460.asset", 460f, 60),
        };

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
        public void S4b_AC11_The_Run_Holds_The_Document_Wave_By_Wave()
        {
            var run = Load<RunDefinition>("Run_Prototype.asset");
            Assert.AreEqual(115, run.StartingGold);
            Assert.AreEqual(50, run.SellRefundPercent);
            Assert.AreEqual(10, run.Lives);

            Wave[] waves = run.ToWaves();
            Assert.AreEqual(Waves.Length, waves.Length);
            for (int i = 0; i < waves.Length; i++)
            {
                string n = $"wave {i + 1}";
                EnemyStats asset = Load<EnemyDefinition>(Waves[i].asset).ToStats();
                WaveGroup group = waves[i].GroupAt(0);
                Assert.AreEqual(1, waves[i].GroupCount, n + ": one group");
                Assert.AreEqual(Waves[i].bonus, waves[i].SurvivalBonus, n + " bonus");
                Assert.AreEqual(3 + i, group.Count, n + " count");
                Assert.AreEqual(1f, group.SpawnIntervalSeconds, n + " interval");
                Assert.AreEqual(i == 0 ? 15f : 8f, waves[i].DelayBeforeSeconds, n + " delay");
                Assert.AreEqual((2f, Waves[i].health, 10), (group.Enemy.Speed, group.Enemy.MaxHealth, group.Enemy.GoldReward), n + " enemy");
                Assert.AreEqual(Waves[i].element == "Light" ? LightElement : ShadowElement, group.Enemy.Element, n + " element");
                Assert.AreEqual((asset.Speed, asset.MaxHealth, asset.GoldReward, asset.Element),
                    (group.Enemy.Speed, group.Enemy.MaxHealth, group.Enemy.GoldReward, group.Enemy.Element), $"{n} uses {Waves[i].asset}");
            }
        }

        [Test]
        public void S4b_AC11_Every_Tower_And_Enemy_Asset_Has_An_Element_The_Chart_Lists()
        {
            var listed = new HashSet<UnityEngine.Object>();
            SerializedProperty elements = new SerializedObject(Load<ElementChartDefinition>("ElementChart.asset")).FindProperty("elements");
            for (int i = 0; i < elements.arraySize; i++) listed.Add(elements.GetArrayElementAtIndex(i).objectReferenceValue);

            string[] towers = AssetDatabase.FindAssets("t:TowerDefinition", new[] { Dir.TrimEnd('/') });
            string[] enemies = AssetDatabase.FindAssets("t:EnemyDefinition", new[] { Dir.TrimEnd('/') });
            Assert.AreEqual(2, towers.Length, "the two towers");
            Assert.AreEqual(10, enemies.Length, "the ten enemies of the waves");
            foreach (string guid in towers)
                AssertListedElement(new SerializedObject(AssetDatabase.LoadAssetAtPath<TowerDefinition>(AssetDatabase.GUIDToAssetPath(guid))), listed, guid);
            foreach (string guid in enemies)
                AssertListedElement(new SerializedObject(AssetDatabase.LoadAssetAtPath<EnemyDefinition>(AssetDatabase.GUIDToAssetPath(guid))), listed, guid);
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

        // ---- 4b-AC12 Design target (provisional, from the balance document)

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
            (2220, 10, 1, ElementName.Light, true, 5),
            (3360, 4, 6, ElementName.Shadow, true, 50),
            (3390, 4, 7, ElementName.Shadow, true, 20),
            (3630, 3, 7, ElementName.Shadow, true, 0),
            (3780, 6, 8, ElementName.Light, true, 10),
            (3960, 0, 6, ElementName.Light, true, 0),
            (4290, 1, 6, ElementName.Light, true, 35),
            (4650, 3, 4, ElementName.Light, true, 5),
            (5190, 2, 5, ElementName.Light, true, 5),
            (5970, 1, 3, ElementName.Shadow, true, 85),
            (5970, 1, 2, ElementName.Shadow, true, 45),
            (6030, 3, 3, ElementName.Shadow, true, 15),
            (6360, 3, 2, ElementName.Shadow, true, 50),
            (6360, 6, 5, ElementName.Shadow, true, 10),
            (6360, 11, 2, ElementName.Light, false, 30),
            (6360, 11, 3, ElementName.Light, false, 50),
            (6360, 7, 7, ElementName.Shadow, true, 10),
            (6810, 7, 5, ElementName.Light, true, 0),
            (7200, 8, 3, ElementName.Light, true, 0),
            (7410, 9, 2, ElementName.Light, true, 40),
            (7410, 8, 5, ElementName.Light, true, 0),
            (8340, 7, 3, ElementName.Light, true, 0),
            (8700, 7, 1, ElementName.Light, true, 0),
            (8940, 6, 1, ElementName.Light, true, 55),
            (8940, 9, 1, ElementName.Light, true, 15),
            (9510, 0, 3, ElementName.Light, true, 5),
            (9900, 3, 1, ElementName.Light, true, 5),
            };
            var result = new (int, int, int, bool, bool, int)[rows.Length];
            for (int i = 0; i < rows.Length; i++)
                result[i] = (rows[i].Item1, rows[i].Item2, rows[i].Item3, rows[i].Item4 == ElementName.Light, rows[i].Item5, rows[i].Item6);
            return result;
        }

        private enum ElementName { Light, Shadow }

        [Test]
        public void S4b_AC12_At_The_Start_The_Gold_Buys_Exactly_Two_Towers_And_Refuses_The_Third()
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
        public void S4b_AC12_DesignTarget_Building_Nothing_Is_Defeated_At_Tick_2931_With_175_Gold_And_Two_Bonuses()
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
        public void S4b_AC12_DesignTarget_The_Reference_Build_Order_Wins_With_All_Lives_And_105_Gold_Left()
        {
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
            Assert.AreEqual(10165, ticks, 0, "document: won on the 10165th Tick()");
            Assert.AreEqual(1288, firstKillTick, 0, "document: the first kill pays on tick 1288");
            Assert.AreEqual(10, firstKillPay);
            Assert.AreEqual((16, 12), (lightTowers, shadowTowers), "document: 28 towers, 16 Light and 12 Shadow");
        }

        [Test]
        public void S4b_AC12_Rule_10_A_Shadow_Tower_Takes_A_Light_Enemy_Of_45_To_25_5_And_Kills_It_Third_And_Light_Cannot_Hurt_It()
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
