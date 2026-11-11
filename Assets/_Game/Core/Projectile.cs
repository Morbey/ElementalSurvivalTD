using System;

namespace ElementalTD.Core
{
    /// <summary>A shot homing on one enemy. Reused through the battle's free list.</summary>
    public sealed class Projectile
    {
        public float X { get; private set; }
        public float Y { get; private set; }
        /// <summary>Position at the previous tick.</summary>
        public float PreviousX { get; private set; }
        public float PreviousY { get; private set; }

        // Copied at launch, so a shot keeps flying after its tower is removed.
        internal Enemy Target { get; private set; }
        internal float Damage { get; private set; }
        /// <summary>Its tower's at launch.</summary>
        public Element Element { get; private set; }
        private float _speed;
        private int _targetEntry;

        /// <summary>True once the target has left the board, even if its object has since been reused by another enemy.</summary>
        internal bool TargetGone => !Target.IsOnBoard || Target.Entry != _targetEntry;

        internal void Launch(GridPos from, Enemy target, in TowerStats stats)
        {
            X = from.X;
            Y = from.Y;
            PreviousX = X;
            PreviousY = Y;
            Target = target;
            _targetEntry = target.Entry;
            Damage = stats.Damage;
            Element = stats.Element;
            _speed = stats.ProjectileSpeed;
        }

        /// <summary>Moves towards the target's current position; returns true when it is within this step.</summary>
        internal bool Advance(float dt)
        {
            PreviousX = X;
            PreviousY = Y;

            float dx = Target.X - X;
            float dy = Target.Y - Y;
            float step = _speed * dt;
            float distanceSquared = dx * dx + dy * dy;
            if (distanceSquared <= step * step)
            {
                X = Target.X;
                Y = Target.Y;
                return true;
            }

            float scale = step / MathF.Sqrt(distanceSquared);
            X += dx * scale;
            Y += dy * scale;
            return false;
        }

        internal void Release() => Target = null;
    }
}
