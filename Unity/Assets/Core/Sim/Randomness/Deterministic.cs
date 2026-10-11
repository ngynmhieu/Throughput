using System;

namespace Throughput.Core.Sim.Randomness
{
    /// <summary>Always the same duration, CV = 0. The "D" in M/D/1. Uses no random numbers.</summary>
    public sealed class Deterministic : IDistribution
    {
        public Deterministic(double value)
        {
            if (!(value > 0))
                throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be above 0.");
            Mean = value;
        }

        public double Mean { get; }

        public double Cv => 0.0;

        public double Sample(Pcg32 rng) => Mean;
    }
}
