using UnityEngine.TestTools.Constraints;
using NUnit.Framework;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace ElementalTD.Core.Tests
{
    public class PathFollowerAllocationTests
    {
        [Test]
        public void AC7_Step_AllocatesNothing_AfterWarmup_Within_And_Across_Cells()
        {
            FlowField field = TestMaps.PrototypeField(out _);
            var inCell = new PathFollower(field, TestMaps.Spawn);
            inCell.Step(0.016f, 2f); // warm-up
            Assert.That(() => { inCell.Step(0.016f, 2f); }, Is.Not.AllocatingGCMemory());

            var crossing = new PathFollower(field, TestMaps.Spawn);
            crossing.Step(0.016f, 100f); // warm-up; 1.6 cells per call, so every call crosses a boundary
            Assert.That(() => { crossing.Step(0.016f, 100f); }, Is.Not.AllocatingGCMemory());
            Assert.That(() => { crossing.Step(0.016f, 100f); }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void AC7_TryGetNext_AllocatesNothing_AfterWarmup()
        {
            FlowField field = TestMaps.PrototypeField(out _);
            field.TryGetNext(TestMaps.Spawn, out _);
            Assert.That(() => { field.TryGetNext(TestMaps.Spawn, out _); }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void S2b_AC10_Board_Place_Refuse_Remove_Step_And_Restart_Allocate_Nothing_After_Warmup()
        {
            var board = new Board(new GridMap(16, 9, TestMaps.PrototypeBlocked()), TestMaps.Spawn, TestMaps.Exit);
            var a = new GridPos(2, 2);
            var c1 = new GridPos(5, 7);
            var c2 = new GridPos(5, 8);
            PathFollower follower = board.AddFollower();

            // Warm-up: every path once.
            board.TryPlaceTower(a); board.TryRemoveTower(a);
            board.TryPlaceTower(c1); board.TryPlaceTower(c2); board.TryRemoveTower(c1);
            follower.Step(0.016f, 2f); follower.Restart(TestMaps.Spawn);

            Assert.That(() => { board.TryPlaceTower(a); }, Is.Not.AllocatingGCMemory());            // placed
            Assert.That(() => { board.TryRemoveTower(a); }, Is.Not.AllocatingGCMemory());           // removed
            board.TryPlaceTower(c1);
            Assert.That(() => { board.TryPlaceTower(c2); }, Is.Not.AllocatingGCMemory());           // refused, reverted
            Assert.That(() => { board.TryPlaceTower(TestMaps.Spawn); }, Is.Not.AllocatingGCMemory()); // refused early
            Assert.That(() => { follower.Step(0.016f, 2f); }, Is.Not.AllocatingGCMemory());
            Assert.That(() => { follower.Restart(TestMaps.Spawn); }, Is.Not.AllocatingGCMemory());
        }
    }
}
