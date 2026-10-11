using Throughput.Core.Results;
using Throughput.Core.Sim.Events;
using Throughput.Core.Sim.Network;
using Throughput.Core.Sim.Randomness;

namespace Throughput.Core.Tests.Support
{
    /// <summary>Builds and runs the standard test setups.</summary>
    public static class Experiments
    {
        /// <summary>One source → one station ("desk") → exit. Runs until everyone has left.</summary>
        public static RunLog RunSingleStation(ulong seed, IDistribution gap, IDistribution service, int staff,
            double stopTime, bool keepEntries)
        {
            var streams = new RngStreams(seed);
            var station = new Station("desk", staff, service, streams.Get("service:desk"));
            var source = new Source("entry", new RenewalArrivals(gap, streams.Get("arrivals:entry")), station, stopTime);
            var sim = new Simulator(source);
            var log = new RunLog(keepEntries);
            sim.AddObserver(log);
            sim.Run();
            return log;
        }

        /// <summary>Mean wait in line of passengers who joined at or after <paramref name="warmup"/> (skips the empty start).</summary>
        public static double MeanWait(RunLog log, double warmup)
        {
            double sum = 0.0;
            int count = 0;
            foreach (PassengerRecord p in log.Passengers)
            {
                if (p.QueueJoin < warmup) continue;
                sum += p.Wait;
                count++;
            }
            return count == 0 ? double.NaN : sum / count;
        }
    }
}
