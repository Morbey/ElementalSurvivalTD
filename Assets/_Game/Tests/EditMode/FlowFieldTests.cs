using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ElementalTD.Core.Tests
{
    public class FlowFieldTests
    {
        [Test]
        public void AC3_EmptyGrid_Distance_Equals_Manhattan_ForEveryCell()
        {
            var exit = new GridPos(3, 2);
            FlowField field = TestMaps.OpenField(7, 5, exit, out GridMap map);
            for (int y = 0; y < 5; y++)
                for (int x = 0; x < 7; x++)
                    Assert.AreEqual(Math.Abs(x - exit.X) + Math.Abs(y - exit.Y), field.Distance(new GridPos(x, y)), $"cell {x},{y}");
        }

        [Test]
        public void AC3_PrototypeLayout_Distance_Spawn_Is_27()
        {
            // Hand count: 4 right + 3 up + 5 right + 6 down + 6 right + 3 up = 27.
            FlowField field = TestMaps.PrototypeField(out _);
            Assert.AreEqual(27, field.Distance(TestMaps.Spawn));
        }

        [Test]
        public void AC3_PrototypeLayout_Route_Is_The_S_Shape_And_Legal()
        {
            FlowField field = TestMaps.PrototypeField(out GridMap map);
            var expected = new List<GridPos>();
            var corners = new[] { new GridPos(0, 4), new GridPos(4, 4), new GridPos(4, 7), new GridPos(9, 7), new GridPos(9, 1), new GridPos(15, 1), new GridPos(15, 4) };
            expected.Add(corners[0]);
            for (int i = 1; i < corners.Length; i++)
            {
                GridPos a = corners[i - 1], b = corners[i];
                int dx = Math.Sign(b.X - a.X), dy = Math.Sign(b.Y - a.Y);
                GridPos p = a;
                while (p != b) { p = new GridPos(p.X + dx, p.Y + dy); expected.Add(p); }
            }
            Assert.AreEqual(28, expected.Count);

            var route = new List<GridPos> { TestMaps.Spawn };
            GridPos cur = TestMaps.Spawn;
            while (field.TryGetNext(cur, out GridPos next))
            {
                Assert.IsFalse(map.IsBlocked(next), $"entered blocked {next}");
                Assert.AreEqual(1, Math.Abs(next.X - cur.X) + Math.Abs(next.Y - cur.Y), $"non 4-neighbour step {cur}->{next}");
                route.Add(next);
                cur = next;
                Assert.Less(route.Count, 100, "route does not terminate");
            }
            Assert.AreEqual(TestMaps.Exit, cur);
            Assert.AreEqual(27, route.Count - 1);
            CollectionAssert.AreEqual(expected, route);
        }

        [Test]
        public void AC3_TieBreak_Order_PlusX_MinusX_PlusY_MinusY()
        {
            // Exit in the middle of a 3x3 grid: the four neighbours of the corner-adjacent cells tie.
            // From (0,0) to exit (2,2): +x and +y both reduce distance; +x must win.
            FlowField field = TestMaps.OpenField(3, 3, new GridPos(2, 2), out _);
            Assert.IsTrue(field.TryGetNext(new GridPos(0, 0), out GridPos n1));
            Assert.AreEqual(new GridPos(1, 0), n1);
            // From (2,0) to exit (0,2): -x and +y tie; -x must win over +y.
            field = TestMaps.OpenField(3, 3, new GridPos(0, 2), out _);
            Assert.IsTrue(field.TryGetNext(new GridPos(2, 0), out GridPos n2));
            Assert.AreEqual(new GridPos(1, 0), n2);
            // From (0,2) to exit (2,0): +x and -y tie; +x must win.
            field = TestMaps.OpenField(3, 3, new GridPos(2, 0), out _);
            Assert.IsTrue(field.TryGetNext(new GridPos(0, 2), out GridPos n3));
            Assert.AreEqual(new GridPos(1, 2), n3);
            // From (2,2) to exit (0,0): -x and -y tie; -x must win.
            field = TestMaps.OpenField(3, 3, new GridPos(0, 0), out _);
            Assert.IsTrue(field.TryGetNext(new GridPos(2, 2), out GridPos n4));
            Assert.AreEqual(new GridPos(1, 2), n4);
            // From (1,0) to exit (1,2) is a straight +y; from (1,2) to exit (1,0) is -y.
            field = TestMaps.OpenField(3, 3, new GridPos(1, 0), out _);
            Assert.IsTrue(field.TryGetNext(new GridPos(1, 2), out GridPos n5));
            Assert.AreEqual(new GridPos(1, 1), n5);
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
