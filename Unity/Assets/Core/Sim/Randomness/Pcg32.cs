namespace Throughput.Core.Sim.Randomness
{
    /// <summary>
    /// PCG32 random number generator (Melissa O'Neill, pcg-random.org).
    /// Small, fast, and gives the same numbers on every platform. We never use System.Random,
    /// whose output can change between .NET versions (spec S-D3).
    /// </summary>
    public sealed class Pcg32
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private const double ToUnit = 1.0 / 4294967296.0; // 2^-32

        private ulong _state;
        private readonly ulong _increment;

        /// <param name="seed">Starting point, e.g. the run seed.</param>
        /// <param name="stream">Which of 2^63 independent sequences to use.</param>
        public Pcg32(ulong seed, ulong stream)
        {
            _state = 0UL;
            _increment = (stream << 1) | 1UL;
            NextUInt();
            _state = unchecked(_state + seed);
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * Multiplier + _increment);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rotation = (int)(old >> 59);
            return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
        }

        /// <summary>A number in [0, 1).</summary>
        public double NextDouble() => NextUInt() * ToUnit;

        /// <summary>A number in (0, 1): never exactly 0 or 1, so it's safe inside Math.Log.</summary>
        public double NextOpenDouble() => (NextUInt() + 0.5) * ToUnit;
    }
}
