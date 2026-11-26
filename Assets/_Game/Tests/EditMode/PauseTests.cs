using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using static ElementalTD.Core.Tests.PerkKit;

namespace ElementalTD.Core.Tests
{
    /// <summary>Step 5b, 5b-AC4 (the pause) and 5b-AC5 (the pick): the pause pin tick by tick with its launch ticks, in Unity's Mono.</summary>
    public class PauseTests
    {
        private static readonly Wave[] LongDelay = { new Wave(new EnemyStats(1f, 10f), 1, 1f, 1_000_000f) };

        /// <summary>One tick, recording the tick of every launch (a landing keeps the sum, a launch adds one) and of every entry.</summary>
        private sealed class Recorder
        {
            public readonly Battle Battle;
            public readonly List<int> Launches = new List<int>();
            public readonly List<int> Entries = new List<int>();
            public readonly List<(int tick, float amount)> Hits = new List<(int, float)>();
            private int _seenHits;
            private int _enemiesEver;

            public Recorder(Battle battle) { Battle = battle; }

            public void Tick()
            {
                int before = Battle.ProjectileCount + Battle.HitCount;
                int enemiesBefore = Battle.Board.EnemyCount;
                Battle.Tick();
                if (Battle.ProjectileCount + Battle.HitCount > before) Launches.Add(Battle.TickCount);
                // An entry shows as one more enemy on the board, or as a spawn the same tick as a kill (not in the pin).
                if (Battle.Board.EnemyCount > enemiesBefore) Entries.Add(Battle.TickCount);
                while (_seenHits < Battle.HitCount)
                {
                    Hit hit = Battle.HitAt(_seenHits++);
                    Hits.Add((hit.Tick, hit.Amount));
                }
            }

            public void TickUntilChoiceOrEnd(int max = 1000)
            {
                for (int i = 0; i < max && !Battle.AwaitingChoice && Battle.Outcome == RunOutcome.Running; i++) Tick();
            }
        }

        // ---- The pause pin

