using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Results;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Engine
{
    /// <summary>Two stations in a row (ID check → scanner → exit). Fixed times, worked out by hand.</summary>
    public class ChainTests
    {
        private static Station Desk(string name, double service, int lineCapacity = int.MaxValue) =>
            new Station(name, 1, new Deterministic(service), new Pcg32(1UL, 1UL), lineCapacity);

        private static (Runner runner, RunLog log) Run(Station first, params double[] arrivals)
        {
            var runner = new Runner(new Source("entry", new FixedArrivals(arrivals), first));
            var log = new RunLog();
            runner.AddObserver(log);
            runner.Run();
            return (runner, log);
        }

        private static readonly Dictionary<string, int> OneEach = new Dictionary<string, int> { { "id", 1 }, { "scan", 1 } };

        [Test]
        public void TwoStages_HandTimeline()
        {
            // ID 60 s, scanner 100 s, no walk. A at 0, B at 10.
            // A: ID 0–60, scanner 60–160, exits 160.
            // B: ID 60–120 (wait 50), scanner busy until 160 → 160–260 (wait 40), exits 260.
            Station id = Desk("id", 60.0), scan = Desk("scan", 100.0);
            id.ConnectTo(scan);
            var (runner, log) = Run(id, 0.0, 10.0);

            var p = log.Passengers;
            Assert.That(p.Select(r => r.Visits.Count), Is.EqualTo(new[] { 2, 2 }));
            Assert.That(p.Select(r => r.Visits[1].ServiceStart), Is.EqualTo(new[] { 60.0, 160.0 }));
            Assert.That(p.Select(r => r.Visits[0].Wait), Is.EqualTo(new[] { 0.0, 50.0 }));
            Assert.That(p.Select(r => r.Visits[1].Wait), Is.EqualTo(new[] { 0.0, 40.0 }));
            Assert.That(p.Select(r => r.Exit), Is.EqualTo(new[] { 160.0, 260.0 }));
            InvariantChecker.AssertAll(log, OneEach);
        }

        [Test]
        public void WalkTime_DelaysArrival_DeskFreedAtOnce()
        {
            // ID 60 s, walk 30 s, scanner 100 s. A at 0, B at 60.
            // A: ID 0–60, walks, reaches the scanner at 90, scanner 90–190.
            // The ID desk is free while A walks: B starts at 60 with no wait, ID 60–120, reaches at 150,
            // scanner busy until 190 → 190–290.
            Station id = Desk("id", 60.0), scan = Desk("scan", 100.0);
            id.ConnectTo(scan, walkTime: 30.0);
            var (runner, log) = Run(id, 0.0, 60.0);

            var p = log.Passengers;
            Assert.That(p.Select(r => r.Visits[1].QueueJoin), Is.EqualTo(new[] { 90.0, 150.0 }));
            Assert.That(p[1].Visits[0].Wait, Is.EqualTo(0.0));
            Assert.That(p.Select(r => r.Visits[1].ServiceStart), Is.EqualTo(new[] { 90.0, 190.0 }));
            Assert.That(p.Select(r => r.Exit), Is.EqualTo(new[] { 190.0, 290.0 }));
            InvariantChecker.AssertAll(log, OneEach);
        }

        [Test]
        public void ConnectTo_RejectsBadLinks()
        {
            Station id = Desk("id", 1.0), scan = Desk("scan", 1.0);
            Assert.Throws<System.ArgumentException>(() => id.ConnectTo(id));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => id.ConnectTo(scan, walkTime: -1.0));
            id.ConnectTo(scan);
            Assert.Throws<System.InvalidOperationException>(() => id.ConnectTo(scan));
        }
    }
}
