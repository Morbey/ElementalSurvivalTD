using System;
using System.Collections;
using System.Linq;
using ElementalTD.Core;
using ElementalTD.Presentation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace ElementalTD.PlayModeTests
{
    /// <summary>Step 5b, 5b-AC13: a damage number shows the damage dealt, and a repeated amount costs no allocation.</summary>
    public class PerkNumbersSceneTests : FloatingSceneBase
    {
        [UnityTest]
        public IEnumerator S5b_AC13_A_Light_Tower_Hits_A_Shadow_Enemy_For_25_After_A_Light_Damage_Pick_And_The_Repeated_Amount_Allocates_Nothing()
        {
            Assert.AreEqual(PlacementResult.Placed, Battle.TryBuyTower(new GridPos(1, 3), _input.TowerAt(0).ToStats()));
            Board.SpawnEnemy(new EnemyStats(0f, 1_000_000f, 0, ElementOfTower(1)));   // a Shadow enemy that stands at the spawn
            yield return Frames(2);

            // Before the pick: 10 x2 = 20.
            while (Battle.HitCount < 1) Tick();
            yield return null;
            Assert.AreEqual(20f, Battle.HitAt(0).Amount, 0f);
            Assert.IsTrue(DrawnNumbers().Any(n => n.Text == "20"), "a 20 shows before the pick");

            NamePicks(0);
            Battle.Progression.AddXp(30);
            Tick();
            Assert.IsFalse(Battle.AwaitingChoice);
            int seen = Battle.HitCount;
            while (Battle.HitCount == seen) Tick();
            Assert.AreEqual(25f, Battle.HitAt(seen).Amount, 0f, "10 x1.25 x2");
            yield return null;
            Assert.IsTrue(DrawnNumbers().Any(n => n.Text == "25"), "the number reads the damage dealt: 25");

            // The amount 25 has been seen: further hits of 25 allocate nothing on their frame.
            Action frame = FrameMethod(NumberView);
            NumberView.enabled = false;
            frame();
            int repeats = 0;
            for (int i = 0; i < 400 && repeats < 3; i++)
            {
                int before = Battle.HitCount;
                Tick();
                int count = Allocations.Of(frame);
                if (Battle.HitCount > before)
                {
                    repeats++;
                    Assert.AreEqual(25f, Battle.HitAt(Battle.HitCount - 1).Amount, 0f);
                    Assert.AreEqual(0, count, $"the frame of repeated hit {repeats}, an amount seen before");
                }
                yield return null;
            }
            Assert.AreEqual(3, repeats);
            NumberView.enabled = true;
        }
    }
}
