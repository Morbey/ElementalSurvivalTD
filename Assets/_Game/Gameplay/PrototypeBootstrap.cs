using System;
using ElementalTD.Core;
using ElementalTD.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ElementalTD.Gameplay
{
    /// <summary>
    /// Wires the prototype scene: builds the Core board and battle from data, and is the battle's only driver,
    /// running whole fixed ticks from the frame time until the run is won or lost. While a choice of perks is open it runs
    /// no tick and gathers no time (ADR 0007). Holds no game rules.
    /// </summary>
    public sealed class PrototypeBootstrap : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private MapDefinition map;
        [SerializeField] private RunDefinition run;
        [SerializeField] private ElementChartDefinition elementChart;

        /// <summary>For tests: the seed of the next run's draw, taken and cleared when the run is built; null: from the clock.</summary>
        public static uint? SeedForNextRun;

        private float _accumulator;

        /// <summary>Null when a data reference is missing.</summary>
        public Board Board { get; private set; }
        /// <summary>Null when the board is missing or the spawn cannot reach the exit.</summary>
        public Battle Battle { get; private set; }
        public MapDefinition Map => map;
        public RunDefinition Run => run;
        /// <summary>The time left over after the last tick, as a fraction of a tick (0 to 1).</summary>
        public float TickAlpha => _accumulator / Battle.TickSeconds;

        private void Awake()
        {
            if (map == null || run == null || elementChart == null)
            {
                Debug.LogError("PrototypeBootstrap: assign the map, the run and the element chart.", this);
                return;
            }

            Board = new Board(map.ToGridMap(), map.Spawn, map.Exit);
            if (!Board.Field.IsReachable(Board.Spawn))
            {
                Debug.LogError($"PrototypeBootstrap: spawn {map.Spawn} cannot reach exit {map.Exit}. No run started.", this);
                return;
            }

            uint seed = SeedForNextRun ?? unchecked((uint)DateTime.UtcNow.Ticks);
            SeedForNextRun = null;
            Battle = new Battle(Board, run.ToWaves(), run.Lives, run.StartingGold, run.SellRefundPercent, elementChart.ToChart(),
                new Progression(run.ToLevelTable(), run.ToPerks(), seed));
        }

        private void Update()
        {
            if (Battle == null || Battle.Outcome != RunOutcome.Running) return;
            if (Battle.AwaitingChoice)
            {
                // No time gathers during a choice, so the frame after the pick runs only its own ticks.
                _accumulator = 0f;
                return;
            }

            _accumulator += Time.deltaTime;
            while (_accumulator >= Battle.TickSeconds)
            {
                Battle.Tick();
                _accumulator -= Battle.TickSeconds;
                // Stopping on the tick that ends the run freezes TickAlpha, so no view moves afterwards.
                if (Battle.Outcome != RunOutcome.Running) break;
                if (Battle.AwaitingChoice)
                {
                    _accumulator = 0f;
                    break;
                }
            }
        }

        /// <summary>Starts a fresh run by reloading the scene, so no state of this run can leak into the next (ADR 0004).</summary>
        public void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
