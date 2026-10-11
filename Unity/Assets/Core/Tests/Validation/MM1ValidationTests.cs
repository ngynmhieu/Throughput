using System.Collections.Generic;
using NUnit.Framework;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Validation
{
    /// <summary>
    /// The engine against the M/M/1 formula Wq = ρ / (μ − λ) (docs/game-mechanism/knowledge/formulas/mm1.md).
    /// μ = 1 per time unit, so λ = ρ.
    /// </summary>
    public class MM1ValidationTests
    {
        private const double Mu = 1.0;

        private static double MeasuredMeanWait(ulong seed, double rho, int passengers)
        {
            double lambda = rho * Mu;
            var log = Experiments.RunSingleStation(seed, new Exponential(1.0 / lambda), new Exponential(1.0 / Mu),
                staff: 1, stopTime: passengers / lambda, keepEntries: false);
            return Experiments.MeanWait(log, warmup: 1000.0 / lambda); // skip ~1000 arrivals: the line starts empty
        }

        [TestCase(0.5)]
        [TestCase(0.8)]
        public void Fast_MeanWaitWithin10Percent(double rho)
        {
            double measured = MeasuredMeanWait(7UL, rho, 200_000);
            double theory = Theory.MM1Wq(rho * Mu, Mu);
            string detail = $"ρ = {rho}: measured {measured:F3} vs theory {theory:F3}";
            TestContext.WriteLine(detail);
            Assert.That(measured, Is.EqualTo(theory).Within(10).Percent, detail);
        }

        [Test, Category("Strict")]
        [TestCase(0.5)]
        [TestCase(0.8)]
        [TestCase(0.9)]
        public void Strict_MeanWaitWithin3PercentAndInsideCI(double rho)
        {
            const int replications = 20;
            const int passengers = 300_000;
            var means = new List<double>();
            for (int i = 0; i < replications; i++)
                means.Add(MeasuredMeanWait((ulong)(1000 + i), rho, passengers));

            double theory = Theory.MM1Wq(rho * Mu, Mu);
            double mean = Stats.Mean(means);
            var (low, high) = Stats.ConfidenceInterval99(means);
            string detail = $"ρ = {rho}: measured {mean:F4} (99% CI {low:F4}–{high:F4}) vs theory {theory:F4}";
            TestContext.WriteLine(detail);

            Assert.That(theory, Is.InRange(low, high), detail);
            Assert.That(mean, Is.EqualTo(theory).Within(3).Percent, detail);
        }

        [Test]
        public void Invariants_HoldOnRandomRun()
        {
            var log = Experiments.RunSingleStation(11UL, new Exponential(1.25), new Exponential(1.0),
                staff: 1, stopTime: 5_000.0, keepEntries: true);
            InvariantChecker.AssertAll(log, new Dictionary<string, int> { { "desk", 1 } });
        }
    }
}
