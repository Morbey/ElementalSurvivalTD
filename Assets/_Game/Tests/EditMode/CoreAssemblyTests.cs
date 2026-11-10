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

        [Test]
        public void S2b_AC9_Gameplay_Asmdef_Does_Not_Reference_Presentation_And_Presentation_Asmdef_Exists()
        {
            string game = Path.GetFullPath(Path.Combine(Application.dataPath, "_Game"));
            var gameplay = JsonUtility.FromJson<AsmdefData>(File.ReadAllText(Path.Combine(game, "Gameplay/ElementalTD.Gameplay.asmdef")));
            CollectionAssert.DoesNotContain(gameplay.references, "ElementalTD.Presentation");
            Assert.IsTrue(File.Exists(Path.Combine(game, "Presentation/ElementalTD.Presentation.asmdef")));
        }
    }
}
