using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ElementalTD.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace ElementalTD.Core.Tests
{
    /// <summary>Gold, buying and selling in the Battle (3b-AC1, AC2, AC4, AC5, AC10, AC11, and the Core side of AC6).</summary>
    public class GoldTests
    {
        private static readonly GridPos Spawn = new GridPos(0, 4);
        private static readonly GridPos Near = new GridPos(1, 3);   // 1.41 cells from the spawn
        private static readonly GridPos Near2 = new GridPos(1, 5);

        // A shot crosses 1.67 cells per tick, so a tower next to the spawn kills on the tick it launches.
        private static TowerStats FastTower(int cost, float damage = 10f) => new TowerStats(20f, damage, 0.1f, 100f, cost);
        // One cell per second: the shot is still in flight 40 ticks after launch.
        private static TowerStats SlowTower(int cost) => new TowerStats(20f, 10f, 0.1f, 1f, cost);
        private static EnemyStats Still(int reward, float health = 10f) => new EnemyStats(0f, health, reward);
        private static readonly EnemyStats Runner = new EnemyStats(60f, 10f, 9);

        private static Board OpenBoard() =>
            new Board(new GridMap(16, 9, new List<GridPos>()), Spawn, new GridPos(15, 4));

        private static Board PrototypeBoard() =>
            new Board(new GridMap(16, 9, TestMaps.PrototypeBlocked()), TestMaps.Spawn, TestMaps.Exit);

        private static readonly Wave[] Idle = { new Wave(new EnemyStats(1f, 10f, 5), 1, 1f, 1_000_000f) };

        private static Battle IdleBattle(Board board, int gold, int percent = 100) => new Battle(board, Idle, 10, gold, percent);

        private static Battle OneEnemy(Board board, int reward, int gold, int percent = 100, float health = 10f) =>
            new Battle(board, new[] { new Wave(Still(reward, health), 1, 1f, 0f) }, 10, gold, percent);

        private static void Ticks(Battle battle, int count)
        {
            for (int i = 0; i < count; i++) battle.Tick();
        }

        private static int[] Snapshot(Board board)
        {
            var list = new List<int>();
            for (int y = 0; y < board.Map.Height; y++)
                for (int x = 0; x < board.Map.Width; x++)
                {
                    var p = new GridPos(x, y);
                    list.Add(board.Field.Distance(p));
                    list.Add(board.Map.IsBlocked(p) ? 1 : 0);
                }
            list.Add(board.TowerCount);
            list.Add(board.EnemyCount);
            return list.ToArray();
        }

        // ---- 3b-AC1 Starting gold

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(40)]
        [TestCase(int.MaxValue)]
        public void S3b_AC1_The_Battle_Starts_With_Its_Starting_Gold(int gold)
        {
            Assert.AreEqual(gold, IdleBattle(OpenBoard(), gold).Gold);
        }

        [Test]
        public void S3b_AC1_The_Optional_Parameters_Default_To_No_Gold_And_A_Full_Refund()
        {
            var battle = new Battle(OpenBoard(), Idle, 10);
            Assert.AreEqual(0, battle.Gold);
            Assert.AreEqual(PlacementResult.NotEnoughGold, battle.TryBuyTower(Near, FastTower(1)));
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(Near, FastTower(0)), "a free tower needs no gold");
            Assert.IsTrue(battle.TrySellTower(Near));
            Assert.AreEqual(0, battle.Gold);
        }

        [Test]
        public void S3b_AC1_Invalid_Constructor_Values_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Battle(OpenBoard(), Idle, 10, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Battle(OpenBoard(), Idle, 10, 5, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Battle(OpenBoard(), Idle, 10, 5, 101));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TowerStats(1f, 1f, 1f, 1f, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyStats(1f, 1f, -1));
            Assert.DoesNotThrow(() => new Battle(OpenBoard(), Idle, 10, 0, 0));
            Assert.DoesNotThrow(() => new Battle(OpenBoard(), Idle, 10, 0, 100));
            Assert.DoesNotThrow(() => new TowerStats(1f, 1f, 1f, 1f, 0));
            Assert.DoesNotThrow(() => new EnemyStats(1f, 1f, 0));
        }

        [Test]
        public void S3b_AC1_Gold_Never_Goes_Below_0_Whatever_Is_Refused_Or_Bought()
        {
            var battle = IdleBattle(OpenBoard(), 6);
            int lowest = battle.Gold;
            for (int i = 0; i < 5; i++)
            {
                battle.TryBuyTower(new GridPos(2 + i, 2), FastTower(4));
                lowest = Math.Min(lowest, battle.Gold);
                battle.TryBuyTower(new GridPos(2 + i, 2), FastTower(4)); // same cell: refused
                lowest = Math.Min(lowest, battle.Gold);
            }
            Assert.GreaterOrEqual(lowest, 0);
            Assert.AreEqual(2, battle.Gold, "6 buys one tower of 4, then 2 is too little for the rest");
            Assert.AreEqual(1, battle.Board.TowerCount);
        }

        // ---- 3b-AC2 Buying

        [Test]
        public void S3b_AC2_Gold_Equal_To_The_Cost_Buys_The_Tower_And_Leaves_Nothing()
        {
            Board board = OpenBoard();
            var battle = IdleBattle(board, 5);
            int placed = 0;
            board.TowerPlaced += _ => placed++;

            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(Near, FastTower(5)));

            Assert.AreEqual(0, battle.Gold);
            Assert.AreEqual(1, board.TowerCount);
            Assert.AreEqual(5, board.TowerOn(Near).Stats.Cost);
            Assert.AreEqual(1, placed);
        }

        [Test]
        public void S3b_AC2_One_Gold_Short_Is_Refused_And_Changes_Nothing()
        {
            Board board = PrototypeBoard();
            var battle = IdleBattle(board, 4);
            board.SpawnEnemy(Still(1));
            int events = 0;
            board.TowerPlaced += _ => events++;
            board.TowerRemoved += _ => events++;
            int[] before = Snapshot(board);

            Assert.AreEqual(PlacementResult.NotEnoughGold, battle.TryBuyTower(new GridPos(8, 2), FastTower(5)));

            Assert.AreEqual(4, battle.Gold);
            Assert.AreEqual(0, board.TowerCount);
            Assert.AreEqual(0, events);
            CollectionAssert.AreEqual(before, Snapshot(board));
        }

        [TestCase("outOfBounds")]
        [TestCase("spawn")]
        [TestCase("exit")]
        [TestCase("wall")]
        [TestCase("tower")]
        [TestCase("enemy")]
        [TestCase("blocksPath")]
        public void S3b_AC2_NotEnoughGold_Beats_Every_Board_Refusal_And_With_Gold_The_Board_Decides(string which)
        {
            var map = new[] { "outOfBounds", "spawn", "exit", "wall", "tower", "enemy", "blocksPath" };
            PlacementResult expected = which switch
            {
                "outOfBounds" => PlacementResult.OutOfBounds,
                "spawn" => PlacementResult.SpawnOrExit,
                "exit" => PlacementResult.SpawnOrExit,
                "wall" => PlacementResult.CellBlocked,
                "tower" => PlacementResult.CellBlocked,
                "enemy" => PlacementResult.EnemyInTheWay,
                _ => PlacementResult.WouldBlockPath
            };
            Assert.Contains(which, map);

            foreach (bool enoughGold in new[] { false, true })
            {
                Board board = PrototypeBoard();
                var battle = IdleBattle(board, 100);
                TowerStats stats = FastTower(5);
                GridPos cell;
                switch (which)
                {
                    case "outOfBounds": cell = new GridPos(16, 0); break;
                    case "spawn": cell = TestMaps.Spawn; break;
                    case "exit": cell = TestMaps.Exit; break;
                    case "wall": cell = new GridPos(5, 0); break;
                    case "tower":
                        cell = new GridPos(3, 2);
                        Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(cell, stats));
                        break;
                    case "enemy":
                        board.SpawnEnemy(Still(1));
                        cell = board.EnemyAt(0).Follower.To;
                        Assert.AreNotEqual(TestMaps.Spawn, cell);
                        break;
                    default:
                        Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(new GridPos(5, 7), stats));
                        cell = new GridPos(5, 8);
                        break;
                }

                // Spend down so the same battle can be short of gold: a fresh battle with the wanted gold.
                battle = IdleBattle(board, enoughGold ? 100 : 4);
                int[] before = Snapshot(board);

                PlacementResult result = battle.TryBuyTower(cell, stats);

                Assert.AreEqual(enoughGold ? expected : PlacementResult.NotEnoughGold, result, $"{which}, enoughGold={enoughGold}");
                Assert.AreEqual(enoughGold ? 100 : 4, battle.Gold, "a refused purchase costs nothing");
                CollectionAssert.AreEqual(before, Snapshot(board));
            }
        }

        [Test]
        public void S3b_AC2_Only_A_Placed_Tower_Is_Paid_For_And_Each_Purchase_Costs_Its_Own_Price()
        {
            Board board = OpenBoard();
            var battle = IdleBattle(board, 20);

            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(Near, FastTower(8)));
            Assert.AreEqual(12, battle.Gold);
            Assert.AreEqual(PlacementResult.CellBlocked, battle.TryBuyTower(Near, FastTower(8)));
            Assert.AreEqual(12, battle.Gold);
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(Near2, FastTower(3)));
            Assert.AreEqual(9, battle.Gold);
            Assert.AreEqual(PlacementResult.SpawnOrExit, battle.TryBuyTower(Spawn, FastTower(1)));
            Assert.AreEqual(9, battle.Gold);
        }

        // ---- 3b-AC4 Kills pay

        [Test]
        public void S3b_AC4_A_Kill_Pays_Its_Reward_Once_On_The_Tick_The_Health_Reaches_0()
        {
            Board board = OpenBoard();
            var battle = OneEnemy(board, 7, 10);
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(Near, FastTower(4)));
            Assert.AreEqual(6, battle.Gold);

            battle.Tick(); // enters, launches and lands in this tick

            Assert.AreEqual(0, board.EnemyCount);
            Assert.AreEqual(13, battle.Gold, "6 + 7");
            Ticks(battle, 50);
            Assert.AreEqual(13, battle.Gold, "paid once");
        }

        [Test]
        public void S3b_AC4_A_Shot_That_Does_Not_Kill_Pays_Nothing()
        {
            Board board = OpenBoard();
            var battle = OneEnemy(board, 7, 10, health: 10f);
            battle.TryBuyTower(Near, FastTower(0, damage: 4f));

            battle.Tick();

            Assert.AreEqual(1, board.EnemyCount);
            Assert.AreEqual(6f, board.EnemyAt(0).Health, 1e-4f);
            Assert.AreEqual(10, battle.Gold);
        }

        [Test]
        public void S3b_AC4_Overkill_And_Health_Exactly_0_Both_Pay()
        {
            foreach (float damage in new[] { 10f, 1000f })
            {
                Board board = OpenBoard();
                var battle = OneEnemy(board, 3, 0, health: 10f);
                battle.TryBuyTower(Near, FastTower(0, damage));
                battle.Tick();
                Assert.AreEqual(3, battle.Gold, $"damage {damage}");
            }
        }

        [TestCase(10f)]  // each of the two shots kills alone
        [TestCase(6f)]   // only the second shot kills
        public void S3b_AC4_Two_Shots_Landing_On_One_Enemy_In_One_Tick_Pay_Once(float damage)
        {
            Board board = OpenBoard();
            var battle = OneEnemy(board, 7, 0, health: 10f);
            battle.TryBuyTower(Near, FastTower(0, damage));
            battle.TryBuyTower(Near2, FastTower(0, damage));

            battle.Tick();

            Assert.AreEqual(0, board.EnemyCount);
            Assert.AreEqual(7, battle.Gold);
            Ticks(battle, 30);
            Assert.AreEqual(7, battle.Gold);
        }

        [Test]
        public void S3b_AC4_Waves_Of_Different_Definitions_Pay_Their_Own_Reward_Also_When_The_Object_Is_Reused()
        {
            Board board = OpenBoard();
            var waves = new[]
            {
                new Wave(Still(3), 1, 1f, 0f),
                new Wave(Still(11), 1, 1f, 1f),
                new Wave(Still(1), 1, 1f, 1f),
            };
            var battle = new Battle(board, waves, 10, 0, 100);
            battle.TryBuyTower(Near, new TowerStats(20f, 10f, 5f, 100f, 0)); // fires again within 0.2 s

            var increments = new List<int>();
            int last = 0;
            Enemy firstObject = null;
            for (int i = 0; i < 600 && battle.Outcome == RunOutcome.Running; i++)
            {
                battle.Tick();
                if (firstObject == null && board.EnemyCount > 0) firstObject = board.EnemyAt(0);
                if (battle.Gold != last) increments.Add(battle.Gold - last);
                last = battle.Gold;
            }

            CollectionAssert.AreEqual(new[] { 3, 11, 1 }, increments);
            Assert.AreEqual(RunOutcome.Won, battle.Outcome);
            Assert.AreEqual(15, battle.Gold);
        }

        [Test]
        public void S3b_AC4_A_Reused_Enemy_Object_With_A_Different_Definition_Carries_The_New_Reward()
        {
            Board board = OpenBoard();
            Enemy first = board.SpawnEnemy(Still(3));
            Assert.AreEqual(3, first.GoldReward);
            board.RemoveEnemy(first);
            Enemy second = board.SpawnEnemy(Still(11));
            Assert.AreSame(first, second, "the object is reused");
            Assert.AreEqual(11, second.GoldReward);
        }

        [Test]
        public void S3b_AC4_An_Enemy_That_Reaches_The_Exit_Pays_Nothing()
        {
            Board board = OpenBoard();
            var battle = new Battle(board, new[] { new Wave(Runner, 3, 1f, 0f) }, 10, 4, 100);

            Ticks(battle, 300);

            Assert.AreEqual(RunOutcome.Won, battle.Outcome);
            Assert.AreEqual(7, battle.Lives);
            Assert.AreEqual(4, battle.Gold);
        }

        [Test]
        public void S3b_AC4_A_Shot_Of_A_Tower_Sold_Meanwhile_Still_Pays_Its_Kill()
        {
            Board board = OpenBoard();
            var battle = OneEnemy(board, 7, 10, 50);
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(Near, SlowTower(4)));
            battle.Tick(); // launched, in flight
            Assert.AreEqual(1, battle.ProjectileCount);
            Assert.AreEqual(1, board.EnemyCount);

            Assert.IsTrue(battle.TrySellTower(Near));
            Assert.AreEqual(6 + 2, battle.Gold, "it had fired: 4 * 50 / 100");
            Assert.AreEqual(0, board.TowerCount);

            Ticks(battle, 200);

            Assert.AreEqual(0, board.EnemyCount);
            Assert.AreEqual(8 + 7, battle.Gold);
        }

        // ---- 3b-AC5 Selling

        [Test]
        public void S3b_AC5_A_Tower_That_Never_Launched_Refunds_Its_Full_Cost_Even_After_Minutes()
        {
            Board board = OpenBoard();
            var battle = IdleBattle(board, 15, 70);
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(Near, FastTower(15)));
            Assert.AreEqual(0, battle.Gold);

            Ticks(battle, 6000); // 100 s with nothing in range
            Assert.IsTrue(battle.TrySellTower(Near));

            Assert.AreEqual(15, battle.Gold);
            Assert.IsFalse(board.TowerOn(Near) != null);
        }

        [Test]
        public void S3b_AC5_Sold_Before_The_Tick_It_Would_Launch_Refunds_Everything()
        {
            Board board = OpenBoard();
            var battle = OneEnemy(board, 7, 10, 50);
            battle.TryBuyTower(Near, FastTower(10));
            Assert.AreEqual(0, battle.Gold);

            battle.TrySellTower(Near);
            battle.Tick(); // an enemy enters; no tower is left to fire

            Assert.AreEqual(10, battle.Gold);
            Assert.AreEqual(1, board.EnemyCount);
        }

        [TestCase(15, 70, 10)]
        [TestCase(5, 50, 2)]
        [TestCase(15, 0, 0)]
        [TestCase(15, 100, 15)]
        [TestCase(7, 33, 2)]
        [TestCase(1, 99, 0)]
        [TestCase(0, 50, 0)]
        [TestCase(1000000, 99, 990000)]
        [TestCase(17, 70, 11)] // 11.9: nearest would give 12
        [TestCase(9, 75, 6)]   // 6.75: nearest would give 7
        public void S3b_AC5_One_Tick_After_Its_First_Launch_With_The_Shot_In_Flight_It_Refunds_The_Percent_Rounded_Down(int cost, int percent, int expected)
        {
            Board board = OpenBoard();
            var battle = OneEnemy(board, 7, cost, percent);
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(Near, SlowTower(cost)));
            battle.Tick();
            Assert.AreEqual(1, battle.ProjectileCount, "launched, still flying");

            Assert.IsTrue(battle.TrySellTower(Near));

            Assert.AreEqual(expected, battle.Gold);
        }

        [TestCase(15, 70, 10)]
        [TestCase(5, 50, 2)]
        [TestCase(17, 70, 11)]
        [TestCase(9, 75, 6)]
        [TestCase(9, 100, 9)]
        [TestCase(9, 0, 0)]
        public void S3b_AC5_A_Sale_On_The_Launch_Tick_Itself_Which_Also_Kills_Refunds_The_Percent(int cost, int percent, int expected)
        {
            Board board = OpenBoard();
            var battle = OneEnemy(board, 7, cost, percent);
            battle.TryBuyTower(Near, FastTower(cost));

            battle.Tick(); // launches and lands in the same tick
            Assert.AreEqual(7, battle.Gold);
            battle.TrySellTower(Near);

            Assert.AreEqual(7 + expected, battle.Gold);
        }

        [Test]
        public void S3b_AC5_Two_Towers_Bought_At_Different_Costs_Each_Refund_Their_Own()
        {
            Board board = OpenBoard();
            var battle = new Battle(board, new[] { new Wave(Still(0), 1, 1f, 0f) }, 10, 40, 50);
            // A fires at the enemy; B is out of range of everything and never launches.
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(Near, SlowTower(4)));
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(new GridPos(10, 8), new TowerStats(0.5f, 10f, 0.1f, 1f, 9)));
            Assert.AreEqual(27, battle.Gold);
            battle.Tick();

            battle.TrySellTower(Near);
            Assert.AreEqual(27 + 2, battle.Gold, "4 * 50 / 100");
            battle.TrySellTower(new GridPos(10, 8));
            Assert.AreEqual(27 + 2 + 9, battle.Gold, "never fired: full cost");
        }

        [Test]
        public void S3b_AC5_The_Refund_Is_The_Cost_The_Tower_Was_Bought_With()
        {
            Board board = OpenBoard();
            var battle = IdleBattle(board, 30, 100);
            battle.TryBuyTower(Near, FastTower(12));
            battle.TryBuyTower(Near, FastTower(1)); // refused: the cell is taken; the 12 tower stays
            battle.TrySellTower(Near);
            battle.TryBuyTower(Near, FastTower(3));
            battle.TrySellTower(Near);

            Assert.AreEqual(30, battle.Gold);
        }

        [Test]
        public void S3b_AC5_Selling_Where_There_Is_No_Tower_Returns_False_And_Changes_Nothing()
        {
            Board board = PrototypeBoard();
            var battle = IdleBattle(board, 9, 50);
            battle.TryBuyTower(new GridPos(3, 2), FastTower(1));
            int events = 0;
            board.TowerRemoved += _ => events++;
            int[] before = Snapshot(board);

            foreach (GridPos cell in new[] { new GridPos(4, 4), new GridPos(5, 0), TestMaps.Spawn, TestMaps.Exit, new GridPos(-1, 0), new GridPos(16, 9), new GridPos(99, 99) })
                Assert.IsFalse(battle.TrySellTower(cell), cell.ToString());

            Assert.AreEqual(8, battle.Gold);
            Assert.AreEqual(0, events);
            CollectionAssert.AreEqual(before, Snapshot(board));
            Assert.IsTrue(board.Map.IsBlocked(new GridPos(5, 0)), "the wall stays");
        }

        [Test]
        public void S3b_AC5_A_Sale_Frees_The_Cell_Restores_The_Field_Raises_TowerRemoved_Once_And_Cannot_Repeat()
        {
            Board board = PrototypeBoard();
            var battle = IdleBattle(board, 10, 50);
            int[] empty = Snapshot(board);
            battle.TryBuyTower(new GridPos(5, 7), FastTower(4));
            int removed = 0;
            GridPos removedCell = default;
            board.TowerRemoved += c => { removed++; removedCell = c; };

            Assert.IsTrue(battle.TrySellTower(new GridPos(5, 7)));

            Assert.AreEqual(1, removed);
            Assert.AreEqual(new GridPos(5, 7), removedCell);
            Assert.IsFalse(board.HasTower(new GridPos(5, 7)));
            Assert.IsFalse(board.Map.IsBlocked(new GridPos(5, 7)));
            CollectionAssert.AreEqual(empty, Snapshot(board));
            Assert.AreEqual(10, battle.Gold);
            Assert.IsFalse(battle.TrySellTower(new GridPos(5, 7)), "already sold");
            Assert.AreEqual(10, battle.Gold);
            Assert.AreEqual(1, removed);
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(new GridPos(5, 7), FastTower(4)), "the cell can be bought again");
            Assert.AreEqual(6, battle.Gold);
        }


        [Test]
        public void S3b_AC5_Buy_Fire_Sell_Rebuy_The_Same_Cell_And_Sell_At_Once_Refunds_In_Full()
        {
            Board board = OpenBoard();
            var battle = OneEnemy(board, 0, 20, 50);
            battle.TryBuyTower(Near, SlowTower(10));
            battle.Tick(); // fires
            battle.TrySellTower(Near);
            Assert.AreEqual(10 + 5, battle.Gold);

            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(Near, SlowTower(10)));
            Assert.AreEqual(5, battle.Gold);
            battle.TrySellTower(Near);

            Assert.AreEqual(15, battle.Gold, "the rebought tower never launched");
        }

        [Test]
        public void S3b_AC4_Gold_Is_Unchanged_After_Each_Non_Lethal_Hit_Then_Rises_Once_On_The_Kill()
        {
            Board board = OpenBoard();
            var battle = new Battle(board, new[] { new Wave(Still(8, 30f), 1, 1f, 0f) }, 10, 0, 100);
            battle.TryBuyTower(Near, new TowerStats(20f, 10f, 5f, 100f, 0)); // a hit every 0.2 s

            var golds = new List<int>();
            var healths = new List<float>();
            for (int i = 0; i < 60; i++)
            {
                battle.Tick();
                if (board.EnemyCount > 0)
                {
                    float h = board.EnemyAt(0).Health;
                    if (healths.Count == 0 || healths[healths.Count - 1] != h) { healths.Add(h); golds.Add(battle.Gold); }
                }
                else { golds.Add(battle.Gold); break; }
            }

            CollectionAssert.AreEqual(new[] { 20f, 10f }, healths);
            CollectionAssert.AreEqual(new[] { 0, 0, 8 }, golds, "no gold after the first and second hit, 8 on the kill");
            Ticks(battle, 100);
            Assert.AreEqual(8, battle.Gold);
        }

        [Test]
        public void S3b_AC4_A_Later_Shot_At_The_Dead_Enemy_Pays_Nothing()
        {
            Board board = OpenBoard();
            var battle = OneEnemy(board, 7, 0, health: 10f);
            battle.TryBuyTower(Near, SlowTower(0));    // its shot is still flying when the enemy dies
            battle.TryBuyTower(Near2, FastTower(0));   // kills at once
            battle.Tick();
            Assert.AreEqual(0, board.EnemyCount);
            Assert.AreEqual(7, battle.Gold);

            Ticks(battle, 200);

            Assert.AreEqual(7, battle.Gold, "the slow shot found its target gone");
            Assert.AreEqual(0, battle.ProjectileCount);
        }

        [Test]
        public void S3b_AC4_An_Exit_Costs_A_Life_And_Leaves_The_Gold_Unchanged()
        {
            var battle = new Battle(OpenBoard(), new[] { new Wave(Runner, 1, 1f, 0f) }, 5, 12, 100);
            Ticks(battle, 40);
            Assert.AreEqual(4, battle.Lives);
            Assert.AreEqual(12, battle.Gold);
        }

        // ---- 3b-AC8 (the part that needs no asset): the definitions hand the numbers on

        [Test]
        public void S3b_AC8_Definitions_Built_In_Memory_Return_Their_Cost_Reward_Gold_And_Refund_Percent()
        {
            var tower = ScriptableObject.CreateInstance<TowerDefinition>();
            var enemy = ScriptableObject.CreateInstance<EnemyDefinition>();
            var run = ScriptableObject.CreateInstance<RunDefinition>();
            try
            {
                var so = new SerializedObject(tower);
                so.FindProperty("cost").intValue = 17;
                so.FindProperty("shotsPerSecond").floatValue = 1f;
                so.FindProperty("projectileSpeedCellsPerSecond").floatValue = 1f;
                so.ApplyModifiedPropertiesWithoutUndo();
                so = new SerializedObject(enemy);
                so.FindProperty("goldReward").intValue = 6;
                so.ApplyModifiedPropertiesWithoutUndo();
                so = new SerializedObject(run);
                so.FindProperty("startingGold").intValue = 33;
                so.FindProperty("sellRefundPercent").intValue = 70;
                so.ApplyModifiedPropertiesWithoutUndo();

                Assert.AreEqual(17, tower.ToStats().Cost);
                Assert.AreEqual(6, enemy.ToStats().GoldReward);
                Assert.AreEqual(33, run.StartingGold);
                Assert.AreEqual(70, run.SellRefundPercent);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tower);
                UnityEngine.Object.DestroyImmediate(enemy);
                UnityEngine.Object.DestroyImmediate(run);
            }
        }

        // ---- Core side of 3b-AC6: a finished run is frozen

        [Test]
        public void S3b_AC6_Once_The_Run_Is_Lost_Or_Won_Ticks_Do_Not_Change_The_Gold()
        {
            var lost = new Battle(OpenBoard(), new[] { new Wave(Runner, 3, 1f, 0f) }, 1, 5, 100);
            Ticks(lost, 100);
            Assert.AreEqual(RunOutcome.Lost, lost.Outcome);
            int gold = lost.Gold;
            Ticks(lost, 500);
            Assert.AreEqual(gold, lost.Gold);

            Board board = OpenBoard();
            var won = OneEnemy(board, 7, 5);
            won.TryBuyTower(Near, FastTower(1));
            Ticks(won, 5);
            Assert.AreEqual(RunOutcome.Won, won.Outcome);
            gold = won.Gold;
            Ticks(won, 500);
            Assert.AreEqual(gold, won.Gold);
        }

        // ---- 3b-AC10 Core, determinism and the source rule

        private readonly struct Action
        {
            public readonly int Tick;
            public readonly bool Buy;
            public readonly GridPos Cell;
            public Action(int tick, bool buy, GridPos cell) { Tick = tick; Buy = buy; Cell = cell; }
        }

        private static List<(int gold, int lives, RunOutcome outcome)> PlayScript(List<Action> script)
        {
            var tower = new TowerStats(2.5f, 10f, 1.5f, 10f, 3);
            var weak = new EnemyStats(2f, 30f, 2);
            var waves = new[] { new Wave(weak, 10, 0.5f, 0f), new Wave(weak, 8, 0.5f, 5f) };
            Board board = PrototypeBoard();
            var battle = new Battle(board, waves, 12, 10, 60);
            var states = new List<(int, int, RunOutcome)>();
            int next = 0;
            for (int tick = 0; tick < 3000; tick++)
            {
                while (next < script.Count && script[next].Tick == tick)
                {
                    if (script[next].Buy) battle.TryBuyTower(script[next].Cell, tower);
                    else battle.TrySellTower(script[next].Cell);
                    next++;
                }
                battle.Tick();
                states.Add((battle.Gold, battle.Lives, battle.Outcome));
            }
            return states;
        }

        [Test]
        public void S3b_AC10_The_Same_Purchases_And_Sales_On_The_Same_Ticks_Give_The_Same_Gold_Lives_And_Outcome()
        {
            var rnd = new System.Random(20260101);
            var script = new List<Action>();
            for (int tick = 0; tick < 3000; tick += 7)
            {
                var cell = new GridPos(rnd.Next(1, 15), rnd.Next(0, 9));
                script.Add(new Action(tick, rnd.Next(3) != 0, cell));
            }

            var a = PlayScript(script);
            var b = PlayScript(script);

            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i], b[i], $"tick {i}");
            var distinct = new HashSet<int>();
            foreach (var s in a) distinct.Add(s.gold);
            Assert.Greater(distinct.Count, 5, "the script must actually move gold");
        }

        [Test]
        public void S3b_AC10_Outside_Core_And_Tests_Nothing_Calls_TryPlaceTower_Or_TryRemoveTower()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "_Game"));
            string testsDir = Path.Combine(root, "Tests") + Path.DirectorySeparatorChar;
            string coreDir = Path.Combine(root, "Core") + Path.DirectorySeparatorChar;
            int scanned = 0;
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.StartsWith(testsDir) || file.StartsWith(coreDir)) continue;
                scanned++;
                string text = File.ReadAllText(file);
                StringAssert.DoesNotContain("TryPlaceTower", text, file);
                StringAssert.DoesNotContain("TryRemoveTower", text, file);
            }
            Assert.Greater(scanned, 0);
        }

        // ---- 3b-AC11 Allocation

        [Test]
        public void S3b_AC11_A_Sale_And_A_Refused_Purchase_Allocate_Nothing()
        {
            Board board = PrototypeBoard();
            var battle = IdleBattle(board, 1000, 50);
            TowerStats stats = FastTower(5);
            var a = new GridPos(2, 2);
            var c1 = new GridPos(5, 7);
            var c2 = new GridPos(5, 8);

            // Warm-up: every path once.
            battle.TryBuyTower(a, stats); battle.TrySellTower(a);
            battle.TryBuyTower(c1, stats); battle.TryBuyTower(c2, stats); battle.TrySellTower(c1);
            battle.TrySellTower(new GridPos(9, 9));
            var poor = IdleBattle(PrototypeBoard(), 0);
            poor.TryBuyTower(a, stats);

            battle.TryBuyTower(a, stats); // a purchase allocates its tower object
            Assert.That(() => { battle.TrySellTower(a); }, Is.Not.AllocatingGCMemory());
            battle.TryBuyTower(c1, stats);
            Assert.That(() => { battle.TryBuyTower(c2, stats); }, Is.Not.AllocatingGCMemory());                 // refused: would block
            Assert.That(() => { battle.TryBuyTower(TestMaps.Spawn, stats); }, Is.Not.AllocatingGCMemory());     // refused: spawn
            Assert.That(() => { battle.TryBuyTower(c1, stats); }, Is.Not.AllocatingGCMemory());                 // refused: occupied
            Assert.That(() => { poor.TryBuyTower(a, stats); }, Is.Not.AllocatingGCMemory());                    // refused: gold
            Assert.That(() => { battle.TrySellTower(new GridPos(9, 9)); }, Is.Not.AllocatingGCMemory());        // nothing to sell
            Assert.That(() => { battle.TrySellTower(new GridPos(99, 99)); }, Is.Not.AllocatingGCMemory());      // off the grid
        }

        [Test]
        public void S3b_AC11_Tick_Allocates_Nothing_After_The_Peak_With_Kills_That_Pay()
        {
            var weak = new EnemyStats(2f, 30f, 3);
            var tower = new TowerStats(2.5f, 10f, 1.5f, 10f, 2);
            var waves = new List<Wave> { new Wave(weak, 14, 0.5f, 0f), new Wave(weak, 6, 0.5f, 25f) };
            for (int i = 0; i < 60; i++) waves.Add(new Wave(weak, 6, 0.5f, 1f));
            Board board = PrototypeBoard();
            var battle = new Battle(board, waves.ToArray(), 100_000, 4, 50);
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(new GridPos(6, 6), tower));
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(new GridPos(11, 2), tower));
            Ticks(battle, 3000); // warm-up

            int goldBefore = battle.Gold;
            int lastGold = goldBefore, payments = 0;
            Assert.That(() =>
            {
                for (int i = 0; i < 1500; i++)
                {
                    battle.Tick();
                    if (battle.Gold > lastGold) payments++;
                    lastGold = battle.Gold;
                }
            }, Is.Not.AllocatingGCMemory());

            Assert.Greater(payments, 0, "kills paid during the measured ticks");
            Assert.Greater(battle.Gold, goldBefore);
            Assert.AreEqual(RunOutcome.Running, battle.Outcome);
        }
    }
}
