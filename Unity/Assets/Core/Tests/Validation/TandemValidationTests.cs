using NUnit.Framework;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Results;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Validation
{
    /// <summary>
    /// Two M/M/1 stages in a row with unlimited lines. Burke's theorem: each stage waits like its own
    /// M/M/1 with the same λ (docs/game-mechanism/knowledge/formulas/tandem-burke.md). λ = 0.8, μ = 1.
    /// </summary>
    public class TandemValidationTests
    {
        private const double Lambda = 0.8;
        private const double Mu = 1.0;

        private static (double Id, double Scan) Measure(ulong seed, int passengers)
        {
            var streams = new RngStreams(seed);
            var id = new Station("id", 1, new Exponential(1.0 / Mu), streams.Get("service:id"));
            var scan = new Station("scan", 1, new Exponential(1.0 / Mu), streams.Get("service:scan"));
            id.ConnectTo(scan);
            double gapMean = 1.0 / Lambda;
            var source = new Source("entry", new RenewalArrivals(new Exponential(gapMean), streams.Get("arrivals:entry")),
                id, stopTime: passengers * gapMean);
            var runner = new Runner(source);
            var log = new RunLog(keepEntries: false);
            runner.AddObserver(log);
            runner.Run();

            double warmup = 1000.0 * gapMean;
            double sumId = 0.0, sumScan = 0.0;
            int count = 0;
            foreach (PassengerRecord p in log.Passengers)
            {
                if (p.QueueJoin < warmup) continue;
                sumId += p.Visits[0].Wait;
                sumScan += p.Visits[1].Wait;
                count++;
            }
            return (sumId / count, sumScan / count);
        }

        [Test]
        public void Fast_EachStageWaitsLikeMM1()
        {
            var (id, scan) = Measure(7UL, 200_000);
            double theory = Theory.MM1Wq(Lambda, Mu);
            string detail = $"ID {id:F3}, scanner {scan:F3} vs theory {theory:F3} each";
            TestContext.WriteLine(detail);
            Assert.That(id, Is.EqualTo(theory).Within(10).Percent, detail);
            Assert.That(scan, Is.EqualTo(theory).Within(10).Percent, detail);
        }

        [Test, Category("Strict")]
        public void Strict_EachStageWithin3PercentAndInsideCI()
        {
            double theory = Theory.MM1Wq(Lambda, Mu);
            StrictCheck.AssertMeanMatches(seed => Measure(seed, 300_000).Id, theory, "Tandem stage 1 (ID)");
            StrictCheck.AssertMeanMatches(seed => Measure(seed, 300_000).Scan, theory, "Tandem stage 2 (scanner)");
        }
    }
}
