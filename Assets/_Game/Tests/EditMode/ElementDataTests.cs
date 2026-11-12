using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ElementalTD.Data;
using NUnit.Framework;
using UnityEngine;

namespace ElementalTD.Core.Tests
{
    /// <summary>
    /// The definitions that carry elements into Core, built in memory (4b-AC1, AC3, AC13).
    /// The shipped assets are checked in RealAssetElementTests once the assets exist.
    /// </summary>
    public class ElementDataTests
    {
        private readonly List<UnityEngine.Object> _made = new List<UnityEngine.Object>();

        [TearDown]
        public void DestroyMade()
        {
            foreach (UnityEngine.Object made in _made) UnityEngine.Object.DestroyImmediate(made);
            _made.Clear();
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType().Name} has no field '{name}'");
            field.SetValue(target, value);
        }

        private T Make<T>(string name, params (string field, object value)[] fields) where T : ScriptableObject
        {
            T made = ScriptableObject.CreateInstance<T>();
            made.name = name;
            _made.Add(made);
            foreach ((string field, object value) in fields) SetField(made, field, value);
            return made;
        }

        private ElementDefinition Element(string name, int id) => Make<ElementDefinition>(name, ("id", id), ("displayName", name));

        private static readonly Type RelationType = typeof(ElementChartDefinition).GetNestedType("Relation", BindingFlags.NonPublic);

        private static object Relation(ElementDefinition attacker, ElementDefinition defender, float multiplier)
        {
            object relation = Activator.CreateInstance(RelationType, true);
            SetField(relation, "attacker", attacker);
            SetField(relation, "defender", defender);
            SetField(relation, "damageMultiplier", multiplier);
            return relation;
        }

        private ElementChartDefinition Chart(ElementDefinition[] elements, params object[] relations)
        {
            Array array = Array.CreateInstance(RelationType, relations.Length);
            for (int i = 0; i < relations.Length; i++) array.SetValue(relations[i], i);
            return Make<ElementChartDefinition>("Chart", ("elements", elements), ("relations", array));
        }

        private TowerDefinition Tower(ElementDefinition element) => Make<TowerDefinition>("Tower",
            ("rangeCells", 3.5f), ("damage", 12f), ("shotsPerSecond", 1.25f), ("projectileSpeedCellsPerSecond", 7f), ("cost", 9), ("element", element));

        private EnemyDefinition Enemy(ElementDefinition element) => Make<EnemyDefinition>("Enemy",
            ("speedCellsPerSecond", 1.5f), ("maxHealth", 45f), ("goldReward", 3), ("element", element));

        // ---- ElementDefinition

