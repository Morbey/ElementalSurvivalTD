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
        /// <summary>In gold.</summary>
        public readonly int Cost;
        /// <summary>Dealt by its shots; none deals x1.</summary>
        public readonly Element Element;

        public TowerStats(float range, float damage, float shotsPerSecond, float projectileSpeed, int cost = 0, Element element = default)
        {
            if (range < 0f) throw new ArgumentOutOfRangeException(nameof(range), range, "Range cannot be negative.");
            if (damage < 0f) throw new ArgumentOutOfRangeException(nameof(damage), damage, "Damage cannot be negative.");
            if (shotsPerSecond <= 0f) throw new ArgumentOutOfRangeException(nameof(shotsPerSecond), shotsPerSecond, "Shots per second must be above 0.");
            if (projectileSpeed <= 0f) throw new ArgumentOutOfRangeException(nameof(projectileSpeed), projectileSpeed, "Projectile speed must be above 0.");
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost), cost, "Cost cannot be negative.");
            Range = range;
            Damage = damage;
            ShotsPerSecond = shotsPerSecond;
            ProjectileSpeed = projectileSpeed;
            Cost = cost;
            Element = element;
        }
    }
}
