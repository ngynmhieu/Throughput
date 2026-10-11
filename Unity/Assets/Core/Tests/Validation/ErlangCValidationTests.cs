using System.Collections.Generic;
using NUnit.Framework;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Validation
{
    /// <summary>
    /// The engine against Erlang C (M/M/c): c desks share one line (docs/game-mechanism/knowledge/formulas/erlang-c.md).
    /// μ = 1 per desk, λ = ρ × c.
    /// </summary>
    public class ErlangCValidationTests
    {
        private const double Mu = 1.0;

        private static double Measure(ulong seed, int c, double rho, int passengers) =>
            Experiments.MeasureMeanWait(seed, new Exponential(1.0 / (rho * c * Mu)), new Exponential(1.0 / Mu), staff: c, passengers);

        private static double TheoryWq(int c, double rho) => Theory.ErlangCWq(rho * c * Mu, Mu, c);

        [TestCase(2, 0.7)]
        [TestCase(3, 0.7)]
        [TestCase(5, 0.7)]
        public void Fast_MeanWaitWithin10Percent(int c, double rho)
        {
            double measured = Measure(7UL, c, rho, 200_000);
            double theory = TheoryWq(c, rho);
            string detail = $"c = {c}, ρ = {rho}: measured {measured:F4} vs theory {theory:F4}";
            TestContext.WriteLine(detail);
            Assert.That(measured, Is.EqualTo(theory).Within(10).Percent, detail);
        }

        [Test, Category("Strict")]
        [TestCase(2, 0.7)]
        [TestCase(3, 0.7)]
        [TestCase(5, 0.7)]
        [TestCase(2, 0.9)]
        [TestCase(3, 0.9)]
        [TestCase(5, 0.9)]
        public void Strict_MeanWaitWithin3PercentAndInsideCI(int c, double rho)
        {
            StrictCheck.AssertMeanMatches(seed => Measure(seed, c, rho, 300_000), TheoryWq(c, rho), $"Erlang C c = {c}, ρ = {rho}");
        }

        [TestCase(3, 0.8)]
        [TestCase(5, 0.3)] // more desks than people most of the time
        public void Invariants_HoldOnRandomRun(int c, double rho)
        {
            var log = Experiments.RunSingleStation(11UL, new Exponential(1.0 / (rho * c * Mu)), new Exponential(1.0 / Mu),
                staff: c, stopTime: 5_000.0, keepEntries: true);
            InvariantChecker.AssertAll(log, new Dictionary<string, int> { { "desk", c } });
        }
    }
}
