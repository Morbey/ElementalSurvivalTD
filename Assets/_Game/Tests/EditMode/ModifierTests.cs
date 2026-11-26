using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using static ElementalTD.Core.Tests.PerkKit;

namespace ElementalTD.Core.Tests
{
    /// <summary>Step 5b, 5b-AC1 (modifiers) and 5b-AC2 (towers): the modifier pin and the rules 16 to 19, in Unity's Mono.</summary>
    public class ModifierTests
    {
        private static readonly GridPos Far = new GridPos(8, 8);

        private static Modifiers Added(params int[] picks)
        {
            Modifier[] pool = PinPool();
            var modifiers = new Modifiers();
            foreach (int pick in picks) modifiers.Add(pool[pick]);
            return modifiers;
        }

        private static TowerStats Stats(Element element) => new TowerStats(2.5f, 10f, 1.5f, 10f, 40, element);

        // ---- 5b-AC1 Modifiers

        [Test]
        public void S5b_AC1_The_Modifier_Pin_After_The_Picks_0_2_3_0_2_Each_Value_By_Its_Bits()
        {
            Modifiers modifiers = Added(0, 2, 3, 0, 2);
            TowerStats light = modifiers.Apply(Stats(Light));
            TowerStats shadow = modifiers.Apply(Stats(Shadow));

            Assert.AreEqual(15.625f, light.Damage, 0f, "Light damage 10 x1.25 x1.25");
            Assert.AreEqual(10f, shadow.Damage, 0f, "Shadow damage untouched");
            Assert.AreEqual(0x3FFDEB84, Bits(light.ShotsPerSecond), "Light rate 1.9837499");
            Assert.AreEqual(0x3FFDEB84, Bits(shadow.ShotsPerSecond), "Shadow rate 1.9837499");
            Assert.AreEqual(3.0f, light.Range, 0f);
            Assert.AreEqual(3.0f, shadow.Range, 0f);
            // The plan prints 0.5040958, seven digits; the float32 quotient is 0.50409581 (the literal 0.5040958f is the float below it).
            Assert.AreEqual(0.5040958f, 1f / light.ShotsPerSecond, 1e-7f, "the cooldown added per launch");
            Assert.AreEqual(0.5040958f, 1f / shadow.ShotsPerSecond, 1e-7f);
            Assert.AreEqual(Bits(1f / light.ShotsPerSecond), Bits(1f / shadow.ShotsPerSecond));
        }

        [Test]
        public void S5b_AC1_One_Pick_Of_The_Rate_Gives_1_7249999_And_Three_Give_2_2813122_Not_The_Product_Rounded_Once()
        {
            Assert.AreEqual(0x3FDCCCCC, Bits(Added(2).Apply(Stat.TowerShotsPerSecond, Light, 1.5f)), "one pick, 1.7249999");
            Assert.AreEqual(0x40120105, Bits(Added(2, 2, 2).Apply(Stat.TowerShotsPerSecond, Light, 1.5f)), "three picks: float32 after each");
            // What a mutation (the product of the stacks rounded once, then applied) would give.
            float once = 1.5f * (1.15f * 1.15f * 1.15f);
            Assert.AreEqual(0x40120106, Bits(once), "the plan's other value really is the product rounded once");
            Assert.AreNotEqual(Bits(once), Bits(Added(2, 2, 2).Apply(Stat.TowerShotsPerSecond, Light, 1.5f)));
        }

        [Test]
        public void S5b_AC1_The_Pin_Is_The_Same_Through_The_Tower_Stats_Overload_And_Changes_Nothing()
        {
            Modifiers modifiers = Added(0, 2, 3, 0, 2);
            var stats = Stats(Light);
            TowerStats effective = modifiers.Apply(stats);
            Assert.AreEqual(5, modifiers.Count, "Apply adds nothing");
            Assert.AreEqual((2.5f, 10f, 1.5f), (stats.Range, stats.Damage, stats.ShotsPerSecond), "the given stats are a value and stay");
            Assert.AreEqual((10f, 40, Light), (effective.ProjectileSpeed, effective.Cost, effective.Element), "the rest as given");
        }

