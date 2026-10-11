using System.Collections.Generic;
using NUnit.Framework;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Validation
{
    /// <summary>
    /// The engine against Kingman's G/G/1 approximation (docs/game-mechanism/knowledge/formulas/kingman.md).
    /// Arrival gaps have CV Ca, service has CV Cs, E[S] = 1, λ = ρ. Approximate: ±15% at ρ ≥ 0.8.
    /// </summary>
    public class KingmanValidationTests
    {
        private const double MeanService = 1.0;
        private const int Seeds = 5;
        private const int Passengers = 300_000;

        private static double AverageMeasured(double rho, double ca, double cs)
        {
            var means = new List<double>();
            for (int i = 0; i < Seeds; i++)
                means.Add(Experiments.MeasureMeanWait((ulong)(500 + i), Distribution.FromMeanCv(MeanService / rho, ca),
                    Distribution.FromMeanCv(MeanService, cs), staff: 1, Passengers));
            return Stats.Mean(means);
        }

        private static double TheoryWq(double rho, double ca, double cs) => Theory.KingmanWq(rho / MeanService, MeanService, ca, cs);

        [TestCase(0.9, 0.5, 0.5)]
        [TestCase(0.9, 0.5, 1.0)]
        [TestCase(0.9, 1.5, 0.5)]
        [TestCase(0.8, 1.0, 0.5)]
        [TestCase(0.8, 1.5, 1.0)]
        public void Accuracy_Within15Percent(double rho, double ca, double cs)
        {
            double measured = AverageMeasured(rho, ca, cs);
            double theory = TheoryWq(rho, ca, cs);
            string detail = $"ρ = {rho}, Ca = {ca}, Cs = {cs}: measured {measured:F4} vs Kingman {theory:F4}";
            TestContext.WriteLine(detail);
            Assert.That(measured, Is.EqualTo(theory).Within(15).Percent, detail);
        }

        [Test]
        public void Trend_HalvingServiceCvCutsWaitAsKingmanPredicts()
        {
            // Smooth arrivals (Ca = 0.5), ρ = 0.9. Cs 1 → 0.5:
            // Kingman ratio = (0.25 + 0.25) / (0.25 + 1) = 0.4, i.e. a 60% shorter wait.
            const double rho = 0.9, ca = 0.5;
            double ratioMeasured = AverageMeasured(rho, ca, 0.5) / AverageMeasured(rho, ca, 1.0);
            double ratioTheory = TheoryWq(rho, ca, 0.5) / TheoryWq(rho, ca, 1.0);
            string detail = $"wait ratio Cs 0.5 / Cs 1: measured {ratioMeasured:F3} vs Kingman {ratioTheory:F3}";
            TestContext.WriteLine(detail);
            Assert.That(ratioMeasured, Is.LessThan(1.0), "Smoother service must shorten the wait.");
            Assert.That(ratioMeasured, Is.EqualTo(ratioTheory).Within(15).Percent, detail);
        }

        [Test]
        public void Invariants_HoldWithSmoothArrivals()
        {
            var log = Experiments.RunSingleStation(11UL, Distribution.FromMeanCv(1.25, 0.5), Distribution.FromMeanCv(1.0, 0.5),
                staff: 1, stopTime: 5_000.0, keepEntries: true);
            InvariantChecker.AssertAll(log, new Dictionary<string, int> { { "desk", 1 } });
        }
    }
}
