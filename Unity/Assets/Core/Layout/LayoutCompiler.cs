using System;
using System.Collections.Generic;
using System.Linq;
using Throughput.Core.Engine.Behaviours;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Levels;

namespace Throughput.Core.Layout
{
    /// <summary>The plan can't be built. Problems lists every reason, in plain English (L8).</summary>
    public sealed class PlanException : Exception
    {
        public PlanException(IReadOnlyList<string> problems)
            : base("The plan can't be built:\n- " + string.Join("\n- ", problems))
        {
            Problems = problems;
        }

        public IReadOnlyList<string> Problems { get; }
    }

    /// <summary>A built network, ready to run.</summary>
    public sealed class CompiledRun
    {
        internal CompiledRun(Runner runner, IReadOnlyList<Station> stations)
        {
            Runner = runner;
            Stations = stations;
            StaffByStation = stations.ToDictionary(s => s.Name, s => s.Staff);
            LineCapacity = stations.Where(s => s.LineCapacity != int.MaxValue).ToDictionary(s => s.Name, s => s.LineCapacity);
        }

        public Runner Runner { get; }

        /// <summary>First to last.</summary>
        public IReadOnlyList<Station> Stations { get; }

        public IReadOnlyDictionary<string, int> StaffByStation { get; }

        /// <summary>Only the limited waiting areas.</summary>
        public IReadOnlyDictionary<string, int> LineCapacity { get; }
    }

    /// <summary>Level + plan → network (L1, L2). Checks everything first and reports all problems at once.</summary>
    public static class LayoutCompiler
    {
        public static CompiledRun Compile(LevelDef level, PlanDef plan, ulong runSeed)
        {
            IReadOnlyList<string> problems = Check(level, plan);
            if (problems.Count > 0) throw new PlanException(problems);

            var streams = new RngStreams(runSeed);
            var stations = new List<Station>();
            foreach (PlannedStage ps in plan.Stages)
            {
                StageTypeDef type = level.Stages.First(t => t.Name == ps.Type);
                var station = new Station(type.Name, ps.Staff, type.Service.Build(), streams.Get("service:" + type.Name),
                    ps.LineCapacity ?? int.MaxValue);
                if (stations.Count > 0)
                    stations[stations.Count - 1].ConnectTo(station, plan.Stages[stations.Count - 1].WalkToNext);
                stations.Add(station);
            }

            Wave[] waves = level.Waves.Select(w => new Wave(w.Start, w.End, w.PerMinute)).ToArray();
            var source = new Source("entry", new ArrivalWaves(waves, streams.Get("arrivals:entry")), stations[0]);
            var runner = new Runner(source);
            if (level.Patience != null)
                runner.AddBehaviour(new Reneging(level.Patience.Build(), streams));
            return new CompiledRun(runner, stations);
        }

        public static IReadOnlyList<string> Check(LevelDef level, PlanDef plan)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var problems = new List<string>();
            CheckLevel(level, problems);
            CheckPlan(level, plan, problems);
            return problems;
        }

        private static void CheckLevel(LevelDef level, List<string> problems)
        {
            List<WaveDef> waves = level.Waves ?? new List<WaveDef>();
            if (waves.Count == 0)
                problems.Add("The level has no arrival waves.");
            WaveDef previous = null;
            for (int i = 0; i < waves.Count; i++)
            {
                WaveDef w = waves[i];
                if (w == null) { problems.Add($"Wave {i + 1} is empty."); continue; }
                if (!(w.Start >= 0) || !(w.End > w.Start) || double.IsInfinity(w.End))
                    problems.Add($"Wave {i + 1} must start at 0 or later and end after it starts ({w.Start}–{w.End}).");
                if (!(w.PerMinute >= 0) || double.IsInfinity(w.PerMinute))
                    problems.Add($"Wave {i + 1} needs a rate of 0 or more people per minute ({w.PerMinute}).");
                if (previous != null && w.Start < previous.End)
                    problems.Add($"Wave {i + 1} starts at {w.Start}, before wave {i} ends ({previous.End}).");
                previous = w;
            }

            if (level.Stages == null || level.Stages.Count == 0)
                problems.Add("The level offers no desk types.");
            var names = new HashSet<string>();
            foreach (StageTypeDef t in level.Stages ?? new List<StageTypeDef>())
            {
                if (t == null) { problems.Add("A desk type is empty."); continue; }
                if (string.IsNullOrEmpty(t.Name)) { problems.Add("A desk type has no name."); continue; }
                if (!names.Add(t.Name)) problems.Add($"Desk type '{t.Name}' is defined twice.");
                if (t.MaxStaff < 1) problems.Add($"Desk type '{t.Name}': max staff must be at least 1.");
                CheckDistribution(t.Service, $"Desk type '{t.Name}' service time", problems);
            }
            if (level.Patience != null)
                CheckDistribution(level.Patience, "Patience", problems);
            if (level.Space < 0)
                problems.Add($"The level's space can't be negative ({level.Space}).");
        }

        private static void CheckDistribution(DistributionDef d, string what, List<string> problems)
        {
            if (d == null) { problems.Add($"{what} is missing."); return; }
            try
            {
                d.Build();
            }
            catch (ArgumentException e)
            {
                problems.Add($"{what}: {FirstLine(e.Message)}");
            }
        }

        private static void CheckPlan(LevelDef level, PlanDef plan, List<string> problems)
        {
            if (plan.Stages == null || plan.Stages.Count == 0)
            {
                problems.Add("The plan needs at least one stage.");
                return;
            }

            var used = new HashSet<string>();
            int space = 0;
            for (int i = 0; i < plan.Stages.Count; i++)
            {
                PlannedStage ps = plan.Stages[i];
                if (ps == null) { problems.Add($"Stage {i + 1} is empty."); continue; }
                if (string.IsNullOrEmpty(ps.Type)) { problems.Add($"Stage {i + 1} doesn't say which desk type it is."); continue; }
                string name = $"'{ps.Type}'";
                StageTypeDef type = (level.Stages ?? new List<StageTypeDef>()).FirstOrDefault(t => t != null && t.Name == ps.Type);
                if (type == null)
                {
                    problems.Add($"Stage {i + 1}: this level has no desk type {name}.");
                    continue;
                }
                if (!used.Add(ps.Type))
                    problems.Add($"Stage {i + 1}: {name} is used twice (one stage per desk type until lanes arrive).");
                if (ps.Staff < 1 || ps.Staff > type.MaxStaff)
                    problems.Add($"Stage {i + 1} {name}: {ps.Staff} staff (this level allows 1 to {type.MaxStaff}).");
                if (i == 0 && ps.LineCapacity.HasValue)
                    problems.Add($"Stage 1 {name}: the first waiting area has no size (people wait in the hall).");
                if (i > 0 && ps.LineCapacity.HasValue)
                {
                    if (ps.LineCapacity.Value < 1)
                        problems.Add($"Stage {i + 1} {name}: the waiting area must hold at least 1 person.");
                    else
                        space += ps.LineCapacity.Value;
                }
                if (!(ps.WalkToNext >= 0) || double.IsInfinity(ps.WalkToNext))
                    problems.Add($"Stage {i + 1} {name}: walk time must be 0 or more ({ps.WalkToNext}).");
            }
            if (space > level.Space)
                problems.Add($"Waiting areas use {space} places, but this level has space for {level.Space}.");
        }

        private static string FirstLine(string message)
        {
            int cut = message.IndexOf(Environment.NewLine, StringComparison.Ordinal);
            return cut < 0 ? message : message.Substring(0, cut);
        }
    }
}
