using System;
using ElementalTD.Core;
using ElementalTD.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ElementalTD.Gameplay
{
    /// <summary>
    /// Turns the pointer into board cells: hover, and a click that selects a tower on the board, ends that selection,
    /// or tries to buy the tower to buy. Holds the buyable towers and which one is the tower to buy: number key N chooses the N-th.
    /// Picks nothing without a running battle, so a click on the end screen never reaches the board.
    /// The selection is input state only; the rules live in the Core Battle and Board (ADR 0004).
    /// </summary>
    public sealed class TowerPlacementInput : MonoBehaviour
    {
        [SerializeField] private PrototypeBootstrap bootstrap;
        [SerializeField] private Camera viewCamera;
        [Tooltip("The buyable towers, in the order of their keys and buttons. While empty, nothing is picked.")]
        [SerializeField] private TowerDefinition[] towers = new TowerDefinition[0];

        private static readonly Plane Ground = new Plane(Vector3.up, Vector3.zero);

        private Tower _selectedTower;

        /// <summary>Never raised for OutOfBounds.</summary>
        public event Action<PlacementResult> PlacementRefused;

        public bool HasHoverCell { get; private set; }
        public GridPos HoverCell { get; private set; }
        public int TowerCount => towers == null ? 0 : towers.Length;
        public TowerDefinition TowerAt(int index) => towers[index];
        /// <summary>The index of the tower to buy; 0 at the start of every run.</summary>
        public int SelectedIndex { get; private set; }
        /// <summary>The tower to buy: the one a click on a free cell buys. Null while the list is empty.</summary>
        public TowerDefinition Tower => TowerCount > 0 ? towers[SelectedIndex] : null;

        /// <summary>The tower selected on the board. Null when none is, once it was sold, and while the run is not running.</summary>
        public Tower SelectedTower
        {
            get
            {
                if (_selectedTower == null || bootstrap == null) return null;
                Battle battle = bootstrap.Battle;
                if (battle == null || battle.Outcome != RunOutcome.Running) return null;
                return battle.Board.TowerOn(_selectedTower.Cell) == _selectedTower ? _selectedTower : null;
            }
        }

        /// <summary>Chooses the tower to buy and ends the selection on the board. Ignores an index outside the list.</summary>
        public void Select(int index)
        {
            if (index < 0 || index >= TowerCount) return;
            SelectedIndex = index;
            Deselect();
        }

        /// <summary>Ends the selection on the board; nothing else changes.</summary>
        public void Deselect() => _selectedTower = null;

        /// <summary>Sells the selected tower through the battle and ends the selection. False with none selected.</summary>
        public bool SellSelected()
        {
            Tower tower = SelectedTower;
            if (tower == null) return false;

            bool sold = bootstrap.Battle.TrySellTower(tower.Cell);
            Deselect();
            return sold;
        }

        private void Update()
        {
            ReadKeys();

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

        /// <summary>
        /// Off the grid nothing happens: the press of a click on a UI button reaches here too, before the button's own click.
        /// </summary>
        public void Click(Vector2 screenPosition)
        {
            if (!TryPick(screenPosition, out GridPos cell)) return;

            Battle battle = bootstrap.Battle;
            Tower tower = battle.Board.TowerOn(cell);
            if (tower != null)
            {
                _selectedTower = tower;
                return;
            }

            if (SelectedTower != null)
            {
                Deselect();
                return;
            }

            PlacementResult result = battle.TryBuyTower(cell, Tower.ToStats());
            if (result != PlacementResult.Placed) PlacementRefused?.Invoke(result);
        }

        private void ReadKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame) Deselect();
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
