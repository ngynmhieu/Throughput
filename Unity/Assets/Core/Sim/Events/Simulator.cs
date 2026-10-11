using System;
using System.Collections.Generic;
using Throughput.Core.Sim.Network;

namespace Throughput.Core.Sim.Events
{
    /// <summary>
    /// The event loop: take the earliest event, handle it, repeat. Time only moves by jumping
    /// to the next event; it never depends on frames or real time.
    /// </summary>
    public sealed class Simulator
    {
        private readonly EventList _events = new EventList();
        private readonly List<ISimObserver> _observers = new List<ISimObserver>();
        private readonly Source[] _sources;
        private int _nextPassengerId;
        private bool _started;

        public Simulator(params Source[] sources)
        {
            if (sources == null || sources.Length == 0)
                throw new ArgumentException("A simulation needs at least one source.", nameof(sources));
            foreach (Source s in sources)
                if (s == null) throw new ArgumentException("Sources can't be null.", nameof(sources));
            _sources = (Source[])sources.Clone();
        }

        public double Now { get; private set; }

        public void AddObserver(ISimObserver observer)
        {
            _observers.Add(observer ?? throw new ArgumentNullException(nameof(observer)));
        }

        /// <summary>Handles every event up to and including time <paramref name="until"/>. Can be called again to continue.</summary>
        public void Run(double until = double.PositiveInfinity)
        {
            if (!_started)
            {
                _started = true;
                foreach (Source source in _sources)
                    ScheduleNextArrival(source);
            }

            while (_events.Count > 0 && _events.Peek().Time <= until)
            {
                SimEvent e = _events.Pop();
                Now = e.Time;
                Handle(e);
            }
        }

        internal void Schedule(double time, EventType type, object node, Passenger passenger)
        {
            if (!(time >= Now))
                throw new InvalidOperationException($"Can't schedule an event at {time}: the clock is already at {Now}.");
            _events.Push(new SimEvent(time, type, node, passenger));
        }

        internal void NotifyJoinQueue(Station station, Passenger p)
        {
            foreach (ISimObserver o in _observers) o.OnJoinQueue(Now, station, p);
        }

        internal void NotifyStartService(Station station, Passenger p)
        {
            foreach (ISimObserver o in _observers) o.OnStartService(Now, station, p);
        }

        internal void NotifyEndService(Station station, Passenger p)
        {
            foreach (ISimObserver o in _observers) o.OnEndService(Now, station, p);
        }

        internal void NotifyExit(Passenger p)
        {
            foreach (ISimObserver o in _observers) o.OnExit(Now, p);
        }

        private void Handle(SimEvent e)
        {
            switch (e.Type)
            {
                case EventType.Arrive:
                    var source = (Source)e.Node;
                    var passenger = new Passenger(_nextPassengerId++, Now);
                    foreach (ISimObserver o in _observers) o.OnArrive(Now, passenger);
                    ScheduleNextArrival(source);
                    source.Target.Accept(this, passenger);
                    break;
                case EventType.EndService:
                    ((Station)e.Node).Finish(this, e.Passenger);
                    break;
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
