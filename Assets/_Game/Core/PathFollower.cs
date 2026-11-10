using System;

namespace ElementalTD.Core
{
    /// <summary>
    /// Walks a flow field cell centre to cell centre. Grid coordinates only; the caller maps them to the world.
    /// </summary>
    public sealed class PathFollower
    {
        private readonly FlowField _field;

        public GridPos From { get; private set; }
        public GridPos To { get; private set; }
        public float Progress { get; private set; }
        public bool HasArrived { get; private set; }

        public PathFollower(FlowField field, GridPos start)
        {
            _field = field ?? throw new ArgumentNullException(nameof(field));
            From = start;
            To = field.TryGetNext(start, out GridPos next) ? next : start;
            Progress = 0f;
            HasArrived = From == To && field.Distance(start) == 0;
        }

        public bool Step(float dt, float speedCellsPerSecond)
        {
            if (HasArrived || From == To) return HasArrived;

            float advance = dt * speedCellsPerSecond;
            if (advance <= 0f) return false;

            Progress += advance;
            while (Progress >= 1f)
            {
                Progress -= 1f;
                From = To;
                if (_field.TryGetNext(From, out GridPos next))
                {
                    To = next;
                }
                else
                {
                    To = From;
                    Progress = 0f;
                    HasArrived = _field.Distance(From) == 0;
                    break;
                }
            }

            return HasArrived;
        }
    }
}
