using System.Collections;
using System.Collections.Generic;
using ElementalTD.Core;
using ElementalTD.Data;
using ElementalTD.Gameplay;
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

        private static EnemyAgent[] Agents() => Object.FindObjectsByType<EnemyAgent>();

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
            EnemyAgent[] agents = Agents();
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

        [UnityTest]
        public IEnumerator S2b_AC6_Enemy_Restarts_At_Spawn_Same_Single_Instance_And_Walks_The_Route_Again()
        {
            GridMap grid = _bootstrap.Board.Map;
            EnemyAgent agent = Agents()[0];
            Vector3 previous = agent.transform.position;
            bool reachedExit = false, jumped = false, passedCornerAgain = false;
            Time.timeScale = 5f;
            float deadline = Time.realtimeSinceStartup + 8f;

            while (!passedCornerAgain && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                EnemyAgent[] agents = Agents();
                Assert.AreEqual(1, agents.Length, "exactly one enemy at all times");
                Assert.AreSame(agent, agents[0], "same EnemyAgent object throughout");

                Vector3 p = agent.transform.position;
                GridPos cell = CellOf(p);
                Assert.IsTrue(grid.InBounds(cell), $"enemy left the grid at {p}");
                Assert.IsFalse(grid.IsBlocked(cell), $"enemy inside blocked cell {cell}");

                if (!jumped)
                {
                    if (cell == _map.Exit) reachedExit = true;
                    if (Vector3.Distance(p, previous) > 3f * _map.CellSize)
                    {
                        jumped = true;
                        Assert.IsTrue(reachedExit, "enemy must reach the exit cell before it restarts");
                        Assert.Less(Vector3.Distance(p, _map.CellToWorld(_map.Spawn)), 0.01f, "restart lands exactly on the spawn");
                    }
                }
                else if (cell == new GridPos(4, 4))
                {
                    passedCornerAgain = true;
                }
                previous = p;
            }

            Assert.IsTrue(jumped, "enemy never restarted");
            Assert.IsTrue(passedCornerAgain, "enemy did not walk the route again");
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
    }
}