        [Test]
        public void S5b_AC1_Additions_Come_Before_Multiplications_Whatever_The_Pick_Order()
        {
            var multiplyFirst = new Modifiers();
            multiplyFirst.Add(Mod(Stat.TowerDamage, Operation.Multiply, 1.5f));
            multiplyFirst.Add(Mod(Stat.TowerDamage, Operation.Add, 2f));
            var addFirst = new Modifiers();
            addFirst.Add(Mod(Stat.TowerDamage, Operation.Add, 2f));
            addFirst.Add(Mod(Stat.TowerDamage, Operation.Multiply, 1.5f));

            Assert.AreEqual(18f, multiplyFirst.Apply(Stat.TowerDamage, default, 10f), 0f, "(10 + 2) x 1.5, not 10 x 1.5 + 2 = 17");
            Assert.AreEqual(18f, addFirst.Apply(Stat.TowerDamage, default, 10f), 0f);
        }

        [Test]
        public void S5b_AC1_Percents_Compound_Instead_Of_Adding_Up()
        {
            Assert.AreEqual(15.625f, Added(0, 0).Apply(Stat.TowerDamage, Light, 10f), 0f, "15.625, not 15");
        }

        [Test]
        public void S5b_AC1_Several_Additions_Add_In_The_Order_Added_And_A_Stat_Reads_Only_Its_Own()
        {
            var modifiers = new Modifiers();
            modifiers.Add(Mod(Stat.TowerRange, Operation.Add, 0.5f));
            modifiers.Add(Mod(Stat.TowerRange, Operation.Add, 0.25f));
            modifiers.Add(Mod(Stat.TowerDamage, Operation.Add, 9f));
            Assert.AreEqual(3.25f, modifiers.Apply(Stat.TowerRange, default, 2.5f), 0f);
            Assert.AreEqual(2.5f, modifiers.Apply(Stat.TowerShotsPerSecond, default, 2.5f), 0f, "another stat is not read");
        }

        [Test]
        public void S5b_AC1_An_Element_Perk_Leaves_The_Other_Element_And_A_Tower_Of_No_Element_Alone()
        {
            Modifiers lightOnly = Added(0);
            Assert.AreEqual(12.5f, lightOnly.Apply(Stat.TowerDamage, Light, 10f), 0f);
            Assert.AreEqual(10f, lightOnly.Apply(Stat.TowerDamage, Shadow, 10f), 0f, "the other element");
            Assert.AreEqual(10f, lightOnly.Apply(Stat.TowerDamage, default, 10f), 0f, "a tower of no element takes no element's modifier");

            Modifiers everyTower = Added(2, 3);
            Assert.AreEqual(2.5f + 0.5f, everyTower.Apply(Stat.TowerRange, default, 2.5f), 0f, "a modifier of no element reaches a tower of no element");
            Assert.AreEqual(3.0f, everyTower.Apply(Stat.TowerRange, Light, 2.5f), 0f, "and every element");
            Assert.AreEqual(0x3FDCCCCC, Bits(everyTower.Apply(Stat.TowerShotsPerSecond, default, 1.5f)), "also the rate");
        }

        [Test]
        public void S5b_AC1_A_Whole_Stat_Takes_Its_Additions_And_Reads_Nothing_Else()
        {
            var modifiers = new Modifiers();
            Assert.AreEqual(10, modifiers.ApplyWhole(Stat.GoldPerKill, 10), "no modifier, the reward");
            modifiers.Add(Mod(Stat.GoldPerKill, Operation.Add, 2f));
            modifiers.Add(Mod(Stat.TowerRange, Operation.Add, 1f));
            modifiers.Add(Mod(Stat.GoldPerKill, Operation.Add, 3f));
            modifiers.Add(Mod(Stat.TowerDamage, Operation.Multiply, 2f));
            Assert.AreEqual(15, modifiers.ApplyWhole(Stat.GoldPerKill, 10), "10 + 2 + 3; the tower range +1 is not gold");
            Assert.AreEqual(0, new Modifiers().ApplyWhole(Stat.GoldPerKill, 0));
        }

