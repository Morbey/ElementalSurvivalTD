using System;
using NUnit.Framework;

namespace ElementalTD.Core.Tests
{
    public class PathFollowerTests
    {
        [Test]
        public void AC4_SpawnEqualsExit_HasArrived_AtConstruction_And_Step_Stays()
        {
            FlowField field = TestMaps.OpenField(4, 4, new GridPos(2, 2), out _);
            var f = new PathFollower(field, new GridPos(2, 2));
            Assert.IsTrue(f.HasArrived);
            Assert.AreEqual(f.From, f.To);
            Assert.IsTrue(f.Step(0.5f, 2f));
            Assert.AreEqual(new GridPos(2, 2), f.From);
        }

        [Test]
        public void AC3_UnreachableStart_Step_ReturnsFalse_Forever_And_DoesNotMove()
        {
            var map = new GridMap(5, 5, new[] { new GridPos(1, 2), new GridPos(3, 2), new GridPos(2, 1), new GridPos(2, 3) });
            var field = new FlowField(map);
            field.Compute(new GridPos(4, 4));
            var f = new PathFollower(field, new GridPos(2, 2));
            Assert.IsFalse(f.HasArrived);
            for (int i = 0; i < 100; i++) Assert.IsFalse(f.Step(1f, 5f));
            Assert.AreEqual(new GridPos(2, 2), f.From);
            Assert.AreEqual(new GridPos(2, 2), f.To);
            Assert.AreEqual(0f, f.Progress);
        }

        [Test]
        public void AC3_OutOfBounds_Start_Throws()
        {
            FlowField field = TestMaps.OpenField(4, 4, new GridPos(0, 0), out _);
            Assert.Throws<ArgumentOutOfRangeException>(() => new PathFollower(field, new GridPos(9, 9)));
        }

        [TestCase(0f, 2f)]
        [TestCase(-0.5f, 2f)]
        [TestCase(0.5f, 0f)]
        [TestCase(0.5f, -2f)]
        public void AC3_NonPositive_Dt_Or_Speed_DoesNotMove(float dt, float speed)
        {
            FlowField field = TestMaps.OpenField(5, 1, new GridPos(4, 0), out _);
            var f = new PathFollower(field, new GridPos(0, 0));
            Assert.IsFalse(f.Step(dt, speed));
            Assert.AreEqual(new GridPos(0, 0), f.From);
            Assert.AreEqual(0f, f.Progress);
        }

        [Test]
        public void AC3_AC4_Straight_Path_Arrives_After_N_Over_S_Seconds()
        {
            // 10 cells in a row: 9 steps at 2 cells/s with dt 0.1 = 4.5 s = 45 frames (+-1).
            FlowField field = TestMaps.OpenField(10, 1, new GridPos(9, 0), out _);
            var f = new PathFollower(field, new GridPos(0, 0));
            int frames = 0;
            while (!f.Step(0.1f, 2f) && frames < 200) frames++;
            frames++;
            Assert.That(frames, Is.InRange(44, 46));
            Assert.AreEqual(new GridPos(9, 0), f.From);
            Assert.AreEqual(f.From, f.To);
            Assert.IsTrue(f.HasArrived);
        }

        [Test]
        public void AC3_Progress_Interpolates_And_Large_Dt_Crosses_Several_Cells()
        {
            FlowField field = TestMaps.OpenField(10, 1, new GridPos(9, 0), out _);
            var f = new PathFollower(field, new GridPos(0, 0));
            f.Step(0.25f, 2f);
            Assert.AreEqual(new GridPos(0, 0), f.From);
            Assert.AreEqual(new GridPos(1, 0), f.To);
            Assert.AreEqual(0.5f, f.Progress, 1e-5f);

            f = new PathFollower(field, new GridPos(0, 0));
            Assert.IsFalse(f.Step(1.3f, 2f)); // 2.6 cells
            Assert.AreEqual(new GridPos(2, 0), f.From);
            Assert.AreEqual(new GridPos(3, 0), f.To);
            Assert.AreEqual(0.6f, f.Progress, 1e-4f);
        }

        [Test]
        public void AC4_Large_Dt_Past_Exit_Arrives_And_Stays()
        {
            FlowField field = TestMaps.OpenField(10, 1, new GridPos(9, 0), out _);
            var f = new PathFollower(field, new GridPos(0, 0));
            Assert.IsTrue(f.Step(100f, 2f));
            Assert.AreEqual(new GridPos(9, 0), f.From);
            Assert.AreEqual(f.From, f.To);
            Assert.IsTrue(f.Step(1f, 2f));
            Assert.AreEqual(new GridPos(9, 0), f.From);
        }

        [Test]
        public void S3c_AC4_Full_Prototype_Run_Is_21_73_Cells_At_2_CellsPerSecond_Is_10_86_Seconds()
        {
            FlowField field = TestMaps.PrototypeField(out GridMap map);
            var f = new PathFollower(field, TestMaps.Spawn);
            int frames = 0;
            while (!f.Step(0.01f, 2f))
            {
                frames++;
                Assert.IsFalse(map.IsBlocked(f.From));
                Assert.IsFalse(map.IsBlocked(f.To));
                Assert.Less(frames, 5000);
            }
            frames++;
            Assert.That(frames * 0.01f, Is.EqualTo(10.864f).Within(0.02f));
        }

        [Test]
        public void S2b_AC6_Restart_Leaves_Follower_Equal_To_A_New_One()
        {
            FlowField field = TestMaps.PrototypeField(out _);
            var used = new PathFollower(field, TestMaps.Spawn);
            used.Step(100f, 2f);
            Assert.IsTrue(used.HasArrived);
            used.Restart(TestMaps.Spawn);
            var fresh = new PathFollower(field, TestMaps.Spawn);
            Assert.AreEqual(fresh.From, used.From);
            Assert.AreEqual(fresh.To, used.To);
            Assert.AreEqual(fresh.Progress, used.Progress);
            Assert.AreEqual(fresh.HasArrived, used.HasArrived);
            Assert.IsFalse(used.HasArrived);
        }
    }
}
