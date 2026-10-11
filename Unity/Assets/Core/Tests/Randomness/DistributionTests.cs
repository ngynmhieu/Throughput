using System;
using NUnit.Framework;
using Throughput.Core.Sim.Randomness;

namespace Throughput.Core.Tests.Randomness
{
    public class DistributionTests
    {
        private const int Samples = 200_000;

        private static (double mean, double cv, double min) Measure(IDistribution d, ulong seed)
        {
            var rng = new RngStreams(seed).Get("test");
            double sum = 0, sumSq = 0, min = double.MaxValue;
            for (int i = 0; i < Samples; i++)
            {
                double x = d.Sample(rng);
                sum += x;
                sumSq += x * x;
                if (x < min) min = x;
            }
            double mean = sum / Samples;
            double variance = (sumSq - Samples * mean * mean) / (Samples - 1);
            return (mean, Math.Sqrt(variance) / mean, min);
        }

        [TestCase(2.0)]
        [TestCase(0.25)]
        public void Exponential_HasRightMeanAndCvOfOne(double mean)
        {
            var (m, cv, min) = Measure(new Exponential(mean), 1UL);
            Assert.That(m, Is.EqualTo(mean).Within(1).Percent);
            Assert.That(cv, Is.EqualTo(1.0).Within(2).Percent);
            Assert.That(min, Is.GreaterThanOrEqualTo(0.0));
        }

        [TestCase(1.0, 0.5)]
        [TestCase(3.0, 0.3)]
        [TestCase(1.0, 2.0)]
        public void Gamma_HasRequestedMeanAndCv(double mean, double cv)
        {
            var (m, measuredCv, min) = Measure(new GammaDistribution(mean, cv), 2UL);
            Assert.That(m, Is.EqualTo(mean).Within(1).Percent);
            Assert.That(measuredCv, Is.EqualTo(cv).Within(2).Percent);
            Assert.That(min, Is.GreaterThanOrEqualTo(0.0));
        }

        [Test]
        public void Deterministic_AlwaysReturnsTheValue()
        {
            var d = new Deterministic(120.0);
            var rng = new Pcg32(1UL, 1UL);
            for (int i = 0; i < 10; i++)
                Assert.That(d.Sample(rng), Is.EqualTo(120.0));
            Assert.That(d.Cv, Is.EqualTo(0.0));
        }

        [Test]
        public void FromMeanCv_PicksTheSimplestShape()
        {
            Assert.That(Distribution.FromMeanCv(1.0, 0.0), Is.TypeOf<Deterministic>());
            Assert.That(Distribution.FromMeanCv(1.0, 1.0), Is.TypeOf<Exponential>());
            Assert.That(Distribution.FromMeanCv(1.0, 0.5), Is.TypeOf<GammaDistribution>());

            var d = Distribution.FromMeanCv(4.0, 0.5);
            Assert.That(d.Mean, Is.EqualTo(4.0));
            Assert.That(d.Cv, Is.EqualTo(0.5));
        }

        [Test]
        public void FromMeanCv_RejectsBadInputs()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Distribution.FromMeanCv(0.0, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Distribution.FromMeanCv(-1.0, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Distribution.FromMeanCv(double.NaN, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Distribution.FromMeanCv(1.0, -0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Distribution.FromMeanCv(1.0, double.NaN));
        }

        [Test]
        public void SameSeed_GivesSameSamples()
        {
            var d = new GammaDistribution(1.0, 0.5);
            var a = new RngStreams(3UL).Get("s");
            var b = new RngStreams(3UL).Get("s");
            for (int i = 0; i < 1000; i++)
                Assert.That(d.Sample(a), Is.EqualTo(d.Sample(b)));
        }
    }
}