        [Test]
        public void S4b_AC1_ToElement_Gives_The_Id_Written_On_The_Asset()
        {
            Assert.AreEqual(new Element(1), Element("Light", 1).ToElement());
            Assert.AreEqual(new Element(42), Element("Other", 42).ToElement());
            Assert.AreEqual("Shadow", Element("Shadow", 2).DisplayName);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void S4b_AC1_ToElement_Refuses_An_Id_Below_One_So_An_Asset_Never_Means_No_Element(int id)
        {
            ElementDefinition definition = Element("Broken", id);
            Assert.Throws<InvalidOperationException>(() => definition.ToElement());
        }

        // ---- ElementChartDefinition.ToChart

        [Test]
        public void S4b_AC1_ToChart_Holds_The_Rows_Of_The_Asset_And_Nothing_Else()
        {
            ElementDefinition light = Element("Light", 1), shadow = Element("Shadow", 2), other = Element("Other", 3);
            ElementChart chart = Chart(new[] { light, shadow, other },
                Relation(light, shadow, 2f),
                Relation(shadow, light, 2f),
                Relation(other, light, 0.25f),
                Relation(light, other, 0.75f)).ToChart();

            var l = new Element(1);
            var s = new Element(2);
            var o = new Element(3);
            Assert.AreEqual(2f, chart.Multiplier(l, s));
            Assert.AreEqual(2f, chart.Multiplier(s, l));
            Assert.AreEqual(0.25f, chart.Multiplier(o, l));
            Assert.AreEqual(0.75f, chart.Multiplier(l, o));
            Assert.AreEqual(1f, chart.Multiplier(s, o), "not listed");
            Assert.AreEqual(1f, chart.Multiplier(o, s), "not listed");
            for (int e = 1; e <= 3; e++) Assert.AreEqual(0f, chart.Multiplier(new Element(e), new Element(e)), $"element {e} is immune to itself");
        }

        [Test]
        public void S4b_AC1_ToChart_Works_With_Ids_That_Are_Not_Consecutive()
        {
            ElementDefinition first = Element("First", 1), far = Element("Far", 4);
            ElementChart chart = Chart(new[] { first, far }, Relation(first, far, 2f)).ToChart();

            Assert.AreEqual(2f, chart.Multiplier(new Element(1), new Element(4)));
            Assert.AreEqual(1f, chart.Multiplier(new Element(4), new Element(1)));
            Assert.AreEqual(1f, chart.Multiplier(new Element(2), new Element(1)), "an id between the listed ones");
        }

        [Test]
        public void S4b_AC1_ToChart_Of_An_Empty_Chart_Or_Null_Lists_Still_Has_Immunity_And_X1()
        {
            ElementChart empty = Chart(new ElementDefinition[0]).ToChart();
            Assert.AreEqual(0f, empty.Multiplier(new Element(1), new Element(1)));
            Assert.AreEqual(1f, empty.Multiplier(new Element(1), new Element(2)));

            ElementChartDefinition nulls = Make<ElementChartDefinition>("Nulls", ("elements", null), ("relations", null));
            ElementChart chart = nulls.ToChart();
            Assert.AreEqual(0f, chart.Multiplier(new Element(2), new Element(2)));
            Assert.AreEqual(1f, chart.Multiplier(new Element(1), new Element(2)));
        }

        [Test]
        public void S4b_AC1_ToChart_Throws_On_A_Null_In_The_Element_List()
        {
            ElementChartDefinition chart = Chart(new[] { Element("Light", 1), null });
            Assert.Throws<InvalidOperationException>(() => chart.ToChart());
        }

        [Test]
        public void S4b_AC1_ToChart_Throws_On_Two_Definitions_With_One_Id()
        {
            ElementChartDefinition chart = Chart(new[] { Element("Light", 1), Element("Shadow", 1) });
            var failure = Assert.Throws<InvalidOperationException>(() => chart.ToChart());
            StringAssert.Contains("share the id 1", failure.Message);
        }

        [Test]
        public void S4b_AC1_ToChart_Throws_On_The_Same_Definition_Listed_Twice()
        {
            ElementDefinition light = Element("Light", 1);
            ElementChartDefinition chart = Chart(new[] { light, light });
            Assert.Throws<InvalidOperationException>(() => chart.ToChart());
        }

        [Test]
        public void S4b_AC1_ToChart_Throws_On_An_Element_With_An_Id_Below_One()
        {
            ElementChartDefinition chart = Chart(new[] { Element("Light", 1), Element("Broken", 0) });
            Assert.Throws<InvalidOperationException>(() => chart.ToChart());
        }

        [Test]
        public void S4b_AC1_ToChart_Throws_On_A_Relation_With_A_Null_Side_Or_A_Null_Relation()
        {
            ElementDefinition light = Element("Light", 1), shadow = Element("Shadow", 2);
            var elements = new[] { light, shadow };

            Assert.Throws<InvalidOperationException>(() => Chart(elements, Relation(null, shadow, 2f)).ToChart(), "attacker");
            Assert.Throws<InvalidOperationException>(() => Chart(elements, Relation(light, null, 2f)).ToChart(), "defender");
            Assert.Throws<InvalidOperationException>(() => Chart(elements, Relation(null, null, 2f)).ToChart(), "both");
            Assert.Throws<InvalidOperationException>(() => Chart(elements, new object[] { null }).ToChart(), "the relation itself");
        }

        [Test]
        public void S4b_AC1_ToChart_Throws_On_A_Relation_Using_An_Element_The_Chart_Does_Not_List()
        {
            ElementDefinition light = Element("Light", 1), shadow = Element("Shadow", 2), unlisted = Element("Unlisted", 3);
            var elements = new[] { light, shadow };

            Assert.Throws<InvalidOperationException>(() => Chart(elements, Relation(unlisted, light, 2f)).ToChart(), "attacker");
            Assert.Throws<InvalidOperationException>(() => Chart(elements, Relation(light, unlisted, 2f)).ToChart(), "defender");
        }

        [Test]
        public void S4b_AC3_ToChart_Refuses_A_Relation_Of_One_Element_And_A_Multiplier_At_Or_Below_Zero()
        {
            ElementDefinition light = Element("Light", 1), shadow = Element("Shadow", 2);
            var elements = new[] { light, shadow };

            Assert.Throws<ArgumentException>(() => Chart(elements, Relation(light, light, 2f)).ToChart(), "the same element, whatever the value");
            Assert.Throws<ArgumentException>(() => Chart(elements, Relation(light, light, 0f)).ToChart(), "an explicit immunity row");
            Assert.Throws<ArgumentOutOfRangeException>(() => Chart(elements, Relation(light, shadow, 0f)).ToChart(), "zero");
            Assert.Throws<ArgumentOutOfRangeException>(() => Chart(elements, Relation(light, shadow, -1f)).ToChart(), "negative");
            Assert.Throws<ArgumentOutOfRangeException>(() => Chart(elements, Relation(light, shadow, float.NaN)).ToChart(), "not a number");
        }

        [Test]
        public void S4b_AC1_A_Later_Relation_For_A_Pair_Replaces_An_Earlier_One()
        {
            ElementDefinition light = Element("Light", 1), shadow = Element("Shadow", 2);
            ElementChart chart = Chart(new[] { light, shadow }, Relation(light, shadow, 2f), Relation(light, shadow, 0.5f)).ToChart();
            Assert.AreEqual(0.5f, chart.Multiplier(new Element(1), new Element(2)));
        }

        // ---- ToStats

        [Test]
        public void S4b_AC1_A_Tower_Definition_Passes_Its_Element_And_Its_Numbers_To_Its_Stats()
        {
            TowerStats stats = Tower(Element("Shadow", 2)).ToStats();

            Assert.AreEqual(new Element(2), stats.Element);
            Assert.AreEqual(3.5f, stats.Range);
            Assert.AreEqual(12f, stats.Damage);
            Assert.AreEqual(1.25f, stats.ShotsPerSecond);
            Assert.AreEqual(7f, stats.ProjectileSpeed);
            Assert.AreEqual(9, stats.Cost);
        }

        [Test]
        public void S4b_AC1_An_Enemy_Definition_Passes_Its_Element_And_Its_Numbers_To_Its_Stats()
        {
            EnemyStats stats = Enemy(Element("Light", 1)).ToStats();

            Assert.AreEqual(new Element(1), stats.Element);
            Assert.AreEqual(1.5f, stats.Speed);
            Assert.AreEqual(45f, stats.MaxHealth);
            Assert.AreEqual(3, stats.GoldReward);
        }

        [Test]
        public void S4b_AC3_A_Tower_Or_Enemy_Definition_With_No_Element_Assigned_Gives_No_Element()
        {
            Assert.IsTrue(Tower(null).ToStats().Element.IsNone);
            Assert.IsTrue(Enemy(null).ToStats().Element.IsNone);
        }

        [Test]
        public void S4b_AC1_A_Definition_Whose_Element_Has_An_Id_Below_One_Does_Not_Turn_Into_Stats()
        {
            ElementDefinition broken = Element("Broken", 0);
            Assert.Throws<InvalidOperationException>(() => Tower(broken).ToStats());
            Assert.Throws<InvalidOperationException>(() => Enemy(broken).ToStats());
        }

        [Test]
        public void S4b_AC1_The_Two_Towers_Of_Different_Elements_Differ_Only_In_Their_Element()
        {
            TowerStats a = Tower(Element("Light", 1)).ToStats();
            TowerStats b = Tower(Element("Shadow", 2)).ToStats();

            Assert.AreNotEqual(a.Element, b.Element);
            Assert.AreEqual((a.Range, a.Damage, a.ShotsPerSecond, a.ProjectileSpeed, a.Cost), (b.Range, b.Damage, b.ShotsPerSecond, b.ProjectileSpeed, b.Cost));
        }
    }

