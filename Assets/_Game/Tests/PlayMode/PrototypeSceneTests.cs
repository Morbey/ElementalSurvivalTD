using System.Collections;
using System.Collections.Generic;
using ElementalTD.Core;
using ElementalTD.Data;
using ElementalTD.Gameplay;
using ElementalTD.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Is = UnityEngine.TestTools.Constraints.Is;
using UnityEngine.TestTools.Constraints;

namespace ElementalTD.PlayModeTests
{
    public class PrototypeSceneTests
    {
        private const string SpawnOrExitText = "Cannot build on the spawn or the exit.";
        private const string WallText = "Cannot build on a wall.";
        private const string EnemyText = "An enemy is in the way.";
        private const string PathText = "That would block the path.";

        private MapDefinition _map;
        private PrototypeBootstrap _bootstrap;
        private TowerPlacementInput _input;
        private Camera _camera;
        private Label _label;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            _map = UnityEditor.AssetDatabase.LoadAssetAtPath<MapDefinition>("Assets/_Game/Data/Assets/Map_Prototype.asset");
#endif
            Assert.IsNotNull(_map, "Map_Prototype.asset not found");
            Time.timeScale = 0f; // enemy must not move before the first assertions
            yield return SceneManager.LoadSceneAsync("Prototype", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _bootstrap = Object.FindAnyObjectByType<PrototypeBootstrap>();
            _input = Object.FindAnyObjectByType<TowerPlacementInput>();
            _camera = Object.FindAnyObjectByType<Camera>();
            Assert.IsNotNull(_bootstrap);
            Assert.IsNotNull(_input);
            Assert.IsNotNull(_camera);
            // A real mouse device would overwrite the hover and could click; the tests drive the public methods.
            _input.enabled = false;
            var document = Object.FindAnyObjectByType<UIDocument>();
            Assert.IsNotNull(document);
            _label = document.rootVisualElement.Q<Label>("placement-message");
            Assert.IsNotNull(_label);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            if (_camera != null) _camera.ResetAspect();
            yield return null;
        }

        private static EnemyView[] Agents() => Object.FindObjectsByType<EnemyView>(FindObjectsInactive.Include);

        private GridPos CellOf(Vector3 p) =>
            new GridPos(Mathf.FloorToInt(p.x / _map.CellSize), Mathf.FloorToInt(p.z / _map.CellSize));

        private Vector2 ScreenOf(GridPos cell) => _camera.WorldToScreenPoint(_map.CellToWorld(cell));

        private void ClickCell(int x, int y) => _input.Click(ScreenOf(new GridPos(x, y)));

        // A screen corner is always off the grid: the frame leaves half a cell of padding on the fitting side.
        private static readonly Vector2 OffGrid = Vector2.zero;

        private bool MessageShown() => _label.resolvedStyle.display == DisplayStyle.Flex;

        private static Transform FindByName(string name)
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name == name) return t;
            return null;
        }

        [Test]
        public void AC2_Scene_Has_Exactly_One_Enemy_At_The_Spawn_Cell_Centre()
        {
            EnemyView[] agents = Agents();
            Assert.AreEqual(1, agents.Length);
            Vector3 expected = _map.CellToWorld(_map.Spawn);
            Assert.Less(Vector3.Distance(expected, agents[0].transform.position), 0.01f);
        }

        [Test]
        public void AC1_Scene_Camera_Is_Orthographic_And_Straight_Down()
        {
            Assert.IsTrue(_camera.orthographic);
            Assert.Greater(Vector3.Dot(_camera.transform.forward, Vector3.down), 0.999f);
            Assert.AreEqual(_map.Width * _map.CellSize * 0.5f, _camera.transform.position.x, 0.01f);
            Assert.AreEqual(_map.Height * _map.CellSize * 0.5f, _camera.transform.position.z, 0.01f);
            Assert.GreaterOrEqual(_camera.orthographicSize, _map.Height * _map.CellSize * 0.5f);
        }

        private static string MaterialPath(Material m)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.GetAssetPath(m);
#else
            return string.Empty;
