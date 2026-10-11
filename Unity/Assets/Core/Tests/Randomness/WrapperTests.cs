using System;
using NUnit.Framework;
using Throughput.Core.Engine.Randomness;

namespace Throughput.Core.Tests.Randomness
{
    public class WrapperTests
    {
        [Test]
        public void Shifted_NeverBelowMin_AndMeanAddsUp()
        {
            var d = new Shifted(0.5, new Exponential(1.0));
            var rng = new Pcg32(1UL, 1UL);
            double sum = 0.0;
            const int n = 200_000;
            for (int i = 0; i < n; i++)
            {
                double x = d.Sample(rng);
                Assert.That(x, Is.GreaterThanOrEqualTo(0.5));
                sum += x;
            }
            Assert.That(d.Mean, Is.EqualTo(1.5));
            Assert.That(sum / n, Is.EqualTo(1.5).Within(1).Percent);
            Assert.That(d.Cv, Is.EqualTo(1.0 / 1.5).Within(1e-12)); // same spread (sd 1), bigger mean
        }

        [Test]
        public void Bounded_StaysInRange()
        {
            var d = new Bounded(0.2, 3.0, new Exponential(1.0));
            var rng = new Pcg32(1UL, 2UL);
            for (int i = 0; i < 100_000; i++)
                Assert.That(d.Sample(rng), Is.InRange(0.2, 3.0));
        }

        [Test]
        public void Bounded_ImpossibleRange_Throws()
        {
            var d = new Bounded(0.0, 5.0, new Deterministic(10.0));
            Assert.Throws<InvalidOperationException>(() => d.Sample(new Pcg32(1UL, 3UL)));
        }

        [Test]
        public void Wrappers_RejectBadArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Shifted(-1.0, new Exponential(1.0)));
            Assert.Throws<ArgumentNullException>(() => new Shifted(1.0, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Bounded(3.0, 3.0, new Exponential(1.0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Bounded(-1.0, 3.0, new Exponential(1.0)));
        }
    }
}
