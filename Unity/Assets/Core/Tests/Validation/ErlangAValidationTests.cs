using System.Collections.Generic;
using NUnit.Framework;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Validation
{
    /// <summary>
    /// The engine against Erlang A (M/M/c+M): c desks, exponential patience with rate θ
    /// (docs/game-mechanism/knowledge/formulas/erlang-a.md). μ = 1. Checks both the mean time in line
    /// (over everyone, including those who gave up) and the share who gave up.
    /// </summary>
    public class ErlangAValidationTests
    {
        private const double Mu = 1.0;

        [TestCase(2, 1.8, 0.5)]  // ρ = 0.9
        [TestCase(3, 3.6, 1.0)]  // ρ = 1.2: overloaded, only reneging keeps it finite
        [TestCase(1, 1.0, 0.2)]  // ρ = 1.0, very patient
        public void Fast_WaitAndAbandonWithin10Percent(int c, double lambda, double theta)
        {
            var (wait, abandon) = Experiments.MeasureErlangA(7UL, lambda, Mu, c, theta, 200_000);
            ErlangAResult theory = Theory.ErlangA(lambda, Mu, c, theta);
            string detail = $"c = {c}, λ = {lambda}, θ = {theta}: wait {wait:F4} vs {theory.Wq:F4}, gave up {abandon:F4} vs {theory.ProbAbandon:F4}";
            TestContext.WriteLine(detail);
            Assert.That(wait, Is.EqualTo(theory.Wq).Within(10).Percent, detail);
            Assert.That(abandon, Is.EqualTo(theory.ProbAbandon).Within(10).Percent, detail);
        }

        [Test, Category("Strict")]
        [TestCase(2, 1.8, 0.5)]
        [TestCase(3, 3.6, 1.0)]
        [TestCase(1, 1.0, 0.2)]
        public void Strict_WaitAndAbandonWithin3PercentAndInsideCI(int c, double lambda, double theta)
        {
            ErlangAResult theory = Theory.ErlangA(lambda, Mu, c, theta);
            string label = $"Erlang A c = {c}, λ = {lambda}, θ = {theta}";
            StrictCheck.AssertMeanMatches(seed => Experiments.MeasureErlangA(seed, lambda, Mu, c, theta, 300_000).Wait,
                theory.Wq, label + " wait");
            StrictCheck.AssertMeanMatches(seed => Experiments.MeasureErlangA(seed, lambda, Mu, c, theta, 300_000).Abandon,
                theory.ProbAbandon, label + " gave up");
        }

        [Test]
        public void Invariants_HoldWithReneging()
        {
            // Includes Little's Law with renegers: queue area = total time in line, givers-up included.
            var log = Experiments.RunSingleStation(11UL, new Exponential(1.0 / 2.4), new Exponential(1.0), staff: 2,
                stopTime: 5_000.0, keepEntries: true, patience: new Exponential(2.0));
            InvariantChecker.AssertAll(log, new Dictionary<string, int> { { "desk", 2 } });
        }
    }
}
