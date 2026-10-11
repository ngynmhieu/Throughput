using System.Linq;
using NUnit.Framework;
using Throughput.Core.Layout;
using Throughput.Core.Levels;
using Throughput.Core.Results;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Layout
{
    public class LayoutCompilerTests
    {
        private static LevelDef Level => LevelJson.ReadLevel(SampleLevels.Level);

        [Test]
        public void Compile_SampleBuildsTheChain()
        {
            CompiledRun run = LayoutCompiler.Compile(Level, LevelJson.ReadPlan(SampleLevels.Plan), runSeed: 7UL);

            Assert.That(run.Stations.Select(s => s.Name), Is.EqualTo(new[] { "id", "scanner" }));
            Assert.That(run.Stations[0].Staff, Is.EqualTo(3));
            Assert.That(run.Stations[0].Next, Is.SameAs(run.Stations[1]));
            Assert.That(run.Stations[0].WalkTime, Is.EqualTo(0.5));
            Assert.That(run.Stations[1].LineCapacity, Is.EqualTo(12));
            Assert.That(run.Stations[1].Next, Is.Null);
            Assert.That(run.LineCapacity.Keys, Is.EqualTo(new[] { "scanner" }));
        }

        [Test]
        public void Compile_SampleRunsToTheEnd()
        {
            CompiledRun run = LayoutCompiler.Compile(Level, LevelJson.ReadPlan(SampleLevels.Plan), runSeed: 7UL);
            var log = new RunLog();
            run.Runner.AddObserver(log);
            run.Runner.Run();

            Assert.That(run.Runner.IsFinished, Is.True);
            Assert.That(log.Passengers.Count, Is.EqualTo(135).Within(25).Percent); // 30×1.5 + 30×3 = 135 expected
            Assert.That(log.Passengers.All(p => p.Arrive < 60.0), Is.True);
            InvariantChecker.AssertAll(log, run.StaffByStation, lineCapacity: run.LineCapacity);
        }

        [Test]
        public void Compile_SameSeedSameRun()
        {
            string[] Once()
            {
                CompiledRun run = LayoutCompiler.Compile(Level, LevelJson.ReadPlan(SampleLevels.Plan), runSeed: 9UL);
                var log = new RunLog();
                run.Runner.AddObserver(log);
                run.Runner.Run();
                return log.Entries.Select(e => e.ToString()).ToArray();
            }
            Assert.That(Once(), Is.EqualTo(Once()));
        }

        [Test]
        public void Compile_ListsEveryProblem()
        {
            var plan = new PlanDef();
            plan.Stages.Add(new PlannedStage { Type = "id", Staff = 9, LineCapacity = 5 });   // too many staff; first area has no size
            plan.Stages.Add(new PlannedStage { Type = "xray", Staff = 1 });                    // unknown desk type
            plan.Stages.Add(new PlannedStage { Type = "scanner", Staff = 1, LineCapacity = 30 }); // more than the level's space (20)
            plan.Stages.Add(new PlannedStage { Type = "scanner", Staff = 1, LineCapacity = 1 });  // used twice

            var e = Assert.Throws<PlanException>(() => LayoutCompiler.Compile(Level, plan, 1UL));
            string all = string.Join("\n", e.Problems);
            StringAssert.Contains("'id': 9 staff", all);
            StringAssert.Contains("first waiting area", all);
            StringAssert.Contains("'xray'", all);
            StringAssert.Contains("space", all);
            StringAssert.Contains("'scanner' is used twice", all);
            Assert.That(e.Problems.Count, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void Check_FindsLevelProblems()
        {
            LevelDef level = Level;
            level.Waves.Add(new WaveDef { Start = 50, End = 70, PerMinute = 1 }); // overlaps the second wave
            level.Stages[1].Service.Max = 0.5;                                    // max below the mean
            var problems = LayoutCompiler.Check(level, LevelJson.ReadPlan(SampleLevels.Plan));
            string all = string.Join("\n", problems);
            StringAssert.Contains("Wave 3", all);
            StringAssert.Contains("'scanner'", all);
        }

        [Test]
        public void Check_EmptyPlan()
        {
            StringAssert.Contains("at least one", string.Join("\n", LayoutCompiler.Check(Level, new PlanDef())));
        }
    }
}
