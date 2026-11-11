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
            _label = Root.Q<Label>("placement-message");
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

        /// <summary>The eight towers of the 3a-AC13 plan, placed through the input before the first wave.</summary>
        protected void PlaceEightTowers()
        {
            foreach (var (x, y) in new[] { (3, 5), (6, 6), (8, 6), (8, 3), (11, 2), (13, 2), (14, 3), (3, 3) })
            {
                ClickCell(x, y);
                Assert.IsTrue(Board.HasTower(new GridPos(x, y)), $"tower on ({x},{y})");
            }
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