    /// <summary>The source rules of 4b-AC1 and AC13.</summary>
    public class ElementSourceRulesTests
    {
        private static IEnumerable<string> Files(params string[] folders)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "_Game"));
            string testsDir = Path.Combine(root, "Tests") + Path.DirectorySeparatorChar;
            foreach (string folder in folders)
            {
                string dir = folder == "" ? root : Path.Combine(root, folder);
                foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
                    if (!file.StartsWith(testsDir)) yield return file;
            }
        }

        [Test]
        public void S4b_AC1_No_Code_File_Outside_Tests_Names_An_Element()
        {
            int scanned = 0;
            foreach (string file in Files(""))
            {
                scanned++;
                string text = File.ReadAllText(file);
                StringAssert.DoesNotContain("Light", text, file);
                StringAssert.DoesNotContain("Shadow", text, file);
            }
            Assert.Greater(scanned, 10);
        }

        [Test]
        public void S4b_AC13_No_File_Under_Core_Data_Or_Gameplay_Mentions_A_Material_Or_A_Colour()
        {
            int scanned = 0;
            foreach (string file in Files("Core", "Data", "Gameplay"))
            {
                scanned++;
                string text = File.ReadAllText(file);
                StringAssert.DoesNotContain("Material", text, file);
                StringAssert.DoesNotContain("Color", text, file);
                StringAssert.DoesNotContain("Colour", text, file);
            }
            Assert.Greater(scanned, 10);
        }
    }
}
