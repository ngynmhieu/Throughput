using System;
using System.Collections.Generic;
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
        private readonly RngStreams _streams;
        private readonly Dictionary<Station, Pcg32> _byStation = new Dictionary<Station, Pcg32>();

        /// <summary>One shared patience stream (tests and single-station runs).</summary>
        public Reneging(IDistribution patience, Pcg32 rng)
        {
            _patience = patience ?? throw new ArgumentNullException(nameof(patience));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        /// <summary>One stream per station ("patience:&lt;station&gt;"), so a change at one desk doesn't reshuffle the others.</summary>
        public Reneging(IDistribution patience, RngStreams streams)
        {
            _patience = patience ?? throw new ArgumentNullException(nameof(patience));
            _streams = streams ?? throw new ArgumentNullException(nameof(streams));
        }

        public void OnJoinQueue(Runner runner, Station station, Passenger passenger)
        {
            runner.Schedule(runner.Now + _patience.Sample(StreamFor(station)), EventType.Renege, station, passenger);
        }

        private Pcg32 StreamFor(Station station)
        {
            if (_streams == null) return _rng;
            if (!_byStation.TryGetValue(station, out Pcg32 rng))
            {
                rng = _streams.Get("patience:" + station.Name);
                _byStation[station] = rng;
            }
            return rng;
        }
    }
}
