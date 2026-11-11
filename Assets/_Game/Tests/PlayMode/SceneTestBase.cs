using System.Collections;
using ElementalTD.Core;
using ElementalTD.Data;
using ElementalTD.Gameplay;
using ElementalTD.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ElementalTD.PlayModeTests
{
    /// <summary>
    /// Loads Prototype.unity with the time scale at 0, so nothing moves until a test ticks the battle itself
    /// (or raises the time scale). Views render between the test's ticks.
    /// </summary>
    public abstract class SceneTestBase
    {
        protected MapDefinition _map;
        protected PrototypeBootstrap _bootstrap;
        protected TowerPlacementInput _input;
        protected Camera _camera;
        protected UIDocument _document;
        protected Label _label;
        private Mouse _addedMouse;

        // A screen corner is always off the grid: the frame leaves half a cell of padding on the fitting side.
        protected static readonly Vector2 OffGrid = Vector2.zero;

        protected Battle Battle => _bootstrap.Battle;
        protected Board Board => _bootstrap.Board;
        protected VisualElement Root => _document.rootVisualElement;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            _map = UnityEditor.AssetDatabase.LoadAssetAtPath<MapDefinition>("Assets/_Game/Data/Assets/Map_Prototype.asset");
#endif
            Assert.IsNotNull(_map, "Map_Prototype.asset not found");
            Time.timeScale = 0f; // nothing may move before the first assertions
            yield return SceneManager.LoadSceneAsync("Prototype", LoadSceneMode.Single);
            yield return null;
            yield return null;
            AcquireScene(disableInput: true);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            if (_camera != null) _camera.ResetAspect();
            if (_addedMouse != null)
            {
                InputSystem.RemoveDevice(_addedMouse);
                _addedMouse = null;
            }
            yield return null;
        }

        /// <summary>Finds the scene objects again; also used after a restart reloads the scene.</summary>
        protected void AcquireScene(bool disableInput)
        {
            _bootstrap = Object.FindAnyObjectByType<PrototypeBootstrap>();
            _input = Object.FindAnyObjectByType<TowerPlacementInput>();
            _camera = Object.FindAnyObjectByType<Camera>();
            _document = Object.FindAnyObjectByType<UIDocument>();
            Assert.IsNotNull(_bootstrap);
            Assert.IsNotNull(_input);
            Assert.IsNotNull(_camera);
            Assert.IsNotNull(_document);
            // A real mouse device would overwrite the hover and could click; the tests drive the public methods.
            if (disableInput) _input.enabled = false;
            _label = Root.Q<Label>("message");
            Assert.IsNotNull(_label);
        }

        protected static EnemyView[] Agents() => Object.FindObjectsByType<EnemyView>(FindObjectsInactive.Include);

        protected GridPos CellOf(Vector3 p) =>
            new GridPos(Mathf.FloorToInt(p.x / _map.CellSize), Mathf.FloorToInt(p.z / _map.CellSize));

        protected Vector2 ScreenOf(GridPos cell) => _camera.WorldToScreenPoint(_map.CellToWorld(cell));

        protected void ClickCell(int x, int y) => _input.Click(ScreenOf(new GridPos(x, y)));

        protected bool MessageShown() => _label.resolvedStyle.display == DisplayStyle.Flex;

        protected static Transform FindByName(string name)
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name == name) return t;
            return null;
        }

        /// <summary>The n-th pooled enemy view; it shows the n-th enemy on the board.</summary>
        protected static Transform EnemyViewAt(int index) => FindByName("Enemies").GetChild(index);

        protected static Transform FillOf(Transform enemyView)
        {
            foreach (Transform t in enemyView.GetComponentsInChildren<Transform>(true))
                if (t.name == "FillPivot") return t;
            return null;
        }

        /// <summary>Ticks the battle until the condition holds (checked after every tick), letting the views render every few ticks, then once more.</summary>
        protected IEnumerator TickUntil(System.Func<bool> condition, int maxTicks, int ticksPerFrame = 5)
        {
            int ticks = 0;
            while (!condition() && ticks < maxTicks)
            {
                Battle.Tick();
                ticks++;
                if (ticks % ticksPerFrame == 0) yield return null;
            }
            Assert.IsTrue(condition(), "the condition was never reached");
            yield return null;
        }

        protected IEnumerator TickUntilFirstEnemy() => TickUntil(() => Board.EnemyCount > 0, 1200, 50);

        /// <summary>
        /// The reference build order of docs/balance/0001-prototype-economy.md: after this many Tick() calls, click this cell
        /// (a purchase, or a sale when the cell holds a tower). The same table is in RealAssetRunTests.
        /// </summary>
        protected static readonly (int tick, int x, int y, bool buy)[] ReferenceOrder =
        {
            (0, 0, 8, true), (0, 0, 8, false), (0, 11, 2, true), (0, 11, 3, true),
            (1350, 14, 2, true), (2100, 13, 2, true), (2760, 12, 0, true), (3690, 10, 1, true), (3690, 4, 7, true),
            (4350, 3, 7, true), (4620, 4, 6, true), (5190, 1, 6, true), (5400, 11, 2, false), (5400, 6, 8, true),
            (5730, 0, 6, true), (6270, 2, 5, true), (6540, 1, 3, true), (7200, 3, 4, true), (7590, 1, 2, true),
            (7890, 3, 2, true), (8550, 6, 5, true), (8790, 7, 7, true), (9180, 7, 5, true), (9810, 3, 3, true),
        };

        private int _nextAction;

        /// <summary>Clicks the reference actions that are due after this many ticks, checking that each one did what it should.</summary>
        protected void RunReferenceActionsAt(int ticks)
        {
            int cost = _input.Tower.ToStats().Cost;
            while (_nextAction < ReferenceOrder.Length && ReferenceOrder[_nextAction].tick == ticks)
            {
                var (tick, x, y, buy) = ReferenceOrder[_nextAction++];
                int gold = Battle.Gold;
                ClickCell(x, y);
                if (buy)
                {
                    Assert.GreaterOrEqual(gold, cost, $"tick {tick} ({x},{y}): affordable");
                    Assert.IsTrue(Board.HasTower(new GridPos(x, y)), $"tick {tick}: bought ({x},{y})");
                    Assert.AreEqual(gold - cost, Battle.Gold, $"tick {tick}: paid for ({x},{y})");
                }
                else
                {
                    Assert.IsFalse(Board.HasTower(new GridPos(x, y)), $"tick {tick}: sold ({x},{y})");
                    Assert.Greater(Battle.Gold, gold, $"tick {tick}: refund for ({x},{y})");
                }
            }
        }

        protected Label GoldLabel => Root.Q<Label>("gold-label");

        protected int StartingGold()
        {
            int gold = -1;
#if UNITY_EDITOR
            gold = UnityEditor.AssetDatabase.LoadAssetAtPath<RunDefinition>("Assets/_Game/Data/Assets/Run_Prototype.asset").StartingGold;
#endif
            Assert.GreaterOrEqual(gold, 0, "Run_Prototype.asset not found");
            return gold;
        }

        /// <summary>A real left click through the Input System, which the UI Toolkit panel and the placement input both read.</summary>
        protected IEnumerator ClickScreen(Vector2 position)
        {
            if (Mouse.current == null) _addedMouse = InputSystem.AddDevice<Mouse>();
            // A batch run has no focused window; without this the Input System drops the events.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Mouse mouse = Mouse.current;
            if (!mouse.enabled) InputSystem.EnableDevice(mouse); // the unfocused batch window disabled it
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
        }
    }
}
