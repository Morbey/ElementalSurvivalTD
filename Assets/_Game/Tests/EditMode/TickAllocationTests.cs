using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ElementalTD.Core.Tests
{
    /// <summary>The measuring helper can fail: it sees a tick and a call that allocate, and stays silent for one that does not.</summary>
    public class TickAllocationTests
    {
        private static Board OpenBoard() => new Board(new GridMap(16, 9, new List<GridPos>()), new GridPos(0, 4), new GridPos(15, 4));

        [Test]
        public void The_First_Spawn_Tick_Allocates_And_The_Helper_Names_The_Tick_And_The_Count()
        {
            // The first enemy of a board is a new object: tick 1 allocates.
            var battle = new Battle(OpenBoard(), new[] { new Wave(new EnemyStats(1f, 10f), 1, 1f, 0f) }, 10);

            var failure = Assert.Throws<AssertionException>(() => TickAllocation.AssertNoneAcross(battle, 5));

            StringAssert.Contains("tick 1 of 5 allocated", failure.Message);
            StringAssert.Contains("allocation(s)", failure.Message);
        }

        [Test]
        public void A_Purchase_Allocates_A_Tower_Object_And_Is_Counted()
        {
            var battle = new Battle(OpenBoard(), new[] { new Wave(new EnemyStats(1f, 10f), 1, 1f, 1_000_000f) }, 10);
            var stats = new TowerStats(2f, 1f, 1f, 1f);
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(new GridPos(8, 8), stats)); // warm-up of the code path

            int count = TickAllocation.AllocationsOf(() => battle.TryBuyTower(new GridPos(7, 7), stats));

            Assert.Greater(count, 0);
        }

        [Test]
        public void A_Tick_That_Allocates_Nothing_Passes_And_Runs_The_Callback_Once_Per_Tick()
        {
            var battle = new Battle(OpenBoard(), new[] { new Wave(new EnemyStats(1f, 10f), 1, 1f, 1_000_000f) }, 10);
            battle.Tick(); // warm-up
            int calls = 0, last = 0;

            TickAllocation.AssertNoneAcross(battle, 100, i => { calls++; last = i; });

            Assert.AreEqual(100, calls);
            Assert.AreEqual(100, last);
            Assert.AreEqual(0, TickAllocation.AllocationsOf(() => { }));
            Assert.AreEqual(0, TickAllocation.AllocationsOf(() => battle.Tick()));
        }
    }
}
