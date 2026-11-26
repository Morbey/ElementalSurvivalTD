using System;
using System.Collections.Generic;
using System.Text;
using ElementalTD.Data;
using NUnit.Framework;
using UnityEditor;
using static ElementalTD.Core.Tests.Reference5b;

namespace ElementalTD.Core.Tests
{
    /// <summary>
    /// Step 5b on the shipped assets: 5b-AC8's asset part (the pool equals the document), 5b-AC9 (the design target: the reference order with
    /// its picks, building nothing), and 5b-AC16's determinism and cost over the real run with picks. The numbers are those of
    /// docs/balance/0001-prototype-economy.md "Step 5b"; if the game disagrees, the game is wrong.
    /// </summary>
    public class ReferenceRunTests
    {
        // ---- 5b-AC8 The asset part

        // The pool in order, as the document's table states it: stat, element name (null: none), operation, value, title, attribute text.
        private static readonly (string asset, Stat stat, string element, Operation operation, float value, string title, string text)[] DocPool =
        {
            ("Perk_LightDamage", Stat.TowerDamage, "Light", Operation.Multiply, 1.25f, "{0} towers", "damage"),
            ("Perk_ShadowDamage", Stat.TowerDamage, "Shadow", Operation.Multiply, 1.25f, "{0} towers", "damage"),
            ("Perk_FireRate", Stat.TowerShotsPerSecond, null, Operation.Multiply, 1.1f, "All towers", "fire rate"),
            ("Perk_Range", Stat.TowerRange, null, Operation.Add, 1f, "All towers", "range"),
            ("Perk_GoldPerKill", Stat.GoldPerKill, null, Operation.Add, 3f, "Gold", "per kill"),
        };

        [Test]
        public void S5b_AC8_The_Run_Lists_The_Five_Perks_In_The_Documents_Order_With_The_Documents_Amounts_And_Words()
        {
            RunDefinition run = Run;
            Modifier[] pool = run.ToPerks();
            Assert.AreEqual(DocPool.Length, pool.Length, "five perks");
            for (int place = 0; place < DocPool.Length; place++)
            {
                var doc = DocPool[place];
                string where = $"place {place} ({doc.asset})";
                PerkDefinition perk = run.PerkAt(place);
                Assert.AreEqual(doc.asset, perk.name, where + ": the asset in that place");
                Assert.AreEqual((doc.stat, doc.operation, doc.value), (pool[place].Stat, pool[place].Operation, pool[place].Value), where);
                if (doc.element == null)
                {
                    Assert.IsTrue(pool[place].Element.IsNone, where + ": no element");
                    Assert.IsNull(perk.Element, where);
                }
                else
                {
                    Assert.AreEqual(doc.element, perk.Element.DisplayName, where + ": the element");
                    Assert.AreEqual(perk.Element.ToElement(), pool[place].Element, where);
                }
                Assert.AreEqual((doc.title, doc.text), (perk.Title, perk.AttributeText), where + ": the words of its card");
                Assert.AreSame(perk, LoadPerk(doc.asset), where + ": the asset on disk is the one listed");
            }
        }

        private static PerkDefinition LoadPerk(string name) => Load<PerkDefinition>(name + ".asset");

        [Test]
        public void S5b_AC8_There_Are_Exactly_Five_Perk_Assets_And_The_Pool_Needs_No_Other()
        {
            string[] guids = AssetDatabase.FindAssets("t:PerkDefinition", new[] { Dir.TrimEnd('/') });
            Assert.AreEqual(5, guids.Length, "no perk asset outside the pool");
            Assert.AreEqual(5, Run.ToPerks().Length);
        }

        [Test]
        public void S5b_AC8_The_Level_Table_Of_The_Run_Reaches_Seven_Choices_And_Keeps_Spare_Levels()
        {
            int[] table = Run.ToLevelTable();
            Assert.AreEqual(10, table.Length);
            int total = 0;
            foreach (int item in table) total += item;
            Assert.AreEqual(1190, total);
            Assert.AreEqual(30, table[0]);
        }

        // ---- 5b-AC9 The design target

        private static float ScanHits(Battle battle, ref int seen, float largest)
        {
            while (seen < battle.HitCount)
            {
                float amount = battle.HitAt(seen++).Amount;
                if (amount > largest) largest = amount;
            }
            return largest;
        }

