using ElementalTD.Data;
using NUnit.Framework;
using UnityEditor;

namespace ElementalTD.Core.Tests
{
    /// <summary>The prototype run asset (3a-AC12) and the provisional design targets built on it (3a-AC13).</summary>
    public class RealAssetRunTests
    {
        private const string Dir = "Assets/_Game/Data/Assets/";

        private static T Load<T>(string name) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Dir + name);
            Assert.IsNotNull(asset, name);
            return asset;
        }

        private static Battle Build(out Board board, out TowerStats tower, int? lives = null)
        {
            var map = Load<MapDefinition>("Map_Prototype.asset");
            var run = Load<RunDefinition>("Run_Prototype.asset");
            tower = Load<TowerDefinition>("Tower_Prototype.asset").ToStats();
            board = new Board(map.ToGridMap(), map.Spawn, map.Exit);
            return new Battle(board, run.ToWaves(), lives ?? run.Lives);
        }

        [Test]
        public void S3a_AC12_Run_Prototype_Holds_The_Closed_Values()
        {
            var run = Load<RunDefinition>("Run_Prototype.asset");
            var enemy = Load<EnemyDefinition>("Enemy_Prototype.asset").ToStats();

            Wave[] waves = run.ToWaves();

            Assert.AreEqual(10, run.Lives);
            Assert.AreEqual(10, waves.Length);
            for (int i = 0; i < waves.Length; i++)
            {
                Assert.AreEqual(3 + i, waves[i].Count, $"wave {i + 1} count");
                Assert.AreEqual(1f, waves[i].SpawnIntervalSeconds, $"wave {i + 1} interval");
                Assert.AreEqual(i == 0 ? 15f : 8f, waves[i].DelayBeforeSeconds, $"wave {i + 1} delay");
                Assert.That(waves[i].DelayBeforeSeconds, Is.InRange(5f, 20f));
                Assert.AreEqual((enemy.Speed, enemy.MaxHealth), (waves[i].Enemy.Speed, waves[i].Enemy.MaxHealth), $"wave {i + 1} enemy");
            }
        }

        [Test]
        public void S3a_AC13_ProvisionalTarget_The_Waves_Enter_At_The_Planned_Ticks_And_The_Last_Enemy_Enters_152s_In()
        {
            Battle battle = Build(out _, out _, lives: 100_000); // enough lives for the run to stay open
            var expectedFirst = new[] { 901, 1501, 2160, 2880, 3660, 4500, 5400, 6360, 7380, 8460 };
            var firstTicks = new System.Collections.Generic.List<int>();
            int started = 0, last = -1;
            for (int tick = 1; tick <= 12000 && last < 0; tick++)
            {
                battle.Tick();
                if (battle.Waves.StartedWaves > started)
                {
                    started = battle.Waves.StartedWaves;
                    firstTicks.Add(tick);
                }
                if (battle.Waves.IsFinished) last = tick;
            }

            Assert.AreEqual(expectedFirst.Length, firstTicks.Count);
            for (int i = 0; i < expectedFirst.Length; i++) Assert.AreEqual(expectedFirst[i], firstTicks[i], 1, $"wave {i + 1}");
            Assert.AreEqual(9120, last, 1, "the last enemy enters 152 s in");
        }

        [Test]
        public void S3a_AC13_ProvisionalTarget_Building_Nothing_Is_Defeated_50_To_53_Seconds_In()
        {
            Battle battle = Build(out _, out _);
            int lostAt = -1;
            for (int tick = 1; tick <= 6000 && lostAt < 0; tick++)
            {
                battle.Tick();
                if (battle.Outcome == RunOutcome.Lost) lostAt = tick;
            }

            Assert.Greater(lostAt, 0, "never lost");
            Assert.That(lostAt / 60f, Is.InRange(50f, 53f));
            Assert.AreEqual(0, battle.Lives);
        }

        [Test]
        public void S3a_AC13_ProvisionalTarget_The_Eight_Planned_Towers_Win_With_All_Lives_Between_2_30_And_3_00()
        {
            Battle battle = Build(out Board board, out TowerStats tower);
            foreach (var (x, y) in new[] { (3, 5), (6, 6), (8, 6), (8, 3), (11, 2), (13, 2), (14, 3), (3, 3) })
                Assert.AreEqual(PlacementResult.Placed, board.TryPlaceTower(new GridPos(x, y), tower), $"({x},{y})");

            int wonAt = -1;
            for (int tick = 1; tick <= 12000 && battle.Outcome == RunOutcome.Running; tick++)
            {
                battle.Tick();
                if (battle.Outcome != RunOutcome.Running) wonAt = tick;
            }

            Assert.AreEqual(RunOutcome.Won, battle.Outcome);
            Assert.AreEqual(10, battle.Lives, "all lives");
            Assert.That(wonAt / 60f, Is.InRange(150f, 180f));
        }
    }
}
