using System;
using System.Collections.Generic;
using NUnit.Framework;
using static ElementalTD.Core.Tests.PerkKit;

namespace ElementalTD.Core.Tests
{
    /// <summary>Step 5b, 5b-AC3: the generator pin and the draw (rules 20 and 21), in Unity's Mono.</summary>
    public class DrawTests
    {
        private static readonly Wave[] LongDelay = { new Wave(new EnemyStats(1f, 10f), 1, 1f, 1_000_000f) };

        private static Modifier[] Pool(int count)
        {
            var pool = new Modifier[count];
            for (int i = 0; i < count; i++) pool[i] = Mod(Stat.TowerRange, Operation.Add, 0.5f);
            return pool;
        }

        private static Battle BattleOf(int perks, uint seed, int levels)
        {
            var table = new int[levels];
            for (int i = 0; i < levels; i++) table[i] = 1;
            return new Battle(OpenBoard(), LongDelay, 10, 0, 100, null, new Progression(table, Pool(perks), seed));
        }

        /// <summary>Opens the row of `draws` choices and answers each with slot 0, listing every offer.</summary>
        private static List<int[]> Offers(Battle battle, int draws)
        {
            battle.Progression.AddXp(draws);
            battle.Tick();
            var offers = new List<int[]>();
            for (int i = 0; i < draws; i++)
            {
                Assert.IsTrue(battle.AwaitingChoice, $"choice {i + 1} is open");
                offers.Add(Offer(battle.Progression));
                battle.PickPerk(0);
            }
            return offers;
        }

        // ---- The generator pin

        [Test]
        public void S5b_AC3_Seed_1_Returns_96A0F96B_12BC8390_971E9964()
        {
            var rng = new Rng(1);
            Assert.AreEqual(0x96A0F96Bu, rng.Next());
            Assert.AreEqual(0x12BC8390u, rng.Next());
            Assert.AreEqual(0x971E9964u, rng.Next());
        }

        [Test]
        public void S5b_AC3_Seed_0_Is_Allowed_And_Starts_With_92CA2F0E()
        {
            Assert.AreEqual(0x92CA2F0Eu, new Rng(0).Next());
        }

        [Test]
        public void S5b_AC3_Two_Generators_On_One_Seed_Agree_And_Two_Seeds_Differ()
        {
            var a = new Rng(12345);
            var b = new Rng(12345);
            var c = new Rng(12346);
            bool differ = false;
            for (int i = 0; i < 50; i++)
            {
                uint x = a.Next();
                Assert.AreEqual(x, b.Next());
                if (x != c.Next()) differ = true;
            }
            Assert.IsTrue(differ);
        }

        [Test]
        public void S5b_AC3_The_Generator_Wraps_Modulo_2_To_The_32_At_The_Largest_Seed()
        {
            var rng = new Rng(uint.MaxValue);
            Assert.DoesNotThrow(() => { for (int i = 0; i < 10; i++) rng.Next(); });
            // State after one step is 0xFFFFFFFF + 0x9E3779B9 = 0x9E3779B8 modulo 2^32; the mix is rule 20's.
            uint z = 0x9E3779B8u;
            z ^= z >> 16; z = unchecked(z * 0x85EBCA6Bu); z ^= z >> 13; z = unchecked(z * 0xC2B2AE35u); z ^= z >> 16;
            Assert.AreEqual(z, new Rng(uint.MaxValue).Next());
        }

        [Test]
        public void S5b_AC3_Below_Is_The_64_Bit_Product_Shifted_And_Stays_Inside_0_To_Count_Minus_1()
        {
            var a = new Rng(99);
            var b = new Rng(99);
            foreach (int n in new[] { 1, 2, 3, 5, 6, 1000, int.MaxValue })
            {
                int below = a.Below(n);
                int expected = (int)(((ulong)b.Next() * (ulong)n) >> 32);
                Assert.AreEqual(expected, below, $"Below({n})");
                Assert.That(below, Is.InRange(0, n - 1));
            }
            Assert.AreEqual(0, new Rng(5).Below(1), "a count of 1 has one answer");
            Assert.Throws<ArgumentOutOfRangeException>(() => new Rng(5).Below(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Rng(5).Below(-3));
        }

        [Test]
        public void S5b_AC3_Below_Takes_The_High_Bits_Not_A_Remainder()
        {
            // With 0x96A0F96B, the 64-bit product gives 2 while a remainder by 5 gives 1.
            Assert.AreEqual(2, new Rng(1).Below(5));
            Assert.AreEqual(1u, 0x96A0F96Bu % 5, "the test's premise: the remainder would differ");
        }

        // ---- The draw

        [Test]
        public void S5b_AC3_Seed_1_With_A_Pool_Of_5_Draws_2_1_3_Then_2_0_4_Then_4_2_1_Then_3_2_1()
        {
            List<int[]> offers = Offers(BattleOf(5, 1, 4), 4);
            CollectionAssert.AreEqual(new[] { 2, 1, 3 }, offers[0]);
            CollectionAssert.AreEqual(new[] { 2, 0, 4 }, offers[1]);
            CollectionAssert.AreEqual(new[] { 4, 2, 1 }, offers[2]);
            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, offers[3]);
        }

