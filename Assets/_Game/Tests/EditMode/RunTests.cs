using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ElementalTD.Core.Tests
{
    /// <summary>The run: waves, lives and the outcome of a Battle built from waves (3a-AC1 to AC5, AC14, AC15).</summary>
    public class RunTests
    {
        private static readonly TowerStats Fast = new TowerStats(20f, 10f, 0.1f, 100f);
        private static readonly TowerStats Slow = new TowerStats(20f, 10f, 0.1f, 1f);
        private static readonly TowerStats Ac8Tower = new TowerStats(2.5f, 10f, 1.5f, 10f);
        private static readonly EnemyStats Ac8Enemy = new EnemyStats(2f, 100f);
        private static readonly EnemyStats Runner = new EnemyStats(60f, 10f); // one cell per tick: reaches the exit in about 16 ticks
        private static readonly EnemyStats Still = new EnemyStats(0f, 10f);

        private static Board OpenBoard() =>
            new Board(new GridMap(16, 9, new List<GridPos>()), new GridPos(0, 4), new GridPos(15, 4));

        private static Board PrototypeBoard() =>
            new Board(new GridMap(16, 9, TestMaps.PrototypeBlocked()), TestMaps.Spawn, TestMaps.Exit);

        private static void Ticks(Battle battle, int count)
        {
            for (int i = 0; i < count; i++) battle.Tick();
        }

        private static void Place(Board board, int x, int y, TowerStats stats) =>
            Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(new GridPos(x, y), stats));

        // ---- 3a-AC1 / AC14 order inside a tick

        [Test]
        public void S3a_AC14_An_Enemy_Enters_And_Moves_In_The_Same_Tick_Waves_Before_Enemies()
        {
            Board board = OpenBoard();
            var battle = new Battle(board, new[] { new Wave(Ac8Enemy, 1, 1f, 0f) }, 10);

            battle.Tick();

            Enemy enemy = board.EnemyAt(0);
            Assert.AreEqual(Ac8Enemy.Speed * Battle.TickSeconds, enemy.Follower.Progress, 1e-5f);
            Assert.AreEqual(enemy.PreviousX, 0f);
        }

        // ---- 3a-AC2 Gone for good

        [Test]
        public void S3a_AC2_After_A_Kill_The_Enemy_Count_Stays_Down_Until_The_Next_Wave()
        {
            Board board = OpenBoard();
            var battle = new Battle(board, new[] { new Wave(Still, 1, 1f, 0f), new Wave(Still, 1, 1f, 5f) }, 10);
            Place(board, 1, 3, Fast);

            battle.Tick();
            Assert.AreEqual(0, board.EnemyCount, "killed on entry");
            Ticks(battle, 250);
            Assert.AreEqual(0, board.EnemyCount, "does not come back");
            Assert.AreEqual(10, battle.Lives, "a kill costs no life");
            Assert.AreEqual(RunOutcome.Running, battle.Outcome);
            Ticks(battle, 100); // wave 2 at about 5 s
            Assert.AreEqual(1, board.EnemyCount);
        }

        [Test]
        public void S3a_AC2_Over_A_Whole_Run_Exactly_The_Sum_Of_The_Counts_Enters()
        {
            Board board = OpenBoard();
            var waves = new[] { new Wave(Runner, 2, 0.5f, 0.5f), new Wave(Runner, 3, 0.5f, 1f), new Wave(Runner, 1, 0.5f, 1f) };
            var battle = new Battle(board, waves, 100);

            Ticks(battle, 2000);

            Assert.AreEqual(RunOutcome.Won, battle.Outcome);
            Assert.AreEqual(100 - 6, battle.Lives, "every one of the 6 enemies entered and exited");
            Assert.AreEqual(3, battle.Waves.StartedWaves);
        }

        // ---- 3a-AC3 / AC4 Lives and outcome

        [Test]
        public void S3a_AC3_Each_Exit_Costs_One_Life()
        {
            Board board = OpenBoard();
            var battle = new Battle(board, new[] { new Wave(Runner, 3, 1f, 0f), new Wave(Still, 1, 1f, 1_000_000f) }, 10);

            Ticks(battle, 20);
            Assert.AreEqual(9, battle.Lives);
            Ticks(battle, 60);
            Assert.AreEqual(8, battle.Lives);
            Ticks(battle, 60);
            Assert.AreEqual(7, battle.Lives);
            Assert.AreEqual(RunOutcome.Running, battle.Outcome);
        }

        [Test]
        public void S3a_AC3_Lives_Never_Go_Below_0_Also_When_Several_Enemies_Exit_On_The_Same_Tick()
        {
            Board board = OpenBoard();
            var battle = new Battle(board, new[] { new Wave(Still, 1, 1f, 1_000_000f) }, 2);
            for (int i = 0; i < 5; i++) board.SpawnEnemy(Runner);

            Ticks(battle, 40);

            Assert.AreEqual(0, battle.Lives);
            Assert.AreEqual(RunOutcome.Lost, battle.Outcome);
        }

        [Test]
        public void S3a_AC4_The_Run_Is_Lost_On_The_Tick_The_Last_Life_Goes_Before_The_Last_Wave_Has_Entered()
        {
            Board board = OpenBoard();
            var battle = new Battle(board, new[] { new Wave(Runner, 3, 2f, 0f) }, 2);

            int lostAt = -1;
            for (int tick = 1; tick <= 1000 && lostAt < 0; tick++)
            {
                battle.Tick();
                if (battle.Outcome == RunOutcome.Lost) lostAt = tick;
            }

            Assert.Greater(lostAt, 0);
            Assert.AreEqual(0, battle.Lives);
            Assert.IsFalse(battle.Waves.IsFinished, "the third enemy had not entered");
        }

        [Test]
        public void S3a_AC4_Loss_Beats_Win_When_The_Last_Enemy_Of_The_Run_Takes_The_Last_Life()
        {
            Board board = OpenBoard();
            var battle = new Battle(board, new[] { new Wave(Runner, 1, 1f, 0f) }, 1);

            Ticks(battle, 30);

            Assert.AreEqual(RunOutcome.Lost, battle.Outcome);
            Assert.AreEqual(0, battle.Lives);
            Assert.IsTrue(battle.Waves.IsFinished);
            Assert.AreEqual(0, board.EnemyCount);
        }

        [Test]
        public void S3a_AC4_The_Last_Enemy_Exiting_With_A_Life_To_Spare_Wins()
        {
            Board board = OpenBoard();
            var battle = new Battle(board, new[] { new Wave(Runner, 1, 1f, 0f) }, 2);

            Ticks(battle, 30);

            Assert.AreEqual(RunOutcome.Won, battle.Outcome);
            Assert.AreEqual(1, battle.Lives);
        }

        [Test]
        public void S3a_AC4_Killing_The_Last_Enemy_Wins_On_That_Tick_And_Not_While_A_Wave_Is_To_Come()
        {
            Board board = OpenBoard();
            var battle = new Battle(board, new[] { new Wave(Still, 1, 1f, 0f), new Wave(Still, 1, 1f, 3f) }, 10);
            Place(board, 1, 3, new TowerStats(20f, 10f, 1f, 100f));

            battle.Tick(); // wave 1's enemy enters and dies
            Assert.AreEqual(0, board.EnemyCount);
            Assert.AreEqual(RunOutcome.Running, battle.Outcome, "a wave is still to come");

            int wonAt = -1;
            for (int tick = 2; tick <= 400 && wonAt < 0; tick++)
            {
                battle.Tick();
                if (battle.Outcome == RunOutcome.Won) wonAt = tick;
            }
            Assert.Greater(wonAt, 0);
            Assert.IsTrue(battle.Waves.IsFinished);
            Assert.AreEqual(10, battle.Lives);
        }

        // ---- 3a-AC5 The run stops

        [TestCase("won")]
        [TestCase("lost")]
        public void S3a_AC5_After_The_Outcome_More_Ticks_Change_Nothing_And_A_Won_Run_Has_No_Projectile(string how)
        {
            Board board = OpenBoard();
            Battle battle;
            if (how == "won")
            {
                battle = new Battle(board, new[] { new Wave(Still, 1, 1f, 0f) }, 10);
                Place(board, 6, 3, Slow); // its shot is still in flight when the run is won
                Place(board, 1, 3, Fast);
                battle.Tick();
                Assert.AreEqual(RunOutcome.Won, battle.Outcome);
            }
            else
            {
                battle = new Battle(board, new[] { new Wave(Runner, 2, 0.5f, 0f), new Wave(Still, 1, 1f, 100f) }, 1);
                board.SpawnEnemy(new EnemyStats(0.5f, 100f)); // still on the board when the run is lost
                Ticks(battle, 100);
                Assert.AreEqual(RunOutcome.Lost, battle.Outcome);
            }
            if (how == "won") Assert.AreEqual(0, battle.ProjectileCount, "a won run has no projectile left");

            int lives = battle.Lives, enemies = board.EnemyCount, shots = battle.ProjectileCount, started = battle.Waves.StartedWaves;
            float seconds = battle.Waves.SecondsToNextWave;
            var positions = new List<float>();
            for (int i = 0; i < board.EnemyCount; i++) positions.Add(board.EnemyAt(i).X + board.EnemyAt(i).Y);

            Ticks(battle, 500);

            Assert.AreEqual(lives, battle.Lives);
            Assert.AreEqual(enemies, board.EnemyCount);
            Assert.AreEqual(shots, battle.ProjectileCount);
            Assert.AreEqual(started, battle.Waves.StartedWaves);
            Assert.AreEqual(seconds, battle.Waves.SecondsToNextWave);
            Assert.AreEqual(how == "won" ? RunOutcome.Won : RunOutcome.Lost, battle.Outcome);
            for (int i = 0; i < positions.Count; i++)
                Assert.AreEqual(positions[i], board.EnemyAt(i).X + board.EnemyAt(i).Y, "enemies do not move");
        }

        // ---- 3a-AC14 Determinism of a whole run

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(2024)]
        public void S3a_AC14_Same_Waves_And_Placements_At_The_Same_Ticks_Give_The_Same_Run_After_Every_Tick(int seed)
        {
            var waves = new[] { new Wave(Ac8Enemy, 3, 0.5f, 2f), new Wave(Ac8Enemy, 4, 0.4f, 2f), new Wave(Ac8Enemy, 2, 1f, 3f) };
            Board boardA = PrototypeBoard(), boardB = PrototypeBoard();
            var a = new Battle(boardA, waves, 3);
            var b = new Battle(boardB, waves, 3);
            var rnd = new Random(seed);

            for (int tick = 0; tick < 4000; tick++)
            {
                if (rnd.Next(20) == 0)
                {
                    var cell = new GridPos(rnd.Next(16), rnd.Next(9));
                    bool remove = boardA.HasTower(cell) && rnd.Next(2) == 0;
                    if (remove) Assert.AreEqual(boardA.TryRemoveTower(cell), boardB.TryRemoveTower(cell));
                    else Assert.AreEqual(boardA.TryPlaceTower(cell, Ac8Tower), boardB.TryPlaceTower(cell, Ac8Tower));
                }
                a.Tick();
                b.Tick();

                Assert.AreEqual(a.Lives, b.Lives, $"lives, tick {tick}");
                Assert.AreEqual(a.Outcome, b.Outcome, $"outcome, tick {tick}");
                Assert.AreEqual(a.Waves.StartedWaves, b.Waves.StartedWaves, $"waves, tick {tick}");
                Assert.AreEqual(a.Waves.SecondsToNextWave, b.Waves.SecondsToNextWave, $"countdown, tick {tick}");
                Assert.AreEqual(boardA.EnemyCount, boardB.EnemyCount, $"enemies, tick {tick}");
                for (int i = 0; i < boardA.EnemyCount; i++)
                {
                    Assert.AreEqual(boardA.EnemyAt(i).Health, boardB.EnemyAt(i).Health);
                    Assert.AreEqual(boardA.EnemyAt(i).X, boardB.EnemyAt(i).X);
                    Assert.AreEqual(boardA.EnemyAt(i).Y, boardB.EnemyAt(i).Y);
                }
                Assert.AreEqual(a.ProjectileCount, b.ProjectileCount, $"projectiles, tick {tick}");
                for (int i = 0; i < a.ProjectileCount; i++)
                {
                    Assert.AreEqual(a.ProjectileAt(i).X, b.ProjectileAt(i).X);
                    Assert.AreEqual(a.ProjectileAt(i).Y, b.ProjectileAt(i).Y);
                }
            }
            Assert.AreNotEqual(RunOutcome.Running, a.Outcome, "the run must end within the ticks");
        }

        [Test]
        public void S3a_AC14_An_Enemy_That_Steps_Into_Range_On_A_Tick_Is_Shot_On_That_Tick_Enemies_Before_Towers()
        {
            Board board = OpenBoard();
            var battle = new Battle(board, new[] { new Wave(new EnemyStats(2f, 100f), 1, 1f, 0f) }, 10);
            var towerCell = new GridPos(8, 3);
            Place(board, 8, 3, new TowerStats(1.5f, 10f, 1f, 1f)); // a window of about 2 cells along the route

            int enteredRange = -1;
            for (int tick = 1; tick <= 600 && enteredRange < 0; tick++)
            {
                battle.Tick();
                Enemy enemy = board.EnemyAt(0);
                float dx = enemy.X - towerCell.X, dy = enemy.Y - towerCell.Y;
                bool inRange = dx * dx + dy * dy <= 1.5f * 1.5f;
                if (!inRange) Assert.AreEqual(0, battle.ProjectileCount, $"tick {tick}: shot before the enemy was in range");
                else enteredRange = tick;
            }

            Assert.Greater(enteredRange, 0, "the enemy never came into range");
            Assert.AreEqual(1, battle.ProjectileCount, "shot on the tick it stepped into range");
        }

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(2024)]
        public void S3a_AC4_After_Every_Tick_No_Lives_Means_Lost_And_A_Finished_Empty_Board_With_Lives_Means_Won(int seed)
        {
            var waves = new[] { new Wave(Ac8Enemy, 3, 0.5f, 2f), new Wave(Ac8Enemy, 4, 0.4f, 2f), new Wave(Ac8Enemy, 2, 1f, 3f) };
            Board board = PrototypeBoard();
            var battle = new Battle(board, waves, 4);
            var rnd = new Random(seed);
            bool ended = false;

            for (int tick = 0; tick < 4000; tick++)
            {
                if (rnd.Next(15) == 0) board.TryPlaceTower(new GridPos(rnd.Next(16), rnd.Next(9)), Ac8Tower);
                battle.Tick();

                if (battle.Lives == 0) Assert.AreEqual(RunOutcome.Lost, battle.Outcome, $"tick {tick}");
                else if (battle.Waves.IsFinished && board.EnemyCount == 0) Assert.AreEqual(RunOutcome.Won, battle.Outcome, $"tick {tick}");
                else Assert.AreEqual(RunOutcome.Running, battle.Outcome, $"tick {tick}");
                ended |= battle.Outcome != RunOutcome.Running;
            }
            Assert.IsTrue(ended, "the run must end within the ticks");
        }

        // ---- 3a-AC15 Allocation

        [Test]
        public void S3a_AC15_Tick_Allocates_Nothing_After_The_Peak_Across_Spawns_Kills_Exits_And_Lost_Lives()
        {
            // The biggest wave is first, so the warm-up reaches the peak. Many small waves follow to keep the run going.
            var weak = new EnemyStats(2f, 30f); // three hits: some die, some reach the exit
            var waves = new List<Wave> { new Wave(weak, 14, 0.5f, 0f), new Wave(weak, 6, 0.5f, 25f) }; // the board empties after the peak
            for (int i = 0; i < 60; i++) waves.Add(new Wave(weak, 6, 0.5f, 1f));
            Board board = PrototypeBoard();
            var battle = new Battle(board, waves.ToArray(), 100_000);
            Place(board, 6, 6, Ac8Tower);
            Place(board, 11, 2, Ac8Tower);
            Ticks(battle, 3000); // warm-up: the pools reach their peak and the board empties once

            int spawns = 0, kills = 0, exits = 0, maxShots = 0;
            int started = battle.Waves.StartedWaves, before = board.EnemyCount, lives = battle.Lives;
            TickAllocation.AssertNoneAcross(battle, 1500, _ =>
            {
                if (battle.Lives < lives) exits += lives - battle.Lives;
                else if (board.EnemyCount < before) kills++;
                if (board.EnemyCount > before || battle.Waves.StartedWaves > started) spawns++;
                if (battle.ProjectileCount > maxShots) maxShots = battle.ProjectileCount;
                started = battle.Waves.StartedWaves;
                before = board.EnemyCount;
                lives = battle.Lives;
            });

            Assert.AreEqual(RunOutcome.Running, battle.Outcome);
            Assert.Greater(spawns, 0);
            Assert.Greater(kills, 0);
            Assert.Greater(exits, 0, "lives were lost");
            Assert.Greater(maxShots, 0);
        }
    }
}