        [Test]
        public void S5b_AC4_The_Pause_Pin_Tick_By_Tick_With_Its_Launch_Ticks()
        {
            Battle battle = PausePin(out Board board);
            var run = new Recorder(battle);

            // Ticks 1 to 95: enter 1; launches 1, 40, 80; hits on 12, 49, 95 for 20 (Light x2 on Shadow).
            run.TickUntilChoiceOrEnd();
            Assert.AreEqual(95, battle.TickCount, "the choice opens on tick 95");
            CollectionAssert.AreEqual(new[] { 1 }, run.Entries, "enemy 1 enters on tick 1");
            CollectionAssert.AreEqual(new[] { 1, 40, 80 }, run.Launches, "launch ticks before the pause");
            CollectionAssert.AreEqual(new[] { 12, 49, 95 }, run.Hits.ConvertAll(h => h.tick), "landing ticks");
            Assert.AreEqual(new[] { 20f, 20f, 20f }, run.Hits.ConvertAll(h => h.amount).ToArray());
            Assert.IsTrue(battle.AwaitingChoice);
            Assert.AreEqual(17, battle.Gold, "0 + 10 + the wave bonus 7: the choice sees the tick's bonus");
            Assert.AreEqual(1, battle.SurvivedWaves);
            Assert.AreEqual((10, 2), (battle.Progression.Xp, battle.Progression.Level));
            CollectionAssert.AreEqual(new[] { 2, 1, 0 }, Offer(battle.Progression));
            Assert.AreEqual(0.4167f, battle.Waves.SecondsToNextWave, 0.0001f);
            float countdown = battle.Waves.SecondsToNextWave;
            Assert.AreEqual(3, battle.HitCount);
            Assert.AreEqual((2, 1, 1), (battle.Progression.ChoiceLevel, battle.Progression.ChoiceNumber, battle.Progression.ChoicesInARow));

            // Any number of Tick() calls: nothing moves.
            for (int i = 0; i < 500; i++) battle.Tick();
            Assert.AreEqual(95, battle.TickCount, "the tick count");
            Assert.AreEqual(17, battle.Gold);
            Assert.AreEqual(countdown, battle.Waves.SecondsToNextWave, 0f, "the countdown");
            Assert.AreEqual(3, battle.HitCount, "the hit count");
            Assert.AreEqual(0, board.EnemyCount);
            Assert.AreEqual(0, battle.ProjectileCount);
            Assert.AreEqual(1, battle.Waves.StartedWaves);

            // Pick slot 2 (perk 0): damage 12.5; the next tick is 96.
            battle.PickPerk(2);
            Assert.IsFalse(battle.AwaitingChoice);
            Assert.AreEqual(12.5f, board.TowerOn(new GridPos(2, 3)).Stats.Damage, 0f);
            Assert.AreEqual(10f, board.TowerOn(new GridPos(2, 3)).BaseStats.Damage, 0f);
            run.Tick();
            Assert.AreEqual(96, battle.TickCount, "the next Tick() is tick 96");

            // Second choice: enemy 2 enters on 121; launches 121 and 160 land on 132 and 169 for 25; gold 27, XP 20, level 3.
            run.TickUntilChoiceOrEnd();
            Assert.AreEqual(169, battle.TickCount);
            Assert.AreEqual(new[] { 1, 121 }, run.Entries.ToArray(), "enemy 2 enters on 121");
            Assert.AreEqual(new[] { 1, 40, 80, 121, 160 }, run.Launches.ToArray());
            Assert.AreEqual(new[] { 12, 49, 95, 132, 169 }, run.Hits.ConvertAll(h => h.tick).ToArray());
            Assert.AreEqual(new[] { 25f, 25f }, run.Hits.GetRange(3, 2).ConvertAll(h => h.amount).ToArray());
            Assert.AreEqual(27, battle.Gold);
            Assert.AreEqual((20, 3), (battle.Progression.Xp, battle.Progression.Level));
            Assert.AreEqual(1, battle.SurvivedWaves, "wave 2 is not survived");
            CollectionAssert.AreEqual(new[] { 1, 2, 0 }, Offer(battle.Progression));
            for (int i = 0; i < 100; i++) battle.Tick();
            Assert.AreEqual((169, 27), (battle.TickCount, battle.Gold), "stands still again");

            battle.PickPerk(1);   // perk 2: gold +2
            Assert.AreEqual(2, battle.Modifiers.Count);
            run.TickUntilChoiceOrEnd();

            // Enemy 3 enters on 181; launches 200 and 240 land on 208 and 251; the kill pays 10 + 2, the wave 9.
            Assert.AreEqual(251, battle.TickCount);
            Assert.AreEqual(new[] { 1, 121, 181 }, run.Entries.ToArray());
            Assert.AreEqual(new[] { 1, 40, 80, 121, 160, 200, 240 }, run.Launches.ToArray(), "every launch tick of the run");
            Assert.AreEqual(new[] { 12, 49, 95, 132, 169, 208, 251 }, run.Hits.ConvertAll(h => h.tick).ToArray());
            Assert.AreEqual(new[] { 25f, 25f }, run.Hits.GetRange(5, 2).ConvertAll(h => h.amount).ToArray());
            Assert.AreEqual(48, battle.Gold, "27 + (10 + 2) + 9");
            Assert.AreEqual(RunOutcome.Won, battle.Outcome);
            Assert.AreEqual(10, battle.Lives);
            Assert.AreEqual((30, 4), (battle.Progression.Xp, battle.Progression.Level));
            Assert.IsFalse(battle.AwaitingChoice, "a level gained on the winning tick opens no choice");
            Assert.AreEqual(2, battle.Progression.ChoicesOpened, "two opened in the run");
            Assert.AreEqual(2, battle.SurvivedWaves);
        }

        [Test]
        public void S5b_AC4_A_Tick_While_Open_Moves_No_Enemy_No_Shot_And_No_Cooldown_The_Launch_Comes_When_It_Would_Have_Without_The_Pause()
        {
            // The pin run, but the choice is left open for ticks of "waiting": the launches must fall on 121 and 160 all the same,
            // which only the launch ticks can tell (the cooldown quotient has no explicit float cast).
            Battle waited = PausePin(out Board boardWaited);
            var run = new Recorder(waited);
            run.TickUntilChoiceOrEnd();
            for (int i = 0; i < 3000; i++) waited.Tick();
            waited.PickPerk(2);
            run.TickUntilChoiceOrEnd();
            Assert.AreEqual(new[] { 1, 40, 80, 121, 160 }, run.Launches.ToArray());
        }

