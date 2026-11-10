using System;
using System.Collections.Generic;

namespace ElementalTD.Core
{
    public sealed class GridMap
    {
        private readonly bool[] _blocked;

        public int Width { get; }
        public int Height { get; }
        public int CellCount => _blocked.Length;

        public GridMap(int width, int height, IReadOnlyList<GridPos> blocked)
        {
            if (width < 1) throw new ArgumentException("Width must be at least 1.", nameof(width));
            if (height < 1) throw new ArgumentException("Height must be at least 1.", nameof(height));

            Width = width;
            Height = height;
            _blocked = new bool[width * height];

            if (blocked == null) return;
            for (int i = 0; i < blocked.Count; i++)
            {
                GridPos cell = blocked[i];
                if (!InBounds(cell))
                    throw new ArgumentException($"Blocked cell {cell} is outside the {width}x{height} grid.", nameof(blocked));
                _blocked[Index(cell)] = true;
            }
        }

        public bool InBounds(GridPos pos) => pos.X >= 0 && pos.X < Width && pos.Y >= 0 && pos.Y < Height;

        public bool IsBlocked(GridPos pos) => _blocked[Index(pos)];

        public int Index(GridPos pos)
        {
            if (!InBounds(pos))
                throw new ArgumentOutOfRangeException(nameof(pos), pos, "Position is outside the grid.");
            return pos.Y * Width + pos.X;
        }
    }
}
