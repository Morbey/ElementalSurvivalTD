using ElementalTD.Core;
using ElementalTD.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ElementalTD.Gameplay
{
    /// <summary>
    /// Wires the prototype scene: builds the Core board and battle from data, and is the battle's only driver,
    /// running whole fixed ticks from the frame time until the run is won or lost. Holds no game rules.
    /// </summary>
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private MapDefinition map;
        [SerializeField] private RunDefinition run;

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
            if (map == null || run == null)
            {
                Debug.LogError("PrototypeBootstrap: assign the map and the run.", this);
                return;
            }

            Board = new Board(map.ToGridMap(), map.Spawn, map.Exit);
            if (!Board.Field.IsReachable(Board.Spawn))
            {
                Debug.LogError($"PrototypeBootstrap: spawn {map.Spawn} cannot reach exit {map.Exit}. No run started.", this);
                return;
            }

            Battle = new Battle(Board, run.ToWaves(), run.Lives, run.StartingGold, run.SellRefundPercent);
        }

        private void Update()
        {
            if (Battle == null || Battle.Outcome != RunOutcome.Running) return;

            _accumulator += Time.deltaTime;
            while (_accumulator >= Battle.TickSeconds)
            {
                Battle.Tick();
                _accumulator -= Battle.TickSeconds;
                // Stopping on the tick that ends the run freezes TickAlpha, so no view moves afterwards.
                if (Battle.Outcome != RunOutcome.Running) break;
            }
        }

        /// <summary>Starts a fresh run by reloading the scene, so no state of this run can leak into the next (ADR 0004).</summary>
        public void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
