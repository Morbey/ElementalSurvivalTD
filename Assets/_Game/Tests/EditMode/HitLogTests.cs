using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ElementalTD.Core.Tests
{
    /// <summary>The tick count, the kind of a hit, the hit log and the immunity mark (plan 0009, 4d-AC1 to AC5).</summary>
    public class HitLogTests
    {
        private static readonly Element Light = new Element(1);
        private static readonly Element Shadow = new Element(2);
        private static readonly GridPos Spawn = new GridPos(0, 4);

        // Never starts a wave: enemies come through Board.SpawnEnemy.
        private static readonly Wave[] Idle = { new Wave(new EnemyStats(1f, 10f, 5), 1, 1f, 1_000_000f) };

        private static ElementChart LightShadow(float lightAgainstShadow = 2f)
        {
            var chart = new ElementChart(2);
            chart.Set(Light, Shadow, lightAgainstShadow);
            chart.Set(Shadow, Light, 2f);
            return chart;
        }

        private static Board OpenBoard() => new Board(new GridMap(16, 9, new List<GridPos>()), Spawn, new GridPos(15, 4));

        private static Battle Parked(Board board, ElementChart chart, int lives = 10) => new Battle(board, Idle, lives, 0, 100, chart);

        // Speed 100 cells a second crosses 1.67 cells a tick, so a tower beside the spawn lands on its launch tick.
        private static TowerStats Quick(Element element, float damage = 10f, float shotsPerSecond = 2f) =>
            new TowerStats(20f, damage, shotsPerSecond, 100f, 0, element);

        private static EnemyStats Still(Element element, float health = 100_000f) => new EnemyStats(0f, health, 0, element);

        private static void Ticks(Battle battle, int count)
        {
            for (int i = 0; i < count; i++) battle.Tick();
        }

        private static void Buy(Battle battle, int x, int y, in TowerStats stats) =>
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(new GridPos(x, y), stats));

        /// <summary>Ticks until a record is added; returns its index.</summary>
        private static int TickToNextRecord(Battle battle)
        {
            int before = battle.HitCount;
            for (int i = 0; i < 200; i++)
            {
                battle.Tick();
                if (battle.HitCount > before) return before;
            }
            Assert.Fail("no shot landed within 200 ticks");
            return -1;
        }

        private static void AssertSameHit(Hit expected, Hit actual, string what)
        {
            Assert.AreEqual(expected.X, actual.X, what + " X");
            Assert.AreEqual(expected.Y, actual.Y, what + " Y");
            Assert.AreEqual(expected.Amount, actual.Amount, what + " amount");
            Assert.AreEqual(expected.Kind, actual.Kind, what + " kind");
            Assert.AreEqual(expected.Tick, actual.Tick, what + " tick");
            Assert.AreEqual(expected.TargetEntry, actual.TargetEntry, what + " entry");
        }

        // ---- 4d-AC1 tick count

        [Test]
        public void S4d_AC1_The_Count_Is_0_Before_The_First_Tick_And_N_After_N_Ticks()
        {
            Battle battle = Parked(OpenBoard(), null);
            Assert.AreEqual(0, battle.TickCount);
            for (int n = 1; n <= 50; n++)
            {
                battle.Tick();
                Assert.AreEqual(n, battle.TickCount);
            }
        }

        [TestCase(1, RunOutcome.Lost)]
        [TestCase(2, RunOutcome.Won)]
        public void S4d_AC1_A_Finished_Battle_Counts_No_More_After_A_Loss_Or_A_Win(int lives, RunOutcome expected)
        {
            // One fast enemy walks to the exit at once: with one life that is a loss, with two the last wave is survived.
            var waves = new[] { new Wave(new EnemyStats(1000f, 10f), 1, 1f, 0f) };
            var battle = new Battle(OpenBoard(), waves, lives);
            int guard = 0;
            while (battle.Outcome == RunOutcome.Running && guard++ < 1000) battle.Tick();
            Assert.AreEqual(expected, battle.Outcome);

            int count = battle.TickCount, hits = battle.HitCount;
            Assert.Greater(count, 0);
            Ticks(battle, 10);
            Assert.AreEqual(count, battle.TickCount, "a finished battle counts no more");
            Assert.AreEqual(hits, battle.HitCount);
        }

        // ---- 4d-AC2 kind of a hit

        [TestCase(0f, HitKind.Immune)]
        [TestCase(0.25f, HitKind.Weak)]
        [TestCase(0.75f, HitKind.Weak)]
        [TestCase(0.9999f, HitKind.Weak)]
        [TestCase(1f, HitKind.Normal)]
        [TestCase(1.0001f, HitKind.Strong)]
        [TestCase(2f, HitKind.Strong)]
        [TestCase(1000f, HitKind.Strong)]
        public void S4d_AC2_KindOf_Names_The_Multiplier(float multiplier, HitKind expected) =>
            Assert.AreEqual(expected, ElementChart.KindOf(multiplier));

        [Test]
        public void S4d_AC2_No_File_Under_Presentation_Or_Gameplay_Reads_A_Multiplier()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "_Game"));
            int scanned = 0;
            foreach (string folder in new[] { "Presentation", "Gameplay" })
                foreach (string file in Directory.GetFiles(Path.Combine(root, folder), "*.cs", SearchOption.AllDirectories))
                {
                    scanned++;
                    StringAssert.DoesNotContain("Multiplier", File.ReadAllText(file), file);
                }
            Assert.Greater(scanned, 10);
        }

        [Test]
        public void S4d_AC2_No_Element_Against_No_Element_Is_Normal_And_Never_Immune()
        {
            // Mutation: the kind taken from element equality would call two empty elements immune.
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow());
            Buy(battle, 1, 3, Quick(default));
            Enemy enemy = board.SpawnEnemy(Still(default));

            int index = TickToNextRecord(battle);

            Hit hit = battle.HitAt(index);
            Assert.AreEqual(HitKind.Normal, hit.Kind);
            Assert.AreEqual(10f, hit.Amount);
            Assert.AreEqual(0, enemy.ImmuneHitTick);
        }

        [Test]
        public void S4d_AC2_No_Element_Against_An_Element_And_An_Element_Against_None_Are_Normal()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow());
            Buy(battle, 1, 3, Quick(default));
            board.SpawnEnemy(Still(Light));
            Assert.AreEqual(HitKind.Normal, battle.HitAt(TickToNextRecord(battle)).Kind);

            Board other = OpenBoard();
            Battle second = Parked(other, LightShadow());
            Buy(second, 1, 3, Quick(Light));
            other.SpawnEnemy(Still(default));
            Assert.AreEqual(HitKind.Normal, second.HitAt(TickToNextRecord(second)).Kind);
        }

        // ---- 4d-AC3 hit log

        [Test]
        public void S4d_AC3_At_X2_A_Shot_Of_10_Adds_A_Strong_Record_Of_20_At_The_Targets_Position_On_The_Tick()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow());
            Buy(battle, 1, 3, Quick(Light));
            Enemy enemy = board.SpawnEnemy(Still(Shadow));
            Assert.AreEqual(0, battle.HitCount);

            battle.Tick();

            Assert.AreEqual(1, battle.HitCount);
            Hit hit = battle.HitAt(0);
            Assert.AreEqual(20f, hit.Amount);
            Assert.AreEqual(HitKind.Strong, hit.Kind);
            Assert.AreEqual(enemy.X, hit.X);
            Assert.AreEqual(enemy.Y, hit.Y);
            Assert.AreEqual(battle.TickCount, hit.Tick, "the count on the tick the shot landed");
            Assert.AreEqual(1, hit.Tick);
            Assert.AreEqual(enemy.Entry, hit.TargetEntry);
            Assert.AreEqual(1, enemy.Entry);
        }

        [Test]
        public void S4d_AC3_The_Record_Is_Stamped_With_The_Count_Of_Its_Own_Tick_And_Not_The_Count_Read_Later()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow());
            Buy(battle, 1, 3, Quick(Light)); // one shot every 30 ticks
            board.SpawnEnemy(Still(Shadow));

            var landedOn = new List<int>();
            for (int i = 0; i < 70; i++)
            {
                int before = battle.HitCount;
                battle.Tick();
                if (battle.HitCount > before) landedOn.Add(battle.TickCount);
            }

            Assert.AreEqual(3, battle.HitCount);
            Assert.AreEqual(1, landedOn[0], "the first lands on the first tick");
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(landedOn[i], battle.HitAt(i).Tick, $"record {i} carries the count of the tick it was added on");
            Assert.Greater(landedOn[1], 1);
            Assert.Greater(landedOn[2], landedOn[1]);
            Assert.AreEqual(70, battle.TickCount);
        }

        [Test]
        public void S4d_AC3_The_Record_Holds_The_Position_Of_The_Enemy_Mid_Step_Not_Its_Previous_One()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow());
            Buy(battle, 8, 3, Quick(Light));
            Enemy enemy = board.SpawnEnemy(new EnemyStats(3f, 100_000f, 0, Shadow));
            Ticks(battle, 100);

            int index = TickToNextRecord(battle);

            Hit hit = battle.HitAt(index);
            Assert.AreNotEqual(enemy.PreviousX, enemy.X, "the enemy is mid-step");
            Assert.AreEqual(enemy.X, hit.X, 0f, "its position after this tick's step");
            Assert.AreEqual(enemy.Y, hit.Y);
            Assert.AreEqual(battle.TickCount, hit.Tick);
        }

        [Test]
        public void S4d_AC3_No_Element_Gives_Normal_At_10_And_A_Row_Of_0_25_Gives_Weak_At_2_5()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, null);
            Buy(battle, 1, 3, Quick(default));
            board.SpawnEnemy(Still(Shadow));
            Hit normal = battle.HitAt(TickToNextRecord(battle));
            Assert.AreEqual(HitKind.Normal, normal.Kind);
            Assert.AreEqual(10f, normal.Amount);

            Board weakBoard = OpenBoard();
            Battle weakBattle = Parked(weakBoard, LightShadow(0.25f));
            Buy(weakBattle, 1, 3, Quick(Light));
            weakBoard.SpawnEnemy(Still(Shadow));
            Hit weak = weakBattle.HitAt(TickToNextRecord(weakBattle));
            Assert.AreEqual(HitKind.Weak, weak.Kind);
            Assert.AreEqual(2.5f, weak.Amount);
        }

        [Test]
        public void S4d_AC3_A_Shot_Of_0_Damage_Still_Adds_A_Record_Of_0_Normal()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, null);
            Buy(battle, 1, 3, Quick(default, damage: 0f));
            board.SpawnEnemy(Still(Shadow));

            Hit hit = battle.HitAt(TickToNextRecord(battle));

            Assert.AreEqual(0f, hit.Amount);
            Assert.AreEqual(HitKind.Normal, hit.Kind);
        }

        [Test]
        public void S4d_AC3_A_Killing_Hit_Records_The_Whole_Amount_With_The_Killed_Enemys_Number_And_The_Record_Outlives_It()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow());
            Buy(battle, 1, 3, Quick(Light));
            board.SpawnEnemy(Still(Shadow, 5f)); // keeps the stats of a still enemy
            Enemy enemy = board.EnemyAt(0);

            battle.Tick();

            Assert.AreEqual(0, board.EnemyCount, "the hit killed it");
            Assert.IsFalse(enemy.IsOnBoard);
            Hit hit = battle.HitAt(0);
            Assert.AreEqual(20f, hit.Amount, "not cut to the 5 health left");
            Assert.AreEqual(enemy.Entry, hit.TargetEntry);
            Assert.AreEqual(HitKind.Strong, hit.Kind);
            Assert.AreEqual(1, battle.HitCount);
            Ticks(battle, 5);
            AssertSameHit(hit, battle.HitAt(0), "still there once the enemy is gone");
        }

        [Test]
        public void S4d_AC3_A_Shot_Whose_Target_Left_Adds_No_Record()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow());
            // Speed 10 cells a second: in flight for many ticks.
            Buy(battle, 8, 0, new TowerStats(20f, 10f, 2f, 10f, 0, Light));
            Enemy enemy = board.SpawnEnemy(Still(Shadow));
            battle.Tick();
            Assert.AreEqual(1, battle.ProjectileCount);

            board.RemoveEnemy(enemy);
            Ticks(battle, 100);

            Assert.AreEqual(0, battle.HitCount);
            Assert.AreEqual(0, enemy.ImmuneHitTick);
        }

        [Test]
        public void S4d_AC3_Two_Hits_On_One_Tick_Are_Two_Records_In_Landing_Order()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow());
            Buy(battle, 1, 3, Quick(Light, damage: 10f));
            Buy(battle, 1, 5, Quick(Light, damage: 7f));
            board.SpawnEnemy(Still(Shadow));

            battle.Tick();

            Assert.AreEqual(2, battle.HitCount);
            Assert.AreEqual(20f, battle.HitAt(0).Amount, "the first tower's shot landed first");
            Assert.AreEqual(14f, battle.HitAt(1).Amount);
            Assert.AreEqual(1, battle.HitAt(0).Tick);
            Assert.AreEqual(1, battle.HitAt(1).Tick);
        }

        [Test]
        public void S4d_AC3_Entries_Are_Numbered_From_1_And_An_Enemy_On_A_Reused_Object_Has_A_New_Number()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow());
            Enemy first = board.SpawnEnemy(Still(Shadow));
            Enemy second = board.SpawnEnemy(Still(Shadow));
            Enemy third = board.SpawnEnemy(Still(Shadow));
            Assert.AreEqual(new[] { 1, 2, 3 }, new[] { first.Entry, second.Entry, third.Entry });

            board.RemoveEnemy(first);
            Enemy fourth = board.SpawnEnemy(Still(Shadow));

            Assert.AreSame(first, fourth, "the pool hands the same object out");
            Assert.AreEqual(4, fourth.Entry);
            Buy(battle, 1, 3, Quick(Light));
            // The nearest to the exit is hit first; remove the others so only the fourth is in range.
            board.RemoveEnemy(second);
            board.RemoveEnemy(third);
            Hit hit = battle.HitAt(TickToNextRecord(battle));
            Assert.AreEqual(4, hit.TargetEntry);
        }

        [Test]
        public void S4d_AC3_Past_64_Hits_HitCount_Is_Exact_And_The_Log_Answers_For_The_Last_64_In_Order()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow());
            Buy(battle, 1, 3, Quick(Light, shotsPerSecond: 60f)); // at most one landing a tick, on its launch tick
            Enemy enemy = board.SpawnEnemy(Still(Shadow, 1_000_000f));
            var written = new List<Hit>();

            while (battle.HitCount < 70)
            {
                battle.Tick();
                while (written.Count < battle.HitCount) written.Add(battle.HitAt(written.Count));
                if (battle.HitCount == 64)
                {
                    Assert.AreEqual(64, written.Count);
                    AssertSameHit(written[0], battle.HitAt(0), "at exactly 64 hits record 0 answers");
                }
                if (battle.HitCount == 65)
                {
                    Assert.Throws<System.ArgumentOutOfRangeException>(() => battle.HitAt(0), "at 65 the first is overwritten");
                    AssertSameHit(written[1], battle.HitAt(1), "and record 1 is hit number 1");
                    AssertSameHit(written[64], battle.HitAt(64), "the 65th");
                }
            }

            Assert.AreEqual(70, battle.HitCount);
            Assert.AreEqual(70, written.Count);
            for (int i = 6; i < 70; i++) AssertSameHit(written[i], battle.HitAt(i), $"record {i}");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => battle.HitAt(5), "the one before the last 64");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => battle.HitAt(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => battle.HitAt(-1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => battle.HitAt(70));
            Assert.AreEqual(1_000_000f - 70 * 20f, enemy.Health, "the damage of the overwritten hits was dealt all the same");
            for (int i = 7; i < 70; i++)
                Assert.Greater(battle.HitAt(i).Tick, battle.HitAt(i - 1).Tick, "landing order");
        }

        [Test]
        public void S4d_AC3_The_Log_Throws_Before_The_First_Hit()
        {
            Battle battle = Parked(OpenBoard(), null);
            Assert.AreEqual(0, battle.HitCount);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => battle.HitAt(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => battle.HitAt(-1));
        }

        // ---- 4d-AC4 immunity mark

        [Test]
        public void S4d_AC4_An_Immune_Landing_Adds_No_Record_Marks_The_Tick_And_A_Second_One_Moves_The_Mark()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow());
            Buy(battle, 1, 3, Quick(Light));
            Enemy enemy = board.SpawnEnemy(Still(Light));
            Assert.AreEqual(0, enemy.ImmuneHitTick, "0 until the first immune shot");

            battle.Tick();
            Assert.AreEqual(0, battle.HitCount);
            Assert.AreEqual(1, enemy.ImmuneHitTick);
            Assert.AreEqual(100_000f, enemy.Health, "and no damage");

            int tick = 1;
            while (enemy.ImmuneHitTick == 1 && tick++ < 100) battle.Tick();
            Assert.AreEqual(0, battle.HitCount);
            Assert.AreEqual(battle.TickCount, enemy.ImmuneHitTick, "moved to the tick of the second landing");
            Assert.Greater(enemy.ImmuneHitTick, 1);
        }

        [Test]
        public void S4d_AC4_Two_Towers_On_One_Enemy_Leave_One_Mark_The_Later_Tick()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow());
            Buy(battle, 1, 3, Quick(Light));
            Enemy enemy = board.SpawnEnemy(Still(Light));
            Ticks(battle, 10);
            Buy(battle, 1, 5, Quick(Light)); // fires on tick 11

            battle.Tick();

            Assert.AreEqual(11, enemy.ImmuneHitTick, "the tick of the later landing");
            Assert.AreEqual(0, battle.HitCount);
        }

        [Test]
        public void S4d_AC4_An_Enemy_Hit_By_The_Other_Element_Keeps_0_And_Gets_A_Record()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow());
            Buy(battle, 1, 3, Quick(Shadow));
            Enemy enemy = board.SpawnEnemy(Still(Light));

            Ticks(battle, 70);

            Assert.AreEqual(0, enemy.ImmuneHitTick);
            Assert.AreEqual(3, battle.HitCount);
        }

        [Test]
        public void S4d_AC4_A_Reused_Object_Starts_At_0_Though_The_Enemy_Before_It_Was_Marked()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow());
            Buy(battle, 1, 3, Quick(Light));
            Enemy enemy = board.SpawnEnemy(Still(Light));
            battle.Tick();
            Assert.AreEqual(1, enemy.ImmuneHitTick);
            board.RemoveEnemy(enemy);

            Enemy next = board.SpawnEnemy(Still(Shadow));

            Assert.AreSame(enemy, next);
            Assert.AreEqual(0, next.ImmuneHitTick);
            Assert.AreEqual(2, next.Entry);
        }

        [Test]
        public void S4d_AC4_A_Tower_Of_Wave_Element_And_An_Enemy_Of_Another_Mix_Marks_Only_The_Own_Element()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow());
            Buy(battle, 1, 3, Quick(Light));
            Enemy lightEnemy = board.SpawnEnemy(Still(Light));
            Enemy shadowEnemy = board.SpawnEnemy(Still(Shadow));
            // Both stand on the spawn; the one nearest the exit is the first (a tie goes to the first to enter).

            battle.Tick();

            Assert.AreEqual(1, lightEnemy.ImmuneHitTick);
            Assert.AreEqual(0, shadowEnemy.ImmuneHitTick);
            Assert.AreEqual(0, battle.HitCount);
        }

        // ---- 4d-AC5 rules untouched

        private static List<long> Run(int ticks)
        {
            var light = new EnemyStats(2f, 30f, 3, Light);
            var shadow = new EnemyStats(2f, 30f, 3, Shadow);
            var waves = new List<Wave> { new Wave(light, 8, 0.5f, 0f), new Wave(shadow, 8, 0.5f, 3f) };
            var board = new Board(new GridMap(16, 9, TestMaps.PrototypeBlocked()), TestMaps.Spawn, TestMaps.Exit);
            var battle = new Battle(board, waves.ToArray(), 100, 100, 50, LightShadow());
            Buy(battle, 6, 6, new TowerStats(3f, 10f, 3f, 10f, 2, Light));
            Buy(battle, 6, 2, new TowerStats(3f, 10f, 3f, 10f, 2, Shadow));
            var log = new List<long>();
            for (int i = 0; i < ticks; i++)
            {
                battle.Tick();
                log.Add(battle.HitCount);
                if (battle.HitCount > 0)
                {
                    Hit last = battle.HitAt(battle.HitCount - 1);
                    log.Add(last.Tick);
                    log.Add(last.TargetEntry);
                    log.Add((long)last.Kind);
                    log.Add(System.BitConverter.SingleToInt32Bits(last.Amount));
                    log.Add(System.BitConverter.SingleToInt32Bits(last.X));
                    log.Add(System.BitConverter.SingleToInt32Bits(last.Y));
                }
                log.Add(battle.Gold);
                log.Add(battle.Lives);
                log.Add(battle.TickCount);
            }
            return log;
        }

        [Test]
        public void S4d_AC5_Two_Runs_Of_The_Same_Setup_Agree_On_Every_Tick_Including_The_Hit_Log()
        {
            List<long> first = Run(1500), second = Run(1500);

            CollectionAssert.AreEqual(first, second);
            Assert.Greater(first.Count, 1500 * 3, "the log was exercised");
        }

        [Test]
        public void S4d_AC5_A_Tick_Allocates_Nothing_Across_Strong_And_Immune_Landings_Past_64_Hits()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow());
            Buy(battle, 8, 3, Quick(Light, shotsPerSecond: 20f)); // strong on Shadow
            Buy(battle, 8, 5, Quick(Shadow, shotsPerSecond: 20f)); // immune on Shadow, strong on Light
            board.SpawnEnemy(new EnemyStats(0.2f, 1_000_000f, 0, Shadow));
            board.SpawnEnemy(new EnemyStats(0.2f, 1_000_000f, 0, Light));
            Ticks(battle, 60); // warm-up: the projectile pool reaches its peak

            TickAllocation.AssertNoneAcross(battle, 600);

            Assert.Greater(battle.HitCount, 64);
            Assert.Greater(board.EnemyAt(0).ImmuneHitTick + board.EnemyAt(1).ImmuneHitTick, 0, "immune landings happened");
        }
    }
}
