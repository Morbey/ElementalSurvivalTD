using ElementalTD.Data;
using NUnit.Framework;
using UnityEditor;

namespace ElementalTD.Core.Tests
{
    /// <summary>The prototype assets (3b-AC8) and the design targets of docs/balance/0001-prototype-economy.md built on them (3b-AC9). The wave entry ticks stay a 3a-AC13 test.</summary>
    public class RealAssetRunTests
    {
        private const string Dir = "Assets/_Game/Data/Assets/";

        private static T Load<T>(string name) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Dir + name);
            Assert.IsNotNull(asset, name);
            return asset;
        }

        private static Battle Build(out Board board, out TowerStats tower, int? lives = null)
        {
            var map = Load<MapDefinition>("Map_Prototype.asset");
            var run = Load<RunDefinition>("Run_Prototype.asset");
            tower = Load<TowerDefinition>("Tower_Prototype.asset").ToStats();
            board = new Board(map.ToGridMap(), map.Spawn, map.Exit);
            return new Battle(board, run.ToWaves(), lives ?? run.Lives, run.StartingGold, run.SellRefundPercent);
        }

        // ---- 3b-AC8 Data: the values of the balance document

        private static readonly string[] WaveAssets =
        {
            "Enemy_Health45.asset", "Enemy_Health55.asset", "Enemy_Health65.asset", "Enemy_Health75.asset", "Enemy_Health90.asset",
            "Enemy_Prototype.asset", "Enemy_Health130.asset", "Enemy_Health175.asset", "Enemy_Health230.asset", "Enemy_Health305.asset"
        };
        private static readonly float[] WaveHealth = { 45f, 55f, 65f, 75f, 90f, 100f, 130f, 175f, 230f, 305f };

        [Test]
        public void S3b_AC8_S4a_AC5_The_Assets_Hold_The_Values_Of_The_Balance_Document()
        {
            var run = Load<RunDefinition>("Run_Prototype.asset");
            TowerStats tower = Load<TowerDefinition>("Tower_Prototype.asset").ToStats();

            Assert.AreEqual(115, run.StartingGold);
            Assert.AreEqual(50, run.SellRefundPercent);
            Assert.AreEqual(10, run.Lives);
            Assert.AreEqual(40, tower.Cost);
            Assert.AreEqual((2.5f, 10f, 1.5f, 10f), (tower.Range, tower.Damage, tower.ShotsPerSecond, tower.ProjectileSpeed), "combat stats unchanged");

            Wave[] waves = run.ToWaves();
            Assert.AreEqual(10, waves.Length);
            for (int i = 0; i < waves.Length; i++)
            {
                EnemyStats asset = Load<EnemyDefinition>(WaveAssets[i]).ToStats();
                Assert.AreEqual(1, waves[i].GroupCount, $"wave {i + 1}: one group");
                Assert.AreEqual(10, waves[i].SurvivalBonus, $"wave {i + 1} bonus");
                Assert.AreEqual(3 + i, waves[i].GroupAt(0).Count, $"wave {i + 1} count");
                Assert.AreEqual(1f, waves[i].GroupAt(0).SpawnIntervalSeconds, $"wave {i + 1} interval");
                Assert.AreEqual(i == 0 ? 15f : 8f, waves[i].DelayBeforeSeconds, $"wave {i + 1} delay");
                Assert.That(waves[i].DelayBeforeSeconds, Is.InRange(5f, 20f));
                Assert.AreEqual((2f, WaveHealth[i], 10), (waves[i].GroupAt(0).Enemy.Speed, waves[i].GroupAt(0).Enemy.MaxHealth, waves[i].GroupAt(0).Enemy.GoldReward), $"wave {i + 1} enemy");
                Assert.AreEqual((asset.Speed, asset.MaxHealth, asset.GoldReward), (waves[i].GroupAt(0).Enemy.Speed, waves[i].GroupAt(0).Enemy.MaxHealth, waves[i].GroupAt(0).Enemy.GoldReward), $"wave {i + 1} uses {WaveAssets[i]}");
            }
        }

        [Test]
        public void S3a_AC13_ProvisionalTarget_The_Waves_Enter_At_The_Planned_Ticks_And_The_Last_Enemy_Enters_152s_In()
        {
            Battle battle = Build(out _, out _, lives: 100_000); // enough lives for the run to stay open
            var expectedFirst = new[] { 901, 1501, 2160, 2880, 3660, 4500, 5400, 6360, 7380, 8460 };
            var firstTicks = new System.Collections.Generic.List<int>();
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


        // ---- 3b-AC9 Design target (provisional, from the balance document)

        // The reference build order of docs/balance/0001-prototype-economy.md. Tick (Tick() calls done before the action), cell, true = buy / false = sell.
        private static readonly (int tick, int x, int y, bool buy)[] ReferenceOrder =
        {
            (0, 0, 8, true), (0, 0, 8, false), (0, 11, 2, true), (0, 11, 3, true),
            (1350, 14, 2, true), (2100, 13, 2, true), (2760, 12, 0, true), (3690, 10, 1, true), (3690, 4, 7, true),
            (4350, 3, 7, true), (4620, 4, 6, true), (5190, 1, 6, true), (5400, 11, 2, false), (5400, 6, 8, true),
            (5730, 0, 6, true), (6270, 2, 5, true), (6540, 1, 3, true), (7200, 3, 4, true), (7590, 1, 2, true),
            (7890, 3, 2, true), (8550, 6, 5, true), (8790, 7, 7, true), (9180, 7, 5, true), (9810, 3, 3, true),
        };

        [Test]
        public void S3c_AC7_AC8_At_The_Start_The_Gold_Buys_Exactly_Two_Towers_And_Refuses_The_Third()
        {
            Battle battle = Build(out _, out TowerStats tower);

            Assert.AreEqual(115, battle.Gold);
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(new GridPos(2, 5), tower));
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(new GridPos(3, 5), tower));
            Assert.AreEqual(35, battle.Gold);
            Assert.AreEqual(PlacementResult.NotEnoughGold, battle.TryBuyTower(new GridPos(6, 6), tower));
            Assert.AreEqual(2, battle.Board.TowerCount);
        }

        [Test]
        public void S3c_AC8_S4a_AC6_DesignTarget_Building_Nothing_Is_Defeated_At_Tick_2931_With_The_Gold_Unspent_Plus_Two_Bonuses()
        {
            Battle battle = Build(out _, out _);
            int ticks = 0, wave1 = -1, wave2 = -1;
            while (battle.Outcome == RunOutcome.Running && ticks < 6000)
            {
                battle.Tick();
                ticks++;
                if (wave1 < 0 && battle.SurvivedWaves >= 1) { wave1 = ticks; Assert.AreEqual(125, battle.Gold, "wave 1 pays 10"); }
                if (wave2 < 0 && battle.SurvivedWaves >= 2) { wave2 = ticks; Assert.AreEqual(135, battle.Gold, "wave 2 pays 10"); }
            }

            Assert.AreEqual(RunOutcome.Lost, battle.Outcome);
            Assert.AreEqual(2931, ticks, 1, "document: lost on the 2931st Tick()");
            Assert.AreEqual(1672, wave1, 1, "document: wave 1 pays on tick 1672");
            Assert.AreEqual(2332, wave2, 1, "document: wave 2 pays on tick 2332");
            Assert.AreEqual(2, battle.SurvivedWaves, "wave 3 is still on the board on the losing tick and pays nothing");
            Assert.AreEqual(0, battle.Lives);
            Assert.AreEqual(135, battle.Gold, "115 + two bonuses");
            Assert.AreEqual(0, battle.Board.TowerCount);
        }

        [Test]
        public void S3c_AC8_S4a_AC6_DesignTarget_The_Reference_Build_Order_Wins_With_All_Lives_And_145_Gold_Left()
        {
            Battle battle = Build(out Board board, out TowerStats tower);
            int next = 0, ticks = 0, firstKillTick = -1;
            int rewardedAt = -1;
            while (battle.Outcome == RunOutcome.Running && ticks < 12000)
            {
                while (next < ReferenceOrder.Length && ReferenceOrder[next].tick == ticks)
                {
                    var a = ReferenceOrder[next++];
                    var cell = new GridPos(a.x, a.y);
                    if (a.buy)
                    {
                        Assert.GreaterOrEqual(battle.Gold, tower.Cost, $"tick {a.tick} ({a.x},{a.y}): affordable");
                        Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(cell, tower), $"tick {a.tick} buy ({a.x},{a.y})");
                    }
                    else Assert.IsTrue(battle.TrySellTower(cell), $"tick {a.tick} sell ({a.x},{a.y})");
                }

                int gold = battle.Gold;
                battle.Tick();
                ticks++;
                if (firstKillTick < 0 && battle.Gold > gold) { firstKillTick = ticks; rewardedAt = battle.Gold - gold; }
            }

            Assert.AreEqual(ReferenceOrder.Length, next, "every action ran");
            Assert.AreEqual(RunOutcome.Won, battle.Outcome);
            Assert.AreEqual(10, battle.Lives);
            Assert.AreEqual(145, battle.Gold, "45 plus the ten bonuses");
            Assert.AreEqual(10136, ticks, 1, "document: won on the 10136th Tick()");
            Assert.AreEqual(1332, firstKillTick, 1, "document: the first kill pays on tick 1332");
            Assert.AreEqual(10, rewardedAt);
            Assert.AreEqual(20, board.TowerCount, "net towers of the order");
        }
    }
}
