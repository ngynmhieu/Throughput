using System;

namespace Throughput.Core.Engine.Randomness
{
    /// <summary>
    /// A check that always takes at least Min, plus a random part: Min + inner sample.
    /// For game levels (realistic times). Formula tests use pure distributions instead.
    /// </summary>
    public sealed class Shifted : IDistribution
    {
        private readonly IDistribution _inner;

        public Shifted(double min, IDistribution inner)
        {
            if (!(min >= 0) || double.IsInfinity(min))
                throw new ArgumentOutOfRangeException(nameof(min), min, "Min must be 0 or more.");
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            Min = min;
        }

        public double Min { get; }

        public double Mean => Min + _inner.Mean;

        /// <summary>The spread is the random part's; the mean is bigger, so the CV is smaller.</summary>
        public double Cv => _inner.Cv * _inner.Mean / Mean;

        public double Sample(Pcg32 rng) => Min + _inner.Sample(rng);
    }
}
