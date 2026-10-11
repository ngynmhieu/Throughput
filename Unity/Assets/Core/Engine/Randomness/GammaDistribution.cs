using System;

namespace Throughput.Core.Engine.Randomness
{
    /// <summary>
    /// Durations with any chosen mean and CV (spec Q2: gamma). Shape k = 1/CV², scale = mean/k.
    /// CV below 1 = more regular than exponential (a scanner belt), above 1 = messier.
    /// </summary>
    public sealed class GammaDistribution : IDistribution
    {
        private readonly double _shape;
        private readonly double _scale;

        public GammaDistribution(double mean, double cv)
        {
            if (!(mean > 0))
                throw new ArgumentOutOfRangeException(nameof(mean), mean, "Mean must be above 0.");
            if (!(cv > 0))
                throw new ArgumentOutOfRangeException(nameof(cv), cv, "Gamma needs a CV above 0. Use Deterministic for CV 0.");
            Mean = mean;
            Cv = cv;
            _shape = 1.0 / (cv * cv);
            _scale = mean / _shape;
        }

        public double Mean { get; }

        public double Cv { get; }

        public double Sample(Pcg32 rng) => SampleStandard(_shape, rng) * _scale;

        // Marsaglia & Tsang (2000). For shape < 1: Gamma(k) = Gamma(k + 1) * U^(1/k).
        private static double SampleStandard(double shape, Pcg32 rng)
        {
            if (shape < 1.0)
            {
                double boosted = SampleStandard(shape + 1.0, rng);
                return boosted * Math.Pow(rng.NextOpenDouble(), 1.0 / shape);
            }

            double d = shape - 1.0 / 3.0;
            double c = 1.0 / Math.Sqrt(9.0 * d);
            while (true)
            {
                double x = StandardNormal(rng);
                double v = 1.0 + c * x;
                if (v <= 0.0)
                    continue;
                v = v * v * v;
                double u = rng.NextOpenDouble();
                if (Math.Log(u) < 0.5 * x * x + d - d * v + d * Math.Log(v))
                    return d * v;
            }
        }

        // Box–Muller, using one of the pair (keeps the generator stateless between samples).
        private static double StandardNormal(Pcg32 rng)
        {
            double u1 = rng.NextOpenDouble();
            double u2 = rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }
    }
}
