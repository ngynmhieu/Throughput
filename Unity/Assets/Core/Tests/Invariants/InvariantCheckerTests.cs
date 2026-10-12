using System.Collections.Generic;
using NUnit.Framework;
using Throughput.Core.Results;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Invariants
{
    /// <summary>The checker must actually catch broken runs, or a green test means nothing.</summary>
    public class InvariantCheckerTests
    {
        private static readonly Dictionary<string, int> NoStations = new Dictionary<string, int>();

        [Test]
        public void Fails_WhenSomeoneJoinsALaneClosedFromTheStart()
        {
            var lane = new Station("x", 1, new Deterministic(1.0), new Pcg32(1UL, 1UL));
            var p = new Passenger(0, 5.0);
            var log = new RunLog();
            log.OnArrive(5.0, p);
            log.OnJoinQueue(5.0, lane, p);

            Assert.Throws<AssertionException>(() => InvariantChecker.AssertAll(log, new Dictionary<string, int> { { "x", 1 } },
                expectDrained: false, closedAtStart: new[] { "x" }));
        }

        [Test]
        public void Fails_WhenTimeGoesBackward()
        {
            var log = new RunLog();
            log.OnArrive(5.0, new Passenger(0, 5.0));
            log.OnArrive(3.0, new Passenger(1, 3.0));

            Assert.Throws<AssertionException>(() => InvariantChecker.AssertAll(log, NoStations, expectDrained: false));
        }

        [Test]
        public void Fails_WhenSomeoneNeverLeaves()
        {
            var log = new RunLog();
            log.OnArrive(0.0, new Passenger(0, 0.0));

            Assert.Throws<AssertionException>(() => InvariantChecker.AssertAll(log, NoStations, expectDrained: true));
        }

        [Test]
        public void Fails_WhenLineIsNotFirstComeFirstServed()
        {
            // p0 joins first, but p1 is served first: a FIFO bug the M/M/1 mean wait can't see.
            var desk = new Station("desk", 1, new Deterministic(1.0), new Pcg32(1UL, 1UL));
            var p0 = new Passenger(0, 0.0);
            var p1 = new Passenger(1, 1.0);
            var log = new RunLog();
            log.OnArrive(0.0, p0);
            log.OnJoinQueue(0.0, desk, p0);
            log.OnArrive(1.0, p1);
            log.OnJoinQueue(1.0, desk, p1);
            log.OnStartService(2.0, desk, p1);
            log.OnStartService(3.0, desk, p0);

            var staff = new Dictionary<string, int> { { "desk", 1 } };
            Assert.Throws<AssertionException>(() => InvariantChecker.AssertAll(log, staff, expectDrained: false));
        }

        [Test]
        public void Fails_WhenSomeoneIsServedAndAlsoGivesUp()
        {
            var desk = new Station("desk", 1, new Deterministic(1.0), new Pcg32(1UL, 1UL));
            var p0 = new Passenger(0, 0.0);
            var log = new RunLog();
            log.OnArrive(0.0, p0);
            log.OnJoinQueue(0.0, desk, p0);
            log.OnStartService(0.0, desk, p0);
            log.OnRenege(5.0, desk, p0);

            var staff = new Dictionary<string, int> { { "desk", 1 } };
            Assert.Throws<AssertionException>(() => InvariantChecker.AssertAll(log, staff, expectDrained: false));
        }

        [Test]
        public void Passes_OnEmptyRun()
        {
            InvariantChecker.AssertAll(new RunLog(), NoStations);
        }
    }
}
