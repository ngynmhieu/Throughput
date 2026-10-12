using System.Collections.Generic;
using NUnit.Framework;
using Throughput.Core.Engine.Behaviours;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Policies;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Results;

namespace Throughput.Core.Tests.Results
{
    /// <summary>What a run measured (G2–G5). Fixed times, worked out by hand.</summary>
    public class RunStatsTests
    {
        private static Station Desk(string name) => new Station(name, 1, new Deterministic(100.0), new Pcg32(1UL, 1UL));

        private static (Runner runner, RunStats stats, Station s1, Station s2) TwoLanes(params double[] arrivals)
        {
            Station s1 = Desk("s1"), s2 = Desk("s2");
            var stage = new Router("scan", new[] { s1, s2 }, ShortestLine.Instance);
            var runner = new Runner(new Source("entry", new FixedArrivals(arrivals), stage));
            var stats = new RunStats(new[] { s1, s2 });
            runner.AddObserver(stats);
            return (runner, stats, s1, s2);
        }

        [Test]
        public void Stats_OpenedLaneCostsFromThen()
        {
            // s2 closed at 0, opened at 50. A, B, C at 0 all join s1: 0–100, 100–200, 200–300 (waits 0, 100, 200).
            // D at 60 → s2 (empty), 60–160. Run ends at 300.
            // Cost: s1 300 + s2 (300 − 50) = 550. Busy share: s1 300/300, s2 100/250.
            var (runner, stats, s1, s2) = TwoLanes(0.0, 0.0, 0.0, 60.0);
            runner.CloseLane(s2);
            runner.Run(50.0);
            runner.OpenLane(s2);
            runner.Run();
            RunSummary s = stats.Summarize(runner.LastActiveTime);

            Assert.That((s.Arrived, s.Exited, s.MissedFlights), Is.EqualTo((4, 4, 0)));
            Assert.That(s.End, Is.EqualTo(300.0));
            Assert.That(s.AverageWait, Is.EqualTo(75.0));
            Assert.That(s.LongestWait, Is.EqualTo(200.0));
            Assert.That(s.StaffMinutes, Is.EqualTo(550.0));
            Assert.That(s.BusyShare["s1"], Is.EqualTo(1.0));
            Assert.That(s.BusyShare["s2"], Is.EqualTo(0.4));
            Assert.That(s.Throughput, Is.EqualTo(4.0 / 300.0));
        }

        [Test]
        public void Stats_ClosedLaneCostsUntilItDrains()
        {
            // As above, but s2 is closed again at 120 while D is still at its desk (until 160): it costs 50–160 = 110.
            var (runner, stats, s1, s2) = TwoLanes(0.0, 0.0, 0.0, 60.0);
            runner.CloseLane(s2);
            runner.Run(50.0);
            runner.OpenLane(s2);
            runner.Run(120.0);
            runner.CloseLane(s2);
            runner.Run();
            RunSummary s = stats.Summarize(runner.LastActiveTime);

            Assert.That(s.StaffMinutes, Is.EqualTo(410.0));
            Assert.That(s.BusyShare["s2"], Is.EqualTo(100.0 / 110.0));
        }

        [Test]
        public void Stats_GivingUpIsAMissedFlight()
        {
            // One desk, 100 s. A and B at 0; patience 50. A starts at once; B gives up at 50.
            Station s1 = Desk("s1");
            var runner = new Runner(new Source("entry", new FixedArrivals(0.0, 0.0), s1));
            runner.AddBehaviour(new Reneging(new Deterministic(50.0), new Pcg32(1UL, 3UL)));
            var stats = new RunStats(new[] { s1 });
            runner.AddObserver(stats);
            runner.Run();
            RunSummary s = stats.Summarize(runner.LastActiveTime);

            Assert.That((s.Arrived, s.Exited, s.MissedFlights), Is.EqualTo((2, 1, 1)));
            Assert.That(s.AverageWait, Is.EqualTo(25.0));
            Assert.That(s.LongestWait, Is.EqualTo(50.0));
            Assert.That(s.StaffMinutes, Is.EqualTo(100.0));
        }

        [Test]
        public void Stats_UnknownStation_Throws()
        {
            Station s1 = Desk("s1"), other = Desk("other");
            var runner = new Runner(new Source("entry", new FixedArrivals(0.0), s1));
            var stats = new RunStats(new[] { other });
            runner.AddObserver(stats);
            Assert.Throws<System.InvalidOperationException>(() => runner.Run());

            var (runner2, stats2, _, _) = TwoLanes(0.0);
            runner2.Run();
            Assert.Throws<System.ArgumentOutOfRangeException>(() => stats2.Summarize(50.0)); // the last change was at 100
            Assert.Throws<System.ArgumentNullException>(() => new RunStats(null));
        }

        [Test]
        public void Stats_DeckEventsAfterTheEnd_DontBreakTheSummary()
        {
            // One person, 0–100. A breakdown at 150 comes after everyone left: the run ends at 100 and costs 100.
            Station s1 = Desk("s1");
            var stage = new Router("scan", new[] { s1 }, ShortestLine.Instance);
            var runner = new Runner(new Source("entry", new FixedArrivals(0.0), stage));
            runner.AddBreakdown(stage, 150.0, new Deterministic(10.0), new Pcg32(1UL, 1UL));
            var stats = new RunStats(new[] { s1 });
            runner.AddObserver(stats);
            runner.Run();
            RunSummary s = stats.Summarize(runner.LastActiveTime);

            Assert.That(s.End, Is.EqualTo(100.0));
            Assert.That(s.StaffMinutes, Is.EqualTo(100.0));
        }

        [Test]
        public void Stats_MidRun_CountsPeopleStillWaiting()
        {
            // One desk, 100 s. A and B at 0. At 60, B has waited 60 so far.
            Station s1 = Desk("s1");
            var runner = new Runner(new Source("entry", new FixedArrivals(0.0, 0.0), s1));
            var stats = new RunStats(new[] { s1 });
            runner.AddObserver(stats);
            runner.Run(60.0);
            RunSummary s = stats.Summarize(60.0);

            Assert.That(s.LongestWait, Is.EqualTo(60.0));
            Assert.That(s.AverageWait, Is.EqualTo(30.0));
        }

        [Test]
        public void Stats_AttachedLate_CountsFromThen()
        {
            // One desk, 100 s, A and B at 0 (busy 0–200). Stats attached at 50 see 150 staffed and busy minutes.
            Station s1 = Desk("s1");
            var runner = new Runner(new Source("entry", new FixedArrivals(0.0, 0.0), s1));
            runner.Run(50.0);
            var stats = new RunStats(new[] { s1 }, startTime: runner.Now);
            runner.AddObserver(stats);
            runner.Run();
            RunSummary s = stats.Summarize(runner.LastActiveTime);

            Assert.That(s.StaffMinutes, Is.EqualTo(150.0));
            Assert.That(s.BusyShare["s1"], Is.EqualTo(1.0));
        }
    }
}
