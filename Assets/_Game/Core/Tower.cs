namespace ElementalTD.Core
{
    public sealed class Tower
    {
        public GridPos Cell { get; }
        /// <summary>The stats it was bought with; the cost and the sell value read these.</summary>
        public TowerStats BaseStats { get; }
        /// <summary>
        /// Effective: the base stats with the run's modifiers, cached so a tick reads a struct (ADR 0007).
        /// The base until the battle sets it; only the battle writes it.
        /// </summary>
        public TowerStats Stats { get; internal set; }

        /// <summary>Seconds until it may fire again; 0 or below means ready.</summary>
        internal float Cooldown;

        /// <summary>True from its first launch on; from then a sale refunds only part of its cost.</summary>
        internal bool HasFired;

        internal Tower(GridPos cell, in TowerStats stats)
        {
            Cell = cell;
            BaseStats = stats;
            Stats = stats;
        }
    }
}
