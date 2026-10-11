using System.Collections.Generic;
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

        [Test]
        public void FullLine_BlocksUpstream_AndRestartsInOrder()
        {
            // ID 10 s → scanner 100 s, scanner line holds 1. Four people arrive at 0.
            // A: ID 0–10, scanner 10–110.            B: ID 10–20, waits in the scanner line (now full).
            // C: ID 20–30, line full → ID BLOCKED holding C. D waits at ID.
            // 110: A leaves; scanner starts B; the line has room → C released, ID starts D (110–120).
            // 120: D done, line full (C) → blocked again. 210: B done → scanner starts C → D released.
            // Scanner starts 10, 110, 210, 310; exits 110, 210, 310, 410.
            Station id = Desk("id", 10.0), scan = Desk("scan", 100.0, lineCapacity: 1);
            id.ConnectTo(scan);
            var (runner, log) = Run(id, 0.0, 0.0, 0.0, 0.0);

            var p = log.Passengers;
            Assert.That(p.Select(r => r.Visits[1].ServiceStart), Is.EqualTo(new[] { 10.0, 110.0, 210.0, 310.0 }));
            Assert.That(p.Select(r => r.Exit), Is.EqualTo(new[] { 110.0, 210.0, 310.0, 410.0 }));
            Assert.That(p.Select(r => r.Visits[0].BlockedTime), Is.EqualTo(new[] { 0.0, 0.0, 80.0, 90.0 }));
            Assert.That(p[3].Visits[0].ServiceStart, Is.EqualTo(110.0));
            Assert.That(log.Entries.Count(e => e.Kind == LogKind.Blocked), Is.EqualTo(2));
            InvariantChecker.AssertAll(log, OneEach, lineCapacity: new Dictionary<string, int> { { "scan", 1 } });
        }

        [Test]
        public void GiveUpInFullLine_ReleasesBlockedDesk()
        {
            // ID 10 s → scanner 100 s, scanner line holds 1, patience 50 s at every line.
            // A: ID 0–10, scanner 10–110. B: ID 10–20, joins the scanner line at 20 (full).
            // C: ID 20–30, blocked. B gives up at 20 + 50 = 70 → the line has room → C released at 70.
            // (C's own ID-line timer from 0 + 50 = 50 is ignored: C started at 20.)
            Station id = Desk("id", 10.0), scan = Desk("scan", 100.0, lineCapacity: 1);
            id.ConnectTo(scan);
            var runner = new Runner(new Source("entry", new FixedArrivals(new[] { 0.0, 0.0, 0.0 }), id));
            runner.AddBehaviour(new Reneging(new Deterministic(50.0), new Pcg32(1UL, 3UL)));
            var log = new RunLog();
            runner.AddObserver(log);
            runner.Run();

            var p = log.Passengers;
            Assert.That(p[1].Renege, Is.EqualTo(70.0));
            Assert.That(p[2].Visits[0].Unblocked, Is.EqualTo(70.0));
            Assert.That(p[2].Visits[1].QueueJoin, Is.EqualTo(70.0));
            InvariantChecker.AssertAll(log, OneEach, lineCapacity: new Dictionary<string, int> { { "scan", 1 } });
        }
    }
}
