using System;

namespace Throughput.Core.Sim.Randomness
{
    /// <summary>Fully random ("memoryless") durations, CV = 1. The "M" in M/M/1.</summary>
    public sealed class Exponential : IDistribution
    {
        public Exponential(double mean)
        {
            if (!(mean > 0))
                throw new ArgumentOutOfRangeException(nameof(mean), mean, "Mean must be above 0.");
            Mean = mean;
        }

        public double Mean { get; }

        public double Cv => 1.0;

        public double Sample(Pcg32 rng) => -Mean * Math.Log(rng.NextOpenDouble());
    }
}
