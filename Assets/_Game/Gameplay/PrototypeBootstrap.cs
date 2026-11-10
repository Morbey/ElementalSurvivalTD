using ElementalTD.Core;
using ElementalTD.Data;
using UnityEngine;

namespace ElementalTD.Gameplay
{
    /// <summary>
    /// Wires the prototype scene: builds the Core board from data and spawns one enemy. Holds no game rules.
    /// </summary>
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private MapDefinition map;
        [SerializeField] private EnemyDefinition enemy;

        [Header("Presentation")]
        [Tooltip("Authored for a cell size of 1; its root is scaled by the map's cell size.")]
        [SerializeField] private EnemyAgent enemyPrefab;

        /// <summary>Null when a reference is missing.</summary>
        public Board Board { get; private set; }
        public MapDefinition Map => map;

        private void Awake()
        {
            if (map == null || enemy == null || enemyPrefab == null)
            {
                Debug.LogError("PrototypeBootstrap: assign the map, the enemy and the enemy prefab.", this);
                return;
            }

            Board = new Board(map.ToGridMap(), map.Spawn, map.Exit);
        }

        private void Start()
        {
            if (Board == null) return;

            if (!Board.Field.IsReachable(Board.Spawn))
            {
                Debug.LogError($"PrototypeBootstrap: spawn {map.Spawn} cannot reach exit {map.Exit}. No enemy spawned.", this);
                return;
            }

            EnemyAgent agent = Instantiate(enemyPrefab, map.CellToWorld(map.Spawn), Quaternion.identity);
            agent.name = "Enemy";
            agent.transform.localScale = Vector3.one * map.CellSize;
            agent.Init(Board, enemy.SpeedCellsPerSecond, map);
        }
    }
}
