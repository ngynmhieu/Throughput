using System;
using System.Collections.Generic;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Randomness;

namespace Throughput.Core.Engine.Network
{
    /// <summary>
    /// One line and Staff desks, each serving one passenger at a time: a shared line, or one lane of a
    /// stage (N2). People may leave the line early (reneging). After service a passenger walks to a lane
    /// of the Next stage, or exits if there is none. If every open lane there is full, the desk keeps the
    /// passenger and stops (blocking, D8) until a spot frees up.
    /// </summary>
    public sealed class Station
    {
        private readonly LinkedList<Passenger> _line = new LinkedList<Passenger>();
        private readonly Dictionary<Passenger, LinkedListNode<Passenger>> _inLine = new Dictionary<Passenger, LinkedListNode<Passenger>>();
        private readonly IDistribution _service;
        private readonly Pcg32 _rng;
        private int _busy;
        private int _walkingHere;

        public Station(string name, int staff, IDistribution service, Pcg32 rng, int lineCapacity = int.MaxValue)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A station needs a name.", nameof(name));
            if (staff < 1) throw new ArgumentOutOfRangeException(nameof(staff), staff, "A station needs at least 1 staff.");
            if (lineCapacity < 1) throw new ArgumentOutOfRangeException(nameof(lineCapacity), lineCapacity, "A line needs room for at least 1 person.");
            Name = name;
            Staff = staff;
            LineCapacity = lineCapacity;
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public string Name { get; }

        public int Staff { get; }

        /// <summary>
        /// Most people this line holds, counting those already walking here. It only stops stations that
        /// feed this line: arrivals from a Source always join (they spill back into the hall, D8).
        /// </summary>
        public int LineCapacity { get; }

        /// <summary>The stage this station is a lane of (set by the Router); null until something feeds it.</summary>
        public Router Stage { get; internal set; }

        /// <summary>The stage passengers go to after this one; null means they exit.</summary>
        public Router Next { get; private set; }

        /// <summary>A closed lane gets nobody new but still serves its line (N5).</summary>
        public bool IsOpen { get; private set; } = true;

        public double WalkTime { get; private set; }

        /// <summary>Staff serving someone, or holding a finished passenger who can't move on yet (blocked).</summary>
        public int Busy => _busy;

        /// <summary>People waiting in line (not counting those being served or walking here).</summary>
        public int QueueLength => _line.Count;

        /// <summary>People who chose this line and are walking here.</summary>
        public int WalkingHere => _walkingHere;

        internal bool HasRoom => _line.Count + _walkingHere < LineCapacity;

        internal void SetOpen(bool open) => IsOpen = open;

        /// <summary>Feed a single station (a shared line).</summary>
        public void ConnectTo(Station next, double walkTime = 0.0)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            if (next == this) throw new ArgumentException("A station can't feed itself.", nameof(next));
            ConnectTo(Router.For(next), walkTime);
        }

        /// <summary>Feed a stage; each passenger walks to the lane its router picks (N4).</summary>
        public void ConnectTo(Router next, double walkTime = 0.0)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            if (next == Stage) throw new ArgumentException("A station can't feed its own stage.", nameof(next));
            if (!(walkTime >= 0) || double.IsInfinity(walkTime))
                throw new ArgumentOutOfRangeException(nameof(walkTime), walkTime, "Walk time must be 0 or more.");
            if (Next != null) throw new InvalidOperationException($"Station '{Name}' already feeds '{Next.Name}'.");
            Next = next;
            WalkTime = walkTime;
        }

        internal void Accept(Runner runner, Passenger passenger)
        {
            _inLine[passenger] = _line.AddLast(passenger);
            runner.NotifyJoinQueue(this, passenger);
            TryStart(runner);
        }

        /// <summary>A passenger walking from the previous stage got here; their reserved spot becomes a real one.</summary>
        internal void Reach(Runner runner, Passenger passenger)
        {
            _walkingHere--;
            Accept(runner, passenger);
        }

        internal void Finish(Runner runner, Passenger passenger)
        {
            if (Next != null && !Next.HasRoom)
            {
                runner.NotifyEndService(this, passenger);
                Next.Block(this, passenger);
                runner.NotifyBlocked(this, passenger);
                return;
            }
            _busy--;
            runner.NotifyEndService(this, passenger);
            MoveOn(runner, passenger);
            TryStart(runner);
        }

        /// <summary>A Renege event fired. Returns false (and does nothing) if the passenger already left the line.</summary>
        internal bool Renege(Runner runner, Passenger passenger)
        {
            if (!_inLine.TryGetValue(passenger, out LinkedListNode<Passenger> node)) return false;
            _line.Remove(node);
            _inLine.Remove(passenger);
            runner.NotifyRenege(this, passenger);
            Stage?.ReleaseBlocked(runner);
            return true;
        }

        internal void Unblock(Runner runner, Passenger passenger)
        {
            _busy--;
            runner.NotifyUnblocked(this, passenger);
            MoveOn(runner, passenger);
            TryStart(runner);
        }

        private void MoveOn(Runner runner, Passenger passenger)
        {
            if (Next == null)
            {
                runner.NotifyExit(passenger);
                return;
            }
            Station lane = Next.PickWithRoom(); // Finish and ReleaseBlocked only get here when there is room
            lane._walkingHere++;
            runner.Schedule(runner.Now + WalkTime, EventType.Reach, lane, passenger);
        }

        private void TryStart(Runner runner)
        {
            while (_busy < Staff && _line.Count > 0)
            {
                Passenger next = _line.First.Value;
                _line.RemoveFirst();
                _inLine.Remove(next);
                _busy++;
                runner.NotifyStartService(this, next);
                runner.Schedule(runner.Now + _service.Sample(_rng), EventType.EndService, this, next);
            }
            Stage?.ReleaseBlocked(runner);
        }
    }
}
