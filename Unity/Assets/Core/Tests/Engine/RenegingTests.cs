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
    /// <summary>Passengers give up when their patience runs out. Fixed times, worked out by hand.</summary>
    public class RenegingTests
    {
        private static (Runner runner, RunLog log) Build(double service, double patience, params double[] arrivals)
        {
            var station = new Station("desk", 1, new Deterministic(service), new Pcg32(1UL, 1UL));
            var source = new Source("entry", new FixedArrivals(arrivals), station);
            var runner = new Runner(source);
            runner.AddBehaviour(new Reneging(new Deterministic(patience), new Pcg32(1UL, 3UL)));
            var log = new RunLog();
            runner.AddObserver(log);
            return (runner, log);
        }

        private static void CheckInvariants(RunLog log) =>
            InvariantChecker.AssertAll(log, new Dictionary<string, int> { { "desk", 1 } });

        [Test]
        public void GivesUp_WhenPatienceRunsOut()
        {
            // 1 desk, 120 s each, patience 60 s. A at 0, B at 30.
            // A starts at once (A's own timer at 60 is ignored). B would wait until 120 (90 s),
            // but gives up at 30 + 60 = 90.
            var (runner, log) = Build(120.0, 60.0, 0.0, 30.0);
            runner.Run();

            PassengerRecord a = log.Passengers[0], b = log.Passengers[1];
            Assert.That(a.HasReneged, Is.False);
            Assert.That(a.ServiceEnd, Is.EqualTo(120.0));
            Assert.That(b.HasReneged, Is.True);
            Assert.That(b.Renege, Is.EqualTo(90.0));
            Assert.That(b.Wait, Is.EqualTo(60.0));
            Assert.That(double.IsNaN(b.ServiceStart), Is.True);
            Assert.That(b.HasExited, Is.False);
            Assert.That(runner.Now, Is.EqualTo(120.0));
            CheckInvariants(log);
        }

        [Test]
        public void GivesUp_ThenNextInLineServedAtTie()
        {
            // 1 desk, 120 s each, patience 100 s. A at 0, B at 10, C at 20.
            // B gives up at 110 (first in line).
            // C's patience ends at 120, the same moment A finishes: EndService goes first (R1), so C is served.
            var (runner, log) = Build(120.0, 100.0, 0.0, 10.0, 20.0);
            runner.Run();

            var p = log.Passengers;
            Assert.That(p.Select(r => r.HasReneged), Is.EqualTo(new[] { false, true, false }));
            Assert.That(p[1].Renege, Is.EqualTo(110.0));
            Assert.That(p[2].ServiceStart, Is.EqualTo(120.0));
            Assert.That(p.Select(r => r.Wait), Is.EqualTo(new[] { 0.0, 100.0, 100.0 }));
            Assert.That(runner.Now, Is.EqualTo(240.0));
            CheckInvariants(log);
        }

        [Test]
        public void GivesUp_FromTheMiddleOfTheLine_OthersKeepTheirOrder()
        {
            // 1 desk, 300 s each. A at 0, B at 50, C at 60, D at 70. Patience: C 30 s, everyone else 1000 s.
            // At 90 the line is B, C, D and C gives up from the middle. A finishes at 300 → B (wait 250);
            // B finishes at 600 → D (wait 530). FIFO holds for those who stayed.
            var station = new Station("desk", 1, new Deterministic(300.0), new Pcg32(1UL, 1UL));
            var runner = new Runner(new Source("entry", new FixedArrivals(new[] { 0.0, 50.0, 60.0, 70.0 }), station));
            runner.AddBehaviour(new Reneging(new ListDistribution(1000.0, 1000.0, 30.0, 1000.0), new Pcg32(1UL, 3UL)));
            var log = new RunLog();
            runner.AddObserver(log);
            runner.Run();

            var p = log.Passengers;
            Assert.That(p.Select(r => r.HasReneged), Is.EqualTo(new[] { false, false, true, false }));
            Assert.That(p[2].Renege, Is.EqualTo(90.0));
            Assert.That(p.Select(r => r.Wait), Is.EqualTo(new[] { 0.0, 250.0, 30.0, 530.0 }));
            Assert.That(p[3].ServiceEnd, Is.EqualTo(900.0)); // Now ends later, at D's ignored alarm (70 + 1000)
            CheckInvariants(log);
        }

        [Test]
        public void ZeroPatience_LeavesAtOnceWhenDeskBusy()
        {
            // Patience 0: A is served at once; B, arriving while the desk is busy, gives up the moment they join.
            // Deterministic rejects 0, so the patience comes from a list.
            var station = new Station("desk", 1, new Deterministic(120.0), new Pcg32(1UL, 1UL));
            var runner = new Runner(new Source("entry", new FixedArrivals(new[] { 0.0, 30.0 }), station));
            runner.AddBehaviour(new Reneging(new ListDistribution(0.0, 0.0), new Pcg32(1UL, 3UL)));
            var log = new RunLog();
            runner.AddObserver(log);
            runner.Run();

            Assert.That(log.Passengers[0].HasReneged, Is.False);
            Assert.That(log.Passengers[1].Renege, Is.EqualTo(30.0));
            Assert.That(log.Passengers[1].Wait, Is.EqualTo(0.0));
            CheckInvariants(log);
        }

        /// <summary>Hands out fixed values in order (one per passenger who joins the line), for hand tests.</summary>
        private sealed class ListDistribution : IDistribution
        {
            private readonly double[] _values;
            private int _next;

            public ListDistribution(params double[] values) => _values = values;

            public double Mean => _values.Average();

            public double Cv => double.NaN; // not used by hand tests

            public double Sample(Pcg32 rng) => _values[_next++];
        }
    }
}
