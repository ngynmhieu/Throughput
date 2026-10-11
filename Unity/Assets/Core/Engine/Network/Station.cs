using System;
using System.Collections.Generic;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Randomness;

namespace Throughput.Core.Engine.Network
{
    /// <summary>
    /// A stage with one FIFO line and Staff servers, each serving one passenger at a time.
    /// People may leave the line early (reneging).
    /// </summary>
    public sealed class Station
    {
        private readonly LinkedList<Passenger> _line = new LinkedList<Passenger>();
        private readonly Dictionary<Passenger, LinkedListNode<Passenger>> _inLine = new Dictionary<Passenger, LinkedListNode<Passenger>>();
        private readonly IDistribution _service;
        private readonly Pcg32 _rng;
        private int _busy;

        public Station(string name, int staff, IDistribution service, Pcg32 rng)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A station needs a name.", nameof(name));
            if (staff < 1) throw new ArgumentOutOfRangeException(nameof(staff), staff, "A station needs at least 1 staff.");
            Name = name;
            Staff = staff;
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public string Name { get; }

        public int Staff { get; }

        /// <summary>Staff currently serving someone.</summary>
        public int Busy => _busy;

        /// <summary>People waiting in line (not counting those being served).</summary>
        public int QueueLength => _line.Count;

        internal void Accept(Runner runner, Passenger passenger)
        {
            _inLine[passenger] = _line.AddLast(passenger);
            runner.NotifyJoinQueue(this, passenger);
            TryStart(runner);
        }

        internal void Finish(Runner runner, Passenger passenger)
        {
            _busy--;
            runner.NotifyEndService(this, passenger);
            runner.NotifyExit(passenger);
            TryStart(runner);
        }

        /// <summary>A Renege event fired. Does nothing if the passenger already left the line (service started).</summary>
        internal void Renege(Runner runner, Passenger passenger)
        {
            if (!_inLine.TryGetValue(passenger, out LinkedListNode<Passenger> node)) return;
            _line.Remove(node);
            _inLine.Remove(passenger);
            runner.NotifyRenege(this, passenger);
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
        }
    }
}
