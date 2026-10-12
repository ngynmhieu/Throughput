using System.Collections.Generic;
using Throughput.Core.Engine.Network;

namespace Throughput.Core.Engine.Policies
{
    /// <summary>Picks which lane a passenger joins (N3). It only ever sees open lanes (with room, for walkers).</summary>
    public interface IRoutingPolicy
    {
        /// <summary>Returns an index into <paramref name="lanes"/>, which is never empty.</summary>
        int Choose(IReadOnlyList<Station> lanes);
    }
}
