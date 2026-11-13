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
            _nextAction = 0;    // the fixture object is shared by its tests: each replays the reference order from its start
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
            // The editor's own mouse stays between tests: a test that moved it (a click on the Restart button, the pointer on Sell)
            // would leave it over the board, and the next scene would start with a hover cell. Put it back at the corner.
            if (Mouse.current != null) InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = Vector2.zero });
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
            if (disableInput)
            {
                _input.enabled = false;
                _input.Hover(OffGrid); // clears a hover the two frames of loading may have taken from a mouse resting over the board
            }
            _label = Root.Q<Label>("message");
            Assert.IsNotNull(_label);
        }

        protected static EnemyView[] Agents() => Object.FindObjectsByType<EnemyView>(FindObjectsInactive.Include);

        protected GridPos CellOf(Vector3 p) =>
            new GridPos(Mathf.FloorToInt(p.x / _map.CellSize), Mathf.FloorToInt(p.z / _map.CellSize));

        protected Vector2 ScreenOf(GridPos cell) => _camera.WorldToScreenPoint(_map.CellToWorld(cell));

        protected void ClickCell(int x, int y) => _input.Click(ScreenOf(new GridPos(x, y)));

        // Sizes resolve on the physical pixel grid. The batch window is 640 x 480 and the panel scales by 0.73, so a size is off by up to
        // one physical pixel (1.4 reference pixels) either way: a button of 192 resolves to 190.9, a label of 18 to 19.1.
        protected const float Snap = 1.5f;

        protected static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        /// <summary>Selects the tower on the cell by a click and checks that it is the selected one.</summary>
        protected Tower SelectByClick(int x, int y)
        {
            var cell = new GridPos(x, y);
            Tower tower = Board.TowerOn(cell);
            Assert.IsNotNull(tower, $"no tower on ({x},{y})");
            ClickCell(x, y);
            Assert.AreSame(tower, _input.SelectedTower, $"a click on ({x},{y}) selects its tower");
            return tower;
        }

        /// <summary>
        /// Sells the tower on the cell as a player does since 4c: a click selects it, then Sell. Sell's own click is
        /// SellSelected (a batch run has no focused window; the button's click is in the panel tests).
        /// </summary>
        protected void SellByClick(int x, int y)
        {
            SelectByClick(x, y);
            Assert.IsTrue(_input.SellSelected(), $"selling ({x},{y})");
            Assert.IsNull(_input.SelectedTower, "a sale ends the selection");
            Assert.IsFalse(Board.HasTower(new GridPos(x, y)), $"({x},{y}) sold");
        }

        /// <summary>The line is shown from the frame after a message, by the class the style sheet fades on.</summary>
        protected bool MessageShown() => _label.ClassListContains("message--shown");

        /// <summary>The refusal's own label (4d-AC10): it shows where the refused cell is and never on the message line.</summary>
        protected Label RefusalLabel => Root.Q<Label>("refusal");

        /// <summary>Shown when the view made the label visible; the text of a refusal that ended stays on the label.</summary>
        protected bool RefusalShown() => RefusalLabel.resolvedStyle.visibility == Visibility.Visible;

        protected string RefusalText => RefusalLabel.text;

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
            (2220, 10, 1, false, true, 5),
            (2910, 4, 7, true, true, 5),
            (3360, 3, 7, true, true, 10),
            (3630, 4, 6, false, true, 0),
            (3840, 6, 8, false, true, 10),
            (4110, 1, 6, true, true, 0),
            (4470, 0, 6, true, true, 35),
            (4620, 3, 4, true, true, 5),
            (5310, 2, 5, true, true, 5),
            (5580, 1, 2, false, true, 45),
            (5580, 3, 2, false, true, 5),
            (5970, 1, 3, false, true, 5),
            (6390, 3, 3, true, true, 50),
            (6390, 6, 5, true, true, 10),
            (6930, 7, 7, false, true, 0),
            (7410, 7, 5, true, true, 0),
            (7860, 8, 3, false, true, 50),
            (7860, 9, 2, true, true, 10),
            (8310, 8, 5, false, true, 0),
            (8790, 6, 1, true, true, 0),
            (9000, 7, 3, false, true, 65),
            (9000, 7, 1, true, true, 25),
            (9450, 9, 1, false, true, 5),
            (9900, 0, 3, false, true, 5),
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
                if (buy) ClickCell(x, y); else SellByClick(x, y);
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

        private Mouse EnsureMouse()
        {
            if (Mouse.current == null) _addedMouse = InputSystem.AddDevice<Mouse>();
            // A batch run has no focused window; without this the Input System drops the events.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Mouse mouse = Mouse.current;
            if (!mouse.enabled) InputSystem.EnableDevice(mouse); // the unfocused batch window disabled it
            return mouse;
        }

        /// <summary>Puts the real pointer at a screen position for one frame; it stays there until it is moved. The placement input must be enabled to read it.</summary>
        protected IEnumerator MovePointer(Vector2 position)
        {
            Mouse mouse = EnsureMouse();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
        }

        /// <summary>A real left click through the Input System, which the UI Toolkit panel and the placement input both read.</summary>
        protected IEnumerator ClickScreen(Vector2 position)
        {
            Mouse mouse = EnsureMouse();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
        }

        protected const string MaterialDir = "Assets/_Game/Presentation/Materials/";

        protected static Color Hex(string hex)
        {
            Assert.IsTrue(ColorUtility.TryParseHtmlString("#" + hex, out Color color), hex);
            return color;
        }

        protected static void AssertColor(Color expected, Color actual, string what, float tolerance = 1.5f / 255f)
        {
            Assert.AreEqual(expected.r, actual.r, tolerance, what + " red");
            Assert.AreEqual(expected.g, actual.g, tolerance, what + " green");
            Assert.AreEqual(expected.b, actual.b, tolerance, what + " blue");
            Assert.AreEqual(expected.a, actual.a, tolerance, what + " alpha");
        }

        protected static Material LoadMaterial(string name)
        {
#if UNITY_EDITOR
            Material material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(MaterialDir + name + ".mat");
#else
            Material material = null;
#endif
            Assert.IsNotNull(material, name);
            return material;
        }

        protected static string PathOf(Object asset)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.GetAssetPath(asset);
