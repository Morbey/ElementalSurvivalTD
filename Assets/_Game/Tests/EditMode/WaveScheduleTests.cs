using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ElementalTD.Core.Tests
{
    public class WaveScheduleTests
    {
        private const float Dt = Battle.TickSeconds;
        private static readonly EnemyStats Plain = new EnemyStats(2f, 100f);

        // Steps the schedule and returns the 1-based ticks on which an enemy entered.
        private static List<int> EntryTicks(WaveSchedule schedule, int ticks)
        {
            var result = new List<int>();
            for (int tick = 1; tick <= ticks; tick++)
                if (schedule.Step(Dt, out _)) result.Add(tick);
            return result;
        }

        [Test]
        public void S3a_AC1_A_Delay_Of_15s_Gives_The_First_Enemy_On_Tick_900_And_The_Others_One_Interval_Apart()
        {
            var schedule = new WaveSchedule(new[] { new Wave(Plain, 3, 1f, 15f) });

            List<int> ticks = EntryTicks(schedule, 1200);

            Assert.AreEqual(3, ticks.Count);
            Assert.AreEqual(900, ticks[0], 1);
            Assert.AreEqual(960, ticks[1], 1);
            Assert.AreEqual(1020, ticks[2], 1);
        }

        [Test]
        public void S3a_AC1_The_Next_Waves_Delay_Counts_From_The_Tick_Of_The_Previous_Waves_Last_Enemy()
        {
            var schedule = new WaveSchedule(new[] { new Wave(Plain, 2, 1f, 1f), new Wave(Plain, 1, 1f, 2f) });

            List<int> ticks = EntryTicks(schedule, 600);

            Assert.AreEqual(3, ticks.Count);
            Assert.AreEqual(60, ticks[0], 1);
            Assert.AreEqual(120, ticks[1], 1);
            Assert.AreEqual(ticks[1] + 120, ticks[2], 1);
        }

        [Test]
        public void S3a_AC1_Delay_Zero_Enters_On_The_First_Tick_And_Never_More_Than_One_Per_Tick()
        {
            var schedule = new WaveSchedule(new[] { new Wave(Plain, 3, 1f, 0f), new Wave(Plain, 2, 1f, 0f) });

            List<int> ticks = EntryTicks(schedule, 500);

            Assert.AreEqual(5, ticks.Count);
            Assert.AreEqual(1, ticks[0]);
            Assert.AreEqual(61, ticks[1], 1);
            Assert.AreEqual(121, ticks[2], 1);
            Assert.AreEqual(121, ticks[3], 1, "wave 2 starts at once, on the tick of the last enemy of wave 1 or the next");
            Assert.AreEqual(181, ticks[4], 1);
        }

        [Test]
        public void S3a_AC1_An_Interval_Below_One_Tick_Gives_Exactly_One_Enemy_Per_Tick()
        {
            var schedule = new WaveSchedule(new[] { new Wave(Plain, 5, 0.001f, 0f) });

            List<int> ticks = EntryTicks(schedule, 20);

            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, ticks);
        }

        [Test]
        public void S3a_AC1_Counters_Hold_Their_Documented_Values_Before_During_And_After_Each_Wave()
        {
            var schedule = new WaveSchedule(new[] { new Wave(Plain, 2, 1f, 1f), new Wave(Plain, 1, 1f, 2f) });

            Assert.AreEqual(2, schedule.WaveCount);
            Assert.AreEqual(0, schedule.StartedWaves);
            Assert.AreEqual(1f, schedule.SecondsToNextWave, 1e-4f, "before the first wave");
            Assert.IsFalse(schedule.IsFinished);

            while (!schedule.Step(Dt, out _)) { }
            Assert.AreEqual(1, schedule.StartedWaves, "the first enemy of wave 1 entered");
            Assert.AreEqual(0f, schedule.SecondsToNextWave, "0 while a wave spawns");

            while (!schedule.Step(Dt, out _)) { }
            Assert.AreEqual(1, schedule.StartedWaves);
            Assert.AreEqual(2f, schedule.SecondsToNextWave, Dt + 1e-4f, "wave 2's delay runs from the last enemy of wave 1");
            Assert.IsFalse(schedule.IsFinished);

            while (!schedule.Step(Dt, out _)) { }
            Assert.AreEqual(2, schedule.StartedWaves);
            Assert.AreEqual(0f, schedule.SecondsToNextWave, "0 from the last wave on");
            Assert.IsTrue(schedule.IsFinished);

            for (int i = 0; i < 300; i++) Assert.IsFalse(schedule.Step(Dt, out _), "nothing enters after the end");
            Assert.AreEqual(2, schedule.StartedWaves);
        }

        [Test]
        public void S3a_AC1_Each_Enemy_Carries_The_Stats_Of_Its_Own_Wave()
        {
            var a = new EnemyStats(2f, 100f);
            var b = new EnemyStats(3f, 40f);
            var schedule = new WaveSchedule(new[] { new Wave(a, 2, 0.5f, 0f), new Wave(b, 1, 0.5f, 0f) });
            var seen = new List<EnemyStats>();
            for (int i = 0; i < 300; i++)
                if (schedule.Step(Dt, out EnemyStats stats)) seen.Add(stats);

            Assert.AreEqual(3, seen.Count);
            Assert.AreEqual((2f, 100f), (seen[0].Speed, seen[0].MaxHealth));
            Assert.AreEqual((2f, 100f), (seen[1].Speed, seen[1].MaxHealth));
            Assert.AreEqual((3f, 40f), (seen[2].Speed, seen[2].MaxHealth));
        }

        [Test]
        public void S3a_AC1_The_One_Tick_Cap_Keeps_A_Fast_Wave_From_Eating_Into_The_Next_Delay()
        {
            // Wave 1: 30 enemies on ticks 1 to 30, one per tick. Wave 2's 1 s delay then runs from tick 30 and ends on tick 90.
            // Without the cap on the carried remainder the fast wave would shave about 28 ticks off it.
            var schedule = new WaveSchedule(new[] { new Wave(Plain, 30, 0.001f, 0f), new Wave(Plain, 1, 1f, 1f) });

            List<int> ticks = EntryTicks(schedule, 200);

            Assert.AreEqual(31, ticks.Count);
            Assert.AreEqual(30, ticks[29]);
            Assert.AreEqual(90, ticks[30], 1);
        }

        [Test]
        public void S3a_AC1_The_Cadence_Does_Not_Drift_Over_A_Hundred_Intervals()
        {
            // 101 enemies 0.25 s apart: the first on tick 1, the last 100 * 15 ticks later.
            var schedule = new WaveSchedule(new[] { new Wave(Plain, 101, 0.25f, 0f) });

            List<int> ticks = EntryTicks(schedule, 2000);

            Assert.AreEqual(101, ticks.Count);
            Assert.AreEqual(1500, ticks[100], 1);
        }

        [Test]
        public void S3a_AC1_The_Schedule_Keeps_Its_Own_Copy_Of_The_Waves_And_Rejects_A_Default_Wave()
        {
            var waves = new[] { new Wave(Plain, 1, 1f, 1f) };
            var schedule = new WaveSchedule(waves);
            waves[0] = new Wave(Plain, 5, 0.1f, 0f); // changing the caller's array must not change the running schedule

            List<int> ticks = EntryTicks(schedule, 600);

            Assert.AreEqual(1, ticks.Count);
            Assert.AreEqual(60, ticks[0], 1);

            Assert.Throws<ArgumentException>(() => new WaveSchedule(new Wave[1]), "a default Wave has no enemy");
        }

        [Test]
        public void S3a_AC1_Invalid_Waves_An_Empty_List_And_Zero_Lives_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Wave(Plain, 0, 1f, 1f), "no enemy");
            Assert.Throws<ArgumentOutOfRangeException>(() => new Wave(Plain, 1, 0f, 1f), "interval 0");
            Assert.Throws<ArgumentOutOfRangeException>(() => new Wave(Plain, 1, -1f, 1f), "negative interval");
            Assert.Throws<ArgumentOutOfRangeException>(() => new Wave(Plain, 1, 1f, -0.1f), "negative delay");
            Assert.Throws<ArgumentException>(() => new WaveSchedule(new Wave[0]));
            Assert.Throws<ArgumentNullException>(() => new WaveSchedule(null));

            var board = new Board(new GridMap(4, 4, new List<GridPos>()), new GridPos(0, 0), new GridPos(3, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Battle(board, new[] { new Wave(Plain, 1, 1f, 0f) }, 0));
        }
    }
}
