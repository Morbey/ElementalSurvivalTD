using System;
using ElementalTD.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ElementalTD.Core.Tests
{
    /// <summary>Step 5b, 5b-AC8's code part: PerkDefinition and RunDefinition built in the test, never the shipped assets.</summary>
    public class PerkDataTests
    {
        private static ElementDefinition Element(string name, int id)
        {
            var element = ScriptableObject.CreateInstance<ElementDefinition>();
            element.name = name;
            var so = new SerializedObject(element);
            so.FindProperty("id").intValue = id;
            so.FindProperty("displayName").stringValue = name;
            so.ApplyModifiedPropertiesWithoutUndo();
            return element;
        }

        private static PerkDefinition Perk(string name, Stat stat, Operation operation, float value, ElementDefinition element = null,
            string title = "Title", string text = "Text")
        {
            var perk = ScriptableObject.CreateInstance<PerkDefinition>();
            perk.name = name;
            var so = new SerializedObject(perk);
            so.FindProperty("stat").enumValueIndex = (int)stat;
            so.FindProperty("operation").enumValueIndex = (int)operation;
            so.FindProperty("value").floatValue = value;
            so.FindProperty("element").objectReferenceValue = element;
            so.FindProperty("title").stringValue = title;
            so.FindProperty("attributeText").stringValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
            return perk;
        }

        private static RunDefinition Run(string name, params PerkDefinition[] perks)
        {
            var run = ScriptableObject.CreateInstance<RunDefinition>();
            run.name = name;
            var so = new SerializedObject(run);
            SerializedProperty array = so.FindProperty("perks");
            array.arraySize = perks.Length;
            for (int i = 0; i < perks.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = perks[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return run;
        }

        private static PerkDefinition[] ThreeGood() => new[]
        {
            Perk("Perk_A", Stat.TowerDamage, Operation.Multiply, 1.25f),
            Perk("Perk_B", Stat.TowerRange, Operation.Add, 0.5f),
            Perk("Perk_C", Stat.GoldPerKill, Operation.Add, 2f),
        };

        // ---- PerkDefinition

        [Test]
        public void S5b_AC8_A_Perk_Holds_One_Modifier_Its_Title_And_Its_Attribute_Text()
        {
            ElementDefinition light = Element("Light", 1);
            PerkDefinition perk = Perk("Perk_LightDamage", Stat.TowerDamage, Operation.Multiply, 1.25f, light, "{0} towers", "damage");
            Modifier modifier = perk.ToModifier();
            Assert.AreEqual((Stat.TowerDamage, Operation.Multiply, 1.25f, new Element(1)), (modifier.Stat, modifier.Operation, modifier.Value, modifier.Element));
            Assert.AreEqual(-1, modifier.Source, "the place is given by the pool, not the asset");
            Assert.AreEqual(("{0} towers", "damage"), (perk.Title, perk.AttributeText));
            Assert.AreSame(light, perk.Element);

            Assert.IsTrue(Perk("P", Stat.TowerShotsPerSecond, Operation.Multiply, 1.15f).ToModifier().Element.IsNone, "an unassigned element is none");
            Assert.IsNull(Perk("P", Stat.TowerRange, Operation.Add, 1f).Element);
            Assert.AreEqual(Stat.GoldPerKill, Perk("P", Stat.GoldPerKill, Operation.Add, 2f).ToModifier().Stat);
        }

        [Test]
        public void S5b_AC8_An_Addition_At_Or_Below_0_Is_Refused_Naming_The_Asset()
        {
            foreach (float value in new[] { 0f, -0.5f, -3f })
            {
                var ex = Assert.Throws<InvalidOperationException>(() => Perk("Perk_Bad", Stat.TowerRange, Operation.Add, value).ToModifier());
                StringAssert.StartsWith("Perk_Bad", ex.Message);
                StringAssert.Contains("addition", ex.Message);
            }
            Assert.DoesNotThrow(() => Perk("Perk_Ok", Stat.TowerRange, Operation.Add, 0.001f).ToModifier());
        }

        [Test]
        public void S5b_AC8_A_Multiplication_At_Or_Below_1_Is_Refused_Naming_The_Asset()
        {
            foreach (float value in new[] { 1f, 0.9f, 0f, -2f })
            {
                var ex = Assert.Throws<InvalidOperationException>(() => Perk("Perk_Bad", Stat.TowerDamage, Operation.Multiply, value).ToModifier());
                StringAssert.StartsWith("Perk_Bad", ex.Message);
                StringAssert.Contains("multiplication", ex.Message);
            }
            Assert.DoesNotThrow(() => Perk("Perk_Ok", Stat.TowerDamage, Operation.Multiply, 1.001f).ToModifier());
        }

        [Test]
        public void S5b_AC8_What_Core_Refuses_Is_Refused_Naming_The_Asset_Gold_Times_2_Gold_Plus_A_Fraction_An_Element_On_Gold()
        {
            ElementDefinition light = Element("Light", 1);
            var multiply = Assert.Throws<InvalidOperationException>(() => Perk("Perk_GoldX2", Stat.GoldPerKill, Operation.Multiply, 2f).ToModifier());
            StringAssert.StartsWith("Perk_GoldX2", multiply.Message);
            StringAssert.Contains("GoldPerKill", multiply.Message);

            var fraction = Assert.Throws<InvalidOperationException>(() => Perk("Perk_GoldHalf", Stat.GoldPerKill, Operation.Add, 1.5f).ToModifier());
            StringAssert.StartsWith("Perk_GoldHalf", fraction.Message);
            StringAssert.Contains("whole", fraction.Message);

            var element = Assert.Throws<InvalidOperationException>(() => Perk("Perk_GoldLight", Stat.GoldPerKill, Operation.Add, 2f, light).ToModifier());
            StringAssert.StartsWith("Perk_GoldLight", element.Message);
            StringAssert.Contains("tower stat", element.Message);
        }

        [Test]
        public void S5b_AC8_An_Element_Asset_That_Cannot_Stand_For_An_Element_Is_Refused_Through_The_Perk()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                Perk("Perk_Bad", Stat.TowerDamage, Operation.Multiply, 1.25f, Element("Broken", 0)).ToModifier());
            StringAssert.Contains("Broken", ex.Message);
        }

        // ---- RunDefinition

        [Test]
        public void S5b_AC8_An_Empty_Pool_Is_Allowed_And_Gives_No_Modifiers()
        {
            Assert.AreEqual(0, Run("Run_Empty").ToPerks().Length);
            var progression = new Progression(new[] { 1 }, Run("Run_Empty").ToPerks(), 1);
            Assert.AreEqual(0, progression.PerkCount);
        }

        [Test]
        public void S5b_AC8_A_Pool_Of_One_Or_Two_Is_Refused_Naming_The_Run()
        {
            PerkDefinition[] three = ThreeGood();
            for (int count = 1; count <= 2; count++)
            {
                var pool = new PerkDefinition[count];
                Array.Copy(three, pool, count);
                var ex = Assert.Throws<InvalidOperationException>(() => Run("Run_Short", pool).ToPerks(), $"{count} perk(s)");
                StringAssert.StartsWith("Run_Short", ex.Message);
                StringAssert.Contains("pool", ex.Message);
            }
        }

        [Test]
        public void S5b_AC8_A_Pool_Of_Three_Is_Accepted_In_Order()
        {
            PerkDefinition[] perks = ThreeGood();
            Modifier[] pool = Run("Run_Ok", perks).ToPerks();
            Assert.AreEqual(3, pool.Length);
            Assert.AreEqual((Stat.TowerDamage, Stat.TowerRange, Stat.GoldPerKill), (pool[0].Stat, pool[1].Stat, pool[2].Stat), "the pool's order is the list's");
            Modifier[] other = Run("Run_Ok", perks[2], perks[0], perks[1]).ToPerks();
            Assert.AreEqual((Stat.GoldPerKill, Stat.TowerDamage, Stat.TowerRange), (other[0].Stat, other[1].Stat, other[2].Stat));
            Assert.AreSame(perks[1], Run("Run_Ok", perks).PerkAt(1));
        }

        [Test]
        public void S5b_AC8_An_Empty_Place_Is_Refused_Naming_The_Run_And_The_Place()
        {
            PerkDefinition[] perks = ThreeGood();
            var ex = Assert.Throws<InvalidOperationException>(() => Run("Run_Hole", perks[0], null, perks[2]).ToPerks());
            StringAssert.StartsWith("Run_Hole", ex.Message);
            StringAssert.Contains("empty", ex.Message);
            StringAssert.Contains("2", ex.Message.Substring(ex.Message.IndexOf("perk", StringComparison.Ordinal)));
        }

        [Test]
        public void S5b_AC8_A_Perk_Listed_Twice_Is_Refused_Naming_The_Run_And_The_Asset()
        {
            PerkDefinition[] perks = ThreeGood();
            var ex = Assert.Throws<InvalidOperationException>(() => Run("Run_Twice", perks[0], perks[1], perks[0]).ToPerks());
            StringAssert.StartsWith("Run_Twice", ex.Message);
            StringAssert.Contains("Perk_A", ex.Message);
            StringAssert.Contains("twice", ex.Message);
        }

        [Test]
        public void S5b_AC8_A_Perk_Its_Own_Asset_Refuses_Is_Refused_By_The_Run_Naming_The_Run_The_Place_And_The_Asset()
        {
            PerkDefinition[] perks = ThreeGood();
            PerkDefinition bad = Perk("Perk_Bad", Stat.TowerDamage, Operation.Multiply, 1f);
            var ex = Assert.Throws<InvalidOperationException>(() => Run("Run_Bad", perks[0], perks[1], bad).ToPerks());
            StringAssert.StartsWith("Run_Bad", ex.Message);
            StringAssert.Contains("Perk_Bad", ex.Message);
            StringAssert.Contains("multiplication", ex.Message);
        }

        [Test]
        public void S5b_AC8_The_Pool_From_A_Run_Reaches_A_Progression_With_Places_As_Sources_And_Opens_A_Choice()
        {
            Modifier[] pool = Run("Run_Ok", ThreeGood()).ToPerks();
            var battle = new Battle(new Board(new GridMap(16, 9, new GridPos[0]), new GridPos(0, 4), new GridPos(15, 4)),
                new[] { new Wave(new EnemyStats(1f, 10f), 1, 1f, 1_000_000f) }, 10, 0, 100, null, new Progression(new[] { 1 }, pool, 2));
            battle.Progression.AddXp(1);
            battle.Tick();
            Assert.IsTrue(battle.AwaitingChoice);
            Assert.AreEqual(2, battle.Progression.PerkAt(2).Source);
        }

    }
}
