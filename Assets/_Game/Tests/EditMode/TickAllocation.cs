using System;
using NUnit.Framework;
using UnityEngine.Profiling;

namespace ElementalTD.Core.Tests
{
    /// <summary>
    /// Counts what one call allocates, on this thread only, around every single call, so a whole-run test says which tick allocated.
    /// NUnit's <c>Is.Not.AllocatingGCMemory</c> reads the allocations of all threads over the whole window, so another thread
    /// of the editor made it fail at random. <c>GC.GetAllocatedBytesForCurrentThread</c> is no use: this Mono returns 0 for any allocation.
    /// The profiler counts allocations, not bytes.
    /// </summary>
    internal static class TickAllocation
    {
        private static readonly Recorder Allocations = Recorder.Get("GC.Alloc");

        /// <summary>
        /// Ticks the battle and fails on the first tick that allocates, naming it and the number of allocations.
        /// <paramref name="afterTick"/> runs outside the measured call, with the number of the tick that just ran (from 1).
        /// </summary>
        public static void AssertNoneAcross(Battle battle, int ticks, Action<int> afterTick = null)
        {
            Assert.IsTrue(Allocations.isValid, "the GC.Alloc profiler counter must exist");
            for (int i = 1; i <= ticks; i++)
            {
                Allocations.enabled = false;
                Allocations.FilterToCurrentThread();
                Allocations.enabled = true;
                battle.Tick();
                Allocations.enabled = false;
                int count = Allocations.sampleBlockCount;
                if (count != 0) Assert.Fail($"tick {i} of {ticks} allocated ({count} allocation(s))");
                afterTick?.Invoke(i);
            }
        }

        /// <summary>The number of allocations one call made on this thread.</summary>
        public static int AllocationsOf(Action action)
        {
            Allocations.enabled = false;
            Allocations.FilterToCurrentThread();
            Allocations.enabled = true;
            action();
            Allocations.enabled = false;
            return Allocations.sampleBlockCount;
        }
    }
}
