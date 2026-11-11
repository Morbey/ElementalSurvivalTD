using System.Collections;
using ElementalTD.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ElementalTD.PlayModeTests
{
    /// <summary>The survived line and the gold label on the scene (4a-AC4). Nothing is built unless a test says so: wave 1 pays on tick 1672 and wave 2 on tick 2332.</summary>
    public class BonusSceneTests : SceneTestBase
    {
        private int _ticks;

        // NUnit reuses one fixture instance for every test.
        [SetUp]
        public void ResetTicks() => _ticks = 0;

        /// <summary>
        /// Ticks by hand until this many waves have paid, stopping on the paying tick itself and yielding no frame after it.
        /// Frames in between only let the views render; a frame never falls between the paying tick and the caller's checks.
        /// </summary>
        private IEnumerator TickToSurvived(int waves, int ticksPerFrame = 50)
        {
            while (Battle.SurvivedWaves < waves && _ticks < 6000)
            {
                Battle.Tick();
                _ticks++;
                if (Battle.SurvivedWaves < waves && _ticks % ticksPerFrame == 0) yield return null;
            }
            Assert.AreEqual(waves, Battle.SurvivedWaves, "the wave never paid");
        }

        private string Survived(int wave) => $"Wave {wave} survived +{Battle.Waves.WaveAt(wave - 1).SurvivalBonus}";

        [UnityTest]
        public IEnumerator S4a_AC4_The_Line_And_The_Gold_Label_Follow_The_Paying_Tick_By_One_Frame()
        {
            int start = Battle.Gold;
            int bonus = Battle.Waves.WaveAt(0).SurvivalBonus;
            yield return TickToSurvived(1);

            Assert.AreEqual(1672, _ticks, 1, "document: wave 1 pays on tick 1672");
            Assert.AreEqual(start + bonus, Battle.Gold);
            Assert.AreEqual(125, Battle.Gold);
            Assert.AreEqual(1, Battle.LastSurvivedWave);
            Assert.IsFalse(MessageShown(), "no frame has run since the paying tick");
            Assert.AreEqual($"Gold {start}", GoldLabel.text, "the label still shows the old gold");

            yield return null;

            Assert.IsTrue(MessageShown());
            Assert.AreEqual("Wave 1 survived +10", _label.text);
            Assert.AreEqual(Survived(1), _label.text);
            Assert.AreEqual($"Gold {start + bonus}", GoldLabel.text);
        }

        [UnityTest]
        public IEnumerator S4a_AC4_The_Line_Hides_After_The_Duration_In_Unscaled_Time_And_The_Gold_Stays()
        {
            yield return TickToSurvived(1);
            yield return null;
            Assert.IsTrue(MessageShown());

            yield return new WaitForSecondsRealtime(1.5f);
            Assert.IsTrue(MessageShown(), "still shown before 2 s");
            yield return new WaitForSecondsRealtime(1.0f);
            Assert.IsFalse(MessageShown(), "hidden after 2 s");
            Assert.AreEqual($"Gold {Battle.Gold}", GoldLabel.text);
        }

        [UnityTest]
        public IEnumerator S4a_AC4_A_Refusal_Replaces_The_Survived_Line()
        {
            yield return TickToSurvived(1);
            yield return null;
            Assert.AreEqual(Survived(1), _label.text);

            ClickCell(_map.Spawn.X, _map.Spawn.Y); // refused: spawn or exit
            yield return null;

            Assert.IsTrue(MessageShown());
            Assert.AreNotEqual(Survived(1), _label.text, "replaced");
            StringAssert.DoesNotContain("survived", _label.text);
            Assert.IsNotEmpty(_label.text);
        }

        [UnityTest]
        public IEnumerator S4a_AC4_The_Survived_Line_Replaces_A_Refusal_And_Starts_Its_Own_Timer()
        {
            ClickCell(_map.Spawn.X, _map.Spawn.Y);
            yield return null;
            string refusal = _label.text;
            Assert.IsNotEmpty(refusal);
            Assert.IsTrue(MessageShown());

            yield return TickToSurvived(1, 100);
            yield return null;
            Assert.AreNotEqual(refusal, _label.text);
            Assert.AreEqual(Survived(1), _label.text);
            Assert.IsTrue(MessageShown());

            // Whatever time the refusal had left, the survived line gets its full 2 s.
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.IsTrue(MessageShown(), "the line has its own full duration");
            yield return new WaitForSecondsRealtime(1.0f);
            Assert.IsFalse(MessageShown());
        }

        [UnityTest]
        public IEnumerator S4a_AC4_Two_Waves_Paying_Within_One_Frame_Show_The_Later_Waves_Text()
        {
            yield return TickToSurvived(1);
            Assert.AreEqual(1672, _ticks, 1);
            yield return TickToSurvived(2, ticksPerFrame: int.MaxValue); // no frame between the two payments

            Assert.AreEqual(2332, _ticks, 1, "document: wave 2 pays on tick 2332");
            Assert.AreEqual(135, Battle.Gold);
            Assert.AreEqual(2, Battle.LastSurvivedWave);
            yield return null;

            Assert.IsTrue(MessageShown());
            Assert.AreEqual(Survived(2), _label.text);
            Assert.AreEqual("Gold 135", GoldLabel.text);
        }

        [UnityTest]
        public IEnumerator S4a_AC4_The_Last_Waves_Line_On_A_Win_Shows_With_The_End_Screen_Without_Breaking_It()
        {
            int ticks = 0;
            while (Battle.Outcome == RunOutcome.Running && ticks < 12000)
            {
                for (int i = 0; i < 30 && Battle.Outcome == RunOutcome.Running; i++, ticks++)
                {
                    RunReferenceActionsAt(ticks);
                    Battle.Tick();
                }
                yield return null;
            }
            Assert.AreEqual(RunOutcome.Won, Battle.Outcome);
            Assert.AreEqual(10, Battle.SurvivedWaves, "the last wave paid on the winning tick");
            Assert.AreEqual(10, Battle.LastSurvivedWave);
            yield return null;

            VisualElement end = Root.Q<VisualElement>("end-screen");
            Assert.AreEqual(DisplayStyle.Flex, end.resolvedStyle.display);
            Assert.AreEqual("Victory", Root.Q<Label>("end-title").text);
            Assert.IsTrue(MessageShown());
            Assert.AreEqual("Wave 10 survived +10", _label.text);
            Assert.AreEqual($"Gold {Battle.Gold}", GoldLabel.text);
            Assert.AreEqual("Gold 145", GoldLabel.text);

            yield return new WaitForSecondsRealtime(2.3f);
            Assert.IsFalse(MessageShown(), "the line times out");
            Assert.AreEqual(DisplayStyle.Flex, end.resolvedStyle.display, "the end screen stays");
            Assert.AreEqual(RunOutcome.Won, Battle.Outcome);
        }
    }
}
