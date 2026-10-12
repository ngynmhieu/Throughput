using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Policies;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Results;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Engine
{
    /// <summary>Stages with separate lanes (N1–N10). Fixed times, worked out by hand.</summary>
    public class LaneTests
    {
        private static Station Desk(string name, double service, int staff = 1, int lineCapacity = int.MaxValue) =>
            new Station(name, staff, new Deterministic(service), new Pcg32(1UL, 1UL), lineCapacity);

        private static Router Lanes(string name, params Station[] lanes) => new Router(name, lanes, ShortestLine.Instance);

        private static (Runner runner, RunLog log) Start(Router first, params double[] arrivals) =>
            Start(new Source("entry", new FixedArrivals(arrivals), first));

        private static (Runner runner, RunLog log) Start(Station first, params double[] arrivals) =>
            Start(new Source("entry", new FixedArrivals(arrivals), first));

        private static (Runner runner, RunLog log) Start(Source source)
        {
            var runner = new Runner(source);
            var log = new RunLog();
            runner.AddObserver(log);
            return (runner, log);
        }

        private static Dictionary<string, int> Staff(params Station[] stations) => stations.ToDictionary(s => s.Name, s => s.Staff);

        [Test]
        public void ShortestLine_FromTheEntrance_TiesGoToTheFirstLane()
        {
            // Two lanes, 100 s each. A at 0 → s1 (tie 0–0). B at 10 → s2 (s1 has A at the desk).
            // C at 20 → tie 1–1 → s1, waits for A. Starts 0, 10, 100; exits 100, 110, 200.
            Station s1 = Desk("s1", 100.0), s2 = Desk("s2", 100.0);
            var (runner, log) = Start(Lanes("scan", s1, s2), 0.0, 10.0, 20.0);
            runner.Run();

            var p = log.Passengers;
            Assert.That(p.Select(r => r.Station), Is.EqualTo(new[] { "s1", "s2", "s1" }));
            Assert.That(p.Select(r => r.ServiceStart), Is.EqualTo(new[] { 0.0, 10.0, 100.0 }));
            Assert.That(p.Select(r => r.Exit), Is.EqualTo(new[] { 100.0, 110.0, 200.0 }));
            InvariantChecker.AssertAll(log, Staff(s1, s2));
        }

        [Test]
        public void ShortestLine_CountsPeopleWalkingThere()
        {
            // ID has 2 desks, 10 s. A and B arrive at 0, both finish at 10 and walk 30 s to two scanner lanes.
            // A picks s1 (tie). B must see A walking to s1 and pick s2.
            Station id = Desk("id", 10.0, staff: 2), s1 = Desk("s1", 100.0), s2 = Desk("s2", 100.0);
            id.ConnectTo(Lanes("scan", s1, s2), walkTime: 30.0);
            var (runner, log) = Start(id, 0.0, 0.0);
            runner.Run();

            var p = log.Passengers;
            Assert.That(p.Select(r => r.Visits[1].Station), Is.EqualTo(new[] { "s1", "s2" }));
            Assert.That(p.Select(r => r.Visits[1].ServiceStart), Is.EqualTo(new[] { 40.0, 40.0 }));
            InvariantChecker.AssertAll(log, Staff(id, s1, s2));
        }

        [Test]
        public void Lanes_AllFull_BlockAtTheStage()
        {
            // ID 10 s → two scanner lanes, 100 s, each line holds 1. Five people arrive at 0.
            // A: ID 0–10 → s1, served 10–110.  B: ID 10–20 → s2 (s1 busy), served 20–120.
            // C: ID 20–30 → tie 1–1 → s1's line.  D: ID 30–40 → s2's line (s1 now has 2 people).
            // E: ID 40–50 → both lines full → ID BLOCKED at the stage.
            // 110: s1 finishes A, starts C → s1's line has room → E released to s1 (joins at 110).
            // 120: s2 starts D. 210: s1 starts E. Exits 110, 120, 210, 220, 310.
            Station id = Desk("id", 10.0);
            Station s1 = Desk("s1", 100.0, lineCapacity: 1), s2 = Desk("s2", 100.0, lineCapacity: 1);
            id.ConnectTo(Lanes("scan", s1, s2));
            var (runner, log) = Start(id, 0.0, 0.0, 0.0, 0.0, 0.0);
            runner.Run();

            var p = log.Passengers;
            Assert.That(p.Select(r => r.Visits[1].Station), Is.EqualTo(new[] { "s1", "s2", "s1", "s2", "s1" }));
            Assert.That(p[4].Visits[0].Unblocked, Is.EqualTo(110.0));
            Assert.That(p[4].Visits[1].QueueJoin, Is.EqualTo(110.0));
            Assert.That(p.Select(r => r.Exit), Is.EqualTo(new[] { 110.0, 120.0, 210.0, 220.0, 310.0 }));
            InvariantChecker.AssertAll(log, Staff(id, s1, s2),
                lineCapacity: new Dictionary<string, int> { { "s1", 1 }, { "s2", 1 } });
        }

        [Test]
        public void Router_RejectsBadLanes()
        {
            Station a = Desk("a", 1.0), b = Desk("b", 1.0);
            Assert.Throws<System.ArgumentException>(() => Lanes("x"));
            Assert.Throws<System.ArgumentException>(() => Lanes("x", a, a));
            Assert.Throws<System.ArgumentNullException>(() => new Router("x", new[] { a }, null));
            Lanes("x", a);
            Assert.Throws<System.ArgumentException>(() => Lanes("y", a, b)); // a is already a lane of x
            Assert.That(b.Stage, Is.Null, "a failed router must not claim any lane");
        }

        [Test]
        public void SharedLine_IsAStageWithOneLane()
        {
            Station id = Desk("id", 1.0), scan = Desk("scan", 1.0);
            id.ConnectTo(scan);
            Assert.That(id.Next.Lanes, Is.EqualTo(new[] { scan }));
            Assert.That(scan.Stage, Is.SameAs(id.Next));
            new Source("entry", new FixedArrivals(0.0), scan);
            Assert.That(scan.Stage, Is.SameAs(id.Next), "a source feeding scan reuses its stage");
        }

        [Test]
        public void CloseLane_Drains()
        {
            // Two lanes, 100 s. Four people at 0: A s1, B s2, C s1, D s2. Close s2 at 50.
            // E at 60 and F at 70 must go to s1. s2 still serves D (100–200): it drains.
            // s1: A 0–100, C 100–200, E 200–300, F 300–400.
            Station s1 = Desk("s1", 100.0), s2 = Desk("s2", 100.0);
            var (runner, log) = Start(Lanes("scan", s1, s2), 0.0, 0.0, 0.0, 0.0, 60.0, 70.0);
            runner.Run(50.0);
            runner.CloseLane(s2);
            runner.Run();

            var p = log.Passengers;
            Assert.That(p.Select(r => r.Station), Is.EqualTo(new[] { "s1", "s2", "s1", "s2", "s1", "s1" }));
            Assert.That(p[3].ServiceEnd, Is.EqualTo(200.0));
            Assert.That(p.Select(r => r.Exit), Is.EqualTo(new[] { 100.0, 100.0, 200.0, 200.0, 300.0, 400.0 }));
            LogEntry closed = log.Entries.Single(e => e.Kind == LogKind.LaneClosed);
            Assert.That((closed.Time, closed.Station, closed.PassengerId), Is.EqualTo((50.0, "s2", -1)));
            Assert.That(s2.IsOpen, Is.False);
            InvariantChecker.AssertAll(log, Staff(s1, s2));
        }

        [Test]
        public void OpenLane_TakesNewPeopleAtOnce()
        {
            // s2 starts closed. A, B, C at 0 all go to s1. Open s2 at 50; D at 60 goes to s2 (empty).
            // Nobody already in s1's line moves (no jockeying, N10).
            Station s1 = Desk("s1", 100.0), s2 = Desk("s2", 100.0);
            var (runner, log) = Start(Lanes("scan", s1, s2), 0.0, 0.0, 0.0, 60.0);
            runner.CloseLane(s2);
            runner.Run(50.0);
            runner.OpenLane(s2);
            runner.Run();

            var p = log.Passengers;
            Assert.That(p.Select(r => r.Station), Is.EqualTo(new[] { "s1", "s1", "s1", "s2" }));
            Assert.That(p.Select(r => r.ServiceStart), Is.EqualTo(new[] { 0.0, 100.0, 200.0, 60.0 }));
            Assert.That(log.Entries.Where(e => e.PassengerId == -1).Select(e => (e.Time, e.Kind)),
                Is.EqualTo(new[] { (0.0, LogKind.LaneClosed), (50.0, LogKind.LaneOpened) }));
            InvariantChecker.AssertAll(log, Staff(s1, s2));
        }

        [Test]
        public void OpenLane_ReleasesBlockedDesk()
        {
            // ID 10 s → s1 (open) and s2 (closed), 100 s, each line holds 1. Three people at 0.
            // A: ID 0–10, s1 10–110. B: ID 10–20, s1's line (full). C: ID 20–30 → BLOCKED.
            // Open s2 at 50 → C released at 50, joins s2 and starts at once.
            Station id = Desk("id", 10.0);
            Station s1 = Desk("s1", 100.0, lineCapacity: 1), s2 = Desk("s2", 100.0, lineCapacity: 1);
            id.ConnectTo(Lanes("scan", s1, s2));
            var (runner, log) = Start(id, 0.0, 0.0, 0.0);
            runner.CloseLane(s2);
            runner.Run(50.0);
            runner.OpenLane(s2);
            runner.Run();

            var c = log.Passengers[2];
            Assert.That(c.Visits[0].Unblocked, Is.EqualTo(50.0));
            Assert.That(c.Visits[1].Station, Is.EqualTo("s2"));
            Assert.That(c.Visits[1].ServiceStart, Is.EqualTo(50.0));
            InvariantChecker.AssertAll(log, Staff(id, s1, s2),
                lineCapacity: new Dictionary<string, int> { { "s1", 1 }, { "s2", 1 } });
        }

        [Test]
        public void Action_AtAnEventTime_SeesThatEvent()
        {
            // Two lanes, 100 s. A at 0 → s1. B arrives at exactly 50 and picks s2.
            // Closing s2 after Run(50) comes after B's arrival: B stays in s2 and is served there.
            Station s1 = Desk("s1", 100.0), s2 = Desk("s2", 100.0);
            var (runner, log) = Start(Lanes("scan", s1, s2), 0.0, 50.0);
            runner.Run(50.0);
            runner.CloseLane(s2);
            runner.Run();

            Assert.That(log.Passengers[1].Station, Is.EqualTo("s2"));
            Assert.That(log.Passengers[1].Exit, Is.EqualTo(150.0));
            InvariantChecker.AssertAll(log, Staff(s1, s2));
        }

        [Test]
        public void CloseLane_Rules()
        {
            Station s1 = Desk("s1", 100.0), s2 = Desk("s2", 100.0);
            var (runner, log) = Start(Lanes("scan", s1, s2), 0.0);
            runner.CloseLane(s2);
            runner.CloseLane(s2); // already closed: nothing happens
            var e = Assert.Throws<System.InvalidOperationException>(() => runner.CloseLane(s1));
            StringAssert.Contains("last open lane", e.Message);
            runner.OpenLane(s1);  // already open: nothing happens
            Assert.Throws<System.ArgumentNullException>(() => runner.CloseLane(null));
            Assert.That(log.Entries.Count(x => x.PassengerId == -1), Is.EqualTo(1));
            Assert.That(s1.IsOpen, Is.True);
        }
    }
}
