using System;
using ElementalTD.Core;
using ElementalTD.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ElementalTD.Gameplay
{
    /// <summary>
    /// Turns the pointer into board cells: hover, and a click that removes a tower or tries to place one.
    /// Picks nothing once the run is over, so a click on the end screen never reaches the board.
    /// The rule itself lives in the Core Board.
    /// </summary>
    public sealed class TowerPlacementInput : MonoBehaviour
    {
        [SerializeField] private PrototypeBootstrap bootstrap;
        [SerializeField] private Camera viewCamera;
        [Tooltip("The tower a click places. While unassigned, nothing is picked.")]
        [SerializeField] private TowerDefinition tower;

        private static readonly Plane Ground = new Plane(Vector3.up, Vector3.zero);

        /// <summary>Never raised for OutOfBounds.</summary>
        public event Action<PlacementResult> PlacementRefused;

        public bool HasHoverCell { get; private set; }
        public GridPos HoverCell { get; private set; }
        public TowerDefinition Tower => tower;

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 position = mouse.position.ReadValue();
            Hover(position);
            if (mouse.leftButton.wasPressedThisFrame) Click(position);
        }

        public void Hover(Vector2 screenPosition)
        {
            HasHoverCell = TryPick(screenPosition, out GridPos cell);
            if (HasHoverCell) HoverCell = cell;
        }

        public void Click(Vector2 screenPosition)
        {
            if (!TryPick(screenPosition, out GridPos cell)) return;

            Board board = bootstrap.Board;
            if (board.HasTower(cell))
            {
                board.TryRemoveTower(cell);
                return;
            }

            PlacementResult result = board.TryPlaceTower(cell, tower.ToStats());
            if (result != PlacementResult.Placed) PlacementRefused?.Invoke(result);
        }

        private bool TryPick(Vector2 screenPosition, out GridPos cell)
        {
            cell = default;
            if (bootstrap == null || bootstrap.Board == null || viewCamera == null || tower == null) return false;
            Battle battle = bootstrap.Battle;
            if (battle != null && battle.Outcome != RunOutcome.Running) return false;

            Ray ray = viewCamera.ScreenPointToRay(screenPosition);
            if (!Ground.Raycast(ray, out float distance)) return false;

            cell = bootstrap.Map.WorldToCell(ray.GetPoint(distance));
            return bootstrap.Board.Map.InBounds(cell);
        }
    }
}
