using System;
using System.Collections.Generic;

namespace Throughput.Core.Sim.Randomness
{
    /// <summary>
    /// One independent random generator per named source of randomness ("arrivals:entry",
    /// "service:desk", ...), all derived from the run seed. Because each stream only depends on
    /// (run seed, name), using one stream more or less never shifts another (spec, Randomness).
    /// </summary>
    public sealed class RngStreams
    {
        private readonly Dictionary<string, Pcg32> _streams = new Dictionary<string, Pcg32>();

        public RngStreams(ulong runSeed)
        {
            RunSeed = runSeed;
        }

        public ulong RunSeed { get; }

        public Pcg32 Get(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("A random stream needs a name.", nameof(name));

            if (!_streams.TryGetValue(name, out Pcg32 rng))
            {
                rng = new Pcg32(RunSeed, StreamId(name));
                _streams[name] = rng;
            }
            return rng;
        }

        /// <summary>FNV-1a 64-bit hash. Never use string.GetHashCode: it changes between runs in .NET.</summary>
        public static ulong StreamId(string name)
        {
            ulong hash = 14695981039346656037UL;
            foreach (char c in name)
            {
                hash ^= c;
                hash = unchecked(hash * 1099511628211UL);
            }
            return hash;
        }
    }
}