        [Test]
        public void S5b_AC9_The_Reference_Order_With_Its_Picks_Meets_Every_Choice_On_Its_Tick_With_Its_Offer_And_Ends_As_Stated()
        {
            Battle battle = Build(out Board board);
            Progression p = battle.Progression;
            var taken = new List<int>();
            int previousXp = 0, previousLevel = 1, firstKill = -1, wonAt = -1, seenHits = 0;
            float largest = 0f;
            int opened = 0;
            Replay(battle, tick =>
            {
                if (p.Xp != previousXp)
                {
                    Assert.AreEqual(10, p.Xp - previousXp, $"tick {tick}: a kill is 10 XP, so no tick gains two levels");
                    if (firstKill < 0) firstKill = tick;
                }
                Assert.LessOrEqual(p.Level - previousLevel, 1, $"tick {tick}: at most one level on one tick");
                if (battle.AwaitingChoice) opened++;
                previousXp = p.Xp;
                previousLevel = p.Level;
                largest = ScanHits(battle, ref seenHits, largest);
                if (battle.Outcome == RunOutcome.Won) wonAt = tick;
            }, pickedPlaces: taken);

            Assert.AreEqual(Choices.Length, opened, "seven choices met");
            Assert.AreEqual(Choices.Length, p.ChoicesOpened);
            Assert.AreEqual(new[] { 1, 4, 3, 2, 1, 0, 0 }, taken.ToArray(), "the perks taken, by place (slots 0, 1, 0, 2, 0, 2, 2 of the offers)");
            Assert.AreEqual(new[] { 2, 2, 1, 1, 1 }, new[] { p.TimesTaken(0), p.TimesTaken(1), p.TimesTaken(2), p.TimesTaken(3), p.TimesTaken(4) },
                "both damages twice, the rest once");

            Assert.AreEqual(RunOutcome.Won, battle.Outcome);
            Assert.AreEqual(WonOnTick, wonAt, "won on the document's tick");
            Assert.AreEqual(WonOnTick, battle.TickCount);
            Assert.AreEqual((Lives, Gold), (battle.Lives, battle.Gold));
            Assert.AreEqual((Xp, Level), (p.Xp, p.Level));
            Assert.AreEqual(Kills, p.Xp / 10, "75 kills");
            Assert.AreEqual(FirstKillTick, firstKill, "the first kill");
            int light = 0, shadow = 0;
            for (int i = 0; i < board.TowerCount; i++)
                if (board.TowerAt(i).BaseStats.Element == Tower(true).Element) light++; else shadow++;
            Assert.AreEqual((Light, Shadow), (light, shadow), "32 towers, 16 and 16");
            Assert.AreEqual(32, board.TowerCount);
            Assert.AreEqual(LargestHit, largest, 0f, "the largest hit of the run");
            Assert.Less(largest, 100f, "no damage amount reaches 100");
        }

        [Test]
        public void S5b_AC9_No_Choice_Falls_On_A_Tick_With_An_Action_And_The_Document_Has_34_Actions_And_7_Choices()
        {
            Assert.AreEqual(34, Actions.Length);
            Assert.AreEqual(7, Choices.Length);
            foreach (var choice in Choices)
                foreach (var action in Actions)
                    Assert.AreNotEqual(choice.tick, action.tick, $"a choice and an action on tick {choice.tick}");
        }

        [Test]
        public void S5b_AC9_Building_Nothing_Loses_On_Tick_2931_With_175_Gold_No_XP_Level_1_And_No_Choice()
        {
            Battle battle = Build(out _);
            int ticks = 0;
            while (battle.Outcome == RunOutcome.Running && ticks < 6000)
            {
                Assert.IsFalse(battle.AwaitingChoice, $"tick {ticks}");
                battle.Tick();
                ticks++;
            }
            Assert.AreEqual(RunOutcome.Lost, battle.Outcome);
            Assert.AreEqual(NothingLostOnTick, ticks);
            Assert.AreEqual(NothingLostOnTick, battle.TickCount);
            Assert.AreEqual((NothingGold, 0, 1, 0, 0), (battle.Gold, battle.Progression.Xp, battle.Progression.Level, battle.Progression.ChoicesOpened, battle.Board.TowerCount));
        }

        [Test]
        public void S5b_AC9_Every_Action_After_Tick_0_Shifted_1_To_30_Ticks_Later_Is_Still_Accepted_And_The_Run_Still_Wins_With_10_Lives_And_189_Gold()
        {
            foreach (int shift in new[] { 1, 7, 15, 30 })
            {
                Battle battle = Build(out _);
                var taken = new List<int>();
                Replay(battle, shift: shift, checkGold: false, pickedPlaces: taken);
                Assert.AreEqual(RunOutcome.Won, battle.Outcome, $"shift {shift}");
                Assert.AreEqual((10, 189, 32), (battle.Lives, battle.Gold, battle.Board.TowerCount), $"shift {shift}");
                Assert.That(battle.TickCount, Is.InRange(10019, 10057), $"shift {shift}: the document's range for the end");
            }
        }

        [Test]
        public void S5b_AC9_The_Largest_Damage_Any_Run_Can_Show_Stays_Below_100_And_The_Smallest_Step_Forbids_Two_Levels_On_One_Tick()
        {
            Modifier[] pool = Run.ToPerks();
            float best = 0f;
            foreach (Modifier m in pool)
            {
                if (m.Stat != Stat.TowerDamage) continue;
                // A run holds at most seven perks (75 kills); the chart's x2 on the other element.
                best = Math.Max(best, 10f * m.Stacked(7) * 2f);
            }
            Assert.AreEqual(95.37f, best, 0.01f, "seven damage picks of one element against the other");
            Assert.Less(best, 100f);
            Assert.Greater(Run.ToLevelTable()[0], 10, "the first step is more than one kill's XP");
        }

