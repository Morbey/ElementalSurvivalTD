using System;

namespace ElementalTD.Core
{
    public readonly struct EnemyStats
    {
        public readonly float Speed;
        public readonly float MaxHealth;

        public EnemyStats(float speed, float maxHealth)
        {
            if (speed < 0f) throw new ArgumentOutOfRangeException(nameof(speed), speed, "Speed cannot be negative.");
            if (maxHealth <= 0f) throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "Maximum health must be above 0.");
            Speed = speed;
            MaxHealth = maxHealth;
        }
    }
}
