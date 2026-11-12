using System;
using NUnit.Framework;

namespace ElementalTD.Core.Tests
{
    /// <summary>The Element value and the Core chart: 4b-AC1, AC3 (ADR 0005, decisions 1, 3, 4 and 7).</summary>
    public class ElementChartTests
    {
        private static readonly Element Light = new Element(1);
        private static readonly Element Shadow = new Element(2);
        private static readonly Element Third = new Element(3);

        // ---- 4b-AC1 the value

        [Test]
        public void S4b_AC1_The_Default_Element_Is_None_And_An_Id_From_One_Is_An_Element()
        {
            Assert.IsTrue(default(Element).IsNone);
            Assert.AreEqual(0, default(Element).Id);
            Assert.IsTrue(new Element(0).IsNone);
            Assert.IsFalse(Light.IsNone);
            Assert.IsFalse(new Element(int.MaxValue).IsNone);
        }

        [Test]
        public void S4b_AC1_Elements_Compare_By_Id()
        {
            Assert.IsTrue(new Element(2) == Shadow);
            Assert.IsTrue(Light != Shadow);
            Assert.IsTrue(Light.Equals(new Element(1)));
            Assert.IsTrue(Light.Equals((object)new Element(1)));
            Assert.IsFalse(Light.Equals((object)1));
            Assert.IsFalse(Light.Equals(null));
            Assert.AreEqual(Light.GetHashCode(), new Element(1).GetHashCode());
            Assert.IsTrue(default(Element) == new Element(0));
            Assert.IsTrue(default(Element) != Light);
        }

        [Test]
        public void S4b_AC1_A_Negative_Id_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Element(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Element(int.MinValue));
        }

        [Test]
        public void S4b_AC1_Stats_Hold_The_Element_And_Default_To_None()
        {
            Assert.AreEqual(Shadow, new TowerStats(1f, 1f, 1f, 1f, 0, Shadow).Element);
            Assert.AreEqual(Shadow, new EnemyStats(1f, 1f, 0, Shadow).Element);
            Assert.IsTrue(new TowerStats(1f, 1f, 1f, 1f).Element.IsNone);
            Assert.IsTrue(new TowerStats(1f, 1f, 1f, 1f, 5).Element.IsNone);
            Assert.IsTrue(new EnemyStats(1f, 1f).Element.IsNone);
            Assert.IsTrue(new EnemyStats(1f, 1f, 4).Element.IsNone);
        }

        // ---- 4b-AC1 the chart: x1 unless set

        [Test]
        public void S4b_AC1_Every_Pair_Starts_At_One_And_Only_The_Same_Element_Differs()
        {
            var chart = new ElementChart(3);
            for (int a = 1; a <= 3; a++)
                for (int d = 1; d <= 3; d++)
                    Assert.AreEqual(a == d ? 0f : 1f, chart.Multiplier(new Element(a), new Element(d)), $"{a} against {d}");
        }

        [Test]
        public void S4b_AC1_Setting_Attacker_Against_Defender_Leaves_The_Reverse_Pair_At_One()
        {
            var chart = new ElementChart(3);
            chart.Set(Light, Shadow, 2f);

            Assert.AreEqual(2f, chart.Multiplier(Light, Shadow));
            Assert.AreEqual(1f, chart.Multiplier(Shadow, Light));
            for (int a = 1; a <= 3; a++)
                for (int d = 1; d <= 3; d++)
                {
                    if (a == d || (a == 1 && d == 2)) continue;
                    Assert.AreEqual(1f, chart.Multiplier(new Element(a), new Element(d)), $"{a} against {d}");
                }
        }

        [TestCase(0.25f)]
        [TestCase(0.75f)]
        [TestCase(1f)]
        [TestCase(2f)]
        [TestCase(1000f)]
        [TestCase(float.Epsilon)]
        [TestCase(float.MaxValue)]
        public void S4b_AC1_The_Value_Set_Is_The_Value_Answered_For_That_Ordered_Pair(float value)
        {
            var chart = new ElementChart(3);
            chart.Set(Shadow, Third, value);

            Assert.AreEqual(value, chart.Multiplier(Shadow, Third));
            Assert.AreEqual(1f, chart.Multiplier(Third, Shadow));
        }

        [Test]
        public void S4b_AC1_Setting_A_Pair_Again_Replaces_The_Value()
        {
            var chart = new ElementChart(2);
            chart.Set(Light, Shadow, 2f);
            chart.Set(Light, Shadow, 0.25f);

            Assert.AreEqual(0.25f, chart.Multiplier(Light, Shadow));
        }

        [Test]
        public void S4b_AC1_Both_Orders_Can_Be_Set_To_Different_Values()
        {
            var chart = new ElementChart(2);
            chart.Set(Light, Shadow, 2f);
            chart.Set(Shadow, Light, 0.25f);

            Assert.AreEqual(2f, chart.Multiplier(Light, Shadow));
            Assert.AreEqual(0.25f, chart.Multiplier(Shadow, Light));
        }

        // ---- 4b-AC3 what Set refuses

        [Test]
        public void S4b_AC3_Set_Refuses_The_Same_Element_On_Both_Sides()
        {
            var chart = new ElementChart(2);
            Assert.Throws<ArgumentException>(() => chart.Set(Light, Light, 2f));
            Assert.Throws<ArgumentException>(() => chart.Set(Shadow, new Element(2), 0.5f));
            Assert.AreEqual(0f, chart.Multiplier(Light, Light), "still immune");
        }

