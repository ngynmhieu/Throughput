using System;
using System.Collections.Generic;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Policies;

namespace Throughput.Core.Engine.Network
{
    /// <summary>
    /// The entrance of a stage (N2): picks one of its lanes for each passenger. A shared line is a stage
    /// with one lane. Desks upstream that can't send anyone (every open lane full) wait here and are
    /// released first blocked, first out, to whichever lane gets room (N9).
    /// </summary>
    public sealed class Router
    {
        private readonly Station[] _lanes;
        private readonly IRoutingPolicy _policy;
        private readonly LinkedList<(Station From, Passenger Passenger)> _blocked = new LinkedList<(Station, Passenger)>();
        private readonly List<Station> _choices = new List<Station>();

        public Router(string name, IReadOnlyList<Station> lanes, IRoutingPolicy policy)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A stage needs a name.", nameof(name));
            if (lanes == null || lanes.Count == 0) throw new ArgumentException("A stage needs at least one lane.", nameof(lanes));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _lanes = new Station[lanes.Count];
            for (int i = 0; i < lanes.Count; i++)
            {
                Station lane = lanes[i] ?? throw new ArgumentException("Lanes can't be null.", nameof(lanes));
                if (Array.IndexOf(_lanes, lane, 0, i) >= 0)
                    throw new ArgumentException($"Station '{lane.Name}' is listed twice.", nameof(lanes));
                if (lane.Stage != null)
                    throw new ArgumentException($"Station '{lane.Name}' is already a lane of stage '{lane.Stage.Name}'.", nameof(lanes));
                _lanes[i] = lane;
            }
            foreach (Station lane in _lanes)
                lane.Stage = this; // only after every check passed, so a failed router claims nothing
            Name = name;
        }

        public string Name { get; }

        public IReadOnlyList<Station> Lanes => _lanes;

        public int OpenLanes
        {
            get
            {
                int open = 0;
                foreach (Station lane in _lanes)
                    if (lane.IsOpen) open++;
                return open;
            }
        }

        /// <summary>Some open lane can take one more walker.</summary>
        internal bool HasRoom
        {
            get
            {
                foreach (Station lane in _lanes)
                    if (lane.IsOpen && lane.HasRoom) return true;
                return false;
            }
        }

        /// <summary>The station's own stage, or a new one-lane stage for it (a shared line).</summary>
        internal static Router For(Station station) =>
            station.Stage ?? new Router(station.Name, new[] { station }, ShortestLine.Instance);

        /// <summary>For a walker from a desk upstream: an open lane with room (they reserve a place there), or null.</summary>
        internal Station PickWithRoom() => Pick(needRoom: true);

        /// <summary>For someone from the entrance: any open lane (lines in the hall have no limit, B4), or null.</summary>
        internal Station PickAny() => Pick(needRoom: false);

        internal void Block(Station from, Passenger passenger) => _blocked.AddLast((from, passenger));

        /// <summary>Room opened in a lane: blocked desks upstream move their passengers on, first blocked first.</summary>
        internal void ReleaseBlocked(Runner runner)
        {
            while (_blocked.Count > 0 && HasRoom)
            {
                (Station from, Passenger passenger) = _blocked.First.Value;
                _blocked.RemoveFirst();
                from.Unblock(runner, passenger);
            }
        }

        private Station Pick(bool needRoom)
        {
            _choices.Clear();
            foreach (Station lane in _lanes)
                if (lane.IsOpen && (!needRoom || lane.HasRoom))
                    _choices.Add(lane);
            if (_choices.Count == 0) return null;
            int i = _policy.Choose(_choices);
            if (i < 0 || i >= _choices.Count)
                throw new InvalidOperationException($"The routing policy chose lane {i} of {_choices.Count} at '{Name}'.");
            return _choices[i];
        }
    }
}
