using System;
using NUnit.Framework;
using Throughput.Core.Engine.Behaviours;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Results;

namespace Throughput.Core.Tests.Engine
{
    /// <summary>Running in steps while the player watches (A2 revised, decisions C2–C4).</summary>
    public class SteppingTests
    {
        private static (Runner runner, RunLog log) Build(double service, params double[] arrivals)
        {
            var station = new Station("desk", 1, new Deterministic(service), new Pcg32(1UL, 1UL));
            var runner = new Runner(new Source("entry", new FixedArrivals(arrivals), station));
            var log = new RunLog();
            runner.AddObserver(log);
            return (runner, log);
        }

        [Test]
        public void RunUntil_StopsClockExactly()
        {
            // 1 desk, 120 s each. A at 0, B at 30. Stop at 50: both arrived, A still being served.
            var (runner, log) = Build(120.0, 0.0, 30.0);
            runner.Run(50.0);

            Assert.That(runner.Now, Is.EqualTo(50.0));
            Assert.That(log.Passengers.Count, Is.EqualTo(2));
            Assert.That(double.IsNaN(log.Passengers[0].ServiceEnd), Is.True);
            Assert.That(runner.IsFinished, Is.False);

            runner.Run(55.0); // a step with no events: only the clock moves
            Assert.That(runner.Now, Is.EqualTo(55.0));

            runner.Run();
            Assert.That(runner.Now, Is.EqualTo(240.0));
            Assert.That(runner.IsFinished, Is.True);
        }

        [Test]
        public void RunUntil_HandlesEventAtExactlyUntil()
        {
            var (runner, log) = Build(120.0, 0.0, 30.0);
            runner.Run(30.0);
            Assert.That(log.Passengers.Count, Is.EqualTo(2));
        }

        [Test]
        public void RunUntil_RejectsThePast()
        {
            var (runner, _) = Build(120.0, 0.0, 30.0);
            runner.Run(50.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => runner.Run(10.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => runner.Run(double.NaN));
            runner.Run(50.0); // the same time is fine (e.g. paused)
        }

        [Test]
        public void LastActiveTime_IgnoresDeadPatienceAlarms()
        {
            // 1 desk, 300 s each, patience 1000 s. A at 0, B at 50. Both are served: A 0–300, B 300–600.
            // Their alarms (1000 and 1050) fire later and do nothing: Now ends at 1050, the run really ended at 600.
            var (runner, _) = Build(300.0, 0.0, 50.0);
            runner.AddBehaviour(new Reneging(new Deterministic(1000.0), new Pcg32(1UL, 3UL)));
            runner.Run();

            Assert.That(runner.Now, Is.EqualTo(1050.0));
            Assert.That(runner.LastActiveTime, Is.EqualTo(600.0));
            Assert.That(runner.IsFinished, Is.True);
        }

        [Test]
        public void AfterFinished_RunUntilOnlyMovesTheClock()
        {
            var (runner, log) = Build(120.0, 0.0);
            runner.Run();
            int entries = log.Entries.Count;
            runner.Run(1000.0);
            Assert.That(runner.Now, Is.EqualTo(1000.0));
            Assert.That(log.Entries.Count, Is.EqualTo(entries));
            Assert.That(runner.LastActiveTime, Is.EqualTo(120.0));
        }
    }
}
