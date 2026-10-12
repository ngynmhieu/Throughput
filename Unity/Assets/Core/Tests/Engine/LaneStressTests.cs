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
    /// <summary>Random runs with lanes, full lines, giving up, rules, breakdowns and random actions: every invariant must hold.</summary>
    public class LaneStressTests
    {
        private sealed class Setup
        {
            public Runner Runner;
            public RunLog Log;
            public Router Stage;
            public Station[] Lanes;
            public RngStreams Streams;
        }

        private static Setup Build(int seed)
        {
            var streams = new RngStreams((ulong)seed);
            var id = new Station("id", 2, Distribution.FromMeanCv(1.0, 1.0), streams.Get("service:id"));
            Station[] lanes = Enumerable.Range(1, 3)
                .Select(n => new Station($"scan {n}", 1, Distribution.FromMeanCv(1.4, 1.0), streams.Get($"service:scan {n}"), 3))
                .ToArray();
            var stage = new Router("scan", lanes, ShortestLine.Instance);
            id.ConnectTo(stage, walkTime: 0.2);
            var source = new Source("entry", new RenewalArrivals(Distribution.FromMeanCv(0.6, 1.0), streams.Get("arrivals:entry")),
                id, stopTime: 200.0);
            var runner = new Runner(source);
            runner.AddBehaviour(new Reneging(Distribution.FromMeanCv(15.0, 1.0), streams));
            var log = new RunLog();
            runner.AddObserver(log);
            return new Setup { Runner = runner, Log = log, Stage = stage, Lanes = lanes, Streams = streams };
        }

        private static void AssertInvariants(Setup s)
        {
            Assert.That(s.Runner.IsFinished, Is.True);
            var staff = new Dictionary<string, int> { { "id", 2 } };
            foreach (Station lane in s.Lanes) staff[lane.Name] = 1;
            InvariantChecker.AssertAll(s.Log, staff, lineCapacity: s.Lanes.ToDictionary(l => l.Name, l => 3));
        }

        [Test]
        public void RandomActions_KeepEveryInvariant([Range(1, 20)] int seed)
        {
            Setup s = Build(seed);
            Pcg32 manager = s.Streams.Get("test:manager");
            for (double t = 5.0; t < 200.0; t += 5.0)
            {
                s.Runner.Run(t);
                Station lane = s.Lanes[manager.NextUInt() % 3];
                if (lane.IsOpen && lane.Stage.OpenLanes > 1) s.Runner.CloseLane(lane);
                else s.Runner.OpenLane(lane);
            }
            s.Runner.Run();

            Assert.That(s.Log.Entries.Count(e => e.Kind == LogKind.LaneClosed), Is.GreaterThan(0));
            AssertInvariants(s);
        }

        [Test]
        public void RuleBreakdownsAndActions_KeepEveryInvariant([Range(1, 20)] int seed)
        {
            Setup s = Build(seed);
            s.Runner.AddRule(new StaffingRule(s.Stage, openAbove: 2.0, closeBelow: 0.5, setupTime: 1.0));
            s.Runner.AddBreakdown(s.Stage, 30.0 + seed, Distribution.FromMeanCv(8.0, 0.5), s.Streams.Get("breakdown:0"));
            s.Runner.AddBreakdown(s.Stage, 120.0, Distribution.FromMeanCv(8.0, 0.5), s.Streams.Get("breakdown:1"));
            Pcg32 manager = s.Streams.Get("test:manager");
            for (double t = 7.0; t < 200.0; t += 7.0)
            {
                s.Runner.Run(t);
                Station lane = s.Lanes[manager.NextUInt() % 3];
                switch (manager.NextUInt() % 3)
                {
                    case 0: s.Runner.OpenLane(lane); break;
                    case 1: if (!lane.IsOpen || lane.Stage.OpenLanes > 1) s.Runner.CloseLane(lane); break;
                    default: s.Runner.ReturnToRule(lane); break;
                }
            }
            s.Runner.Run();

            Assert.That(s.Log.Entries.Count(e => e.Kind == LogKind.LaneBroken), Is.EqualTo(2));
            Assert.That(s.Log.Entries.Count(e => e.Kind == LogKind.LaneRepaired), Is.EqualTo(2));
            Assert.That(s.Log.Entries.Any(e => e.PassengerId == -1 && !e.ByManager && e.Kind == LogKind.LaneClosed), Is.True, "the rule acted");
            AssertInvariants(s);
        }
    }
}
