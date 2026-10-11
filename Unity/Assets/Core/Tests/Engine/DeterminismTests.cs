using System.Linq;
using NUnit.Framework;
using Throughput.Core.Engine.Behaviours;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Results;
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

        /// <summary>A busy 3-stage chain with walking, blocking and giving up: every kind of same-time event.</summary>
        private static (Runner runner, RunLog log) BuildChain(ulong seed)
        {
            var streams = new RngStreams(seed);
            var id = new Station("id", 2, new Exponential(1.6), streams.Get("service:id"));
            var scan = new Station("scan", 1, Distribution.FromMeanCv(0.9, 0.5), streams.Get("service:scan"), lineCapacity: 3);
            var bags = new Station("bags", 1, new Deterministic(0.85), streams.Get("service:bags"), lineCapacity: 2);
            id.ConnectTo(scan, walkTime: 0.2);
            scan.ConnectTo(bags);
            var runner = new Runner(new Source("entry", new RenewalArrivals(new Exponential(1.0), streams.Get("arrivals:entry")),
                id, stopTime: 2_000.0));
            runner.AddBehaviour(new Reneging(new Exponential(20.0), streams.Get("patience")));
            var log = new RunLog();
            runner.AddObserver(log);
            return (runner, log);
        }

        [Test]
        public void ManySmallSteps_EqualOneRun()
        {
            // The game calls Run(until: now) every frame. That must give exactly the run you'd get in one go.
            var (whole, wholeLog) = BuildChain(42UL);
            whole.Run();

            var (stepped, steppedLog) = BuildChain(42UL);
            double t = 0.0;
            while (!stepped.IsFinished)
            {
                t += 0.37; // an awkward step size: steps land between, and sometimes exactly on, events
                stepped.Run(t);
            }

            Assert.That(steppedLog.Entries.Select(e => e.ToString()), Is.EqualTo(wholeLog.Entries.Select(e => e.ToString())));
            Assert.That(stepped.LastActiveTime, Is.EqualTo(whole.LastActiveTime));
        }
    }
}
