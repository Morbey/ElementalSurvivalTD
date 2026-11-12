using System.Collections;
using System.Collections.Generic;
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
        private Keyboard _addedKeyboard;

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
            if (_addedKeyboard != null)
            {
                InputSystem.RemoveDevice(_addedKeyboard);
                _addedKeyboard = null;
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

        /// <summary>The line is shown from the frame after a message, by the class the style sheet fades on.</summary>
        protected bool MessageShown() => _label.ClassListContains("message--shown");

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
        /// The reference build order of docs/balance/0001-prototype-economy.md: after this many Tick() calls, select the tower
        /// (true: the first, Light; false: the second, Shadow) and click the cell (a purchase, or a sale when the cell holds a tower),
        /// then the gold the document states. The same table is in RealAssetRunTests.
        /// </summary>
        protected static readonly (int tick, int x, int y, bool light, bool buy, int goldAfter)[] ReferenceOrder =
        {
            (0, 0, 8, true, true, 75),
            (0, 0, 8, true, false, 115),
            (0, 11, 2, true, true, 75),
            (0, 11, 3, true, true, 35),
            (1290, 14, 2, false, true, 5),
            (1470, 13, 2, false, true, 25),
            (2130, 12, 0, false, true, 5),
            (2220, 10, 1, true, true, 5),
            (3360, 4, 6, false, true, 50),
            (3390, 4, 7, false, true, 20),
            (3630, 3, 7, false, true, 0),
            (3780, 6, 8, true, true, 10),
            (3960, 0, 6, true, true, 0),
            (4290, 1, 6, true, true, 35),
            (4650, 3, 4, true, true, 5),
            (5190, 2, 5, true, true, 5),
            (5970, 1, 3, false, true, 85),
            (5970, 1, 2, false, true, 45),
            (6030, 3, 3, false, true, 15),
            (6360, 3, 2, false, true, 50),
            (6360, 6, 5, false, true, 10),
            (6360, 11, 2, true, false, 30),
            (6360, 11, 3, true, false, 50),
            (6360, 7, 7, false, true, 10),
            (6810, 7, 5, true, true, 0),
            (7200, 8, 3, true, true, 0),
            (7410, 9, 2, true, true, 40),
            (7410, 8, 5, true, true, 0),
            (8340, 7, 3, true, true, 0),
            (8700, 7, 1, true, true, 0),
            (8940, 6, 1, true, true, 55),
            (8940, 9, 1, true, true, 15),
            (9510, 0, 3, true, true, 5),
            (9900, 3, 1, true, true, 5),
        };

        private int _nextAction;

        /// <summary>Clicks the reference actions that are due after this many ticks, checking that each one did what it should.</summary>
        protected void RunReferenceActionsAt(int ticks)
        {
            while (_nextAction < ReferenceOrder.Length && ReferenceOrder[_nextAction].tick == ticks)
            {
                var (tick, x, y, light, buy, goldAfter) = ReferenceOrder[_nextAction++];
                int gold = Battle.Gold;
                if (buy) _input.Select(light ? 0 : 1);
                int cost = _input.Tower.ToStats().Cost;
                ClickCell(x, y);
                string where = $"tick {tick} ({x},{y}) {(buy ? "buy" : "sell")}";
                if (buy)
                {
                    Assert.GreaterOrEqual(gold, cost, $"{where}: affordable");
                    Assert.IsTrue(Board.HasTower(new GridPos(x, y)), $"{where}: bought");
                    Assert.AreEqual(ElementOfTower(light ? 0 : 1), Board.TowerOn(new GridPos(x, y)).Stats.Element, $"{where}: the element of the tower named");
                }
                else
                {
                    Assert.IsFalse(Board.HasTower(new GridPos(x, y)), $"{where}: sold");
                }
                Assert.AreEqual(goldAfter, Battle.Gold, $"{where}: gold after");
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

        // ---- Elements, buttons and keys (4b)

        private ElementPalette _palette;

        /// <summary>The palette asset the scene's views hold.</summary>
        protected ElementPalette Palette
        {
            get
            {
#if UNITY_EDITOR
                if (_palette == null) _palette = UnityEditor.AssetDatabase.LoadAssetAtPath<ElementPalette>("Assets/_Game/Presentation/ElementPalette.asset");
#endif
                Assert.IsNotNull(_palette, "ElementPalette.asset not found");
                return _palette;
            }
        }

        protected ElementPalette.Look LookOf(Element element)
        {
            Assert.IsTrue(Palette.TryGetLook(element, out ElementPalette.Look look), $"the palette has no look for {element}");
            return look;
        }

        /// <summary>An element name as the HUD writes it: rich text in the element's UI colour.</summary>
        protected string Markup(Element element)
        {
            ElementPalette.Look look = LookOf(element);
            return $"<color=#{ColorUtility.ToHtmlStringRGB(look.UiColor)}>{look.Element.DisplayName}</color>";
        }

        /// <summary>The names of the distinct elements of a wave's groups, in group order, joined by " + ".</summary>
        protected string WaveElements(Wave wave)
        {
            var seen = new List<Element>();
            for (int g = 0; g < wave.GroupCount; g++)
                if (!seen.Contains(wave.GroupAt(g).Enemy.Element)) seen.Add(wave.GroupAt(g).Enemy.Element);
            var names = new List<string>();
            foreach (Element e in seen) names.Add(Markup(e));
            return string.Join(" + ", names);
        }

        protected Element ElementOfTower(int index) => _input.TowerAt(index).ToStats().Element;

        protected Button[] TowerButtons()
        {
            var buttons = new List<Button>();
            Root.Q<VisualElement>("tower-bar").Query<Button>().ForEach(buttons.Add);
            return buttons.ToArray();
        }

        /// <summary>A panel rectangle's centre in screen pixels (origin bottom-left).</summary>
        protected Vector2 ScreenCentreOf(VisualElement element)
        {
            Vector2 centre = element.worldBound.center; // panel coordinates, origin top-left
            float scale = Screen.width / Root.worldBound.width;
            return new Vector2(centre.x * scale, Screen.height - centre.y * scale);
        }

        /// <summary>The click a button listens to. A batch run has no focused window, so a real mouse click on a button is checked by hand and in the browser.</summary>
        protected static void ClickButton(Button button)
        {
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }

        /// <summary>Presses and releases a key through the Input System; the placement input must be enabled to read it.</summary>
        protected IEnumerator PressKey(Key key)
        {
            if (Keyboard.current == null) _addedKeyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Keyboard keyboard = Keyboard.current;
            if (!keyboard.enabled) InputSystem.EnableDevice(keyboard);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        protected static Material SharedMaterialOf(Component view) => view.GetComponentInChildren<Renderer>().sharedMaterial;

        /// <summary>The first active child of a pooled root.</summary>
        protected static Transform FirstActiveChild(string root)
        {
            foreach (Transform child in FindByName(root))
                if (child.gameObject.activeSelf) return child;
            return null;
        }

        /// <summary>The method Unity calls on a view each frame, as a delegate, so that a measurement does not count a reflection call.</summary>
        protected static System.Action FrameMethod(MonoBehaviour view)
        {
            foreach (string name in new[] { "LateUpdate", "Update" })
            {
                System.Reflection.MethodInfo method = view.GetType().GetMethod(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (method != null) return (System.Action)System.Delegate.CreateDelegate(typeof(System.Action), view, method);
            }
            Assert.Fail($"{view.GetType().Name} has no Update or LateUpdate");
            return null;
        }
    }

    /// <summary>The allocations of one call on this thread, from the profiler (GC.GetAllocatedBytesForCurrentThread reads 0 in this Mono).</summary>
    internal static class Allocations
    {
        private static readonly UnityEngine.Profiling.Recorder Recorder = UnityEngine.Profiling.Recorder.Get("GC.Alloc");

        public static int Of(System.Action action)
        {
            Recorder.enabled = false;
            Recorder.FilterToCurrentThread();
            Recorder.enabled = true;
            action();
            Recorder.enabled = false;
            return Recorder.sampleBlockCount;
        }
    }
}