#endif
        }

        private static Material CellMaterial(int x, int y)
        {
            GameObject cell = GameObject.Find($"Cell {x},{y}");
            Assert.IsNotNull(cell, $"Cell {x},{y} not found");
            return cell.GetComponentInChildren<Renderer>().sharedMaterial;
        }

        [UnityTest]
        public IEnumerator S2a_AC4_Every_Renderer_Uses_A_Presentation_Material_With_A_URP_Shader()
        {
            // With a tower placed and the hover shown, so the tower and hover materials are checked too.
            ClickCell(3, 2);
            _input.Hover(ScreenOf(new GridPos(3, 2)));
            yield return null;
            Assert.IsNotNull(GameObject.Find("Tower 3,2"));

            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
            Assert.Greater(renderers.Length, 144);
            foreach (Renderer r in renderers)
            {
                if (r is PanelRenderer) continue; // UI Toolkit draws the HUD panel with its own shader; this check is about world visuals
                Assert.IsNotNull(r.sharedMaterial, r.name);
                StringAssert.StartsWith("Assets/_Game/Presentation/Materials/", MaterialPath(r.sharedMaterial), r.name);
                StringAssert.StartsWith("Universal Render Pipeline/", r.sharedMaterial.shader.name, r.name);
            }
            Assert.AreEqual(0, Object.FindObjectsByType<Collider>(FindObjectsInactive.Include).Length);
        }

        [Test]
        public void S2a_AC4_Spawn_Exit_Wall_Free_Cells_And_Enemy_Use_Five_Different_Materials()
        {
            var materials = new HashSet<Material>
            {
                CellMaterial(_map.Spawn.X, _map.Spawn.Y),
                CellMaterial(_map.Exit.X, _map.Exit.Y),
                CellMaterial(5, 0),
                CellMaterial(1, 0),
                Agents()[0].GetComponentInChildren<Renderer>().sharedMaterial
            };
            Assert.AreEqual(5, materials.Count);
        }

        [Test]
        public void S2a_AC4_Scene_Has_No_Colliders()
        {
            Assert.AreEqual(0, Object.FindObjectsByType<Collider>(FindObjectsInactive.Include).Length);
        }

        // ---- 2c: driven tick by tick (timeScale 0 in SetUp), rendering every few ticks

        private Battle Battle => _bootstrap.Battle;

        private static Transform FillPivot() => FindByName("FillPivot");

        /// <summary>Ticks the battle until the condition holds (checked after every tick), letting views render every 5 ticks, then once more.</summary>
        private IEnumerator TickUntil(System.Func<bool> condition, int maxTicks)
        {
            int ticks = 0;
            while (!condition() && ticks < maxTicks)
            {
                Battle.Tick();
                ticks++;
                if (ticks % 5 == 0) yield return null;
            }
            Assert.IsTrue(condition(), "the condition was never reached");
            yield return null;
        }

        private void PlaceKillingTowers()
        {
            ClickCell(6, 6);
            ClickCell(11, 2);
            Assert.IsTrue(_bootstrap.Board.HasTower(new GridPos(6, 6)));
            Assert.IsTrue(_bootstrap.Board.HasTower(new GridPos(11, 2)));
        }

        [UnityTest]
        public IEnumerator S2c_AC4_AC6_Enemy_Reaches_The_Exit_View_Is_Inactive_For_About_1s_Then_The_Same_View_Is_Back_On_The_Spawn()
        {
            Board board = _bootstrap.Board;
            Enemy enemy = board.EnemyAt(0);
            EnemyView view = Agents()[0];
            Assert.IsTrue(view.gameObject.activeSelf);

            yield return TickUntil(() => board.EnemyCount == 0, 2000);
            Assert.AreEqual(enemy.MaxHealth, enemy.Health, "no tower: it reached the exit");
            Assert.IsFalse(view.gameObject.activeSelf, "not drawn while off the board");

            int ticks = 0;
            while (board.EnemyCount == 0 && ticks < 200) { Battle.Tick(); ticks++; }
            yield return null;

            Assert.That(ticks, Is.InRange(60, 62), "about one second of game time");
            Assert.IsTrue(view.gameObject.activeSelf);
            Assert.Less(Vector3.Distance(_map.CellToWorld(_map.Spawn), view.transform.position), 0.01f);
            EnemyView[] all = Agents();
            Assert.AreEqual(1, all.Length, "a single enemy view throughout");
            Assert.AreSame(view, all[0]);
            Assert.AreEqual(1f, FillPivot().localScale.x, 1e-4f);
        }

        [UnityTest]
        public IEnumerator S2b_AC1_AC5_Enemy_Routes_Around_A_Tower_Placed_On_The_S_Route()
        {
            ClickCell(5, 7); // the gap of wall A: the route must go through (5,8)
            Assert.IsTrue(_bootstrap.Board.HasTower(new GridPos(5, 7)));
            Assert.AreEqual(29, _bootstrap.Board.Field.Distance(_map.Spawn), "route recalculated in the same frame");

            var visited = new HashSet<GridPos>();
            Time.timeScale = 5f;
            float deadline = Time.realtimeSinceStartup + 6f;
            while (!visited.Contains(new GridPos(9, 7)) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                GridPos cell = CellOf(Agents()[0].transform.position);
                Assert.IsFalse(_bootstrap.Board.Map.IsBlocked(cell), $"enemy inside blocked cell {cell}");
                visited.Add(cell);
            }

            Assert.IsTrue(visited.Contains(new GridPos(9, 7)), "enemy never got past the tower");
            Assert.IsFalse(visited.Contains(new GridPos(5, 7)));
            Assert.IsTrue(visited.Contains(new GridPos(5, 8)), "enemy did not detour");
        }

        [UnityTest]
        public IEnumerator S2b_AC1_AC2_Click_Places_Tower_On_Cell_Centre_And_Blocks_It_Second_Click_Removes_It()
        {
            var cell = new GridPos(2, 2);
            ClickCell(2, 2);
            GameObject tower = GameObject.Find("Tower 2,2");
            Assert.IsNotNull(tower);
            Assert.Less(Vector3.Distance(_map.CellToWorld(cell), tower.transform.position), 0.01f);
            Assert.IsTrue(_bootstrap.Board.HasTower(cell));
            Assert.IsTrue(_bootstrap.Board.Map.IsBlocked(cell));

            ClickCell(2, 2);
            yield return null;
            Assert.IsNull(GameObject.Find("Tower 2,2"));
            Assert.IsFalse(_bootstrap.Board.HasTower(cell));
            Assert.IsFalse(_bootstrap.Board.Map.IsBlocked(cell));

            // A fixed wall cannot be removed by clicking.
            ClickCell(5, 0);
            Assert.IsTrue(_bootstrap.Board.Map.IsBlocked(new GridPos(5, 0)));
        }

        [UnityTest]
        public IEnumerator S2b_AC4_Message_Text_Per_Refusal(
            [ValueSource(nameof(RefusalCases))] string reason)
        {
            yield return null;
            Assert.IsFalse(MessageShown(), "message must start hidden");

            string expected;
            switch (reason)
            {
                case "spawnOrExit": ClickCell(_map.Spawn.X, _map.Spawn.Y); expected = SpawnOrExitText; break;
                case "wall": ClickCell(5, 0); expected = WallText; break;
                case "enemy": ClickCell(1, 4); expected = EnemyText; break; // the follower's To at start
                default:
                    ClickCell(5, 7);
                    yield return null;
                    Assert.IsFalse(MessageShown(), "a placement shows no message");
                    ClickCell(5, 8);
                    expected = PathText;
                    break;
            }
            yield return null;
            Assert.IsTrue(MessageShown());
            Assert.AreEqual(expected, _label.text);
        }

        private static readonly string[] RefusalCases = { "spawnOrExit", "wall", "enemy", "path" };

        [UnityTest]
        public IEnumerator S2b_AC4_Message_Hides_After_2s_Unscaled_And_A_New_Refusal_Replaces_It_And_Restarts_The_Timer()
        {
            ClickCell(_map.Spawn.X, _map.Spawn.Y);
            yield return null;
            Assert.IsTrue(MessageShown());
            Assert.AreEqual(SpawnOrExitText, _label.text);

            yield return new WaitForSecondsRealtime(1.5f);
            ClickCell(5, 0);
            yield return null;
            Assert.AreEqual(WallText, _label.text, "a new refusal replaces the text");

            yield return new WaitForSecondsRealtime(1.0f); // 2.5 s after the first, 1.0 s after the second
            Assert.IsTrue(MessageShown(), "timer must restart on the new refusal");

            yield return new WaitForSecondsRealtime(1.4f); // 2.4 s after the second
            Assert.IsFalse(MessageShown(), "message must hide after 2 s");
        }

        [UnityTest]
        public IEnumerator S2b_AC4_Click_Outside_The_Grid_Does_Nothing()
        {
            int distanceBefore = _bootstrap.Board.Field.Distance(_map.Spawn);
            _input.Click(OffGrid);
            yield return null;
            Assert.IsFalse(MessageShown());
            Assert.AreEqual(distanceBefore, _bootstrap.Board.Field.Distance(_map.Spawn));
            Assert.AreEqual(0, FindByName("Towers").childCount);
        }

        [UnityTest]
        public IEnumerator S2b_AC7_Hover_Shows_On_Free_And_Tower_Cells_With_One_Material_Above_The_Tower_And_Hides_Off_Grid()
        {
            Transform hover = FindByName("Hover");
            Assert.IsNotNull(hover);
            Assert.IsFalse(hover.gameObject.activeSelf, "no hover before the mouse is on the grid");

            var free = new GridPos(2, 2);
            _input.Hover(ScreenOf(free));
            yield return null;
            Assert.IsTrue(hover.gameObject.activeSelf);
            Assert.Less(Vector3.Distance(_map.CellToWorld(free), new Vector3(hover.position.x, 0f, hover.position.z)), 0.01f);
            Material freeMaterial = hover.GetComponentInChildren<Renderer>().sharedMaterial;

            ClickCell(3, 2);
            var towerCell = new GridPos(3, 2);
            _input.Hover(ScreenOf(towerCell));
            yield return null;
            Assert.IsTrue(hover.gameObject.activeSelf);
            Assert.Less(Vector3.Distance(_map.CellToWorld(towerCell), new Vector3(hover.position.x, 0f, hover.position.z)), 0.01f);
            Renderer hoverRenderer = hover.GetComponentInChildren<Renderer>();
            Assert.AreSame(freeMaterial, hoverRenderer.sharedMaterial, "one neutral material on free and tower cells");
            Renderer towerRenderer = GameObject.Find("Tower 3,2").GetComponentInChildren<Renderer>();
            Assert.GreaterOrEqual(hoverRenderer.bounds.min.y, towerRenderer.bounds.max.y - 0.001f, "hover must show above the tower");

            _input.Hover(OffGrid);
            yield return null;
            Assert.IsFalse(_input.HasHoverCell);
            Assert.IsFalse(hover.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator S2b_AC8_Camera_Reframes_For_Aspect_1_And_2_5_And_Grid_Stays_In_View(
            [Values(1f, 2.5f)] float aspect)
        {
            _camera.aspect = aspect;
            yield return null;
            yield return null;

            float halfHeight = _camera.orthographicSize;
            float halfWidth = halfHeight * _camera.aspect;
            Vector3 c = _camera.transform.position;
            float width = _map.Width * _map.CellSize, depth = _map.Height * _map.CellSize;
            Assert.LessOrEqual(c.x - halfWidth, 0f);
            Assert.GreaterOrEqual(c.x + halfWidth, width);
            Assert.LessOrEqual(c.z - halfHeight, 0f);
            Assert.GreaterOrEqual(c.z + halfHeight, depth);
        }

        [UnityTest]
        public IEnumerator S2b_AC10_Hover_Allocates_Nothing_After_Warmup()
        {
            Vector2 onGrid = ScreenOf(new GridPos(2, 2));
            _input.Hover(onGrid);
            _input.Hover(OffGrid);
            yield return null;
            Assert.That(() => { _input.Hover(onGrid); }, Is.Not.AllocatingGCMemory());
            Assert.That(() => { _input.Hover(OffGrid); }, Is.Not.AllocatingGCMemory());
        }

        [UnityTest]
        public IEnumerator S2c_AC5_A_Projectile_View_Shows_Between_Tower_And_Enemy_And_The_Bar_Shrinks_To_Health_Over_Max()
        {
            var towerCell = new GridPos(1, 3);
            ClickCell(1, 3);
            Enemy enemy = _bootstrap.Board.EnemyAt(0);
            Assert.AreEqual(1f, FillPivot().localScale.x, 1e-4f, "full on entry");

            yield return TickUntil(() => Battle.ProjectileCount > 0, 100);

            Transform root = FindByName("Projectiles");
            Transform shown = null;
            foreach (Transform child in root)
                if (child.gameObject.activeSelf) shown = child;
            Assert.IsNotNull(shown, "an active projectile view");
            Vector3 tower = _map.CellToWorld(towerCell);
            Vector3 target = Agents()[0].transform.position;
            float detour = Vector3.Distance(tower, shown.position) + Vector3.Distance(shown.position, target) - Vector3.Distance(tower, target);
            Assert.Less(detour, 0.3f * _map.CellSize, "on the way from the tower to the enemy");

            yield return TickUntil(() => enemy.Health < enemy.MaxHealth, 300);
            Assert.AreEqual(90f, enemy.Health);
            Assert.AreEqual(enemy.Health / enemy.MaxHealth, FillPivot().localScale.x, 1e-4f);
        }

        [UnityTest]
        public IEnumerator S2c_AC5_With_Towers_On_6_6_And_11_2_The_Enemy_View_Hides_Before_The_Exit_And_Returns_With_A_Full_Bar()
        {
            PlaceKillingTowers();
            Board board = _bootstrap.Board;
            Enemy enemy = board.EnemyAt(0);
            EnemyView view = Agents()[0];

            yield return TickUntil(() => board.EnemyCount == 0, 3000);
            Assert.LessOrEqual(enemy.Health, 0f, "killed, not arrived");
            Assert.IsFalse(view.gameObject.activeSelf);

            yield return TickUntil(() => board.EnemyCount == 1, 200);
            Assert.IsTrue(view.gameObject.activeSelf);
            Assert.AreEqual(1f, FillPivot().localScale.x, 1e-4f, "full bar on the first visible frame");
            Assert.Less(Vector3.Distance(_map.CellToWorld(_map.Spawn), view.transform.position), 0.01f);
        }

        [UnityTest]
        public IEnumerator S2c_AC6_After_Several_Passes_With_Three_Towers_No_Enemy_Or_Projectile_View_Was_Created_Or_Destroyed()
        {
            ClickCell(6, 6);
            ClickCell(11, 2);
            ClickCell(2, 2);
            yield return null;

            var before = new List<Transform>();
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name == "Enemy" || t.name == "Projectile") before.Add(t);
            Assert.AreEqual(17, before.Count, "one enemy view and a pool of 16");

            int deaths = 0, maxShown = 0;
            Enemy enemy = _bootstrap.Board.EnemyAt(0);
            bool wasOn = true;
            for (int tick = 0; tick < 4000; tick++)
            {
                Battle.Tick();
                if (wasOn && !enemy.IsOnBoard) deaths++;
                wasOn = enemy.IsOnBoard;
                if (Battle.ProjectileCount > maxShown) maxShown = Battle.ProjectileCount;
                if (tick % 5 == 0) yield return null;
            }
            yield return null;

            Assert.GreaterOrEqual(deaths, 3, "a few passes");
            Assert.Greater(maxShown, 0);
            Assert.LessOrEqual(maxShown, 16, "the pool did not need to grow");
            var after = new List<Transform>();
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name == "Enemy" || t.name == "Projectile") after.Add(t);
            CollectionAssert.AreEquivalent(before, after, "the same view objects, none created or destroyed");
            foreach (Transform t in before) Assert.IsTrue(t != null);
            Assert.AreEqual(0, Object.FindObjectsByType<Collider>(FindObjectsInactive.Include).Length);
        }

        [UnityTest]
        public IEnumerator S2c_AC11_On_A_Straight_Stretch_The_Enemy_View_Moves_On_Every_Frame_By_Speed_Times_Frame_Time()
        {
            Time.timeScale = 1f;
            EnemyView view = Agents()[0];
            float speed = _bootstrap.Board.EnemyAt(0).Speed;
            Enemy enemy = _bootstrap.Board.EnemyAt(0);
            // Let the new time scale take effect and the enemy leave the spawn: until its first ticks the view has nothing to interpolate.
            for (int i = 0; i < 3 || enemy.X < 0.2f; i++) yield return null;
            Vector3 last = view.transform.position;
            float lastDt = Time.deltaTime;
            for (int frame = 0; frame < 40; frame++)
            {
                yield return null;
                Vector3 now = view.transform.position;
                // The view shown now was drawn by the previous frame's LateUpdate, from that frame's delta time.
                float expected = speed * lastDt * _map.CellSize;
                float moved = Vector3.Distance(last, now);
                Assert.Greater(moved, 0f, $"frame {frame}: the enemy did not move");
                Assert.AreEqual(expected, moved, 0.15f * expected + 0.002f, $"frame {frame}");
                last = now;
                lastDt = Time.deltaTime;
            }
            Assert.IsTrue(_bootstrap.Board.EnemyAt(0).Follower.To.X <= 4, "still on the first straight stretch");
        }

        [UnityTest]
        public IEnumerator S2c_AC14_Range_Disc_Follows_The_Hover_With_The_Tower_Range_And_Hides_Off_The_Grid()
        {
            Transform disc = FindByName("RangeDisc");
            Assert.IsNotNull(disc);
            Assert.IsFalse(disc.gameObject.activeSelf);

            var cell = new GridPos(2, 2);
            _input.Hover(ScreenOf(cell));
            yield return null;

            Assert.IsTrue(disc.gameObject.activeSelf);
            Vector3 centre = _map.CellToWorld(cell);
            Assert.Less(Vector3.Distance(centre, new Vector3(disc.position.x, 0f, disc.position.z)), 0.01f);
            float radius = disc.GetComponentInChildren<Renderer>().bounds.extents.x;
            Assert.AreEqual(_input.Tower.RangeCells * _map.CellSize, radius, 0.05f);

            _input.Hover(OffGrid);
            yield return null;
            Assert.IsFalse(disc.gameObject.activeSelf);
        }
    }
}