        [TestCase(0f)]
        [TestCase(-0f)]
        [TestCase(-1f)]
        [TestCase(-0.0001f)]
        [TestCase(float.MinValue)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(float.NaN)]
        public void S4b_AC3_Set_Refuses_A_Multiplier_At_Or_Below_Zero_Or_Not_A_Number(float value)
        {
            var chart = new ElementChart(2);
            Assert.Throws<ArgumentOutOfRangeException>(() => chart.Set(Light, Shadow, value));
            Assert.AreEqual(1f, chart.Multiplier(Light, Shadow), "the refusal left the pair at x1");
        }

        [Test]
        public void S4b_AC3_A_Refused_Set_Leaves_An_Earlier_Value_In_Place()
        {
            var chart = new ElementChart(2);
            chart.Set(Light, Shadow, 2f);
            Assert.Throws<ArgumentOutOfRangeException>(() => chart.Set(Light, Shadow, 0f));
            Assert.AreEqual(2f, chart.Multiplier(Light, Shadow));
        }

        [Test]
        public void S4b_AC3_Set_Refuses_No_Element_And_An_Id_Outside_The_Chart()
        {
            var chart = new ElementChart(2);
            Assert.Throws<ArgumentException>(() => chart.Set(default, Light, 2f), "no element as attacker");
            Assert.Throws<ArgumentException>(() => chart.Set(Light, default, 2f), "no element as defender");
            Assert.Throws<ArgumentException>(() => chart.Set(default, default, 2f), "none against none");
            Assert.Throws<ArgumentOutOfRangeException>(() => chart.Set(Third, Light, 2f), "attacker above the chart");
            Assert.Throws<ArgumentOutOfRangeException>(() => chart.Set(Light, Third, 2f), "defender above the chart");
            Assert.Throws<ArgumentOutOfRangeException>(() => chart.Set(new Element(int.MaxValue), Light, 2f));
            Assert.AreEqual(1f, chart.Multiplier(Light, Shadow));
            Assert.AreEqual(1f, chart.Multiplier(Shadow, Light));
        }

        [Test]
        public void S4b_AC3_An_Empty_Chart_Refuses_Every_Set_And_A_Negative_Size_Throws()
        {
            var empty = new ElementChart(0);
            Assert.Throws<ArgumentOutOfRangeException>(() => empty.Set(Light, Shadow, 2f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ElementChart(-1));
        }

        // ---- 4b-AC3 immunity is a rule, not a row

        [Test]
        public void S4b_AC3_The_Same_Element_Is_Immune_In_An_Empty_Chart()
        {
            var empty = new ElementChart(0);
            Assert.AreEqual(0f, empty.Multiplier(Light, Light));
            Assert.AreEqual(0f, empty.Multiplier(Shadow, new Element(2)));
            Assert.AreEqual(1f, empty.Multiplier(Light, Shadow), "different elements deal x1 in an empty chart");
        }

        [Test]
        public void S4b_AC3_The_Same_Element_Is_Immune_For_An_Id_Above_The_Chart()
        {
            var chart = new ElementChart(2);
            Assert.AreEqual(0f, chart.Multiplier(new Element(3), new Element(3)));
            Assert.AreEqual(0f, chart.Multiplier(new Element(int.MaxValue), new Element(int.MaxValue)));
        }

        [Test]
        public void S4b_AC3_The_Same_Element_Stays_Immune_Whatever_Else_Was_Set()
        {
            var chart = new ElementChart(3);
            for (int a = 1; a <= 3; a++)
                for (int d = 1; d <= 3; d++)
                    if (a != d) chart.Set(new Element(a), new Element(d), 5f);

            for (int e = 1; e <= 3; e++)
                Assert.AreEqual(0f, chart.Multiplier(new Element(e), new Element(e)), $"element {e}");
        }

        [Test]
        public void S4b_AC3_No_Element_Deals_And_Takes_One_In_Either_Order()
        {
            var chart = new ElementChart(2);
            chart.Set(Light, Shadow, 2f);
            chart.Set(Shadow, Light, 2f);

            Assert.AreEqual(1f, chart.Multiplier(default, default), "none against none is never immune");
            Assert.AreEqual(1f, chart.Multiplier(Light, default));
            Assert.AreEqual(1f, chart.Multiplier(default, Light));
            Assert.AreEqual(1f, chart.Multiplier(default, Shadow));
            Assert.AreEqual(1f, new ElementChart(0).Multiplier(default, default));
            Assert.AreEqual(1f, chart.Multiplier(default, new Element(99)), "none against an id above the chart");
            Assert.AreEqual(1f, chart.Multiplier(new Element(99), default));
        }

        [Test]
        public void S4b_AC1_A_Different_Element_With_An_Id_Above_The_Chart_Takes_One()
        {
            var chart = new ElementChart(2);
            chart.Set(Light, Shadow, 2f);

            Assert.AreEqual(1f, chart.Multiplier(Light, new Element(9)));
            Assert.AreEqual(1f, chart.Multiplier(new Element(9), Light));
            Assert.AreEqual(1f, chart.Multiplier(new Element(9), new Element(8)));
        }
    }
}
