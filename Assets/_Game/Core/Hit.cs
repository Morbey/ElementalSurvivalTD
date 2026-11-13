namespace ElementalTD.Core
{
    /// <summary>
    /// One record of the battle's hit log (ADR 0006). It names its target by entry number, not by object, so a hit that kills still shows.
    /// </summary>
    public readonly struct Hit
    {
        /// <summary>The target's position in cells on the tick the shot landed.</summary>
        public readonly float X, Y;
        /// <summary>The damage dealt: the shot's damage times the multiplier, not cut to the health left.</summary>
        public readonly float Amount;
        /// <summary>Never Immune in the log.</summary>
        public readonly HitKind Kind;
        /// <summary>Battle.TickCount on that tick.</summary>
        public readonly int Tick;
        /// <summary>The target's Enemy.Entry: names the enemy hit, also once its object is another enemy's.</summary>
        public readonly int TargetEntry;

        public Hit(float x, float y, float amount, HitKind kind, int tick, int targetEntry)
        {
            X = x;
            Y = y;
            Amount = amount;
            Kind = kind;
            Tick = tick;
            TargetEntry = targetEntry;
        }
    }
}
