using System;

namespace ElementalTD.Core
{
    public readonly struct EnemyStats
    {
        public readonly float Speed;
        public readonly float MaxHealth;
        /// <summary>Gold paid when it is killed.</summary>
        public readonly int GoldReward;
        /// <summary>None takes x1 and is never immune.</summary>
        public readonly Element Element;

        public EnemyStats(float speed, float maxHealth, int goldReward = 0, Element element = default)
        {
            if (speed < 0f) throw new ArgumentOutOfRangeException(nameof(speed), speed, "Speed cannot be negative.");
            if (maxHealth <= 0f) throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "Maximum health must be above 0.");
            if (goldReward < 0) throw new ArgumentOutOfRangeException(nameof(goldReward), goldReward, "Gold reward cannot be negative.");
            Speed = speed;
            MaxHealth = maxHealth;
            GoldReward = goldReward;
            Element = element;
        }
    }
}
