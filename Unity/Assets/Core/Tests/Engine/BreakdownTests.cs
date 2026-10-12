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
    /// <summary>The event deck's breakdowns (K6–K8). Fixed times, worked out by hand.</summary>
    public class BreakdownTests
    {
        private static Station Desk(string name, double service, int lineCapacity = int.MaxValue) =>
            new Station(name, 1, new Deterministic(service), new Pcg32(1UL, 1UL), lineCapacity);

        private static Router Lanes(string name, params Station[] lanes) => new Router(name, lanes, ShortestLine.Instance);

        private static (Runner runner, RunLog log) Start(Source source)
        {
            var runner = new Runner(source);
            var log = new RunLog();
            runner.AddObserver(log);
            return (runner, log);
        }

        private static IEnumerable<(double, LogKind, string)> LaneEntries(RunLog log) =>
            log.Entries.Where(e => e.PassengerId == -1).Select(e => (e.Time, e.Kind, e.Station));

        [Test]
        public void Breakdown_PausesAndResumes()
        {
            // One lane, 100 s. A at 0, B at 10. Breaks at 30 (A has 70 s left), repaired after 50 s at 80.
            // A ends at 150 (the old end at 100 is ignored). B waits, 150–250.
            Station s1 = Desk("s1", 100.0);
            Router stage = Lanes("scan", s1);
            var (runner, log) = Start(new Source("entry", new FixedArrivals(0.0, 10.0), stage));
            runner.AddBreakdown(stage, 30.0, new Deterministic(50.0), new Pcg32(1UL, 1UL));
            runner.Run();

            var p = log.Passengers;
            Assert.That(p[0].ServiceEnd, Is.EqualTo(150.0));
            Assert.That(p[1].ServiceStart, Is.EqualTo(150.0));
            Assert.That(log.Entries.Count(e => e.Kind == LogKind.EndService && e.PassengerId == 0), Is.EqualTo(1));
            Assert.That(LaneEntries(log), Is.EqualTo(new[] { (30.0, LogKind.LaneBroken, "s1"), (80.0, LogKind.LaneRepaired, "s1") }));
            Assert.That(runner.LastActiveTime, Is.EqualTo(250.0));
            Assert.That(s1.IsBroken, Is.False);
            InvariantChecker.AssertAll(log, new Dictionary<string, int> { { "s1", 1 } });
        }

        [Test]
        public void Breakdown_NewPeopleAvoidTheBrokenLane()
        {
            // Two lanes, 100 s. A → s1, B → s2 at 0. A stream is picked so the breakdown at 10 hits s1
            // (A has 90 s left; repaired after 50 s at 60, A ends at 150). C at 20: both lanes hold 1 person,
            // a tie that would go to s1, but s1 is broken → C joins s2 behind B, 100–200.
            ulong id = 1;
            while (new Pcg32(9UL, id).NextUInt() % 2 != 0) id++;
            Station s1 = Desk("s1", 100.0), s2 = Desk("s2", 100.0);
            Router stage = Lanes("scan", s1, s2);
            var (runner, log) = Start(new Source("entry", new FixedArrivals(0.0, 0.0, 20.0), stage));
            runner.AddBreakdown(stage, 10.0, new Deterministic(50.0), new Pcg32(9UL, id));
            runner.Run();

            var p = log.Passengers;
            Assert.That(LaneEntries(log).First(), Is.EqualTo((10.0, LogKind.LaneBroken, "s1")));
            Assert.That(p[0].ServiceEnd, Is.EqualTo(150.0));
            Assert.That(p[2].Station, Is.EqualTo("s2"));
            Assert.That(p[2].ServiceStart, Is.EqualTo(100.0));
            InvariantChecker.AssertAll(log, new Dictionary<string, int> { { "s1", 1 }, { "s2", 1 } });
        }

        [Test]
        public void Breakdown_OnlyLane_BlocksUpstreamUntilRepaired()
        {
            // ID 10 s → one scanner lane, 100 s, its line holds 1. Three people at 0.
            // A: ID 0–10, scanner from 10. B: ID 10–20, joins the scanner line at 20.
            // Breaks at 20 (after B's events at 20): A has 90 s left. C: ID 20–30 → no working lane has room → BLOCKED.
            // Repaired after 100 s at 120: A ends at 210 → B starts → C released at 210, scanner 310–410.
            Station id = Desk("id", 10.0), s1 = Desk("s1", 100.0, lineCapacity: 1);
            Router stage = Lanes("scan", s1);
            id.ConnectTo(stage);
            var (runner, log) = Start(new Source("entry", new FixedArrivals(0.0, 0.0, 0.0), id));
            runner.AddBreakdown(stage, 20.0, new Deterministic(100.0), new Pcg32(1UL, 1UL));
            runner.Run();

            var p = log.Passengers;
            Assert.That(p[1].Visits[1].QueueJoin, Is.EqualTo(20.0));
            Assert.That(p[2].Visits[0].ServiceEnd, Is.EqualTo(30.0));
            Assert.That(p[2].Visits[0].Unblocked, Is.EqualTo(210.0));
            Assert.That(p.Select(r => r.Exit), Is.EqualTo(new[] { 210.0, 310.0, 410.0 }));
            InvariantChecker.AssertAll(log, new Dictionary<string, int> { { "id", 1 }, { "s1", 1 } },
                lineCapacity: new Dictionary<string, int> { { "s1", 1 } });
        }

        [Test]
        public void AddBreakdown_Rules()
        {
            Station s1 = Desk("s1", 1.0);
            Router stage = Lanes("scan", s1);
            var runner = new Runner(new Source("entry", new FixedArrivals(0.0), stage));
            var rng = new Pcg32(1UL, 1UL);
            Assert.Throws<System.ArgumentNullException>(() => runner.AddBreakdown(null, 1.0, new Deterministic(1.0), rng));
            Assert.Throws<System.ArgumentNullException>(() => runner.AddBreakdown(stage, 1.0, null, rng));
            Assert.Throws<System.ArgumentNullException>(() => runner.AddBreakdown(stage, 1.0, new Deterministic(1.0), null));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => runner.AddBreakdown(stage, -1.0, new Deterministic(1.0), rng));
            runner.Run();
            Assert.Throws<System.InvalidOperationException>(() => runner.AddBreakdown(stage, 5.0, new Deterministic(1.0), rng));
        }
    }
}
