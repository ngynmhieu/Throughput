using System;
using System.Collections.Generic;
using Throughput.Core.Engine.Randomness;

namespace Throughput.Core.Levels
{
    /// <summary>What a level offers (L1–L9). Times in minutes (L4).</summary>
    public sealed class LevelDef
    {
        public string Name { get; set; }

        /// <summary>Flight banks, in order, not overlapping (L7).</summary>
        public List<WaveDef> Waves { get; set; } = new List<WaveDef>();

        /// <summary>The desk types the player may use.</summary>
        public List<StageTypeDef> Stages { get; set; } = new List<StageTypeDef>();

        /// <summary>How long people wait before giving up; null = nobody gives up.</summary>
        public DistributionDef Patience { get; set; }

        /// <summary>The event deck (K7, D12): the same breakdowns every attempt, at random times.</summary>
        public List<BreakdownDef> Breakdowns { get; set; } = new List<BreakdownDef>();

        /// <summary>Total places in waiting areas between desks (the hall before the first desk doesn't count, L6).</summary>
        public int Space { get; set; }

        /// <summary>The main goal (D9). Checked in S9.</summary>
        public int MaxMissedFlights { get; set; }
    }

    public sealed class WaveDef
    {
        public double Start { get; set; }

        public double End { get; set; }

        public double PerMinute { get; set; }
    }

    public sealed class StageTypeDef
    {
        public string Name { get; set; }

        public DistributionDef Service { get; set; }

        public int MaxStaff { get; set; }
    }

    /// <summary>One desk of this type breaks at a random time between From and To (minutes) and is repaired after Repair.</summary>
    public sealed class BreakdownDef
    {
        public string Stage { get; set; }

        public double From { get; set; }

        public double To { get; set; }

        public DistributionDef Repair { get; set; }
    }

    /// <summary>
    /// A duration (L5): Mean is the total average. With Min, it always takes at least Min plus a random part
    /// whose CV is Cv. Max is a safety net: values above it are drawn again.
    /// </summary>
    public sealed class DistributionDef
    {
        public double Mean { get; set; }

        public double Cv { get; set; } = 1.0;

        public double Min { get; set; }

        public double? Max { get; set; }

        /// <summary>Builds the distribution; throws ArgumentException (with a plain message) if the numbers don't make sense.</summary>
        public IDistribution Build()
        {
            if (!(Mean > Min))
                throw new ArgumentException($"mean ({Mean}) must be above min ({Min}).");
            IDistribution d = Distribution.FromMeanCv(Mean - Min, Cv);
            if (Min > 0) d = new Shifted(Min, d);
            if (Max.HasValue)
            {
                if (!(Max.Value > Mean))
                    throw new ArgumentException($"max ({Max.Value}) must be above the mean ({Mean}).");
                d = new Bounded(Min, Max.Value, d);
            }
            return d;
        }
    }

    /// <summary>What the player built (L1, L2): a chain of stages, first to last.</summary>
    public sealed class PlanDef
    {
        public List<PlannedStage> Stages { get; set; } = new List<PlannedStage>();
    }

    public sealed class PlannedStage
    {
        /// <summary>A desk type name from the level.</summary>
        public string Type { get; set; }

        /// <summary>Shared line: the number of desks. Lanes: how many lanes are open at the start (the first ones, N8).</summary>
        public int Staff { get; set; }

        /// <summary>Separate lanes (N1, N8): this many lanes, each with one desk and its own line. Null = one shared line.</summary>
        public int? Lanes { get; set; }

        /// <summary>Waiting-area size in people (per lane, with lanes); null = unlimited. Must be null for the first stage (L6).</summary>
        public int? LineCapacity { get; set; }

        /// <summary>Walk time to the next stage, in minutes. Ignored for the last stage.</summary>
        public double WalkToNext { get; set; }

        /// <summary>Staffing rule for a stage with lanes (K1–K5); null = lanes change only by the manager.</summary>
        public RuleDef Rule { get; set; }
    }

    /// <summary>Open a lane above OpenAbove people waiting per open lane, close one below CloseBelow; Setup in minutes (K3, K4).</summary>
    public sealed class RuleDef
    {
        public double OpenAbove { get; set; }

        public double CloseBelow { get; set; }

        public double Setup { get; set; }
    }
}
