using System;
using System.Collections.Generic;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;

namespace Throughput.Core.Results
{
    /// <summary>What a run did, summed up (S9, G2–G5). Times in the run's unit (minutes in game levels).</summary>
    public sealed class RunSummary
    {
        public RunSummary(int arrived, int exited, int missedFlights, double averageWait, double longestWait,
            double staffMinutes, double end, IReadOnlyDictionary<string, double> busyShare)
        {
            Arrived = arrived;
            Exited = exited;
            MissedFlights = missedFlights;
            AverageWait = averageWait;
            LongestWait = longestWait;
            StaffMinutes = staffMinutes;
            End = end;
            BusyShare = busyShare ?? new Dictionary<string, double>();
        }

        public int Arrived { get; }

        public int Exited { get; }

        /// <summary>People who gave up (G2).</summary>
        public int MissedFlights { get; }

        /// <summary>Time in lines per passenger, over all stages, averaged over everyone who arrived (G4).</summary>
        public double AverageWait { get; }

        /// <summary>The worst passenger's total time in lines (G4).</summary>
        public double LongestWait { get; }

        /// <summary>Desks × minutes staffed: open, in setup, or still serving after closing (G3).</summary>
        public double StaffMinutes { get; }

        public double End { get; }

        /// <summary>People through per minute.</summary>
        public double Throughput => End > 0 ? Exited / End : 0.0;

        /// <summary>Per station: busy minutes (serving or holding a blocked passenger) ÷ staffed minutes (G5).</summary>
        public IReadOnlyDictionary<string, double> BusyShare { get; }
    }

    /// <summary>
    /// Watches a run and measures it (S9). Give it every station of the run before the run starts; it only reads.
    /// Call Summarize with the run's end (Runner.LastActiveTime) when the run is over.
    /// </summary>
    public sealed class RunStats : IRunObserver
    {
        private sealed class Desk
        {
            public Station Station;
            public double Since;
            public bool Staffed;
            public int Busy;
            public double StaffedMinutes;
            public double BusyMinutes;
        }

        private readonly Dictionary<Station, Desk> _desks = new Dictionary<Station, Desk>();
        private readonly List<Desk> _order = new List<Desk>();
        private readonly Dictionary<int, double> _joined = new Dictionary<int, double>();
        private readonly Dictionary<int, double> _waited = new Dictionary<int, double>();
        private int _arrived;
        private int _exited;
        private int _missed;

        public RunStats(IEnumerable<Station> stations)
        {
            if (stations == null) throw new ArgumentNullException(nameof(stations));
            foreach (Station station in stations)
            {
                if (station == null) throw new ArgumentException("Stations can't be null.", nameof(stations));
                if (_desks.ContainsKey(station)) continue;
                var desk = new Desk { Station = station, Staffed = IsStaffed(station), Busy = station.Busy };
                _desks[station] = desk;
                _order.Add(desk);
            }
        }

        public void OnArrive(double time, Passenger passenger) => _arrived++;

        public void OnJoinQueue(double time, Station station, Passenger passenger)
        {
            _joined[passenger.Id] = time;
            Update(time, station);
        }

        public void OnStartService(double time, Station station, Passenger passenger)
        {
            AddWait(passenger, time);
            Update(time, station);
        }

        public void OnEndService(double time, Station station, Passenger passenger) => Update(time, station);

        public void OnBlocked(double time, Station station, Passenger passenger) => Update(time, station);

        public void OnUnblocked(double time, Station station, Passenger passenger) => Update(time, station);

        public void OnRenege(double time, Station station, Passenger passenger)
        {
            AddWait(passenger, time);
            _missed++;
            Update(time, station);
        }

        public void OnLaneChanged(double time, Station lane, LaneChange change, bool byManager) => Update(time, lane);

        public void OnExit(double time, Passenger passenger) => _exited++;

        /// <summary>The totals up to <paramref name="end"/>, which must not be before the last change seen.</summary>
        public RunSummary Summarize(double end)
        {
            var busyShare = new Dictionary<string, double>();
            double staffMinutes = 0.0;
            foreach (Desk d in _order)
            {
                if (!(end >= d.Since))
                    throw new ArgumentOutOfRangeException(nameof(end), end, $"'{d.Station.Name}' changed at {d.Since}, after {end}.");
                double staffed = d.StaffedMinutes + (d.Staffed ? (end - d.Since) * d.Station.Staff : 0.0);
                double busy = d.BusyMinutes + (end - d.Since) * d.Busy;
                staffMinutes += staffed;
                busyShare[d.Station.Name] = staffed > 0 ? busy / staffed : 0.0;
            }

            double total = 0.0, longest = 0.0;
            foreach (double w in _waited.Values)
            {
                total += w;
                if (w > longest) longest = w;
            }
            double average = _arrived > 0 ? total / _arrived : 0.0;
            return new RunSummary(_arrived, _exited, _missed, average, longest, staffMinutes, end, busyShare);
        }

        /// <summary>Someone must be at the desk: it's open, being set up, or still has people after closing (G3).</summary>
        private static bool IsStaffed(Station s) => s.IsOpen || s.IsOpening || s.QueueLength + s.Busy + s.WalkingHere > 0;

        private void AddWait(Passenger passenger, double time)
        {
            _waited.TryGetValue(passenger.Id, out double sum);
            _waited[passenger.Id] = sum + (time - _joined[passenger.Id]);
        }

        /// <summary>Adds the time since the last change at its old state, then reads the new state.</summary>
        private void Update(double time, Station station)
        {
            if (!_desks.TryGetValue(station, out Desk d))
                throw new InvalidOperationException($"RunStats wasn't given station '{station.Name}'.");
            double dt = time - d.Since;
            if (d.Staffed) d.StaffedMinutes += dt * station.Staff;
            d.BusyMinutes += dt * d.Busy;
            d.Since = time;
            d.Staffed = IsStaffed(station);
            d.Busy = station.Busy;
        }
    }
}
