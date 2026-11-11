namespace ElementalTD.Core
{
    public sealed class Tower
    {
        public GridPos Cell { get; }
        public TowerStats Stats { get; }

        /// <summary>Seconds until it may fire again; 0 or below means ready.</summary>
        internal float Cooldown;

        /// <summary>True from its first launch on; from then a sale refunds only part of its cost.</summary>
        internal bool HasFired;

        internal Tower(GridPos cell, in TowerStats stats)
        {
            Cell = cell;
            Stats = stats;
        }
    }
}
