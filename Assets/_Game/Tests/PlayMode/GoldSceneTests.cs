using System.Collections;
using ElementalTD.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ElementalTD.PlayModeTests
{
    /// <summary>Gold on the scene: the label, purchases, refusals, kills and sales (3b-AC3, AC4, AC5, AC7). The end of the run is in RunSceneTests.</summary>
    public class GoldSceneTests : SceneTestBase
    {
        private const string NotEnoughGoldText = "Not enough gold.";

        private int Cost => _input.Tower.ToStats().Cost;

        [UnityTest]
        public IEnumerator S3b_AC7_The_Label_Reads_Gold_N_With_The_Assets_Starting_Gold_At_The_Start()
        {
            yield return null;
            Assert.AreEqual(StartingGold(), Battle.Gold);
            Assert.AreEqual($"Gold {StartingGold()}", GoldLabel.text);
        }

        [UnityTest]
        public IEnumerator S3b_AC7_A_Purchase_Lowers_The_Label_By_The_Cost_And_A_Sale_Before_Firing_Restores_It()
        {
            yield return null;
            int start = Battle.Gold;

            ClickCell(2, 2);
            yield return null; // the label follows one frame after the gold
            Assert.AreEqual($"Gold {start - Cost}", GoldLabel.text);

            SellByClick(2, 2);
            yield return null;
            Assert.AreEqual($"Gold {start}", GoldLabel.text);
            Assert.AreEqual(0, FindByName("Towers").childCount);
        }

        [UnityTest]
        public IEnumerator S3b_AC3_When_The_Gold_Is_Short_The_Click_Shows_The_Message_Places_No_Tower_And_Leaves_The_Label_Alone()
        {
            int bought = 0;
            for (int x = 2; Battle.Gold >= Cost; x++, bought++) ClickCell(x, 2);
            Assert.Greater(bought, 0);
            yield return null;
            int gold = Battle.Gold;
            string label = GoldLabel.text;
            Assert.Less(gold, Cost);
            Assert.AreEqual($"Gold {gold}", label);
            Assert.IsFalse(RefusalShown(), "purchases that work show no refusal");

            ClickCell(2 + bought, 2);
            yield return null;

            Assert.IsTrue(RefusalShown());
            Assert.AreEqual(NotEnoughGoldText, RefusalText);
            Assert.IsFalse(MessageShown(), "4d-AC10: the message line stays quiet");
            Assert.IsNull(GameObject.Find($"Tower {2 + bought},2"));
            Assert.AreEqual(bought, Board.TowerCount);
            Assert.AreEqual(bought, FindByName("Towers").childCount);
            Assert.AreEqual(gold, Battle.Gold);
            Assert.AreEqual(label, GoldLabel.text);
        }

        [UnityTest]
        public IEnumerator S3b_AC3_Gold_Is_Checked_Before_The_Board_Even_On_A_Wall()
        {
            for (int x = 2; Battle.Gold >= Cost; x++) ClickCell(x, 2);
            ClickCell(5, 0); // a wall
            yield return null;
            Assert.AreEqual(NotEnoughGoldText, RefusalText);
        }

        [UnityTest]
        public IEnumerator S3b_AC4_AC7_The_First_Kill_Raises_The_Label_By_The_Reward_And_A_Sale_After_Firing_Gives_The_Rounded_Down_Percent()
        {
            var cell = new GridPos(1, 3);
            ClickCell(1, 3); // one tower cannot kill a 45 health enemy in passing: two cover it
            ClickCell(1, 5);
            int afterBuy = Battle.Gold;
            yield return TickUntil(() => Battle.Gold > afterBuy, 3000, 50);
            int reward = Battle.Gold - afterBuy;
            Assert.AreEqual(10, reward, "the document's reward");
            yield return null;
            Assert.AreEqual($"Gold {Battle.Gold}", GoldLabel.text);

            // The tower has fired by now: the sale gives cost * percent / 100, not the full cost.
            int before = Battle.Gold;
            SellByClick(1, 3);
            yield return null;
            int refund = Battle.Gold - before;
            Assert.AreEqual(Cost * 50 / 100, refund, "50 percent of the cost, rounded down");
            Assert.Less(refund, Cost);
            Assert.AreEqual($"Gold {Battle.Gold}", GoldLabel.text);
            Assert.IsFalse(Board.HasTower(cell));
            Assert.AreEqual(1, FindByName("Towers").childCount, "the other tower stays");
        }
    }
}
