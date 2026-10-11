using System;
using System.Collections.Generic;
using System.Globalization;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;

namespace Throughput.Core.Results
{
    public enum LogKind
    {
        Arrive,
        JoinQueue,
        StartService,
        EndService,
        Exit,
    }

    /// <summary>One step of the run, with the engine's own queue length and busy count right after it.</summary>
    public readonly struct LogEntry
    {
        public LogEntry(double time, LogKind kind, int passengerId, string station, int queueLength, int busy)
        {
            Time = time;
            Kind = kind;
            PassengerId = passengerId;
            Station = station;
            QueueLength = queueLength;
            Busy = busy;
        }

        public double Time { get; }

        public LogKind Kind { get; }

        public int PassengerId { get; }

        /// <summary>Null for Arrive and Exit.</summary>
        public string Station { get; }

        public int QueueLength { get; }

        public int Busy { get; }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "{0:R} {1} p{2} {3} q={4} b={5}",
                Time, Kind, PassengerId, Station ?? "-", QueueLength, Busy);
    }

    /// <summary>One passenger's journey. Times are NaN until that step happens.</summary>
    public sealed class PassengerRecord
    {
        public PassengerRecord(int id, double arrive)
        {
            Id = id;
            Arrive = arrive;
        }

        public int Id { get; }

        public double Arrive { get; }

        public string Station { get; internal set; }

        public double QueueJoin { get; internal set; } = double.NaN;

        public double ServiceStart { get; internal set; } = double.NaN;

        public double ServiceEnd { get; internal set; } = double.NaN;

        public double Exit { get; internal set; } = double.NaN;

        /// <summary>Time spent in the line before service started.</summary>
        public double Wait => ServiceStart - QueueJoin;

        public bool HasExited => !double.IsNaN(Exit);
    }

    /// <summary>Records every passenger and (optionally) every step. Big runs pass keepEntries: false to save memory.</summary>
    public sealed class RunLog : IRunObserver
    {
        private readonly List<PassengerRecord> _passengers = new List<PassengerRecord>();
        private readonly List<LogEntry> _entries = new List<LogEntry>();
        private readonly bool _keepEntries;

        public RunLog(bool keepEntries = true)
        {
            _keepEntries = keepEntries;
        }

        public IReadOnlyList<PassengerRecord> Passengers => _passengers;

        public IReadOnlyList<LogEntry> Entries => _entries;

        public void OnArrive(double time, Passenger passenger)
        {
            if (passenger.Id != _passengers.Count)
                throw new InvalidOperationException($"Passenger ids must be 0, 1, 2, ... (got {passenger.Id}, expected {_passengers.Count}).");
            _passengers.Add(new PassengerRecord(passenger.Id, time));
            Add(time, LogKind.Arrive, passenger, null);
        }

        public void OnJoinQueue(double time, Station station, Passenger passenger)
        {
            PassengerRecord r = _passengers[passenger.Id];
            r.Station = station.Name;
            r.QueueJoin = time;
            Add(time, LogKind.JoinQueue, passenger, station);
        }

        public void OnStartService(double time, Station station, Passenger passenger)
        {
            _passengers[passenger.Id].ServiceStart = time;
            Add(time, LogKind.StartService, passenger, station);
        }

        public void OnEndService(double time, Station station, Passenger passenger)
        {
            _passengers[passenger.Id].ServiceEnd = time;
            Add(time, LogKind.EndService, passenger, station);
        }

        public void OnExit(double time, Passenger passenger)
        {
            _passengers[passenger.Id].Exit = time;
            Add(time, LogKind.Exit, passenger, null);
        }

        private void Add(double time, LogKind kind, Passenger passenger, Station station)
        {
            if (!_keepEntries) return;
            _entries.Add(new LogEntry(time, kind, passenger.Id, station?.Name,
                station?.QueueLength ?? 0, station?.Busy ?? 0));
        }
    }
}
