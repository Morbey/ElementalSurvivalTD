namespace ElementalTD.Core
{
    /// <summary>Outcome of a tower placement, in the order the rule checks it.</summary>
    public enum PlacementResult
    {
        Placed,
        /// <summary>Checked by the battle before the board rule.</summary>
        NotEnoughGold,
        OutOfBounds,
        SpawnOrExit,
        /// <summary>A fixed wall or an existing tower.</summary>
        CellBlocked,
        EnemyInTheWay,
        WouldBlockPath
    }
}
