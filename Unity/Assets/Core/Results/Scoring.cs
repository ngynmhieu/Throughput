using System;
using System.Collections.Generic;
using System.Globalization;
using Throughput.Core.Levels;

namespace Throughput.Core.Results
{
    /// <summary>One optional extra challenge and how the run did (D9, G1).</summary>
    public sealed class ExtraResult
    {
        public ExtraResult(string name, double target, double actual, bool met)
        {
            Name = name;
            Target = target;
            Actual = actual;
            Met = met;
        }

        public string Name { get; }

        public double Target { get; }

        public double Actual { get; }

        public bool Met { get; }
    }

    /// <summary>Pass or fail on the level's one goal, plus the extra ticks (D9, G6). No stars.</summary>
    public sealed class RunResult
    {
        public RunResult(bool passed, int missedFlights, int maxMissedFlights, IReadOnlyList<ExtraResult> extras)
        {
            Passed = passed;
            MissedFlights = missedFlights;
            MaxMissedFlights = maxMissedFlights;
            Extras = extras;
        }

        public bool Passed { get; }

        public int MissedFlights { get; }

        public int MaxMissedFlights { get; }

        public IReadOnlyList<ExtraResult> Extras { get; }
    }

    public static class Scoring
    {
        public static RunResult Score(LevelDef level, RunSummary summary)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (summary == null) throw new ArgumentNullException(nameof(summary));

            var extras = new List<ExtraResult>();
            if (level.Extras?.MaxWait is double maxWait)
                extras.Add(new ExtraResult($"Nobody waits over {Number(maxWait)} min", maxWait, summary.LongestWait,
                    summary.LongestWait <= maxWait));
            if (level.Extras?.MaxStaffMinutes is double maxCost)
                extras.Add(new ExtraResult($"Spend at most {Number(maxCost)} staff-minutes", maxCost, summary.StaffMinutes,
                    summary.StaffMinutes <= maxCost));

            bool passed = summary.MissedFlights <= level.MaxMissedFlights;
            return new RunResult(passed, summary.MissedFlights, level.MaxMissedFlights, extras);
        }

        private static string Number(double x) => x.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
