using System;
using System.Collections.Generic;
using ElementalTD.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace ElementalTD.Core.Tests
{
    public class BattleTests
    {
        // Open 16x9 board, spawn (0,4), exit (15,4): the default route is the straight line along y = 4.
        private static Battle OpenBattle(out Board board, EnemyStats enemy, float delay)
        {
            board = new Board(new GridMap(16, 9, new List<GridPos>()), new GridPos(0, 4), new GridPos(15, 4));
            return new Battle(board, enemy, delay);
        }

        private static Battle PrototypeBattle(out Board board, EnemyStats enemy, float delay)
        {
            board = new Board(new GridMap(16, 9, TestMaps.PrototypeBlocked()), TestMaps.Spawn, TestMaps.Exit);
            return new Battle(board, enemy, delay);
        }

        private static void Place(Board board, int x, int y, TowerStats stats) =>
            Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(new GridPos(x, y), stats));

        private static void Ticks(Battle battle, int count)
        {
            for (int i = 0; i < count; i++) battle.Tick();
        }

        // A fast shot (hits in one tick at distance <= 1.67) and a slow one that stays in flight.
        private static readonly TowerStats Fast = new TowerStats(20f, 10f, 0.1f, 100f);
        private static readonly TowerStats Slow = new TowerStats(20f, 10f, 0.1f, 1f);
        private static readonly EnemyStats Still10 = new EnemyStats(0f, 10f);

        // ---- 2c-AC1 Firing

        [TestCase(3f, true)]
        [TestCase(2.999f, false)]
        public void S2c_AC1_Range_Is_Inclusive_And_A_Wall_Does_Not_Block_The_Shot(float range, bool shoots)
        {
            var blocked = new List<GridPos> { new GridPos(1, 4), new GridPos(2, 4) }; // between tower and enemy
            var board = new Board(new GridMap(16, 9, blocked), new GridPos(0, 4), new GridPos(15, 4));
            var battle = new Battle(board, new EnemyStats(0f, 100f), 1f);
            Place(board, 3, 4, new TowerStats(range, 10f, 1f, 10f));

            battle.Tick();

            Assert.AreEqual(shoots ? 1 : 0, battle.ProjectileCount);
        }

        [Test]
        public void S2c_AC1_A_Tower_Placed_Next_To_A_Waiting_Enemy_Fires_On_The_First_Tick()
        {
            Battle battle = OpenBattle(out Board board, new EnemyStats(0f, 100f), 1f);
            Place(board, 1, 3, new TowerStats(2.5f, 10f, 1.5f, 10f));
            battle.Tick();
            Assert.AreEqual(1, battle.ProjectileCount);
        }

        [Test]
        public void S2c_AC1_Cadence_Is_1_5_Shots_Per_Second_90_Over_60_Seconds()
        {
            // A shot hits within the tick it is fired (distance 1, step 1.67), so hits count shots.
            Battle battle = OpenBattle(out Board board, new EnemyStats(0f, 1_000_000f), 1f);
            Place(board, 1, 3, new TowerStats(2f, 1f, 1.5f, 100f));
            Ticks(battle, 3600);
            float hits = 1_000_000f - board.EnemyAt(0).Health;
            Assert.AreEqual(90f, hits, 1f);
        }

        [Test]
        public void S2c_AC1_A_Tower_Fires_At_Most_Once_Per_Tick_Even_Above_60_Shots_Per_Second()
        {
            Battle battle = OpenBattle(out Board board, new EnemyStats(0f, 100f), 1f);
            Place(board, 1, 3, new TowerStats(20f, 1f, 600f, 0.001f)); // shots never land
            for (int tick = 1; tick <= 10; tick++)
            {
                battle.Tick();
                Assert.AreEqual(tick, battle.ProjectileCount, $"tick {tick}");
            }
        }

        // ---- 2c-AC2 Targeting

        [TestCase(5)]  // both still head for the same cell, the first has more progress
        [TestCase(90)] // the first is a cell further along
        public void S2c_AC2_The_Enemy_Nearer_The_Exit_Is_Shot(int ticksBeforeSecondEnters)
        {
            Battle battle = OpenBattle(out Board board, new EnemyStats(2f, 100f), 1f);
            Enemy first = board.EnemyAt(0);
            Ticks(battle, ticksBeforeSecondEnters);
            var second = new Enemy(board.Field, board.Spawn, new EnemyStats(2f, 100f));
            board.AddEnemy(second);
            Place(board, 2, 2, new TowerStats(20f, 10f, 0.1f, 100f));

            Ticks(battle, 3);

            Assert.AreEqual(90f, first.Health);
            Assert.AreEqual(100f, second.Health);
        }

        [Test]
        public void S2c_AC2_On_An_Exact_Tie_The_First_To_Enter_Is_Shot()
        {
            Battle battle = OpenBattle(out Board board, new EnemyStats(2f, 100f), 1f);
            var second = new Enemy(board.Field, board.Spawn, new EnemyStats(2f, 100f));
            board.AddEnemy(second);
            Place(board, 2, 2, new TowerStats(20f, 10f, 0.1f, 100f));

            Ticks(battle, 3);

            Assert.AreEqual(90f, board.EnemyAt(0).Health);
            Assert.AreEqual(100f, second.Health);
        }

        // ---- 2c-AC3 Projectile

        [Test]
        public void S2c_AC3_A_Shot_Starts_On_The_Tower_Cell_Gets_Closer_Every_Tick_And_Hits_Once()
        {
            Battle battle = OpenBattle(out Board board, new EnemyStats(0f, 100f), 1f);
            Place(board, 6, 4, new TowerStats(10f, 10f, 0.1f, 1f)); // one shot per 10 s, 1 cell/s

            battle.Tick();
            Projectile shot = battle.ProjectileAt(0);
            Assert.AreEqual(6f, shot.PreviousX);
            Assert.AreEqual(4f, shot.PreviousY);

            float last = shot.X;
            int guard = 0;
            while (battle.ProjectileCount > 0 && guard++ < 1000)
            {
                Assert.AreEqual(100f, board.EnemyAt(0).Health, "no damage before the hit");
                battle.Tick();
                if (battle.ProjectileCount == 0) break;
                Assert.Less(shot.X, last, "closer every tick");
                last = shot.X;
            }

            Assert.AreEqual(0, battle.ProjectileCount);
            Assert.AreEqual(90f, board.EnemyAt(0).Health);
            Ticks(battle, 300); // the next shot is 10 s after the first
            Assert.AreEqual(90f, board.EnemyAt(0).Health, "damage applied once");
        }

        [Test]
        public void S2c_AC3_A_Second_Shot_In_Flight_When_The_First_Kills_Vanishes_With_No_Effect()
        {
            Battle battle = OpenBattle(out Board board, Still10, 100f);
            Enemy enemy = board.EnemyAt(0);
            Place(board, 6, 4, Slow);
            Place(board, 1, 3, Fast);

            Ticks(battle, 2);

            Assert.AreEqual(0, battle.ProjectileCount);
            Assert.AreEqual(0, board.EnemyCount);
            Assert.AreEqual(0f, enemy.Health, "only the killing shot applied its damage");
        }

        [Test]
        public void S2c_AC3_After_TryRemoveTower_That_Towers_Shot_Still_Hits()
        {
            Battle battle = OpenBattle(out Board board, new EnemyStats(0f, 100f), 1f);
            Place(board, 3, 4, new TowerStats(10f, 10f, 0.1f, 10f));
            battle.Tick();
            Assert.AreEqual(1, battle.ProjectileCount);

            Assert.IsTrue(board.TryRemoveTower(new GridPos(3, 4)));
            Assert.AreEqual(1, battle.ProjectileCount, "the shot keeps flying");
            Ticks(battle, 30);

            Assert.AreEqual(0, battle.ProjectileCount);
            Assert.AreEqual(90f, board.EnemyAt(0).Health);
        }

        [Test]
        public void S2c_AC3_Delay_0_A_Slow_Shot_Fired_Earlier_Does_Not_Hit_The_Enemy_That_Re_Entered()
        {
            Battle battle = OpenBattle(out Board board, Still10, 0f);
            Enemy enemy = board.EnemyAt(0);
            Place(board, 6, 4, Slow); // fired first, still in flight when the next one kills
            Place(board, 1, 3, Fast);

            battle.Tick(); // the fast shot kills
            Assert.AreEqual(0, board.EnemyCount);
            battle.Tick(); // delay 0: back on the next tick

            Assert.IsTrue(enemy.IsOnBoard);
            Assert.AreEqual(enemy.MaxHealth, enemy.Health);
            Assert.AreEqual(new GridPos(0, 4), enemy.Follower.From);
            Assert.AreEqual(0, battle.ProjectileCount, "the stale shot is gone");
            Ticks(battle, 100);
            Assert.AreEqual(enemy.MaxHealth, enemy.Health, "full until the towers fire again");
        }

        // ---- 2c-AC4 Leaving and re-entering

        [TestCase("killed")]
        [TestCase("exit")]
        public void S2c_AC4_The_Enemy_Leaves_The_Board_And_The_Cell_It_Stood_On_Can_Be_Built_On(string how)
        {
            var cell = new GridPos(1, 4);
            Battle battle;
            Board board;
            Enemy enemy;
            if (how == "killed")
            {
                battle = OpenBattle(out board, new EnemyStats(2f, 10f), 100f);
                enemy = board.EnemyAt(0);
                Place(board, 3, 2, Fast);
                Assert.AreEqual(PlacementResult.EnemyInTheWay, board.TryPlaceTower(cell, Fast), "the enemy is heading for it");
                Ticks(battle, 3);
            }
            else
            {
                battle = OpenBattle(out board, new EnemyStats(60f, 10f), 100f); // one cell per tick
                enemy = board.EnemyAt(0);
                Ticks(battle, 30);
            }
            Assert.AreEqual(0, board.EnemyCount);
            Assert.IsFalse(enemy.IsOnBoard);
            Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(cell, Fast));
        }

        [Test]
        public void S2c_AC4_Delay_1s_The_Enemy_Is_Back_Within_One_Tick_Of_60_Ticks_On_The_Spawn_With_Full_Health()
        {
            Battle battle = OpenBattle(out Board board, Still10, 1f);
            Enemy enemy = board.EnemyAt(0);
            Place(board, 1, 3, Fast);
            battle.Tick(); // killed
            Assert.IsFalse(enemy.IsOnBoard);
            Assert.AreEqual(0f, enemy.Health, "health keeps its last value off the board");

            int ticks = 0;
            while (!enemy.IsOnBoard && ticks < 200) { battle.Tick(); ticks++; }

            Assert.That(ticks, Is.InRange(60, 62));
            Assert.AreEqual(enemy.MaxHealth, enemy.Health);
            Assert.AreEqual(new GridPos(0, 4), enemy.Follower.From);
            Assert.AreEqual(0f, enemy.X);
            Assert.AreEqual(4f, enemy.Y);
        }

        [Test]
        public void S2c_AC4_A_Tower_Placed_On_The_Old_Route_During_The_Gap_Is_Respected_On_Re_Entry()
        {
            Battle battle = OpenBattle(out Board board, new EnemyStats(2f, 10f), 1f);
            Enemy enemy = board.EnemyAt(0);
            Place(board, 3, 2, Fast);
            Ticks(battle, 3);
            Assert.IsFalse(enemy.IsOnBoard);
            var onRoute = new GridPos(1, 4);
            Place(board, 1, 4, Slow);

            while (!enemy.IsOnBoard) battle.Tick();
            for (int i = 0; i < 100; i++)
            {
                Assert.AreNotEqual(onRoute, enemy.Follower.From);
                Assert.AreNotEqual(onRoute, enemy.Follower.To);
                battle.Tick();
            }
        }

        [Test]
        public void S2c_AC4_A_Tower_Is_Not_Fired_At_An_Enemy_That_Is_Off_The_Board()
        {
            Battle battle = OpenBattle(out Board board, Still10, 100f);
            Place(board, 1, 3, Fast);
            Ticks(battle, 2); // killed
            Place(board, 2, 4, new TowerStats(20f, 10f, 1f, 10f));
            Ticks(battle, 120);
            Assert.AreEqual(0, board.EnemyCount);
            Assert.AreEqual(0, battle.ProjectileCount);
        }

        // ---- 2c-AC7 2b still holds

        [Test]
        public void S2c_AC7_TowerPlaced_And_TowerRemoved_Fire_Once_After_The_Change_And_Never_On_A_Refusal()
        {
            Board board = new Board(new GridMap(16, 9, TestMaps.PrototypeBlocked()), TestMaps.Spawn, TestMaps.Exit);
            var placed = new List<(GridPos cell, bool hasTower)>();
            var removed = new List<(GridPos cell, bool hasTower)>();
            board.TowerPlaced += c => placed.Add((c, board.HasTower(c)));
            board.TowerRemoved += c => removed.Add((c, board.HasTower(c)));
            var cell = new GridPos(2, 2);

            board.TryPlaceTower(cell, Fast);
            board.TryPlaceTower(cell, Fast);              // refused: occupied
            board.TryPlaceTower(TestMaps.Spawn, Fast);    // refused: spawn
            board.TryRemoveTower(new GridPos(3, 3));      // nothing there

            Assert.AreEqual(1, placed.Count);
            Assert.AreEqual((cell, true), placed[0]);
            Assert.AreEqual(0, removed.Count);

            board.TryRemoveTower(cell);

            Assert.AreEqual(1, placed.Count);
            Assert.AreEqual(1, removed.Count);
            Assert.AreEqual((cell, false), removed[0]);
        }

        [Test]
        public void S2c_AC7_While_The_Enemy_Is_Off_The_Board_Only_The_Spawn_Must_Still_Reach_The_Exit()
        {
            Battle battle = PrototypeBattle(out Board board, Still10, 100f);
            Place(board, 1, 2, Fast);
            Ticks(battle, 2); // killed
            Assert.AreEqual(0, board.EnemyCount);

            Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(new GridPos(5, 7), Fast));
            Assert.AreEqual(PlacementResult.WouldBlockPath, board.TryPlaceTower(new GridPos(5, 8), Fast));
            Assert.IsTrue(board.Field.IsReachable(TestMaps.Spawn));
        }

        // ---- 2c-AC9 Provisional design target (retune with the balance-designer)

        private static readonly TowerStats Ac8Tower = new TowerStats(2.5f, 10f, 1.5f, 10f);
        private static readonly EnemyStats Ac8Enemy = new EnemyStats(2f, 100f);

        // Ticks until the enemy leaves the board and returns its health at that moment.
        private static float HealthWhenItLeaves(Battle battle, Board board)
        {
            Enemy enemy = board.EnemyAt(0);
            for (int i = 0; i < 5000 && board.EnemyCount > 0; i++) battle.Tick();
            Assert.AreEqual(0, board.EnemyCount, "the enemy never left");
            return enemy.Health;
        }

        private const string AssetDir = "Assets/_Game/Data/Assets/";

        private static Battle AssetBattle(out Board board, out TowerStats tower, float delay)
        {
            var map = AssetDatabase.LoadAssetAtPath<MapDefinition>(AssetDir + "Map_Prototype.asset");
            var towerDef = AssetDatabase.LoadAssetAtPath<TowerDefinition>(AssetDir + "Tower_Prototype.asset");
            var enemyDef = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(AssetDir + "Enemy_Prototype.asset");
            Assert.IsNotNull(map);
            Assert.IsNotNull(towerDef);
            Assert.IsNotNull(enemyDef);
            board = new Board(map.ToGridMap(), map.Spawn, map.Exit);
            tower = towerDef.ToStats();
            return new Battle(board, enemyDef.ToStats(), delay);
        }

        [Test]
        public void S2c_AC8_The_Prototype_Assets_Hold_The_Closed_Values()
        {
            AssetBattle(out Board board, out TowerStats tower, 1f);
            Enemy enemy = board.EnemyAt(0);
            Assert.AreEqual(new[] { 2.5f, 10f, 1.5f, 10f }, new[] { tower.Range, tower.Damage, tower.ShotsPerSecond, tower.ProjectileSpeed });
            Assert.AreEqual(new[] { 2f, 100f }, new[] { enemy.Speed, enemy.MaxHealth });
        }

        [Test]
        public void S2c_AC9_ProvisionalTarget_No_Single_Tower_Kills_In_One_Pass()
        {
            int tried = 0;
            for (int y = 0; y < 9; y++)
                for (int x = 0; x < 16; x++)
                {
                    Battle battle = AssetBattle(out Board board, out TowerStats tower, 1000f);
                    if (board.TryPlaceTower(new GridPos(x, y), tower) != PlacementResult.Placed) continue;
                    tried++;
                    Assert.Greater(HealthWhenItLeaves(battle, board), 0f, $"a single tower on ({x},{y}) kills");
                }
            Assert.Greater(tried, 100, "the check must cover the free cells");
        }

        [Test]
        public void S2c_AC9_ProvisionalTarget_Towers_On_6_6_And_11_2_Kill_In_One_Pass()
        {
            Battle battle = AssetBattle(out Board board, out TowerStats tower, 1000f);
            Place(board, 6, 6, tower);
            Place(board, 11, 2, tower);
            Assert.LessOrEqual(HealthWhenItLeaves(battle, board), 0f);
        }

        // ---- 2c-AC10 Fixed tick

        [TestCase(1)]
        [TestCase(7)]
        [TestCase(2024)]
        public void S2c_AC10_Same_Placements_At_The_Same_Ticks_Give_The_Same_State_After_Every_Tick(int seed)
        {
            Battle a = PrototypeBattle(out Board boardA, Ac8Enemy, 1f);
            Battle b = PrototypeBattle(out Board boardB, Ac8Enemy, 1f);
            var rnd = new Random(seed);
            Enemy ea = boardA.EnemyAt(0), eb = boardB.EnemyAt(0);
            int leaves = 0;
            bool wasOn = true;

            for (int tick = 0; tick < 4000; tick++)
            {
                if (rnd.Next(20) == 0)
                {
                    var cell = new GridPos(rnd.Next(16), rnd.Next(9));
                    bool remove = boardA.HasTower(cell) && rnd.Next(2) == 0;
                    if (remove) Assert.AreEqual(boardA.TryRemoveTower(cell), boardB.TryRemoveTower(cell));
                    else Assert.AreEqual(boardA.TryPlaceTower(cell, Ac8Tower), boardB.TryPlaceTower(cell, Ac8Tower));
                }
                a.Tick();
                b.Tick();

                Assert.AreEqual(ea.Health, eb.Health, $"health, tick {tick}");
                Assert.AreEqual(ea.IsOnBoard, eb.IsOnBoard, $"on board, tick {tick}");
                Assert.AreEqual(ea.X, eb.X, $"x, tick {tick}");
                Assert.AreEqual(ea.Y, eb.Y, $"y, tick {tick}");
                Assert.AreEqual(a.ProjectileCount, b.ProjectileCount, $"projectiles, tick {tick}");
                for (int i = 0; i < a.ProjectileCount; i++)
                {
                    Assert.AreEqual(a.ProjectileAt(i).X, b.ProjectileAt(i).X);
                    Assert.AreEqual(a.ProjectileAt(i).Y, b.ProjectileAt(i).Y);
                }
                if (wasOn && !ea.IsOnBoard) leaves++;
                wasOn = ea.IsOnBoard;
            }
            Assert.Greater(leaves, 0, "the run must cover at least one leave and re-entry");
        }

        // ---- 2c-AC13 Allocation

        [Test]
        public void S2c_AC13_Tick_Allocates_Nothing_After_Warmup_Across_Shots_Hits_Deaths_And_Reentries()
        {
            Battle battle = PrototypeBattle(out Board board, Ac8Enemy, 1f);
            Place(board, 6, 6, Ac8Tower);
            Place(board, 11, 2, Ac8Tower);
            Enemy enemy = board.EnemyAt(0);
            Ticks(battle, 2000); // warm-up: shots, hits, deaths and re-entries

            int deaths = 0, entries = 0, maxShots = 0;
            bool wasOn = enemy.IsOnBoard;
            Assert.That(() =>
            {
                for (int i = 0; i < 1500; i++)
                {
                    battle.Tick();
                    if (wasOn && !enemy.IsOnBoard) deaths++;
                    if (!wasOn && enemy.IsOnBoard) entries++;
                    wasOn = enemy.IsOnBoard;
                    if (battle.ProjectileCount > maxShots) maxShots = battle.ProjectileCount;
                }
            }, Is.Not.AllocatingGCMemory());

            Assert.Greater(deaths, 0);
            Assert.Greater(entries, 0);
            Assert.Greater(maxShots, 0);
        }

        // ---- Data rules of the new stats

        [TestCase(-1f, 10f, 1f, 1f)]
        [TestCase(1f, -1f, 1f, 1f)]
        [TestCase(1f, 10f, 0f, 1f)]
        [TestCase(1f, 10f, 1f, 0f)]
        public void S2c_AC8_TowerStats_Rejects_Invalid_Values(float range, float damage, float shots, float speed)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TowerStats(range, damage, shots, speed));
        }

        [TestCase(-1f, 10f)]
        [TestCase(1f, 0f)]
        public void S2c_AC8_EnemyStats_Rejects_Invalid_Values(float speed, float health)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnemyStats(speed, health));
        }
    }
}
