using System;
using System.Collections.Generic;
using ElementalTD.Data;
using NUnit.Framework;
using UnityEditor;

namespace ElementalTD.Core.Tests
{
    /// <summary>
    /// The reference seed and order of docs/balance/0001-prototype-economy.md ("Reference seed and order", step 5b), on the shipped assets.
    /// The numbers are the document's, typed here from it; nothing is read from what the game produced. The scene tests keep their own copy
    /// of the same table (SceneTestBase), as the two test assemblies share no code.
    /// </summary>
    internal static class Reference5b
    {
        public const string Dir = "Assets/_Game/Data/Assets/";
        public const uint Seed = 4;

        /// <summary>Tick() calls done before the action, cell, tower (true: Light), buy or sell, the gold the document states after it.</summary>
        public static readonly (int tick, int x, int y, bool light, bool buy, int goldAfter)[] Actions =
        {
            (0, 0, 8, true, true, 75), (0, 0, 8, true, false, 115), (0, 11, 2, true, true, 75), (0, 11, 3, true, true, 35),
            (1290, 14, 2, false, true, 5), (1470, 13, 2, false, true, 25), (2130, 12, 0, false, true, 5), (2220, 10, 1, false, true, 5),
            (2790, 4, 7, true, true, 4), (3150, 4, 6, false, true, 28), (3390, 3, 7, true, true, 1), (3600, 6, 8, false, true, 0),
            (4050, 0, 6, true, true, 42), (4110, 1, 6, true, true, 15), (4230, 2, 5, true, true, 1), (4260, 3, 4, false, true, 9),
            (4860, 1, 2, true, true, 8), (5190, 1, 3, true, true, 7), (5370, 3, 3, true, true, 33), (5460, 3, 2, false, true, 6),
            (5670, 6, 5, false, true, 5), (5880, 7, 7, false, true, 4), (6060, 7, 5, false, true, 35), (6540, 8, 3, true, true, 8),
            (6900, 9, 2, false, true, 7), (7230, 8, 5, true, true, 6), (7470, 7, 3, false, true, 55), (7470, 7, 1, true, true, 15),
            (7800, 6, 1, false, true, 1), (8250, 9, 1, true, true, 0), (8760, 0, 3, true, true, 12), (8880, 1, 5, false, true, 66),
            (8880, 14, 1, true, true, 26), (9330, 3, 1, false, true, 25),
        };

        /// <summary>The choice opens after this many Tick() calls, at this level, with this offer (slots 0 to 2), and the slot picked.</summary>
        public static readonly (int tick, int level, int[] offer, int slot)[] Choices =
        {
            (1452, 2, new[] { 1, 0, 2 }, 0),
            (2209, 3, new[] { 1, 4, 2 }, 1),
            (3369, 4, new[] { 3, 4, 2 }, 0),
            (4175, 5, new[] { 4, 1, 2 }, 2),
            (5446, 6, new[] { 1, 3, 2 }, 0),
            (6980, 7, new[] { 2, 1, 0 }, 2),
            (8808, 8, new[] { 2, 3, 0 }, 2),
        };

        public const int WonOnTick = 10026, Lives = 10, Gold = 189, Kills = 75, Xp = 750, Level = 8, FirstKillTick = 1288, Light = 16, Shadow = 16;
        public const int NothingLostOnTick = 2931, NothingGold = 175;
        public const float LargestHit = 31.25f;

        public static T Load<T>(string name) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Dir + name);
            Assert.IsNotNull(asset, name);
            return asset;
        }

        public static RunDefinition Run => Load<RunDefinition>("Run_Prototype.asset");
        public static ElementChart Chart() => Load<ElementChartDefinition>("ElementChart.asset").ToChart();
        public static TowerStats Tower(bool light) => Load<TowerDefinition>(light ? "Tower_Light.asset" : "Tower_Shadow.asset").ToStats();

        /// <summary>The real run: the shipped waves, the table and the pool (<paramref name="pool"/>), the seed (<paramref name="seed"/>).</summary>
        public static Battle Build(out Board board, bool pool = true, bool table = true, uint seed = Seed)
        {
            var map = Load<MapDefinition>("Map_Prototype.asset");
            RunDefinition run = Run;
            board = new Board(map.ToGridMap(), map.Spawn, map.Exit);
            Progression progression = table ? new Progression(run.ToLevelTable(), pool ? run.ToPerks() : null, seed) : null;
            return new Battle(board, run.ToWaves(), run.Lives, run.StartingGold, run.SellRefundPercent, Chart(), progression);
        }

        /// <summary>
        /// Replays the order as the document does: an action runs after its number of Tick() calls, before the next tick; a choice opened by a tick
        /// is answered before any action at that count (rule 24), each checked against the document's tick, level and offer.
        /// <paramref name="afterTick"/> gets the number of ticks done, with the choice that tick opened still open.
        /// </summary>
        public static void Replay(Battle battle, Action<int> afterTick = null, Action<int> beforeTick = null, bool build = true, bool picks = true,
            bool checkGold = true, int maxTicks = 12000, List<int> pickedPlaces = null, int shift = 0)
        {
            int nextAction = 0, nextChoice = 0, ticks = 0;
            while (battle.Outcome == RunOutcome.Running && ticks < maxTicks)
            {
                if (battle.AwaitingChoice)
                {
                    Assert.IsTrue(picks, $"tick {ticks}: a choice opened and this replay answers none");
                    Assert.Less(nextChoice, Choices.Length, $"tick {ticks}: a choice the document does not have");
                    var row = Choices[nextChoice++];
                    if (shift == 0) Assert.AreEqual(row.tick, ticks, $"choice {nextChoice}: the tick it opens on");
                    Assert.AreEqual(row.level, battle.Progression.ChoiceLevel, $"choice {nextChoice}: its level");
                    CollectionAssert.AreEqual(row.offer, PerkKit.Offer(battle.Progression), $"choice {nextChoice} on tick {ticks}: its offer");
                    pickedPlaces?.Add(battle.Progression.OfferAt(row.slot));
                    battle.PickPerk(row.slot);
                    Assert.IsFalse(battle.AwaitingChoice, "one level at a time: no choice in a row");
                }

                while (build && nextAction < Actions.Length && Actions[nextAction].tick + (Actions[nextAction].tick > 0 ? shift : 0) == ticks)
                {
                    var a = Actions[nextAction++];
                    var cell = new GridPos(a.x, a.y);
                    string where = $"tick {a.tick} ({a.x},{a.y}) {(a.buy ? "buy" : "sell")}";
                    if (checkGold)
                    {
                        if (a.buy) Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(cell, Tower(a.light)), where);
                        else Assert.IsTrue(battle.TrySellTower(cell), where);
                        Assert.AreEqual(a.goldAfter, battle.Gold, where + ": gold after");
                    }
                    else if (a.buy) battle.TryBuyTower(cell, Tower(a.light));
                    else battle.TrySellTower(cell);
                }

                beforeTick?.Invoke(ticks);
                battle.Tick();
                ticks++;
                afterTick?.Invoke(ticks);
            }
        }
    }
}
