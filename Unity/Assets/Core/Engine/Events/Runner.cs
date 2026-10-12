using System;
using System.Collections.Generic;
using Throughput.Core.Engine.Behaviours;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Randomness;

namespace Throughput.Core.Engine.Events
{
    /// <summary>
    /// The event loop: take the earliest event, handle it, repeat. Time only moves by jumping
    /// to the next event; it never depends on frames or real time.
    /// </summary>
    public sealed class Runner
    {
        private readonly EventList _events = new EventList();
        private readonly List<IRunObserver> _observers = new List<IRunObserver>();
        private readonly List<IBehaviour> _behaviours = new List<IBehaviour>();
        private readonly List<Breakdown> _breakdowns = new List<Breakdown>();
        private readonly Source[] _sources;
        private int _nextPassengerId;
        private bool _started;

        public Runner(params Source[] sources)
        {
            if (sources == null || sources.Length == 0)
                throw new ArgumentException("A run needs at least one source.", nameof(sources));
            foreach (Source s in sources)
                if (s == null) throw new ArgumentException("Sources can't be null.", nameof(sources));
            _sources = (Source[])sources.Clone();
        }

        public double Now { get; private set; }

        /// <summary>Time of the last event that changed something (ignored patience alarms don't count): the run's real end.</summary>
        public double LastActiveTime { get; private set; }

        /// <summary>Nothing is left on the calendar: no arrivals to come, nobody in the checkpoint.</summary>
        public bool IsFinished => _started && _events.Count == 0;

        public void AddObserver(IRunObserver observer)
        {
            _observers.Add(observer ?? throw new ArgumentNullException(nameof(observer)));
        }

        public void AddBehaviour(IBehaviour behaviour)
        {
            _behaviours.Add(behaviour ?? throw new ArgumentNullException(nameof(behaviour)));
        }

        /// <summary>
        /// Event deck (K7): at <paramref name="time"/> one open working lane of the stage breaks, picked with
        /// <paramref name="rng"/>, and is repaired after a time drawn from <paramref name="repair"/>. Add before the run starts.
        /// </summary>
        public void AddBreakdown(Router stage, double time, IDistribution repair, Pcg32 rng)
        {
            if (stage == null) throw new ArgumentNullException(nameof(stage));
            if (repair == null) throw new ArgumentNullException(nameof(repair));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (!(time >= 0) || double.IsInfinity(time))
                throw new ArgumentOutOfRangeException(nameof(time), time, "A breakdown needs a time of 0 or more.");
            if (_started) throw new InvalidOperationException("Add breakdowns before the run starts.");
            _breakdowns.Add(new Breakdown(stage, time, repair, rng));
        }

        /// <summary>
        /// Manager action (N6, N7): the lane takes new people from now on and blocked desks upstream move on.
        /// Call it after Run(t), so it sees every event at or before t. Opening an open lane does nothing.
        /// </summary>
        public void OpenLane(Station lane)
        {
            if (lane == null) throw new ArgumentNullException(nameof(lane));
            if (lane.IsOpen) return;
            lane.SetOpen(true);
            Notify(lane, LaneChange.Opened, byManager: true);
            lane.Stage?.ReleaseBlocked(this);
        }

        /// <summary>
        /// Manager action (N5, N7): the lane gets nobody new but serves everyone already in its line, at its
        /// desk, or walking to it (it drains). The last open lane of a stage can't be closed.
        /// </summary>
        public void CloseLane(Station lane)
        {
            if (lane == null) throw new ArgumentNullException(nameof(lane));
            if (!lane.IsOpen) return;
            if (lane.Stage != null && lane.Stage.OpenLanes == 1)
                throw new InvalidOperationException($"'{lane.Name}' is the last open lane of '{lane.Stage.Name}'.");
            lane.SetOpen(false);
            Notify(lane, LaneChange.Closed, byManager: true);
        }

