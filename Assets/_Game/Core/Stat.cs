namespace ElementalTD.Core
{
    /// <summary>What a modifier changes (ADR 0007). Serialized on assets by value: append only, never reorder.</summary>
    public enum Stat
    {
        TowerDamage,
        TowerShotsPerSecond,
        TowerRange,
        GoldPerKill
    }
}
