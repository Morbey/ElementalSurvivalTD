using System;
using ElementalTD.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ElementalTD.Core.Tests
{
    public class RunDefinitionTests
    {
        private static EnemyDefinition Enemy(float speed, float health)
        {
            var enemy = ScriptableObject.CreateInstance<EnemyDefinition>();
            JsonUtility.FromJsonOverwrite($"{{\"speedCellsPerSecond\":{speed},\"maxHealth\":{health}}}", enemy);
            return enemy;
        }

        private static RunDefinition Run(int lives, params (EnemyDefinition enemy, int count, float interval, float delay)[] waves)
        {
            var run = ScriptableObject.CreateInstance<RunDefinition>();
            var so = new SerializedObject(run);
            so.FindProperty("lives").intValue = lives;
            SerializedProperty array = so.FindProperty("waves");
            array.arraySize = waves.Length;
            for (int i = 0; i < waves.Length; i++)
            {
                SerializedProperty entry = array.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("enemy").objectReferenceValue = waves[i].enemy;
                entry.FindPropertyRelative("count").intValue = waves[i].count;
                entry.FindPropertyRelative("spawnIntervalSeconds").floatValue = waves[i].interval;
                entry.FindPropertyRelative("delayBeforeSeconds").floatValue = waves[i].delay;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return run;
        }

        [Test]
        public void S3a_AC12_ToWaves_Returns_The_Entries_In_Order_With_Their_Own_Enemy_And_Lives_Are_Exposed()
        {
            EnemyDefinition a = Enemy(2f, 100f), b = Enemy(3f, 40f);
            RunDefinition run = Run(7, (a, 3, 1f, 15f), (b, 4, 0.5f, 8f));
            try
            {
                Wave[] waves = run.ToWaves();

                Assert.AreEqual(7, run.Lives);
                Assert.AreEqual(2, waves.Length);
                Assert.AreEqual((2f, 100f, 3, 1f, 15f), (waves[0].GroupAt(0).Enemy.Speed, waves[0].GroupAt(0).Enemy.MaxHealth, waves[0].GroupAt(0).Count, waves[0].GroupAt(0).SpawnIntervalSeconds, waves[0].DelayBeforeSeconds));
                Assert.AreEqual((3f, 40f, 4, 0.5f, 8f), (waves[1].GroupAt(0).Enemy.Speed, waves[1].GroupAt(0).Enemy.MaxHealth, waves[1].GroupAt(0).Count, waves[1].GroupAt(0).SpawnIntervalSeconds, waves[1].DelayBeforeSeconds));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(run);
                UnityEngine.Object.DestroyImmediate(a);
                UnityEngine.Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void S3a_AC12_ToWaves_Throws_For_No_Wave_And_For_A_Wave_Without_An_Enemy()
        {
            EnemyDefinition a = Enemy(2f, 100f);
            RunDefinition empty = Run(3);
            RunDefinition missing = Run(3, (a, 1, 1f, 0f), (null, 1, 1f, 0f));
            try
            {
                Assert.Throws<InvalidOperationException>(() => empty.ToWaves());
                Assert.Throws<InvalidOperationException>(() => missing.ToWaves());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(empty);
                UnityEngine.Object.DestroyImmediate(missing);
                UnityEngine.Object.DestroyImmediate(a);
            }
        }
    }
}
