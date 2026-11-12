using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ElementalTD.Core.Tests
{
    /// <summary>Combat rules (2c, 3a-AC10): enemies are spawned by hand on a battle whose only wave never enters.</summary>
    public class BattleTests
    {
        // Keeps the run going; its delay is longer than any test.
        private static readonly Wave Parked = new Wave(new EnemyStats(0f, 1f), 1, 1f, 1_000_000f);

        private static Board OpenBoard(List<GridPos> blocked = null) =>
            new Board(new GridMap(16, 9, blocked ?? new List<GridPos>()), new GridPos(0, 4), new GridPos(15, 4));

        private static Board PrototypeBoard() =>
            new Board(new GridMap(16, 9, TestMaps.PrototypeBlocked()), TestMaps.Spawn, TestMaps.Exit);

        private static Battle Manual(Board board, int lives = 10) => new Battle(board, new[] { Parked }, lives);

        private static Enemy Spawn(Board board, float speed, float health) => board.SpawnEnemy(new EnemyStats(speed, health));

        private static void Place(Board board, int x, int y, TowerStats stats) =>
            Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(new GridPos(x, y), stats));

        private static void Ticks(Battle battle, int count)
        {
            for (int i = 0; i < count; i++) battle.Tick();
        }

        // A fast shot (hits in one tick at distance <= 1.67) and a slow one that stays in flight.
        private static readonly TowerStats Fast = new TowerStats(20f, 10f, 0.1f, 100f);
        private static readonly TowerStats Slow = new TowerStats(20f, 10f, 0.1f, 1f);

        // ---- 2c-AC1 Firing

        [TestCase(3f, true)]
        [TestCase(2.999f, false)]
        public void S2c_AC1_Range_Is_Inclusive_And_A_Wall_Does_Not_Block_The_Shot(float range, bool shoots)
        {
            Board board = OpenBoard(new List<GridPos> { new GridPos(1, 4), new GridPos(2, 4) }); // between tower and enemy
            Battle battle = Manual(board);
            Spawn(board, 0f, 100f);
            Place(board, 3, 4, new TowerStats(range, 10f, 1f, 10f));

            battle.Tick();

            Assert.AreEqual(shoots ? 1 : 0, battle.ProjectileCount);
        }

        [Test]
        public void S2c_AC1_A_Tower_Placed_Next_To_A_Waiting_Enemy_Fires_On_The_First_Tick()
        {
            Board board = OpenBoard();
            Battle battle = Manual(board);
            Spawn(board, 0f, 100f);
            Place(board, 1, 3, new TowerStats(2.5f, 10f, 1.5f, 10f));
            battle.Tick();
            Assert.AreEqual(1, battle.ProjectileCount);
        }

        [Test]
        public void S2c_AC1_Cadence_Is_1_5_Shots_Per_Second_90_Over_60_Seconds()
        {
            // A shot hits within the tick it is fired (distance 1.4, step 1.67), so hits count shots.
            Board board = OpenBoard();
            Battle battle = Manual(board);
            Enemy enemy = Spawn(board, 0f, 1_000_000f);
            Place(board, 1, 3, new TowerStats(2f, 1f, 1.5f, 100f));
            Ticks(battle, 3600);
            Assert.AreEqual(90f, 1_000_000f - enemy.Health, 1f);
        }

        [Test]
        public void S2c_AC1_A_Tower_Fires_At_Most_Once_Per_Tick_Even_Above_60_Shots_Per_Second()
        {
            Board board = OpenBoard();
            Battle battle = Manual(board);
            Spawn(board, 0f, 100f);
            Place(board, 1, 3, new TowerStats(20f, 1f, 600f, 0.001f)); // shots never land
            for (int tick = 1; tick <= 10; tick++)
            {
                battle.Tick();
                Assert.AreEqual(tick, battle.ProjectileCount, $"tick {tick}");
            }
        }

        [Test]
        public void S2c_AC1_No_Enemy_On_The_Board_Means_No_Shot()
        {
            Board board = OpenBoard();
            Battle battle = Manual(board);
            Place(board, 1, 3, Fast);
            Ticks(battle, 120);
            Assert.AreEqual(0, battle.ProjectileCount);
        }

        // ---- 2c-AC2 / 3a-AC10 Targeting with several enemies

        [TestCase(5)]  // both still head for the same cell, the first has more progress
        [TestCase(90)] // the first is a cell further along
        public void S2c_AC2_The_Enemy_Nearer_The_Exit_Is_Shot(int ticksBeforeSecondEnters)
        {
            Board board = OpenBoard();
            Battle battle = Manual(board);
            Enemy first = Spawn(board, 2f, 100f);
            Ticks(battle, ticksBeforeSecondEnters);
            Enemy second = Spawn(board, 2f, 100f);
            Place(board, 2, 2, new TowerStats(20f, 10f, 0.1f, 100f));

            Ticks(battle, 3);

            Assert.AreEqual(90f, first.Health);
            Assert.AreEqual(100f, second.Health);
        }

        [Test]
        public void S3a_AC10_With_Three_Enemies_On_The_Board_The_One_Nearest_The_Exit_Is_Shot()
        {
            Board board = OpenBoard();
            Battle battle = Manual(board);
            Enemy front = Spawn(board, 2f, 100f);
            Ticks(battle, 60);
            Enemy middle = Spawn(board, 2f, 100f);
            Ticks(battle, 60);
            Enemy back = Spawn(board, 2f, 100f);
            Place(board, 2, 2, new TowerStats(20f, 10f, 0.1f, 100f));

            Ticks(battle, 3);

            Assert.AreEqual(90f, front.Health);
            Assert.AreEqual(100f, middle.Health);
            Assert.AreEqual(100f, back.Health);
        }

        [Test]
        public void S2c_AC2_On_An_Exact_Tie_The_First_To_Enter_Is_Shot()
        {
            Board board = OpenBoard();
            Battle battle = Manual(board);
            Enemy first = Spawn(board, 2f, 100f);
            Enemy second = Spawn(board, 2f, 100f);
            Place(board, 2, 2, new TowerStats(20f, 10f, 0.1f, 100f));

            Ticks(battle, 3);

            Assert.AreEqual(90f, first.Health);
            Assert.AreEqual(100f, second.Health);
        }

        // ---- 2c-AC3 / 3a-AC10 Projectile

        [Test]
        public void S2c_AC3_A_Shot_Starts_On_The_Tower_Cell_Gets_Closer_Every_Tick_And_Hits_Once()
        {
            Board board = OpenBoard();
            Battle battle = Manual(board);
            Enemy enemy = Spawn(board, 0f, 100f);
            Place(board, 6, 4, new TowerStats(10f, 10f, 0.1f, 1f)); // one shot per 10 s, 1 cell/s

            battle.Tick();
            Projectile shot = battle.ProjectileAt(0);
            Assert.AreEqual(6f, shot.PreviousX);
            Assert.AreEqual(4f, shot.PreviousY);

            float last = shot.X;
            int guard = 0;
            while (battle.ProjectileCount > 0 && guard++ < 1000)
            {
                Assert.AreEqual(100f, enemy.Health, "no damage before the hit");
                battle.Tick();
                if (battle.ProjectileCount == 0) break;
                Assert.Less(shot.X, last, "closer every tick");
                last = shot.X;
            }

            Assert.AreEqual(0, battle.ProjectileCount);
            Assert.AreEqual(90f, enemy.Health);
            Ticks(battle, 300); // the next shot is 10 s after the first
            Assert.AreEqual(90f, enemy.Health, "damage applied once");
        }

        [Test]
        public void S2c_AC3_A_Second_Shot_In_Flight_When_The_First_Kills_Vanishes_With_No_Effect()
        {
            Board board = OpenBoard();
            Battle battle = Manual(board);
            Enemy enemy = Spawn(board, 0f, 10f);
            Place(board, 6, 4, Slow);
            Place(board, 1, 3, Fast);

            Ticks(battle, 2);

            Assert.AreEqual(0, battle.ProjectileCount);
            Assert.AreEqual(0, board.EnemyCount);
            Assert.AreEqual(0f, enemy.Health, "only the killing shot applied its damage");
        }

        [Test]
        public void S2c_AC3_After_TryRemoveTower_That_Towers_Shot_Still_Hits()
        {
            Board board = OpenBoard();
            Battle battle = Manual(board);
            Enemy enemy = Spawn(board, 0f, 100f);
            Place(board, 3, 4, new TowerStats(10f, 10f, 0.1f, 10f));
            battle.Tick();
            Assert.AreEqual(1, battle.ProjectileCount);

            Assert.IsTrue(board.TryRemoveTower(new GridPos(3, 4)));
            Assert.AreEqual(1, battle.ProjectileCount, "the shot keeps flying");
            Ticks(battle, 30);

            Assert.AreEqual(0, battle.ProjectileCount);
            Assert.AreEqual(90f, enemy.Health);
        }

        [Test]
        public void S3a_AC10_A_Slow_Shot_At_A_Killed_Enemy_Vanishes_When_The_Next_Enemy_Reuses_The_Object()
        {
            // A wave of two, one tick apart: the first dies on tick 1 and the second enters on tick 2, in the same object.
            var wave = new Wave(new EnemyStats(0f, 10f), 2, Battle.TickSeconds, 0f);
            Board board = OpenBoard();
            var battle = new Battle(board, new[] { wave, Parked }, 10);
            Place(board, 6, 3, Slow);
            Place(board, 1, 3, Fast);

            battle.Tick();
            Assert.AreEqual(0, board.EnemyCount, "killed on the tick it entered");
            Assert.AreEqual(1, battle.ProjectileCount, "the slow shot is still in flight");

            battle.Tick();
            Assert.AreEqual(1, board.EnemyCount);
            Enemy second = board.EnemyAt(0);
            Assert.AreEqual(second.MaxHealth, second.Health);
            Assert.AreEqual(0, battle.ProjectileCount, "the stale shot is gone");
            Ticks(battle, 400); // the stale shot would need about 365 ticks to reach the spawn
            Assert.AreEqual(second.MaxHealth, second.Health, "the stale shot never hits");
        }

        [Test]
        public void S3a_AC10_A_Reused_Enemy_Object_Is_The_Same_Instance_With_The_New_Wave_Stats()
        {
            Board board = OpenBoard();
            Enemy first = board.SpawnEnemy(new EnemyStats(2f, 100f));
            board.RemoveEnemy(first);

            Enemy second = board.SpawnEnemy(new EnemyStats(3f, 40f));

            Assert.AreSame(first, second);
            Assert.AreEqual(3f, second.Speed);
            Assert.AreEqual(40f, second.MaxHealth);
            Assert.AreEqual(40f, second.Health);
            Assert.IsTrue(second.IsOnBoard);
            Assert.AreEqual(1, board.EnemyCount);
        }

        // ---- 2c-AC4 / 3a-AC2 Leaving

        [TestCase("killed")]
        [TestCase("exit")]
        public void S2c_AC4_The_Enemy_Leaves_The_Board_And_The_Cell_It_Stood_On_Can_Be_Built_On(string how)
        {
            var cell = new GridPos(1, 4);
            Board board = OpenBoard();
            Battle battle = Manual(board);
            Enemy enemy;
            if (how == "killed")
            {
                enemy = Spawn(board, 2f, 10f);
                Place(board, 3, 2, Fast);
                Assert.AreEqual(PlacementResult.EnemyInTheWay, board.TryPlaceTower(cell, Fast), "the enemy is heading for it");
                Ticks(battle, 3);
            }
            else
            {
                enemy = Spawn(board, 60f, 10f); // one cell per tick
                Ticks(battle, 30);
            }

            Assert.AreEqual(0, board.EnemyCount);
            Assert.IsFalse(enemy.IsOnBoard);
            Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(cell, Fast));
        }

        // ---- 2c-AC7 / 3a-AC11 Placement

        [Test]
        public void S2c_AC7_TowerPlaced_And_TowerRemoved_Fire_Once_After_The_Change_And_Never_On_A_Refusal()
        {
            Board board = PrototypeBoard();
            var placed = new List<(GridPos cell, bool hasTower)>();
            var removed = new List<(GridPos cell, bool hasTower)>();
            board.TowerPlaced += c => placed.Add((c, board.HasTower(c)));
            board.TowerRemoved += c => removed.Add((c, board.HasTower(c)));
            var cell = new GridPos(2, 2);

            board.TryPlaceTower(cell, Fast);
            board.TryPlaceTower(cell, Fast);              // refused: occupied
            board.TryPlaceTower(TestMaps.Spawn, Fast);    // refused: spawn
            board.TryRemoveTower(new GridPos(3, 3));      // nothing there

            Assert.AreEqual(1, placed.Count);
            Assert.AreEqual((cell, true), placed[0]);
            Assert.AreEqual(0, removed.Count);

            board.TryRemoveTower(cell);

            Assert.AreEqual(1, placed.Count);
            Assert.AreEqual(1, removed.Count);
            Assert.AreEqual((cell, false), removed[0]);
        }

        [Test]
        public void S3a_AC11_With_No_Enemy_On_The_Board_Only_The_Spawn_Must_Still_Reach_The_Exit()
        {
            Board board = PrototypeBoard();
            Manual(board);
            Assert.AreEqual(0, board.EnemyCount);

            Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(new GridPos(5, 7), Fast));
            Assert.AreEqual(PlacementResult.WouldBlockPath, board.TryPlaceTower(new GridPos(5, 8), Fast));
            Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(new GridPos(1, 4), Fast), "a cell no enemy is on");
            Assert.IsTrue(board.Field.IsReachable(TestMaps.Spawn));
        }

        [Test]
        public void S3a_AC11_With_Enemies_In_Two_Corridors_Cutting_Off_Either_Corridor_Is_Refused()
        {
            // 7x5, spawn (0,2), exit (6,2), walls on x = 1..5, y = 1..3: corridors y = 0 and y = 4.
            var blocked = new List<GridPos>();
            for (int x = 1; x <= 5; x++)
                for (int y = 1; y <= 3; y++)
                    blocked.Add(new GridPos(x, y));
            var board = new Board(new GridMap(7, 5, blocked), new GridPos(0, 2), new GridPos(6, 2));
            Battle battle = Manual(board);

            Enemy first = Spawn(board, 0.5f, 100f);
            for (int i = 0; i < 2000 && first.Follower.From.X < 2; i++) battle.Tick();
            Assert.AreEqual(2, first.Follower.From.X);
            int firstRow = first.Follower.To.Y;
            int otherRow = 4 - firstRow;
            Place(board, 1, firstRow, Fast); // close the entrance behind it, so the next enemy takes the other corridor

            Enemy second = Spawn(board, 2f, 100f);
            for (int i = 0; i < 2000 && second.Follower.From.X < 2; i++) battle.Tick();
            Assert.AreEqual(2, second.Follower.From.X);
            Assert.AreEqual(otherRow, second.Follower.To.Y, "the second enemy took the other corridor");

            // The first enemy's corridor is closed behind it, so cutting it ahead of it strands it, although the spawn still reaches the exit.
            Assert.IsTrue(board.Field.IsReachable(board.Spawn));
            Assert.AreEqual(PlacementResult.WouldBlockPath, board.TryPlaceTower(new GridPos(5, firstRow), Fast), "cuts the first enemy off");
            Assert.AreEqual(PlacementResult.WouldBlockPath, board.TryPlaceTower(new GridPos(5, otherRow), Fast), "cuts the second enemy off");
            Assert.IsTrue(board.Field.IsReachable(board.Spawn));
            Assert.IsFalse(board.HasTower(new GridPos(5, firstRow)) || board.HasTower(new GridPos(5, otherRow)), "nothing changed");
        }

        // ---- Data rules of the stats

        [TestCase(-1f, 10f, 1f, 1f)]
        [TestCase(1f, -1f, 1f, 1f)]
        [TestCase(1f, 10f, 0f, 1f)]
        [TestCase(1f, 10f, 1f, 0f)]
        public void S2c_AC8_TowerStats_Rejects_Invalid_Values(float range, float damage, float shots, float speed)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TowerStats(range, damage, shots, speed));
        }

        [TestCase(-1f, 10f)]
        [TestCase(1f, 0f)]
        public void S2c_AC8_EnemyStats_Rejects_Invalid_Values(float speed, float health)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyStats(speed, health));
        }
    }
}
