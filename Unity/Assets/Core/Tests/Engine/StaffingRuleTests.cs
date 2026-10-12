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
    /// <summary>The staffing rule (K1–K5) and the manager's pins. Fixed times, worked out by hand.</summary>
    public class StaffingRuleTests
    {
        private static Station Desk(string name) => new Station(name, 1, new Deterministic(100.0), new Pcg32(1UL, 1UL));

        private static (Runner runner, RunLog log, Station s1, Station s2, Station s3, Router stage) ThreeLanes(params double[] arrivals)
        {
            Station s1 = Desk("s1"), s2 = Desk("s2"), s3 = Desk("s3");
            var stage = new Router("scan", new[] { s1, s2, s3 }, ShortestLine.Instance);
            var runner = new Runner(new Source("entry", new FixedArrivals(arrivals), stage));
            var log = new RunLog();
            runner.AddObserver(log);
            return (runner, log, s1, s2, s3, stage);
        }

        private static IEnumerable<(double, LogKind, string)> LaneEntriesAfterStart(RunLog log) =>
            log.Entries.Where(e => e.PassengerId == -1 && e.Time > 0).Select(e => (e.Time, e.Kind, e.Station));

        private static readonly Dictionary<string, int> Staff = new Dictionary<string, int> { { "s1", 1 }, { "s2", 1 }, { "s3", 1 } };

        [Test]
        public void Rule_OpensAfterSetup_AndClosesWhenQuiet()
        {
            // Three lanes, 100 s; only s1 open (s2, s3 closed and handed to the rule). Rule: open above 2 waiting
            // per open lane, close below 0.5, setup 30 s. People at 0, 1, 2, 3, 4 all join s1.
            // At 3: B, C, D wait → 3 per lane > 2 → s2 starts opening, ready at 33. At 4: 4 waiting, but s2 is
            // already opening (one at a time). At 33 s2 opens: 4 / 2 = 2, not above 2. F at 40 → s2 (empty), 40–140.
            // s1 serves A 0–100, B –200, C –300, D –400, E –500. At 400 nobody waits: 0 < 0.5 → close s2.
            var (runner, log, s1, s2, s3, stage) = ThreeLanes(0.0, 1.0, 2.0, 3.0, 4.0, 40.0);
            runner.CloseLane(s2);
            runner.ReturnToRule(s2);
            runner.CloseLane(s3);
            runner.ReturnToRule(s3);
            runner.AddRule(new StaffingRule(stage, openAbove: 2.0, closeBelow: 0.5, setupTime: 30.0));
            runner.Run();

            Assert.That(LaneEntriesAfterStart(log), Is.EqualTo(new[]
            {
                (3.0, LogKind.LaneOpening, "s2"), (33.0, LogKind.LaneOpened, "s2"), (400.0, LogKind.LaneClosed, "s2"),
            }));
            Assert.That(log.Entries.Where(e => e.Time > 0 && e.PassengerId == -1).All(e => !e.ByManager), Is.True);
            Assert.That(log.Passengers[5].Station, Is.EqualTo("s2"));
            Assert.That(log.Passengers[5].ServiceStart, Is.EqualTo(40.0));
            Assert.That(log.Passengers[4].Station, Is.EqualTo("s1"));
            InvariantChecker.AssertAll(log, Staff);
        }

        [Test]
        public void Rule_ManagerCloseCancelsSetup_AndPins()
        {
            // As above, but at 3.5 (s2 is in setup) the manager closes s2: setup cancelled and s2 pinned closed.
            // The rule checks at once: 3 waiting per lane > 2 → it opens s3 instead, ready at 33.5.
            // The old LaneReady for s2 at 33 is ignored. F at 40 → s3. At 400 the rule closes s3 (s2 is pinned).
            var (runner, log, s1, s2, s3, stage) = ThreeLanes(0.0, 1.0, 2.0, 3.0, 4.0, 40.0);
            runner.CloseLane(s2);
            runner.ReturnToRule(s2);
            runner.CloseLane(s3);
            runner.ReturnToRule(s3);
            runner.AddRule(new StaffingRule(stage, 2.0, 0.5, 30.0));
            runner.Run(3.5);
            runner.CloseLane(s2);
            runner.Run();

            Assert.That(LaneEntriesAfterStart(log), Is.EqualTo(new[]
            {
                (3.0, LogKind.LaneOpening, "s2"), (3.5, LogKind.LaneClosed, "s2"), (3.5, LogKind.LaneOpening, "s3"),
                (33.5, LogKind.LaneOpened, "s3"), (400.0, LogKind.LaneClosed, "s3"),
            }));
            Assert.That(log.Entries.Single(e => e.Time == 3.5 && e.Kind == LogKind.LaneClosed).ByManager, Is.True);
            Assert.That(s2.IsManual, Is.True);
            Assert.That(s2.IsOpening, Is.False);
            Assert.That(log.Passengers[5].Station, Is.EqualTo("s3"));
            InvariantChecker.AssertAll(log, Staff);
        }

        [Test]
        public void Rule_LeavesPinnedLanes_UntilReturned()
        {
            // Two lanes, both pinned open by the manager. Nobody waits, so the rule would close one, but both are pinned.
            // At 10 the manager returns s2 to the rule → the rule closes it at once.
            Station s1 = Desk("s1"), s2 = Desk("s2");
            var stage = new Router("scan", new[] { s1, s2 }, ShortestLine.Instance);
            var runner = new Runner(new Source("entry", new FixedArrivals(0.0), stage));
            var log = new RunLog();
            runner.AddObserver(log);
            runner.OpenLane(s1);
            runner.OpenLane(s2);
            runner.AddRule(new StaffingRule(stage, 5.0, 0.5, 0.0));
            runner.Run(10.0);
            Assert.That(s2.IsOpen, Is.True);
            runner.ReturnToRule(s2);
            runner.Run();

            Assert.That(log.Entries.Where(e => e.PassengerId == -1).Select(e => (e.Time, e.Kind, e.Station, e.ByManager)), Is.EqualTo(new[]
            {
                (0.0, LogKind.LaneOpened, "s1", true), (0.0, LogKind.LaneOpened, "s2", true),
                (10.0, LogKind.LaneReturnedToRule, "s2", true), (10.0, LogKind.LaneClosed, "s2", false),
            }));
            Assert.That(s1.IsManual && !s2.IsManual, Is.True);
        }

        [Test]
        public void StaffingRule_RejectsBadNumbers()
        {
            var stage = new Router("scan", new[] { Desk("s1") }, ShortestLine.Instance);
            Assert.Throws<System.ArgumentNullException>(() => new StaffingRule(null, 2.0, 1.0, 0.0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new StaffingRule(stage, 1.0, 1.0, 0.0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new StaffingRule(stage, 2.0, -1.0, 0.0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new StaffingRule(stage, double.NaN, 1.0, 0.0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new StaffingRule(stage, double.PositiveInfinity, 1.0, 0.0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new StaffingRule(stage, 2.0, 1.0, -1.0));
        }
    }
}
