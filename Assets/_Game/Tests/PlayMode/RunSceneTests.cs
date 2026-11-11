using System.Collections;
using System.Collections.Generic;
using ElementalTD.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ElementalTD.PlayModeTests
{
    /// <summary>The run on the scene (plan 0004): HUD, enemy views, end screen and Restart. Driven tick by tick unless said otherwise.</summary>
    public class RunSceneTests : SceneTestBase
    {
        private const int Waves = 10;

        private Label Text(string name) => Root.Q<Label>(name);
        private VisualElement End => Root.Q<VisualElement>("end-screen");
        private bool Shown(VisualElement e) => e.resolvedStyle.display == DisplayStyle.Flex;

        // What a run showed, collected frame by frame.
        private readonly HashSet<string> _seenCountdowns = new HashSet<string>();
        private readonly List<string> _seenLives = new List<string>();
        private readonly HashSet<Enemy> _everOnBoard = new HashSet<Enemy>();
        private readonly HashSet<Enemy> _currentlyOnBoard = new HashSet<Enemy>();
        private int _reusedObjects, _maxEnemies, _maxProjectiles;
        private List<Transform> _enemyViews, _projectileViews;

        // ---- Checks that must hold at the start of every frame, before the test ticks again

        private void CaptureViews()
        {
            _enemyViews = new List<Transform>();
            foreach (Transform t in FindByName("Enemies")) _enemyViews.Add(t);
            _projectileViews = new List<Transform>();
            foreach (Transform t in FindByName("Projectiles")) _projectileViews.Add(t);
            Assert.AreEqual(32, _enemyViews.Count, "enemy pool pre-warmed to 32");
            Assert.AreEqual(16, _projectileViews.Count, "projectile pool pre-warmed to 16");
        }

        /// <summary>HUD, enemy views and projectile views all agree with the battle; the pools did not change.</summary>
        private void AssertFrame()
        {
            WaveSchedule waves = Battle.Waves;
            Assert.AreEqual($"Wave {waves.StartedWaves}/{Waves}", Text("wave-label").text);
            Assert.AreEqual($"Lives {Battle.Lives}", Text("lives-label").text);
            Assert.AreEqual($"Gold {Battle.Gold}", GoldLabel.text);
            Label countdown = Text("next-wave-label");
            if (waves.SecondsToNextWave > 0f)
            {
                Assert.IsTrue(Shown(countdown), "countdown shows while a delay runs");
                Assert.AreEqual($"Next wave in {Mathf.CeilToInt(waves.SecondsToNextWave)} s", countdown.text);
                _seenCountdowns.Add(countdown.text);
            }
            else
            {
                Assert.IsFalse(Shown(countdown), $"countdown hidden while a wave spawns and from the last wave on (waves started {waves.StartedWaves})");
            }
            if (_seenLives.Count == 0 || _seenLives[_seenLives.Count - 1] != Text("lives-label").text) _seenLives.Add(Text("lives-label").text);

            Assert.AreEqual(Battle.Outcome != RunOutcome.Running, Shown(End));

            int count = Board.EnemyCount;
            _maxEnemies = Mathf.Max(_maxEnemies, count);
            Assert.LessOrEqual(count, _enemyViews.Count, "the enemy pool did not need to grow");
            _currentlyOnBoard.Clear();
            for (int i = 0; i < _enemyViews.Count; i++)
            {
                Transform view = _enemyViews[i];
                Assert.IsTrue(view != null, "an enemy view was destroyed");
                Assert.AreEqual(i < count, view.gameObject.activeSelf, $"enemy view {i} with {count} enemies on the board");
                if (i >= count) continue;
                Enemy enemy = Board.EnemyAt(i);
                _currentlyOnBoard.Add(enemy);
                Vector3 expected = _map.CellToWorld(Mathf.Lerp(enemy.PreviousX, enemy.X, _bootstrap.TickAlpha),
                    Mathf.Lerp(enemy.PreviousY, enemy.Y, _bootstrap.TickAlpha));
                Assert.Less(Vector3.Distance(expected, view.position), 0.01f, $"enemy view {i} position");
                Assert.AreEqual(enemy.Health / enemy.MaxHealth, FillOf(view).localScale.x, 1e-4f, $"enemy view {i} fill");
            }
            foreach (Enemy enemy in _currentlyOnBoard)
                if (!_everOnBoard.Add(enemy) && !_previouslyShown.Contains(enemy)) _reusedObjects++;
            _previouslyShown.Clear();
            foreach (Enemy enemy in _currentlyOnBoard) _previouslyShown.Add(enemy);

            int shots = Battle.ProjectileCount;
            _maxProjectiles = Mathf.Max(_maxProjectiles, shots);
            Assert.LessOrEqual(shots, _projectileViews.Count, "the projectile pool did not need to grow");
            for (int i = 0; i < _projectileViews.Count; i++)
            {
                Assert.IsTrue(_projectileViews[i] != null, "a projectile view was destroyed");
                Assert.AreEqual(i < shots, _projectileViews[i].gameObject.activeSelf, $"projectile view {i}");
            }
        }

        private readonly HashSet<Enemy> _previouslyShown = new HashSet<Enemy>();

        /// <summary>Plays the whole run by ticking (some ticks per frame), checking every frame, until the outcome is decided.</summary>
        private IEnumerator PlayToTheEnd(int ticksPerFrame, int maxTicks, bool referenceOrder = false)
        {
            CaptureViews();
            yield return null;
            AssertFrame();
            int ticks = 0;
            while (Battle.Outcome == RunOutcome.Running && ticks < maxTicks)
            {
                for (int i = 0; i < ticksPerFrame && Battle.Outcome == RunOutcome.Running; i++, ticks++)
                {
                    if (referenceOrder) RunReferenceActionsAt(ticks);
                    Battle.Tick();
                }
                yield return null;
                AssertFrame();
            }
            Assert.AreNotEqual(RunOutcome.Running, Battle.Outcome, "the run never ended");
        }

        // ---- 3a-AC8, AC9: the start

        [UnityTest]
        public IEnumerator S3a_AC8_AC9_At_Start_No_Enemy_View_Is_Active_32_Are_Pooled_And_The_Hud_Reads_The_Start_Values()
        {
            yield return null;
            CaptureViews();
            Assert.AreEqual(0, Board.EnemyCount);
            foreach (Transform view in _enemyViews) Assert.IsFalse(view.gameObject.activeSelf);
            Assert.AreEqual(32, CountNamed("Enemy"), "32 objects named Enemy");
            Assert.AreEqual(16, CountNamed("Projectile"), "16 objects named Projectile");

            Assert.AreEqual("Wave 0/10", Text("wave-label").text);
            Assert.AreEqual("Lives 10", Text("lives-label").text);
            Assert.AreEqual($"Gold {StartingGold()}", GoldLabel.text, "3b-AC7: the start shows the asset's starting gold");
            Assert.AreEqual("Next wave in 15 s", Text("next-wave-label").text);
            Assert.IsTrue(Shown(Text("next-wave-label")));
            Assert.IsFalse(Shown(End), "the end screen starts hidden");
        }

        private static int CountNamed(string name)
        {
            int count = 0;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name == name) count++;
            return count;
        }

        // ---- 3a-AC3, AC6, AC8, AC9: a run with no tower is lost

        [UnityTest]
        public IEnumerator S3a_AC8_AC9_A_Run_With_No_Tower_Shows_Wave_Lives_And_Countdown_As_They_Change_And_Ends_In_Defeat()
        {
            yield return PlayToTheEnd(10, 6000);

            Assert.AreEqual(RunOutcome.Lost, Battle.Outcome);
            Assert.That(Battle.Waves.StartedWaves, Is.GreaterThanOrEqualTo(3));
            CollectionAssert.Contains(_seenCountdowns, "Next wave in 15 s");
            CollectionAssert.Contains(_seenCountdowns, "Next wave in 8 s", "right after the last enemy of a wave");
            for (int lives = 10; lives >= 0; lives--) CollectionAssert.Contains(_seenLives, $"Lives {lives}", "lives drop one at a time");
            Assert.AreEqual("Defeat", Text("end-title").text);
            Assert.IsTrue(Shown(End));
            Assert.Greater(_maxEnemies, 1, "several enemies were drawn at once");
            Assert.AreEqual(0, Object.FindObjectsByType<Collider>(FindObjectsInactive.Include).Length);
        }

        // ---- 3a-AC5, AC6, AC8, AC9, AC13: a run with the eight planned towers is won

        [UnityTest]
        public IEnumerator S3b_AC9_AC8_A_Run_Of_The_Reference_Build_Order_Wins_Every_Frame_Is_Consistent_And_No_View_Is_Created_Or_Destroyed()
        {
            yield return PlayToTheEnd(3, 12000, referenceOrder: true);

            Assert.AreEqual(RunOutcome.Won, Battle.Outcome);
            Assert.AreEqual(10, Battle.Lives);
            Assert.AreEqual(50, Battle.Gold);
            Assert.AreEqual("Gold 50", GoldLabel.text);
            Assert.AreEqual("Wave 10/10", Text("wave-label").text);
            Assert.IsFalse(Shown(Text("next-wave-label")), "hidden from wave 10 on");
            Assert.AreEqual("Victory", Text("end-title").text);
            Assert.IsTrue(Shown(End));
            Assert.AreEqual(0, Battle.ProjectileCount);
            foreach (Transform view in _projectileViews) Assert.IsFalse(view.gameObject.activeSelf, "no projectile view is active after a win");
            Assert.Greater(_reusedObjects, 0, "an enemy object was reused by a later enemy, and its view stayed right");
            Assert.Greater(_maxProjectiles, 0);
            Assert.AreEqual(32, CountNamed("Enemy"));
            Assert.AreEqual(16, CountNamed("Projectile"));
            Assert.AreEqual(0, Object.FindObjectsByType<Collider>(FindObjectsInactive.Include).Length);

            // 3b-AC6: the run is over, so a click on a tower and a click on a free cell change neither the towers nor the gold.
            int towers = Board.TowerCount;
            ClickCell(14, 2); // a tower of the order
            ClickCell(2, 2);
            _input.Hover(ScreenOf(new GridPos(2, 2)));
            yield return null;
            yield return null;
            Assert.IsTrue(Board.HasTower(new GridPos(14, 2)));
            Assert.AreEqual(towers, Board.TowerCount);
            Assert.AreEqual(towers, FindByName("Towers").childCount);
            Assert.AreEqual(50, Battle.Gold);
            Assert.AreEqual("Gold 50", GoldLabel.text);
            Assert.IsFalse(_input.HasHoverCell);
            Assert.IsFalse(FindByName("Hover").gameObject.activeSelf);
        }

        // ---- 3a-AC5: the tick that ends the run freezes everything

        [UnityTest]
        public IEnumerator S3a_AC5_AC6_On_The_Tick_That_Loses_The_Run_The_End_Screen_Shows_And_Nothing_Moves_Over_The_Next_Frames()
        {
            // Tick by hand to the last life, then let the bootstrap itself play the final seconds in real time.
            yield return TickUntil(() => Battle.Lives == 1, 6000, 50);
            Assert.AreEqual(RunOutcome.Running, Battle.Outcome);
            Time.timeScale = 50f;
            int frames = 0;
            while (Battle.Outcome == RunOutcome.Running && frames++ < 20000) yield return null;
            Assert.AreEqual(RunOutcome.Lost, Battle.Outcome);

            yield return null; // the frame after the ending
            Assert.IsTrue(Shown(End));
            Assert.AreEqual("Defeat", Text("end-title").text);
            Assert.Greater(Board.EnemyCount, 0, "enemies are still on the frozen board");

            float alpha = _bootstrap.TickAlpha;
            var positions = new List<Vector3>();
            int active = 0;
            foreach (Transform view in FindByName("Enemies"))
                if (view.gameObject.activeSelf) { positions.Add(view.position); active++; }
            Assert.AreEqual(Board.EnemyCount, active);

            for (int frame = 0; frame < 30; frame++)
            {
                yield return null;
                Assert.AreEqual(alpha, _bootstrap.TickAlpha, "TickAlpha is frozen");
                int index = 0;
                foreach (Transform view in FindByName("Enemies"))
                    if (view.gameObject.activeSelf) Assert.AreEqual(positions[index++], view.position, "no enemy view moves");
            }

            // A free cell neither hovers nor takes a click.
            Time.timeScale = 0f;
            _input.Hover(ScreenOf(new GridPos(2, 2)));
            ClickCell(2, 2);
            yield return null;
            Assert.IsFalse(_input.HasHoverCell);
            Assert.IsFalse(FindByName("Hover").gameObject.activeSelf);
            Assert.AreEqual(0, Board.TowerCount);
            Assert.IsFalse(MessageShown());
        }

        // ---- 3b-AC6: after a defeat, a click on a tower and on a free cell change nothing

        [UnityTest]
        public IEnumerator S3b_AC6_After_A_Defeat_A_Click_On_A_Tower_Or_A_Free_Cell_Changes_Neither_The_Towers_Nor_The_Gold()
        {
            ClickCell(0, 0); // far from the route: the run is lost
            ClickCell(1, 0);
            Assert.AreEqual(0, Battle.Gold);
            yield return TickUntil(() => Battle.Outcome == RunOutcome.Lost, 6000, 50);
            yield return null;
            Assert.AreEqual(0, Battle.Gold);

            ClickCell(0, 0); // a tower
            ClickCell(4, 0); // a free cell
            yield return null;
            yield return null;

            Assert.AreEqual(2, Board.TowerCount);
            Assert.AreEqual(2, FindByName("Towers").childCount);
            Assert.IsTrue(Board.HasTower(new GridPos(0, 0)));
            Assert.AreEqual(0, Battle.Gold);
            Assert.AreEqual("Gold 0", GoldLabel.text);
            Assert.IsFalse(MessageShown());
        }

        // ---- 3a-AC7: Restart through a real click, twice

        private Vector2 RestartButtonOnScreen()
        {
            Button button = Root.Q<Button>("restart-button");
            Vector2 centre = button.worldBound.center; // panel coordinates, origin top-left
            float scale = Screen.width / Root.worldBound.width;
            return new Vector2(centre.x * scale, Screen.height - centre.y * scale);
        }

        [UnityTest]
        public IEnumerator S3a_AC7_A_Click_On_Restart_Starts_A_Fresh_Run_Each_Time_And_Places_No_Tower_Under_The_Button()
        {
            for (int round = 1; round <= 2; round++)
            {
                // Lose the run; in round 2 with one tower, so a leftover would show.
                if (round == 2) ClickCell(2, 2);
                yield return TickUntil(() => Battle.Outcome == RunOutcome.Lost, 6000, 50);
                Assert.IsTrue(Shown(End), $"round {round}: end screen");

                PrototypeBootstrapMarker old = new PrototypeBootstrapMarker(_bootstrap);
                _input.enabled = true; // a stray click must be able to place a tower
                for (int i = 0; i < 3; i++) yield return null; // the panel lays the end screen out
                // The mouse goes down and up over the button: a placement under it must not happen (the run is over).
                yield return ClickScreen(RestartButtonOnScreen());
                Assert.AreEqual(0, Board.TowerCount - (round == 2 ? 1 : 0), $"round {round}: the click on the board placed nothing");
                // A batch run has no focused window, so the panel ignores the mouse; the button gets the UI Toolkit event
                // its own click handler listens to. A real mouse click on the button is checked by hand and in the browser.
                Button button = Root.Q<Button>("restart-button");
                using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
                {
                    submit.target = button;
                    button.SendEvent(submit);
                }

                int guard = 0;
                while (old.StillAlive && guard++ < 120) yield return null;
                Assert.IsFalse(old.StillAlive, $"round {round}: the scene never reloaded");
                yield return null;
                AcquireScene(disableInput: false);
                for (int i = 0; i < 3; i++) yield return null;

                Assert.AreEqual(0, Board.TowerCount, $"round {round}: no tower on the board");
                Assert.AreEqual(0, CountNamed("Tower 2,2") + CountTowerObjects(), $"round {round}: no tower object");
                Assert.AreEqual(0, Board.EnemyCount);
                Assert.AreEqual(0, Battle.ProjectileCount);
                Assert.AreEqual(RunOutcome.Running, Battle.Outcome);
                Assert.AreEqual(10, Battle.Lives);
                Assert.AreEqual("Wave 0/10", Text("wave-label").text);
                Assert.AreEqual("Lives 10", Text("lives-label").text);
                Assert.AreEqual($"Gold {StartingGold()}", GoldLabel.text, $"round {round}: the starting gold again");
                Assert.AreEqual(StartingGold(), Battle.Gold);
                Assert.AreEqual("Next wave in 15 s", Text("next-wave-label").text);
                Assert.IsFalse(Shown(End), $"round {round}: end screen hidden");
                _input.enabled = false;
            }
        }

        private static int CountTowerObjects()
        {
            int count = 0;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name.StartsWith("Tower ")) count++;
            return count;
        }

        /// <summary>Remembers the bootstrap of the old run; Unity's null check says when its scene is gone.</summary>
        private sealed class PrototypeBootstrapMarker
        {
            private readonly Object _bootstrap;
            public PrototypeBootstrapMarker(Object bootstrap) { _bootstrap = bootstrap; }
            public bool StillAlive => _bootstrap != null;
        }
    }
}
