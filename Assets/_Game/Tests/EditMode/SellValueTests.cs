using System.Collections.Generic;
using NUnit.Framework;

namespace ElementalTD.Core.Tests
{
    /// <summary>
    /// 4c-AC1: what a sale of a tower would pay now (Battle.TryGetSellValue), asked without changing anything, and the sale paying
    /// exactly that. The names M1 to M4 are the wrong implementations of the tech-lead's list that each test cannot pass.
    /// </summary>
    public class SellValueTests
    {
        private static readonly Element Light = new Element(1);
        private static readonly Element Shadow = new Element(2);
        private static readonly GridPos Spawn = new GridPos(0, 4);
        private static readonly GridPos Exit = new GridPos(15, 4);
        private static readonly GridPos Near = new GridPos(1, 3);   // 1.41 cells from the spawn, where the enemies stand
        private static readonly GridPos Near2 = new GridPos(2, 5);
        private static readonly GridPos Far = new GridPos(14, 0);   // out of range of a range-1 tower for ever

        // Never starts a wave: enemies come through Board.SpawnEnemy.
        private static readonly Wave[] Idle = { new Wave(new EnemyStats(1f, 10f, 5), 1, 1f, 1_000_000f) };

        private static Board OpenBoard() => new Board(new GridMap(16, 9, new List<GridPos>()), Spawn, Exit);

        private static ElementChart LightShadow()
        {
            var chart = new ElementChart(2);
            chart.Set(Light, Shadow, 2f);
            chart.Set(Shadow, Light, 2f);
            return chart;
        }

        // Speed 10 cells a second: a shot is in flight for many ticks, so ProjectileCount rises on the launch tick.
        private static TowerStats Flying(int cost, Element element = default, float range = 20f) => new TowerStats(range, 10f, 2f, 10f, cost, element);

        private static EnemyStats Still(Element element = default, float health = 1_000_000f) => new EnemyStats(0f, health, 0, element);

        private static Battle Parked(int gold, int percent, ElementChart chart = null) => new Battle(OpenBoard(), Idle, 10, gold, percent, chart);

        private static void Buy(Battle battle, GridPos cell, in TowerStats stats) =>
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(cell, stats));

        private static int Value(Battle battle, GridPos cell)
        {
            Assert.IsTrue(battle.TryGetSellValue(cell, out int gold), $"a tower on {cell}");
            return gold;
        }

        /// <summary>A battle with one tower of the cost on Near that has launched its first shot.</summary>
        private static Battle Fired(int cost, int percent, out int ticks)
        {
            Battle battle = Parked(cost * 2, percent);
            Buy(battle, Near, Flying(cost));
            battle.Board.SpawnEnemy(Still());
            ticks = 0;
            while (battle.ProjectileCount == 0 && ticks < 100)
            {
                battle.Tick();
                ticks++;
            }
            Assert.Greater(battle.ProjectileCount, 0, "the tower launched");
            return battle;
        }

        // ---- 4c-AC1 the value

        [TestCase(40, 0, 0)]
        [TestCase(40, 50, 20)]
        [TestCase(40, 100, 40)]
        [TestCase(45, 50, 22)]    // M3: rounding to nearest gives 23 (22.5)
        [TestCase(17, 70, 11)]    // M3: rounding to nearest gives 12 (11.9)
        [TestCase(1, 50, 0)]
        [TestCase(1, 99, 0)]
        [TestCase(99, 99, 98)]
        [TestCase(0, 50, 0)]
        [TestCase(1_000_000, 100, 1_000_000)]
        [TestCase(21_474_836, 100, 21_474_836)]
        public void S4c_AC1_M3_The_Value_Is_The_Cost_Before_The_First_Launch_And_Cost_Times_Percent_Over_100_Rounded_Down_After_It(int cost, int percent, int refund)
        {
            Battle idle = Parked(cost, percent);
            Buy(idle, Near, Flying(cost));
            Assert.AreEqual(cost, Value(idle, Near), "never launched: the full cost, whatever the percent");

            Battle fired = Fired(cost, percent, out _);
            Assert.AreEqual(refund, Value(fired, Near), $"{cost} at {percent} percent after the first launch");
            Assert.AreEqual(cost * (long)percent / 100, Value(fired, Near), "the same as the exact integer division");
        }

