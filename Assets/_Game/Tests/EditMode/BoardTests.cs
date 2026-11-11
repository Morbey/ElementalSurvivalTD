using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ElementalTD.Core.Tests
{
    public class BoardTests
    {
        private static readonly TowerStats Stats = new TowerStats(2.5f, 10f, 1.5f, 10f);

        private static Enemy AddEnemy(Board board) => board.SpawnEnemy(new EnemyStats(2f, 100f));

        private static Board PrototypeBoard()
        {
            return new Board(new GridMap(16, 9, TestMaps.PrototypeBlocked()), TestMaps.Spawn, TestMaps.Exit);
        }

        // 7x5, spawn (0,2), exit (6,2). Rows y=1..3 are walled for x=1..5: two corridors, y=0 and y=4.
        private static Board TwoCorridorBoard()
        {
            var blocked = new List<GridPos>();
            for (int x = 1; x <= 5; x++)
                for (int y = 1; y <= 3; y++)
                    blocked.Add(new GridPos(x, y));
            return new Board(new GridMap(7, 5, blocked), new GridPos(0, 2), new GridPos(6, 2));
        }

        private static int[] Snapshot(Board board)
        {
            int w = board.Map.Width, h = board.Map.Height;
            var s = new int[w * h * 3];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = new GridPos(x, y);
                    int i = (y * w + x) * 3;
                    s[i] = board.Field.Distance(c);
                    s[i + 1] = board.Map.IsBlocked(c) ? 1 : 0;
                    s[i + 2] = board.HasTower(c) ? 1 : 0;
                }
            return s;
        }

        // One whole cell per call (dt 0.5 at 2 cells/s).
        private static void StepUntil(PathFollower f, Func<PathFollower, bool> done)
        {
            for (int i = 0; i < 200 && !done(f); i++) f.Step(0.5f, 2f);
            Assert.IsTrue(done(f), "follower never reached the wanted state");
        }

        [Test]
        public void S2b_AC1_AC2_Place_Blocks_Cell_And_Changes_Distances_Remove_Restores_Them_Exactly()
        {
            Board board = PrototypeBoard();
            int[] before = Snapshot(board);
            var cell = new GridPos(5, 7); // the gap of wall A: closing it forces the detour through (5,8)

            Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(cell, Stats));
            Assert.IsTrue(board.HasTower(cell));
            Assert.IsTrue(board.Map.IsBlocked(cell));
            Assert.AreEqual(29, board.Field.Distance(TestMaps.Spawn)); // up to (5,8) and back: 2 extra cells
            CollectionAssert.AreNotEqual(before, Snapshot(board));

            Assert.IsTrue(board.TryRemoveTower(cell));
            Assert.IsFalse(board.HasTower(cell));
            Assert.IsFalse(board.Map.IsBlocked(cell));
            CollectionAssert.AreEqual(before, Snapshot(board));
        }

        [Test]
        public void S2b_AC2_Remove_Returns_False_On_Free_Cell_And_On_Wall_And_Wall_Stays_Blocked()
        {
            Board board = PrototypeBoard();
            int[] before = Snapshot(board);
            Assert.IsFalse(board.TryRemoveTower(new GridPos(2, 2)));
            Assert.IsFalse(board.TryRemoveTower(new GridPos(5, 0)));
            Assert.IsFalse(board.TryRemoveTower(new GridPos(99, 99)));
            Assert.IsTrue(board.Map.IsBlocked(new GridPos(5, 0)));
            CollectionAssert.AreEqual(before, Snapshot(board));
        }

        [TestCase("spawn", PlacementResult.SpawnOrExit)]
        [TestCase("exit", PlacementResult.SpawnOrExit)]
        [TestCase("wall", PlacementResult.CellBlocked)]
        [TestCase("tower", PlacementResult.CellBlocked)]
        [TestCase("followerFrom", PlacementResult.EnemyInTheWay)]
        [TestCase("followerTo", PlacementResult.EnemyInTheWay)]
        [TestCase("outOfBounds", PlacementResult.OutOfBounds)]
        public void S2b_AC3_Refusal_Reason_And_Nothing_Changes(string which, PlacementResult expected)
        {
            Board board = PrototypeBoard();
            PathFollower f = AddEnemy(board).Follower;
            f.Step(0.5f, 2f); // From (1,4), To (2,4)
            Assert.AreEqual(new GridPos(1, 4), f.From);
            Assert.AreEqual(new GridPos(2, 4), f.To);
            board.TryPlaceTower(new GridPos(3, 2), Stats);

            GridPos cell;
            switch (which)
            {
                case "spawn": cell = TestMaps.Spawn; break;
                case "exit": cell = TestMaps.Exit; break;
                case "wall": cell = new GridPos(5, 0); break;
                case "tower": cell = new GridPos(3, 2); break;
                case "followerFrom": cell = f.From; break;
                case "followerTo": cell = f.To; break;
                default: cell = new GridPos(16, 0); break;
            }

            int[] before = Snapshot(board);
            Assert.AreEqual(expected, board.TryPlaceTower(cell, Stats));
            CollectionAssert.AreEqual(before, Snapshot(board));
        }

        [Test]
        public void S2b_AC3_Prototype_Layout_Tower_On_5_7_Placed_Then_5_8_WouldBlockPath_And_Nothing_Changes()
        {
            Board board = PrototypeBoard();
            Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(new GridPos(5, 7), Stats));
            int[] before = Snapshot(board);
            Assert.AreEqual(PlacementResult.WouldBlockPath, board.TryPlaceTower(new GridPos(5, 8), Stats));
            CollectionAssert.AreEqual(before, Snapshot(board));
            Assert.IsTrue(board.Field.IsReachable(TestMaps.Spawn));
        }

        [Test]
        public void S2b_AC3_Two_Corridors_Cell_Ahead_Of_Follower_WouldBlockPath_Although_Spawn_Still_Reaches_Exit()
        {
            Board board = TwoCorridorBoard();
            PathFollower f = AddEnemy(board).Follower;
            StepUntil(f, p => p.From == new GridPos(2, 4));
            Assert.AreEqual(new GridPos(3, 4), f.To);

            Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(new GridPos(1, 4), Stats)); // close the entrance behind it
            Assert.IsTrue(board.Field.IsReachable(board.Spawn), "spawn must still reach the exit by the other corridor");
            Assert.IsTrue(board.Field.IsReachable(f.To));

            int[] before = Snapshot(board);
            Assert.AreEqual(PlacementResult.WouldBlockPath, board.TryPlaceTower(new GridPos(4, 4), Stats));
            CollectionAssert.AreEqual(before, Snapshot(board));
            Assert.IsTrue(board.Field.IsReachable(board.Spawn));
        }

        [Test]
        public void S2b_AC3_Priority_SpawnOrExit_Beats_EnemyInTheWay()
        {
            Board board = PrototypeBoard();
            PathFollower f = AddEnemy(board).Follower; // From is the spawn
            Assert.AreEqual(board.Spawn, f.From);
            Assert.AreEqual(PlacementResult.SpawnOrExit, board.TryPlaceTower(board.Spawn, Stats));
        }

        [Test]
        public void S2b_AC3_Priority_EnemyInTheWay_Beats_WouldBlockPath()
        {
            Board board = PrototypeBoard();
            Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(new GridPos(5, 7), Stats));
            PathFollower f = AddEnemy(board).Follower;
            StepUntil(f, p => p.To == new GridPos(5, 8));
            // Blocking (5,8) would also cut the path, but the follower is on it.
            Assert.AreEqual(PlacementResult.EnemyInTheWay, board.TryPlaceTower(new GridPos(5, 8), Stats));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(42)]
        [TestCase(2024)]
        [TestCase(987654)]
        public void S2b_AC5_Seeded_Fuzz_Followers_Never_Stall_Never_Enter_Blocked_And_Arrive(int seed)
        {
            var rnd = new Random(seed);
            Board board = PrototypeBoard();
            PathFollower[] followers = { AddEnemy(board).Follower, AddEnemy(board).Follower };

            for (int i = 0; i < 3000; i++)
            {
                PathFollower near = followers[rnd.Next(2)];
                GridPos cell = rnd.Next(2) == 0
                    ? new GridPos(near.To.X + rnd.Next(-2, 3), near.To.Y + rnd.Next(-2, 3))
                    : new GridPos(rnd.Next(-1, 17), rnd.Next(-1, 10));
                if (board.HasTower(cell)) { if (rnd.Next(2) == 0) board.TryRemoveTower(cell); }
                else board.TryPlaceTower(cell, Stats);

                for (int k = 0; k < followers.Length; k++)
                {
                    if (k == 1 && i % 2 == 1) continue;
                    PathFollower f = followers[k];
                    if (f.Step((float)(0.02 + rnd.NextDouble() * 0.6), 2f)) f.Restart(board.Spawn);
                }

                foreach (PathFollower f in followers)
                {
                    Assert.IsFalse(board.Map.IsBlocked(f.From), $"seed {seed} i {i}: From {f.From} blocked");
                    Assert.IsFalse(board.Map.IsBlocked(f.To), $"seed {seed} i {i}: To {f.To} blocked");
                    Assert.IsTrue(board.Field.IsReachable(f.To), $"seed {seed} i {i}: To {f.To} unreachable");
                    Assert.IsFalse(f.From == f.To && !f.HasArrived, $"seed {seed} i {i}: follower stalled at {f.From}");
                }
            }

            foreach (PathFollower f in followers)
            {
                int budget = board.Field.Distance(f.To) + 2;
                int calls = 0;
                while (!f.Step(0.5f, 2f) && calls++ < budget) { }
                Assert.IsTrue(f.HasArrived, $"seed {seed}: did not arrive within {budget} cells");
                Assert.AreEqual(board.Exit, f.From);
            }
        }

        [Test]
        public void S2b_AC9_Board_Constructor_Throws_For_OutOfBounds_Spawn_Or_Exit()
        {
            var map = new GridMap(4, 4, null);
            Assert.Throws<ArgumentOutOfRangeException>(() => new Board(map, new GridPos(4, 0), new GridPos(1, 1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Board(map, new GridPos(0, 0), new GridPos(1, -1)));
        }
    }
}
