using System;
using System.Collections.Generic;
using Throughput.Core.Sim.Events;
using Throughput.Core.Sim.Randomness;

namespace Throughput.Core.Sim.Network
{
    /// <summary>A stage with one FIFO line and Staff servers, each serving one passenger at a time.</summary>
    public sealed class Station
    {
        private readonly Queue<Passenger> _line = new Queue<Passenger>();
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

        internal void Accept(Simulator sim, Passenger passenger)
        {
            _line.Enqueue(passenger);
            sim.NotifyJoinQueue(this, passenger);
            TryStart(sim);
        }

        internal void Finish(Simulator sim, Passenger passenger)
        {
            _busy--;
            sim.NotifyEndService(this, passenger);
            sim.NotifyExit(passenger);
            TryStart(sim);
        }

        private void TryStart(Simulator sim)
        {
            while (_busy < Staff && _line.Count > 0)
            {
                Passenger next = _line.Dequeue();
                _busy++;
                sim.NotifyStartService(this, next);
                sim.Schedule(sim.Now + _service.Sample(_rng), EventType.EndService, this, next);
            }
        }
    }
}
