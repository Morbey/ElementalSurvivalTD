using System.Collections.Generic;
using NUnit.Framework;

namespace ElementalTD.Core.Tests
{
    public class NoReversalTests
    {
        private static readonly TowerStats Stats = new TowerStats(2.5f, 10f, 1.5f, 10f);

        private static Board PrototypeBoard() =>
            new Board(new GridMap(16, 9, TestMaps.PrototypeBlocked()), TestMaps.Spawn, TestMaps.Exit);

        private static void StepUntil(PathFollower f, System.Func<PathFollower, bool> done)
        {
            for (int i = 0; i < 4000 && !done(f); i++) f.Step(1f / 60f, 2f);
            Assert.IsTrue(done(f), "follower never reached the wanted state");
        }

        // Open 5x5, exit (4,4), wall on (1,0).
        private static FlowField WalledField()
        {
            var map = new GridMap(5, 5, new[] { new GridPos(1, 0) });
            var field = new FlowField(map);
            field.Compute(new GridPos(4, 4));
            return field;
        }

        [Test]
        public void S2c_AC16_On_A_Tie_The_Follower_Goes_Straight_Ahead()
        {
            // Open 5x5, exit (4,4), wall (1,0): from (0,0) it must go up; at (0,1) (0,2) and (1,1) are equally short.
            FlowField field = WalledField();
            var f = new PathFollower(field, new GridPos(0, 0));
            Assert.AreEqual(new GridPos(0, 1), f.To);

            StepUntil(f, p => p.From == new GridPos(0, 1));

            Assert.AreEqual(new GridPos(0, 2), f.To);
        }

        [Test]
        public void S2c_AC16_With_No_Last_Move_The_Fixed_Order_Applies()
        {
            FlowField field = WalledField();

            var f = new PathFollower(field, new GridPos(0, 1));

            Assert.AreEqual(new GridPos(1, 1), f.To, "+x comes first");
        }

        // The follower walks (before) -> (corner); a tower is placed while it heads for the corner; at the corner it goes to (expected).
        [TestCase(3, 4, 4, 4, 4, 6, 4, 5)]
        [TestCase(3, 4, 4, 4, 4, 7, 4, 5)]
        [TestCase(8, 7, 9, 7, 9, 3, 9, 6)]
        [TestCase(14, 1, 15, 1, 15, 3, 15, 2)]
        [TestCase(3, 4, 4, 4, 4, 5, 3, 4)] // strictly shorter to go back: it turns
        [TestCase(8, 7, 9, 7, 9, 6, 8, 7)]
        [TestCase(14, 1, 15, 1, 15, 2, 14, 1)]
        public void S2c_AC16_A_Turn_Rather_Than_Back_On_A_Tie_And_Back_Only_When_Strictly_Shortest(
            int bx, int by, int cx, int cy, int tx, int ty, int ex, int ey)
        {
            Board board = PrototypeBoard();
            var enemy = new Enemy(board.Field, board.Spawn, new EnemyStats(2f, 100f));
            board.AddEnemy(enemy);
            PathFollower f = enemy.Follower;
            var before = new GridPos(bx, by);
            var corner = new GridPos(cx, cy);
            StepUntil(f, p => p.From == before && p.To == corner);

            Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(new GridPos(tx, ty), Stats));
            StepUntil(f, p => p.From == corner);

            Assert.AreEqual(new GridPos(ex, ey), f.To);
        }

        [Test]
        public void S2c_AC16_The_Default_Route_Is_Unchanged_27_Steps_Through_The_Corners()
        {
            Board board = PrototypeBoard();
            var enemy = new Enemy(board.Field, board.Spawn, new EnemyStats(2f, 100f));
            board.AddEnemy(enemy);
            PathFollower f = enemy.Follower;
            var visited = new List<GridPos> { f.From };
            for (int i = 0; i < 4000 && !f.HasArrived; i++)
            {
                f.Step(1f / 60f, 2f);
                if (f.From != visited[visited.Count - 1]) visited.Add(f.From);
            }

            Assert.AreEqual(28, visited.Count, "27 steps");
            foreach (var corner in new[] { new GridPos(4, 4), new GridPos(4, 7), new GridPos(9, 7), new GridPos(9, 1), new GridPos(15, 1) })
                CollectionAssert.Contains(visited, corner);
            Assert.Less(visited.IndexOf(new GridPos(4, 4)), visited.IndexOf(new GridPos(4, 7)));
            Assert.Less(visited.IndexOf(new GridPos(4, 7)), visited.IndexOf(new GridPos(9, 7)));
            Assert.Less(visited.IndexOf(new GridPos(9, 7)), visited.IndexOf(new GridPos(9, 1)));
            Assert.Less(visited.IndexOf(new GridPos(9, 1)), visited.IndexOf(new GridPos(15, 1)));
            Assert.AreEqual(new GridPos(15, 4), visited[visited.Count - 1]);
        }
    }
}
