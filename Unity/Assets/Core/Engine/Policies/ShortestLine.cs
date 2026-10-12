using System.Collections.Generic;
using Throughput.Core.Engine.Network;

namespace Throughput.Core.Engine.Policies
{
    /// <summary>The lane with the fewest people; ties go to the first lane (N3).</summary>
    public sealed class ShortestLine : IRoutingPolicy
    {
        public static readonly ShortestLine Instance = new ShortestLine();

        public int Choose(IReadOnlyList<Station> lanes)
        {
            int best = 0;
            for (int i = 1; i < lanes.Count; i++)
                if (PeopleAt(lanes[i]) < PeopleAt(lanes[best]))
                    best = i;
            return best;
        }

        /// <summary>Waiting in line + walking there + at the desk (being served or blocked).</summary>
        public static int PeopleAt(Station lane) => lane.QueueLength + lane.WalkingHere + lane.Busy;
    }
}