        // ---- 5b-AC16 Determinism and cost over the real run

        private static string Snapshots(out List<string> log, out List<int> taken)
        {
            Battle battle = Build(out Board board);
            var lines = new List<string>();
            var picks = new List<int>();
            Replay(battle, tick =>
            {
                float sum = 0f, towers = 0f;
                for (int e = 0; e < board.EnemyCount; e++) sum += board.EnemyAt(e).X * 3f + board.EnemyAt(e).Y + board.EnemyAt(e).Health + board.EnemyAt(e).WaveIndex;
                for (int t = 0; t < board.TowerCount; t++) towers += board.TowerAt(t).Stats.Damage + board.TowerAt(t).Stats.ShotsPerSecond * 3f + board.TowerAt(t).Stats.Range * 7f;
                string offer = battle.AwaitingChoice ? string.Join(",", PerkKit.Offer(battle.Progression)) : "-";
                lines.Add($"{battle.Gold}|{battle.Lives}|{board.EnemyCount}|{battle.HitCount}|{battle.ProjectileCount}|{battle.SurvivedWaves}|{sum:R}|{towers:R}|" +
                    $"{battle.Progression.Xp}|{battle.Progression.Level}|{battle.Progression.ChoicesOpened}|{battle.Modifiers.Count}|{offer}");
            }, pickedPlaces: picks);
            log = lines;
            taken = picks;
            return string.Join("\n", lines);
        }

        [Test]
        public void S5b_AC16_Two_Replays_Of_The_Reference_Order_Are_Identical_Tick_For_Tick_Offers_Picks_And_Tower_Stats_Included()
        {
            string first = Snapshots(out List<string> a, out List<int> pickedA);
            string second = Snapshots(out List<string> b, out List<int> pickedB);
            Assert.AreEqual(WonOnTick, a.Count);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i], b[i], $"tick {i + 1}");
            Assert.AreEqual(pickedA, pickedB);
            Assert.AreEqual(first, second);
        }

        [Test]
        public void S5b_AC16_Another_Seed_Offers_Other_Cards_On_The_Same_Run()
        {
            Battle other = Build(out _, seed: 5);
            Replay(other, checkGold: false, picks: false, maxTicks: 1452);
            Assert.IsTrue(other.AwaitingChoice, "the first choice still opens after tick 1452: the kills do not depend on the seed");
            Assert.AreEqual(1452, other.TickCount);
            CollectionAssert.AreNotEqual(Choices[0].offer, PerkKit.Offer(other.Progression), "seed 5 does not offer the reference's first cards");
        }

        [Test]
        public void S5b_AC16_Ticking_The_Real_Run_With_Its_Picks_Allocates_Only_To_Grow_A_Pool_And_Never_On_The_Tick_That_Opens_A_Choice()
        {
            Battle battle = Build(out Board board);
            var allocating = new List<(int tick, int count)>();
            int ticksDone = 0, nextChoice = 0, nextAction = 0, peakEnemies = 0, peakShots = 0;
            while (battle.Outcome == RunOutcome.Running && ticksDone < 12000)
            {
                if (battle.AwaitingChoice) battle.PickPerk(Choices[nextChoice++].slot);
                while (nextAction < Actions.Length && Actions[nextAction].tick == ticksDone)
                {
                    var a = Actions[nextAction++];
                    if (a.buy) Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(new GridPos(a.x, a.y), Tower(a.light)));
                    else Assert.IsTrue(battle.TrySellTower(new GridPos(a.x, a.y)));
                }
                int count = TickAllocation.AllocationsOf(battle.Tick);
                ticksDone++;
                if (count != 0) allocating.Add((ticksDone, count));
                peakEnemies = Math.Max(peakEnemies, board.EnemyCount);
                peakShots = Math.Max(peakShots, battle.ProjectileCount);
            }

            Assert.AreEqual(WonOnTick, ticksDone);
            string report = $"5b-AC16: {allocating.Count} allocating ticks of {ticksDone}: {string.Join(", ", allocating)}";
            UnityEngine.Debug.Log(report);
            foreach ((int tick, int count) in allocating) Assert.LessOrEqual(count, 4, $"tick {tick}: one new pooled object. " + report);
            // Every one of the seven ticks that opens a choice (the draw included) allocates nothing.
            foreach (var choice in Choices)
                CollectionAssert.DoesNotContain(allocating.ConvertAll(x => x.tick), choice.tick, $"the tick that opens the choice on {choice.tick}. " + report);
            Assert.AreEqual(Choices.Length, nextChoice, "every choice was answered");
            Assert.LessOrEqual(allocating.Count, peakEnemies + 2 + peakShots + board.TowerCount, "growing the pools explains every allocation: " + report);
        }
    }
}