        /// <summary>
        /// Handles every event up to and including time <paramref name="until"/>, then stops the clock exactly
        /// at <paramref name="until"/> (when it's finite). Call it again with a later time to continue: this is
        /// how the game steps the run while the player watches. Run() with no argument runs to the end.
        /// </summary>
        public void Run(double until = double.PositiveInfinity)
        {
            if (double.IsNaN(until) || until < Now)
                throw new ArgumentOutOfRangeException(nameof(until), until, $"Can't run to {until}: the clock is already at {Now}.");

            if (!_started)
            {
                _started = true;
                foreach (Source source in _sources)
                    ScheduleNextArrival(source);
                foreach (Breakdown breakdown in _breakdowns)
                    Schedule(breakdown.Time, EventType.Breakdown, breakdown, null);
            }

            while (_events.Count > 0 && _events.Peek().Time <= until)
            {
                EngineEvent e = _events.Pop();
                Now = e.Time;
                if (Handle(e))
                    LastActiveTime = Now;
            }

            if (!double.IsPositiveInfinity(until))
                Now = until;
        }

        internal void Schedule(double time, EventType type, object node, Passenger passenger)
        {
            if (!(time >= Now))
                throw new InvalidOperationException($"Can't schedule an event at {time}: the clock is already at {Now}.");
            _events.Push(new EngineEvent(time, type, node, passenger));
        }

        internal void NotifyJoinQueue(Station station, Passenger p)
        {
            foreach (IRunObserver o in _observers) o.OnJoinQueue(Now, station, p);
            foreach (IBehaviour b in _behaviours) b.OnJoinQueue(this, station, p);
        }

        internal void NotifyStartService(Station station, Passenger p)
        {
            foreach (IRunObserver o in _observers) o.OnStartService(Now, station, p);
        }

        internal void NotifyEndService(Station station, Passenger p)
        {
            foreach (IRunObserver o in _observers) o.OnEndService(Now, station, p);
        }

        internal void NotifyBlocked(Station station, Passenger p)
        {
            foreach (IRunObserver o in _observers) o.OnBlocked(Now, station, p);
        }

        internal void NotifyUnblocked(Station station, Passenger p)
        {
            foreach (IRunObserver o in _observers) o.OnUnblocked(Now, station, p);
        }

        internal void NotifyRenege(Station station, Passenger p)
        {
            foreach (IRunObserver o in _observers) o.OnRenege(Now, station, p);
        }

        internal void NotifyExit(Passenger p)
        {
            foreach (IRunObserver o in _observers) o.OnExit(Now, p);
        }

        private void Notify(Station lane, LaneChange change, bool byManager)
        {
            foreach (IRunObserver o in _observers) o.OnLaneChanged(Now, lane, change, byManager);
        }

        private bool Handle(EngineEvent e)
        {
            switch (e.Type)
            {
                case EventType.Arrive:
                    var source = (Source)e.Node;
                    var passenger = new Passenger(_nextPassengerId++, Now);
                    foreach (IRunObserver o in _observers) o.OnArrive(Now, passenger);
                    ScheduleNextArrival(source);
                    Station lane = source.Target.PickAny()
                        ?? throw new InvalidOperationException($"Every lane of '{source.Target.Name}' is closed.");
                    lane.Accept(this, passenger);
                    return true;
                case EventType.EndService:
                    return ((Station)e.Node).Finish(this, e.Passenger, e.Time);
                case EventType.Reach:
                    ((Station)e.Node).Reach(this, e.Passenger);
                    return true;
                case EventType.Renege:
                    return ((Station)e.Node).Renege(this, e.Passenger);
                case EventType.Breakdown:
                    var breakdown = (Breakdown)e.Node;
                    Station broken = breakdown.PickLane();
                    if (broken == null) return false;
                    Notify(broken, LaneChange.Broken, byManager: false);
                    broken.Break(this);
                    Schedule(Now + breakdown.Repair.Sample(breakdown.Rng), EventType.Repair, broken, null);
                    return true;
                case EventType.Repair:
                    var repaired = (Station)e.Node;
                    Notify(repaired, LaneChange.Repaired, byManager: false);
                    repaired.Repair(this);
                    return true;
                default:
                    throw new InvalidOperationException($"Unhandled event type {e.Type}.");
            }
        }

        private void ScheduleNextArrival(Source source)
        {
            if (source.Arrivals.TryNext(Now, out double time) && time < source.StopTime)
                Schedule(time, EventType.Arrive, source, null);
        }
    }
}
