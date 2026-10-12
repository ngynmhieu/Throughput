using System;
using System.Collections.Generic;
using Throughput.Core.Engine.Behaviours;
using Throughput.Core.Engine.Network;

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
                    ((Station)e.Node).Finish(this, e.Passenger);
                    return true;
                case EventType.Reach:
                    ((Station)e.Node).Reach(this, e.Passenger);
                    return true;
                case EventType.Renege:
                    return ((Station)e.Node).Renege(this, e.Passenger);
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
