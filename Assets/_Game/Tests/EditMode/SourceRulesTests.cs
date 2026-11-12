using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ElementalTD.Core.Tests
{
    public class SourceRulesTests
    {
        // ADR 0002 rule 3 forbids creating a material by constructor; an array of material references is fine.
        private static readonly Regex NewMaterialCall = new Regex(@"new\s+(UnityEngine\.)?Material\s*\(");

        [Test]
        public void S2a_AC4_The_New_Material_Rule_Matches_The_Constructor_And_Not_An_Array()
        {
            Assert.IsTrue(NewMaterialCall.IsMatch("var m = new Material(shader);"));
            Assert.IsTrue(NewMaterialCall.IsMatch("var m = new UnityEngine.Material(shader);"));
            Assert.IsTrue(NewMaterialCall.IsMatch("var m = new  Material (shader);"));
            Assert.IsFalse(NewMaterialCall.IsMatch("Material[] all = new Material[looks.Length];"));
            Assert.IsFalse(NewMaterialCall.IsMatch("Material[] all = new UnityEngine.Material[2];"));
        }

        [Test]
        public void S2a_AC4_No_CreatePrimitive_Or_New_Material_Outside_Tests()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "_Game"));
            string testsDir = Path.Combine(root, "Tests") + Path.DirectorySeparatorChar;
            int scanned = 0;
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.StartsWith(testsDir)) continue;
                scanned++;
                string text = File.ReadAllText(file);
                StringAssert.DoesNotContain("CreatePrimitive", text, file);
                Assert.IsFalse(NewMaterialCall.IsMatch(text), file);
            }
            Assert.Greater(scanned, 0);
        }

        [Test]
        public void S2c_AC12_No_Gameplay_Source_Instantiates_Anything()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "_Game", "Gameplay"));
            string[] files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
            Assert.Greater(files.Length, 0);
            foreach (string file in files)
                StringAssert.DoesNotContain("Instantiate", File.ReadAllText(file), file);
        }

        [Test]
        public void S2c_AC10_Only_The_Bootstrap_Calls_Battle_Tick_Outside_Core_And_Tests()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "_Game"));
            string testsDir = Path.Combine(root, "Tests") + Path.DirectorySeparatorChar;
            string coreDir = Path.Combine(root, "Core") + Path.DirectorySeparatorChar;
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.StartsWith(testsDir) || file.StartsWith(coreDir) || Path.GetFileName(file) == "PrototypeBootstrap.cs") continue;
                StringAssert.DoesNotContain(".Tick()", File.ReadAllText(file), file);
            }
        }

        [Test]
        public void S3a_AC2_No_Script_Scene_Asset_Or_Prefab_Mentions_A_Reentry()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "_Game"));
            string testsDir = Path.Combine(root, "Tests") + Path.DirectorySeparatorChar;
            int scanned = 0;
            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(file);
                if (extension != ".cs" && extension != ".unity" && extension != ".asset" && extension != ".prefab") continue;
                if (file.StartsWith(testsDir)) continue;
                scanned++;
                string text = File.ReadAllText(file).ToLowerInvariant().Replace("_", "").Replace("-", "");
                StringAssert.DoesNotContain("reentry", text, file);
            }
            Assert.Greater(scanned, 0);
        }

        private static string[] SourcesOutsideCoreAndTests(out string root)
        {
            root = Path.GetFullPath(Path.Combine(Application.dataPath, "_Game"));
            string testsDir = Path.Combine(root, "Tests") + Path.DirectorySeparatorChar;
            string coreDir = Path.Combine(root, "Core") + Path.DirectorySeparatorChar;
            var files = new System.Collections.Generic.List<string>();
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                if (!file.StartsWith(testsDir) && !file.StartsWith(coreDir)) files.Add(file);
            return files.ToArray();
        }

        [Test]
        public void S4c_AC11_Outside_Core_And_Tests_Only_The_Placement_Input_Calls_TrySellTower()
        {
            int callers = 0;
            foreach (string file in SourcesOutsideCoreAndTests(out _))
            {
                if (!File.ReadAllText(file).Contains("TrySellTower(")) continue;
                callers++;
                Assert.AreEqual("TowerPlacementInput.cs", Path.GetFileName(file), "another file sells");
            }
            Assert.AreEqual(1, callers, "the placement input sells");
        }

        [Test]
        public void S4c_AC11_No_Presentation_File_Mentions_The_Refund_Percent_Or_Computes_A_Refund()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "_Game", "Presentation"));
            int scanned = 0;
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                scanned++;
                string text = File.ReadAllText(file);
                StringAssert.DoesNotContain("RefundPercent", text, file);
                StringAssert.DoesNotContain("TrySellTower", text, file);
                StringAssert.DoesNotContain("SellRefund", text, file);
            }
            Assert.Greater(scanned, 5);
        }

        [Test]
        public void S4c_AC11_The_Selection_Lives_In_Gameplay_Knows_No_View_And_Its_Update_Allocates_Nothing_By_Source()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "_Game"));
            string text = File.ReadAllText(Path.Combine(root, "Gameplay", "TowerPlacementInput.cs"));
            StringAssert.Contains("Tower SelectedTower", text);
            foreach (string forbidden in new[] { "ElementalTD.Presentation", "UIElements", "Material", "Color", "Renderer", "VisualElement" })
                StringAssert.DoesNotContain(forbidden, text, "Gameplay mentions a view, a material or a colour");

            int start = text.IndexOf("private void Update()");
            Assert.GreaterOrEqual(start, 0);
            int open = text.IndexOf('{', start), depth = 0, end = open;
            for (; end < text.Length; end++)
            {
                if (text[end] == '{') depth++;
                else if (text[end] == '}' && --depth == 0) break;
            }
            string update = text.Substring(open, end - open);
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(update, @"\bnew\b"), "Update has a new");

            foreach (string file in Directory.GetFiles(Path.Combine(root, "Core"), "*.cs", SearchOption.AllDirectories))
                StringAssert.DoesNotContain("UnityEngine", File.ReadAllText(file), file);
        }

        [Test]
        public void S4c_AC10_The_Web_Canvas_Is_960_By_540_In_The_Player_Settings_And_On_Disk()
        {
            Assert.AreEqual(960, UnityEditor.PlayerSettings.defaultWebScreenWidth);
            Assert.AreEqual(540, UnityEditor.PlayerSettings.defaultWebScreenHeight);
            string file = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ProjectSettings", "ProjectSettings.asset"));
            string text = File.ReadAllText(file);
            StringAssert.Contains("defaultScreenWidthWeb: 960", text);
            StringAssert.Contains("defaultScreenHeightWeb: 540", text);
        }
    }
}
