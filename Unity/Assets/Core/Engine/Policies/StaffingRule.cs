using System;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;

namespace Throughput.Core.Engine.Policies
{
    /// <summary>
    /// Opens a lane when more than OpenAbove people wait per open lane, closes one when fewer than CloseBelow wait
    /// (K3). It opens the lowest-numbered closed lane after SetupTime, one at a time, and closes the
    /// highest-numbered open lane at once (it drains), never the last. Lanes the manager pinned are left alone (K2, K4).
    /// </summary>
    public sealed class StaffingRule
    {
        public StaffingRule(Router stage, double openAbove, double closeBelow, double setupTime)
        {
            Stage = stage ?? throw new ArgumentNullException(nameof(stage));
            if (!(closeBelow >= 0) || double.IsInfinity(closeBelow))
                throw new ArgumentOutOfRangeException(nameof(closeBelow), closeBelow, "Close below must be 0 or more.");
            if (!(openAbove > closeBelow) || double.IsInfinity(openAbove))
                throw new ArgumentOutOfRangeException(nameof(openAbove), openAbove, "Open above must be more than close below.");
            if (!(setupTime >= 0) || double.IsInfinity(setupTime))
                throw new ArgumentOutOfRangeException(nameof(setupTime), setupTime, "Setup time must be 0 or more.");
            OpenAbove = openAbove;
            CloseBelow = closeBelow;
            SetupTime = setupTime;
        }

        public Router Stage { get; }

        public double OpenAbove { get; }

        public double CloseBelow { get; }

        public double SetupTime { get; }

        internal void Check(Runner runner)
        {
            int open = 0, waiting = 0;
            bool opening = false;
            foreach (Station lane in Stage.Lanes)
            {
                if (lane.IsOpen)
                {
                    open++;
                    waiting += lane.QueueLength;
                }
                if (lane.IsOpening) opening = true;
            }
            if (open == 0) return;
            double perLane = (double)waiting / open;

            if (perLane > OpenAbove)
            {
                if (opening) return;
                foreach (Station lane in Stage.Lanes)
                    if (!lane.IsOpen && !lane.IsManual && !lane.IsBroken)
                    {
                        runner.StartOpening(lane, SetupTime);
                        return;
                    }
            }
            else if (perLane < CloseBelow && open > 1)
            {
                for (int i = Stage.Lanes.Count - 1; i >= 0; i--)
                {
                    Station lane = Stage.Lanes[i];
                    if (lane.IsOpen && !lane.IsManual)
                    {
                        runner.CloseByRule(lane);
                        return;
                    }
                }
            }
        }
    }
}
