using System.Linq;
using NUnit.Framework;
using Throughput.Core.Layout;
using Throughput.Core.Levels;
using Throughput.Core.Results;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Validation
{
    /// <summary>
    /// P3, fair not lucky (G7): on the sample level a good plan passes on almost every seed and a bad plan fails on
    /// almost every seed, so the result depends on the plan, not the dice.
    /// </summary>
    public class FairnessTests
    {
        private const int Seeds = 200;

        private static int Passes(string planJson)
        {
            int passes = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                LevelDef level = LevelJson.ReadLevel(SampleLevels.Level);
                CompiledRun run = LayoutCompiler.Compile(level, LevelJson.ReadPlan(planJson), (ulong)seed);
                var stats = new RunStats(run.Stations);
                run.Runner.AddObserver(stats);
                run.Runner.Run();
                if (Scoring.Score(level, stats.Summarize(run.Runner.LastActiveTime)).Passed) passes++;
            }
            return passes;
        }

        [Test]
        public void GoodPlan_PassesAlmostAlways()
        {
            int passes = Passes(SampleLevels.Plan);
            TestContext.Out.WriteLine($"Good plan passed {passes}/{Seeds}");
            Assert.That(passes, Is.GreaterThanOrEqualTo(190));
        }

        [Test]
        public void ShortStaffedPlan_FailsAlmostAlways()
        {
            int passes = Passes(SampleLevels.ShortStaffedPlan);
            TestContext.Out.WriteLine($"Short-staffed plan passed {passes}/{Seeds}");
            Assert.That(Seeds - passes, Is.GreaterThanOrEqualTo(160));
        }
    }
}
