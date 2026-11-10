using ElementalTD.Core;
using ElementalTD.Data;
using UnityEngine;

namespace ElementalTD.Gameplay
{
    /// <summary>
    /// Wires the prototype scene: builds the Core board and battle from data, and is the battle's only driver,
    /// running whole fixed ticks from the frame time. Holds no game rules.
    /// </summary>
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private MapDefinition map;
        [SerializeField] private EnemyDefinition enemy;

        [Header("Prototype loop")]
        [Tooltip("Seconds between the enemy leaving the board, killed or at the exit, and re-entering at the spawn.")]
        [SerializeField, Min(0f)] private float reentryDelaySeconds;

        private float _accumulator;

        /// <summary>Null when a data reference is missing.</summary>
        public Board Board { get; private set; }
        /// <summary>Null when the board is missing or the spawn cannot reach the exit.</summary>
        public Battle Battle { get; private set; }
        public MapDefinition Map => map;
        /// <summary>The time left over after the last tick, as a fraction of a tick (0 to 1).</summary>
        public float TickAlpha => _accumulator / Battle.TickSeconds;

        private void Awake()
        {
            if (map == null || enemy == null)
            {
                Debug.LogError("PrototypeBootstrap: assign the map and the enemy.", this);
                return;
            }

            Board = new Board(map.ToGridMap(), map.Spawn, map.Exit);
            if (!Board.Field.IsReachable(Board.Spawn))
            {
                Debug.LogError($"PrototypeBootstrap: spawn {map.Spawn} cannot reach exit {map.Exit}. No enemy spawned.", this);
                return;
            }

            Battle = new Battle(Board, enemy.ToStats(), reentryDelaySeconds);
        }

        private void Update()
        {
            if (Battle == null) return;

            _accumulator += Time.deltaTime;
            while (_accumulator >= Battle.TickSeconds)
            {
                Battle.Tick();
                _accumulator -= Battle.TickSeconds;
            }
        }
    }
}
