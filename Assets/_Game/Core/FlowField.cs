using System;

namespace ElementalTD.Core
{
    /// <summary>
    /// Shortest distances from the exit over an 8-neighbour grid, in whole numbers so no rounding decides a route.
    /// A diagonal step is legal only when both cells it passes between are free. One field serves every enemy.
    /// </summary>
    public sealed class FlowField
    {
        public const int Unreachable = int.MaxValue;
        /// <summary>Distance / StraightCost is the distance in cells.</summary>
        public const int StraightCost = 408;
        /// <summary>577/408 stands for the square root of 2: up to 408 cells it orders paths exactly as their true lengths.</summary>
        public const int DiagonalCost = 577;

        // The eight steps, counter-clockwise from +x.
        private static readonly int[] StepX = { 1, 1, 0, -1, -1, -1, 0, 1 };
        private static readonly int[] StepY = { 0, 1, 1, 1, 0, -1, -1, -1 };
        // Turns from the heading in order of preference: ahead, 45° left, 45° right, ... back. Left is from +x towards +y.
        private static readonly int[] TurnOrder = { 0, 1, -1, 2, -2, 3, -3, 4 };
        // Step index by (dx + 1) * 3 + (dy + 1); the centre (no last step) counts as +x.
        private static readonly int[] HeadingIndex = { 5, 4, 3, 6, 0, 2, 7, 0, 1 };

        private readonly GridMap _map;
        private readonly int[] _distance;
        private readonly int[] _queue;
        private readonly bool[] _queued;

        public FlowField(GridMap map)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _distance = new int[map.CellCount];
            _queue = new int[map.CellCount];
            _queued = new bool[map.CellCount];
            for (int i = 0; i < _distance.Length; i++) _distance[i] = Unreachable;
        }

        /// <summary>
        /// With two step costs a cell can improve after its first visit, so it re-enters the queue when it does.
        /// The result does not depend on the visiting order.
        /// </summary>
        public void Compute(GridPos exit)
        {
            int exitIndex = _map.Index(exit);
            for (int i = 0; i < _distance.Length; i++) _distance[i] = Unreachable;
            if (_map.IsBlocked(exit)) return;

            int width = _map.Width;
            int size = _queue.Length;
            int head = 0;
            int count = 1;
            _distance[exitIndex] = 0;
            _queue[0] = exitIndex;
            _queued[exitIndex] = true;

            // Each cell is in the queue at most once, so a ring over one array per cell never overflows.
            while (count > 0)
            {
                int current = _queue[head];
                head = head + 1 == size ? 0 : head + 1;
                count--;
                _queued[current] = false;
                int x = current % width;
                int y = current / width;

                for (int step = 0; step < 8; step++)
                {
                    if (!CanStep(x, y, step)) continue;
                    int candidate = _distance[current] + Cost(step);
                    int neighbour = (y + StepY[step]) * width + x + StepX[step];
                    if (candidate >= _distance[neighbour]) continue;

                    _distance[neighbour] = candidate;
                    if (_queued[neighbour]) continue;
                    _queued[neighbour] = true;
                    int tail = head + count;
                    _queue[tail >= size ? tail - size : tail] = neighbour;
                    count++;
                }
            }
        }

        public bool IsReachable(GridPos pos) => _distance[_map.Index(pos)] != Unreachable;

        /// <summary>In cost units (<see cref="StraightCost"/> per cell); <see cref="Unreachable"/> with no path.</summary>
        public int Distance(GridPos pos) => _distance[_map.Index(pos)];

        /// <summary>Next cell with no last step, which counts as heading +x.</summary>
        public bool TryGetNext(GridPos from, out GridPos next) => TryGetNext(from, from, out next);

        /// <summary>
        /// Next cell for a follower that reached <paramref name="from"/> from <paramref name="cameFrom"/>.
        /// Among the legal steps on a shortest path it takes the smallest turn from its heading, left before right,
        /// so it goes back only when nothing else is as short.
        /// Passing <paramref name="from"/> as <paramref name="cameFrom"/> means no last step.
        /// </summary>
        public bool TryGetNext(GridPos from, GridPos cameFrom, out GridPos next)
        {
            next = from;
            int current = Distance(from);
            if (current == 0 || current == Unreachable) return false;

            int heading = HeadingIndex[(Math.Sign(from.X - cameFrom.X) + 1) * 3 + Math.Sign(from.Y - cameFrom.Y) + 1];
            int width = _map.Width;
            for (int i = 0; i < TurnOrder.Length; i++)
            {
                int step = (heading + TurnOrder[i]) & 7;
                if (!CanStep(from.X, from.Y, step)) continue;
                int x = from.X + StepX[step];
                int y = from.Y + StepY[step];
                // Subtracting keeps clear of overflow when the target is Unreachable.
                if (_distance[y * width + x] != current - Cost(step)) continue;

                next = new GridPos(x, y);
                return true;
            }
            return false;
        }

        private static int Cost(int step) => (step & 1) == 0 ? StraightCost : DiagonalCost;

        /// <summary>The target is on the grid and free and, for a diagonal, so are both cells it passes between.</summary>
        private bool CanStep(int x, int y, int step)
        {
            int dx = StepX[step];
            int dy = StepY[step];
            if (!IsFree(x + dx, y + dy)) return false;
            return (step & 1) == 0 || (IsFree(x + dx, y) && IsFree(x, y + dy));
        }

        private bool IsFree(int x, int y) =>
            x >= 0 && x < _map.Width && y >= 0 && y < _map.Height && !_map.IsBlocked(new GridPos(x, y));
    }
}
