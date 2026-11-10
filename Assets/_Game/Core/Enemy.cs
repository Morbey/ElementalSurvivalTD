using System;

namespace ElementalTD.Core
{
    /// <summary>
    /// One enemy: a path follower with health. Positions are in cell coordinates (a cell's centre is its x, y).
    /// </summary>
    public sealed class Enemy
    {
        public PathFollower Follower { get; }
        public float Speed { get; }
        public float MaxHealth { get; }
        /// <summary>Keeps its last value while off the board.</summary>
        public float Health { get; private set; }
        public bool IsOnBoard { get; internal set; }
        public float X { get; private set; }
        public float Y { get; private set; }
        /// <summary>Position at the previous tick.</summary>
        public float PreviousX { get; private set; }
        public float PreviousY { get; private set; }
        /// <summary>Counts entries, so a shot can tell a re-entered enemy from the one it was fired at.</summary>
        internal int Entry { get; private set; }

        /// <summary>Remaining path in cells: the distance of the cell it is heading to plus the unfinished step.</summary>
        public float DistanceToExit => Follower.DistanceToExit;

        public Enemy(FlowField field, GridPos start, in EnemyStats stats)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            Follower = new PathFollower(field, start);
            Speed = stats.Speed;
            MaxHealth = stats.MaxHealth;
            Health = MaxHealth;
            SyncPosition();
            KeepPosition();
        }

        internal void Enter(GridPos spawn)
        {
            Follower.Restart(spawn);
            Health = MaxHealth;
            SyncPosition();
            KeepPosition();
            IsOnBoard = true;
            Entry++;
        }

        /// <summary>Returns true on arrival at the exit.</summary>
        internal bool Step(float dt)
        {
            KeepPosition();
            bool arrived = Follower.Step(dt, Speed);
            SyncPosition();
            return arrived;
        }

        internal void TakeDamage(float damage) => Health -= damage;

        private void KeepPosition()
        {
            PreviousX = X;
            PreviousY = Y;
        }

        private void SyncPosition()
        {
            GridPos from = Follower.From;
            GridPos to = Follower.To;
            float t = Follower.Progress;
            X = from.X + (to.X - from.X) * t;
            Y = from.Y + (to.Y - from.Y) * t;
        }
    }
}