        [Test]
        public void S4c_AC1_The_Value_Falls_To_The_Refund_On_The_Tick_Of_The_First_Launch_And_Stays_There()
        {
            Battle battle = Parked(40, 50);
            Buy(battle, Near, Flying(40));
            battle.Board.SpawnEnemy(Still());
            Assert.AreEqual(40, Value(battle, Near), "before any tick");
            battle.Tick();
            Assert.AreEqual(1, battle.ProjectileCount, "the tower launched on the first tick");
            for (int i = 0; i < 300; i++)
            {
                Assert.AreEqual(20, Value(battle, Near), $"tick {i + 1}: the refund holds while shots fly, land and the tower reloads");
                battle.Tick();
            }
        }

        [Test]
        public void S4c_AC1_A_Tower_That_Never_Launches_Keeps_The_Full_Value_For_Ever()
        {
            Battle battle = Parked(40, 50);
            Buy(battle, Far, Flying(40, range: 1f));
            battle.Board.SpawnEnemy(Still());
            for (int i = 0; i < 600; i++)
            {
                battle.Tick();
                Assert.AreEqual(40, Value(battle, Far), $"tick {i + 1}");
            }
            Assert.AreEqual(0, battle.ProjectileCount);
        }

        // ---- 4c-AC1 not a tower

        [Test]
        public void S4c_AC1_It_Answers_False_And_0_Off_The_Grid_On_Every_Side()
        {
            Battle battle = Parked(100, 50);
            Buy(battle, Near, Flying(40));
            int gold = 12345;
            foreach (var cell in new[]
            {
                new GridPos(-1, 4), new GridPos(16, 4), new GridPos(4, -1), new GridPos(4, 9), new GridPos(-1, -1), new GridPos(16, 9),
                new GridPos(99, 99), new GridPos(int.MinValue, 0), new GridPos(0, int.MaxValue), new GridPos(-1, 3), new GridPos(2, 9),
            })
            {
                gold = 12345;
                Assert.IsFalse(battle.TryGetSellValue(cell, out gold), $"{cell}");
                Assert.AreEqual(0, gold, $"{cell}");
            }
        }

        [Test]
        public void S4c_AC1_It_Answers_False_And_0_On_A_Wall_A_Free_Cell_The_Spawn_The_Exit_And_A_Just_Sold_Cell()
        {
            var board = new Board(new GridMap(16, 9, TestMaps.PrototypeBlocked()), TestMaps.Spawn, TestMaps.Exit);
            var battle = new Battle(board, Idle, 10, 100, 50);
            Buy(battle, Near, Flying(40));
            Assert.IsTrue(battle.TrySellTower(Near));

            foreach (var (cell, what) in new[]
            {
                (new GridPos(5, 0), "a wall"), (new GridPos(3, 3), "a free cell"), (TestMaps.Spawn, "the spawn"),
                (TestMaps.Exit, "the exit"), (Near, "a just-sold cell"),
            })
            {
                int gold = 999;
                Assert.IsFalse(battle.TryGetSellValue(cell, out gold), what);
                Assert.AreEqual(0, gold, what);
            }
        }

        [Test]
        public void S4c_AC1_A_Cell_Sold_And_Bought_Again_Answers_For_The_New_Tower_With_Its_Own_Launch_State()
        {
            Battle battle = Fired(40, 50, out _);
            Assert.AreEqual(20, Value(battle, Near));
            Assert.IsTrue(battle.TrySellTower(Near));
            Assert.IsFalse(battle.TryGetSellValue(Near, out _));
            battle.Board.RemoveEnemy(battle.Board.EnemyAt(0)); // nothing in range: the new tower stays unfired
            Buy(battle, Near, Flying(40));
            Assert.AreEqual(40, Value(battle, Near), "the new tower has not launched");
        }

