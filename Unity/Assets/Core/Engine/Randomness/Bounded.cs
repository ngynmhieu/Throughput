using System;

namespace Throughput.Core.Engine.Randomness
{
    /// <summary>
    /// A safety net for game levels: samples outside [Min, Max] are drawn again. It should rarely trigger.
    /// Mean and Cv report the inner distribution's (approximate: the cut-off changes them slightly).
    /// </summary>
    public sealed class Bounded : IDistribution
    {
        private const int MaxTries = 10_000;
        private readonly IDistribution _inner;

        public Bounded(double min, double max, IDistribution inner)
        {
            if (!(min >= 0) || double.IsInfinity(min))
                throw new ArgumentOutOfRangeException(nameof(min), min, "Min must be 0 or more.");
            if (!(max > min))
                throw new ArgumentOutOfRangeException(nameof(max), max, $"Max must be above min ({min}).");
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            Min = min;
            Max = max;
        }

        public double Min { get; }

        public double Max { get; }

        public double Mean => _inner.Mean;

        public double Cv => _inner.Cv;

        public double Sample(Pcg32 rng)
        {
            for (int i = 0; i < MaxTries; i++)
            {
                double x = _inner.Sample(rng);
                if (x >= Min && x <= Max) return x;
            }
            throw new InvalidOperationException(
                $"No sample landed in [{Min}, {Max}] after {MaxTries} tries (mean {Mean}): the range is almost impossible.");
        }
    }
}
