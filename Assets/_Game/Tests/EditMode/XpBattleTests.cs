using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ElementalTD.Core.Tests
{
    /// <summary>XP on a Battle with a hand-built board (5a-AC1, and 5a-AC3 on small runs).</summary>
    public class XpBattleTests
    {
        private static readonly GridPos Spawn = new GridPos(0, 4);
        private static readonly GridPos Near = new GridPos(1, 3);
        private static readonly GridPos Near2 = new GridPos(1, 5);

        // A shot crosses 1.67 cells per tick, so a tower next to the spawn kills on the tick it launches.
        private static TowerStats FastTower() => new TowerStats(20f, 10f, 0.1f, 100f);
        // One cell per second: the shot is in flight for about 85 ticks.
        private static TowerStats SlowTower() => new TowerStats(20f, 10f, 0.1f, 1f);

        private static EnemyStats Still(int gold, int xp, float health = 10f) => new EnemyStats(0f, health, gold, default, xp);

        private static Board OpenBoard() => new Board(new GridMap(16, 9, new List<GridPos>()), Spawn, new GridPos(15, 4));

        private static readonly Wave[] Idle = { new Wave(new EnemyStats(1f, 10f, 5, default, 7), 1, 1f, 1_000_000f) };

        private static Battle OneEnemy(EnemyStats enemy, Progression progression = null, int lives = 10) =>
            new Battle(OpenBoard(), new[] { new Wave(enemy, 1, 1f, 0f) }, lives, 0, 100, null, progression);

        // ---- 5a-AC1 XP from kills

        [Test]
        public void S5a_AC1_XP_Starts_At_0_And_A_Battle_Without_A_Progression_Makes_One_That_Counts_XP_And_Stays_At_Level_1()
        {
            Battle battle = OneEnemy(Still(5, 7));
            Assert.IsNotNull(battle.Progression);
            Assert.AreEqual((0, 1), (battle.Progression.Xp, battle.Progression.Level));
            battle.TryBuyTower(Near, FastTower());
            for (int i = 0; i < 5; i++) battle.Tick();
            Assert.AreEqual((7, 1, true), (battle.Progression.Xp, battle.Progression.Level, battle.Progression.TableEnded), "5a-AC3: no table, no level");
        }

        [Test]
        public void S5a_AC1_The_Battle_Keeps_The_Progression_It_Was_Given()
        {
            var progression = new Progression(new[] { 10 });
            Assert.AreSame(progression, OneEnemy(Still(0, 0), progression).Progression);
        }

        [Test]
        public void S5a_AC1_XP_Rises_By_The_Reward_On_The_Tick_Of_The_Killing_Hit_And_On_No_Other()
        {
            Battle battle = OneEnemy(Still(5, 7));
            battle.TryBuyTower(Near, FastTower());
            var xpAfter = new List<int>();
            var goldAfter = new List<int>();
            for (int i = 0; i < 30; i++)
            {
                battle.Tick();
                xpAfter.Add(battle.Progression.Xp);
                goldAfter.Add(battle.Gold);
            }

            int killTick = goldAfter.FindIndex(g => g > 0);
            Assert.GreaterOrEqual(killTick, 0, "the enemy was killed");
            for (int i = 0; i < xpAfter.Count; i++)
                Assert.AreEqual(i >= killTick ? 7 : 0, xpAfter[i], $"tick {i + 1}: XP rises on the kill's tick ({killTick + 1}) and only once");
        }

        [Test]
        public void S5a_AC1_A_Shot_In_Flight_Pays_Nothing_At_Launch_And_Pays_When_It_Lands()
        {
            Battle battle = OneEnemy(Still(5, 7));
            battle.TryBuyTower(Near, SlowTower());
            battle.Tick();
            Assert.AreEqual(1, battle.ProjectileCount, "launched");
            Assert.AreEqual(0, battle.Progression.Xp, "no XP at launch");

            int ticks = 1;
            while (battle.ProjectileCount > 0 && ticks < 300)
            {
                Assert.AreEqual(0, battle.Progression.Xp, $"tick {ticks}: in flight");
                battle.Tick();
                ticks++;
            }
            Assert.Greater(ticks, 40, "the flight took many ticks");
            Assert.AreEqual(7, battle.Progression.Xp, "paid on the tick the shot landed");
            Assert.AreEqual(5, battle.Gold, "with the gold");
        }

        [Test]
        public void S5a_AC1_Two_Shots_That_Land_On_One_Tick_On_A_Dying_Enemy_Pay_Once()
        {
            Battle battle = OneEnemy(Still(5, 7));
            battle.TryBuyTower(Near, FastTower());
            battle.TryBuyTower(Near2, FastTower());
            battle.Tick();

            Assert.AreEqual(0, battle.Board.EnemyCount, "dead on the first tick");
            Assert.AreEqual(1, battle.HitCount, "the second shot found its target gone");
            Assert.AreEqual((5, 7), (battle.Gold, battle.Progression.Xp), "gold and XP paid once");
            for (int i = 0; i < 20; i++) battle.Tick();
            Assert.AreEqual((5, 7), (battle.Gold, battle.Progression.Xp), "and never again");
        }

        [Test]
        public void S5a_AC1_An_Enemy_That_Exits_Pays_No_XP()
        {
            Battle battle = OneEnemy(new EnemyStats(60f, 10f, 9, default, 7));
            for (int i = 0; i < 200 && battle.Lives == 10; i++) battle.Tick();

            Assert.AreEqual(9, battle.Lives, "it reached the exit");
            Assert.AreEqual((0, 0), (battle.Progression.Xp, battle.Gold));
        }

        [Test]
        public void S5a_AC1_A_Shot_Dropped_Because_Its_Target_Left_Pays_No_XP()
        {
            Battle battle = OneEnemy(new EnemyStats(60f, 10f, 9, default, 7));
            battle.TryBuyTower(Near, SlowTower());
            for (int i = 0; i < 300; i++) battle.Tick();

            Assert.AreEqual(9, battle.Lives, "the runner left");
            Assert.AreEqual(0, battle.ProjectileCount, "the slow shot was dropped");
            Assert.AreEqual((0, 0), (battle.Progression.Xp, battle.Gold));
        }

        [Test]
        public void S5a_AC1_The_Kill_That_Wins_The_Run_Counts()
        {
            Battle battle = OneEnemy(Still(5, 7));
            battle.TryBuyTower(Near, FastTower());
            battle.Tick();

            Assert.AreEqual(RunOutcome.Won, battle.Outcome);
            Assert.AreEqual(7, battle.Progression.Xp);
            for (int i = 0; i < 50; i++) battle.Tick();
            Assert.AreEqual(7, battle.Progression.Xp, "a finished run adds nothing");
        }

        [Test]
        public void S5a_AC1_A_Shot_That_Would_Land_After_The_Run_Was_Lost_Pays_Nothing()
        {
            // One life. The slow shot at the still enemy is in flight when a runner reaches the exit and loses the run.
            var board = OpenBoard();
            var battle = new Battle(board, Idle, 1, 0, 100, null, new Progression(new[] { 10 }));
            battle.TryBuyTower(Near, SlowTower());
            board.SpawnEnemy(Still(5, 7));
            battle.Tick();
            Assert.AreEqual(1, battle.ProjectileCount, "the slow shot is launched");
            board.SpawnEnemy(new EnemyStats(60f, 10f, 0, default, 3));
            for (int i = 0; i < 400; i++) battle.Tick();

            Assert.AreEqual(RunOutcome.Lost, battle.Outcome);
            Assert.AreEqual(1, board.EnemyCount, "the still enemy was never killed");
            Assert.AreEqual((0, 0, 1), (battle.Progression.Xp, battle.Gold, battle.Progression.Level));
        }

        [Test]
        public void S5a_AC1_XP_Is_Paid_On_Exactly_The_Ticks_Gold_Is_Paid_For_A_Kill_In_A_Run_With_Kills_And_Leaks()
        {
            // Gold 5 and XP 7 a kill, no wave bonus: every tick's gold gain times 7 is its XP gain times 5.
            Board board = OpenBoard();
            var battle = new Battle(board, new[] { new Wave(new EnemyStats(3f, 10f, 5, default, 7), 12, 0.5f, 0f) }, 100);
            battle.TryBuyTower(new GridPos(7, 3), new TowerStats(2.5f, 4f, 1.5f, 10f));
            int kills = 0, lastGold = 0, lastXp = 0, lastLives = battle.Lives;
            for (int i = 0; i < 1500 && battle.Outcome == RunOutcome.Running; i++)
            {
                battle.Tick();
                int dGold = battle.Gold - lastGold, dXp = battle.Progression.Xp - lastXp;
                Assert.AreEqual(dGold * 7, dXp * 5, $"tick {battle.TickCount}");
                kills += dGold / 5;
                lastGold = battle.Gold;
                lastXp = battle.Progression.Xp;
            }
            Assert.Greater(kills, 0, "some enemies were killed");
            Assert.Less(battle.Lives, lastLives, "and some leaked");
            Assert.AreEqual(kills * 7, battle.Progression.Xp);
            Assert.AreEqual(12 - kills, lastLives - battle.Lives, "every enemy either died or leaked");
        }

        [Test]
        public void S5a_AC1_An_Enemy_With_No_XP_Reward_Pays_None_And_The_Default_Reward_Is_0()
        {
            Assert.AreEqual(0, new EnemyStats(1f, 1f).XpReward);
            Assert.AreEqual(0, new EnemyStats(1f, 1f, 5).XpReward);
            Battle battle = OneEnemy(Still(5, 0));
            battle.TryBuyTower(Near, FastTower());
            battle.Tick();
            Assert.AreEqual((5, 0), (battle.Gold, battle.Progression.Xp));
        }

        [Test]
        public void S5a_AC1_A_Negative_XP_Reward_Is_Refused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyStats(1f, 1f, 0, default, -1));
            Assert.DoesNotThrow(() => new EnemyStats(1f, 1f, 0, default, 0));
            Assert.DoesNotThrow(() => new EnemyStats(1f, 1f, 0, default, int.MaxValue));
        }

        // ---- 5a-AC2 on the board

        [Test]
        public void S5a_AC2_The_Level_Rises_On_The_Kill_Tick_Itself_Together_With_The_Gold()
        {
            var progression = new Progression(new[] { 7, 20 });
            Battle battle = OneEnemy(Still(5, 7), progression);
            battle.TryBuyTower(Near, FastTower());
            while (battle.Gold == 0 && battle.TickCount < 30) battle.Tick();

            Assert.AreEqual(5, battle.Gold, "the kill's tick");
            Assert.AreEqual((7, 2, 0), (progression.Xp, progression.Level, progression.XpIntoLevel), "the level is there on that tick, not a tick later");
        }

        [Test]
        public void S5a_AC2_One_Kill_Worth_Several_Levels_Gains_Them_All_On_One_Tick()
        {
            var progression = new Progression(new[] { 10, 10, 10 });
            Battle battle = OneEnemy(Still(0, 35), progression);
            battle.TryBuyTower(Near, FastTower());
            battle.Tick();

            Assert.AreEqual((35, 4, true), (progression.Xp, progression.Level, progression.TableEnded));
        }

        // ---- 5a-AC3 Nothing else changes, on small runs

        private static List<(int, int, int, int, int, int, RunOutcome, int)> Run(Progression progression, out Progression used)
        {
            Board board = OpenBoard();
            var battle = new Battle(board, new[] { new Wave(new EnemyStats(3f, 10f, 5, default, 7), 12, 0.5f, 0f, 20) }, 100, 0, 100, null, progression);
            battle.TryBuyTower(new GridPos(7, 3), new TowerStats(2.5f, 4f, 1.5f, 10f));
            var snapshots = new List<(int, int, int, int, int, int, RunOutcome, int)>();
            for (int i = 0; i < 1500; i++)
            {
                battle.Tick();
                snapshots.Add((battle.Gold, battle.Lives, board.EnemyCount, battle.HitCount, battle.ProjectileCount, battle.SurvivedWaves, battle.Outcome, battle.TickCount));
            }
            used = battle.Progression;
            return snapshots;
        }

        [Test]
        public void S5a_AC3_A_Run_With_A_Table_Equals_The_Same_Run_Without_One_On_Every_Tick_Except_For_The_Level()
        {
            var with = Run(new Progression(new[] { 7, 14, 21 }), out Progression a);
            var without = Run(null, out Progression b);

            Assert.AreEqual(without, with);
            Assert.AreEqual(a.Xp, b.Xp, "XP counts the same");
            Assert.Greater(a.Xp, 0);
            Assert.AreEqual(1, b.Level);
            Assert.Greater(a.Level, 1);
        }

        [Test]
        public void S5a_AC1_Two_Battles_Built_Without_A_Progression_Hold_Their_Own()
        {
            Battle a = OneEnemy(Still(5, 7)), b = OneEnemy(Still(5, 7));
            Assert.AreNotSame(a.Progression, b.Progression);
            a.Progression.AddXp(50);
            Assert.AreEqual(0, b.Progression.Xp);
        }

        [Test]
        public void S5a_AC1_A_Kill_On_The_Tick_That_Loses_The_Run_Pays_XP_As_It_Pays_Gold()
        {
            // One life: a very fast runner exits on the first step, and the fast tower kills the still enemy on the same tick.
            var board = OpenBoard();
            var battle = new Battle(board, Idle, 1, 0, 100, null, new Progression(new[] { 10 }));
            battle.TryBuyTower(Near, FastTower());
            board.SpawnEnemy(Still(5, 7));
            board.SpawnEnemy(new EnemyStats(1000f, 10f, 0, default, 3));
            battle.Tick();

            Assert.AreEqual(RunOutcome.Lost, battle.Outcome);
            Assert.AreEqual(5, battle.Gold, "the kill paid its gold on the losing tick");
            Assert.AreEqual(7, battle.Progression.Xp, "and its XP");
        }
    }
}
