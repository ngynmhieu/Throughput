using System.Collections.Generic;
using NUnit.Framework;
using Throughput.Core.Results;
using Throughput.Core.Sim.Network;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Invariants
{
    /// <summary>The checker must actually catch broken runs, or a green test means nothing.</summary>
    public class InvariantCheckerTests
    {
        private static readonly Dictionary<string, int> NoStations = new Dictionary<string, int>();

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
        public void Passes_OnEmptyRun()
        {
            InvariantChecker.AssertAll(new RunLog(), NoStations);
        }
    }
}
