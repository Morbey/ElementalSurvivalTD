using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ElementalTD.Core.Tests
{
    public class CoreAssemblyTests
    {
        [System.Serializable]
        private class AsmdefData
        {
            public string name;
            public string[] references;
            public bool noEngineReferences;
        }

        private static string CoreDir => Path.GetFullPath(Path.Combine(Application.dataPath, "_Game/Core"));

        [Test]
        public void AC6_Core_Asmdef_NoEngineReferences_True_And_NoReferences()
        {
            var data = JsonUtility.FromJson<AsmdefData>(File.ReadAllText(Path.Combine(CoreDir, "ElementalTD.Core.asmdef")));
            Assert.AreEqual("ElementalTD.Core", data.name);
            Assert.IsTrue(data.noEngineReferences);
            Assert.IsEmpty(data.references);
        }

        [Test]
        public void AC6_Core_Sources_Do_Not_Reference_UnityEngine()
        {
            string[] files = Directory.GetFiles(CoreDir, "*.cs", SearchOption.AllDirectories);
            Assert.IsNotEmpty(files);
            foreach (string file in files)
                StringAssert.DoesNotContain("UnityEngine", File.ReadAllText(file), file);
        }
    }
}
