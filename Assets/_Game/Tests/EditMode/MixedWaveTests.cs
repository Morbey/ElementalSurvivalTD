using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ElementalTD.Core.Tests
{
    /// <summary>
    /// Step 4e, Core part: the walk over a wave's groups (4e-AC1, AC2, AC7) and the mixed-fire pin (4e-AC3), with waves built in code.
    /// Plan: docs/plans/0010-prototype-slice-4e.md.
    /// </summary>
    public class MixedWaveTests
    {
        private const float Dt = Battle.TickSeconds;
        private static readonly Element Light = new Element(1);
        private static readonly Element Shadow = new Element(2);

        // Speed 2, health 45, reward 10: the enemies of both pins.
        private static EnemyStats Pin(Element element) => new EnemyStats(2f, 45f, 10, element);
        private static WaveGroup G(in EnemyStats enemy, int count, float interval) => new WaveGroup(enemy, count, interval);
        private static Wave W(float delay, int bonus, params WaveGroup[] groups) => new Wave(groups, delay, bonus);
        // A marker enemy: its health says which group it came from.
        private static EnemyStats Mark(float health, Element element = default, int reward = 0, float speed = 0f) => new EnemyStats(speed, health, reward, element);

        private sealed class Entry
        {
            public int Tick;
            public EnemyStats Stats;
        }

        private static List<Entry> Run(WaveSchedule schedule, int ticks)
        {
            var result = new List<Entry>();
            for (int tick = 1; tick <= ticks; tick++)
                if (schedule.Step(Dt, out EnemyStats stats)) result.Add(new Entry { Tick = tick, Stats = stats });
            return result;
        }

        private static int[] TicksOf(List<Entry> entries)
        {
            var ticks = new int[entries.Count];
            for (int i = 0; i < ticks.Length; i++) ticks[i] = entries[i].Tick;
            return ticks;
        }

        // ---- 4e-AC1 Groups in order

        private static WaveSchedule SchedulePin() => new WaveSchedule(new[]
        {
            W(1f, 0, G(Pin(Light), 2, 1f), G(Pin(Shadow), 2, 0.5f)),
            W(2f, 0, G(Pin(Light), 1, 1f)),
        });

        [Test]
        public void S4e_AC1_The_Schedule_Pin_Enters_Light_Light_Shadow_Shadow_Then_Wave_2_On_Ticks_61_121_151_181_301()
        {
            List<Entry> entries = Run(SchedulePin(), 400);

            CollectionAssert.AreEqual(new[] { 61, 121, 151, 181, 301 }, TicksOf(entries), "nothing enters on any other tick");
            var elements = new[] { Light, Light, Shadow, Shadow, Light };
            for (int i = 0; i < elements.Length; i++)
            {
                Assert.AreEqual(elements[i], entries[i].Stats.Element, $"entry {i + 1}");
                Assert.AreEqual((2f, 45f, 10), (entries[i].Stats.Speed, entries[i].Stats.MaxHealth, entries[i].Stats.GoldReward), $"entry {i + 1}");
            }
        }

        [Test]
        public void S4e_AC2_The_Schedule_Pin_Counters_On_Every_Tick()
        {
            WaveSchedule schedule = SchedulePin();
            Assert.AreEqual((0, 0, false), (schedule.StartedWaves, schedule.EnteredWaves, schedule.IsFinished));

            for (int tick = 1; tick <= 320; tick++)
            {
                schedule.Step(Dt, out _);
                string at = $"tick {tick}";
                int started = tick < 61 ? 0 : tick < 301 ? 1 : 2;
                int entered = tick < 181 ? 0 : tick < 301 ? 1 : 2;
                Assert.AreEqual(started, schedule.StartedWaves, at + " StartedWaves");
                Assert.AreEqual(entered, schedule.EnteredWaves, at + " EnteredWaves");
                Assert.AreEqual(tick >= 301, schedule.IsFinished, at + " IsFinished");

                float seconds = schedule.SecondsToNextWave;
                if (tick < 60) Assert.Greater(seconds, 0f, at + ": wave 1's delay runs");
                else if (tick >= 61 && tick <= 180) Assert.AreEqual(0f, seconds, at + ": 0 from the first enemy to the last of wave 1, also between groups");
                else if (tick == 181) Assert.AreEqual(2f - Dt, seconds, 1e-3f, at + ": wave 2's delay starts");
                else if (tick > 181 && tick < 300) Assert.Greater(seconds, 0f, at + ": wave 2's delay runs");
                else if (tick >= 301) Assert.AreEqual(0f, seconds, at + ": finished");
            }
        }

        [Test]
        public void S4e_AC1_A_First_Groups_Interval_Of_5s_Does_Not_Delay_The_Waves_First_Enemy()
        {
            // Delay 0: the first enemy enters on tick 1 although its group's interval is 5 s; the second one 5 s later
            // (a delay of 0 leaves a remainder of one tick, so the cadence from the first enemy is one tick short, as before 4e).
            var zero = new WaveSchedule(new[] { W(0f, 0, G(Mark(1f), 2, 5f), G(Mark(2f), 1, 1f)) });
            List<Entry> entries = Run(zero, 500);
            Assert.AreEqual(3, entries.Count);
            Assert.AreEqual(1, entries[0].Tick);
            Assert.AreEqual(301, entries[1].Tick, 1);
            Assert.AreEqual(entries[1].Tick + 60, entries[2].Tick, 1);

            // Delay 1 s: the first enemy enters as the delay ends, at tick 61.
            var delayed = new WaveSchedule(new[] { W(1f, 0, G(Mark(1f), 2, 5f)) });
            List<Entry> later = Run(delayed, 500);
            Assert.AreEqual(2, later.Count);
            Assert.AreEqual(61, later[0].Tick);
            Assert.AreEqual(361, later[1].Tick, 1, "5 s later; float noise may move it by a tick");
        }

        [Test]
        public void S4e_AC1_Four_Groups_Of_One_With_Intervals_1_0p5_2_1_Enter_0p5_2_And_1s_Apart()
        {
            var schedule = new WaveSchedule(new[]
            {
                W(1f, 0, G(Mark(1f), 1, 1f), G(Mark(2f), 1, 0.5f), G(Mark(3f), 1, 2f), G(Mark(4f), 1, 1f)),
            });

            List<Entry> entries = Run(schedule, 400);

            CollectionAssert.AreEqual(new[] { 61, 91, 211, 271 }, TicksOf(entries));
            Assert.IsTrue(schedule.IsFinished);
        }

        [Test]
        public void S4e_AC1_Each_Enemy_Carries_The_Stats_Of_Its_Own_Group()
        {
            var a = new EnemyStats(2f, 11f, 1, Light);
            var b = new EnemyStats(3f, 22f, 2, Shadow);
            var c = new EnemyStats(4f, 33f, 3);
            var schedule = new WaveSchedule(new[]
            {
                W(0f, 0, G(a, 2, 0.1f), G(b, 1, 0.1f), G(c, 2, 0.1f)),
            });

            List<Entry> entries = Run(schedule, 100);

            Assert.AreEqual(5, entries.Count);
            var expected = new[] { a, a, b, c, c };
            for (int i = 0; i < expected.Length; i++)
            {
                EnemyStats s = entries[i].Stats;
                Assert.AreEqual((expected[i].Speed, expected[i].MaxHealth, expected[i].GoldReward, expected[i].Element), (s.Speed, s.MaxHealth, s.GoldReward, s.Element), $"entry {i + 1}");
            }
        }

        [Test]
        public void S4e_AC1_Intervals_Below_One_Tick_Give_Exactly_One_Enemy_Per_Tick_Across_The_Change_Of_Group()
        {
            // 3 + 3 + 4 enemies, every interval far below a tick: ticks 1 to 10, one each, in group order.
            var schedule = new WaveSchedule(new[]
            {
                W(0f, 0, G(Mark(1f), 3, 0.001f), G(Mark(2f), 3, 0.002f), G(Mark(3f), 4, 0.001f)),
            });

            List<Entry> entries = Run(schedule, 30);

            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, TicksOf(entries));
            var healths = new[] { 1f, 1f, 1f, 2f, 2f, 2f, 3f, 3f, 3f, 3f };
            for (int i = 0; i < healths.Length; i++) Assert.AreEqual(healths[i], entries[i].Stats.MaxHealth, $"entry {i + 1}");
        }

        [Test]
        public void S4e_AC1_A_Fast_Mixed_Wave_Does_Not_Eat_Into_The_Next_Waves_Delay()
        {
            // Wave 1 enters on ticks 1 to 30 (three groups of ten, one per tick). Wave 2's 1 s delay runs from tick 30 and ends on tick 90.
            // Without the cap on the carried remainder, the change of group would shave the delay, as the single-group case does.
            var schedule = new WaveSchedule(new[]
            {
                W(0f, 0, G(Mark(1f), 10, 0.001f), G(Mark(2f), 10, 0.0001f), G(Mark(3f), 10, 0.001f)),
                W(1f, 0, G(Mark(4f), 1, 1f)),
            });

            List<Entry> entries = Run(schedule, 200);

            Assert.AreEqual(31, entries.Count);
            Assert.AreEqual(30, entries[29].Tick);
            Assert.AreEqual(90, entries[30].Tick, 1);
            for (int i = 0; i < 29; i++) Assert.AreEqual(i + 1, entries[i].Tick, $"entry {i + 1}: one per tick");
        }

        [Test]
        public void S4e_AC1_A_Sub_Tick_Interval_Before_A_Long_Group_Keeps_At_Most_One_Tick_Of_Remainder()
        {
            // Group 1 ends on tick 2 with its remainder capped at one tick; group 2's 1 s then counts from there: tick 61 or 62, never earlier.
            var schedule = new WaveSchedule(new[] { W(0f, 0, G(Mark(1f), 2, 0.001f), G(Mark(2f), 1, 1f)) });

            List<Entry> entries = Run(schedule, 200);

            Assert.AreEqual(3, entries.Count);
            Assert.AreEqual(new[] { 1, 2 }, new[] { entries[0].Tick, entries[1].Tick });
            Assert.AreEqual(62, entries[2].Tick, 1);
        }

        [Test]
        public void S4e_AC1_A_Hundred_Groups_Of_One_Enemy_0p25s_Apart_Do_Not_Drift()
        {
            var groups = new WaveGroup[100];
            for (int i = 0; i < groups.Length; i++) groups[i] = G(Mark(i + 1), 1, 0.25f);
            var schedule = new WaveSchedule(new[] { W(0f, 0, groups) });

            List<Entry> entries = Run(schedule, 2000);

            Assert.AreEqual(100, entries.Count);
            Assert.AreEqual(1, entries[0].Tick);
            for (int i = 0; i < entries.Count; i++)
            {
                Assert.AreEqual(1 + 15 * i, entries[i].Tick, 1, $"entry {i + 1}: a fixed 15 ticks apart, never accumulating");
                Assert.AreEqual(i + 1, entries[i].Stats.MaxHealth, $"entry {i + 1} is group {i + 1}");
            }
            Assert.IsTrue(schedule.IsFinished);
        }

        [Test]
        public void S4e_AC1_Alternating_Groups_Of_One_Enter_One_Per_Interval_And_Alternate_Elements()
        {
            var groups = new WaveGroup[10];
            for (int i = 0; i < groups.Length; i++) groups[i] = G(Pin(i % 2 == 0 ? Light : Shadow), 1, 1f);
            var schedule = new WaveSchedule(new[] { W(1f, 0, groups) });

            List<Entry> entries = Run(schedule, 800);

            Assert.AreEqual(10, entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                Assert.AreEqual(61 + 60 * i, entries[i].Tick, $"entry {i + 1}");
                Assert.AreEqual(i % 2 == 0 ? Light : Shadow, entries[i].Stats.Element, $"entry {i + 1}");
            }
        }

        [Test]
        public void S4e_AC1_Each_Wave_Starts_At_Its_First_Group_Also_After_A_Wave_That_Ended_In_A_Later_One()
        {
            // The walk's position resets at the end of a wave: wave 2 must read its group 1, not carry on from wave 1's last group.
            var schedule = new WaveSchedule(new[]
            {
                W(0f, 0, G(Mark(11f), 2, 0.5f), G(Mark(12f), 2, 0.5f), G(Mark(13f), 1, 0.5f)),
                W(1f, 0, G(Mark(21f), 1, 0.5f)),
                W(1f, 0, G(Mark(31f), 1, 0.5f), G(Mark(32f), 2, 0.5f)),
            });

            List<Entry> entries = Run(schedule, 1000);

            var healths = new List<float>();
            foreach (Entry e in entries) healths.Add(e.Stats.MaxHealth);
            CollectionAssert.AreEqual(new[] { 11f, 11f, 12f, 12f, 13f, 21f, 31f, 32f, 32f }, healths);
            Assert.IsTrue(schedule.IsFinished);
        }

        [Test]
        public void S4e_AC1_The_Schedule_Refuses_Only_A_Wave_With_No_Group_And_Accepts_Many_Groups()
        {
            Assert.DoesNotThrow(() => new WaveSchedule(new[] { W(0f, 0, G(Mark(1f), 1, 1f), G(Mark(1f), 1, 1f), G(Mark(1f), 1, 1f)) }));
            var one = W(0f, 0, G(Mark(1f), 1, 1f));
            var error = Assert.Throws<ArgumentException>(() => new WaveSchedule(new[] { one, default }));
            StringAssert.Contains("Wave 2 has no group.", error.Message);
            Assert.Throws<ArgumentException>(() => new WaveSchedule(new[] { default(Wave) }));
        }

        // The schedule of 4d, as it was: the oracle for "a wave of one group is tick-for-tick identical to before".
        private sealed class OldSchedule
        {
            private readonly Wave[] _waves;
            private int _wave, _enteredInWave;
            private bool _inDelay = true;
            private float _timer;
            public int StartedWaves, EnteredWaves;
            public bool IsFinished;
            public float SecondsToNextWave => _inDelay && _timer > 0f ? _timer : 0f;

            public OldSchedule(Wave[] waves)
            {
                _waves = waves;
                _timer = waves[0].DelayBeforeSeconds;
            }

            public bool Step(float dt, out EnemyStats enemy)
            {
                enemy = default;
                if (IsFinished) return false;
                _timer -= dt;
                if (_timer > 0f) return false;
                if (_timer < -dt) _timer = -dt;
                WaveGroup group = _waves[_wave].GroupAt(0);
                enemy = group.Enemy;
                if (_enteredInWave == 0) StartedWaves++;
                _enteredInWave++;
                _inDelay = false;
                if (_enteredInWave < group.Count)
                {
                    _timer += group.SpawnIntervalSeconds;
                    return true;
                }
                _wave++;
                EnteredWaves = _wave;
                _enteredInWave = 0;
                if (_wave < _waves.Length)
                {
                    _inDelay = true;
                    _timer += _waves[_wave].DelayBeforeSeconds;
                }
                else IsFinished = true;
                return true;
            }
        }

        [Test]
        public void S4e_AC1_A_Wave_Of_One_Group_Behaves_Tick_For_Tick_As_It_Did_Before_On_Seeded_Random_Schedules()
        {
            var random = new Random(4242);
            float[] intervals = { 0.001f, 0.01f, 0.25f, 0.3f, 0.7f, 1f, 5f };
            float[] delays = { 0f, 0.3f, 1f, 2.5f };
            for (int run = 0; run < 60; run++)
            {
                var waves = new Wave[1 + random.Next(4)];
                for (int w = 0; w < waves.Length; w++)
                    waves[w] = new Wave(Mark(random.Next(1, 100), default, w, w), 1 + random.Next(6), intervals[random.Next(intervals.Length)], delays[random.Next(delays.Length)], w);

                var now = new WaveSchedule(waves);
                var before = new OldSchedule(waves);
                for (int tick = 1; tick <= 2500; tick++)
                {
                    bool enteredNow = now.Step(Dt, out EnemyStats a);
                    bool enteredBefore = before.Step(Dt, out EnemyStats b);
                    string at = $"run {run} tick {tick}";
                    Assert.AreEqual(enteredBefore, enteredNow, at + " entry");
                    Assert.AreEqual((b.Speed, b.MaxHealth, b.GoldReward), (a.Speed, a.MaxHealth, a.GoldReward), at + " stats");
                    Assert.AreEqual(before.StartedWaves, now.StartedWaves, at + " StartedWaves");
                    Assert.AreEqual(before.EnteredWaves, now.EnteredWaves, at + " EnteredWaves");
                    Assert.AreEqual(before.IsFinished, now.IsFinished, at + " IsFinished");
                    Assert.AreEqual(before.SecondsToNextWave, now.SecondsToNextWave, at + " SecondsToNextWave");
                }
            }
        }

        // ---- 4e-AC2 A wave stays the unit

        [Test]
        public void S4e_AC2_StartedWaves_Rises_Once_Per_Wave_Not_Per_Group_EnteredWaves_Only_After_The_Last_Groups_Last_Enemy()
        {
            // Wave 1: groups of 2, 1 and 2 enemies. Wave 2 follows after a 3 s delay.
            var schedule = new WaveSchedule(new[]
            {
                W(1f, 0, G(Mark(1f), 2, 0.5f), G(Mark(2f), 1, 0.5f), G(Mark(3f), 2, 0.5f)),
                W(3f, 0, G(Mark(4f), 1, 0.5f), G(Mark(5f), 1, 0.5f)),
            });

            var seen = new List<(int tick, int started, int entered)>();
            for (int tick = 1; tick <= 600; tick++)
                if (schedule.Step(Dt, out _)) seen.Add((tick, schedule.StartedWaves, schedule.EnteredWaves));

            // Wave 1 enters on ticks 61, 91, 121, 151, 181 (the changes of group at 121 and 151 change nothing); wave 2, 3 s after the last, on 361 and 391.
            var ticks = new[] { 61, 91, 121, 151, 181, 361, 391 };
            Assert.AreEqual(ticks.Length, seen.Count);
            for (int i = 0; i < ticks.Length; i++) Assert.AreEqual(ticks[i], seen[i].tick, 1, $"entry {i + 1}");
            var counters = new[] { (1, 0), (1, 0), (1, 0), (1, 0), (1, 1), (2, 1), (2, 2) };
            for (int i = 0; i < counters.Length; i++) Assert.AreEqual(counters[i], (seen[i].started, seen[i].entered), $"entry {i + 1}: (StartedWaves, EnteredWaves)");
        }

        [Test]
        public void S4e_AC2_SecondsToNextWave_Is_0_Between_Groups_Also_When_The_Next_Groups_Interval_Is_Long_And_Counts_The_Next_Delay_From_The_Last_Enemy()
        {
            var schedule = new WaveSchedule(new[]
            {
                W(1f, 0, G(Mark(1f), 1, 1f), G(Mark(2f), 1, 10f), G(Mark(3f), 1, 5f)),
                W(3f, 0, G(Mark(4f), 1, 1f)),
            });

            // Group entries about 61, 661 (10 s later) and 961 (5 s later); wave 2 about 180 ticks after that. Float noise may move one by a tick.
            var entered = new List<int>();
            var counters = new List<(int started, int entered, float seconds, bool finished)>();
            counters.Add(default);
            for (int tick = 1; tick <= 1200; tick++)
            {
                if (schedule.Step(Dt, out _)) entered.Add(tick);
                counters.Add((schedule.StartedWaves, schedule.EnteredWaves, schedule.SecondsToNextWave, schedule.IsFinished));
            }

            Assert.AreEqual(4, entered.Count);
            Assert.AreEqual(61, entered[0]);
            Assert.AreEqual(entered[0] + 600, entered[1], 1);
            Assert.AreEqual(entered[1] + 300, entered[2], 1);
            Assert.AreEqual(entered[2] + 180, entered[3], 1);
            for (int tick = entered[0]; tick < entered[2]; tick++)
            {
                Assert.AreEqual(0f, counters[tick].seconds, $"tick {tick}: never above 0 inside a wave");
                Assert.AreEqual((1, 0), (counters[tick].started, counters[tick].entered), $"tick {tick}: the second group entered, the wave has not");
            }
            Assert.AreEqual((1, 1), (counters[entered[2]].started, counters[entered[2]].entered));
            Assert.AreEqual(3f - Dt / 2, counters[entered[2]].seconds, Dt / 2 + 1e-3f, "wave 2's delay counts from the last enemy of the last group, less the tick's remainder");
            for (int tick = entered[2] + 1; tick < entered[3]; tick++) Assert.Greater(counters[tick].seconds, 0f, $"tick {tick}");
            Assert.AreEqual((2, 2, 0f, true), (counters[entered[3]].started, counters[entered[3]].entered, counters[entered[3]].seconds, counters[entered[3]].finished));
        }

        // ---- On Battle: waves built in code

        private static Board OpenBoard() => new Board(new GridMap(16, 9, new List<GridPos>()), new GridPos(0, 4), new GridPos(15, 4));
        private static readonly GridPos Near = new GridPos(1, 3);   // 1.41 cells from the spawn
        // Fires every tick; a shot lands on the tick it is launched. Damage 10.
        private static TowerStats Gun(float range) => new TowerStats(range, 10f, 60f, 100f, 0);
        private static readonly Wave Far = new Wave(new EnemyStats(0f, 10f, 0), 1, 1f, 1_000_000f, 0);

        [Test]
        public void S4e_AC2_The_Enemies_Of_Every_Group_Carry_The_Index_Of_Their_Wave()
        {
            var battle = new Battle(OpenBoard(), new[]
            {
                W(0f, 5, G(Mark(10f), 2, 0.1f), G(Mark(20f), 2, 0.1f), G(Mark(30f), 1, 0.1f)),
                W(1f, 5, G(Mark(40f), 1, 0.1f), G(Mark(50f), 2, 0.1f)),
                Far,
            }, 5);

            for (int i = 0; i < 40; i++) battle.Tick();   // wave 1 has entered (ticks 1 to 30), wave 2's delay has not ended
            Assert.AreEqual(5, battle.Board.EnemyCount);
            for (int i = 0; i < 5; i++) Assert.AreEqual(0, battle.Board.EnemyAt(i).WaveIndex, $"enemy {i + 1} of wave 1, health {battle.Board.EnemyAt(i).MaxHealth}");

            for (int i = 0; i < 100; i++) battle.Tick();
            Assert.AreEqual(8, battle.Board.EnemyCount);
            int zero = 0, one = 0;
            for (int i = 0; i < 8; i++)
            {
                Enemy e = battle.Board.EnemyAt(i);
                if (e.WaveIndex == 0) zero++;
                else { one++; Assert.IsTrue(e.MaxHealth == 40f || e.MaxHealth == 50f, "wave 2's own groups"); }
            }
            Assert.AreEqual((5, 3), (zero, one));
        }

        [Test]
        public void S4e_AC2_A_Wave_Whose_First_Group_Is_Dead_Before_The_Second_Enters_Pays_Nothing_Until_The_Last_Enemy_Of_The_Second_Has_Left_Then_Once()
        {
            // Group 1 is killed on tick 1; group 2 (a runner too tough for the gun) enters on tick 121 and leaks about 29 ticks later.
            var battle = new Battle(OpenBoard(), new[]
            {
                W(0f, 50, G(Mark(10f, default, 4), 1, 1f), G(Mark(1000f, default, 0, 31f), 1, 2f)),
                Far,
            }, 5);
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(Near, Gun(2f)));

            battle.Tick();
            Assert.AreEqual(0, battle.Board.EnemyCount, "group 1 died on tick 1");
            Assert.AreEqual(4, battle.Gold, "its kill reward only");

            int tick = 1;
            while (battle.Lives == 5 && tick < 2000)
            {
                battle.Tick();
                tick++;
                if (battle.Lives == 5)
                {
                    Assert.AreEqual(4, battle.Gold, $"tick {tick}: no bonus while the wave has an enemy to enter or on the board");
                    Assert.AreEqual(0, battle.SurvivedWaves, $"tick {tick}");
                }
            }

            Assert.Greater(tick, 121, "the second group entered first");
            Assert.AreEqual(4, battle.Lives);
            Assert.AreEqual(4 + 50, battle.Gold, "the bonus on the tick the last enemy left");
            Assert.AreEqual((1, 1), (battle.SurvivedWaves, battle.LastSurvivedWave));

            for (int i = 0; i < 300; i++) battle.Tick();
            Assert.AreEqual(54, battle.Gold, "once");
            Assert.AreEqual(1, battle.SurvivedWaves);
        }

        [Test]
        public void S4e_AC2_A_Wave_Killed_Entirely_Group_By_Group_Pays_On_The_Tick_Of_The_Last_Kill()
        {
            var battle = new Battle(OpenBoard(), new[]
            {
                W(0f, 25, G(Mark(10f, default, 1), 1, 1f), G(Mark(10f, default, 2), 1, 2f)),
                Far,
            }, 5);
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(Near, Gun(2f)));

            int tick = 0;
            while (battle.Waves.EnteredWaves == 0 && tick < 500)
            {
                battle.Tick();
                tick++;
                if (battle.Waves.EnteredWaves == 0) Assert.AreEqual(0, battle.SurvivedWaves, $"tick {tick}");
            }

            Assert.AreEqual(121, tick);
            Assert.AreEqual(1 + 2 + 25, battle.Gold);
            Assert.AreEqual((1, 1), (battle.SurvivedWaves, battle.LastSurvivedWave));
        }

        [Test]
        public void S4e_AC2_Two_Mixed_Waves_That_Overlap_On_The_Board_Pay_Once_Each_Each_When_Its_Own_Last_Enemy_Has_Left()
        {
            var battle = new Battle(OpenBoard(), new[]
            {
                W(0f, 10, G(Mark(10f, default, 0, 5f), 2, 0.5f), G(Mark(10f, default, 0, 5f), 1, 0.5f)),
                W(0f, 100, G(Mark(10f, default, 0, 5f), 1, 1f), G(Mark(10f, default, 0, 5f), 1, 0.5f)),
            }, 20);

            var payTicks = new List<(int tick, int gold)>();
            bool overlapped = false;
            int lastGold = 0;
            for (int tick = 1; tick <= 3000 && battle.Outcome == RunOutcome.Running; tick++)
            {
                battle.Tick();
                bool zero = false, one = false;
                for (int i = 0; i < battle.Board.EnemyCount; i++)
                {
                    if (battle.Board.EnemyAt(i).WaveIndex == 0) zero = true;
                    if (battle.Board.EnemyAt(i).WaveIndex == 1) one = true;
                }
                overlapped |= zero && one;
                if (battle.Gold != lastGold)
                {
                    payTicks.Add((tick, battle.Gold - lastGold));
                    lastGold = battle.Gold;
                }
            }

            Assert.IsTrue(overlapped, "the waves were on the board together");
            Assert.AreEqual(RunOutcome.Won, battle.Outcome);
            Assert.AreEqual(2, payTicks.Count, "two payments");
            Assert.AreEqual(10, payTicks[0].gold);
            Assert.AreEqual(100, payTicks[1].gold);
            Assert.Less(payTicks[0].tick, payTicks[1].tick);
            Assert.AreEqual((2, 2), (battle.SurvivedWaves, battle.LastSurvivedWave));
            Assert.AreEqual(110, battle.Gold);
        }

        // ---- 4e-AC3 Mixed fire: the pin

        private static ElementChart LightShadowChart()
        {
            var chart = new ElementChart(2);
            chart.Set(Light, Shadow, 2f);
            chart.Set(Shadow, Light, 2f);
            return chart;
        }

        [Test]
        public void S4e_AC3_The_Mixed_Fire_Pin_Holds_Tick_By_Tick_On_The_Prototype_Map()
        {
            var board = new Board(new GridMap(16, 9, TestMaps.PrototypeBlocked()), TestMaps.Spawn, TestMaps.Exit);
            var battle = new Battle(board, new[] { W(0f, 7, G(Pin(Light), 1, 1f), G(Pin(Shadow), 1, 1f)) }, 10, 40, 50, LightShadowChart());
            var tower = new TowerStats(2.5f, 10f, 1.5f, 10f, 40, Light);
            var cell = new GridPos(2, 3);
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(cell, tower));
            Assert.AreEqual(0, battle.Gold);

            Enemy light = null, shadow = null;
            int shadowEntry = 0;
            var launches = new List<int>();
            var lightPositions = new Dictionary<int, (float x, float y)>();
            var shadowPositions = new Dictionary<int, (float x, float y)>();

            for (int tick = 1; tick <= 712; tick++)
            {
                battle.Tick();
                string at = $"tick {tick}";

                if (tick == 1) { Assert.AreEqual(1, board.EnemyCount, at); light = board.EnemyAt(0); Assert.AreEqual(Light, light.Element); }
                if (tick == 61) { Assert.AreEqual(2, board.EnemyCount, at); shadow = board.EnemyAt(1); shadowEntry = shadow.Entry; Assert.AreEqual(Shadow, shadow.Element); }

                // A shot launched this tick has its previous position on the tower's cell.
                for (int i = 0; i < battle.ProjectileCount; i++)
                {
                    Projectile p = battle.ProjectileAt(i);
                    if (p.PreviousX == cell.X && p.PreviousY == cell.Y) launches.Add(tick);
                }

                if (light != null && light.IsOnBoard) lightPositions[tick] = (light.X, light.Y);
                if (shadow != null && shadow.IsOnBoard) shadowPositions[tick] = (shadow.X, shadow.Y);

                int lightMark = tick < 12 ? 0 : tick < 49 ? 12 : tick < 95 ? 49 : 95;
                if (light != null && light.IsOnBoard) Assert.AreEqual(lightMark, light.ImmuneHitTick, at + ": the Light enemy's immunity mark");
                if (light != null && light.IsOnBoard) Assert.AreEqual(45f, light.Health, at + ": the Light enemy is never hurt");
                if (shadow != null && shadow.IsOnBoard) Assert.AreEqual(0, shadow.ImmuneHitTick, at + ": the Shadow enemy is never marked immune");
                if (shadow != null && shadow.IsOnBoard) Assert.AreEqual(tick < 131 ? 45f : 25f, shadow.Health, at + ": Shadow health");

                Assert.AreEqual(tick < 131 ? 0 : 1, battle.HitCount, at + ": the one hit record of the run");
                Assert.AreEqual(tick < 61 ? 1 : tick < 652 ? 2 : tick < 712 ? 1 : 0, board.EnemyCount, at + ": enemies on the board");
                Assert.AreEqual(tick < 652 ? 10 : tick < 712 ? 9 : 8, battle.Lives, at + ": lives");
                Assert.AreEqual(tick < 712 ? 0 : 7, battle.Gold, at + ": gold (no kill, bonus on the last exit)");
                Assert.AreEqual(tick < 712 ? 0 : 1, battle.SurvivedWaves, at + ": the wave is paid once, on the last exit");
                Assert.AreEqual(tick < 712 ? RunOutcome.Running : RunOutcome.Won, battle.Outcome, at);
            }

            CollectionAssert.AreEqual(new[] { 1, 40, 80, 120 }, launches, "four launches, 40 ticks apart");

            // Tick 80: the tower launches at the Light enemy with the Shadow enemy in range (and nearer the entrance).
            Assert.AreEqual(2.18f, lightPositions[80].x, 0.01f); Assert.AreEqual(5.18f, lightPositions[80].y, 0.01f);
            Assert.AreEqual(0.67f, shadowPositions[80].x, 0.01f); Assert.AreEqual(4.00f, shadowPositions[80].y, 0.01f);
            Assert.LessOrEqual(Dist(shadowPositions[80], cell), 2.5f, "the Shadow enemy is in range at tick 80");
            Assert.LessOrEqual(Dist(lightPositions[80], cell), 2.5f);
            // Tick 120: the Light enemy is out of range; the launch is at the Shadow enemy.
            Assert.Greater(Dist(lightPositions[120], cell), 2.5f, "the Light enemy left the range");
            Assert.AreEqual(1.71f, shadowPositions[120].x, 0.01f); Assert.AreEqual(4.71f, shadowPositions[120].y, 0.01f);
            // Tick 131: the shot lands on the Shadow enemy.
            Assert.AreEqual(1.97f, shadowPositions[131].x, 0.01f); Assert.AreEqual(4.97f, shadowPositions[131].y, 0.01f);
            Hit hit = battle.HitAt(0);
            Assert.AreEqual((20f, HitKind.Strong, 131, shadowEntry), (hit.Amount, hit.Kind, hit.Tick, hit.TargetEntry));
            Assert.AreEqual(1.97f, hit.X, 0.01f); Assert.AreEqual(4.97f, hit.Y, 0.01f);
        }

        private static float Dist((float x, float y) p, GridPos cell)
        {
            float dx = p.x - cell.X, dy = p.y - cell.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        // ---- 4e-AC7 Determinism and allocation with mixed waves

        private static Wave[] SeededMixedWaves(int seed, int waveCount)
        {
            var random = new Random(seed);
            float[] intervals = { 0.1f, 0.25f, 0.5f, 1f };
            var waves = new Wave[waveCount];
            for (int w = 0; w < waves.Length; w++)
            {
                var groups = new WaveGroup[2 + random.Next(3)];
                for (int g = 0; g < groups.Length; g++)
                {
                    Element element = random.Next(2) == 0 ? Light : Shadow;
                    groups[g] = G(new EnemyStats(3 + random.Next(5), 20 + 10 * random.Next(6), 1 + random.Next(5), element), 1 + random.Next(3), intervals[random.Next(intervals.Length)]);
                }
                waves[w] = new Wave(groups, w == 0 ? 0.5f : 0.5f + 0.5f * random.Next(3), random.Next(0, 30));
            }
            return waves;
        }

        private static void BuySeededTowers(Battle battle, int seed, int count)
        {
            var random = new Random(seed);
            for (int i = 0; i < count; i++)
            {
                var cell = new GridPos(random.Next(16), random.Next(9));
                battle.TryBuyTower(cell, new TowerStats(2.5f, 10f, 1.5f, 10f, 0, random.Next(2) == 0 ? Light : Shadow));
            }
        }

        private static Battle SeededBattle(int seed, int waveCount, int lives, int towers)
        {
            var board = new Board(new GridMap(16, 9, TestMaps.PrototypeBlocked()), TestMaps.Spawn, TestMaps.Exit);
            var battle = new Battle(board, SeededMixedWaves(seed, waveCount), lives, 0, 50, LightShadowChart());
            BuySeededTowers(battle, seed + 1, towers);
            return battle;
        }

        private static List<(int, int, int, int, int, int, float)> Snapshots(int seed, out Battle battle)
        {
            battle = SeededBattle(seed, 8, 10, 14);
            var list = new List<(int, int, int, int, int, int, float)>();
            for (int i = 0; i < 6000 && battle.Outcome == RunOutcome.Running; i++)
            {
                battle.Tick();
                float sum = 0f;
                for (int e = 0; e < battle.Board.EnemyCount; e++) sum += battle.Board.EnemyAt(e).X * 3f + battle.Board.EnemyAt(e).Y + battle.Board.EnemyAt(e).Health;
                list.Add((battle.Gold, battle.Lives, battle.Board.EnemyCount, battle.HitCount, battle.ProjectileCount, battle.SurvivedWaves, sum));
            }
            return list;
        }

        [Test]
        public void S4e_AC7_Two_Runs_Of_Seeded_Mixed_Waves_Are_Identical_Tick_For_Tick()
        {
            foreach (int seed in new[] { 1, 99, 2024 })
            {
                var a = Snapshots(seed, out Battle first);
                var b = Snapshots(seed, out Battle second);

                Assert.AreEqual(a, b, $"seed {seed}");
                Assert.AreNotEqual(RunOutcome.Running, first.Outcome, $"seed {seed}: the run is long enough to end");
                Assert.AreEqual(first.Outcome, second.Outcome);
                Assert.Greater(first.HitCount, 0, $"seed {seed}: towers hit something");
            }
            var other = Snapshots(1, out _);
            Assert.AreNotEqual(Snapshots(99, out _), other, "another seed is another run");
        }

        [Test]
        public void S4e_AC7_A_Run_Of_Seeded_Mixed_Waves_Allocates_Nothing_Per_Tick_Including_The_Ticks_Where_The_Group_Changes()
        {
            const int seed = 77, waveCount = 120, dryRun = 9000, measured = 1500;

            // A dry run of the same battle says when the pools last grew (a new enemy or shot object is a pooled allocation, not a tick's).
            Battle dry = SeededBattle(seed, waveCount, 100_000, 14);
            int peakEnemies = 0, peakShots = 0, lastGrowth = 0;
            for (int i = 1; i <= dryRun; i++)
            {
                dry.Tick();
                if (dry.Board.EnemyCount > peakEnemies || dry.ProjectileCount > peakShots) lastGrowth = i;
                peakEnemies = Math.Max(peakEnemies, dry.Board.EnemyCount);
                peakShots = Math.Max(peakShots, dry.ProjectileCount);
            }
            int warmUp = lastGrowth;   // the pools have their final size from here on
            Assert.LessOrEqual(warmUp + measured, dryRun, $"the pools stopped growing early enough to measure (peaks {peakEnemies} enemies, {peakShots} shots, last growth on tick {lastGrowth})");
            Assert.AreEqual(RunOutcome.Running, dry.Outcome, "the run is longer than the measured window");

            Battle battle = SeededBattle(seed, waveCount, 100_000, 14);
            var lockstep = new WaveSchedule(SeededMixedWaves(seed, waveCount));   // only to count the changes of group
            Element previous = default;
            int changes = 0, ticksDone = 0;

            void Observe(bool inWindow)
            {
                if (!lockstep.Step(Dt, out EnemyStats entered)) return;
                if (inWindow && !previous.IsNone && entered.Element != previous) changes++;
                previous = entered.Element;
            }

            for (int i = 0; i < warmUp; i++) { battle.Tick(); Observe(false); }

            TickAllocation.AssertNoneAcross(battle, measured, _ => { ticksDone++; Observe(true); });

            Assert.AreEqual(measured, ticksDone);
            Assert.Greater(changes, 5, "enemies of the other element entered during the measured ticks");
            Assert.AreEqual(RunOutcome.Running, battle.Outcome);
            Assert.Greater(battle.HitCount, 0);
        }
    }
}
