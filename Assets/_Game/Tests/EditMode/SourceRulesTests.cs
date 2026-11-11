using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ElementalTD.Core.Tests
{
    public class SourceRulesTests
    {
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
                StringAssert.DoesNotContain("new Material", text, file);
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
    }
}
