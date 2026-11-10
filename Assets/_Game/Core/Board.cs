using System;
using System.Collections.Generic;

namespace ElementalTD.Core
{
    /// <summary>
    /// Owns the map, the shared flow field, the towers and the living followers, and holds the placement rule:
    /// a tower may never leave the spawn or any follower without a path to the exit.
    /// </summary>
    public sealed class Board
    {
        private readonly bool[] _tower;
        private readonly List<PathFollower> _followers = new List<PathFollower>();

        public GridMap Map { get; }
        public FlowField Field { get; }
        public GridPos Spawn { get; }
        public GridPos Exit { get; }

        public Board(GridMap map, GridPos spawn, GridPos exit)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            if (!map.InBounds(spawn)) throw new ArgumentOutOfRangeException(nameof(spawn), spawn, "Spawn is outside the grid.");
            if (!map.InBounds(exit)) throw new ArgumentOutOfRangeException(nameof(exit), exit, "Exit is outside the grid.");

            Spawn = spawn;
            Exit = exit;
            _tower = new bool[map.CellCount];
            Field = new FlowField(map);
            Field.Compute(exit);
        }

        public bool HasTower(GridPos cell) => Map.InBounds(cell) && _tower[Map.Index(cell)];

        public PathFollower AddFollower()
        {
            var follower = new PathFollower(Field, Spawn);
            _followers.Add(follower);
            return follower;
        }

        public PlacementResult TryPlaceTower(GridPos cell)
        {
            if (!Map.InBounds(cell)) return PlacementResult.OutOfBounds;
            if (cell == Spawn || cell == Exit) return PlacementResult.SpawnOrExit;
            if (Map.IsBlocked(cell)) return PlacementResult.CellBlocked;
            if (IsUnderFollower(cell)) return PlacementResult.EnemyInTheWay;

            Map.SetBlocked(cell, true);
            Field.Compute(Exit);
            if (!EveryoneReachesExit())
            {
                Map.SetBlocked(cell, false);
                Field.Compute(Exit);
                return PlacementResult.WouldBlockPath;
            }

            _tower[Map.Index(cell)] = true;
            return PlacementResult.Placed;
        }

        public bool TryRemoveTower(GridPos cell)
        {
            if (!HasTower(cell)) return false;

            _tower[Map.Index(cell)] = false;
            Map.SetBlocked(cell, false);
            // Removal only opens cells, so no path can break.
            Field.Compute(Exit);
            return true;
        }

        private bool IsUnderFollower(GridPos cell)
        {
            for (int i = 0; i < _followers.Count; i++)
            {
                PathFollower f = _followers[i];
                if (f.From == cell || f.To == cell) return true;
            }
            return false;
        }

        private bool EveryoneReachesExit()
        {
            if (!Field.IsReachable(Spawn)) return false;
            // A follower re-reads the field only at To, so To is the cell that must stay connected.
            for (int i = 0; i < _followers.Count; i++)
                if (!Field.IsReachable(_followers[i].To)) return false;
            return true;
        }
    }
}
