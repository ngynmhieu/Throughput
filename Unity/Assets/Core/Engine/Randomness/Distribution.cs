using System;

namespace Throughput.Core.Engine.Randomness
{
    public static class Distribution
    {
        /// <summary>The simplest distribution with this mean and CV: 0 → Deterministic, 1 → Exponential, else Gamma.</summary>
        public static IDistribution FromMeanCv(double mean, double cv)
        {
            if (!(mean > 0))
                throw new ArgumentOutOfRangeException(nameof(mean), mean, "Mean must be above 0.");
            if (!(cv >= 0))
                throw new ArgumentOutOfRangeException(nameof(cv), cv, "CV can't be negative.");

            if (cv == 0.0) return new Deterministic(mean);
            if (cv == 1.0) return new Exponential(mean);
            return new GammaDistribution(mean, cv);
        }
    }
}