        [Test]
        public void S5b_AC4_While_Open_Nothing_Of_The_Battle_Moves_Enemies_Shots_Tower_Gold_Lives_And_The_Hit_Log()
        {
            var board = OpenBoard();
            var waves = new[] { new Wave(new EnemyStats(1f, 100000f, 10, default, 1), 3, 1f, 0f, 5) };
            var battle = new Battle(board, waves, 10, 100, 100, null, new Progression(new[] { 1, 1 }, PausePool(), 8));
            battle.TryBuyTower(new GridPos(1, 4), new TowerStats(20f, 10f, 3f, 2f, 40, Light));
            for (int i = 0; i < 100; i++) battle.Tick();
            Assert.Greater(board.EnemyCount, 0);
            Assert.Greater(battle.ProjectileCount, 0, "a shot is in flight at the pause");
            battle.Progression.AddXp(1);
            battle.Tick();
            Assert.IsTrue(battle.AwaitingChoice);

            string before = Snapshot(battle);
            for (int i = 0; i < 400; i++) battle.Tick();
            Assert.AreEqual(before, Snapshot(battle), "nothing moved in 400 calls");
            battle.PickPerk(0);
            for (int i = 0; i < 5; i++) battle.Tick();
            Assert.AreNotEqual(before, Snapshot(battle), "it moves again after the pick");
        }

        private static string Snapshot(Battle battle)
        {
            var text = new StringBuilder();
            text.Append(battle.TickCount).Append('|').Append(battle.Gold).Append('|').Append(battle.Lives).Append('|')
                .Append(battle.HitCount).Append('|').Append(battle.ProjectileCount).Append('|').Append(battle.Outcome).Append('|')
                .Append(battle.SurvivedWaves).Append('|').Append(battle.Waves.SecondsToNextWave.ToString("R")).Append('|')
                .Append(battle.Progression.Xp).Append('|').Append(battle.Progression.Level).Append('|')
                .Append(battle.Progression.ChoicesOpened).Append('|').Append(battle.Modifiers.Count).Append('|');
            for (int i = 0; i < battle.Board.EnemyCount; i++)
            {
                Enemy e = battle.Board.EnemyAt(i);
                text.Append(e.X.ToString("R")).Append(',').Append(e.Y.ToString("R")).Append(',').Append(e.Health.ToString("R")).Append(';');
            }
            for (int i = 0; i < battle.ProjectileCount; i++)
            {
                Projectile p = battle.ProjectileAt(i);
                text.Append(p.X.ToString("R")).Append(',').Append(p.Y.ToString("R")).Append(';');
            }
            for (int i = 0; i < battle.Board.TowerCount; i++)
            {
                TowerStats s = battle.Board.TowerAt(i).Stats;
                text.Append(s.Damage.ToString("R")).Append(',').Append(s.ShotsPerSecond.ToString("R")).Append(',').Append(s.Range.ToString("R")).Append(';');
            }
            for (int i = Math.Max(0, battle.HitCount - Battle.HitLogCapacity); i < battle.HitCount; i++)
            {
                Hit h = battle.HitAt(i);
                text.Append(h.Tick).Append(',').Append(h.Amount.ToString("R")).Append(',').Append(h.Kind).Append(';');
            }
            return text.ToString();
        }

        [Test]
        public void S5b_AC4_A_Battle_With_No_Pool_Never_Opens_A_Choice_And_Counts_Every_Tick_Of_The_Pause_Pins_Run()
        {
            Battle noPool = PausePin(out _, pool: Array.Empty<Modifier>());
            int calls = 0;
            while (noPool.Outcome == RunOutcome.Running && calls < 2000)
            {
                int before = noPool.TickCount;
                noPool.Tick();
                calls++;
                Assert.AreEqual(before + 1, noPool.TickCount, $"call {calls}: a tick counted");
                Assert.IsFalse(noPool.AwaitingChoice);
            }
            Assert.AreEqual(RunOutcome.Won, noPool.Outcome);
            Assert.AreEqual(0, noPool.Progression.ChoicesOpened);
            Assert.AreEqual(1 + noPool.Progression.Xp / 10, noPool.Progression.Level, "the level still rises with the XP");
            Assert.Greater(noPool.Progression.Level, 1);
        }

