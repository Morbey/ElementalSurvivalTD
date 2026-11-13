using System;
using System.Collections.Generic;

namespace ElementalTD.Core
{
    /// <summary>
    /// What is on the board: the map, the shared flow field, the towers and the enemies on it.
    /// Holds the placement rule: a tower may never leave the spawn or any enemy on the board without a path to the exit.
    /// </summary>
    public sealed class Board
    {
        private readonly Tower[] _towerAt;
        private readonly List<Tower> _towers = new List<Tower>();
        private readonly List<Enemy> _enemies = new List<Enemy>();
        private readonly Stack<Enemy> _freeEnemies = new Stack<Enemy>();
        private int _entries;

        /// <summary>Raised after the board has changed.</summary>
        public event Action<GridPos> TowerPlaced;
        /// <summary>Raised after the board has changed.</summary>
        public event Action<GridPos> TowerRemoved;

        public GridMap Map { get; }
        public FlowField Field { get; }
        public GridPos Spawn { get; }
        public GridPos Exit { get; }

        public int TowerCount => _towers.Count;
        /// <summary>In placement order.</summary>
        public Tower TowerAt(int index) => _towers[index];
        public int EnemyCount => _enemies.Count;
        /// <summary>In entry order.</summary>
        public Enemy EnemyAt(int index) => _enemies[index];

        public Board(GridMap map, GridPos spawn, GridPos exit)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            if (!map.InBounds(spawn)) throw new ArgumentOutOfRangeException(nameof(spawn), spawn, "Spawn is outside the grid.");
            if (!map.InBounds(exit)) throw new ArgumentOutOfRangeException(nameof(exit), exit, "Exit is outside the grid.");

            Spawn = spawn;
            Exit = exit;
            _towerAt = new Tower[map.CellCount];
            Field = new FlowField(map);
            Field.Compute(exit);
        }

        public bool HasTower(GridPos cell) => TowerOn(cell) != null;

        /// <summary>Null off the grid or with no tower on the cell.</summary>
        public Tower TowerOn(GridPos cell) => Map.InBounds(cell) ? _towerAt[Map.Index(cell)] : null;

        public PlacementResult TryPlaceTower(GridPos cell, in TowerStats stats)
        {
            if (!Map.InBounds(cell)) return PlacementResult.OutOfBounds;
            if (cell == Spawn || cell == Exit) return PlacementResult.SpawnOrExit;
            if (Map.IsBlocked(cell)) return PlacementResult.CellBlocked;
            if (IsUnderEnemy(cell)) return PlacementResult.EnemyInTheWay;

            Map.SetBlocked(cell, true);
            Field.Compute(Exit);
            if (!EveryoneReachesExit())
            {
                Map.SetBlocked(cell, false);
                Field.Compute(Exit);
                return PlacementResult.WouldBlockPath;
            }

            var tower = new Tower(cell, stats);
            _towerAt[Map.Index(cell)] = tower;
            _towers.Add(tower);
            TowerPlaced?.Invoke(cell);
            return PlacementResult.Placed;
        }

        public bool TryRemoveTower(GridPos cell)
        {
            if (!HasTower(cell)) return false;

            int index = Map.Index(cell);
            _towers.RemoveAt(IndexOf(_towers, _towerAt[index]));
            _towerAt[index] = null;
            Map.SetBlocked(cell, false);
            // Removal only opens cells, so no path can break.
            Field.Compute(Exit);
            TowerRemoved?.Invoke(cell);
            return true;
        }

        /// <summary>
        /// An enemy enters at the spawn with these stats and full health; its previous position is its position.
        /// It belongs to the wave of that index, or to none at -1, and takes the next entry number of the run.
        /// Reuses an object that left the board, so a spawn allocates only at a new peak of enemies on the board.
        /// </summary>
        public Enemy SpawnEnemy(in EnemyStats stats, int waveIndex = -1)
        {
            Enemy enemy = _freeEnemies.Count > 0 ? _freeEnemies.Pop() : new Enemy(Field, Spawn);
            enemy.Enter(Spawn, stats, waveIndex, ++_entries);
            _enemies.Add(enemy);
            return enemy;
        }

        /// <summary>The enemy leaves the board and its object returns to the free list.</summary>
        public void RemoveEnemy(Enemy enemy)
        {
            int index = IndexOf(_enemies, enemy);
            if (index < 0) return;
            _enemies.RemoveAt(index);
            enemy.IsOnBoard = false;
            _freeEnemies.Push(enemy);
        }

        /// <summary>
        /// An enemy occupies the rectangle spanned by its step: two cells on a straight step, four on a diagonal one,
        /// so a diagonal step it has started stays legal.
        /// </summary>
        private bool IsUnderEnemy(GridPos cell)
        {
            for (int i = 0; i < _enemies.Count; i++)
            {
                PathFollower f = _enemies[i].Follower;
                if (cell.X >= Math.Min(f.From.X, f.To.X) && cell.X <= Math.Max(f.From.X, f.To.X)
                    && cell.Y >= Math.Min(f.From.Y, f.To.Y) && cell.Y <= Math.Max(f.From.Y, f.To.Y))
                    return true;
            }
            return false;
        }

        private bool EveryoneReachesExit()
        {
            if (!Field.IsReachable(Spawn)) return false;
            // A follower re-reads the field only at To, so To is the cell that must stay connected.
            for (int i = 0; i < _enemies.Count; i++)
                if (!Field.IsReachable(_enemies[i].Follower.To)) return false;
            return true;
        }

        private static int IndexOf<T>(List<T> list, T item) where T : class
        {
            for (int i = 0; i < list.Count; i++)
                if (ReferenceEquals(list[i], item)) return i;
            return -1;
        }
    }
}
