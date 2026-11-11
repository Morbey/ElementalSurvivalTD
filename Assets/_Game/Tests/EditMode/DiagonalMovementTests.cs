using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace ElementalTD.Core.Tests
{
    /// <summary>Step 3c: speed along a diagonal, the placement beside an enemy mid-step, determinism and allocation (3c-AC4, AC5, AC9, AC10).</summary>
    public class DiagonalMovementTests
    {
        private const float Tick = Battle.TickSeconds;
        private static readonly TowerStats Stats = new TowerStats(2.5f, 10f, 1.5f, 10f);

        // Keeps the run going: its delay is longer than any test.
        private static readonly Wave Parked = new Wave(new EnemyStats(0f, 1f), 1, 1f, 1_000_000f);

        private static Battle ParkedBattle(Board board) => new Battle(board, new[] { Parked }, 10);

        private static Board PrototypeBoard() => new Board(new GridMap(16, 9, TestMaps.PrototypeBlocked()), TestMaps.Spawn, TestMaps.Exit);

        private static bool IsDiagonal(PathFollower f) => f.From.X != f.To.X && f.From.Y != f.To.Y;

        private static void Ticks(Battle battle, int count)
        {
            for (int i = 0; i < count; i++) battle.Tick();
        }

        private static int[] Snapshot(Board board)
        {
            int w = board.Map.Width, h = board.Map.Height;
            var s = new int[w * h * 2];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    s[(y * w + x) * 2] = board.Field.Distance(new GridPos(x, y));
                    s[(y * w + x) * 2 + 1] = board.HasTower(new GridPos(x, y)) ? 1 : 0;
                }
            return s;
        }

        // ---- 3c-AC4 Speed

        [Test]
        public void S3c_AC4_A_Diagonal_Step_Is_577_Over_408_Cells_Long_And_A_Straight_Or_Standing_One_Is_1()
        {
            FlowField field = TestMaps.OpenField(5, 5, new GridPos(4, 4), out _);
            var f = new PathFollower(field, new GridPos(0, 0));
            Assert.AreEqual(577f / 408f, f.StepLength);

            FlowField straight = TestMaps.OpenField(5, 1, new GridPos(4, 0), out _);
            Assert.AreEqual(1f, new PathFollower(straight, new GridPos(0, 0)).StepLength);
            Assert.AreEqual(1f, new PathFollower(field, new GridPos(4, 4)).StepLength, "standing on the exit");
            Assert.AreEqual(0f, new PathFollower(field, new GridPos(4, 4)).DistanceToExit);
        }

        [Test]
        public void S3c_AC4_Progress_Counts_Cells_Along_The_Path_And_Carries_Over_The_Step_Change()
        {
            FlowField field = TestMaps.OpenField(5, 5, new GridPos(4, 4), out _);
            var f = new PathFollower(field, new GridPos(0, 0));
            f.Step(0.5f, 2f); // 1 cell along a diagonal step of 1.41422
            Assert.AreEqual(new GridPos(0, 0), f.From);
            Assert.AreEqual(1f, f.Progress, 1e-6f);
            f.Step(0.5f, 2f); // 2 cells: the first step ends at 1.41422, 0.58578 is carried into the next
            Assert.AreEqual(new GridPos(1, 1), f.From);
            Assert.AreEqual(new GridPos(2, 2), f.To);
            Assert.AreEqual(2f - 577f / 408f, f.Progress, 1e-5f);
        }

        [Test]
        public void S3c_AC4_On_Arrival_The_Step_Length_Is_1_And_The_Progress_0()
        {
            FlowField field = TestMaps.OpenField(5, 5, new GridPos(4, 4), out _);
            var f = new PathFollower(field, new GridPos(0, 0));
            Assert.AreEqual(577f / 408f, f.StepLength);
            while (!f.Step(0.1f, 2f)) { }
            Assert.IsTrue(f.HasArrived);
            Assert.AreEqual(1f, f.StepLength);
            Assert.AreEqual(0f, f.Progress);
            Assert.AreEqual(0f, f.DistanceToExit);
        }

        [Test]
        public void S3c_AC4_DistanceToExit_Depends_On_The_Cell_Heading_For_And_The_Progress_Not_On_The_Cell_It_Left()
        {
            // Random buys and sales while an enemy is mid-step. Whenever the distance of its To cell is unchanged by an action,
            // its DistanceToExit must be unchanged, even if the distance of From changed.
            int fromChangedOnly = 0, actions = 0;
            for (int seed = 0; seed < 40; seed++)
            {
                var rnd = new System.Random(seed);
                Board board = PrototypeBoard();
                PathFollower f = board.SpawnEnemy(new EnemyStats(2f, 100f)).Follower;
                for (int i = 0; i < 400; i++)
                {
                    f.Step((float)(0.05 + rnd.NextDouble() * 0.5), 2f);
                    if (f.HasArrived) f.Restart(board.Spawn);
                    GridPos cell = new GridPos(rnd.Next(16), rnd.Next(9));
                    int fromBefore = board.Field.Distance(f.From), toBefore = board.Field.Distance(f.To);
                    float before = f.DistanceToExit;
                    if (board.HasTower(cell)) board.TryRemoveTower(cell); else board.TryPlaceTower(cell, Stats);
                    actions++;
                    if (board.Field.Distance(f.To) != toBefore) continue;
                    Assert.AreEqual(before, f.DistanceToExit, $"seed {seed} i {i}: {cell} changed nothing for To {f.To}");
                    if (board.Field.Distance(f.From) != fromBefore) fromChangedOnly++;
                }
            }
            Assert.Greater(actions, 10000);
            Assert.Greater(fromChangedOnly, 0, "the fuzz must reach a case where only the distance of From changed");
        }

        [Test]
        public void S3c_AC4_On_An_Open_5x5_After_60_Ticks_At_Speed_2_The_Enemy_Is_On_x_y_1_414_And_Arrives_After_2_83_Seconds()
        {
            Board board = new Board(new GridMap(5, 5, null), new GridPos(0, 0), new GridPos(4, 4));
            Battle battle = ParkedBattle(board);
            Enemy enemy = board.SpawnEnemy(new EnemyStats(2f, 100f));

            Ticks(battle, 60);
            Assert.AreEqual(1.414f, enemy.X, 0.001f);
            Assert.AreEqual(1.414f, enemy.Y, 0.001f);

            int ticks = 60;
            while (board.EnemyCount > 0 && ticks < 400) { battle.Tick(); ticks++; }
            Assert.AreEqual(0, board.EnemyCount);
            Assert.AreEqual(2.83f, ticks * Tick, 0.03f, "4 diagonal steps of 1.41422 cells at 2 cells per second = 2.828 s");
            Assert.AreEqual(9, battle.Lives, "the enemy left by the exit");
        }

        [Test]
        public void S3c_AC4_The_Empty_Prototype_Route_Is_9_Straight_And_9_Diagonal_Steps_And_Takes_652_Ticks()
        {
            Board board = PrototypeBoard();
            Battle battle = ParkedBattle(board);
            Enemy enemy = board.SpawnEnemy(new EnemyStats(2f, 100f));
            PathFollower f = enemy.Follower;

            Assert.AreEqual(8865f / 408f, enemy.DistanceToExit, 1e-4f);
            Assert.AreEqual(21.728f, enemy.DistanceToExit, 0.001f);

            int straight = 0, diagonal = 0, ticks = 0;
            GridPos from = f.From;
            float previous = enemy.DistanceToExit;
            while (board.EnemyCount > 0 && ticks < 2000)
            {
                battle.Tick();
                ticks++;
                if (f.From != from)
                {
                    if (f.From.X != from.X && f.From.Y != from.Y) diagonal++; else straight++;
                    from = f.From;
                }
                if (board.EnemyCount > 0)
                {
                    Assert.AreEqual(2f * Tick, previous - enemy.DistanceToExit, 1e-4f, $"tick {ticks}: DistanceToExit must fall by speed * tick");
                    previous = enemy.DistanceToExit;
                }
            }

            Assert.AreEqual(9, straight, "straight steps");
            Assert.AreEqual(9, diagonal, "diagonal steps");
            Assert.That(ticks, Is.InRange(651, 653).Or.EqualTo(652));
            Assert.AreEqual(9, battle.Lives);
        }

        [TestCase(0.37f)]
        [TestCase(0.011f)]
        [TestCase(0.9f)]
        public void S3c_AC4_DistanceToExit_Falls_Smoothly_And_Strictly_Across_Straight_To_Diagonal_Changes_With_Any_Dt(float dt)
        {
            FlowField field = TestMaps.PrototypeField(out _);
            var f = new PathFollower(field, TestMaps.Spawn);
            float speed = 2f;
            float previous = f.DistanceToExit;
            Assert.AreEqual(8865f / 408f, previous, 1e-4f);
            int changes = 0, guard = 0;
            bool wasDiagonal = IsDiagonal(f);
            while (!f.HasArrived && guard++ < 5000)
            {
                bool arrived = f.Step(dt, speed);
                if (arrived) break;
                float current = f.DistanceToExit;
                Assert.Less(current, previous, "strictly falling");
                Assert.AreEqual(dt * speed, previous - current, 1e-3f, $"fall per call at {f.From}->{f.To}");
                if (IsDiagonal(f) != wasDiagonal) changes++;
                wasDiagonal = IsDiagonal(f);
                previous = current;
            }
            Assert.IsTrue(f.HasArrived);
            Assert.AreEqual(0f, f.DistanceToExit);
            Assert.GreaterOrEqual(changes, 4, "the run changes kind of step: straight, diagonal, straight, diagonal, straight, diagonal");
        }

        [Test]
        public void S3c_AC4_Speed_Along_The_Path_Is_The_Same_In_All_Eight_Directions()
        {
            // 9x9 open board, spawn in the middle, the exit 3 cells away in each of the 8 directions.
            int[] dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
            int[] dy = { 0, 1, 1, 1, 0, -1, -1, -1 };
            for (int k = 0; k < 8; k++)
            {
                var spawn = new GridPos(4, 4);
                var exit = new GridPos(4 + 3 * dx[k], 4 + 3 * dy[k]);
                var board = new Board(new GridMap(9, 9, null), spawn, exit);
                Battle battle = ParkedBattle(board);
                Enemy enemy = board.SpawnEnemy(new EnemyStats(2f, 100f));
                int ticks = 0;
                while (board.EnemyCount > 0 && ticks < 500)
                {
                    battle.Tick();
                    ticks++;
                    if (board.EnemyCount == 0) break;
                    float moved = Mathf.Sqrt((enemy.X - enemy.PreviousX) * (enemy.X - enemy.PreviousX) + (enemy.Y - enemy.PreviousY) * (enemy.Y - enemy.PreviousY));
                    Assert.AreEqual(2f * Tick, moved, 1e-4f, $"direction {k} tick {ticks}");
                }
                bool diagonal = dx[k] != 0 && dy[k] != 0;
                float cells = diagonal ? 3 * 577f / 408f : 3f;
                Assert.AreEqual(cells / (2f * Tick), ticks, 1.01f, $"direction {k}");
            }
        }

        [Test]
        public void S3c_AC4_With_Two_Enemies_On_Different_Kinds_Of_Step_The_Tower_Shoots_The_One_With_Less_Path_Left()
        {
            // 12x9 open, spawn (0,0), exit (11,8): 3 straight steps then 8 diagonal ones. A is slow and still on a straight step,
            // B is fast and entered later: B is on a diagonal step with less path left, so it must be shot, not the first to enter.
            var board = new Board(new GridMap(12, 9, null), new GridPos(0, 0), new GridPos(11, 8));
            Battle battle = ParkedBattle(board);
            Enemy a = board.SpawnEnemy(new EnemyStats(0.5f, 100f));
            Ticks(battle, 60);
            Enemy b = board.SpawnEnemy(new EnemyStats(6f, 100f));

            int guard = 0;
            while (!(IsDiagonal(b.Follower) && !IsDiagonal(a.Follower) && b.DistanceToExit < a.DistanceToExit) && guard++ < 300) battle.Tick();
            Assert.Less(guard, 300, "the setup must arise");
            Assert.IsTrue(IsDiagonal(b.Follower));
            Assert.IsFalse(IsDiagonal(a.Follower));
            Assert.Less(b.DistanceToExit, a.DistanceToExit);

            Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(new GridPos(10, 2), new TowerStats(50f, 10f, 0.1f, 100f)));
            Ticks(battle, 12);

            Assert.AreEqual(90f, b.Health);
            Assert.AreEqual(100f, a.Health);
        }

        // ---- 3c-AC5 Placement

        [Test]
        public void S3c_AC5_Beside_An_Enemy_In_Mid_Diagonal_The_Four_Cells_Of_The_Step_Are_Refused_And_A_Cell_Beyond_Is_Placed_And_Followed()
        {
            var board = new Board(new GridMap(5, 5, null), new GridPos(0, 0), new GridPos(4, 4));
            Battle battle = ParkedBattle(board);
            Enemy enemy = board.SpawnEnemy(new EnemyStats(2f, 100f));
            PathFollower f = enemy.Follower;
            int guard = 0;
            while (!(f.From == new GridPos(1, 1) && f.To == new GridPos(2, 2)) && guard++ < 200) battle.Tick();
            Assert.AreEqual(new GridPos(1, 1), f.From);
            Assert.AreEqual(new GridPos(2, 2), f.To);

            foreach (var cell in new[] { new GridPos(1, 1), new GridPos(2, 2), new GridPos(2, 1), new GridPos(1, 2) })
            {
                int[] before = Snapshot(board);
                Assert.AreEqual(PlacementResult.EnemyInTheWay, board.TryPlaceTower(cell, Stats), $"cell {cell}");
                CollectionAssert.AreEqual(before, Snapshot(board));
                Assert.IsFalse(board.Map.IsBlocked(cell));
            }
            Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(new GridPos(3, 2), Stats));

            var visited = new List<GridPos> { f.From };
            guard = 0;
            while (board.EnemyCount > 0 && guard++ < 400)
            {
                battle.Tick();
                if (f.From != visited[visited.Count - 1]) visited.Add(f.From);
                Assert.IsFalse(board.Map.IsBlocked(f.From), $"enemy on blocked {f.From}");
                Assert.IsFalse(board.Map.IsBlocked(f.To), $"enemy heads for blocked {f.To}");
                if (IsDiagonal(f))
                {
                    Assert.IsFalse(board.Map.IsBlocked(new GridPos(f.From.X, f.To.Y)), $"corner of {f.From}->{f.To}");
                    Assert.IsFalse(board.Map.IsBlocked(new GridPos(f.To.X, f.From.Y)), $"corner of {f.From}->{f.To}");
                }
                Assert.IsFalse(f.From == f.To && !f.HasArrived, "stranded");
            }
            Assert.AreEqual(0, board.EnemyCount, "the enemy reaches the exit");
            CollectionAssert.AreEqual(new[] { new GridPos(1, 1), new GridPos(2, 2), new GridPos(2, 3), new GridPos(3, 4), new GridPos(4, 4) }, visited);
        }

        [Test]
        public void S3c_AC5_Beside_A_Straight_Step_Both_Side_Cells_Can_Be_Built_On_But_Not_The_Two_Cells_Of_The_Step()
        {
            var board = new Board(new GridMap(5, 5, null), new GridPos(0, 2), new GridPos(4, 2));
            Battle battle = ParkedBattle(board);
            Enemy enemy = board.SpawnEnemy(new EnemyStats(2f, 100f));
            PathFollower f = enemy.Follower;
            int guard = 0;
            while (!(f.From == new GridPos(1, 2) && f.To == new GridPos(2, 2)) && guard++ < 200) battle.Tick();
            Assert.AreEqual(new GridPos(1, 2), f.From);

            Assert.AreEqual(PlacementResult.EnemyInTheWay, board.TryPlaceTower(new GridPos(1, 2), Stats));
            Assert.AreEqual(PlacementResult.EnemyInTheWay, board.TryPlaceTower(new GridPos(2, 2), Stats));
            Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(new GridPos(1, 1), Stats));
            Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(new GridPos(2, 1), Stats));
            Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(new GridPos(1, 3), Stats));
            Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(new GridPos(2, 3), Stats));
        }

        [Test]
        public void S3c_AC5_The_Enemy_Occupies_Its_Step_Only_So_Once_It_Moves_On_The_Cell_It_Left_Is_Free()
        {
            var board = new Board(new GridMap(5, 5, null), new GridPos(0, 0), new GridPos(4, 4));
            Battle battle = ParkedBattle(board);
            Enemy enemy = board.SpawnEnemy(new EnemyStats(2f, 100f));
            PathFollower f = enemy.Follower;
            int guard = 0;
            while (f.From != new GridPos(2, 2) && guard++ < 200) battle.Tick(); // now on (2,2)->(3,3)
            Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(new GridPos(1, 1), Stats), "the cell it left");
            Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(new GridPos(0, 4), Stats));
            Assert.AreEqual(PlacementResult.EnemyInTheWay, board.TryPlaceTower(new GridPos(3, 2), Stats));
        }

        // ---- 3c-AC9 Determinism and the source rule

        [Test]
        public void S3c_AC9_FlowField_Source_Has_No_Float_Or_Double()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "_Game", "Core", "FlowField.cs"));
            Assert.IsTrue(File.Exists(path), path);
            string text = File.ReadAllText(path);
            StringAssert.DoesNotContain("float", text);
            StringAssert.DoesNotContain("double", text);
            StringAssert.DoesNotContain("Single", text);
            StringAssert.DoesNotContain("Mathf", text);
            StringAssert.DoesNotContain("Math.Sqrt", text);
        }

        [Test]
        public void S3c_AC9_The_Same_Purchases_And_Sales_On_The_Same_Ticks_Give_The_Same_Positions_Gold_Lives_And_Outcome()
        {
            var enemy = new EnemyStats(2f, 40f, 5);
            var wave = new[] { new Wave(enemy, 12, 0.8f, 0f), new Wave(enemy, 12, 0.8f, 4f) };
            var tower = new TowerStats(2.5f, 10f, 1.5f, 10f, 20);
            Battle Make() => new Battle(PrototypeBoard(), wave, 20, 100, 50);
            Battle a = Make(), b = Make();

            for (int tick = 0; tick < 2500; tick++)
            {
                foreach (Battle battle in new[] { a, b })
                {
                    if (tick == 40) battle.TryBuyTower(new GridPos(3, 6), tower);
                    if (tick == 200) battle.TryBuyTower(new GridPos(8, 5), tower);
                    if (tick == 300) battle.TryBuyTower(new GridPos(5, 7), tower);
                    if (tick == 900) battle.TrySellTower(new GridPos(3, 6));
                    battle.Tick();
                }
                Assert.AreEqual(a.Gold, b.Gold, $"gold at tick {tick}");
                Assert.AreEqual(a.Lives, b.Lives, $"lives at tick {tick}");
                Assert.AreEqual(a.Outcome, b.Outcome, $"outcome at tick {tick}");
                Assert.AreEqual(a.Board.EnemyCount, b.Board.EnemyCount, $"enemies at tick {tick}");
                for (int i = 0; i < a.Board.EnemyCount; i++)
                {
                    Enemy ea = a.Board.EnemyAt(i), eb = b.Board.EnemyAt(i);
                    Assert.AreEqual(ea.X, eb.X, $"x of enemy {i} at tick {tick}");
                    Assert.AreEqual(ea.Y, eb.Y, $"y of enemy {i} at tick {tick}");
                    Assert.AreEqual(ea.Health, eb.Health, $"health of enemy {i} at tick {tick}");
                    Assert.AreEqual(ea.DistanceToExit, eb.DistanceToExit, $"distance of enemy {i} at tick {tick}");
                }
            }
            Assert.AreNotEqual(RunOutcome.Running, a.Outcome, "the run must end, so that every part was exercised");
        }

        // ---- 3c-AC10 Allocation

        [Test]
        public void S3c_AC10_Tick_With_Enemies_On_Diagonal_And_Straight_Steps_Allocates_Nothing_After_Warmup()
        {
            Board board = PrototypeBoard();
            Battle battle = ParkedBattle(board);
            // Warm-up of the pools: three fast enemies enter and leave, so the free list and the enemy list reach their size.
            for (int i = 0; i < 3; i++) board.SpawnEnemy(new EnemyStats(40f, 100f));
            Ticks(battle, 60);
            Assert.AreEqual(0, board.EnemyCount);
            board.SpawnEnemy(new EnemyStats(2f, 100f));
            Ticks(battle, 100);
            board.SpawnEnemy(new EnemyStats(3f, 100f));
            Ticks(battle, 100);
            board.SpawnEnemy(new EnemyStats(5f, 100f));
            Ticks(battle, 30);

            int diagonalTicks = 0, straightTicks = 0;
            Assert.That(() =>
            {
                for (int i = 0; i < 300; i++)
                {
                    battle.Tick();
                    for (int k = 0; k < board.EnemyCount; k++)
                    {
                        PathFollower f = board.EnemyAt(k).Follower;
                        if (f.From.X != f.To.X && f.From.Y != f.To.Y) diagonalTicks++; else straightTicks++;
                    }
                }
            }, Is.Not.AllocatingGCMemory());

            Assert.Greater(diagonalTicks, 100, "enemies were on diagonal steps");
            Assert.Greater(straightTicks, 100, "and on straight ones");
        }
    }
}