        private static Modifier[] ShadowOnlyPool() => new[]
        {
            Mod(Stat.TowerDamage, Operation.Multiply, 1.25f, Shadow),
            Mod(Stat.TowerShotsPerSecond, Operation.Multiply, 1.15f, Shadow),
            Mod(Stat.TowerRange, Operation.Add, 0.5f, Shadow),
        };

        [Test]
        public void S5b_AC5_After_A_Pick_Of_A_Perk_That_Touches_No_Tower_Every_Later_Tick_Equals_The_Run_With_No_Pool()
        {
            Battle noPool = PausePin(out _, pool: Array.Empty<Modifier>());
            Battle shadowPool = PausePin(out _, pool: ShadowOnlyPool());
            int picks = 0;
            for (int i = 0; i < 3000 && noPool.Outcome == RunOutcome.Running; i++)
            {
                noPool.Tick();
                shadowPool.Tick();
                if (shadowPool.AwaitingChoice)
                {
                    { shadowPool.PickPerk(picks % 3); picks++; }
                }
                // The choice counts differ by design; everything of the battle itself must agree.
                Assert.AreEqual(Core(noPool), Core(shadowPool), $"after call {i + 1}");
            }
            Assert.AreEqual(2, picks, "two choices were answered in the run with a pool");
            Assert.AreEqual(RunOutcome.Won, shadowPool.Outcome);
            Assert.AreEqual(RunOutcome.Won, noPool.Outcome);
        }

        private static string Core(Battle battle)
        {
            string[] parts = Snapshot(battle).Split('|');
            // Drop the progression-only fields: ChoicesOpened (10) and Modifiers.Count (11).
            parts[10] = parts[11] = "";
            return string.Join("|", parts);
        }

        [Test]
        public void S5b_AC4_A_Level_On_The_Winning_Tick_Opens_No_Choice_And_The_Battle_Stays_Won()
        {
            var waves = new[] { new Wave(new EnemyStats(0f, 10f, 10, default, 10), 1, 1f, 0f, 0) };
            var battle = new Battle(OpenBoard(), waves, 10, 100, 100, null, new Progression(new[] { 10, 10, 10 }, PausePool(), 5));
            battle.TryBuyTower(new GridPos(1, 4), new TowerStats(5f, 10f, 1f, 100f, 40));
            for (int i = 0; i < 20 && battle.Outcome == RunOutcome.Running; i++) battle.Tick();
            Assert.AreEqual(RunOutcome.Won, battle.Outcome);
            Assert.AreEqual(2, battle.Progression.Level, "the level was gained on the tick that won");
            Assert.IsFalse(battle.AwaitingChoice);
            Assert.AreEqual(0, battle.Progression.ChoicesOpened);
            int ticks = battle.TickCount;
            battle.Tick();
            Assert.AreEqual(ticks, battle.TickCount);
            Assert.IsFalse(battle.AwaitingChoice, "and none opens afterwards");
        }

        [Test]
        public void S5b_AC4_A_Level_On_The_Tick_That_Takes_The_Last_Life_Opens_No_Choice()
        {
            // One cell per tick along the row: the level is gained just before the tick on which the enemy leaves.
            var waves = new[] { new Wave(new EnemyStats(60f, 10f), 1, 1f, 0f, 0) };
            var battle = new Battle(OpenBoard(), waves, 1, 0, 100, null, new Progression(new[] { 5 }, PausePool(), 5));
            bool gained = false;
            for (int i = 0; i < 100 && battle.Outcome == RunOutcome.Running; i++)
            {
                if (!gained && battle.Board.EnemyCount == 1 && battle.Board.EnemyAt(0).DistanceToExit <= 1.001f)
                {
                    battle.Progression.AddXp(5);
                    gained = true;
                }
                battle.Tick();
            }
            Assert.IsTrue(gained, "the level was gained before the losing tick");
            Assert.AreEqual(2, battle.Progression.Level);
            Assert.AreEqual(RunOutcome.Lost, battle.Outcome);
            Assert.IsFalse(battle.AwaitingChoice);
            Assert.AreEqual(0, battle.Progression.ChoicesOpened);
            int ticks = battle.TickCount;
            battle.Tick();
            Assert.AreEqual(ticks, battle.TickCount);
            Assert.IsFalse(battle.AwaitingChoice);
        }

