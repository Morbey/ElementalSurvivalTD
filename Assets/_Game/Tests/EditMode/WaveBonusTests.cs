using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ElementalTD.Core.Tests
{
    /// <summary>The wave as one unit and its survival bonus (4a-AC1 to AC3, AC7 and AC8, the Core side).</summary>
    public class WaveBonusTests
    {
        private static readonly GridPos Spawn = new GridPos(0, 4);
        private static readonly GridPos Near = new GridPos(1, 3);   // 1.41 cells from the spawn

        // The open board is a straight path of 15 cells, 0.5167 cells per tick at speed 31.
        private static Board OpenBoard() => new Board(new GridMap(16, 9, new List<GridPos>()), Spawn, new GridPos(15, 4));

        private static EnemyStats Still(int reward = 0, float health = 10f) => new EnemyStats(0f, health, reward);
        private static EnemyStats Runner(float speed, int reward = 0) => new EnemyStats(speed, 10f, reward);
        private static Wave W(EnemyStats enemy, int count, float interval, float delay, int bonus) => new Wave(enemy, count, interval, delay, bonus);
        // Never enters inside a test, so the run does not end by itself.
        private static readonly Wave Far = new Wave(new EnemyStats(0f, 10f, 0), 1, 1f, 1_000_000f, 0);

        // Fires every tick; a shot is fast enough to land on the tick it is launched. Damage 10.
        private static TowerStats Gun(float range = 20f) => new TowerStats(range, 10f, 60f, 100f, 0);

        private static Battle Run(Wave[] waves, int lives = 5, int gold = 0) => new Battle(OpenBoard(), waves, lives, gold);

        private static void Ticks(Battle battle, int count)
        {
            for (int i = 0; i < count; i++) battle.Tick();
        }

        private static void BuyGun(Battle battle, float range = 20f) =>
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(Near, Gun(range)));

        // ---- 4a-AC1 Wave shape

        [Test]
        public void S4a_AC1_A_Wave_Built_From_One_Group_Equals_One_Built_By_The_Short_Constructor()
        {
            var enemy = new EnemyStats(2f, 30f, 4);
            var shortWave = new Wave(enemy, 5, 0.5f, 7f, 12);
            var longWave = new Wave(new[] { new WaveGroup(enemy, 5, 0.5f) }, 7f, 12);

            Assert.AreEqual(shortWave.GroupCount, longWave.GroupCount);
            Assert.AreEqual(1, longWave.GroupCount);
            Assert.AreEqual(shortWave.DelayBeforeSeconds, longWave.DelayBeforeSeconds);
            Assert.AreEqual(shortWave.SurvivalBonus, longWave.SurvivalBonus);
            WaveGroup a = shortWave.GroupAt(0), b = longWave.GroupAt(0);
            Assert.AreEqual((2f, 30f, 4, 5, 0.5f), (a.Enemy.Speed, a.Enemy.MaxHealth, a.Enemy.GoldReward, a.Count, a.SpawnIntervalSeconds));
            Assert.AreEqual((a.Enemy.Speed, a.Enemy.MaxHealth, a.Enemy.GoldReward, a.Count, a.SpawnIntervalSeconds),
                            (b.Enemy.Speed, b.Enemy.MaxHealth, b.Enemy.GoldReward, b.Count, b.SpawnIntervalSeconds));
            Assert.AreEqual((7f, 12), (longWave.DelayBeforeSeconds, longWave.SurvivalBonus));
        }

        [Test]
        public void S4a_AC1_The_Bonus_Defaults_To_0_And_Zero_Is_Valid()
        {
            Assert.AreEqual(0, new Wave(Still(), 1, 1f, 0f).SurvivalBonus);
            Assert.AreEqual(0, new Wave(new[] { new WaveGroup(Still(), 1, 1f) }, 0f).SurvivalBonus);
            Assert.AreEqual(0, new Wave(Still(), 1, 1f, 0f, 0).SurvivalBonus);
            Assert.AreEqual(int.MaxValue, new Wave(Still(), 1, 1f, 0f, int.MaxValue).SurvivalBonus);
        }

        [Test]
        public void S4a_AC1_Null_Or_Empty_Groups_A_Default_Group_And_A_Negative_Bonus_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new Wave((WaveGroup[])null, 0f));
            Assert.Throws<ArgumentException>(() => new Wave(new WaveGroup[0], 0f));
            Assert.Throws<ArgumentException>(() => new Wave(new WaveGroup[1], 0f), "a default group has no enemy");
            Assert.Throws<ArgumentException>(() => new Wave(new[] { new WaveGroup(Still(), 1, 1f), default }, 0f), "any default group");
            Assert.Throws<ArgumentOutOfRangeException>(() => new Wave(Still(), 1, 1f, 0f, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Wave(new[] { new WaveGroup(Still(), 1, 1f) }, 0f, int.MinValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Wave(Still(), 1, 1f, -0.1f), "a negative delay");
            Assert.Throws<ArgumentOutOfRangeException>(() => new WaveGroup(Still(), 0, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WaveGroup(Still(), 1, 0f));
        }

        [Test]
        public void S4a_AC1_A_Default_Wave_Has_No_Group_And_GroupAt_Is_Bounded()
        {
            Wave none = default;
            Assert.AreEqual(0, none.GroupCount);
            Assert.Throws<ArgumentOutOfRangeException>(() => none.GroupAt(0));

            var wave = new Wave(Still(), 1, 1f, 0f);
            Assert.Throws<ArgumentOutOfRangeException>(() => wave.GroupAt(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => wave.GroupAt(-1));
        }

        [Test]
        public void S4a_AC1_The_Wave_Keeps_Its_Own_Copy_Of_The_Groups()
        {
            var groups = new[] { new WaveGroup(new EnemyStats(1f, 10f, 3), 4, 0.5f) };
            var wave = new Wave(groups, 1f, 5);

            groups[0] = new WaveGroup(new EnemyStats(9f, 99f, 9), 9, 9f);

            Assert.AreEqual((1f, 10f, 3, 4, 0.5f), (wave.GroupAt(0).Enemy.Speed, wave.GroupAt(0).Enemy.MaxHealth, wave.GroupAt(0).Enemy.GoldReward, wave.GroupAt(0).Count, wave.GroupAt(0).SpawnIntervalSeconds));
        }

        [Test]
        public void S4a_AC1_WaveSchedule_Refuses_Two_Groups_And_A_Default_Wave_And_WaveAt_Returns_Each_Wave()
        {
            var two = new Wave(new[] { new WaveGroup(Still(), 1, 1f), new WaveGroup(Still(), 1, 1f) }, 0f);
            var one = new Wave(Still(), 1, 1f, 0f, 3);
            Assert.Throws<ArgumentException>(() => new WaveSchedule(new[] { one, two }));
            Assert.Throws<ArgumentException>(() => new WaveSchedule(new[] { one, default }));
            Assert.Throws<ArgumentException>(() => new WaveSchedule(new[] { default(Wave) }));

            var a = new Wave(new EnemyStats(1f, 10f, 1), 2, 1f, 3f, 10);
            var b = new Wave(new EnemyStats(2f, 20f, 2), 3, 2f, 4f, 20);
            var schedule = new WaveSchedule(new[] { a, b });
            Assert.AreEqual(2, schedule.WaveCount);
            Assert.AreEqual((3f, 10, 2), (schedule.WaveAt(0).DelayBeforeSeconds, schedule.WaveAt(0).SurvivalBonus, schedule.WaveAt(0).GroupAt(0).Count));
            Assert.AreEqual((4f, 20, 3), (schedule.WaveAt(1).DelayBeforeSeconds, schedule.WaveAt(1).SurvivalBonus, schedule.WaveAt(1).GroupAt(0).Count));
        }

        [Test]
        public void S4a_AC1_EnteredWaves_Rises_On_The_Tick_A_Waves_Last_Enemy_Enters_Not_The_First()
        {
            // Delay 0: the first enemy enters on tick 1; an interval of 1 tick then gives one enemy per tick.
            var schedule = new WaveSchedule(new[]
            {
                new Wave(Still(), 3, Battle.TickSeconds, 0f),
                new Wave(Still(), 1, 1f, 0f),
            });

            Assert.IsTrue(schedule.Step(Battle.TickSeconds, out _));   // tick 1: first of 3
            Assert.AreEqual((1, 0), (schedule.StartedWaves, schedule.EnteredWaves));
            Assert.IsTrue(schedule.Step(Battle.TickSeconds, out _));   // tick 2: second of 3
            Assert.AreEqual((1, 0), (schedule.StartedWaves, schedule.EnteredWaves));
            Assert.IsTrue(schedule.Step(Battle.TickSeconds, out _));   // tick 3: last of 3
            Assert.AreEqual((1, 1), (schedule.StartedWaves, schedule.EnteredWaves));
            Assert.IsTrue(schedule.Step(Battle.TickSeconds, out _));   // tick 4: wave 2, one enemy
            Assert.AreEqual((2, 2), (schedule.StartedWaves, schedule.EnteredWaves));
            Assert.IsTrue(schedule.IsFinished);
        }

        // ---- 4a-AC2, AC3 Survived and bonus

        [Test]
        public void S4a_AC2_Nothing_Built_The_Bonus_Is_Paid_On_The_Tick_The_Last_Enemy_Exits()
        {
            Battle battle = Run(new[] { W(Runner(31f), 1, 1f, 0f, 25), Far }, 2, 5);

            int tick = 0;
            while (battle.Lives == 2 && tick < 2000)
            {
                battle.Tick();
                tick++;
                if (battle.Lives == 2)
                {
                    Assert.AreEqual(5, battle.Gold, $"tick {tick}: nothing is paid while the enemy is on the board");
                    Assert.AreEqual((0, 0), (battle.SurvivedWaves, battle.LastSurvivedWave));
                }
            }

            Assert.AreEqual(1, battle.Lives, "the enemy exited");
            Assert.AreEqual(30, battle.Gold, "5 + 25 on the exit tick");
            Assert.AreEqual((1, 1), (battle.SurvivedWaves, battle.LastSurvivedWave));
            Assert.AreEqual(RunOutcome.Running, battle.Outcome);
        }

        [Test]
        public void S4a_AC2_A_Wave_Killed_Entirely_Pays_On_The_Tick_Of_The_Last_Kill_With_The_Reward()
        {
            Battle battle = Run(new[] { W(Still(7), 1, 1f, 0f, 25), Far }, 5, 3);
            BuyGun(battle);

            battle.Tick(); // tick 1: enters and dies

            Assert.AreEqual(3 + 7 + 25, battle.Gold);
            Assert.AreEqual((1, 1), (battle.SurvivedWaves, battle.LastSurvivedWave));
            Assert.AreEqual(0, battle.Board.EnemyCount);
            Assert.AreEqual(5, battle.Lives);
        }

        [Test]
        public void S4a_AC2_A_Wave_Leaked_Entirely_Pays_Although_Every_Enemy_Reached_The_Exit()
        {
            Battle battle = Run(new[] { W(Runner(31f, 50), 3, 0.1f, 0f, 40), Far }, 10, 0);

            int tick = 0;
            while (battle.Lives > 7 && tick < 2000) { battle.Tick(); tick++; }
            Assert.AreEqual(7, battle.Lives);
            Assert.AreEqual(40, battle.Gold, "no kill reward, only the bonus, on the tick of the third exit");
            Assert.AreEqual(1, battle.SurvivedWaves);
        }

        [Test]
        public void S4a_AC2_A_Wave_Half_Killed_And_Half_Leaked_Pays_When_The_Last_One_Has_Left()
        {
            // The gun fires once in ten seconds: it kills the first runner, the second one leaks.
            var slowGun = new TowerStats(20f, 10f, 0.1f, 100f, 0);
            Battle battle = Run(new[] { W(Runner(6f, 8), 2, 0.5f, 0f, 30), Far }, 3, 0);
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(Near, slowGun));

            battle.Tick(); // tick 1: first runner enters and is shot
            Assert.AreEqual(8, battle.Gold, "the kill pays its reward only: the wave has one enemy left to enter");

            int tick = 1;
            while (battle.Lives == 3 && tick < 2000)
            {
                battle.Tick();
                tick++;
                if (battle.Lives == 3) Assert.AreEqual(8, battle.Gold);
            }
            Assert.AreEqual(2, battle.Lives);
            Assert.AreEqual(8 + 30, battle.Gold);
            Assert.AreEqual(1, battle.SurvivedWaves);
        }

        [Test]
        public void S4a_AC2_A_Long_Interval_Empties_The_Board_Between_Spawns_And_Nothing_Is_Paid_Until_The_Last_Enemy_Has_Entered_And_Left()
        {
            Battle battle = Run(new[] { W(Still(2), 3, 0.5f, 0f, 100), Far }, 5, 0);
            BuyGun(battle);

            int emptyBoardTicks = 0;
            int ticks = 0;
            while (battle.Waves.EnteredWaves == 0 && ticks < 2000)
            {
                battle.Tick();
                ticks++;
                if (battle.Waves.EnteredWaves == 0)
                {
                    if (battle.Board.EnemyCount == 0) emptyBoardTicks++;
                    Assert.AreEqual(0, battle.SurvivedWaves, $"tick {ticks}: the third enemy has not entered");
                    Assert.Less(battle.Gold, 100, $"tick {ticks}");
                }
            }

            Assert.Greater(emptyBoardTicks, 20, "the board was empty between spawns");
            Assert.AreEqual(3 * 2 + 100, battle.Gold, "three rewards and the bonus, all on the tick the third enemy enters and dies");
            Assert.AreEqual(1, battle.SurvivedWaves);
        }

        [Test]
        public void S4a_AC3_The_Wave_Pays_Once_Not_Again_On_Later_Ticks()
        {
            Battle battle = Run(new[] { W(Still(0), 1, 1f, 0f, 25), Far }, 5, 0);
            BuyGun(battle);

            Ticks(battle, 1);
            Assert.AreEqual(25, battle.Gold);
            Ticks(battle, 500);

            Assert.AreEqual(25, battle.Gold);
            Assert.AreEqual((1, 1), (battle.SurvivedWaves, battle.LastSurvivedWave));
        }

        [Test]
        public void S4a_AC3_A_Bonus_Of_Zero_Counts_As_Survived_And_Pays_Nothing()
        {
            Battle battle = Run(new[] { W(Still(5), 1, 1f, 0f, 0), Far }, 5, 1);
            BuyGun(battle);

            battle.Tick();

            Assert.AreEqual(6, battle.Gold, "only the kill reward");
            Assert.AreEqual((1, 1), (battle.SurvivedWaves, battle.LastSurvivedWave));
        }

        [Test]
        public void S4a_AC2_A_Fast_Later_Wave_Clears_Before_A_Slow_Earlier_One_Pays_First_And_The_Earlier_One_Still_Pays_Later()
        {
            // Wave 1 crawls (3 cells per second), wave 2 enters on tick 2 and runs five times faster.
            Battle battle = Run(new[] { W(Runner(3f), 1, 1f, 0f, 10), W(Runner(15f), 1, 1f, 0f, 100), Far }, 5, 0);

            int ticks = 0;
            while (battle.SurvivedWaves == 0 && ticks < 5000) { battle.Tick(); ticks++; }
            Assert.AreEqual(1, battle.SurvivedWaves);
            Assert.AreEqual(2, battle.LastSurvivedWave, "wave 2 paid first");
            Assert.AreEqual(100, battle.Gold);
            Assert.AreEqual(1, battle.Board.EnemyCount, "wave 1's enemy is still walking");

            int gold = battle.Gold;
            while (battle.SurvivedWaves == 1 && ticks < 5000)
            {
                battle.Tick();
                ticks++;
                if (battle.SurvivedWaves == 1) Assert.AreEqual(gold, battle.Gold);
            }
            Assert.AreEqual(2, battle.SurvivedWaves);
            Assert.AreEqual(1, battle.LastSurvivedWave, "wave 1 paid later");
            Assert.AreEqual(110, battle.Gold);

            Ticks(battle, 300);
            Assert.AreEqual(110, battle.Gold, "each paid once");
        }

        [Test]
        public void S4a_AC2_Two_Waves_That_Clear_On_One_Tick_Both_Pay_On_It_In_Wave_Order()
        {
            // Wave 1 enters on tick 1 at 31 cells per second (0.5167 per tick) and exits on tick 30;
            // wave 2 enters on tick 2 at 32 (0.5333 per tick): 29 steps of it also reach the 15 cells, on tick 30.
            Battle battle = Run(new[] { W(Runner(31f), 1, 1f, 0f, 10), W(Runner(32f), 1, 1f, 0f, 100), Far }, 5, 0);

            int ticks = 0;
            int livesBefore = battle.Lives;
            while (battle.Lives == 5 && ticks < 2000)
            {
                livesBefore = battle.Lives;
                battle.Tick();
                ticks++;
                if (battle.Lives == 5) Assert.AreEqual(0, battle.Gold);
            }

            Assert.AreEqual(3, battle.Lives, "both exited on this very tick");
            Assert.AreEqual(2, battle.SurvivedWaves);
            Assert.AreEqual(110, battle.Gold);
            Assert.AreEqual(2, battle.LastSurvivedWave, "the last to pay on the tick is wave 2: wave order");
        }

        [Test]
        public void S4a_AC2_Every_Enemy_Of_A_Wave_Holds_It_Not_Only_The_Last_To_Enter()
        {
            // Two enemies five seconds apart at one cell per second; the first is still walking when the second enters on tick 300.
            Battle battle = Run(new[] { W(Runner(1f, 3), 2, 5f, 0f, 25), Far }, 5, 0);
            Ticks(battle, 290);
            Assert.AreEqual(1, battle.Board.EnemyCount);
            Assert.AreEqual(0, battle.Board.EnemyAt(0).WaveIndex);
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(Near, Gun(2f))); // reaches the spawn only

            int ticks = 290;
            while (battle.Waves.EnteredWaves == 0 && ticks < 400) { battle.Tick(); ticks++; }

            Assert.AreEqual(300, ticks, 1, "the second enemy enters on tick 300 and dies at once");
            Assert.AreEqual(1, battle.Board.EnemyCount, "one enemy left: the first");
            Assert.AreEqual(0, battle.Board.EnemyAt(0).WaveIndex);
            Assert.AreEqual(3, battle.Gold, "the kill reward only");
            Assert.AreEqual(0, battle.SurvivedWaves, "the first enemy still holds the wave");

            while (battle.Lives == 5 && ticks < 2000)
            {
                battle.Tick();
                ticks++;
                if (battle.Lives == 5) Assert.AreEqual(3, battle.Gold);
            }
            Assert.AreEqual(901, ticks, 2, "the first enemy exits about 15 s after tick 1");
            Assert.AreEqual(3 + 25, battle.Gold, "the bonus on the exit tick");
            Assert.AreEqual(1, battle.SurvivedWaves);
        }

        [Test]
        public void S4a_AC2_Every_Enemy_Of_A_Multi_Enemy_Wave_Carries_Its_Waves_Index()
        {
            Battle battle = Run(new[] { W(Still(), 3, 0.1f, 0f, 1), W(Still(), 2, 0.1f, 0f, 1), Far }, 5, 0);
            Ticks(battle, 40);

            Assert.AreEqual(5, battle.Board.EnemyCount);
            var counts = new int[2];
            for (int i = 0; i < battle.Board.EnemyCount; i++) counts[battle.Board.EnemyAt(i).WaveIndex]++;
            Assert.AreEqual((3, 2), (counts[0], counts[1]));
        }

        [Test]
        public void S4a_AC2_An_Enemy_Spawned_Through_The_Board_Alone_Delays_No_Bonus()
        {
            Battle battle = Run(new[] { W(Runner(31f), 1, 1f, 0f, 25), Far }, 5, 0);
            Enemy stray = battle.Board.SpawnEnemy(Still(0, 1_000_000f));
            Assert.AreEqual(-1, stray.WaveIndex);

            int ticks = 0;
            while (battle.Lives == 5 && ticks < 2000) { battle.Tick(); ticks++; }

            Assert.AreEqual(4, battle.Lives);
            Assert.AreEqual(25, battle.Gold, "paid on the exit tick while the stray is still on the board");
            Assert.AreEqual(1, battle.SurvivedWaves);
            Assert.IsTrue(stray.IsOnBoard);
        }

        [Test]
        public void S4a_AC2_An_Enemy_Object_Reused_By_A_Later_Wave_Does_Not_Hold_Back_Its_Earlier_Wave_And_Counts_For_The_New_One()
        {
            // Wave 1's enemy waits on tick 1; a gun bought afterwards kills it on tick 2. Wave 2 enters six ticks later
            // (0.1 s) on the same object, with 100 health: ten shots.
            Battle battle = Run(new[] { W(Still(0), 1, 1f, 0f, 10), W(Still(0, 100f), 1, 1f, 0.1f, 200), Far }, 5, 0);
            battle.Tick();
            Enemy first = battle.Board.EnemyAt(0);
            Assert.AreEqual(0, first.WaveIndex);
            BuyGun(battle);

            battle.Tick(); // tick 2: killed
            Assert.AreEqual((1, 1, 10), (battle.SurvivedWaves, battle.LastSurvivedWave, battle.Gold));
            Assert.IsFalse(first.IsOnBoard);

            int ticks = 2;
            while (battle.Waves.EnteredWaves < 2 && ticks < 200) { battle.Tick(); ticks++; }
            Assert.AreEqual(2, battle.Waves.EnteredWaves);
            Assert.AreSame(first, battle.Board.EnemyAt(0), "the pooled object came back");
            Assert.AreEqual(1, first.WaveIndex, "now it belongs to wave 2");
            Assert.AreEqual(1, battle.SurvivedWaves, "wave 2 is on the board: wave 1 paid once, wave 2 not yet");
            Assert.AreEqual(10, battle.Gold);

            while (battle.SurvivedWaves == 1 && ticks < 400)
            {
                battle.Tick();
                ticks++;
                if (battle.SurvivedWaves == 1) Assert.AreEqual(1, battle.Board.EnemyCount, "paid only when it is gone");
            }
            Assert.AreEqual((2, 2, 210), (battle.SurvivedWaves, battle.LastSurvivedWave, battle.Gold));
            Assert.AreEqual(0, battle.Board.EnemyCount);
        }

        // ---- 4a-AC3 The ends

        [Test]
        public void S4a_AC3_With_One_Life_The_Only_Enemy_Exiting_Loses_Without_A_Bonus_And_With_Two_Lives_It_Pays()
        {
            Battle one = Run(new[] { W(Runner(31f), 1, 1f, 0f, 25) }, 1, 4);
            Battle two = Run(new[] { W(Runner(31f), 1, 1f, 0f, 25), Far }, 2, 4);

            int ticks = 0;
            while (one.Outcome == RunOutcome.Running && ticks < 2000) { one.Tick(); two.Tick(); ticks++; }

            Assert.AreEqual(RunOutcome.Lost, one.Outcome);
            Assert.AreEqual(4, one.Gold);
            Assert.AreEqual((0, 0), (one.SurvivedWaves, one.LastSurvivedWave));
            Assert.AreEqual(1, two.Lives);
            Assert.AreEqual(29, two.Gold);
            Assert.AreEqual(1, two.SurvivedWaves);
        }

        [Test]
        public void S4a_AC3_The_Tick_That_Takes_The_Last_Life_Pays_Nothing_Even_If_It_Also_Clears_Another_Wave()
        {
            // Wave 1 runs and takes the only life on tick 30. Wave 2 sits still from tick 2; a gun with a short range
            // is bought after tick 29 and kills it on tick 30, the very tick of the loss.
            Battle battle = Run(new[] { W(Runner(31f), 1, 1f, 0f, 10), W(Still(6), 1, 1f, 0f, 100) }, 1, 0);
            Ticks(battle, 29);
            Assert.AreEqual(1, battle.Lives, "the runner is on its last step");
            Assert.AreEqual(2, battle.Board.EnemyCount);
            Assert.AreEqual(0, battle.Gold);
            BuyGun(battle, 2f);

            battle.Tick();

            Assert.AreEqual(RunOutcome.Lost, battle.Outcome);
            Assert.AreEqual(0, battle.Board.EnemyCount, "the still enemy died on the same tick");
            Assert.AreEqual(6, battle.Gold, "the kill reward, and no bonus for either wave");
            Assert.AreEqual((0, 0), (battle.SurvivedWaves, battle.LastSurvivedWave));
        }

        [Test]
        public void S4a_AC3_After_The_Loss_Nothing_Changes()
        {
            Battle battle = Run(new[] { W(Runner(31f), 1, 1f, 0f, 25) }, 1, 4);
            Ticks(battle, 40);
            Assert.AreEqual(RunOutcome.Lost, battle.Outcome);
            int gold = battle.Gold, lives = battle.Lives, survived = battle.SurvivedWaves, last = battle.LastSurvivedWave, enemies = battle.Board.EnemyCount;

            Ticks(battle, 500);

            Assert.AreEqual((gold, lives, survived, last, enemies), (battle.Gold, battle.Lives, battle.SurvivedWaves, battle.LastSurvivedWave, battle.Board.EnemyCount));
            Assert.AreEqual(RunOutcome.Lost, battle.Outcome);
        }

        [Test]
        public void S4a_AC3_The_Last_Wave_Pays_On_The_Tick_That_Wins_And_Later_Ticks_Change_Nothing()
        {
            Battle battle = Run(new[] { W(Still(7), 1, 1f, 0f, 25) }, 3, 2);
            BuyGun(battle);

            battle.Tick();

            Assert.AreEqual(RunOutcome.Won, battle.Outcome);
            Assert.AreEqual(2 + 7 + 25, battle.Gold, "the final gold includes the bonus");
            Assert.AreEqual((1, 1), (battle.SurvivedWaves, battle.LastSurvivedWave));

            Ticks(battle, 300);
            Assert.AreEqual(34, battle.Gold);
            Assert.AreEqual(1, battle.SurvivedWaves);
            Assert.AreEqual(RunOutcome.Won, battle.Outcome);
        }

        [Test]
        public void S4a_AC3_A_Run_Of_Leaks_Ends_Won_With_Every_Bonus_In_The_Final_Gold()
        {
            Battle battle = Run(new[]
            {
                W(Runner(31f), 2, 0.2f, 0f, 10),
                W(Runner(20f), 1, 1f, 0.5f, 20),
                W(Runner(45f), 3, 0.1f, 1f, 30),
            }, 10, 5);

            int ticks = 0;
            while (battle.Outcome == RunOutcome.Running && ticks < 5000) { battle.Tick(); ticks++; }

            Assert.AreEqual(RunOutcome.Won, battle.Outcome);
            Assert.AreEqual(5 + 10 + 20 + 30, battle.Gold);
            Assert.AreEqual(3, battle.SurvivedWaves);
            Assert.AreEqual(10 - 6, battle.Lives);
        }

        // ---- 4a-AC7 Core and determinism

        private static Wave[] DeterminismWaves() => new[]
        {
            W(new EnemyStats(4f, 20f, 3), 6, 0.3f, 0f, 15),
            W(new EnemyStats(9f, 10f, 2), 4, 0.2f, 2f, 25),
            W(new EnemyStats(2f, 40f, 6), 3, 0.6f, 1f, 0),
            W(new EnemyStats(12f, 10f, 1), 5, 0.1f, 1f, 35),
        };

        private static List<int> GoldPerTick(out Battle battle)
        {
            battle = new Battle(OpenBoard(), DeterminismWaves(), 50, 6, 50);
            var golds = new List<int>();
            for (int i = 0; i < 3000 && battle.Outcome == RunOutcome.Running; i++)
            {
                if (i == 10) battle.TryBuyTower(Near, new TowerStats(6f, 10f, 2f, 20f, 5));
                battle.Tick();
                Assert.GreaterOrEqual(battle.Gold, 0, $"tick {i + 1}");
                golds.Add(battle.Gold);
            }
            return golds;
        }

        [Test]
        public void S4a_AC7_Two_Runs_With_Bonuses_Have_The_Same_Gold_After_Every_Tick()
        {
            List<int> a = GoldPerTick(out Battle first);
            List<int> b = GoldPerTick(out Battle second);

            Assert.AreEqual(a, b);
            Assert.AreEqual(first.SurvivedWaves, second.SurvivedWaves);
            Assert.AreEqual(first.LastSurvivedWave, second.LastSurvivedWave);
            Assert.AreEqual(RunOutcome.Won, first.Outcome, "the run is long enough to end");
            Assert.AreEqual(4, first.SurvivedWaves, "the bonuses were part of the compared run");
        }

        // ---- 4a-AC8 Allocation

        [Test]
        public void S4a_AC8_Tick_Allocates_Nothing_On_Ticks_That_Pay_A_Bonus()
        {
            var runner = new EnemyStats(6f, 10f, 0);
            var waves = new List<Wave>();
            for (int i = 0; i < 40; i++) waves.Add(new Wave(runner, 3, 0.3f, i == 0 ? 0f : 1f, 5 + i));
            var battle = new Battle(OpenBoard(), waves.ToArray(), 100_000, 0, 50);
            Ticks(battle, 1000); // warm-up: the board fills to its peak and empties several times

            int survivedBefore = battle.SurvivedWaves;
            Assert.Greater(survivedBefore, 0, "the warm-up already paid bonuses");
            TickAllocation.AssertNoneAcross(battle, 1500);

            Assert.Greater(battle.SurvivedWaves - survivedBefore, 3, "bonuses were paid during the measured ticks");
            Assert.AreEqual(RunOutcome.Running, battle.Outcome);
        }
    }
}
