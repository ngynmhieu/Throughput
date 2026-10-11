using Throughput.Core.Results;
using Throughput.Core.Engine.Behaviours;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Randomness;

namespace Throughput.Core.Tests.Support
{
    /// <summary>Builds and runs the standard test setups.</summary>
    public static class Experiments
    {
        /// <summary>One source → one station ("desk") → exit. Runs until everyone has left.</summary>
        public static RunLog RunSingleStation(ulong seed, IDistribution gap, IDistribution service, int staff,
            double stopTime, bool keepEntries, IDistribution patience = null)
        {
            var streams = new RngStreams(seed);
            var station = new Station("desk", staff, service, streams.Get("service:desk"));
            var source = new Source("entry", new RenewalArrivals(gap, streams.Get("arrivals:entry")), station, stopTime);
            var runner = new Runner(source);
            if (patience != null)
                runner.AddBehaviour(new Reneging(patience, streams.Get("patience:desk")));
            var log = new RunLog(keepEntries);
            runner.AddObserver(log);
            runner.Run();
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

        /// <summary>
        /// One run of about <paramref name="passengers"/> arrivals; returns the mean wait in line,
        /// skipping the first ~1000 arrivals (the line starts empty, which would bias the mean low).
        /// </summary>
        public static double MeasureMeanWait(ulong seed, IDistribution gap, IDistribution service, int staff, int passengers)
        {
            var log = RunSingleStation(seed, gap, service, staff, stopTime: passengers * gap.Mean, keepEntries: false);
            return MeanWait(log, warmup: 1000.0 * gap.Mean);
        }

        /// <summary>Share of passengers who joined at or after <paramref name="warmup"/> and gave up.</summary>
        public static double AbandonShare(RunLog log, double warmup)
        {
            int gaveUp = 0;
            int count = 0;
            foreach (PassengerRecord p in log.Passengers)
            {
                if (p.QueueJoin < warmup) continue;
                if (p.HasReneged) gaveUp++;
                count++;
            }
            return count == 0 ? double.NaN : (double)gaveUp / count;
        }

        /// <summary>One Erlang A run (M/M/c+M): mean time in line over everyone, and the share who gave up.</summary>
        public static (double Wait, double Abandon) MeasureErlangA(ulong seed, double lambda, double mu, int c, double theta, int passengers)
        {
            var gap = new Exponential(1.0 / lambda);
            var log = RunSingleStation(seed, gap, new Exponential(1.0 / mu), c, stopTime: passengers * gap.Mean,
                keepEntries: false, patience: new Exponential(1.0 / theta));
            double warmup = 1000.0 * gap.Mean;
            return (MeanWait(log, warmup), AbandonShare(log, warmup));
        }
    }
}
