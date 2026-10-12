using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Throughput.Core.Engine.Network;
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
            Assert.That(run.Stations[0].Next.Lanes, Is.EqualTo(new[] { run.Stations[1] }));
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

        [Test]
        public void Check_NullsFromJson_AreProblemsNotCrashes()
        {
            LevelDef level = LevelJson.ReadLevel(@"{ ""name"": ""x"", ""waves"": null, ""stages"": [ null ], ""space"": 0 }");
            PlanDef plan = LevelJson.ReadPlan(@"{ ""stages"": [ null, { ""type"": null, ""staff"": 1 } ] }");
            var problems = LayoutCompiler.Check(level, plan);
            Assert.That(problems.Count, Is.GreaterThanOrEqualTo(3));
            Assert.Throws<PlanException>(() => LayoutCompiler.Compile(level, plan, 1UL));

            var nullPlan = LevelJson.ReadPlan(@"{ ""stages"": null }");
            StringAssert.Contains("at least one", string.Join("\n", LayoutCompiler.Check(Level, nullPlan)));
        }

        [Test]
        public void Check_InfiniteWave_IsAProblem()
        {
            LevelDef level = Level;
            level.Waves.Add(new WaveDef { Start = 60, End = double.PositiveInfinity, PerMinute = 1 });
            level.Waves[0].PerMinute = double.PositiveInfinity;
            var e = Assert.Throws<PlanException>(() => LayoutCompiler.Compile(level, LevelJson.ReadPlan(SampleLevels.Plan), 1UL));
            StringAssert.Contains("Wave", string.Join("\n", e.Problems));
        }

        [Test]
        public void Compile_Lanes_OneStationPerLane()
        {
            CompiledRun run = LayoutCompiler.Compile(Level, LevelJson.ReadPlan(SampleLevels.LanesPlan), runSeed: 7UL);

            Assert.That(run.Stations.Select(s => s.Name), Is.EqualTo(new[] { "id", "scanner 1", "scanner 2", "scanner 3" }));
            Assert.That(run.Stages.Select(s => s.Name), Is.EqualTo(new[] { "id", "scanner" }));
            Assert.That(run.Stages[1].Lanes.Select(s => s.Name), Is.EqualTo(new[] { "scanner 1", "scanner 2", "scanner 3" }));
            Assert.That(run.Stations.Select(s => s.IsOpen), Is.EqualTo(new[] { true, true, true, false }));
            Assert.That(run.Stations.Skip(1).Select(s => s.Staff), Is.All.EqualTo(1));
            Assert.That(run.Stations[0].Next, Is.SameAs(run.Stages[1]));
            Assert.That(run.LineCapacity, Is.EqualTo(new Dictionary<string, int> { { "scanner 1", 4 }, { "scanner 2", 4 }, { "scanner 3", 4 } }));
            Assert.That(run.Lane("scanner 3"), Is.SameAs(run.Stations[3]));
            Assert.Throws<System.ArgumentException>(() => run.Lane("xray"));
        }

        [Test]
        public void Compile_Lanes_RunsToTheEnd()
        {
            CompiledRun run = LayoutCompiler.Compile(Level, LevelJson.ReadPlan(SampleLevels.LanesPlan), runSeed: 7UL);
            var log = new RunLog();
            run.Runner.AddObserver(log);
            run.Runner.Run();

            Assert.That(run.Runner.IsFinished, Is.True);
            Assert.That(log.Passengers.SelectMany(p => p.Visits).Any(v => v.Station == "scanner 3"), Is.False, "scanner 3 is closed");
            InvariantChecker.AssertAll(log, run.StaffByStation, lineCapacity: run.LineCapacity, closedAtStart: run.ClosedAtStart);
        }

        [Test]
        public void Check_LaneRules()
        {
            var plan = new PlanDef();
            plan.Stages.Add(new PlannedStage { Type = "id", Staff = 1, Lanes = 0 });                       // at least 1 lane
            plan.Stages.Add(new PlannedStage { Type = "scanner", Staff = 3, Lanes = 2, LineCapacity = 8 }); // 3 staff for 2 lanes
            string all = string.Join("\n", LayoutCompiler.Check(Level, plan));
            StringAssert.Contains("Stage 1 'id': needs at least 1 lane", all);
            StringAssert.Contains("Stage 2 'scanner': 3 staff for 2 lanes", all);

            plan.Stages[0].Lanes = 5;                                     // id allows 4 desks
            plan.Stages[1] = new PlannedStage { Type = "scanner", Staff = 1, Lanes = 3, LineCapacity = 8 }; // 3 × 8 = 24 > 20 space
            all = string.Join("\n", LayoutCompiler.Check(Level, plan));
            StringAssert.Contains("Stage 1 'id': 5 lanes (this level allows up to 4)", all);
            StringAssert.Contains("use 24 places", all);
        }

        [Test]
        public void Check_LaneNamesMustNotClash()
        {
            // Lanes of "scanner" are named "scanner 1", "scanner 2": a desk type called "scanner 1" would clash.
            LevelDef level = Level;
            level.Stages.Add(new StageTypeDef { Name = "scanner 1", Service = new DistributionDef { Mean = 1.0 }, MaxStaff = 2 });
            var plan = new PlanDef();
            plan.Stages.Add(new PlannedStage { Type = "scanner", Staff = 1, Lanes = 2 });
            plan.Stages.Add(new PlannedStage { Type = "scanner 1", Staff = 1, LineCapacity = 2 });

            var e = Assert.Throws<PlanException>(() => LayoutCompiler.Compile(level, plan, 1UL));
            StringAssert.Contains("'scanner 1'", string.Join(" | ", e.Problems));
        }

        [Test]
        public void ManagerAction_ChangesOnlyWhatItAffects()
        {
            // Same seed, with and without opening scanner 3 at minute 20.
            RunLog Once(bool act)
            {
                CompiledRun run = LayoutCompiler.Compile(Level, LevelJson.ReadPlan(SampleLevels.LanesPlan), runSeed: 11UL);
                var log = new RunLog();
                run.Runner.AddObserver(log);
                run.Runner.Run(20.0);
                if (act) run.Runner.OpenLane(run.Lane("scanner 3"));
                run.Runner.Run();
                return log;
            }
            RunLog without = Once(false), with = Once(true), again = Once(true);

            Assert.That(again.Entries.Select(e => e.ToString()), Is.EqualTo(with.Entries.Select(e => e.ToString())), "same actions, same run");
            Assert.That(with.Entries.TakeWhile(e => e.Time < 20.0).Select(e => e.ToString()),
                Is.EqualTo(without.Entries.TakeWhile(e => e.Time < 20.0).Select(e => e.ToString())), "nothing before the action changes");
            Assert.That(with.Passengers.Select(p => p.Arrive), Is.EqualTo(without.Passengers.Select(p => p.Arrive)), "arrivals don't change");
            // The k-th ID check always draws the k-th number from "service:id", whoever it is (people giving up
            // at the ID line can differ between runs), so the ID desk times in start order must match.
            double[] withId = IdDeskTimes(with), withoutId = IdDeskTimes(without);
            int n = Math.Min(withId.Length, withoutId.Length);
            Assert.That(withId.Take(n), Is.EqualTo(withoutId.Take(n)), "ID desk times don't change");
            Assert.That(with.Passengers.SelectMany(p => p.Visits).Any(v => v.Station == "scanner 3"), Is.True, "the action did something");
        }

        private static double[] IdDeskTimes(RunLog log) =>
            log.Entries.Where(e => e.Kind == LogKind.StartService && e.Station == "id")
                .Select(e => log.Passengers[e.PassengerId].Visits[0])
                .Select(v => v.ServiceEnd - v.ServiceStart)
                .ToArray();

        private static LevelDef LevelWithBreakdown()
        {
            LevelDef level = Level;
            level.Breakdowns.Add(new BreakdownDef { Stage = "scanner", From = 20, To = 40, Repair = new DistributionDef { Mean = 5, Cv = 0.5 } });
            return level;
        }

        private static PlanDef LanesPlanWithRule()
        {
            PlanDef plan = LevelJson.ReadPlan(SampleLevels.LanesPlan);
            plan.Stages[1].Rule = new RuleDef { OpenAbove = 3, CloseBelow = 0.5, Setup = 2 };
            return plan;
        }

        [Test]
        public void Json_ReadsRuleAndBreakdowns()
        {
            LevelDef level = LevelJson.ReadLevel(@"{ ""name"": ""x"", ""waves"": [], ""stages"": [], ""space"": 0,
                ""breakdowns"": [ { ""stage"": ""scanner"", ""from"": 20, ""to"": 40, ""repair"": { ""mean"": 5 } } ] }");
            PlanDef plan = LevelJson.ReadPlan(@"{ ""stages"": [ { ""type"": ""scanner"", ""staff"": 1, ""lanes"": 2,
                ""rule"": { ""openAbove"": 3, ""closeBelow"": 1, ""setup"": 2 } } ] }");

            Assert.That((level.Breakdowns[0].Stage, level.Breakdowns[0].From, level.Breakdowns[0].To, level.Breakdowns[0].Repair.Mean),
                Is.EqualTo(("scanner", 20.0, 40.0, 5.0)));
            Assert.That((plan.Stages[0].Rule.OpenAbove, plan.Stages[0].Rule.CloseBelow, plan.Stages[0].Rule.Setup), Is.EqualTo((3.0, 1.0, 2.0)));
        }

        [Test]
        public void Compile_RuleAndBreakdown_RunsToTheEnd()
        {
            CompiledRun run = LayoutCompiler.Compile(LevelWithBreakdown(), LanesPlanWithRule(), runSeed: 7UL);
            var log = new RunLog();
            run.Runner.AddObserver(log);
            run.Runner.Run();

            Assert.That(run.Runner.IsFinished, Is.True);
            LogEntry broken = log.Entries.Single(e => e.Kind == LogKind.LaneBroken);
            Assert.That(broken.Time, Is.InRange(20.0, 40.0));
            Assert.That(broken.Station, Does.StartWith("scanner "));
            Assert.That(log.Entries.Single(e => e.Kind == LogKind.LaneRepaired).Time, Is.GreaterThan(broken.Time));
            Assert.That(log.Entries.Any(e => e.PassengerId == -1 && !e.ByManager && e.Kind == LogKind.LaneClosed), Is.True, "the rule acted");
            InvariantChecker.AssertAll(log, run.StaffByStation, lineCapacity: run.LineCapacity, closedAtStart: run.ClosedAtStart);
        }

        [Test]
        public void Breakdown_RandomTimeEachAttempt_SameForSameSeed()
        {
            double BrokeAt(ulong seed)
            {
                CompiledRun run = LayoutCompiler.Compile(LevelWithBreakdown(), LevelJson.ReadPlan(SampleLevels.LanesPlan), seed);
                var log = new RunLog();
                run.Runner.AddObserver(log);
                run.Runner.Run();
                return log.Entries.Single(e => e.Kind == LogKind.LaneBroken).Time;
            }

            Assert.That(BrokeAt(3UL), Is.EqualTo(BrokeAt(3UL)));
            Assert.That(new ulong[] { 1, 2, 3, 4, 5 }.Select(BrokeAt).Distinct().Count(), Is.GreaterThan(1));
        }

        [Test]
        public void Breakdown_OfAStageThePlanDoesntUse_IsSkipped()
        {
            LevelDef level = Level;
            level.Stages.Add(new StageTypeDef { Name = "xray", Service = new DistributionDef { Mean = 1 }, MaxStaff = 1 });
            level.Breakdowns.Add(new BreakdownDef { Stage = "xray", From = 5, To = 10, Repair = new DistributionDef { Mean = 5 } });
            CompiledRun run = LayoutCompiler.Compile(level, LevelJson.ReadPlan(SampleLevels.Plan), 1UL);
            var log = new RunLog();
            run.Runner.AddObserver(log);
            run.Runner.Run();
            Assert.That(log.Entries.Any(e => e.Kind == LogKind.LaneBroken), Is.False);
        }

        [Test]
        public void Check_RuleAndBreakdownProblems()
        {
            LevelDef level = Level;
            level.Breakdowns.Add(new BreakdownDef { Stage = "xray", From = 30, To = 10, Repair = new DistributionDef { Mean = 5 } });
            level.Breakdowns.Add(null);
            PlanDef plan = LevelJson.ReadPlan(SampleLevels.Plan); // shared lines
            plan.Stages[1].Rule = new RuleDef { OpenAbove = 1, CloseBelow = 2, Setup = -1 };

            string all = string.Join(" | ", LayoutCompiler.Check(level, plan));
            StringAssert.Contains("Breakdown 1: this level has no desk type 'xray'", all);
            StringAssert.Contains("Breakdown 1 must happen between", all);
            StringAssert.Contains("Breakdown 2 is empty", all);
            StringAssert.Contains("a staffing rule needs lanes", all);
            StringAssert.Contains("open above", all);
            StringAssert.Contains("setup time must be 0 or more", all);
        }
    }
}