#else
            return string.Empty;
#endif
        }

        protected struct Area { public float X0, X1, Y0, Y1; }

        /// <summary>The board area as a fraction of the screen, from the bottom-left.</summary>
        protected Area BoardArea()
        {
            Rect root = Root.worldBound, area = Root.Q<VisualElement>("board-area").worldBound;
            return new Area
            {
                X0 = (area.xMin - root.xMin) / root.width,
                X1 = (area.xMax - root.xMin) / root.width,
                Y0 = 1f - (area.yMax - root.yMin) / root.height,
                Y1 = 1f - (area.yMin - root.yMin) / root.height,
            };
        }

        protected void AssertLayout(string when)
        {
            const float e = 0.002f;
            Area a = BoardArea();
            Assert.Greater(a.X1 - a.X0, 0.1f, when + ": the board area has a size");
            float cell = _map.CellSize, pad = 0.3f * cell;
            float width = _map.Width * cell, depth = _map.Height * cell;
            Vector3 lo = _camera.WorldToViewportPoint(new Vector3(-pad, 0f, -pad));
            Vector3 hi = _camera.WorldToViewportPoint(new Vector3(width + pad, 0f, depth + pad));
            Assert.GreaterOrEqual(lo.x, a.X0 - e, when + ": left");
            Assert.LessOrEqual(hi.x, a.X1 + e, when + ": right");
            Assert.GreaterOrEqual(lo.y, a.Y0 - e, when + ": bottom");
            Assert.LessOrEqual(hi.y, a.Y1 + e, when + ": top");

            bool widthBound = Mathf.Abs(lo.x - a.X0) < e && Mathf.Abs(hi.x - a.X1) < e;
            bool heightBound = Mathf.Abs(lo.y - a.Y0) < e && Mathf.Abs(hi.y - a.Y1) < e;
            Assert.IsTrue(widthBound || heightBound, $"{when}: the padded board touches the area on two opposite sides (x {lo.x:F3}..{hi.x:F3} of {a.X0:F3}..{a.X1:F3}, y {lo.y:F3}..{hi.y:F3} of {a.Y0:F3}..{a.Y1:F3})");

            Vector3 centre = _camera.WorldToViewportPoint(new Vector3(width * 0.5f, 0f, depth * 0.5f));
            Assert.AreEqual((a.X0 + a.X1) * 0.5f, centre.x, e, when + ": the board's centre is the area's centre on x");
            Assert.AreEqual((a.Y0 + a.Y1) * 0.5f, centre.y, e, when + ": and on y");

            // The padding is 0.3 cells on the side that binds.
            Vector3 board0 = _camera.WorldToViewportPoint(Vector3.zero), board1 = _camera.WorldToViewportPoint(new Vector3(width, 0f, depth));
            float cellX = (board1.x - board0.x) / _map.Width, cellY = (board1.y - board0.y) / _map.Height;
            if (widthBound) Assert.AreEqual(0.3f * cellX, board0.x - a.X0, e, when + ": 0.3 cells of padding on x");
            if (heightBound) Assert.AreEqual(0.3f * cellY, board0.y - a.Y0, e, when + ": 0.3 cells of padding on y");

            // No bar covers a cell: the board's rectangle in panel coordinates misses both bars.
            Rect root = Root.worldBound;
            var boardRect = Rect.MinMaxRect(board0.x * root.width, (1f - board1.y) * root.height, board1.x * root.width, (1f - board0.y) * root.height);
            Assert.IsFalse(boardRect.Overlaps(Root.Q<VisualElement>("top-bar").worldBound), when + ": the top bar covers no cell");
            Assert.IsFalse(boardRect.Overlaps(Root.Q<VisualElement>("bottom-bar").worldBound), when + ": the bottom bar covers no cell");
            Assert.IsFalse(boardRect.Overlaps(Root.Q<VisualElement>("side-column").worldBound), when + ": the side column covers no cell (4c-AC7)");
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
