using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ElementalTD.Core.Tests
{
    /// <summary>Shared set-up of the step 5b tests: the plan's pins are built here once, in code, never from the assets.</summary>
    internal static class PerkKit
    {
        public static readonly Element Light = new Element(1);
        public static readonly Element Shadow = new Element(2);

        public static ElementChart Chart()
        {
            var chart = new ElementChart(2);
            chart.Set(Light, Shadow, 2f);
            chart.Set(Shadow, Light, 2f);
            return chart;
        }

        public static Board PrototypeBoard() => new Board(new GridMap(16, 9, TestMaps.PrototypeBlocked()), TestMaps.Spawn, TestMaps.Exit);

        public static Board OpenBoard() => new Board(new GridMap(16, 9, new List<GridPos>()), TestMaps.Spawn, TestMaps.Exit);

        /// <summary>Cost 40, range 2.5, damage 10, 1.5 shots/s, projectile 10: the balance document's tower.</summary>
        public static TowerStats Tower(Element element) => new TowerStats(2.5f, 10f, 1.5f, 10f, 40, element);

        public static Modifier Mod(Stat stat, Operation operation, float value, Element element = default) =>
            new Modifier(stat, operation, value, element);

        /// <summary>The modifier pin's pool.</summary>
        public static Modifier[] PinPool() => new[]
        {
            Mod(Stat.TowerDamage, Operation.Multiply, 1.25f, Light),
            Mod(Stat.TowerDamage, Operation.Multiply, 1.25f, Shadow),
            Mod(Stat.TowerShotsPerSecond, Operation.Multiply, 1.15f),
            Mod(Stat.TowerRange, Operation.Add, 0.5f),
            Mod(Stat.GoldPerKill, Operation.Add, 2f),
        };

        /// <summary>The pause pin's pool of three.</summary>
        public static Modifier[] PausePool() => new[]
        {
            Mod(Stat.TowerDamage, Operation.Multiply, 1.25f, Light),
            Mod(Stat.TowerRange, Operation.Add, 0.5f),
            Mod(Stat.GoldPerKill, Operation.Add, 2f),
        };

        public static EnemyStats ShadowEnemy(float speed = 2f, float health = 45f, int gold = 10, int xp = 10) =>
            new EnemyStats(speed, health, gold, Shadow, xp);

        /// <summary>The pause pin's run: seed 2, level table 10, 10, 10, a Light tower bought on (2,3) before the first tick.</summary>
        public static Battle PausePin(out Board board, Modifier[] pool = null, uint seed = 2)
        {
            board = PrototypeBoard();
            EnemyStats enemy = ShadowEnemy();
            var waves = new[]
            {
                new Wave(enemy, 1, 1f, 0f, 7),
                new Wave(enemy, 2, 1f, 2f, 9),
            };
            var battle = new Battle(board, waves, 10, 40, 50, Chart(), new Progression(new[] { 10, 10, 10 }, pool ?? PausePool(), seed));
            Assert.AreEqual(PlacementResult.Placed, battle.TryBuyTower(new GridPos(2, 3), Tower(Light)));
            return battle;
        }

        /// <summary>Picks the slot that offers the perk; the offer must hold it.</summary>
        public static void PickPerkByPlace(Battle battle, int perk)
        {
            Assert.IsTrue(battle.AwaitingChoice, $"a choice is open to pick perk {perk}");
            for (int slot = 0; slot < Progression.OfferSize; slot++)
            {
                if (battle.Progression.OfferAt(slot) != perk) continue;
                battle.PickPerk(slot);
                return;
            }
            Assert.Fail($"perk {perk} is not on the offer");
        }

        public static int[] Offer(Progression progression) =>
            new[] { progression.OfferAt(0), progression.OfferAt(1), progression.OfferAt(2) };

        /// <summary>Opens the first choice of a battle by adding the XP of its level and ticking once.</summary>
        public static void OpenChoiceByXp(Battle battle, int xp)
        {
            battle.Progression.AddXp(xp);
            battle.Tick();
            Assert.IsTrue(battle.AwaitingChoice, "the tick opened a choice");
        }

        public static int Bits(float value) => BitConverter.SingleToInt32Bits(value);
    }
}
