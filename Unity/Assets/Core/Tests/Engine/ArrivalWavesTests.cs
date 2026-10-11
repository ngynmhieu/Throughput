using System;
using System.Collections.Generic;
using NUnit.Framework;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Randomness;

namespace Throughput.Core.Tests.Engine
{
    public class ArrivalWavesTests
    {
        private static List<double> AllArrivals(ArrivalWaves waves)
        {
            var times = new List<double>();
            double now = 0.0;
            while (waves.TryNext(now, out double t))
            {
                times.Add(t);
                now = t;
            }
            return times;
        }

        [Test]
        public void Waves_RateMatchesEachWave()
        {
            // 1 per minute for 10,000 min, then 3 per minute for 10,000 min (Poisson: sd ≈ 1% of the count).
            var waves = new ArrivalWaves(new[] { new Wave(0, 10_000, 1.0), new Wave(10_000, 20_000, 3.0) }, new Pcg32(5UL, 1UL));
            List<double> times = AllArrivals(waves);

            int first = times.FindAll(t => t < 10_000).Count;
            int second = times.Count - first;
            Assert.That(first, Is.EqualTo(10_000).Within(4).Percent);
            Assert.That(second, Is.EqualTo(30_000).Within(4).Percent);
            Assert.That(times[times.Count - 1], Is.LessThan(20_000));
        }

        [Test]
        public void Waves_SkipQuietTimes()
        {
            // Quiet 0–10 (rate 0), busy 10–20, gap 20–50 (no wave), busy 50–60. Nothing after 60.
            var waves = new ArrivalWaves(new[] { new Wave(0, 10, 0.0), new Wave(10, 20, 5.0), new Wave(50, 60, 5.0) },
                new Pcg32(5UL, 2UL));
            List<double> times = AllArrivals(waves);

            Assert.That(times.Count, Is.GreaterThan(0));
            foreach (double t in times)
                Assert.That((t >= 10 && t < 20) || (t >= 50 && t < 60), Is.True, $"Arrival at {t} is outside the waves.");
            Assert.That(times.Exists(t => t >= 50), Is.True, "The second wave must have arrivals too.");
        }

        [Test]
        public void Waves_RejectBadInput()
        {
            var rng = new Pcg32(1UL, 1UL);
            Assert.Throws<ArgumentException>(() => new ArrivalWaves(new[] { new Wave(10, 5, 1.0) }, rng));
            Assert.Throws<ArgumentException>(() => new ArrivalWaves(new[] { new Wave(0, 10, -1.0) }, rng));
            Assert.Throws<ArgumentException>(() => new ArrivalWaves(new[] { new Wave(0, 10, 1.0), new Wave(5, 20, 1.0) }, rng));
        }
    }
}
