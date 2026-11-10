using ElementalTD.Core;
using UnityEngine;

namespace ElementalTD.Data
{
    [CreateAssetMenu(fileName = "Map_", menuName = "ElementalTD/Map Definition")]
    public sealed class MapDefinition : ScriptableObject
    {
        [SerializeField, Min(1)] private int width = 1;
        [SerializeField, Min(1)] private int height = 1;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Vector2Int spawn;
        [SerializeField] private Vector2Int exit;
        [SerializeField] private Vector2Int[] blockedCells = new Vector2Int[0];

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;
        public GridPos Spawn => new GridPos(spawn.x, spawn.y);
        public GridPos Exit => new GridPos(exit.x, exit.y);

        public GridMap ToGridMap()
        {
            int count = blockedCells == null ? 0 : blockedCells.Length;
            var blocked = new GridPos[count];
            for (int i = 0; i < count; i++)
                blocked[i] = new GridPos(blockedCells[i].x, blockedCells[i].y);
            return new GridMap(width, height, blocked);
        }

        /// <summary>World position of a cell's centre on the y = 0 plane; grid Y maps to world Z.</summary>
        public Vector3 CellToWorld(GridPos cell)
        {
            return new Vector3((cell.X + 0.5f) * cellSize, 0f, (cell.Y + 0.5f) * cellSize);
        }

        /// <summary>Cell containing a world point, by flooring x and z; may be out of bounds.</summary>
        public GridPos WorldToCell(Vector3 world)
        {
            return new GridPos(Mathf.FloorToInt(world.x / cellSize), Mathf.FloorToInt(world.z / cellSize));
        }

        private void OnValidate()
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            if (cellSize <= 0f) cellSize = 0.01f;
        }
    }
}
