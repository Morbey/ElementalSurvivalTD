using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace ElementalTD.Core.Tests
{
    /// <summary>Step 3c: eight directions, no corner cutting and the tie-break (3c-AC1 to AC3), judged against references written here.</summary>
    public class DiagonalFlowFieldTests
    {
        private static readonly TowerStats Stats = new TowerStats(2.5f, 10f, 1.5f, 10f);
        private static readonly int[] Dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
        private static readonly int[] Dy = { 0, 1, 1, 1, 0, -1, -1, -1 };

        // ---- helpers: a random board and independent references

        private static GridMap RandomMap(int seed, out GridPos exit)
        {
            var rnd = new Random(seed);
            int density = rnd.Next(0, 45);
            exit = new GridPos(rnd.Next(16), rnd.Next(9));
            var blocked = new List<GridPos>();
            for (int y = 0; y < 9; y++)
                for (int x = 0; x < 16; x++)
                    if (rnd.Next(100) < density && !(x == exit.X && y == exit.Y)) blocked.Add(new GridPos(x, y));
            return new GridMap(16, 9, blocked);
        }

        private static bool Free(GridMap map, int x, int y) => map.InBounds(new GridPos(x, y)) && !map.IsBlocked(new GridPos(x, y));

        /// <summary>The rule of 3c-AC2, written again here: the target is free and, on a diagonal, so are both cells it passes between.</summary>
        private static bool Legal(GridMap map, int x, int y, int dx, int dy)
        {
            if (!Free(map, x, y) || !Free(map, x + dx, y + dy)) return false;
            return dx == 0 || dy == 0 || (Free(map, x + dx, y) && Free(map, x, y + dy));
        }

        /// <summary>Bellman-Ford with doubles and the true square root of 2, to a fixed point.</summary>
        private static double[] ReferenceDistances(GridMap map, GridPos exit)
        {
            int w = map.Width, h = map.Height;
            var d = new double[w * h];
            for (int i = 0; i < d.Length; i++) d[i] = double.PositiveInfinity;
            d[exit.Y * w + exit.X] = 0;
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        for (int k = 0; k < 8; k++)
                        {
                            if (!Legal(map, x, y, Dx[k], Dy[k])) continue;
                            double through = d[(y + Dy[k]) * w + x + Dx[k]] + (Dx[k] != 0 && Dy[k] != 0 ? Math.Sqrt(2) : 1.0);
                            if (through < d[y * w + x] - 1e-12) { d[y * w + x] = through; changed = true; }
                        }
            }
            return d;
        }

        private static bool[] FourNeighbourFloodFill(GridMap map, GridPos exit)
        {
            var seen = new bool[map.Width * map.Height];
            var stack = new Stack<GridPos>();
            seen[exit.Y * map.Width + exit.X] = true;
            stack.Push(exit);
            while (stack.Count > 0)
            {
                GridPos c = stack.Pop();
                for (int k = 0; k < 8; k += 2)
                {
                    int nx = c.X + Dx[k], ny = c.Y + Dy[k];
                    if (!Free(map, nx, ny) || seen[ny * map.Width + nx]) continue;
                    seen[ny * map.Width + nx] = true;
                    stack.Push(new GridPos(nx, ny));
                }
            }
            return seen;
        }

        private static Board OpenBoard(int w, int h, GridPos spawn, GridPos exit) => new Board(new GridMap(w, h, null), spawn, exit);

        private static Board BoardWith(int w, int h, GridPos spawn, GridPos exit, params GridPos[] walls) =>
            new Board(new GridMap(w, h, walls), spawn, exit);

        private static void Place(Board board, int x, int y) =>
            Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(new GridPos(x, y), Stats), $"tower on {x},{y}");

        // ---- 3c-AC1

        [Test]
        public void S3c_AC1_200_Random_Boards_Distances_Match_A_Double_Reference_And_Unreachable_Cells_Agree()
        {
            int unreachableSeen = 0, reachableSeen = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                GridMap map = RandomMap(seed, out GridPos exit);
                var field = new FlowField(map);
                field.Compute(exit);
                double[] reference = ReferenceDistances(map, exit);
                for (int y = 0; y < map.Height; y++)
                    for (int x = 0; x < map.Width; x++)
                    {
                        double r = reference[y * map.Width + x];
                        int actual = field.Distance(new GridPos(x, y));
                        if (double.IsInfinity(r))
                        {
                            Assert.AreEqual(FlowField.Unreachable, actual, $"seed {seed} cell {x},{y} should be unreachable");
                            Assert.IsFalse(field.IsReachable(new GridPos(x, y)));
                            unreachableSeen++;
                        }
                        else
                        {
                            Assert.AreNotEqual(FlowField.Unreachable, actual, $"seed {seed} cell {x},{y} should be reachable");
                            Assert.AreEqual(r, actual / 408.0, 0.001, $"seed {seed} cell {x},{y}");
                            reachableSeen++;
                        }
                    }
            }
            Assert.Greater(unreachableSeen, 100, "the random boards must include walled-off cells");
            Assert.Greater(reachableSeen, 10000);
        }

        [Test]
        public void S3c_AC1_Following_TryGetNext_The_Step_Costs_Add_Up_To_The_Distance_And_Every_Step_Is_Legal()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                GridMap map = RandomMap(seed, out GridPos exit);
                var field = new FlowField(map);
                field.Compute(exit);
                for (int y = 0; y < map.Height; y++)
                    for (int x = 0; x < map.Width; x++)
                    {
                        var start = new GridPos(x, y);
                        if (map.IsBlocked(start)) continue;
                        if (!field.IsReachable(start))
                        {
                            Assert.IsFalse(field.TryGetNext(start, out _), $"seed {seed} {start}: unreachable cell must have no next");
                            continue;
                        }
                        GridPos cur = start, came = start;
                        int sum = 0, guard = 0;
                        while (field.TryGetNext(cur, came, out GridPos next))
                        {
                            int dx = next.X - cur.X, dy = next.Y - cur.Y;
                            Assert.IsTrue(Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1 && (dx != 0 || dy != 0), $"seed {seed}: bad step {cur}->{next}");
                            Assert.IsTrue(Legal(map, cur.X, cur.Y, dx, dy), $"seed {seed}: illegal step {cur}->{next}");
                            sum += dx != 0 && dy != 0 ? 577 : 408;
                            came = cur;
                            cur = next;
                            Assert.Less(++guard, 200, $"seed {seed}: route from {start} does not end");
                        }
                        Assert.AreEqual(exit, cur, $"seed {seed}: route from {start} ends off the exit");
                        Assert.AreEqual(field.Distance(start), sum, $"seed {seed}: cost of the route from {start}");
                    }
            }
        }

        [Test]
        public void S3c_AC1_A_One_Cell_Board_And_A_Blocked_Exit()
        {
            var one = new FlowField(new GridMap(1, 1, null));
            one.Compute(new GridPos(0, 0));
            Assert.AreEqual(0, one.Distance(new GridPos(0, 0)));
            Assert.IsFalse(one.TryGetNext(new GridPos(0, 0), out _));
        }

        // ---- 3c-AC2

        [Test]
        public void S3c_AC2_Wall_On_The_Edge_Cell_Forces_Two_Straight_Steps_Round_Its_End()
        {
            // 3x2, wall on (1,0): (0,0) to the exit (2,0) goes (0,1) (1,1) (2,1) (2,0): 4 straight steps.
            var map = new GridMap(3, 2, new[] { new GridPos(1, 0) });
            var field = new FlowField(map);
            field.Compute(new GridPos(2, 0));
            Assert.AreEqual(4 * 408, field.Distance(new GridPos(0, 0)));
            Assert.AreEqual(1632, field.Distance(new GridPos(0, 0)));
            AssertRoute(field, new GridPos(0, 0), new GridPos(0, 1), new GridPos(1, 1), new GridPos(2, 1), new GridPos(2, 0));
        }

        [Test]
        public void S3c_AC2_Tower_Instead_Of_The_Wall_Gives_The_Same_Result()
        {
            Board board = OpenBoard(3, 2, new GridPos(0, 0), new GridPos(2, 0));
            Place(board, 1, 0);
            Assert.AreEqual(1632, board.Field.Distance(board.Spawn));
            AssertRoute(board.Field, board.Spawn, new GridPos(0, 1), new GridPos(1, 1), new GridPos(2, 1), new GridPos(2, 0));
        }

        [TestCase(1, 0)]
        [TestCase(0, 1)]
        public void S3c_AC2_At_The_Board_Edge_A_Blocked_Side_Cell_Stops_The_Diagonal_On_A_2x2(int wallX, int wallY)
        {
            // 2x2, spawn (0,0), exit (1,1); one side cell of the diagonal is a wall: two straight steps, never the diagonal.
            var map = new GridMap(2, 2, new[] { new GridPos(wallX, wallY) });
            var field = new FlowField(map);
            field.Compute(new GridPos(1, 1));
            Assert.AreEqual(816, field.Distance(new GridPos(0, 0)));
            Assert.IsTrue(field.TryGetNext(new GridPos(0, 0), out GridPos next));
            Assert.AreEqual(new GridPos(1 - wallX, 1 - wallY), next);
        }

        [Test]
        public void S3c_AC2_Towers_Touching_By_A_Corner_Let_Nothing_Through_On_An_Open_4x4()
        {
            Board board = OpenBoard(4, 4, new GridPos(0, 0), new GridPos(3, 3));
            Place(board, 0, 3);
            Place(board, 1, 2);
            Place(board, 2, 1);
            // The way left is round the end of the line, through (3,0): 6 straight steps.
            Assert.AreEqual(2448, board.Field.Distance(board.Spawn));
            AssertRoute(board.Field, board.Spawn, new GridPos(1, 0), new GridPos(2, 0), new GridPos(3, 0), new GridPos(3, 1), new GridPos(3, 2), new GridPos(3, 3));

            int[] before = Snapshot(board);
            Assert.AreEqual(PlacementResult.WouldBlockPath, board.TryPlaceTower(new GridPos(3, 0), Stats));
            CollectionAssert.AreEqual(before, Snapshot(board));
            Assert.IsFalse(board.HasTower(new GridPos(3, 0)));
        }

        [TestCase(4, 8, 5, 7)]
        [TestCase(5, 7, 4, 8)]
        [TestCase(5, 7, 6, 8)]
        [TestCase(6, 8, 5, 7)]
        public void S3c_AC2_On_The_Prototype_Layout_Two_Towers_Touching_By_A_Corner_Close_The_Gap(int ax, int ay, int bx, int by)
        {
            var board = new Board(new GridMap(16, 9, TestMaps.PrototypeBlocked()), TestMaps.Spawn, TestMaps.Exit);
            Place(board, ax, ay);
            int[] before = Snapshot(board);
            Assert.AreEqual(PlacementResult.WouldBlockPath, board.TryPlaceTower(new GridPos(bx, by), Stats));
            CollectionAssert.AreEqual(before, Snapshot(board));
            Assert.IsTrue(board.Field.IsReachable(board.Spawn));
        }

        [Test]
        public void S3c_AC2_L_Shaped_Gap_Is_Passed_In_One_Straight_One_Diagonal_And_One_Straight_Step()
        {
            // 3x3, walls (1,0) and (2,1): (0,1) then the diagonal to (1,2) then (2,2): 408 + 577 + 408.
            var map = new GridMap(3, 3, new[] { new GridPos(1, 0), new GridPos(2, 1) });
            var field = new FlowField(map);
            field.Compute(new GridPos(2, 2));
            Assert.AreEqual(1393, field.Distance(new GridPos(0, 0)));
            AssertRoute(field, new GridPos(0, 0), new GridPos(0, 1), new GridPos(1, 2), new GridPos(2, 2));
        }

        [Test]
        public void S3c_AC2_On_200_Random_Boards_A_Cell_Reaches_The_Exit_Exactly_When_A_Four_Neighbour_Flood_Fill_Does()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                GridMap map = RandomMap(seed, out GridPos exit);
                var field = new FlowField(map);
                field.Compute(exit);
                bool[] reach = FourNeighbourFloodFill(map, exit);
                for (int y = 0; y < map.Height; y++)
                    for (int x = 0; x < map.Width; x++)
                    {
                        if (map.IsBlocked(new GridPos(x, y))) continue;
                        Assert.AreEqual(reach[y * map.Width + x], field.IsReachable(new GridPos(x, y)), $"seed {seed} cell {x},{y}");
                    }
            }
        }

        // ---- 3c-AC3

        // Exit, from, came from, tower (-1 for none), expected next. Open 5x5.
        [TestCase(4, 1, 1, 0, 0, 0, -1, -1, 2, 0, TestName = "Straight ahead on a tie")]
        [TestCase(4, 3, 1, 1, 0, 0, -1, -1, 2, 2, TestName = "A diagonal heading also goes on")]
        [TestCase(4, 3, 2, 2, 2, 1, -1, -1, 3, 3, TestName = "The smaller turn")]
        [TestCase(4, 2, 1, 2, 0, 2, 2, 2, 1, 3, TestName = "Left before right, heading +x")]
        [TestCase(0, 2, 3, 2, 4, 2, 2, 2, 3, 1, TestName = "Left before right, heading -x")]
        [TestCase(2, 4, 2, 1, 2, 0, 2, 2, 1, 1, TestName = "Left before right, heading +y")]
        [TestCase(2, 0, 2, 3, 2, 4, 2, 2, 3, 3, TestName = "Left before right, heading -y")]
        [TestCase(4, 2, 1, 2, 0, 2, 3, 2, 2, 3, TestName = "Left before right, 45 degrees, ahead free but not shortest")]
        [TestCase(4, 3, 2, 2, 3, 3, -1, -1, 3, 2, TestName = "A turn rather than back")]
        [TestCase(2, 0, 2, 2, 2, 1, -1, -1, 2, 1, TestName = "Back when strictly shortest")]
        public void S3c_AC3_Tie_Break_Table(int exitX, int exitY, int fromX, int fromY, int camX, int camY, int towerX, int towerY, int nextX, int nextY)
        {
            Board board = towerX < 0
                ? OpenBoard(5, 5, new GridPos(0, 0), new GridPos(exitX, exitY))
                : BoardWith(5, 5, new GridPos(0, 0), new GridPos(exitX, exitY), new GridPos(towerX, towerY));
            Assert.IsTrue(board.Field.TryGetNext(new GridPos(fromX, fromY), new GridPos(camX, camY), out GridPos next));
            Assert.AreEqual(new GridPos(nextX, nextY), next);
        }

        [Test]
        public void S3c_AC3_Both_Left_Turns_Come_Before_45_Degrees_Right()
        {
            // Walls (2,2) and (3,1), exit (4,2); from (1,1) heading +x: ahead is not shortest, 45 left is a wall,
            // 45 right (2,0) and 90 left (1,2) both cost 2209. Hand count: (2,0) 1632 + 577; (1,2) 1801 + 408.
            FlowField field = BoardWith(5, 5, new GridPos(0, 0), new GridPos(4, 2), new GridPos(2, 2), new GridPos(3, 1)).Field;
            Assert.AreEqual(2209, field.Distance(new GridPos(1, 1)));
            Assert.AreEqual(1632, field.Distance(new GridPos(2, 0)));
            Assert.AreEqual(1801, field.Distance(new GridPos(1, 2)));
            Assert.IsTrue(field.TryGetNext(new GridPos(1, 1), new GridPos(0, 1), out GridPos next));
            Assert.AreEqual(new GridPos(2, 0), next);
        }

        [Test]
        public void S3c_AC3_135_Degrees_Right_Comes_Before_Back()
        {
            // Open 5x5, exit (0,0); from (2,1) heading +x: (1,0) is 135 right, (1,1) is back; both cost 985 in total.
            FlowField field = OpenBoard(5, 5, new GridPos(0, 0), new GridPos(0, 0)).Field;
            Assert.AreEqual(985, field.Distance(new GridPos(2, 1)));
            Assert.IsTrue(field.TryGetNext(new GridPos(2, 1), new GridPos(1, 1), out GridPos next));
            Assert.AreEqual(new GridPos(1, 0), next);
        }

        [Test]
        public void S3c_AC3_135_Degrees_Left_Comes_Before_135_Degrees_Right()
        {
            // Wall (1,1), exit (0,1); from (3,1) heading +x: (2,2) is 135 left, (2,0) is 135 right; both lead round the wall for 1801.
            FlowField field = BoardWith(5, 5, new GridPos(4, 4), new GridPos(0, 1), new GridPos(1, 1)).Field;
            Assert.AreEqual(1801, field.Distance(new GridPos(3, 1)));
            Assert.IsTrue(field.TryGetNext(new GridPos(3, 1), new GridPos(2, 1), out GridPos next));
            Assert.AreEqual(new GridPos(2, 2), next);
        }

        [Test]
        public void S3c_AC2_TryGetNext_Never_Takes_The_Diagonal_Past_A_Wall_Even_When_It_Ties_With_The_Legal_Step()
        {
            // 3x5, walls (2,0) and (1,2), exit (1,4); from (1,0) heading +x. The diagonal to (2,1) would cost 577 + 1393 = 1970,
            // the same as the legal diagonal to (0,1), but it cuts the corner of the wall (2,0).
            FlowField field = BoardWith(3, 5, new GridPos(0, 0), new GridPos(1, 4), new GridPos(2, 0), new GridPos(1, 2)).Field;
            Assert.AreEqual(1970, field.Distance(new GridPos(1, 0)));
            Assert.AreEqual(1393, field.Distance(new GridPos(2, 1)), "the cell the illegal step would reach");
            Assert.IsTrue(field.TryGetNext(new GridPos(1, 0), new GridPos(0, 0), out GridPos next));
            Assert.AreEqual(new GridPos(0, 1), next);
        }

        [Test]
        public void S3c_AC3_The_Left_Before_Right_Row_Is_Not_Symmetric_Heading_Plus_X_Goes_To_Larger_Y_And_Minus_X_To_Smaller_Y()
        {
            // The two rows that a mirrored table would swap: tower on (2,2), exit straight behind it.
            FlowField plusX = BoardWith(5, 5, new GridPos(0, 0), new GridPos(4, 2), new GridPos(2, 2)).Field;
            plusX.TryGetNext(new GridPos(1, 2), new GridPos(0, 2), out GridPos a);
            FlowField minusX = BoardWith(5, 5, new GridPos(0, 0), new GridPos(0, 2), new GridPos(2, 2)).Field;
            minusX.TryGetNext(new GridPos(3, 2), new GridPos(4, 2), out GridPos b);
            Assert.AreEqual(3, a.Y);
            Assert.AreEqual(1, b.Y);
        }

        [Test]
        public void S3c_AC3_No_Last_Step_Counts_As_Heading_Plus_X_Straight_Ahead_On_A_Tie()
        {
            FlowField field = OpenBoard(5, 5, new GridPos(0, 0), new GridPos(4, 1)).Field;
            Assert.IsTrue(field.TryGetNext(new GridPos(0, 0), out GridPos next));
            Assert.AreEqual(new GridPos(1, 0), next);
            // The same through a follower that enters at the cell.
            Assert.AreEqual(new GridPos(1, 0), new PathFollower(field, new GridPos(0, 0)).To);
        }

        [Test]
        public void S3c_AC3_No_Last_Step_With_A_Tower_Ahead_Goes_45_Degrees_Left_Not_Right()
        {
            FlowField field = BoardWith(5, 5, new GridPos(0, 2), new GridPos(4, 2), new GridPos(2, 2)).Field;
            Assert.IsTrue(field.TryGetNext(new GridPos(0, 2), out GridPos next));
            Assert.AreEqual(new GridPos(1, 3), next);
            var follower = new PathFollower(field, new GridPos(0, 2));
            Assert.AreEqual(new GridPos(1, 3), follower.To);
            Assert.AreEqual(577f / 408f, follower.StepLength);
        }

        [Test]
        public void S3c_AC3_A_Follower_From_00_To_The_Exit_On_41_Passes_10_20_And_30()
        {
            Board board = OpenBoard(5, 5, new GridPos(0, 0), new GridPos(4, 1));
            var f = new PathFollower(board.Field, board.Spawn);
            var visited = new List<GridPos> { f.From };
            for (int i = 0; i < 400 && !f.HasArrived; i++)
            {
                f.Step(0.1f, 2f);
                if (f.From != visited[visited.Count - 1]) visited.Add(f.From);
            }
            CollectionAssert.AreEqual(new[] { new GridPos(0, 0), new GridPos(1, 0), new GridPos(2, 0), new GridPos(3, 0), new GridPos(4, 1) }, visited);
        }

        [Test]
        public void S3c_AC3_Two_Corridors_A_Tower_Ahead_Of_A_Follower_Makes_It_Turn_Back_On_Its_Next_Cell()
        {
            // 7x5, spawn (0,2), exit (6,2), cells x 1..5, y 1..3 walled: a follower on (2,4) heading for (3,4).
            var blocked = new List<GridPos>();
            for (int x = 1; x <= 5; x++)
                for (int y = 1; y <= 3; y++)
                    blocked.Add(new GridPos(x, y));
            var board = new Board(new GridMap(7, 5, blocked), new GridPos(0, 2), new GridPos(6, 2));
            PathFollower f = board.SpawnEnemy(new EnemyStats(2f, 100f)).Follower;
            for (int i = 0; i < 200 && !(f.From == new GridPos(2, 4) && f.To == new GridPos(3, 4)); i++) f.Step(0.5f, 2f);
            Assert.AreEqual(new GridPos(2, 4), f.From);
            Assert.AreEqual(new GridPos(3, 4), f.To);

            Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(new GridPos(4, 4), Stats));
            for (int i = 0; i < 200 && f.From != new GridPos(3, 4); i++) f.Step(0.5f, 2f);

            Assert.AreEqual(new GridPos(3, 4), f.From);
            Assert.AreEqual(new GridPos(2, 4), f.To, "back is the only way, so it turns back");
        }

        /// <summary>
        /// The order of 3c-AC3 written as angles, apart from the table in the code: the smallest turn first, left (counter-clockwise
        /// from +x towards +y) before right, back last. Every cell, every incoming heading, 200 random boards.
        /// </summary>
        [Test]
        public void S3c_AC3_Property_The_Step_Is_The_Smallest_Turn_Left_Before_Right_Among_The_Shortest_Legal_Steps()
        {
            int checkedCells = 0, backTaken = 0, ties = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                GridMap map = RandomMap(seed, out GridPos exit);
                var field = new FlowField(map);
                field.Compute(exit);
                for (int y = 0; y < map.Height; y++)
                    for (int x = 0; x < map.Width; x++)
                    {
                        var from = new GridPos(x, y);
                        if (map.IsBlocked(from) || !field.IsReachable(from) || from == exit) continue;
                        for (int h = -1; h < 8; h++)
                        {
                            // h == -1: no last step, which counts as heading +x.
                            int hx = h < 0 ? 1 : Dx[h], hy = h < 0 ? 0 : Dy[h];
                            var came = h < 0 ? from : new GridPos(x - hx, y - hy);
                            if (!map.InBounds(came)) continue;

                            int bestK = -1;
                            double bestKey = double.MaxValue;
                            int candidates = 0;
                            for (int k = 0; k < 8; k++)
                            {
                                if (!Legal(map, x, y, Dx[k], Dy[k])) continue;
                                var t = new GridPos(x + Dx[k], y + Dy[k]);
                                int cost = Dx[k] != 0 && Dy[k] != 0 ? 577 : 408;
                                if (!field.IsReachable(t) || field.Distance(t) + cost != field.Distance(from)) continue;
                                candidates++;
                                double angle = Math.Atan2(hx * Dy[k] - hy * Dx[k], hx * Dx[k] + hy * Dy[k]) * 180.0 / Math.PI; // positive = left
                                double key = Math.Abs(angle) * 10 + (angle > 0 ? 0 : 1); // smaller turn first, left first on equal turns
                                if (key < bestKey) { bestKey = key; bestK = k; }
                            }
                            Assert.GreaterOrEqual(candidates, 1, $"seed {seed} {from}: a reachable cell must have a shortest step");
                            Assert.IsTrue(field.TryGetNext(from, came, out GridPos next), $"seed {seed} {from} heading {h}");
                            Assert.AreEqual(new GridPos(x + Dx[bestK], y + Dy[bestK]), next, $"seed {seed} {from} heading {h}");
                            checkedCells++;
                            if (candidates > 1) ties++;
                            if (next == new GridPos(x - hx, y - hy) && h >= 0) backTaken++;
                        }
                    }
            }
            Assert.Greater(checkedCells, 50000);
            Assert.Greater(ties, 10000, "the boards must contain many ties for the order to matter");
            Assert.Greater(backTaken, 0, "some cell must have back as the only shortest step");
        }

        // ---- 3c-AC9, AC10

        [Test]
        public void S3c_AC9_A_Field_Computed_Twice_And_After_A_Tower_Is_Placed_And_Removed_Is_Equal_Cell_By_Cell()
        {
            var board = new Board(new GridMap(16, 9, TestMaps.PrototypeBlocked()), TestMaps.Spawn, TestMaps.Exit);
            int[] first = Snapshot(board);
            board.Field.Compute(board.Exit);
            CollectionAssert.AreEqual(first, Snapshot(board));

            Place(board, 7, 6);
            Place(board, 12, 3);
            Assert.IsTrue(board.TryRemoveTower(new GridPos(7, 6)));
            Assert.IsTrue(board.TryRemoveTower(new GridPos(12, 3)));
            CollectionAssert.AreEqual(first, Snapshot(board));
        }

        [Test]
        public void S3c_AC9_The_Field_Does_Not_Depend_On_The_Order_Towers_Were_Placed_In()
        {
            var cells = new[] { new GridPos(3, 5), new GridPos(7, 3), new GridPos(8, 6), new GridPos(12, 5), new GridPos(2, 2) };
            var a = new Board(new GridMap(16, 9, TestMaps.PrototypeBlocked()), TestMaps.Spawn, TestMaps.Exit);
            var b = new Board(new GridMap(16, 9, TestMaps.PrototypeBlocked()), TestMaps.Spawn, TestMaps.Exit);
            foreach (GridPos c in cells) Place(a, c.X, c.Y);
            for (int i = cells.Length - 1; i >= 0; i--) Place(b, cells[i].X, cells[i].Y);
            CollectionAssert.AreEqual(Snapshot(a), Snapshot(b));
        }

        [Test]
        public void S3c_AC10_Compute_And_TryGetNext_With_A_Heading_Allocate_Nothing_After_Warmup()
        {
            var board = new Board(new GridMap(16, 9, TestMaps.PrototypeBlocked()), TestMaps.Spawn, TestMaps.Exit);
            FlowField field = board.Field;
            field.Compute(board.Exit);
            field.TryGetNext(new GridPos(1, 4), new GridPos(0, 4), out _);

            Assert.That(() => { field.Compute(board.Exit); }, Is.Not.AllocatingGCMemory());
            Assert.That(() =>
            {
                field.TryGetNext(new GridPos(1, 4), new GridPos(0, 4), out _);
                field.TryGetNext(new GridPos(2, 5), new GridPos(1, 4), out _);
                field.TryGetNext(new GridPos(4, 7), new GridPos(3, 6), out _);
            }, Is.Not.AllocatingGCMemory());
        }

        // ---- helpers

        private static void AssertRoute(FlowField field, GridPos start, params GridPos[] expected)
        {
            var route = new List<GridPos>(); // the cells after the start
            GridPos cur = start, came = start;
            while (field.TryGetNext(cur, came, out GridPos next))
            {
                route.Add(next);
                came = cur;
                cur = next;
                Assert.Less(route.Count, 100);
            }
            CollectionAssert.AreEqual(expected, route);
        }

        private static int[] Snapshot(Board board)
        {
            int w = board.Map.Width, h = board.Map.Height;
            var s = new int[w * h * 2];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    s[(y * w + x) * 2] = board.Field.Distance(new GridPos(x, y));
                    s[(y * w + x) * 2 + 1] = board.Map.IsBlocked(new GridPos(x, y)) ? 1 : 0;
                }
            return s;
        }
    }
}
