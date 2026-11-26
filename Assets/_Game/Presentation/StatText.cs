using System;

namespace ElementalTD.Presentation
{
    /// <summary>
    /// The numbers a stat shows, before any text (visual direction 1.5 and 1.7). A float is cast to decimal, which keeps
    /// seven significant figures, so 1.5 x1.15, held as 1.7249999, rounds as 1.725 (plan 0012, reading 7). Results carry
    /// no trailing zeros, so they print with the fewest digits. Allocates nothing.
    /// </summary>
    public static class StatText
    {
        private const int SignificantFigures = 3;
        // Dividing by a one of the largest scale gives the same value with the smallest scale.
        private const decimal One = 1.0000000000000000000000000000m;

        /// <summary>The value to three significant figures, half up.</summary>
        public static decimal Shown(float value)
        {
            decimal exact = (decimal)value;
            if (exact == 0m) return 0m;

            decimal magnitude = Math.Abs(exact);
            int decimals = SignificantFigures - 1;
            while (magnitude >= 10m)
            {
                magnitude /= 10m;
                decimals--;
            }
            while (magnitude < 1m)
            {
                magnitude *= 10m;
                decimals++;
            }

            // A decimal holds at most 28 decimals.
            if (decimals >= 0) return Math.Round(exact, Math.Min(decimals, 28), MidpointRounding.AwayFromZero) / One;

            decimal step = 1m;
            for (int i = decimals; i < 0; i++) step *= 10m;
            return Math.Round(exact / step, 0, MidpointRounding.AwayFromZero) * step / One;
        }

        /// <summary>A multiplication as the whole percent it adds, half up: 1.25 is 25, 1.5625 is 56.</summary>
        public static decimal Percent(float multiplier) =>
            Math.Round(((decimal)multiplier - 1m) * 100m, 0, MidpointRounding.AwayFromZero) / One;

        /// <summary>The shown value less the shown base: 0 when the two show the same.</summary>
        public static decimal Difference(float value, float baseValue) => (Shown(value) - Shown(baseValue)) / One;
    }
}