        [Test]
        public void S5b_AC4_A_Level_Gained_On_A_Tick_That_Leaves_The_Run_Running_Opens_The_Choice_At_The_End_Of_That_Same_Tick()
        {
            Battle battle = PausePin(out _);
            battle.Progression.AddXp(10);
            Assert.IsFalse(battle.AwaitingChoice, "nothing opens between ticks");
            battle.Tick();
            Assert.IsTrue(battle.AwaitingChoice);
            Assert.AreEqual(1, battle.TickCount);
        }

        // ---- 5b-AC5 The pick

        [Test]
        public void S5b_AC5_Two_And_Three_Levels_From_One_AddXp_Open_Choices_For_Their_Levels_In_Order_Each_With_Its_Own_Draw()
        {
            var battle = new Battle(OpenBoard(), LongDelay, 10, 0, 100, null,
                new Progression(new[] { 1, 1, 1 }, Pool5(), 1));
            Progression p = battle.Progression;
            Assert.AreEqual((0, 0, 0, 0), (p.ChoiceLevel, p.ChoiceNumber, p.ChoicesInARow, p.ChoicesOpened), "nothing open at first");

            p.AddXp(3);
            battle.Tick();
            Assert.AreEqual((2, 1, 3, 1), (p.ChoiceLevel, p.ChoiceNumber, p.ChoicesInARow, p.ChoicesOpened));
            CollectionAssert.AreEqual(new[] { 2, 1, 3 }, Offer(p));
            battle.PickPerk(0);    // perk 2
            Assert.IsTrue(battle.AwaitingChoice, "the next choice opens at once, with no tick");
            Assert.AreEqual((3, 2, 3, 2), (p.ChoiceLevel, p.ChoiceNumber, p.ChoicesInARow, p.ChoicesOpened));
            CollectionAssert.AreEqual(new[] { 2, 0, 4 }, Offer(p), "its own draw, not the first offer again");
            battle.PickPerk(2);    // perk 4
            Assert.AreEqual((4, 3, 3, 3), (p.ChoiceLevel, p.ChoiceNumber, p.ChoicesInARow, p.ChoicesOpened));
            CollectionAssert.AreEqual(new[] { 4, 2, 1 }, Offer(p));
            battle.PickPerk(1);    // perk 2 again
            Assert.IsFalse(battle.AwaitingChoice);
            Assert.AreEqual((0, 0, 0, 3), (p.ChoiceLevel, p.ChoiceNumber, p.ChoicesInARow, p.ChoicesOpened));

            Assert.AreEqual(new[] { 0, 0, 2, 0, 1 }, new[] { p.TimesTaken(0), p.TimesTaken(1), p.TimesTaken(2), p.TimesTaken(3), p.TimesTaken(4) },
                "taken by place: perk 2 twice (slots 0 and 1), perk 4 once");
            Assert.AreEqual(3, battle.Modifiers.Count);
            Assert.AreEqual(new[] { 2, 4, 2 }, new[] { battle.Modifiers.At(0).Source, battle.Modifiers.At(1).Source, battle.Modifiers.At(2).Source },
                "the perk's place is the modifier's source, not the slot");
        }

        private static Modifier[] Pool5() => PinPool();

        [Test]
        public void S5b_AC5_Two_Levels_At_Once_And_One_Level_Gained_While_A_Row_Is_Open_Are_Counted()
        {
            var battle = new Battle(OpenBoard(), LongDelay, 10, 0, 100, null, new Progression(new[] { 1, 1, 1 }, Pool5(), 1));
            Progression p = battle.Progression;
            p.AddXp(2);
            battle.Tick();
            Assert.AreEqual((2, 1, 2), (p.ChoiceLevel, p.ChoiceNumber, p.ChoicesInARow));
            p.AddXp(1);   // a level gained while the row is open
            battle.PickPerk(0);
            Assert.AreEqual((3, 2, 3), (p.ChoiceLevel, p.ChoiceNumber, p.ChoicesInARow), "the row now holds three");
            battle.PickPerk(0);
            Assert.AreEqual((4, 3, 3), (p.ChoiceLevel, p.ChoiceNumber, p.ChoicesInARow));
            battle.PickPerk(0);
            Assert.IsFalse(battle.AwaitingChoice);
        }

