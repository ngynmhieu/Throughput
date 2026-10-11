using System;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Randomness;

namespace Throughput.Core.Engine.Behaviours
{
    /// <summary>
    /// Each passenger who joins a line draws a patience time. If they're still in line when it
    /// runs out, they give up and leave (a missed flight). Starting service cancels it: the
    /// Renege event still fires but finds them gone from the line and does nothing.
    /// </summary>
    public sealed class Reneging : IBehaviour
    {
        private readonly IDistribution _patience;
        private readonly Pcg32 _rng;

        public Reneging(IDistribution patience, Pcg32 rng)
        {
            _patience = patience ?? throw new ArgumentNullException(nameof(patience));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public void OnJoinQueue(Runner runner, Station station, Passenger passenger)
        {
            runner.Schedule(runner.Now + _patience.Sample(_rng), EventType.Renege, station, passenger);
        }
    }
}
