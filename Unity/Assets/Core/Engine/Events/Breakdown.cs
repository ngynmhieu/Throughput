using System;
using System.Collections.Generic;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Randomness;

namespace Throughput.Core.Engine.Events
{
    /// <summary>One event-deck entry (K7): at Time, one lane of Stage breaks until a repair drawn from Repair.</summary>
    internal sealed class Breakdown
    {
        private readonly List<Station> _choices = new List<Station>();

        internal Breakdown(Router stage, double time, IDistribution repair, Pcg32 rng)
        {
            Stage = stage;
            Time = time;
            Repair = repair;
            Rng = rng;
        }

        internal Router Stage { get; }

        internal double Time { get; }

        internal IDistribution Repair { get; }

        internal Pcg32 Rng { get; }

        /// <summary>An open working lane, or (if every open lane is broken) any working lane; null if all are broken.</summary>
        internal Station PickLane()
        {
            Collect(lane => lane.IsOpen && !lane.IsBroken);
            if (_choices.Count == 0) Collect(lane => !lane.IsBroken);
            if (_choices.Count == 0) return null;
            return _choices[(int)(Rng.NextUInt() % (uint)_choices.Count)];
        }

        private void Collect(Func<Station, bool> allowed)
        {
            _choices.Clear();
            foreach (Station lane in Stage.Lanes)
                if (allowed(lane)) _choices.Add(lane);
        }
    }
}
