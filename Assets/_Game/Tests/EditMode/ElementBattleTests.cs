using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ElementalTD.Core.Tests
{
    /// <summary>Elements in a Battle: damage on landing, immunity, targeting, entry and determinism (4b-AC2 to AC5, AC13, AC14).</summary>
    public class ElementBattleTests
    {
        private static readonly Element Light = new Element(1);
        private static readonly Element Shadow = new Element(2);
        private static readonly GridPos Spawn = new GridPos(0, 4);

        // Never starts a wave: enemies come through Board.SpawnEnemy.
        private static readonly Wave[] Idle = { new Wave(new EnemyStats(1f, 10f, 5), 1, 1f, 1_000_000f) };

        private static ElementChart LightShadow(float lightAgainstShadow = 2f, float shadowAgainstLight = 2f)
        {
            var chart = new ElementChart(2);
            chart.Set(Light, Shadow, lightAgainstShadow);
            chart.Set(Shadow, Light, shadowAgainstLight);
            return chart;
        }

        private static Board OpenBoard() => new Board(new GridMap(16, 9, new List<GridPos>()), Spawn, new GridPos(15, 4));

        private static Board PrototypeBoard() =>
            new Board(new GridMap(16, 9, TestMaps.PrototypeBlocked()), TestMaps.Spawn, TestMaps.Exit);

        private static Battle Parked(Board board, ElementChart chart, int gold = 0, int percent = 100, int lives = 10) =>
            new Battle(board, Idle, lives, gold, percent, chart);

        private static EnemyStats Still(Element element, float health = 45f, int reward = 0) => new EnemyStats(0f, health, reward, element);

        // Two shots a second: one every 30 ticks. Speed 100 cells a second crosses 1.67 cells a tick, so a tower next to the spawn lands on its launch tick.
        private static TowerStats Quick(Element element, float damage = 10f, int cost = 0) => new TowerStats(20f, damage, 2f, 100f, cost, element);

        // Speed 10 cells a second: 1/6 of a cell a tick, so a shot is in flight for many ticks.
        private static TowerStats Flying(Element element, float damage = 10f, int cost = 0, float shotsPerSecond = 2f) =>
            new TowerStats(20f, damage, shotsPerSecond, 10f, cost, element);

        private static void Ticks(Battle battle, int count)
        {
            for (int i = 0; i < count; i++) battle.Tick();
        }

        private static void Buy(Battle battle, int x, int y, in TowerStats stats) =>
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(new GridPos(x, y), stats));

        /// <summary>Ticks until the enemy's health changes; returns it. Fails if no shot lands within 200 ticks.</summary>
        private static float TickToNextHit(Battle battle, Enemy enemy)
        {
            float before = enemy.Health;
            for (int i = 0; i < 200; i++)
            {
                battle.Tick();
                if (enemy.Health != before) return enemy.Health;
            }
            Assert.Fail($"no shot landed within 200 ticks, health stayed {before}");
            return before;
        }

        // ---- 4b-AC2 damage on landing

        [Test]
        public void S4b_AC2_At_X2_A_Shot_Of_10_Takes_Health_45_To_25_Then_5_And_The_Third_Hit_Kills()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow());
            Buy(battle, 1, 3, Quick(Light));
            Enemy enemy = board.SpawnEnemy(Still(Shadow, 45f, 7));

            Assert.AreEqual(25f, TickToNextHit(battle, enemy), "45 - 10 x 2");
            Assert.IsTrue(enemy.IsOnBoard);
            Assert.AreEqual(5f, TickToNextHit(battle, enemy), "25 - 20");
            Assert.IsTrue(enemy.IsOnBoard, "alive at 5");
            Assert.AreEqual(0, battle.Gold);

            TickToNextHit(battle, enemy); // 5 - 20
            Assert.IsFalse(enemy.IsOnBoard, "the third hit kills");
            Assert.AreEqual(0, board.EnemyCount);
            Assert.AreEqual(7, battle.Gold, "paid once");
            Assert.AreEqual(10, battle.Lives);
        }

        [TestCase(0.25f, 42.5f)] // 45 - 2.5
        [TestCase(0.75f, 37.5f)] // 45 - 7.5
        [TestCase(1f, 35f)]      // 45 - 10
        [TestCase(2f, 25f)]      // 45 - 20
        [TestCase(4f, 5f)]       // 45 - 40
        public void S4b_AC2_A_Hit_Takes_Damage_Times_The_Multiplier_Exactly(float multiplier, float expectedHealth)
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow(multiplier));
            Buy(battle, 1, 3, Quick(Light));
            Enemy enemy = board.SpawnEnemy(Still(Shadow));

            Assert.AreEqual(expectedHealth, TickToNextHit(battle, enemy));
        }

        [Test]
        public void S4b_AC2_Only_The_Ordered_Pair_Set_Is_Multiplied()
        {
            // Only Light against Shadow is set; Shadow against Light is x1.
            var chart = new ElementChart(2);
            chart.Set(Light, Shadow, 2f);

            Board lightBoard = OpenBoard();
            Battle lightBattle = Parked(lightBoard, chart);
            Buy(lightBattle, 1, 3, Quick(Light));
            Enemy shadowEnemy = lightBoard.SpawnEnemy(Still(Shadow));
            Assert.AreEqual(25f, TickToNextHit(lightBattle, shadowEnemy), "Light against Shadow: x2");

            Board shadowBoard = OpenBoard();
            Battle shadowBattle = Parked(shadowBoard, chart);
            Buy(shadowBattle, 1, 3, Quick(Shadow));
            Enemy lightEnemy = shadowBoard.SpawnEnemy(Still(Light));
            Assert.AreEqual(35f, TickToNextHit(shadowBattle, lightEnemy), "Shadow against Light: x1");
        }

        [Test]
        public void S4b_AC2_A_Pair_The_Chart_Does_Not_List_Deals_One_And_So_Does_A_Missing_Chart()
        {
            var chart = new ElementChart(3);
            chart.Set(Light, Shadow, 2f);
            var thirdElement = new Element(3);

            Board board = OpenBoard();
            Battle battle = Parked(board, chart);
            Buy(battle, 1, 3, Quick(Light));
            Enemy enemy = board.SpawnEnemy(Still(thirdElement));
            Assert.AreEqual(35f, TickToNextHit(battle, enemy), "Light against an unlisted element");

            Board noChartBoard = OpenBoard();
            Battle noChart = Parked(noChartBoard, null);
            Buy(noChart, 1, 3, Quick(Light));
            Enemy other = noChartBoard.SpawnEnemy(Still(Shadow));
            Assert.AreEqual(35f, TickToNextHit(noChart, other), "no chart: x1");
        }

        [Test]
        public void S4b_AC2_A_Damage_Of_Zero_Deals_Nothing_At_Any_Multiplier_And_A_Huge_Multiplier_Kills()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow(2f));
            Buy(battle, 1, 3, Quick(Light, 0f));
            Enemy enemy = board.SpawnEnemy(Still(Shadow));
            Ticks(battle, 200);
            Assert.AreEqual(45f, enemy.Health, "damage 0 x 2");
            Assert.IsTrue(enemy.IsOnBoard);

            Board hugeBoard = OpenBoard();
            Battle huge = Parked(hugeBoard, LightShadow(float.MaxValue));
            Buy(huge, 1, 3, Quick(Light));
            Enemy victim = hugeBoard.SpawnEnemy(Still(Shadow, 45f, 3));
            Ticks(huge, 40);
            Assert.IsFalse(victim.IsOnBoard, "10 x the largest float is infinity, and an infinite hit kills");
            Assert.AreEqual(3, huge.Gold);
        }

        [Test]
        public void S4b_AC2_A_Shot_Keeps_Its_Towers_Element_And_Damage_When_The_Tower_Is_Sold_In_Flight()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow(), 10, 50);
            Buy(battle, 2, 3, Flying(Light, 10f, 10)); // 2.24 cells from the spawn at 1/6 cell a tick: about 13 ticks of flight
            Enemy enemy = board.SpawnEnemy(Still(Shadow));

            battle.Tick(); // launches
            Assert.AreEqual(1, battle.ProjectileCount);
            Assert.IsTrue(battle.TrySellTower(new GridPos(2, 3)));
            Assert.AreEqual(5, battle.Gold, "fired once: the refund percent of 10");
            Assert.AreEqual(0, board.TowerCount);
            Assert.AreEqual(1, battle.ProjectileCount, "the shot flies on");
            Assert.AreEqual(Light, battle.ProjectileAt(0).Element);

            Assert.AreEqual(25f, TickToNextHit(battle, enemy), "45 - 10 x 2, from a tower that is gone");
            Assert.AreEqual(0, battle.ProjectileCount);
        }

        [Test]
        public void S4b_AC2_A_Tower_And_An_Enemy_With_No_Element_Deal_And_Take_One_Whatever_The_Chart_Holds()
        {
            ElementChart chart = LightShadow(5f, 5f);
            foreach ((Element tower, Element enemy, string name) in new[]
            {
                (default(Element), default(Element), "none against none"),
                (default(Element), Light, "none against Light"),
                (Light, default(Element), "Light against none"),
            })
            {
                Board board = OpenBoard();
                Battle battle = Parked(board, chart);
                Buy(battle, 1, 3, Quick(tower));
                Enemy target = board.SpawnEnemy(Still(enemy));
                Assert.AreEqual(35f, TickToNextHit(battle, target), name);
            }
        }

        [Test]
        public void S4b_AC2_A_Reused_Shot_Object_Takes_The_Element_Of_Its_New_Launch_Not_Its_Last()
        {
            var chart = new ElementChart(2);
            chart.Set(Light, Shadow, 2f); // only Light against Shadow is x2

            // (a) a Light shot lands, its tower is sold, and a Shadow tower's shot from the reused object does nothing to a Shadow enemy.
            Board board = OpenBoard();
            Battle battle = Parked(board, chart);
            Buy(battle, 3, 3, Flying(Light));
            Enemy victim = board.SpawnEnemy(Still(Shadow));
            battle.Tick();
            Projectile first = battle.ProjectileAt(0);
            Assert.AreEqual(25f, TickToNextHit(battle, victim), "Light against Shadow: x2");
            Assert.AreEqual(0, battle.ProjectileCount);
            Assert.IsTrue(battle.TrySellTower(new GridPos(3, 3)));
            Buy(battle, 3, 3, Flying(Shadow));
            battle.Tick();
            Assert.AreSame(first, battle.ProjectileAt(0), "the shot object is reused");
            Assert.AreEqual(Shadow, battle.ProjectileAt(0).Element);
            Ticks(battle, 100);
            Assert.AreEqual(25f, victim.Health, "a Shadow shot cannot hurt a Shadow enemy, whatever object carried the Light shot");

            // (b) a shot with no element is followed by a Light shot from the same object: x2, and the reverse order: x1.
            Board board2 = OpenBoard();
            Battle battle2 = Parked(board2, chart);
            Buy(battle2, 3, 3, Flying(default));
            Enemy target = board2.SpawnEnemy(Still(Shadow));
            battle2.Tick();
            Projectile plain = battle2.ProjectileAt(0);
            Assert.IsTrue(plain.Element.IsNone);
            Assert.AreEqual(35f, TickToNextHit(battle2, target), "no element: x1");
            Assert.IsTrue(battle2.TrySellTower(new GridPos(3, 3)));
            Buy(battle2, 3, 3, Flying(Light));
            battle2.Tick();
            Assert.AreSame(plain, battle2.ProjectileAt(0));
            Assert.AreEqual(Light, battle2.ProjectileAt(0).Element);
            Assert.AreEqual(15f, TickToNextHit(battle2, target), "then a Light shot: 35 - 10 x 2");
            Assert.IsTrue(battle2.TrySellTower(new GridPos(3, 3)));
            Buy(battle2, 3, 3, Flying(default));
            battle2.Tick();
            Assert.AreSame(plain, battle2.ProjectileAt(0));
            Assert.IsTrue(battle2.ProjectileAt(0).Element.IsNone, "a Light shot's object carries no element once a no-element tower launches it");
            Assert.AreEqual(5f, TickToNextHit(battle2, target), "15 - 10 x 1");
        }

        // ---- 4b-AC3 immunity

        [Test]
        public void S4b_AC3_A_Shot_At_An_Enemy_Of_Its_Own_Element_Flies_Lands_And_Does_Nothing()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow(), 0, 100, 10);
            Buy(battle, 3, 3, Flying(Light)); // 3.16 cells away: 19 ticks of flight, a shot every 30 ticks
            Enemy enemy = board.SpawnEnemy(Still(Light));

            int launches = 0, landings = 0;
            for (int i = 0; i < 300; i++)
            {
                int before = battle.ProjectileCount;
                battle.Tick();
                if (battle.ProjectileCount > before) launches++;
                if (battle.ProjectileCount < before) landings++;
                Assert.AreEqual(enemy.MaxHealth, enemy.Health, $"tick {i + 1}");
            }

            Assert.GreaterOrEqual(launches, 9, "the tower kept shooting the immune enemy");
            Assert.GreaterOrEqual(landings, 9, "and its shots landed");
            Assert.IsTrue(enemy.IsOnBoard);
            Assert.AreEqual(0, battle.Gold);
            Assert.AreEqual(10, battle.Lives);
        }

        [Test]
        public void S4b_AC3_Immunity_Holds_Without_A_Chart_And_Whatever_The_Chart_Holds_For_Other_Pairs()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, null);
            Buy(battle, 3, 3, Flying(Light));
            Enemy immune = board.SpawnEnemy(Still(Light));
            Ticks(battle, 200);
            Assert.AreEqual(immune.MaxHealth, immune.Health, "no chart: the own element is still immune");

            Board bigBoard = OpenBoard();
            Battle big = Parked(bigBoard, LightShadow(1000f, 1000f));
            Buy(big, 3, 3, Flying(Shadow));
            Enemy shadow = bigBoard.SpawnEnemy(Still(Shadow));
            Ticks(big, 200);
            Assert.AreEqual(shadow.MaxHealth, shadow.Health, "a big chart does not break it");
        }

        [Test]
        public void S4b_AC3_A_Tower_That_Only_Wasted_Shots_Refunds_The_Percent_Not_The_Full_Cost()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow(), 10, 50);
            Buy(battle, 3, 3, Flying(Light, 10f, 10));
            Assert.AreEqual(0, battle.Gold);
            board.SpawnEnemy(Still(Light));
            Ticks(battle, 100);

            Assert.IsTrue(battle.TrySellTower(new GridPos(3, 3)));
            Assert.AreEqual(5, battle.Gold, "it has fired: 50 percent of 10");
        }

        [Test]
        public void S4b_AC3_A_Tower_That_Never_Fired_Refunds_The_Full_Cost_Even_Next_To_An_Immune_Enemy_Out_Of_Range()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow(), 10, 50);
            Buy(battle, 14, 0, new TowerStats(1f, 10f, 2f, 10f, 10, Light)); // range 1: the enemy at the spawn is far
            board.SpawnEnemy(Still(Light));
            Ticks(battle, 100);

            Assert.IsTrue(battle.TrySellTower(new GridPos(14, 0)));
            Assert.AreEqual(10, battle.Gold, "never fired: the full cost");
        }

        [Test]
        public void S4b_AC3_An_Immune_Enemy_Reaches_The_Exit_And_Costs_A_Life_At_Full_Health()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow(), 0, 100, 10);
            Buy(battle, 3, 3, Flying(Light));
            Enemy runner = board.SpawnEnemy(new EnemyStats(6f, 45f, 8, Light));

            Ticks(battle, 400); // 15 cells at 6 a second: 150 ticks

            Assert.IsFalse(runner.IsOnBoard);
            Assert.AreEqual(9, battle.Lives);
            Assert.AreEqual(0, battle.Gold, "no reward without a kill");
            Assert.AreEqual(runner.MaxHealth, runner.Health);
        }

        // ---- 4b-AC4 targeting is unchanged

        [Test]
        public void S4b_AC4_A_Light_Tower_Shoots_The_Nearer_Light_Enemy_And_Not_The_Shadow_One_Until_It_Leaves()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow(), 0, 100, 10);
            Enemy light = board.SpawnEnemy(new EnemyStats(3f, 45f, 0, Light));
            Ticks(battle, 30); // the Light enemy leads by 1.5 cells
            Enemy shadow = board.SpawnEnemy(new EnemyStats(1f, 45f, 0, Shadow));
            Buy(battle, 7, 3, Flying(Light)); // range 20: both are in range for the whole run

            int landingsOnLight = 0;
            int ticks = 0;
            while (light.IsOnBoard && ticks++ < 600)
            {
                int before = battle.ProjectileCount;
                battle.Tick();
                if (battle.ProjectileCount < before && light.IsOnBoard) landingsOnLight++;
                Assert.AreEqual(shadow.MaxHealth, shadow.Health, $"the Shadow enemy is not hit while the Light one leads, tick {ticks}");
                Assert.AreEqual(light.MaxHealth, light.Health, $"tick {ticks}");
            }

            Assert.IsFalse(light.IsOnBoard, "the Light enemy left");
            Assert.AreEqual(9, battle.Lives, "by the exit");
            Assert.Greater(landingsOnLight, 0, "shots were landing on the Light enemy");
            Assert.AreEqual(0, battle.Gold);
            Assert.IsTrue(shadow.IsOnBoard);

            Assert.AreEqual(25f, TickToNextHit(battle, shadow), "the tower turned to the Shadow enemy: 45 - 10 x 2");
        }

        [Test]
        public void S4b_AC4_The_Tower_Shoots_The_Enemy_Nearest_The_Exit_Whichever_Element_It_Has()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow(), 0, 100, 10);
            Enemy shadow = board.SpawnEnemy(new EnemyStats(3f, 45f, 4, Shadow));
            Ticks(battle, 30);
            Enemy light = board.SpawnEnemy(new EnemyStats(1f, 45f, 0, Light));
            Buy(battle, 7, 3, Flying(Light));

            int ticks = 0;
            while (shadow.IsOnBoard && ticks++ < 400)
            {
                battle.Tick();
                Assert.AreEqual(light.MaxHealth, light.Health, $"the Light enemy is untouched, tick {ticks}");
            }

            Assert.IsFalse(shadow.IsOnBoard, "three hits of 20 kill it");
            Assert.AreEqual(10, battle.Lives, "killed, not leaked");
            Assert.AreEqual(4, battle.Gold);
            Assert.AreEqual(light.MaxHealth, light.Health);
        }

        // ---- 4b-AC5 entry

        [Test]
        public void S4b_AC5_A_Shot_At_An_Enemy_Whose_Object_Is_Reused_By_The_Other_Element_Is_Dropped_And_Hurts_Nobody()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow());
            Buy(battle, 2, 3, new TowerStats(20f, 10f, 0.1f, 1f, 0, Light)); // one shot, slow: about 134 ticks of flight; the next after 600
            Enemy first = board.SpawnEnemy(Still(Light));
            Ticks(battle, 5);
            Assert.AreEqual(1, battle.ProjectileCount, "a shot is flying at the first enemy");

            board.RemoveEnemy(first);
            Enemy second = board.SpawnEnemy(Still(Shadow));
            Assert.AreSame(first, second, "the board reuses the object");

            battle.Tick();
            Assert.AreEqual(0, battle.ProjectileCount, "dropped on the next tick");
            Ticks(battle, 450);
            Assert.AreEqual(second.MaxHealth, second.Health, "the new enemy took no damage from the old shot");
            Assert.AreEqual(Shadow, second.Element);
            Assert.AreEqual(0, battle.Gold);
        }

        [Test]
        public void S4b_AC5_An_Enemy_Takes_The_Element_Of_Every_Entry_Also_On_A_Reused_Object()
        {
            Board board = OpenBoard();
            Battle battle = Parked(board, LightShadow());

            Enemy first = board.SpawnEnemy(Still(Light));
            Assert.AreEqual(Light, first.Element);
            Ticks(battle, 50);
            Assert.AreEqual(Light, first.Element, "it keeps its element while on the board");

            board.RemoveEnemy(first);
            Enemy second = board.SpawnEnemy(Still(Shadow));
            Assert.AreSame(first, second);
            Assert.AreEqual(Shadow, second.Element);

            board.RemoveEnemy(second);
            Enemy third = board.SpawnEnemy(new EnemyStats(1f, 10f));
            Assert.AreSame(first, third);
            Assert.IsTrue(third.Element.IsNone, "an entry with no element clears the earlier one");

            board.RemoveEnemy(third);
            Assert.AreEqual(Light, board.SpawnEnemy(Still(Light)).Element, "and back again");
        }

        [Test]
        public void S4b_AC5_Overlapping_Waves_Of_Different_Elements_Keep_Their_Own_Element_Each()
        {
            var lightEnemy = new EnemyStats(1f, 45f, 0, Light);
            var shadowEnemy = new EnemyStats(1f, 45f, 0, Shadow);
            var waves = new[] { new Wave(lightEnemy, 4, 0.5f, 0f), new Wave(shadowEnemy, 4, 0.5f, 1f) };
            Board board = OpenBoard();
            var battle = new Battle(board, waves, 100, 0, 100, LightShadow());

            bool overlapped = false;
            for (int tick = 1; tick <= 900; tick++)
            {
                battle.Tick();
                bool sawLight = false, sawShadow = false;
                for (int i = 0; i < board.EnemyCount; i++)
                {
                    Enemy enemy = board.EnemyAt(i);
                    Assert.AreEqual(enemy.WaveIndex == 0 ? Light : Shadow, enemy.Element, $"tick {tick}, enemy {i}");
                    sawLight |= enemy.Element == Light;
                    sawShadow |= enemy.Element == Shadow;
                }
                overlapped |= sawLight && sawShadow;
            }
            Assert.IsTrue(overlapped, "enemies of both elements shared the board");
        }

        [Test]
        public void S4b_AC5_Enemy_Objects_Reused_By_A_Wave_Of_The_Other_Element_Take_The_New_Element()
        {
            // Runners cross the board in about 16 ticks, so the second wave, a second later, finds the board empty and reuses the objects.
            var lightRunner = new EnemyStats(60f, 45f, 0, Light);
            var shadowRunner = new EnemyStats(60f, 45f, 0, Shadow);
            var waves = new[] { new Wave(lightRunner, 3, 0.1f, 0f), new Wave(shadowRunner, 3, 0.1f, 1f) };
            Board board = OpenBoard();
            var battle = new Battle(board, waves, 100, 0, 100, LightShadow());

            var lastElement = new Dictionary<Enemy, Element>();
            int reusedByOtherElement = 0;
            for (int tick = 1; tick <= 400; tick++)
            {
                battle.Tick();
                for (int i = 0; i < board.EnemyCount; i++)
                {
                    Enemy enemy = board.EnemyAt(i);
                    Assert.AreEqual(enemy.WaveIndex == 0 ? Light : Shadow, enemy.Element, $"tick {tick}");
                    if (lastElement.TryGetValue(enemy, out Element earlier) && earlier != enemy.Element) reusedByOtherElement++;
                    lastElement[enemy] = enemy.Element;
                }
            }
            Assert.Greater(reusedByOtherElement, 0, "an object that carried Light later carried Shadow");
            Assert.AreEqual(100 - 6, battle.Lives, "all six reached the exit");
        }

        // ---- 4b-AC13 determinism over a whole run with both elements

        private static List<long> RunWithBothElements(ElementChart chart, out Battle battle)
        {
            var light = new EnemyStats(2f, 45f, 3, Light);
            var shadow = new EnemyStats(2.5f, 45f, 3, Shadow);
            var waves = new List<Wave>();
            for (int i = 0; i < 12; i++)
            {
                // Alternating elements, 3 s apart: a wave enters while the one before still walks.
                waves.Add(i % 2 == 0 ? new Wave(light, 4, 0.4f, i == 0 ? 0f : 3f, 5) : new Wave(shadow, 3, 0.4f, 3f, 5));
            }

            Board board = PrototypeBoard();
            battle = new Battle(board, waves.ToArray(), 20, 30, 50, chart);
            var rnd = new System.Random(20240611);
            var record = new List<long>();
            for (int tick = 0; tick < 6000; tick++)
            {
                if (tick % 20 == 0)
                {
                    var cell = new GridPos(rnd.Next(16), rnd.Next(9));
                    Element element = rnd.Next(2) == 0 ? Light : Shadow;
                    if (rnd.Next(6) == 0) battle.TrySellTower(cell);
                    else battle.TryBuyTower(cell, new TowerStats(3.5f, 10f, 1.5f, 10f, 10, element));
                }
                battle.Tick();

                float healthSum = 0f;
                for (int i = 0; i < board.EnemyCount; i++) healthSum += board.EnemyAt(i).Health;
                record.Add(battle.Gold);
                record.Add(battle.Lives);
                record.Add(board.EnemyCount);
                record.Add(battle.ProjectileCount);
                record.Add(BitConverter.SingleToInt32Bits(healthSum));
            }
            return record;
        }

        [Test]
        public void S4b_AC13_Two_Runs_With_Both_Elements_And_A_Fixed_Seed_Are_Identical_Tick_For_Tick()
        {
            List<long> first = RunWithBothElements(LightShadow(), out Battle a);
            List<long> second = RunWithBothElements(LightShadow(), out Battle b);

            Assert.AreEqual(first, second);
            Assert.AreEqual(a.Outcome, b.Outcome);
            Assert.AreEqual(a.SurvivedWaves, b.SurvivedWaves);
            Assert.Greater(a.Board.TowerCount, 0, "towers were bought");
            Assert.AreNotEqual(30, a.Gold, "gold moved: kills and bonuses were paid");
        }

        [Test]
        public void S4b_AC13_The_Chart_Changes_The_Run_So_The_Determinism_Test_Exercises_The_Elements()
        {
            List<long> withChart = RunWithBothElements(LightShadow(), out _);
            List<long> withoutChart = RunWithBothElements(null, out _);

            Assert.AreNotEqual(withChart, withoutChart);
        }

        // ---- 4b-AC14 allocation

        [Test]
        public void S4b_AC14_Tick_Allocates_Nothing_After_The_Peak_With_Hits_Of_Both_Kinds()
        {
            var light = new EnemyStats(2f, 30f, 3, Light);
            var shadow = new EnemyStats(2f, 30f, 3, Shadow);
            var waves = new List<Wave> { new Wave(light, 14, 0.5f, 0f), new Wave(shadow, 6, 0.5f, 25f) }; // the board empties after the peak
            for (int i = 0; i < 60; i++) waves.Add(new Wave(i % 2 == 0 ? light : shadow, 6, 0.5f, 1f));

            Board board = PrototypeBoard();
            var battle = new Battle(board, waves.ToArray(), 100_000, 100, 50, LightShadow());
            var tower = new TowerStats(2.5f, 10f, 1.5f, 10f, 2, Light);
            Buy(battle, 6, 6, tower);
            Buy(battle, 11, 2, new TowerStats(2.5f, 10f, 1.5f, 10f, 2, Shadow));
            Buy(battle, 6, 2, tower);
            Ticks(battle, 3000); // warm-up: the pools reach their peak and the board empties once

            int immuneLandings = 0, damagingTicks = 0, kills = 0;
            int projectiles = battle.ProjectileCount, enemies = board.EnemyCount, lives = battle.Lives;
            float healthSum = SumHealth(board);
            TickAllocation.AssertNoneAcross(battle, 1500, _ =>
            {
                float sum = SumHealth(board);
                bool landed = battle.ProjectileCount < projectiles;
                if (landed && board.EnemyCount == enemies && battle.Lives == lives && sum == healthSum) immuneLandings++; // a landing that changed nothing
                if (sum < healthSum) damagingTicks++;
                if (board.EnemyCount < enemies && battle.Lives == lives) kills++;
                projectiles = battle.ProjectileCount;
                enemies = board.EnemyCount;
                lives = battle.Lives;
                healthSum = sum;
            });

            Assert.Greater(immuneLandings, 0, "shots were wasted on immune enemies in the measured ticks");
            Assert.Greater(damagingTicks, 0, "shots hurt in the measured ticks");
            Assert.Greater(kills, 0, "enemies died in the measured ticks");
            Assert.AreEqual(RunOutcome.Running, battle.Outcome);
        }

        private static float SumHealth(Board board)
        {
            float sum = 0f;
            for (int i = 0; i < board.EnemyCount; i++) sum += board.EnemyAt(i).Health;
            return sum;
        }
    }
}
