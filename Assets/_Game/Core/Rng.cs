using System;

namespace ElementalTD.Core
{
    /// <summary>
    /// A seeded generator on 32-bit whole numbers (SplitMix, ADR 0007), stated exactly in rule 20 of plan 0012 so the
    /// balance port can copy it. Whatever draws owns one; nothing shares a stream.
    /// </summary>
    public sealed class Rng
    {
        private uint _state;

        public Rng(uint seed)
        {
            _state = seed;
        }

        public uint Next()
        {
            unchecked
            {
                _state += 0x9E3779B9u;
                uint z = _state;
                z ^= z >> 16;
                z *= 0x85EBCA6Bu;
                z ^= z >> 13;
                z *= 0xC2B2AE35u;
                z ^= z >> 16;
                return z;
            }
        }

        /// <summary>A whole number from 0 to count - 1, from the 64-bit product rather than a remainder.</summary>
        public int Below(int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, "Below needs a count of at least 1.");
            return (int)(((ulong)Next() * (ulong)count) >> 32);
        }
    }
}
