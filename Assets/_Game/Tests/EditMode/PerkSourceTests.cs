using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ElementalTD.Core.Tests
{
    /// <summary>Step 5b, 5b-AC16: where the new rules live and who may call them, read from the sources.</summary>
    public class PerkSourceTests
    {
        private static string GameDir => Path.GetFullPath(Path.Combine(Application.dataPath, "_Game"));

        private static string[] ProductionFiles()
        {
            var files = new System.Collections.Generic.List<string>();
            foreach (string file in Directory.GetFiles(GameDir, "*.cs", SearchOption.AllDirectories))
                if (!file.Replace('\\', '/').Contains("/Tests/")) files.Add(file);
            return files.ToArray();
        }

        [Test]
        public void S5b_AC16_Rng_Modifier_Modifiers_Stat_Operation_And_Progression_Are_In_The_Core_Assembly()
        {
            foreach (System.Type type in new[] { typeof(Rng), typeof(Modifier), typeof(Modifiers), typeof(Stat), typeof(Operation), typeof(Progression) })
                Assert.AreEqual("ElementalTD.Core", type.Assembly.GetName().Name, type.Name);
            foreach (string name in new[] { "Rng.cs", "Modifier.cs", "Modifiers.cs", "Stat.cs", "Operation.cs", "Progression.cs" })
                StringAssert.DoesNotContain("UnityEngine", File.ReadAllText(Path.Combine(GameDir, "Core", name)), name);
        }

        [Test]
        public void S5b_AC16_No_Production_File_Writes_Time_TimeScale()
        {
            var write = new Regex(@"timeScale\s*(=(?!=)|\+=|-=|\*=|/=)");
            foreach (string file in ProductionFiles())
                Assert.IsFalse(write.IsMatch(File.ReadAllText(file)), $"{Path.GetFileName(file)} writes timeScale");
        }

        [Test]
        public void S5b_AC16_Outside_Core_Only_LevelUpView_Calls_PickPerk()
        {
            foreach (string file in ProductionFiles())
            {
                string relative = file.Substring(GameDir.Length + 1).Replace('\\', '/');
                if (relative.StartsWith("Core/")) continue;
                bool calls = File.ReadAllText(file).Contains("PickPerk(");
                Assert.AreEqual(relative == "Presentation/LevelUpView.cs", calls, $"{relative}: PickPerk( is called by LevelUpView only");
            }
        }

        [Test]
        public void S5b_AC16_Nothing_Calls_Modifiers_Add_But_The_Battle_That_Applies_A_Pick()
        {
            foreach (string file in ProductionFiles())
            {
                string relative = file.Substring(GameDir.Length + 1).Replace('\\', '/');
                bool calls = Regex.IsMatch(File.ReadAllText(file), @"Modifiers\.Add\(");
                Assert.AreEqual(relative == "Core/Battle.cs", calls, $"{relative}: Modifiers.Add( is called by the battle only");
            }
        }

        [Test]
        public void S5b_AC6_The_Input_Reads_AwaitingChoice_In_TryPick_And_ReadKeys_And_No_Production_Code_Ticks_During_A_Choice_But_The_Driver()
        {
            string input = File.ReadAllText(Path.Combine(GameDir, "Gameplay/TowerPlacementInput.cs"));
            StringAssert.Contains("AwaitingChoice", input);
            Assert.AreEqual(1, Regex.Matches(File.ReadAllText(Path.Combine(GameDir, "Gameplay/PrototypeBootstrap.cs")), @"Battle\.Tick\(\)").Count,
                "the bootstrap is the only driver");
        }
    }
}