        [Test]
        public void S5b_AC1_Stacked_Applies_The_Modifier_That_Many_Times_To_0_Or_1_Rounded_After_Each()
        {
            Modifier add = Mod(Stat.TowerRange, Operation.Add, 0.5f);
            Modifier multiply = Mod(Stat.TowerDamage, Operation.Multiply, 1.25f);
            Assert.AreEqual(0f, add.Stacked(0), 0f);
            Assert.AreEqual(1f, multiply.Stacked(0), 0f);
            Assert.AreEqual(0.5f, add.Stacked(1), 0f);
            Assert.AreEqual(1.25f, multiply.Stacked(1), 0f);
            Assert.AreEqual(1f, add.Stacked(2), 0f);
            Assert.AreEqual(1.5625f, multiply.Stacked(2), 0f);
            Assert.AreEqual(1.5f, add.Stacked(3), 0f);
            Assert.AreEqual(1.953125f, multiply.Stacked(3), 0f);
            Assert.Throws<ArgumentOutOfRangeException>(() => add.Stacked(-1));
        }

        [Test]
        public void S5b_AC1_A_Modifier_Keeps_Its_Fields_And_A_Source_Of_Minus_1_When_None_Is_Given()
        {
            var modifier = new Modifier(Stat.TowerRange, Operation.Add, 0.5f, Shadow, 4);
            Assert.AreEqual((Stat.TowerRange, Operation.Add, 0.5f, Shadow, 4), (modifier.Stat, modifier.Operation, modifier.Value, modifier.Element, modifier.Source));
            Assert.AreEqual(-1, Mod(Stat.TowerRange, Operation.Add, 1f).Source);
            Assert.IsTrue(Modifier.IsTowerStat(Stat.TowerDamage) && Modifier.IsTowerStat(Stat.TowerShotsPerSecond) && Modifier.IsTowerStat(Stat.TowerRange));
            Assert.IsFalse(Modifier.IsTowerStat(Stat.GoldPerKill));
        }

        [Test]
        public void S5b_AC1_A_Modifier_That_Multiplies_Gold_Adds_A_Fraction_To_It_Or_Puts_An_Element_On_It_Is_Refused_When_Made()
        {
            var multiply = Assert.Throws<ArgumentException>(() => new Modifier(Stat.GoldPerKill, Operation.Multiply, 2f));
            StringAssert.Contains("GoldPerKill", multiply.Message);
            StringAssert.Contains("addition", multiply.Message);

            var fraction = Assert.Throws<ArgumentOutOfRangeException>(() => new Modifier(Stat.GoldPerKill, Operation.Add, 1.5f));
            StringAssert.Contains("GoldPerKill", fraction.Message);
            StringAssert.Contains("whole", fraction.Message);

            var element = Assert.Throws<ArgumentException>(() => new Modifier(Stat.GoldPerKill, Operation.Add, 2f, Light));
            StringAssert.Contains("tower stat", element.Message);
            StringAssert.Contains("GoldPerKill", element.Message);

            Assert.Throws<ArgumentOutOfRangeException>(() => new Modifier(Stat.GoldPerKill, Operation.Add, float.NaN), "NaN is not whole");
            Assert.DoesNotThrow(() => new Modifier(Stat.GoldPerKill, Operation.Add, 2f), "a whole addition is allowed");
            Assert.DoesNotThrow(() => new Modifier(Stat.GoldPerKill, Operation.Add, 0f), "an addition of 0 is allowed in Core; the asset refuses it");
        }

        [Test]
        public void S5b_AC1_Nothing_Removes_A_Modifier_The_List_Only_Grows_And_Is_Read_By_Index()
        {
            Modifiers modifiers = Added(0, 2, 3);
            Assert.AreEqual(3, modifiers.Count);
            Assert.AreEqual(Stat.TowerShotsPerSecond, modifiers.At(1).Stat, "in the order added");
            Assert.AreEqual(Stat.TowerRange, modifiers.At(2).Stat);
            Assert.Throws<ArgumentOutOfRangeException>(() => modifiers.At(3));
            Assert.AreEqual(0, new Modifiers().Count);
            var removers = typeof(Modifiers).GetMethods().Where(m => m.Name.StartsWith("Remove") || m.Name.StartsWith("Clear"));
            Assert.IsEmpty(removers, "no method removes a modifier");
        }

