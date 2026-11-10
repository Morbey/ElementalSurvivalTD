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

        /// <summary>Next cell with no last move: on a tie, the first neighbour in the order +x, -x, +y, -y.</summary>
        public bool TryGetNext(GridPos from, out GridPos next) => TryGetNext(from, from, out next);

        /// <summary>
        /// Next cell for a follower that reached <paramref name="from"/> from the neighbour <paramref name="cameFrom"/>.
        /// Among the neighbours with the lowest distance it prefers straight ahead, then any but the cell it just left
        /// in the order +x, -x, +y, -y, and goes back only when nothing else is as short.
        /// Passing <paramref name="from"/> as <paramref name="cameFrom"/> means no last move.
        /// </summary>
        public bool TryGetNext(GridPos from, GridPos cameFrom, out GridPos next)
        {
            next = from;
            int current = Distance(from);
            if (current == 0 || current == Unreachable) return false;

            int best = current;
            LowerBest(new GridPos(from.X + 1, from.Y), ref best);
            LowerBest(new GridPos(from.X - 1, from.Y), ref best);
            LowerBest(new GridPos(from.X, from.Y + 1), ref best);
            LowerBest(new GridPos(from.X, from.Y - 1), ref best);
            if (best >= current) return false;

            var ahead = new GridPos(2 * from.X - cameFrom.X, 2 * from.Y - cameFrom.Y);
            if (ahead != from && HasDistance(ahead, best))
            {
                next = ahead;
                return true;
            }

            bool backIsBest = false;
            if (Prefer(new GridPos(from.X + 1, from.Y), cameFrom, best, ref next, ref backIsBest)) return true;
            if (Prefer(new GridPos(from.X - 1, from.Y), cameFrom, best, ref next, ref backIsBest)) return true;
            if (Prefer(new GridPos(from.X, from.Y + 1), cameFrom, best, ref next, ref backIsBest)) return true;
            if (Prefer(new GridPos(from.X, from.Y - 1), cameFrom, best, ref next, ref backIsBest)) return true;
            next = backIsBest ? cameFrom : from;
            return backIsBest;
        }

        private void Visit(int index, int distance, ref int tail)
        {
            if (_distance[index] != Unreachable) return;
            if (_map.IsBlocked(new GridPos(index % _map.Width, index / _map.Width))) return;
            _distance[index] = distance;
            _queue[tail++] = index;
        }

        private void LowerBest(GridPos candidate, ref int best)
        {
            if (!_map.InBounds(candidate)) return;
            int d = _distance[_map.Index(candidate)];
            if (d < best) best = d;
        }

        private bool HasDistance(GridPos candidate, int distance) =>
            _map.InBounds(candidate) && _distance[_map.Index(candidate)] == distance;

        /// <summary>Takes the candidate if it is at the best distance and is not the cell just left.</summary>
        private bool Prefer(GridPos candidate, GridPos cameFrom, int best, ref GridPos next, ref bool backIsBest)
        {
            if (!HasDistance(candidate, best)) return false;
            if (candidate == cameFrom)
            {
                backIsBest = true;
                return false;
            }
            next = candidate;
            return true;
        }
    }
}
