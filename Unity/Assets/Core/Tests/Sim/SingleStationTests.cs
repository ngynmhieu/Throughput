using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Throughput.Core.Results;
using Throughput.Core.Sim.Events;
using Throughput.Core.Sim.Network;
using Throughput.Core.Sim.Randomness;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Sim
{
    /// <summary>No randomness: fixed arrivals, fixed service times, timelines worked out by hand.</summary>
    public class SingleStationTests
    {
        private static (Simulator sim, RunLog log) Build(int staff, double service, params double[] arrivals)
        {
            var station = new Station("desk", staff, new Deterministic(service), new Pcg32(1UL, 1UL));
            var source = new Source("entry", new FixedArrivals(arrivals), station);
            var sim = new Simulator(source);
            var log = new RunLog();
            sim.AddObserver(log);
            return (sim, log);
        }

        private static void CheckInvariants(RunLog log, int staff) =>
            InvariantChecker.AssertAll(log, new Dictionary<string, int> { { "desk", staff } });

        [Test]
        public void OneDesk_Fifo_HandTimeline()
        {
            // 1 desk, 120 s each, arrivals at 0, 30, 60.
            // A: 0–120 (wait 0). B: 120–240 (wait 90). C: 240–360 (wait 180).
            var (sim, log) = Build(1, 120.0, 0.0, 30.0, 60.0);
            sim.Run();

            var p = log.Passengers;
            Assert.That(p.Select(r => r.ServiceStart), Is.EqualTo(new[] { 0.0, 120.0, 240.0 }));
            Assert.That(p.Select(r => r.ServiceEnd), Is.EqualTo(new[] { 120.0, 240.0, 360.0 }));
            Assert.That(p.Select(r => r.Wait), Is.EqualTo(new[] { 0.0, 90.0, 180.0 }));
            Assert.That(p.Select(r => r.Exit), Is.EqualTo(new[] { 120.0, 240.0, 360.0 }));
            Assert.That(sim.Now, Is.EqualTo(360.0));
            CheckInvariants(log, staff: 1);
        }

        [Test]
        public void SameTime_DeskFreesBeforeNextArrival()
        {
            // B arrives exactly when A finishes: EndService goes first, so B doesn't wait.
            var (sim, log) = Build(1, 120.0, 0.0, 120.0);
            sim.Run();

            Assert.That(log.Passengers[1].Wait, Is.EqualTo(0.0));
            var at120 = log.Entries.Where(e => e.Time == 120.0).Select(e => e.Kind).ToArray();
            Assert.That(at120, Is.EqualTo(new[]
            {
                LogKind.EndService, LogKind.Exit, LogKind.Arrive, LogKind.JoinQueue, LogKind.StartService,
            }));
            CheckInvariants(log, staff: 1);
        }

        [Test]
        public void IdleDesk_ServesLateArrivalImmediately()
        {
            var (sim, log) = Build(1, 120.0, 0.0, 500.0);
            sim.Run();

            Assert.That(log.Passengers[1].ServiceStart, Is.EqualTo(500.0));
            Assert.That(log.Passengers[1].Wait, Is.EqualTo(0.0));
            CheckInvariants(log, staff: 1);
        }

        [Test]
        public void TwoStaff_ServeTwoAtOnce()
        {
            // 3 arrive together, 2 staff, 100 s each: two start at 0, the third at 100.
            var (sim, log) = Build(2, 100.0, 0.0, 0.0, 0.0);
            sim.Run();

            Assert.That(log.Passengers.Select(r => r.ServiceStart), Is.EqualTo(new[] { 0.0, 0.0, 100.0 }));
            Assert.That(log.Entries.Max(e => e.Busy), Is.EqualTo(2));
            CheckInvariants(log, staff: 2);
        }

        [Test]
        public void StopTime_EndsArrivals()
        {
            // Every 10 s, stop at 35 → arrivals at 10, 20, 30 only.
            var station = new Station("desk", 1, new Deterministic(1.0), new Pcg32(1UL, 1UL));
            var source = new Source("entry", new RenewalArrivals(new Deterministic(10.0), new Pcg32(1UL, 2UL)), station, stopTime: 35.0);
            var sim = new Simulator(source);
            var log = new RunLog();
            sim.AddObserver(log);
            sim.Run();

            Assert.That(log.Passengers.Select(r => r.Arrive), Is.EqualTo(new[] { 10.0, 20.0, 30.0 }));
            CheckInvariants(log, staff: 1);
        }

        [Test]
        public void RunInPieces_EqualsRunAtOnce()
        {
            var (whole, wholeLog) = Build(1, 120.0, 0.0, 30.0, 60.0);
            whole.Run();

            var (pieces, piecesLog) = Build(1, 120.0, 0.0, 30.0, 60.0);
            pieces.Run(until: 150.0);
            Assert.That(pieces.Now, Is.LessThanOrEqualTo(150.0));
            pieces.Run();

            Assert.That(piecesLog.Entries.Select(e => e.ToString()), Is.EqualTo(wholeLog.Entries.Select(e => e.ToString())));
            CheckInvariants(piecesLog, staff: 1);
        }

        [Test]
        public void FixedArrivals_RejectUnsortedOrNegativeTimes()
        {
            Assert.Throws<ArgumentException>(() => new FixedArrivals(0.0, 50.0, 20.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedArrivals(-1.0, 5.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedArrivals(double.NaN));
        }

        [Test]
        public void Station_RejectsZeroStaff()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Station("desk", 0, new Deterministic(1.0), new Pcg32(1UL, 1UL)));
        }
    }
}
