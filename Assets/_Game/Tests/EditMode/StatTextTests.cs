using System.Globalization;
using ElementalTD.Presentation;
using NUnit.Framework;

namespace ElementalTD.Core.Tests
{
    /// <summary>Step 5b, 5b-AC12 and AC16: the one display rule in code, with no scene (plan 0012, reading 7).</summary>
    public class StatTextTests
    {
        private static string Text(decimal value) => value.ToString("0.#####", CultureInfo.InvariantCulture);

        [TestCase(1.7249999f, "1.73", Description = "1.5 x1.15 is held as 1.7249999 and reads 1.73")]
        [TestCase(1.125f, "1.13", Description = "half up, not half to even (1.12)")]
        [TestCase(1.996f, "2", Description = "no trailing zeros")]
        [TestCase(9.995f, "10")]
        [TestCase(1234.5f, "1230", Description = "three significant figures hold above 1000")]
        [TestCase(0.6666667f, "0.667")]
        [TestCase(12.5f, "12.5")]
        [TestCase(15.625f, "15.6")]
        [TestCase(1.65f, "1.65")]
        [TestCase(3.5f, "3.5")]
        [TestCase(0f, "0")]
        [TestCase(-2.5f, "-2.5", Description = "away from zero on the magnitude")]
        [TestCase(-1.125f, "-1.13")]
        public void S5b_AC12_Shown_Is_Three_Significant_Figures_Half_Up_On_The_Seven_The_Cast_Keeps(float value, string expected)
        {
            Assert.AreEqual(expected, Text(StatText.Shown(value)));
        }

        [TestCase(1.5625f, "56")]
        [TestCase(1.125f, "13", Description = "12.5 rounds up")]
        [TestCase(1.25f, "25")]
        [TestCase(1.1f, "10", Description = "x1.1 held as 1.1000000238 adds 10 %")]
        [TestCase(1.953125f, "95")]
        [TestCase(1.21f, "21")]
        public void S5b_AC10_Percent_Is_The_Whole_Percent_A_Multiplication_Adds_Half_Up(float multiplier, string expected)
        {
            Assert.AreEqual(expected, Text(StatText.Percent(multiplier)));
        }

        [Test]
        public void S5b_AC12_The_Tail_Is_The_Difference_Of_Two_Shown_Values_Not_Of_The_Exact_Ones()
        {
            Assert.AreEqual("0.23", Text(StatText.Difference(1.7249999f, 1.5f)), "1.73 less 1.5");
            Assert.AreEqual("5.6", Text(StatText.Difference(15.625f, 10f)), "15.6 less 10, not the 5.63 of the exact values");
            Assert.AreEqual("2.5", Text(StatText.Difference(12.5f, 10f)));
            Assert.AreEqual("0.15", Text(StatText.Difference(1.65f, 1.5f)));
            Assert.AreEqual("1", Text(StatText.Difference(3.5f, 2.5f)));
            Assert.AreEqual(0m, StatText.Difference(2.5f, 2.5f), "no difference");
            Assert.AreEqual(0m, StatText.Difference(1.5000001f, 1.5f), "a difference below the shown figures is none");
        }

        [Test]
        public void S5b_AC12_The_Fire_Rate_Stacks_Of_The_Pool_Read_1_65_1_82_And_2_With_Tails_0_15_0_32_And_0_5()
        {
            var modifiers = new Modifiers();
            var rate = new Modifier(Stat.TowerShotsPerSecond, Operation.Multiply, 1.1f);
            string[] shown = new string[3], tails = new string[3];
            for (int i = 0; i < 3; i++)
            {
                modifiers.Add(rate);
                float effective = modifiers.Apply(Stat.TowerShotsPerSecond, default, 1.5f);
                shown[i] = Text(StatText.Shown(effective));
                tails[i] = Text(StatText.Difference(effective, 1.5f));
            }
            CollectionAssert.AreEqual(new[] { "1.65", "1.82", "2" }, shown);
            CollectionAssert.AreEqual(new[] { "0.15", "0.32", "0.5" }, tails);
        }

        [Test]
        public void S5b_AC12_The_Result_Does_Not_Depend_On_The_Current_Culture()
        {
            CultureInfo before = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("pt-PT");
                Assert.AreEqual("1.73", Text(StatText.Shown(1.7249999f)));
                Assert.AreEqual("56", Text(StatText.Percent(1.5625f)));
            }
            finally { CultureInfo.CurrentCulture = before; }
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void S5b_AC16_The_Cast_To_Decimal_Throws_For_NaN_And_Infinity_So_A_Perk_Amount_Must_Be_Refused_Before_It_Gets_Here(float value)
        {
            Assert.Throws<System.OverflowException>(() => StatText.Shown(value));
        }
    }
}
