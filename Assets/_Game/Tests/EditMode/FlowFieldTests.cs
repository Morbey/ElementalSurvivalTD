using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ElementalTD.Core.Tests
{
    public class FlowFieldTests
    {
        [Test]
        public void S3c_AC1_Open_Board_Distance_Is_408_Times_Max_Minus_Min_Plus_577_Times_Min_ForEveryCell()
        {
            var exit = new GridPos(3, 2);
            FlowField field = TestMaps.OpenField(7, 5, exit, out GridMap map);
            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 7; x++)
                {
                    int a = Math.Abs(x - exit.X), b = Math.Abs(y - exit.Y);
                    int expected = 408 * (Math.Max(a, b) - Math.Min(a, b)) + 577 * Math.Min(a, b);
                    Assert.AreEqual(expected, field.Distance(new GridPos(x, y)), $"cell {x},{y}");
                }
        }

        [Test]
        public void S3c_AC1_Open_Board_Of_400_Cells_Follows_The_Formula_From_A_Corner_Exit()
        {
            FlowField field = TestMaps.OpenField(20, 20, new GridPos(0, 0), out _);
            for (int y = 0; y < 20; y++)
                for (int x = 0; x < 20; x++)
                    Assert.AreEqual(408 * (Math.Max(x, y) - Math.Min(x, y)) + 577 * Math.Min(x, y), field.Distance(new GridPos(x, y)), $"cell {x},{y}");
        }

        [Test]
        public void S3c_AC4_PrototypeLayout_Distance_Spawn_Is_8865()
        {
            // Hand count of the route below: 9 straight steps (408) and 9 diagonal steps (577) = 3672 + 5193.
            FlowField field = TestMaps.PrototypeField(out _);
            Assert.AreEqual(8865, field.Distance(TestMaps.Spawn));
        }

        [Test]
        public void S3c_AC1_PrototypeLayout_Route_Is_The_Diagonal_Route_And_Legal()
        {
            FlowField field = TestMaps.PrototypeField(out GridMap map);
            var expected = new[]
            {
                new GridPos(0, 4), new GridPos(1, 4), new GridPos(2, 5), new GridPos(3, 6), new GridPos(4, 7), new GridPos(5, 7),
                new GridPos(6, 7), new GridPos(7, 6), new GridPos(8, 5), new GridPos(9, 4), new GridPos(9, 3), new GridPos(9, 2),
                new GridPos(9, 1), new GridPos(10, 1), new GridPos(11, 1), new GridPos(12, 1), new GridPos(13, 2), new GridPos(14, 3),
                new GridPos(15, 4),
            };

            var route = new List<GridPos> { TestMaps.Spawn };
            GridPos cur = TestMaps.Spawn;
            int cost = 0;
            while (field.TryGetNext(cur, out GridPos next))
            {
                Assert.IsFalse(map.IsBlocked(next), $"entered blocked {next}");
                int dx = Math.Abs(next.X - cur.X), dy = Math.Abs(next.Y - cur.Y);
                Assert.IsTrue(dx <= 1 && dy <= 1 && dx + dy >= 1, $"not a neighbour step {cur}->{next}");
                if (dx == 1 && dy == 1)
                {
                    Assert.IsFalse(map.IsBlocked(new GridPos(next.X, cur.Y)), $"corner cut {cur}->{next}");
                    Assert.IsFalse(map.IsBlocked(new GridPos(cur.X, next.Y)), $"corner cut {cur}->{next}");
                }
                cost += dx + dy == 2 ? 577 : 408;
                route.Add(next);
                cur = next;
                Assert.Less(route.Count, 100, "route does not terminate");
            }
            Assert.AreEqual(TestMaps.Exit, cur);
            Assert.AreEqual(18, route.Count - 1);
            Assert.AreEqual(8865, cost);
            CollectionAssert.AreEqual(expected, route);
        }

        [Test]
        public void AC3_BlockedSpawn_WalledIn_IsNotReachable_And_TryGetNext_False()
        {
            var blocked = new[] { new GridPos(1, 2), new GridPos(3, 2), new GridPos(2, 1), new GridPos(2, 3) };
            var map = new GridMap(6, 6, blocked);
            var field = new FlowField(map);
            field.Compute(new GridPos(5, 5));
            var spawn = new GridPos(2, 2);
            Assert.IsFalse(field.IsReachable(spawn));
            Assert.AreEqual(FlowField.Unreachable, field.Distance(spawn));
            Assert.IsFalse(field.TryGetNext(spawn, out _));
            Assert.IsTrue(field.IsReachable(new GridPos(0, 0)));
        }

        [Test]
        public void AC4_Exit_TryGetNext_False_Distance_Zero()
        {
            FlowField field = TestMaps.PrototypeField(out _);
            Assert.AreEqual(0, field.Distance(TestMaps.Exit));
            Assert.IsFalse(field.TryGetNext(TestMaps.Exit, out GridPos next));
            Assert.AreEqual(TestMaps.Exit, next);
        }

        [Test]
        public void AC3_Blocked_Or_OutOfBounds_Exit()
        {
            var map = new GridMap(3, 3, new[] { new GridPos(1, 1) });
            var field = new FlowField(map);
            field.Compute(new GridPos(1, 1));
            Assert.IsFalse(field.IsReachable(new GridPos(0, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => field.Compute(new GridPos(3, 3)));
        }
    }
}
