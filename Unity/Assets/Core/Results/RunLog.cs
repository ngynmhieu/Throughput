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
        Renege,
        Blocked,
        Unblocked,
        Exit,
        LaneOpened,
        LaneClosed,
        LaneOpening,
        LaneBroken,
        LaneRepaired,
        LaneReturnedToRule,
    }

    /// <summary>One step of the run, with the engine's own queue length and busy count right after it.</summary>
    public readonly struct LogEntry
    {
        public LogEntry(double time, LogKind kind, int passengerId, string station, int queueLength, int busy, bool byManager = false)
        {
            Time = time;
            Kind = kind;
            PassengerId = passengerId;
            Station = station;
            QueueLength = queueLength;
            Busy = busy;
            ByManager = byManager;
        }
        public double Time { get; }

        public LogKind Kind { get; }

        /// <summary>-1 for lane entries (LaneOpened, LaneClosed, LaneBroken, ...).</summary>
        public int PassengerId { get; }

        /// <summary>Null for Arrive and Exit.</summary>
        public string Station { get; }

        public int QueueLength { get; }

        public int Busy { get; }

        /// <summary>A lane entry caused by the player (K9).</summary>
        public bool ByManager { get; }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "{0:R} {1} p{2} {3} q={4} b={5}{6}",
                Time, Kind, PassengerId, Station ?? "-", QueueLength, Busy, ByManager ? " manager" : "");
    }

    /// <summary>One stop at one station. Times are NaN until that step happens.</summary>
    public sealed class Visit
    {
        internal Visit(string station, double queueJoin)
        {
            Station = station;
            QueueJoin = queueJoin;
        }

        public string Station { get; }

        public double QueueJoin { get; }

        public double ServiceStart { get; internal set; } = double.NaN;

        public double ServiceEnd { get; internal set; } = double.NaN;

        public double Renege { get; internal set; } = double.NaN;

        /// <summary>When a blocked desk finally let this passenger move on (NaN if never blocked).</summary>
        public double Unblocked { get; internal set; } = double.NaN;

        public bool HasReneged => !double.IsNaN(Renege);

        public bool WasBlocked => !double.IsNaN(Unblocked);

        /// <summary>Service is over but the desk is still holding them (the next line is full).</summary>
        public bool IsBlocked { get; internal set; }

        /// <summary>Time spent in this line: until service started, or until they gave up.</summary>
        public double Wait => (HasReneged ? Renege : ServiceStart) - QueueJoin;

        /// <summary>Time held at the desk after service because the next line was full.</summary>
        public double BlockedTime => WasBlocked ? Unblocked - ServiceEnd : 0.0;

        /// <summary>When they left this station (gave up, moved on, or NaN if still here, including still blocked).</summary>
        public double LeftAt => HasReneged ? Renege : IsBlocked ? double.NaN : WasBlocked ? Unblocked : ServiceEnd;
    }

    /// <summary>
    /// One passenger's journey: one Visit per station. The Station/QueueJoin/ServiceStart/ServiceEnd/Wait
    /// shortcuts read the FIRST visit, which is all a single-station run needs.
    /// </summary>
    public sealed class PassengerRecord
    {
        private readonly List<Visit> _visits = new List<Visit>();

        public PassengerRecord(int id, double arrive)
        {
            Id = id;
            Arrive = arrive;
        }

        public int Id { get; }

        public double Arrive { get; }

        public IReadOnlyList<Visit> Visits => _visits;

        public double Exit { get; internal set; } = double.NaN;

        public string Station => First?.Station;

        public double QueueJoin => First?.QueueJoin ?? double.NaN;

        public double ServiceStart => First?.ServiceStart ?? double.NaN;

        public double ServiceEnd => First?.ServiceEnd ?? double.NaN;

        public double Wait => First?.Wait ?? double.NaN;

        /// <summary>When they gave up (only ever at their last visit), or NaN.</summary>
        public double Renege => Current?.Renege ?? double.NaN;

        public bool HasExited => !double.IsNaN(Exit);

        public bool HasReneged => !double.IsNaN(Renege);

        internal Visit Current => _visits.Count == 0 ? null : _visits[_visits.Count - 1];

        private Visit First => _visits.Count == 0 ? null : _visits[0];

        internal void AddVisit(Visit visit) => _visits.Add(visit);
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
            _passengers[passenger.Id].AddVisit(new Visit(station.Name, time));
            Add(time, LogKind.JoinQueue, passenger, station);
        }

        public void OnStartService(double time, Station station, Passenger passenger)
        {
            VisitAt(station, passenger).ServiceStart = time;
            Add(time, LogKind.StartService, passenger, station);
        }

        public void OnEndService(double time, Station station, Passenger passenger)
        {
            VisitAt(station, passenger).ServiceEnd = time;
            Add(time, LogKind.EndService, passenger, station);
        }

        public void OnRenege(double time, Station station, Passenger passenger)
        {
            VisitAt(station, passenger).Renege = time;
            Add(time, LogKind.Renege, passenger, station);
        }

        public void OnBlocked(double time, Station station, Passenger passenger)
        {
            VisitAt(station, passenger).IsBlocked = true; // ServiceEnd already marks the moment
            Add(time, LogKind.Blocked, passenger, station);
        }

        public void OnUnblocked(double time, Station station, Passenger passenger)
        {
            Visit v = VisitAt(station, passenger);
            v.IsBlocked = false;
            v.Unblocked = time;
            Add(time, LogKind.Unblocked, passenger, station);
        }

        /// <summary>The passenger's current visit, which must be at this station.</summary>
        private Visit VisitAt(Station station, Passenger passenger)
        {
            Visit v = _passengers[passenger.Id].Current;
            if (v == null || v.Station != station.Name)
                throw new InvalidOperationException(
                    $"Passenger {passenger.Id} isn't at '{station.Name}' (current: '{v?.Station ?? "none"}').");
            return v;
        }

        public void OnExit(double time, Passenger passenger)
        {
            _passengers[passenger.Id].Exit = time;
            Add(time, LogKind.Exit, passenger, null);
        }

        public void OnLaneChanged(double time, Station lane, LaneChange change, bool byManager)
        {
            if (!_keepEntries) return;
            _entries.Add(new LogEntry(time, KindOf(change), -1, lane.Name, lane.QueueLength, lane.Busy, byManager));
        }

        private static LogKind KindOf(LaneChange change)
        {
            switch (change)
            {
                case LaneChange.Opened: return LogKind.LaneOpened;
                case LaneChange.Closed: return LogKind.LaneClosed;
                case LaneChange.Opening: return LogKind.LaneOpening;
                case LaneChange.Broken: return LogKind.LaneBroken;
                case LaneChange.Repaired: return LogKind.LaneRepaired;
                case LaneChange.ReturnedToRule: return LogKind.LaneReturnedToRule;
                default: throw new ArgumentOutOfRangeException(nameof(change), change, null);
            }
        }

        private void Add(double time, LogKind kind, Passenger passenger, Station station)
        {
            if (!_keepEntries) return;
            _entries.Add(new LogEntry(time, kind, passenger.Id, station?.Name,
                station?.QueueLength ?? 0, station?.Busy ?? 0));
        }
    }
}
