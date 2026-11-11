using System;
using ElementalTD.Core;
using ElementalTD.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ElementalTD.Gameplay
{
    /// <summary>
    /// Turns the pointer into board cells: hover, and a click that sells a tower or tries to buy the selected one.
    /// Holds the buyable towers and which one is selected: number key N selects the N-th.
    /// Picks nothing without a running battle, so a click on the end screen never reaches the board.
    /// The rules live in the Core Battle and Board.
    /// </summary>
    public sealed class TowerPlacementInput : MonoBehaviour
    {
        [SerializeField] private PrototypeBootstrap bootstrap;
        [SerializeField] private Camera viewCamera;
        [Tooltip("The buyable towers, in the order of their keys and buttons. While empty, nothing is picked.")]
        [SerializeField] private TowerDefinition[] towers = new TowerDefinition[0];

        private static readonly Plane Ground = new Plane(Vector3.up, Vector3.zero);

        /// <summary>Never raised for OutOfBounds.</summary>
        public event Action<PlacementResult> PlacementRefused;

        public bool HasHoverCell { get; private set; }
        public GridPos HoverCell { get; private set; }
        public int TowerCount => towers == null ? 0 : towers.Length;
        public TowerDefinition TowerAt(int index) => towers[index];
        /// <summary>0 at the start of every run.</summary>
        public int SelectedIndex { get; private set; }
        /// <summary>The selected tower: the one a click buys. Null while the list is empty.</summary>
        public TowerDefinition Tower => TowerCount > 0 ? towers[SelectedIndex] : null;

        /// <summary>Ignores an index outside the list.</summary>
        public void Select(int index)
        {
            if (index >= 0 && index < TowerCount) SelectedIndex = index;
        }

        private void Update()
        {
            SelectByKey();

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

            Battle battle = bootstrap.Battle;
            if (battle.TrySellTower(cell)) return;

            PlacementResult result = battle.TryBuyTower(cell, Tower.ToStats());
            if (result != PlacementResult.Placed) PlacementRefused?.Invoke(result);
        }

        private void SelectByKey()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            for (int i = 0; i < TowerCount && Key.Digit1 + i <= Key.Digit0; i++)
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame) Select(i);
        }

        private bool TryPick(Vector2 screenPosition, out GridPos cell)
        {
            cell = default;
            if (bootstrap == null || viewCamera == null || Tower == null) return false;
            Battle battle = bootstrap.Battle;
            if (battle == null || battle.Outcome != RunOutcome.Running) return false;

            Ray ray = viewCamera.ScreenPointToRay(screenPosition);
            if (!Ground.Raycast(ray, out float distance)) return false;

            cell = bootstrap.Map.WorldToCell(ray.GetPoint(distance));
            return battle.Board.Map.InBounds(cell);
        }
    }
}
