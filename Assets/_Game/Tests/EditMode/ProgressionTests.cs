using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ElementalTD.Core.Tests
{
    /// <summary>XP and the level on <see cref="Progression"/> alone, with no board (5a-AC2, and the Core side of 5a-AC9).</summary>
    public class ProgressionTests
    {
        private static readonly int[] PrototypeTable = { 30, 40, 70, 90, 110, 130, 150, 170, 190, 210 };

        private static void AssertState(Progression p, int xp, int level, int into, int item, bool ended, string when)
        {
            Assert.AreEqual((xp, level, into, item, ended), (p.Xp, p.Level, p.XpIntoLevel, p.XpForLevel, p.TableEnded), when);
        }

        [Test]
        public void S5a_AC2_A_New_Progression_Is_Level_1_With_No_XP_And_The_First_Item()
        {
            AssertState(new Progression(new[] { 30, 40 }), 0, 1, 0, 30, false, "start");
        }

        // The table 30, 40 of the plan: item 1 is the XP from level 1 to 2.
        [TestCase(0, 1, 0, 30, false)]
        [TestCase(1, 1, 1, 30, false)]
        [TestCase(29, 1, 29, 30, false)]
        [TestCase(30, 2, 0, 40, false)]
        [TestCase(31, 2, 1, 40, false)]
        [TestCase(35, 2, 5, 40, false)]
        [TestCase(69, 2, 39, 40, false)]
        [TestCase(70, 3, 0, 0, true)]
        public void S5a_AC2_One_Gain_Lands_On_The_Level_The_Table_Says(int gain, int level, int into, int item, bool ended)
        {
            var p = new Progression(new[] { 30, 40 });
            p.AddXp(gain);
            AssertState(p, gain, level, into, item, ended, $"one gain of {gain}");
        }

        [Test]
        public void S5a_AC2_The_Same_XP_Gained_One_At_A_Time_Gives_The_Same_State_As_One_Gain()
        {
            for (int total = 0; total <= 80; total++)
            {
                var one = new Progression(new[] { 30, 40 });
                one.AddXp(total);
                var steps = new Progression(new[] { 30, 40 });
                for (int i = 0; i < total; i++) steps.AddXp(1);
                Assert.AreEqual((one.Xp, one.Level, one.TableEnded, one.XpForLevel), (steps.Xp, steps.Level, steps.TableEnded, steps.XpForLevel), $"total {total}");
                if (!one.TableEnded) Assert.AreEqual(one.XpIntoLevel, steps.XpIntoLevel, $"total {total}");
            }
        }

        [Test]
        public void S5a_AC2_One_Gain_Of_100_From_0_Crosses_Two_Levels_And_Ends_The_Table_At_Level_3_With_XP_100()
        {
            var p = new Progression(new[] { 30, 40 });
            p.AddXp(100);
            Assert.AreEqual((100, 3, true, 0), (p.Xp, p.Level, p.TableEnded, p.XpForLevel));
        }

        [Test]
        public void S5a_AC2_One_Gain_May_Cross_Several_Levels_And_The_Rest_Carries_Into_The_Last()
        {
            var p = new Progression(new[] { 10, 10, 10, 50 });
            p.AddXp(25);
            AssertState(p, 25, 3, 5, 10, false, "25 over 10, 10, 10");
            p.AddXp(27); // 5 + 27 = 32: the third item's 10 is spent and 22 carries into level 4
            AssertState(p, 52, 4, 22, 50, false, "level 4 with the carry");
        }

        [Test]
        public void S5a_AC2_A_Gain_That_Lands_Exactly_On_Several_Items_Leaves_Nothing_Over()
        {
            var p = new Progression(new[] { 10, 20 });
            p.AddXp(30);
            Assert.AreEqual((3, true), (p.Level, p.TableEnded));
            var q = new Progression(new[] { 10, 20, 5 });
            q.AddXp(30);
            AssertState(q, 30, 3, 0, 5, false, "30 on the first two items of three");
        }

        [Test]
        public void S5a_AC2_The_Level_Rises_On_The_Gain_That_Reaches_The_Item_Not_One_Gain_Later()
        {
            var p = new Progression(new[] { 30 });
            p.AddXp(29);
            Assert.AreEqual(1, p.Level);
            p.AddXp(1);
            Assert.AreEqual(2, p.Level, "a gain that makes 30 of 30 is the level");
        }

        [Test]
        public void S5a_AC2_Once_The_Table_Has_Ended_No_Level_Is_Gained_And_XP_Still_Counts()
        {
            var p = new Progression(new[] { 30, 40 });
            p.AddXp(70);
            Assert.IsTrue(p.TableEnded);
            for (int i = 1; i <= 5; i++)
            {
                p.AddXp(1000);
                Assert.AreEqual((70 + 1000 * i, 3, true, 0), (p.Xp, p.Level, p.TableEnded, p.XpForLevel), $"gain {i}");
            }
        }

        [Test]
        public void S5a_AC2_A_Table_Of_One_Item_Of_1_Makes_Every_XP_A_Level_Until_It_Ends()
        {
            var p = new Progression(new[] { 1 });
            p.AddXp(1);
            Assert.AreEqual((2, true), (p.Level, p.TableEnded));
        }

        [Test]
        public void S5a_AC2_Zero_XP_Changes_Nothing()
        {
            var p = new Progression(new[] { 30, 40 });
            p.AddXp(35);
            p.AddXp(0);
            AssertState(p, 35, 2, 5, 40, false, "after a gain of 0");
        }

        [Test]
        public void S5a_AC2_A_Gain_Of_The_Largest_Int_Is_Counted_And_Does_Not_Throw()
        {
            var p = new Progression(PrototypeTable);
            Assert.DoesNotThrow(() => p.AddXp(int.MaxValue));
            Assert.AreEqual((int.MaxValue, 11, true), (p.Xp, p.Level, p.TableEnded));
        }

        [TestCase(null)]
        [TestCase(new int[0])]
        public void S5a_AC2_A_Null_Or_Empty_Table_Never_Gains_A_Level_And_Is_Already_Ended_But_Counts_XP(int[] table)
        {
            var p = new Progression(table);
            AssertState(p, 0, 1, 0, 0, true, "start");
            p.AddXp(5000);
            Assert.AreEqual((5000, 1, true, 0), (p.Xp, p.Level, p.TableEnded, p.XpForLevel));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(int.MinValue)]
        public void S5a_AC2_An_Item_Below_1_Is_Refused_Wherever_It_Stands_And_Is_Named(int bad)
        {
            var first = Assert.Throws<ArgumentOutOfRangeException>(() => new Progression(new[] { bad, 10 }));
            StringAssert.Contains("Item 1", first.Message);
            var last = Assert.Throws<ArgumentOutOfRangeException>(() => new Progression(new[] { 30, 40, bad }));
            StringAssert.Contains("Item 3", last.Message);
        }

        [Test]
        public void S5a_AC2_A_Negative_Amount_Is_Refused_And_Changes_Nothing()
        {
            var p = new Progression(new[] { 30, 40 });
            p.AddXp(35);
            Assert.Throws<ArgumentOutOfRangeException>(() => p.AddXp(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => p.AddXp(int.MinValue));
            AssertState(p, 35, 2, 5, 40, false, "after the refusals");
        }

        [Test]
        public void S5a_AC2_The_Table_Is_Copied_Both_Ways()
        {
            var table = new[] { 30, 40 };
            var p = new Progression(table);
            table[0] = 1;
            table[1] = 1;
            p.AddXp(29);
            Assert.AreEqual(1, p.Level, "changing the caller's array afterwards changes nothing");
            Assert.AreEqual(30, p.XpForLevel);
            Assert.AreEqual(new[] { 1, 1 }, table, "the progression does not write into the caller's array");
            p.AddXp(100);
            Assert.AreEqual(new[] { 1, 1 }, table);
        }

        [Test]
        public void S5a_AC2_The_Prototype_Table_Gives_Level_N_At_The_Running_Total_Of_Its_Items_And_The_Share_Is_The_Levels_Own()
        {
            // An independent model: the level is 1 plus the items whose running total the XP has reached.
            for (int xp = 0; xp <= 1250; xp++)
            {
                int level = 1, start = 0;
                while (level <= PrototypeTable.Length && xp >= start + PrototypeTable[level - 1]) start += PrototypeTable[level++ - 1];
                var p = new Progression(PrototypeTable);
                p.AddXp(xp);
                bool ended = level > PrototypeTable.Length;
                Assert.AreEqual((level, ended), (p.Level, p.TableEnded), $"xp {xp}");
                if (ended) continue;
                Assert.AreEqual((xp - start, PrototypeTable[level - 1]), (p.XpIntoLevel, p.XpForLevel), $"xp {xp}");
                Assert.Less(p.XpIntoLevel, p.XpForLevel, $"xp {xp}: the share is below 1 while a level remains");
            }
        }

        [Test]
        public void S5a_AC2_The_Prototype_Table_Ends_At_1190_XP_And_Level_11()
        {
            var p = new Progression(PrototypeTable);
            p.AddXp(1189);
            Assert.AreEqual((10, false), (p.Level, p.TableEnded));
            p.AddXp(1);
            Assert.AreEqual((11, true), (p.Level, p.TableEnded));
        }

        // ---- 5a-AC9 cost and layer

        [Test]
        public void S5a_AC9_AddXp_Allocates_Nothing_Also_When_It_Gains_Several_Levels_And_After_The_End()
        {
            var p = new Progression(PrototypeTable);
            p.AddXp(1); // warm-up of the code path
            Assert.AreEqual(0, TickAllocation.AllocationsOf(() => p.AddXp(10)), "no level");
            Assert.AreEqual(0, TickAllocation.AllocationsOf(() => p.AddXp(30)), "one level");
            Assert.AreEqual(0, TickAllocation.AllocationsOf(() => p.AddXp(400)), "several levels");
            Assert.AreEqual(0, TickAllocation.AllocationsOf(() => p.AddXp(5000)), "the end");
            Assert.AreEqual(0, TickAllocation.AllocationsOf(() => p.AddXp(5)), "past the end");
            Assert.AreEqual(0, TickAllocation.AllocationsOf(() => { _ = p.Xp + p.Level + p.XpIntoLevel + p.XpForLevel; _ = p.TableEnded; }), "the readers");
        }

        [Test]
        public void S5a_AC9_Progression_Is_In_The_Core_Assembly_And_Its_Source_Has_No_UnityEngine_And_AddXp_Has_No_New_Outside_Its_Refusal()
        {
            Assert.AreEqual(typeof(Battle).Assembly, typeof(Progression).Assembly);
            Assert.AreEqual("ElementalTD.Core", typeof(Progression).Namespace);
            string text = File.ReadAllText(Path.Combine(Application.dataPath, "_Game", "Core", "Progression.cs"));
            StringAssert.DoesNotContain("UnityEngine", text);

            int start = text.IndexOf("public void AddXp(", StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            int open = text.IndexOf('{', start), depth = 0, end = open;
            for (; end < text.Length; end++)
            {
                if (text[end] == '{') depth++;
                else if (text[end] == '}' && --depth == 0) break;
            }
            string body = text.Substring(open, end - open);
            foreach (string line in body.Split('\n'))
                if (Regex.IsMatch(line, @"\bnew\b")) StringAssert.Contains("throw new", line, "a new that is not the refusal");
        }
    }
}