        [Test]
        public void S5b_AC5_A_Level_Owed_Opens_The_Next_Choice_On_The_Next_Tick_Not_On_The_Pick_When_Gained_After_The_Row_Closed()
        {
            var battle = new Battle(OpenBoard(), LongDelay, 10, 0, 100, null, new Progression(new[] { 1, 1, 1 }, Pool5(), 1));
            battle.Progression.AddXp(1);
            battle.Tick();
            battle.PickPerk(0);
            Assert.IsFalse(battle.AwaitingChoice);
            battle.Progression.AddXp(1);
            Assert.IsFalse(battle.AwaitingChoice, "a level gained between ticks opens at the end of the next tick");
            battle.Tick();
            Assert.IsTrue(battle.AwaitingChoice);
            Assert.AreEqual((3, 1, 1), (battle.Progression.ChoiceLevel, battle.Progression.ChoiceNumber, battle.Progression.ChoicesInARow));
            CollectionAssert.AreEqual(new[] { 2, 0, 4 }, Offer(battle.Progression), "the second draw of the run");
        }

        [Test]
        public void S5b_AC5_PickPerk_With_No_Choice_Open_And_With_A_Slot_Outside_The_Offer_Throws_And_Changes_Nothing()
        {
            var battle = new Battle(OpenBoard(), LongDelay, 10, 0, 100, null, new Progression(new[] { 1, 1 }, Pool5(), 1));
            Assert.Throws<InvalidOperationException>(() => battle.PickPerk(0), "no choice open");
            Assert.AreEqual(0, battle.Modifiers.Count);
            Assert.Throws<InvalidOperationException>(() => battle.Progression.OfferAt(0), "no offer without a choice");

            battle.Progression.AddXp(2);
            battle.Tick();
            Assert.Throws<ArgumentOutOfRangeException>(() => battle.PickPerk(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => battle.PickPerk(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => battle.Progression.OfferAt(3));
            Assert.IsTrue(battle.AwaitingChoice, "a failed pick does not close the choice");
            Assert.AreEqual(0, battle.Modifiers.Count);
            Assert.AreEqual(0, battle.Progression.TimesTaken(2));
            CollectionAssert.AreEqual(new[] { 2, 1, 3 }, Offer(battle.Progression), "nor change the offer");
            Assert.AreEqual((2, 1, 2, 1), (battle.Progression.ChoiceLevel, battle.Progression.ChoiceNumber, battle.Progression.ChoicesInARow, battle.Progression.ChoicesOpened));

            battle.PickPerk(0);
            CollectionAssert.AreEqual(new[] { 2, 0, 4 }, Offer(battle.Progression), "a failed pick took no number: the next offer is the pin's second draw");
            battle.PickPerk(0);
            Assert.Throws<InvalidOperationException>(() => battle.PickPerk(0), "answered: no choice open again");
        }

        [Test]
        public void S5b_AC5_A_Battle_With_No_Pool_Refuses_PickPerk()
        {
            var battle = new Battle(OpenBoard(), LongDelay, 10);
            Assert.Throws<InvalidOperationException>(() => battle.PickPerk(0));
        }

        [Test]
        public void S5b_AC5_The_Perk_Joins_The_Modifiers_With_Its_Place_As_Source_And_The_Slot_Is_Not_The_Place()
        {
            Battle battle = PausePin(out _);
            battle.Progression.AddXp(10);
            battle.Tick();
            CollectionAssert.AreEqual(new[] { 2, 1, 0 }, Offer(battle.Progression));
            battle.PickPerk(0);   // slot 0 is perk 2, gold +2
            Assert.AreEqual(1, battle.Modifiers.Count);
            Assert.AreEqual((Stat.GoldPerKill, 2), (battle.Modifiers.At(0).Stat, battle.Modifiers.At(0).Source));
            Assert.AreEqual(12.5f - 2.5f, battle.Board.TowerOn(new GridPos(2, 3)).Stats.Damage, 0f, "a gold perk leaves the tower alone");
            Assert.AreEqual(1, battle.Progression.TimesTaken(2));
            Assert.AreEqual(0, battle.Progression.TimesTaken(0));
        }

        // ---- 5b-AC16, Core part

        [Test]
        public void S5b_AC16_Tick_Allocates_Nothing_Also_On_The_Tick_That_Opens_A_Choice_And_On_A_Kill_With_Gold_Perks()
        {
            Battle battle = PausePin(out _);
            Battle control = PausePin(out _, pool: Array.Empty<Modifier>());   // the same kills on the same ticks, no choice
            for (int i = 0; i < 94; i++) { battle.Tick(); control.Tick(); }
            // Tick 95 opens the first choice and is the first kill: the enemy pool's free list may grow on that first removal, in 5a too.
            int withChoice = TickAllocation.AllocationsOf(() => battle.Tick());
            int withoutChoice = TickAllocation.AllocationsOf(() => control.Tick());
            Assert.IsTrue(battle.AwaitingChoice);
            Assert.AreEqual(withoutChoice, withChoice, "opening the choice (the draw included) allocates nothing beyond what the same kill does with no pool");
            battle.PickPerk(2);
            for (int i = 0; i < 5; i++) battle.Tick();
            // From here every pool has its size: the second choice, the gold perk's kill and the win allocate nothing.
            TickAllocation.AssertNoneAcross(battle, 160, afterTick: _ =>
            {
                if (battle.AwaitingChoice) battle.PickPerk(1);
            });
            Assert.AreEqual(2, battle.Progression.ChoicesOpened);
            Assert.AreEqual(RunOutcome.Won, battle.Outcome);
            Assert.AreEqual(48, battle.Gold, "the run played as the pin, gold perk on the last kill included");
        }

        [Test]
        public void S5b_AC16_Apply_And_ApplyWhole_And_The_Draw_Allocate_Nothing()
        {
            var modifiers = new Modifiers();
            foreach (Modifier m in PinPool()) modifiers.Add(m);
            var stats = Tower(Light);
            modifiers.Apply(stats);
            modifiers.ApplyWhole(Stat.GoldPerKill, 10);
            modifiers.Apply(Stat.TowerDamage, Light, 10f);
            Assert.AreEqual(0, TickAllocation.AllocationsOf(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    modifiers.Apply(stats);
                    modifiers.ApplyWhole(Stat.GoldPerKill, 10);
                    modifiers.Apply(Stat.TowerRange, Shadow, 2.5f);
                }
            }));

            Battle battle;
            var table = new int[50];
            for (int i = 0; i < table.Length; i++) table[i] = 1;
            battle = new Battle(OpenBoard(), LongDelay, 10, 0, 100, null, new Progression(table, Pool5(), 1));
            battle.Progression.AddXp(50);
            battle.Tick();
            // A pick grows the list of modifiers (capacity 4, 8, 16, 32); the draw it opens allocates nothing.
            for (int i = 0; i < 25; i++)
            {
                int count = battle.Modifiers.Count;
                int allocations = TickAllocation.AllocationsOf(() => battle.PickPerk(0));
                bool growth = count == 0 || count == 4 || count == 8 || count == 16;
                Assert.AreEqual(growth ? 1 : 0, allocations, $"pick {i + 1} (modifiers before: {count})");
            }
        }

        [Test]
        public void S5b_AC16_Two_Runs_On_One_Seed_And_Picks_Agree_On_Every_Tick_Offers_Picks_And_Tower_Stats_Included()
        {
            string Play(uint seed)
            {
                Battle battle = PausePin(out _, seed: seed);
                var log = new StringBuilder();
                for (int i = 0; i < 300 && battle.Outcome == RunOutcome.Running; i++)
                {
                    battle.Tick();
                    if (battle.AwaitingChoice)
                    {
                        log.Append('[').Append(string.Join(",", Offer(battle.Progression))).Append(']');
                        battle.PickPerk(i % 3);
                        log.Append('p').Append(i % 3);
                    }
                    log.Append(Snapshot(battle)).Append('\n');
                }
                return log.ToString();
            }

            Assert.AreEqual(Play(2), Play(2));
            Assert.AreNotEqual(Play(2), Play(3), "another seed offers other cards");
        }
    }
}
