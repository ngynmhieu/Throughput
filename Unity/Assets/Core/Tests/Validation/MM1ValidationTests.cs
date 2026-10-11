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

        private static double Measure(ulong seed, double rho, int passengers) =>
            Experiments.MeasureMeanWait(seed, new Exponential(1.0 / (rho * Mu)), new Exponential(1.0 / Mu), staff: 1, passengers);

        [TestCase(0.5)]
        [TestCase(0.8)]
        public void Fast_MeanWaitWithin10Percent(double rho)
        {
            double measured = Measure(7UL, rho, 200_000);
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
            StrictCheck.AssertMeanMatches(seed => Measure(seed, rho, 300_000), Theory.MM1Wq(rho * Mu, Mu), $"M/M/1 ρ = {rho}");
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
