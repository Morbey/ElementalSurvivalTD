using ElementalTD.Core;
using ElementalTD.Data;
using UnityEngine;

namespace ElementalTD.Gameplay
{
    /// <summary>
    /// Wires the prototype scene: builds the Core grid from data, draws throwaway primitives,
    /// frames the camera and spawns one enemy. Holds no game rules.
    /// </summary>
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private MapDefinition map;
        [SerializeField] private EnemyDefinition enemy;

        [Header("Scene")]
        [SerializeField] private Camera viewCamera;

        [Header("Presentation (throwaway prototype visuals)")]
        [SerializeField] private Color freeColor = new Color(0.75f, 0.75f, 0.75f);
        [SerializeField] private Color blockedColor = new Color(0.2f, 0.2f, 0.25f);
        [SerializeField] private Color spawnColor = new Color(0.2f, 0.75f, 0.3f);
        [SerializeField] private Color exitColor = new Color(0.85f, 0.25f, 0.2f);
        [SerializeField] private Color enemyColor = new Color(0.95f, 0.8f, 0.1f);
        [Tooltip("Fraction of a cell left empty between tiles so the grid lines show.")]
        [SerializeField, Range(0f, 0.5f)] private float cellGap = 0.05f;
        [SerializeField, Min(0.001f)] private float cellThickness = 0.1f;
        [Tooltip("Extra world units around the grid when framing the camera, in cells.")]
        [SerializeField, Min(0f)] private float framePaddingCells = 0.5f;
        [SerializeField, Min(1f)] private float cameraHeight = 20f;
        [Tooltip("Enemy visual size as a fraction of a cell.")]
        [SerializeField, Range(0.1f, 1f)] private float enemyScale = 0.5f;

        private void Start()
        {
            if (map == null || enemy == null || viewCamera == null)
            {
                Debug.LogError("PrototypeBootstrap: assign the map, enemy and camera references.", this);
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
            Material free = null, blocked = null, spawn = null, exit = null;

            float cellSize = map.CellSize;
            var tileScale = new Vector3(cellSize * (1f - cellGap), cellThickness, cellSize * (1f - cellGap));
            var sink = new Vector3(0f, -cellThickness * 0.5f, 0f);

            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    GameObject tile = CreateVisual(PrimitiveType.Cube, $"Cell {x},{y}", root);
                    tile.transform.position = map.CellToWorld(cell) + sink;
                    tile.transform.localScale = tileScale;

                    var renderer = tile.GetComponent<Renderer>();
                    if (free == null)
                    {
                        Material baseMaterial = renderer.sharedMaterial;
                        free = Tinted(baseMaterial, freeColor, "Free");
                        blocked = Tinted(baseMaterial, blockedColor, "Blocked");
                        spawn = Tinted(baseMaterial, spawnColor, "Spawn");
                        exit = Tinted(baseMaterial, exitColor, "Exit");
                    }

                    if (cell == map.Spawn) renderer.sharedMaterial = spawn;
                    else if (cell == map.Exit) renderer.sharedMaterial = exit;
                    else if (grid.IsBlocked(cell)) renderer.sharedMaterial = blocked;
                    else renderer.sharedMaterial = free;
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
            // Root sits exactly on the cell centre; the visual is a child lifted so it rests on the grid.
            var root = new GameObject("Enemy");
            float size = map.CellSize * enemyScale;
            GameObject body = CreateVisual(PrimitiveType.Capsule, "Body", root.transform);
            body.transform.localScale = new Vector3(size, size * 0.5f, size);
            body.transform.localPosition = new Vector3(0f, size * 0.5f, 0f);
            var bodyRenderer = body.GetComponent<Renderer>();
            bodyRenderer.sharedMaterial = Tinted(bodyRenderer.sharedMaterial, enemyColor, "Enemy");

            var agent = root.AddComponent<EnemyAgent>();
            agent.Init(new PathFollower(field, map.Spawn), enemy.SpeedCellsPerSecond, map);
        }

        private static GameObject CreateVisual(PrimitiveType type, string name, Transform parent)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            // Nothing uses physics in this slice.
            Destroy(go.GetComponent<Collider>());
            return go;
        }

        private static Material Tinted(Material source, Color color, string name)
        {
            var material = new Material(source) { name = $"Prototype {name}" };
            material.color = color;
            return material;
        }
    }
}
