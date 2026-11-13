using System.Collections;
using System.Collections.Generic;
using ElementalTD.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ElementalTD.PlayModeTests
{
    /// <summary>Stack cases that pin single mistakes in the push, the leave and the caps (plan 0009, 4d-AC7, AC8, AC15).</summary>
    public partial class DamageNumberSceneTests
    {
        [UnityTest]
        public IEnumerator S4d_AC7_The_Lift_Is_The_Newer_Numbers_Height_On_Every_Tick_Of_The_Push_Whatever_The_Kinds([Values(HitKind.Weak, HitKind.Strong)] HitKind older, [Values(HitKind.Weak, HitKind.Strong)] HitKind newer)
        {
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f);
            yield return null;
            ReferenceStack model = NewStack();
            float newerHeight = HeightOf(newer);
            yield return Drive(model, 12, k => k == 0 ? new List<Hit> { HitOn(enemy, 9f, older) } : k == 3 ? new List<Hit> { HitOn(enemy, 8f, newer) } : null, (k, pairs) =>
            {
                var first = pairs.Find(p => p.expected.Order == 0);
                if (k < 3 || first.expected.OnLine) return;
                float lift = newerHeight * Ease(Mathf.Min(1f, (k - 3) * Dt / P));
                Assert.AreEqual(Slot0Over(enemy).y - lift, first.drawn.Bottom.y, PositionTolerance, $"tick {k}: lifted by the NEWER number's height ({newerHeight})");
            });
        }

        [UnityTest]
        public IEnumerator S4d_AC7_Three_Records_On_One_Tick_Ease_Their_Lifts_On_Every_Tick()
        {
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f);
            yield return null;
            ReferenceStack model = NewStack();
            yield return Drive(model, 10, k => k == 0 ? new List<Hit> { HitOn(enemy, 1f, HitKind.Strong), HitOn(enemy, 2f, HitKind.Strong), HitOn(enemy, 3f, HitKind.Strong) } : null, (k, pairs) =>
            {
                float e = Ease(Mathf.Min(1f, k * Dt / P));
                foreach (var pair in pairs)
                {
                    if (pair.expected.OnLine) continue;
                    float lift = 23f * e * (2 - pair.expected.Order);
                    Assert.AreEqual(Slot0Over(enemy).y - lift, pair.drawn.Bottom.y, PositionTolerance, $"tick {k}: number {pair.expected.Order} eases, it does not jump");
                }
            });
        }

        [UnityTest]
        public IEnumerator S4d_AC8_A_Record_Read_Late_Makes_The_First_Leave_From_Its_Own_Tick_Or_At_Once_When_That_Is_p_Ago([Values(3, 6)] int ticksAgo)
        {
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f);
            for (int i = 0; i < 20; i++) Battle.Tick();
            yield return null;
            ReferenceStack model = NewStack();
            int now = Battle.TickCount;
            var hits = new List<Hit>();
            foreach (int tick in new[] { now - 8, now - 7, now - 6 })
                hits.Add(new Hit(enemy.X, enemy.Y, 20f, HitKind.Strong, tick, enemy.Entry));
            hits.Add(new Hit(enemy.X, enemy.Y, 20f, HitKind.Strong, now - ticksAgo, enemy.Entry));
            foreach (Hit h in hits) { NumberView.Show(h); model.Show(h); }
            yield return null;
            var pairs = MatchDrawn(model.Now(BoardDrawn()), DrawnNumbers(), $"a fourth record stamped {ticksAgo} ticks ago");
            var first = pairs.Find(p => p.expected.Order == 0);
            if (ticksAgo * Dt >= P)
            {
                Assert.IsNull(first.expected, "hidden on that same frame, its label free");
                Assert.AreEqual(3, pairs.Count);
            }
            else
            {
                float t = 12 * Dt / 0.5f; // the first is 8 ticks old... aged from its own tick
                float age = 8 * Dt, raw = age / 0.5f <= 0.5f ? 1f : (1f - age / 0.5f) / 0.5f;
                Assert.AreEqual(Mathf.Min(0.7f, raw) * (1f - ticksAgo * Dt / P), first.drawn.Opacity, 0.002f, "the leave runs from the record's tick, not from now");
                Assert.IsNotNull(t);
            }
        }

        [UnityTest]
        public IEnumerator S4d_AC8_The_Leave_Multiplies_The_Life_Fade_And_The_Cap_Takes_The_Minimum([Values(6, 8)] int spacing)
        {
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f);
            yield return null;
            ReferenceStack model = NewStack();
            int fourth = spacing * 3;
            yield return Drive(model, fourth + 6, k => k % spacing == 0 && k <= fourth ? new List<Hit> { HitOn(enemy, 20f, HitKind.Strong) } : null, (k, pairs) =>
            {
                var first = pairs.Find(p => p.expected.Order == 0);
                if (k < fourth || k >= fourth + 5 || first.expected == null) return;
                float t = k * Dt / 0.5f;
                float raw = t <= 0.5f ? 1f : (1f - t) / 0.5f;
                float expected = Mathf.Min(0.7f, raw) * (1f - (k - fourth) * Dt / P);
                Assert.AreEqual(expected, first.drawn.Opacity, 0.002f, $"spacing {spacing}, tick {k}: raw {raw:F2}, capped, then multiplied by the leave");
            });
        }

        [UnityTest]
        public IEnumerator S4d_AC8_A_Number_In_The_Last_Slot_At_90_Percent_Of_Its_Life_Shows_0_2_Not_0_7()
        {
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f);
            yield return null;
            ReferenceStack model = NewStack();
            bool seen = false;
            yield return Drive(model, 28, k => k == 0 || k == 12 || k == 24 ? new List<Hit> { HitOn(enemy, 20f, HitKind.Strong) } : null, (k, pairs) =>
            {
                if (k != 27) return;
                var first = pairs.Find(p => p.expected.Order == 0);
                Assert.AreEqual(0.2f, first.drawn.Opacity, 0.01f, "27 of 30 ticks: the life fade is 0.2, below the cap");
                seen = true;
            });
            Assert.IsTrue(seen);
        }

        [UnityTest]
        public IEnumerator S4d_AC8_A_Further_Push_Does_Not_Restart_The_Leave()
        {
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f);
            yield return null;
            ReferenceStack model = NewStack();
            yield return Drive(model, 30, k => k == 0 || k == 6 || k == 12 || k == 18 || k == 20 ? new List<Hit> { HitOn(enemy, 20f, HitKind.Strong) } : null, (k, pairs) =>
            {
                var first = pairs.Find(p => p.expected.Order == 0);
                if (k >= 18 + 5) Assert.IsNull(first.expected, $"tick {k}: the first still ends p after the fourth record");
                if (k == 22) Assert.IsNotNull(first.expected);
            });
        }

        [UnityTest]
        public IEnumerator S4d_AC15_With_Two_Enemies_Having_Numbers_The_First_Exiting_Leaves_The_Seconds_On_The_Second()
        {
            Enemy a = SpawnWave1Enemy(health: 1_000_000f, speed: 12f);
            Enemy b = SpawnWave1Enemy(health: 1_000_000f, speed: 2f);
            yield return null;
            ReferenceStack model = NewStack();
            int ticks = 0;
            while (OnBoard(a) && ticks < 400) { Battle.Tick(); ticks++; if (a.DistanceToExit < 40f && ticks % 6 == 0) { var hb = HitOn(b, 20f, HitKind.Strong); NumberView.Show(hb); model.Show(hb); if (OnBoard(a)) { var ha = HitOn(a, 21f, HitKind.Strong); NumberView.Show(ha); model.Show(ha); } } if (ticks % 2 == 0) { yield return null; MatchDrawn(model.Frame(BoardDrawn()), DrawnNumbers(), $"tick {ticks}"); } }
            yield return null;
            Assert.IsFalse(OnBoard(a));
            var pairs = MatchDrawn(model.Frame(BoardDrawn()), DrawnNumbers(), "after the first exited");
            var onSecond = pairs.FindAll(p => p.expected.Entry == b.Entry && !p.expected.OnLine);
            Assert.Greater(onSecond.Count, 0, "the second enemy still has numbers");
            for (int i = 0; i < 6; i++)
            {
                Battle.Tick();
                yield return null;
                pairs = MatchDrawn(model.Frame(BoardDrawn()), DrawnNumbers(), "after");
                foreach (var pair in pairs)
                    if (pair.expected.Entry == b.Entry && pair.expected.Slot == 0 && !pair.expected.OnLine)
                        Assert.AreEqual(Slot0Over(b).x, pair.drawn.Bottom.x, PositionTolerance, "the second's numbers stay on the second");
            }
        }
    }
}