        [Test]
        public void S5b_AC3_Seed_2_With_A_Pool_Of_5_Draws_3_1_2_Then_2_3_4_Then_2_1_4_Then_0_1_4()
        {
            List<int[]> offers = Offers(BattleOf(5, 2, 4), 4);
            CollectionAssert.AreEqual(new[] { 3, 1, 2 }, offers[0]);
            CollectionAssert.AreEqual(new[] { 2, 3, 4 }, offers[1]);
            CollectionAssert.AreEqual(new[] { 2, 1, 4 }, offers[2]);
            CollectionAssert.AreEqual(new[] { 0, 1, 4 }, offers[3]);
        }

        [Test]
        public void S5b_AC3_The_First_Offer_Is_The_Pins_First_Draw_However_Much_XP_Came_Before_And_However_Many_Ticks_Ran()
        {
            Battle battle = BattleOf(5, 1, 6);
            for (int i = 0; i < 100; i++) battle.Tick();   // no level yet: nothing is drawn, nothing is taken
            Assert.IsFalse(battle.AwaitingChoice);
            battle.Progression.AddXp(0);
            battle.Tick();
            Assert.IsFalse(battle.AwaitingChoice);
            // The XP comes in pieces over several ticks; the choice opens on the first tick after the first level.
            battle.Progression.AddXp(1);
            Assert.IsFalse(battle.AwaitingChoice, "a level does not open a choice until the tick ends");
            battle.Tick();
            CollectionAssert.AreEqual(new[] { 2, 1, 3 }, Offer(battle.Progression));
        }

        [Test]
        public void S5b_AC3_A_Number_Is_Taken_Only_When_A_Choice_Opens_So_The_Same_Draws_Follow_A_Pick_Even_After_Many_Quiet_Ticks()
        {
            Battle a = BattleOf(5, 2, 3);
            Battle b = BattleOf(5, 2, 3);
            for (int i = 0; i < 77; i++) b.Tick();
            a.Progression.AddXp(1);
            a.Tick();
            b.Progression.AddXp(1);
            b.Tick();
            CollectionAssert.AreEqual(Offer(a.Progression), Offer(b.Progression));
            a.PickPerk(0);
            b.PickPerk(0);
            for (int i = 0; i < 30; i++) b.Tick();
            a.Progression.AddXp(1);
            b.Progression.AddXp(1);
            a.Tick();
            b.Tick();
            CollectionAssert.AreEqual(new[] { 2, 3, 4 }, Offer(a.Progression), "the second draw of seed 2");
            CollectionAssert.AreEqual(Offer(a.Progression), Offer(b.Progression));
        }

        [Test]
        public void S5b_AC3_Same_Seed_And_Same_Picks_Give_The_Same_Offers_And_A_Pick_Of_Another_Slot_Changes_Only_What_Follows_The_Perks_Not_The_Draws()
        {
            List<int[]> first = Offers(BattleOf(6, 777, 5), 5);
            List<int[]> second = Offers(BattleOf(6, 777, 5), 5);
            for (int i = 0; i < 5; i++) CollectionAssert.AreEqual(first[i], second[i], $"choice {i + 1}");

            // The draws do not depend on which slot was picked: every draw takes three numbers.
            Battle other = BattleOf(6, 777, 5);
            other.Progression.AddXp(5);
            other.Tick();
            for (int i = 0; i < 5; i++)
            {
                CollectionAssert.AreEqual(first[i], Offer(other.Progression), $"choice {i + 1} with slot 2 picked");
                other.PickPerk(2);
            }
        }

        [Test]
        public void S5b_AC3_Two_Seeds_Offer_Differently()
        {
            CollectionAssert.AreNotEqual(Offers(BattleOf(5, 1, 1), 1)[0], Offers(BattleOf(5, 2, 1), 1)[0]);
        }