        // ---- 5b-AC2 Towers

        private static readonly Wave[] LongDelay = { new Wave(new EnemyStats(1f, 10f), 1, 1f, 1_000_000f) };

        /// <summary>A battle with the pause pin's pool of 3 (every offer holds all three) and a table of 1 XP per level.</summary>
        private static Battle PoolOfThree(Board board, int gold = 1000, int refund = 50, Wave[] waves = null)
        {
            return new Battle(board, waves ?? LongDelay, 10, gold, refund, Chart(), new Progression(new[] { 1, 1, 1, 1, 1 }, PausePool(), 7));
        }

        private static void PickPlaces(Battle battle, params int[] perks)
        {
            battle.Progression.AddXp(perks.Length);
            battle.Tick();
            foreach (int perk in perks) PickPerkByPlace(battle, perk);
            Assert.IsFalse(battle.AwaitingChoice, "every choice answered");
        }

        [Test]
        public void S5b_AC2_A_Tower_Bought_After_Picks_Has_The_Effective_Stats_And_Keeps_The_Base()
        {
            Battle battle = PoolOfThree(OpenBoard());
            PickPlaces(battle, 0, 1, 0);

            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(new GridPos(3, 3), Tower(Light)));
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(new GridPos(3, 5), Tower(Shadow)));
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(new GridPos(5, 5), Tower(default)));
            Tower light = battle.Board.TowerOn(new GridPos(3, 3));
            Tower shadow = battle.Board.TowerOn(new GridPos(3, 5));
            Tower plain = battle.Board.TowerOn(new GridPos(5, 5));

            Assert.AreEqual((15.625f, 3.0f), (light.Stats.Damage, light.Stats.Range));
            Assert.AreEqual((10f, 3.0f), (shadow.Stats.Damage, shadow.Stats.Range), "the other element: range only");
            Assert.AreEqual((10f, 3.0f), (plain.Stats.Damage, plain.Stats.Range), "no element: the modifiers of no element only");
            Assert.AreEqual((10f, 2.5f, 1.5f), (light.BaseStats.Damage, light.BaseStats.Range, light.BaseStats.ShotsPerSecond), "the base is the bought one");
            Assert.AreEqual(1000 - 3 * 40, battle.Gold, "three purchases at the base cost");
            Assert.AreEqual((40, 40, 40), (light.Stats.Cost, shadow.Stats.Cost, plain.Stats.Cost));
        }

        [Test]
        public void S5b_AC2_A_Tower_With_No_Pick_Yet_Has_Its_Base_As_Its_Stats()
        {
            Battle battle = PoolOfThree(OpenBoard());
            battle.TryBuyTower(Far, Tower(Light));
            Tower tower = battle.Board.TowerOn(Far);
            Assert.AreEqual((10f, 2.5f, 1.5f), (tower.Stats.Damage, tower.Stats.Range, tower.Stats.ShotsPerSecond));
            Assert.AreEqual(tower.BaseStats.Damage, tower.Stats.Damage);
        }

        [Test]
        public void S5b_AC2_Every_Tower_On_The_Board_Is_Recomputed_On_A_Pick_From_Its_Base_Not_From_Its_Effective_Stats()
        {
            Battle battle = PoolOfThree(OpenBoard());
            battle.TryBuyTower(new GridPos(3, 3), Tower(Light));
            battle.TryBuyTower(new GridPos(3, 5), Tower(Shadow));
            battle.TryBuyTower(new GridPos(5, 5), Tower(Light));

            battle.Progression.AddXp(2);
            battle.Tick();
            PickPerkByPlace(battle, 0);
            Assert.AreEqual(12.5f, battle.Board.TowerOn(new GridPos(3, 3)).Stats.Damage, 0f, "after the first pick");
            Assert.AreEqual(12.5f, battle.Board.TowerOn(new GridPos(5, 5)).Stats.Damage, 0f, "every Light tower, not only the first");
            Assert.AreEqual(10f, battle.Board.TowerOn(new GridPos(3, 5)).Stats.Damage, 0f);
            PickPerkByPlace(battle, 0);
            Assert.AreEqual(15.625f, battle.Board.TowerOn(new GridPos(3, 3)).Stats.Damage, 0f, "10 x1.25 x1.25; from the effective 12.5 it would be 19.53");
            Assert.AreEqual(10f, battle.Board.TowerOn(new GridPos(3, 3)).BaseStats.Damage, 0f);
            Assert.AreEqual(2, battle.Modifiers.Count);
        }

        [Test]
        public void S5b_AC2_A_Tower_Placed_On_The_Board_Directly_Takes_The_Modifiers_At_The_Next_Pick()
        {
            Battle battle = PoolOfThree(OpenBoard());
            battle.Board.TryPlaceTower(Far, Tower(Light));
            Assert.AreEqual(10f, battle.Board.TowerOn(Far).Stats.Damage, 0f, "base until the next pick");
            PickPlaces(battle, 0);
            Assert.AreEqual(12.5f, battle.Board.TowerOn(Far).Stats.Damage, 0f);
        }

        [Test]
        public void S5b_AC2_The_Sell_Value_And_The_Gold_Check_Of_A_Purchase_Read_The_Base_Cost()
        {
            Battle battle = PoolOfThree(OpenBoard(), gold: 40, refund: 50);
            PickPlaces(battle, 0, 1, 2);
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(Far, Tower(Light)), "exactly the base cost is enough after every pick");
            Assert.AreEqual(0, battle.Gold);
            Assert.IsTrue(battle.TryGetSellValue(Far, out int unfired));
            Assert.AreEqual(40, unfired, "a tower that never fired sells for its base cost");

            Battle poor = PoolOfThree(OpenBoard(), gold: 39);
            PickPlaces(poor, 0, 1, 2);
            Assert.AreEqual(PlacementResult.NotEnoughGold, poor.TryBuyTower(Far, Tower(Light)), "39 is short of the base cost 40");
            Assert.AreEqual(39, poor.Gold);
        }

        [Test]
        public void S5b_AC2_A_Fired_Tower_Refunds_The_Percent_Of_The_Base_Cost_After_A_Damage_Pick()
        {
            Battle battle = PoolOfThree(OpenBoard(), gold: 40, refund: 50,
                waves: new[] { new Wave(new EnemyStats(0f, 1000f), 1, 1f, 0f) });
            battle.TryBuyTower(new GridPos(1, 4), Tower(default));
            battle.Tick();
            Assert.Greater(battle.ProjectileCount + battle.HitCount, 0, "it has fired");
            PickPlaces(battle, 0, 1, 2);
            Assert.IsTrue(battle.TryGetSellValue(new GridPos(1, 4), out int gold));
            Assert.AreEqual(20, gold, "50 % of 40");
            Assert.IsTrue(battle.TrySellTower(new GridPos(1, 4)));
            Assert.AreEqual(20, battle.Gold);
        }

        private static List<int> LaunchTicks(Battle battle, int upToTick, Action<int> afterTick = null)
        {
            var launches = new List<int>();
            while (battle.TickCount < upToTick && !battle.AwaitingChoice)
            {
                int before = battle.ProjectileCount + battle.HitCount;
                battle.Tick();
                int after = battle.ProjectileCount + battle.HitCount;
                // A tick lands a shot and launches one; with no kill and no loss of target, the change counts launches only.
                for (int i = before; i < after; i++) launches.Add(battle.TickCount);
                afterTick?.Invoke(battle.TickCount);
            }
            return launches;
        }

        private static Battle Duel(Modifier[] pool, float shotsPerSecond, float projectileSpeed, float damage = 10f, float range = 2.5f)
        {
            var board = OpenBoard();
            var waves = new[] { new Wave(new EnemyStats(0f, 100000f), 1, 1f, 0f) };
            var battle = new Battle(board, waves, 10, 100, 100, null, new Progression(new[] { 1, 1, 1 }, pool, 3));
            Assert.AreEqual(PlacementResult.Placed,
                battle.TryBuyTower(new GridPos(2, 4), new TowerStats(range, damage, shotsPerSecond, projectileSpeed, 40, Light)));
            return battle;
        }

        [Test]
        public void S5b_AC2_A_Shot_In_Flight_Keeps_Its_Damage_And_A_Running_Cooldown_Keeps_Its_Time_And_The_Next_Launch_Adds_The_New_Cooldown()
        {
            Modifier[] pool =
            {
                Mod(Stat.TowerDamage, Operation.Multiply, 2f, Light),
                Mod(Stat.TowerShotsPerSecond, Operation.Multiply, 2f),
                Mod(Stat.GoldPerKill, Operation.Add, 1f),
            };
            // A slow shot (1 cell/s over 2 cells: about 120 ticks in flight), 1 shot/s.
            Battle reference = Duel(pool, 1f, 1f);
            List<int> noPick = LaunchTicks(reference, 400);

            Battle picked = Duel(pool, 1f, 1f);
            var hits = new List<(int tick, float amount)>();
            int seenHits = 0;
            List<int> launches = LaunchTicks(picked, 30);
            // Shot 1 is in flight and the cooldown is running when the pick lands at the end of tick 30.
            picked.Progression.AddXp(2);
            picked.Tick();
            Assert.IsTrue(picked.AwaitingChoice);
            Assert.AreEqual(1, picked.ProjectileCount, "a shot is in flight at the pick");
            PickPerkByPlace(picked, 0);   // Light damage x2
            PickPerkByPlace(picked, 1);   // every tower's rate x2
            launches.AddRange(LaunchTicks(picked, 400, tick =>
            {
                while (seenHits < picked.HitCount)
                {
                    Hit hit = picked.HitAt(seenHits++);
                    hits.Add((hit.Tick, hit.Amount));
                }
            }));

            Assert.AreEqual(1, noPick[0], "the first launch is on tick 1");
            Assert.AreEqual(noPick[1], launches[1], "the second launch keeps the running cooldown's time, not the new rate's");
            int newGap = launches[2] - launches[1];
            Assert.That(newGap, Is.InRange(29, 31), "from the second launch the cooldown adds 1 / 2 s");
            Assert.Greater(noPick[2] - noPick[1], 55, "the unpicked run keeps its 1 s rhythm");
            Assert.AreEqual(10f, hits[0].amount, 0f, "the shot in flight at the pick lands with the damage it was launched with");
            Assert.AreEqual(20f, hits[1].amount, 0f, "the second shot, launched after the pick, carries the new damage");
        }

        [Test]
        public void S5b_AC2_A_Shot_Launched_After_The_Pick_Carries_The_New_Damage()
        {
            Modifier[] pool =
            {
                Mod(Stat.TowerDamage, Operation.Multiply, 2f, Light),
                Mod(Stat.TowerRange, Operation.Add, 1f),
                Mod(Stat.GoldPerKill, Operation.Add, 1f),
            };
            Battle battle = Duel(pool, 1f, 100f);   // lands on the tick it launches
            for (int i = 0; i < 100 && battle.HitCount < 1; i++) battle.Tick();
            Assert.AreEqual(10f, battle.HitAt(0).Amount, 0f);
            battle.Progression.AddXp(1);
            battle.Tick();
            PickPerkByPlace(battle, 0);
            for (int i = 0; i < 200 && battle.HitCount < 2; i++) battle.Tick();
            Assert.AreEqual(20f, battle.HitAt(1).Amount, 0f, "Light damage x2 on the shot launched after the pick");
        }

        [Test]
        public void S5b_AC2_A_Target_Inside_The_New_Range_And_Outside_The_Old_Is_Shot_On_The_Next_Tick()
        {
            Modifier[] pool =
            {
                Mod(Stat.TowerRange, Operation.Add, 0.5f),
                Mod(Stat.TowerDamage, Operation.Multiply, 2f, Light),
                Mod(Stat.GoldPerKill, Operation.Add, 1f),
            };
            // The enemy stands still on the spawn (0,4); the tower on (3,4) is 3 away, range 2.5 does not reach it.
            var board = OpenBoard();
            var battle = new Battle(board, new[] { new Wave(new EnemyStats(0f, 1000f), 1, 1f, 0f) }, 10, 100, 100, null,
                new Progression(new[] { 1 }, pool, 3));
            battle.TryBuyTower(new GridPos(3, 4), Tower(Light));
            for (int i = 0; i < 5; i++) battle.Tick();
            Assert.AreEqual(1, board.EnemyCount);
            Assert.AreEqual(0, battle.ProjectileCount + battle.HitCount, "out of range: no shot");

            battle.Progression.AddXp(1);
            battle.Tick();
            Assert.AreEqual(0, battle.ProjectileCount + battle.HitCount, "still none while the choice opens");
            PickPerkByPlace(battle, 0);
            battle.Tick();
            Assert.AreEqual(1, battle.ProjectileCount + battle.HitCount, "range 3.0 is inclusive: shot on the next tick");
        }

        [Test]
        public void S5b_AC2_A_Kill_Pays_Its_Reward_Plus_Every_Gold_Addition_And_A_Bonus_Or_An_Exit_Pays_Nothing_More()
        {
            Modifier[] pool = PausePool();   // perk 2: gold +2
            // Two levels at 1 XP: the same perk taken twice adds 4.
            var board = OpenBoard();
            var waves = new[] { new Wave(new EnemyStats(0f, 10f, 10, default, 0), 1, 1f, 0f, 7) };
            var battle = new Battle(board, waves, 10, 100, 100, null, new Progression(new[] { 1, 1 }, pool, 5));
            battle.Progression.AddXp(2);
            battle.Tick();
            PickPerkByPlace(battle, 2);
            PickPerkByPlace(battle, 2);
            Assert.AreEqual(100, battle.Gold, "the pick pays nothing");

            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(new GridPos(1, 3), new TowerStats(5f, 10f, 1f, 100f, 40)));
            Assert.AreEqual(60, battle.Gold);
            for (int i = 0; i < 10 && battle.Outcome == RunOutcome.Running; i++) battle.Tick();
            Assert.AreEqual(RunOutcome.Won, battle.Outcome);
            Assert.AreEqual(60 + 10 + 4 + 7, battle.Gold, "reward 10, two gold perks +4, the wave bonus 7 with no perk on it");
        }

        [Test]
        public void S5b_AC2_An_Enemy_That_Leaves_By_The_Exit_Pays_No_Gold_Perk()
        {
            var board = OpenBoard();
            var waves = new[] { new Wave(new EnemyStats(200f, 10f, 10, default, 0), 1, 1f, 0f, 0) };
            var battle = new Battle(board, waves, 3, 50, 100, null, new Progression(new[] { 1 }, PausePool(), 5));
            battle.Progression.AddXp(1);
            battle.Tick();
            PickPerkByPlace(battle, 2);
            for (int i = 0; i < 20 && battle.Outcome == RunOutcome.Running; i++) battle.Tick();
            Assert.AreEqual(2, battle.Lives, "the enemy left by the exit");
            Assert.AreEqual(50, battle.Gold, "no gold at all: neither reward nor perk");
        }

        [Test]
        public void S5b_AC2_A_Battle_Has_Its_Own_Modifiers_Never_Null_And_Empty()
        {
            var battle = new Battle(OpenBoard(), LongDelay, 1);
            Assert.IsNotNull(battle.Modifiers);
            Assert.AreEqual(0, battle.Modifiers.Count);
            Assert.AreNotSame(battle.Modifiers, new Battle(OpenBoard(), LongDelay, 1).Modifiers);
        }
    }
}
