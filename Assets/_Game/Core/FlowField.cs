using System;

namespace ElementalTD.Core
{
    /// <summary>
    /// Breadth-first distances from the exit over a 4-neighbour grid. One field serves every enemy.
    /// </summary>
    public sealed class FlowField
    {
        public const int Unreachable = int.MaxValue;

        private readonly GridMap _map;
        private readonly int[] _distance;
        private readonly int[] _queue;

        public FlowField(GridMap map)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _distance = new int[map.CellCount];
            _queue = new int[map.CellCount];
            for (int i = 0; i < _distance.Length; i++) _distance[i] = Unreachable;
        }

        public void Compute(GridPos exit)
        {
            int exitIndex = _map.Index(exit);
            for (int i = 0; i < _distance.Length; i++) _distance[i] = Unreachable;
            if (_map.IsBlocked(exit)) return;

            int width = _map.Width;
            int height = _map.Height;
            int head = 0;
            int tail = 0;
            _distance[exitIndex] = 0;
            _queue[tail++] = exitIndex;

            while (head < tail)
            {
                int current = _queue[head++];
                int x = current % width;
                int y = current / width;
                int next = _distance[current] + 1;

                if (x + 1 < width) Visit(current + 1, next, ref tail);
                if (x - 1 >= 0) Visit(current - 1, next, ref tail);
                if (y + 1 < height) Visit(current + width, next, ref tail);
                if (y - 1 >= 0) Visit(current - width, next, ref tail);
            }
        }

        public bool IsReachable(GridPos pos) => _distance[_map.Index(pos)] != Unreachable;

        public int Distance(GridPos pos) => _distance[_map.Index(pos)];

        public bool TryGetNext(GridPos from, out GridPos next)
        {
            next = from;
            int current = Distance(from);
            if (current == 0 || current == Unreachable) return false;

            int best = current;
            // Strict "<" keeps the first neighbour on a tie: +x, -x, +y, -y.
            Consider(new GridPos(from.X + 1, from.Y), ref best, ref next);
            Consider(new GridPos(from.X - 1, from.Y), ref best, ref next);
            Consider(new GridPos(from.X, from.Y + 1), ref best, ref next);
            Consider(new GridPos(from.X, from.Y - 1), ref best, ref next);
            return best < current;
        }

        private void Visit(int index, int distance, ref int tail)
        {
            if (_distance[index] != Unreachable) return;
            if (_map.IsBlocked(new GridPos(index % _map.Width, index / _map.Width))) return;
            _distance[index] = distance;
            _queue[tail++] = index;
        }

        private void Consider(GridPos candidate, ref int best, ref GridPos next)
        {
            if (!_map.InBounds(candidate)) return;
            int d = _distance[_map.Index(candidate)];
            if (d < best)
            {
                best = d;
                next = candidate;
            }
        }
    }
}
