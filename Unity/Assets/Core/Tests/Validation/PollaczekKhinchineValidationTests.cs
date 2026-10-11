using System.Collections.Generic;
using NUnit.Framework;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Validation
{
    /// <summary>
    /// The engine against Pollaczek–Khinchine (M/G/1): random arrivals, any service CV
    /// (docs/game-mechanism/knowledge/formulas/pollaczek-khinchine.md). E[S] = 1, λ = ρ.
    /// </summary>
    public class PollaczekKhinchineValidationTests
    {
        private const double MeanService = 1.0;

        private static double Measure(ulong seed, double rho, double cs, int passengers) =>
            Experiments.MeasureMeanWait(seed, new Exponential(MeanService / rho), Distribution.FromMeanCv(MeanService, cs),
                staff: 1, passengers);

        private static double TheoryWq(double rho, double cs) => Theory.PollaczekKhinchineWq(rho / MeanService, MeanService, cs);

        [TestCase(0.0)]
        [TestCase(0.5)]
        [TestCase(1.0)]
        public void Fast_MeanWaitWithin10Percent(double cs)
        {
            const double rho = 0.8;
            double measured = Measure(7UL, rho, cs, 200_000);
            double theory = TheoryWq(rho, cs);
            string detail = $"Cs = {cs}, ρ = {rho}: measured {measured:F4} vs theory {theory:F4}";
            TestContext.WriteLine(detail);
            Assert.That(measured, Is.EqualTo(theory).Within(10).Percent, detail);
        }

        [Test, Category("Strict")]
        [TestCase(0.8, 0.0)]
        [TestCase(0.8, 0.5)]
        [TestCase(0.8, 1.0)]
        [TestCase(0.9, 0.0)]
        [TestCase(0.9, 0.5)]
        [TestCase(0.9, 1.0)]
        public void Strict_MeanWaitWithin3PercentAndInsideCI(double rho, double cs)
        {
            StrictCheck.AssertMeanMatches(seed => Measure(seed, rho, cs, 300_000), TheoryWq(rho, cs), $"P-K ρ = {rho}, Cs = {cs}");
        }

        [Test]
        public void Invariants_HoldWithDeterministicService()
        {
            // Cs = 0: every service is exactly 1, so many events land on the same instant.
            var log = Experiments.RunSingleStation(11UL, new Exponential(1.25), new Deterministic(1.0),
                staff: 1, stopTime: 5_000.0, keepEntries: true);
            InvariantChecker.AssertAll(log, new Dictionary<string, int> { { "desk", 1 } });
        }
    }
}
