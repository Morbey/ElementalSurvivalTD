using System;

namespace ElementalTD.Core
{
    /// <summary>
    /// One enemy: a path follower with health. Positions are in cell coordinates (a cell's centre is its x, y).
    /// </summary>
    public sealed class Enemy
    {
        public PathFollower Follower { get; }
        public float Speed { get; private set; }
        public float MaxHealth { get; private set; }
        /// <summary>Keeps its last value while off the board.</summary>
        public float Health { get; private set; }
        public int GoldReward { get; private set; }
        /// <summary>The index of the wave it entered with; -1 outside a wave.</summary>
        public int WaveIndex { get; private set; }
        public bool IsOnBoard { get; internal set; }
        public float X { get; private set; }
        public float Y { get; private set; }
        /// <summary>Position at the previous tick.</summary>
        public float PreviousX { get; private set; }
        public float PreviousY { get; private set; }
        /// <summary>Counts entries, so a shot can tell the enemy it was fired at from a later one that reuses this object.</summary>
        internal int Entry { get; private set; }

        /// <summary>Remaining path in cells: the distance of the cell it is heading to plus the unfinished step.</summary>
        public float DistanceToExit => Follower.DistanceToExit;

        /// <summary>Created off the board by <see cref="Board.SpawnEnemy"/>; its stats come with each entry.</summary>
        internal Enemy(FlowField field, GridPos start)
        {
            if (field == null) throw new ArgumentNullException(nameof(field));
            Follower = new PathFollower(field, start);
        }

        internal void Enter(GridPos spawn, in EnemyStats stats, int waveIndex)
        {
            WaveIndex = waveIndex;
            Speed = stats.Speed;
            MaxHealth = stats.MaxHealth;
            Health = MaxHealth;
            GoldReward = stats.GoldReward;
            Follower.Restart(spawn);
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
            float t = Follower.Progress / Follower.StepLength;
            X = from.X + (to.X - from.X) * t;
            Y = from.Y + (to.Y - from.Y) * t;
        }
    }
}
