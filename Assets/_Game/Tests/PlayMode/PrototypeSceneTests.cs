using System.Collections;
using System.Collections.Generic;
using ElementalTD.Core;
using ElementalTD.Data;
using ElementalTD.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ElementalTD.PlayModeTests
{
    public class PrototypeSceneTests
    {
        private MapDefinition _map;

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
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        private static EnemyAgent[] Agents() => Object.FindObjectsByType<EnemyAgent>(FindObjectsSortMode.None);

        private GridPos CellOf(Vector3 p) =>
            new GridPos(Mathf.FloorToInt(p.x / _map.CellSize), Mathf.FloorToInt(p.z / _map.CellSize));

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
            Camera cam = Object.FindFirstObjectByType<Camera>();
            Assert.IsNotNull(cam);
            Assert.IsTrue(cam.orthographic);
            Assert.Greater(Vector3.Dot(cam.transform.forward, Vector3.down), 0.999f);
            Assert.AreEqual(_map.Width * _map.CellSize * 0.5f, cam.transform.position.x, 0.01f);
            Assert.AreEqual(_map.Height * _map.CellSize * 0.5f, cam.transform.position.z, 0.01f);
            Assert.GreaterOrEqual(cam.orthographicSize, _map.Height * _map.CellSize * 0.5f);
        }

        [UnityTest]
        public IEnumerator AC3_AC4_Enemy_Follows_S_Route_Never_Enters_Blocked_And_Despawns_At_Exit()
        {
            GridMap grid = _map.ToGridMap();
            var visited = new HashSet<GridPos>();
            GridPos lastCell = _map.Spawn;
            Time.timeScale = 5f;
            float deadline = Time.realtimeSinceStartup + 5f;

            EnemyAgent[] agents = Agents();
            Assert.AreEqual(1, agents.Length);
            while (agents[0] != null && Time.realtimeSinceStartup < deadline)
            {
                Vector3 p = agents[0].transform.position;
                GridPos cell = CellOf(p);
                Assert.IsTrue(grid.InBounds(cell), $"enemy left the grid at {p}");
                Assert.IsFalse(grid.IsBlocked(cell), $"enemy inside blocked cell {cell} at {p}");
                visited.Add(cell);
                lastCell = cell;
                yield return null;
            }

            Assert.IsTrue(agents[0] == null, "enemy still alive after 5 real seconds");
            Assert.AreEqual(0, Agents().Length);
            foreach (var corner in new[] { new GridPos(4, 4), new GridPos(4, 7), new GridPos(9, 7), new GridPos(9, 1), new GridPos(15, 1) })
                Assert.IsTrue(visited.Contains(corner), $"route missed {corner}");
            Assert.IsTrue(lastCell == _map.Exit || lastCell == new GridPos(15, 3) || lastCell == new GridPos(14, 4) || lastCell == new GridPos(15, 5),
                $"last seen at {lastCell}");
            // Nothing else happens: no extra enemy appears, the grid stays.
            yield return null;
            yield return null;
            Assert.AreEqual(0, Agents().Length);
            Assert.IsNotNull(GameObject.Find("Grid"));
        }
    }
}