        // ---- 4c-AC1 asking changes nothing; the sale pays the answer

        [Test]
        public void S4c_AC1_M2_Asking_100_Times_Before_The_First_Launch_Still_Answers_The_Cost_And_Changes_Nothing()
        {
            Battle battle = Parked(100, 50);
            Buy(battle, Near, Flying(40));
            battle.Board.SpawnEnemy(Still());
            int gold = battle.Gold, towers = battle.Board.TowerCount;
            for (int i = 0; i < 100; i++) Assert.AreEqual(40, Value(battle, Near), $"ask {i + 1}");
            Assert.AreEqual((gold, towers), (battle.Gold, battle.Board.TowerCount), "neither the gold nor the board changed");
            Assert.AreEqual(0, battle.ProjectileCount);
            battle.Tick();
            Assert.AreEqual(20, Value(battle, Near), "and the first launch still lowers it");
        }

        [Test]
        public void S4c_AC1_M2_A_Battle_Asked_On_Every_Tick_Equals_One_Never_Asked_After_Every_Tick()
        {
            Battle asked = ReplayBattle(50), plain = ReplayBattle(50);
            var cells = new[] { Near, Far, Near2 };
            for (int tick = 1; tick <= 150; tick++)
            {
                if (tick == 30) { Buy(asked, Near2, Flying(45)); Buy(plain, Near2, Flying(45)); }
                asked.Tick();
                plain.Tick();
                foreach (GridPos cell in cells) asked.TryGetSellValue(cell, out _);
                Assert.AreEqual(plain.Gold, asked.Gold, $"tick {tick}: gold");
                Assert.AreEqual(plain.ProjectileCount, asked.ProjectileCount, $"tick {tick}: shots in flight");
                Assert.AreEqual(plain.Board.EnemyAt(0).Health, asked.Board.EnemyAt(0).Health, $"tick {tick}: the enemy's health");
            }
            foreach (GridPos cell in cells) Assert.AreEqual(Value(plain, cell), Value(asked, cell), $"{cell}: the value at the end");
            Assert.AreEqual(40, Value(asked, Far), "the tower that never launched still answers its cost");
        }

        /// <summary>Two towers on the board (one launching at once, one never) and an enemy that stands for ever; a third is bought later by the caller.</summary>
        private static Battle ReplayBattle(int percent)
        {
            Battle battle = Parked(1000, percent);
            Buy(battle, Near, Flying(17));
            Buy(battle, Far, Flying(40, range: 1f));
            battle.Board.SpawnEnemy(Still());
            return battle;
        }

        [Test]
        public void S4c_AC1_M1_On_Every_Tick_Of_A_Run_The_Gold_Rises_By_Exactly_What_The_Query_Answered([Values(50, 70, 100, 0)] int percent)
        {
            var cells = new[] { Near, Far, Near2 };
            var costs = new Dictionary<GridPos, int> { { Near, 17 }, { Far, 40 }, { Near2, 45 } };
            int launchesSeen = 0;
            for (int tick = 0; tick <= 120; tick++)
            {
                // The run is deterministic: replay it to the tick, so every tick is checked on a battle that has not been sold from.
                Battle battle = ReplayBattle(percent);
                for (int i = 1; i <= tick; i++)
                {
                    if (i == 30) Buy(battle, Near2, Flying(45));
                    battle.Tick();
                }

                foreach (GridPos cell in cells)
                {
                    bool has = battle.TryGetSellValue(cell, out int answer);
                    int before = battle.Gold;
                    bool sold = battle.TrySellTower(cell);
                    Assert.AreEqual(has, sold, $"tick {tick} {cell}: the query and the sale agree on whether there is a tower");
                    Assert.AreEqual(before + answer, battle.Gold, $"tick {tick} {cell}: the sale pays the query's answer");
                    if (has && answer < costs[cell]) launchesSeen++;
                    Assert.IsFalse(battle.TryGetSellValue(cell, out int after), $"tick {tick} {cell}: nothing left to sell");
                    Assert.AreEqual(0, after);
                }
            }
            if (percent < 100) Assert.Greater(launchesSeen, 100, "the run had launches: sales were checked at the refund too");
            else Assert.AreEqual(0, launchesSeen, "at 100 percent the refund equals the cost");
        }