        [Test]
        public void S5b_AC3_The_Three_Perks_Of_An_Offer_Are_Distinct_And_Inside_The_Pool_For_10000_Draws_Over_Pools_Of_3_To_6()
        {
            for (int perks = 3; perks <= 6; perks++)
            {
                Battle battle = BattleOf(perks, (uint)(1000 + perks), 10000);
                var seenPerks = new HashSet<int>();
                battle.Progression.AddXp(10000);
                battle.Tick();
                for (int draw = 0; draw < 10000; draw++)
                {
                    int a = battle.Progression.OfferAt(0), b = battle.Progression.OfferAt(1), c = battle.Progression.OfferAt(2);
                    Assert.That(a, Is.InRange(0, perks - 1), $"pool {perks}, draw {draw}");
                    Assert.That(b, Is.InRange(0, perks - 1));
                    Assert.That(c, Is.InRange(0, perks - 1));
                    Assert.IsTrue(a != b && a != c && b != c, $"pool {perks}, draw {draw}: ({a}, {b}, {c}) are not distinct");
                    seenPerks.Add(a); seenPerks.Add(b); seenPerks.Add(c);
                    battle.PickPerk(0);
                }
                Assert.AreEqual(perks, seenPerks.Count, $"pool {perks}: every perk is offered somewhere");
            }
        }

        [Test]
        public void S5b_AC3_A_Pool_Of_Exactly_3_Always_Offers_All_Three_In_Some_Order()
        {
            Battle battle = BattleOf(3, 4242, 200);
            battle.Progression.AddXp(200);
            battle.Tick();
            for (int i = 0; i < 200; i++)
            {
                int[] offer = Offer(battle.Progression);
                Array.Sort(offer);
                CollectionAssert.AreEqual(new[] { 0, 1, 2 }, offer);
                battle.PickPerk(1);
            }
        }

        [Test]
        public void S5b_AC3_The_Pause_Pins_Seed_2_Pool_Of_3_Draws_2_1_0_Then_1_2_0()
        {
            List<int[]> offers = Offers(BattleOf(3, 2, 2), 2);
            CollectionAssert.AreEqual(new[] { 2, 1, 0 }, offers[0]);
            CollectionAssert.AreEqual(new[] { 1, 2, 0 }, offers[1]);
        }

        // ---- The pool

        [Test]
        public void S5b_AC3_A_Pool_Of_No_Perk_Never_Opens_A_Choice_Whatever_The_XP()
        {
            foreach (Modifier[] none in new Modifier[][] { null, new Modifier[0] })
            {
                var battle = new Battle(OpenBoard(), LongDelay, 10, 0, 100, null, new Progression(new[] { 1, 1, 1 }, none, 5));
                battle.Progression.AddXp(3);
                for (int i = 0; i < 5; i++) battle.Tick();
                Assert.IsFalse(battle.AwaitingChoice);
                Assert.AreEqual(0, battle.Progression.ChoicesOpened);
                Assert.AreEqual(4, battle.Progression.Level, "the level still rises");
                Assert.AreEqual(5, battle.TickCount, "and the battle never stood still");
                Assert.AreEqual(0, battle.Progression.PerkCount);
            }
        }

        [Test]
        public void S5b_AC3_A_Pool_Of_One_Or_Two_Is_Refused_And_Three_Is_Accepted_And_Copied_With_Places_As_Sources()
        {
            foreach (int count in new[] { 1, 2 })
            {
                var ex = Assert.Throws<ArgumentException>(() => new Progression(new[] { 1 }, Pool(count), 1), $"pool of {count}");
                StringAssert.Contains("pool", ex.Message);
                StringAssert.Contains(Progression.OfferSize.ToString(), ex.Message);
            }

            Modifier[] given = Pool(3);
            var progression = new Progression(new[] { 1 }, given, 1);
            Assert.AreEqual(3, progression.PerkCount);
            for (int i = 0; i < 3; i++) Assert.AreEqual(i, progression.PerkAt(i).Source, $"perk {i}: its place is its source");
            Assert.AreEqual(-1, given[0].Source, "the pool given is copied, not changed");
        }

        [Test]
        public void S5b_AC3_A_Progression_Makes_Its_Own_Generator_So_Two_Of_Them_Never_Share_A_Stream()
        {
            var a = new Progression(new[] { 1 }, Pool(5), 1);
            var b = new Progression(new[] { 1 }, Pool(5), 1);
            Battle battleA = new Battle(OpenBoard(), LongDelay, 1, 0, 100, null, a);
            Battle battleB = new Battle(OpenBoard(), LongDelay, 1, 0, 100, null, b);
            a.AddXp(1);
            battleA.Tick();
            battleA.PickPerk(0);
            b.AddXp(1);
            battleB.Tick();
            CollectionAssert.AreEqual(new[] { 2, 1, 3 }, Offer(b), "b's first draw is the pin's first draw although a drew before it");
        }
    }
}
