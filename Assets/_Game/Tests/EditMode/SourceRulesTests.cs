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
    }
}
