using System.Linq;
using NUnit.Framework;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Engine
{
    /// <summary>P3: same seed + same setup → exactly the same run.</summary>
    public class DeterminismTests
    {
        private static string[] Run(ulong seed, double meanService)
        {
            var log = Experiments.RunSingleStation(seed, new Exponential(1.0 / 0.8), new Exponential(meanService),
                staff: 1, stopTime: 500.0, keepEntries: true);
            return log.Entries.Select(e => e.ToString()).ToArray();
        }

        [Test]
        public void SameSeed_IdenticalTimeline()
        {
            Assert.That(Run(42UL, 1.0), Is.EqualTo(Run(42UL, 1.0)));
        }

        [Test]
        public void DifferentSeed_DifferentTimeline()
        {
            Assert.That(Run(42UL, 1.0), Is.Not.EqualTo(Run(43UL, 1.0)));
        }

        [Test]
        public void ChangingService_DoesNotMoveArrivals()
        {
            // Separate random streams: a faster desk must not reshuffle who arrives when.
            var slow = Experiments.RunSingleStation(42UL, new Exponential(1.25), new Exponential(1.0), 1, 500.0, false);
            var fast = Experiments.RunSingleStation(42UL, new Exponential(1.25), new Exponential(0.5), 1, 500.0, false);

            Assert.That(fast.Passengers.Select(p => p.Arrive), Is.EqualTo(slow.Passengers.Select(p => p.Arrive)));
        }
    }
}
