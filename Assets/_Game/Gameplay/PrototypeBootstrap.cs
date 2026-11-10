using ElementalTD.Core;
using ElementalTD.Data;
using UnityEngine;

namespace ElementalTD.Gameplay
{
    /// <summary>
    /// Wires the prototype scene: builds the Core grid from data, lays out the cell prefabs,
    /// frames the camera and spawns one enemy. Holds no game rules.
    /// </summary>
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private MapDefinition map;
        [SerializeField] private EnemyDefinition enemy;

        [Header("Scene")]
        [SerializeField] private Camera viewCamera;

        [Header("Presentation")]
        [Tooltip("Authored for a cell size of 1; its root is scaled by the map's cell size. Its tile starts with the free material.")]
        [SerializeField] private GameObject cellPrefab;
        [SerializeField] private Material blockedMaterial;
        [SerializeField] private Material spawnMaterial;
        [SerializeField] private Material exitMaterial;
        [Tooltip("Authored for a cell size of 1; its root is scaled by the map's cell size.")]
        [SerializeField] private EnemyAgent enemyPrefab;
        [Tooltip("Extra world units around the grid when framing the camera, in cells.")]
        [SerializeField, Min(0f)] private float framePaddingCells = 0.5f;
        [SerializeField, Min(1f)] private float cameraHeight = 20f;

        private void Start()
        {
            if (map == null || enemy == null || viewCamera == null || cellPrefab == null || enemyPrefab == null
                || blockedMaterial == null || spawnMaterial == null || exitMaterial == null)
            {
                Debug.LogError("PrototypeBootstrap: assign every data, scene and presentation reference.", this);
                return;
            }

            GridMap grid = map.ToGridMap();
            var field = new FlowField(grid);
            field.Compute(map.Exit);

            BuildGridVisuals(grid);
            FrameCamera();

            if (!field.IsReachable(map.Spawn))
            {
                Debug.LogError($"PrototypeBootstrap: spawn {map.Spawn} cannot reach exit {map.Exit}. No enemy spawned.", this);
                return;
            }

            SpawnEnemy(field);
        }

        private void BuildGridVisuals(GridMap grid)
        {
            var root = new GameObject("Grid").transform;
            var cellScale = Vector3.one * map.CellSize;

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    GameObject tile = Instantiate(cellPrefab, map.CellToWorld(cell), Quaternion.identity, root);
                    tile.name = $"Cell {x},{y}";
                    tile.transform.localScale = cellScale;

                    var renderer = tile.GetComponentInChildren<Renderer>();
                    if (cell == map.Spawn) renderer.sharedMaterial = spawnMaterial;
                    else if (cell == map.Exit) renderer.sharedMaterial = exitMaterial;
                    else if (grid.IsBlocked(cell)) renderer.sharedMaterial = blockedMaterial;
                }
            }
        }

        private void FrameCamera()
        {
            float worldWidth = map.Width * map.CellSize;
            float worldDepth = map.Height * map.CellSize;
            float padding = framePaddingCells * map.CellSize;

            viewCamera.orthographic = true;
            viewCamera.transform.SetPositionAndRotation(
                new Vector3(worldWidth * 0.5f, cameraHeight, worldDepth * 0.5f),
                Quaternion.Euler(90f, 0f, 0f));
            viewCamera.orthographicSize =
                Mathf.Max(worldDepth * 0.5f, worldWidth * 0.5f / viewCamera.aspect) + padding;
        }

        private void SpawnEnemy(FlowField field)
        {
            EnemyAgent agent = Instantiate(enemyPrefab, map.CellToWorld(map.Spawn), Quaternion.identity);
            agent.name = "Enemy";
            agent.transform.localScale = Vector3.one * map.CellSize;
            agent.Init(new PathFollower(field, map.Spawn), enemy.SpeedCellsPerSecond, map);
        }
    }
}
