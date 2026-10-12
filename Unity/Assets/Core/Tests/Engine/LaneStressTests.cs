using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Throughput.Core.Engine.Behaviours;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Policies;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Results;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Engine
{
    /// <summary>Random runs with lanes, full lines, giving up and random open/close actions: every invariant must hold.</summary>
    public class LaneStressTests
    {
        [Test]
        public void RandomActions_KeepEveryInvariant([Range(1, 20)] int seed)
        {
            var streams = new RngStreams((ulong)seed);
            var id = new Station("id", 2, Distribution.FromMeanCv(1.0, 1.0), streams.Get("service:id"));
            Station[] lanes = Enumerable.Range(1, 3)
                .Select(n => new Station($"scan {n}", 1, Distribution.FromMeanCv(1.4, 1.0), streams.Get($"service:scan {n}"), 3))
                .ToArray();
            id.ConnectTo(new Router("scan", lanes, ShortestLine.Instance), walkTime: 0.2);
            var source = new Source("entry", new RenewalArrivals(Distribution.FromMeanCv(0.6, 1.0), streams.Get("arrivals:entry")),
                id, stopTime: 200.0);
            var runner = new Runner(source);
            runner.AddBehaviour(new Reneging(Distribution.FromMeanCv(15.0, 1.0), streams));
            var log = new RunLog();
            runner.AddObserver(log);

            Pcg32 manager = streams.Get("test:manager");
            for (double t = 5.0; t < 200.0; t += 5.0)
            {
                runner.Run(t);
                Station lane = lanes[manager.NextUInt() % 3];
                if (lane.IsOpen && lane.Stage.OpenLanes > 1) runner.CloseLane(lane);
                else runner.OpenLane(lane);
            }
            runner.Run();

            Assert.That(runner.IsFinished, Is.True);
            Assert.That(log.Entries.Count(e => e.Kind == LogKind.LaneClosed), Is.GreaterThan(0));
            var staff = new Dictionary<string, int> { { "id", 2 } };
            foreach (Station s in lanes) staff[s.Name] = 1;
            InvariantChecker.AssertAll(log, staff, lineCapacity: lanes.ToDictionary(s => s.Name, s => 3));
        }
    }
}