        // ---- 4c-AC1 the launch, not the landing (M4), also at an immune enemy

        [TestCase(false)]
        [TestCase(true)]
        public void S4c_AC1_M4_The_Value_Has_Fallen_On_The_Tick_ProjectileCount_First_Rises_Also_When_The_Target_Is_Immune(bool immune)
        {
            Battle battle = Parked(100, 50, LightShadow());
            Buy(battle, Near, Flying(40, Light));
            Buy(battle, Near2, Flying(40, Light, range: 1f)); // never launches: a control that must not fall
            Enemy enemy = battle.Board.SpawnEnemy(Still(immune ? Light : Shadow, 100f));

            for (int tick = 1; tick <= 40; tick++)
            {
                int shots = battle.ProjectileCount;
                battle.Tick();
                if (shots == 0 && battle.ProjectileCount > 0)
                {
                    Assert.AreEqual(20, Value(battle, Near), $"tick {tick}: ProjectileCount rose and the value already fell");
                    Assert.AreEqual(100f, enemy.Health, "and no shot has landed yet: a landing is not the trigger");
                    Assert.AreEqual(40, Value(battle, Near2), "the control stays");
                    return;
                }
                Assert.AreEqual(40, Value(battle, Near), $"tick {tick}: no launch yet");
            }
            Assert.Fail("the tower never launched");
        }

        // ---- 4c-AC1 the run has ended

        [Test]
        public void S4c_AC1_The_Query_Still_Answers_After_A_Defeat_And_After_A_Win()
        {
            // Defeat: one life and an enemy that crosses the board at once.
            var lostWaves = new[] { new Wave(new EnemyStats(60f, 10f, 0), 1, 1f, 0f) };
            var lost = new Battle(OpenBoard(), lostWaves, 1, 100, 50);
            Buy(lost, Far, Flying(40, range: 1f));
            Buy(lost, Near, Flying(30, range: 0f));
            for (int i = 0; i < 2000 && lost.Outcome == RunOutcome.Running; i++) lost.Tick();
            Assert.AreEqual(RunOutcome.Lost, lost.Outcome);
            Assert.AreEqual(40, Value(lost, Far));
            Assert.AreEqual(30, Value(lost, Near));

            // Win: a tower next to the spawn kills the only enemy at once.
            var wonWaves = new[] { new Wave(new EnemyStats(0f, 10f, 3), 1, 1f, 0f) };
            var won = new Battle(OpenBoard(), wonWaves, 10, 100, 50);
            Buy(won, Near, new TowerStats(20f, 10f, 0.1f, 100f, 40));
            for (int i = 0; i < 2000 && won.Outcome == RunOutcome.Running; i++) won.Tick();
            Assert.AreEqual(RunOutcome.Won, won.Outcome);
            Assert.AreEqual(20, Value(won, Near), "it fired: the refund");
        }

        // ---- 4c-AC1 allocation

        [Test]
        public void S4c_AC1_The_Query_Allocates_Nothing()
        {
            Battle idle = Parked(100, 50);
            Buy(idle, Near, Flying(40));
            Battle fired = Fired(40, 50, out _);
            foreach (var (battle, cell, what) in new[]
            {
                (idle, Near, "a tower that never fired"), (fired, Near, "a tower that fired"), (fired, Far, "a free cell"), (fired, new GridPos(99, 99), "off the grid"),
            })
            {
                Battle b = battle;
                GridPos c = cell;
                int sum = 0;
                System.Action ask = () => { b.TryGetSellValue(c, out int gold); sum += gold; };
                ask(); // warm-up
                Assert.AreEqual(0, TickAllocation.AllocationsOf(ask), what);
            }
        }
    }
}
