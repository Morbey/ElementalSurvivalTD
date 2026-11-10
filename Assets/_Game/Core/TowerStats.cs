using System;

namespace ElementalTD.Core
{
    public readonly struct TowerStats
    {
        /// <summary>In cells, from the tower's cell centre; inclusive.</summary>
        public readonly float Range;
        public readonly float Damage;
        public readonly float ShotsPerSecond;
        /// <summary>In cells per second.</summary>
        public readonly float ProjectileSpeed;

        public TowerStats(float range, float damage, float shotsPerSecond, float projectileSpeed)
        {
            if (range < 0f) throw new ArgumentOutOfRangeException(nameof(range), range, "Range cannot be negative.");
            if (damage < 0f) throw new ArgumentOutOfRangeException(nameof(damage), damage, "Damage cannot be negative.");
            if (shotsPerSecond <= 0f) throw new ArgumentOutOfRangeException(nameof(shotsPerSecond), shotsPerSecond, "Shots per second must be above 0.");
            if (projectileSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(projectileSpeed), projectileSpeed, "Projectile speed must be above 0.");
            Range = range;
            Damage = damage;
            ShotsPerSecond = shotsPerSecond;
            ProjectileSpeed = projectileSpeed;
        }
    }
}
