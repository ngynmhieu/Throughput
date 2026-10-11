using System;
using System.Collections.Generic;
using NUnit.Framework;
using Throughput.Core.Results;

namespace Throughput.Core.Tests.Support
{
    /// <summary>
    /// Rules that must hold after every run (spec 5.2). Needs a RunLog with entries kept.
    /// </summary>
    public static class InvariantChecker
    {
        public static void AssertAll(RunLog log, IReadOnlyDictionary<string, int> staffByStation, bool expectDrained = true,
            IReadOnlyDictionary<string, int> lineCapacity = null)
        {
            AssertTimeNeverGoesBackward(log);
            AssertJourneysInOrder(log);
            AssertConservation(log, expectDrained);
            AssertStaffLimit(log, staffByStation);
            if (lineCapacity != null)
                AssertLineCapacity(log, lineCapacity);
            AssertFirstComeFirstServed(log);
            if (expectDrained)
                AssertLittlesLaw(log);
        }

        private static void AssertTimeNeverGoesBackward(RunLog log)
        {
            for (int i = 1; i < log.Entries.Count; i++)
                Assert.That(log.Entries[i].Time, Is.GreaterThanOrEqualTo(log.Entries[i - 1].Time),
                    $"Time went backward at entry {i}: {log.Entries[i - 1]} → {log.Entries[i]}");
        }

        private static void AssertJourneysInOrder(RunLog log)
        {
            foreach (PassengerRecord p in log.Passengers)
            {
                double ready = p.Arrive;
                for (int i = 0; i < p.Visits.Count; i++)
                {
                    Visit v = p.Visits[i];
                    string at = $"at '{v.Station}'";
                    AssertNotBefore(p, v.QueueJoin, ready, $"joined the line {at} before getting there");
                    AssertNotBefore(p, v.ServiceStart, v.QueueJoin, $"started service {at} before joining the line");
                    AssertNotBefore(p, v.ServiceEnd, v.ServiceStart, $"finished {at} before starting");
                    AssertNotBefore(p, v.Unblocked, v.ServiceEnd, $"was released {at} before finishing");
                    AssertNotBefore(p, v.Renege, v.QueueJoin, $"gave up {at} before joining the line");
                    if (v.HasReneged)
                    {
                        Assert.That(double.IsNaN(v.ServiceStart), Is.True, $"Passenger {p.Id} was served {at} and also gave up.");
                        Assert.That(i, Is.EqualTo(p.Visits.Count - 1), $"Passenger {p.Id} gave up {at} but kept going.");
                    }
                    ready = v.LeftAt;
                }
                AssertNotBefore(p, p.Exit, ready, "left before finishing");
            }
        }

        private static void AssertNotBefore(PassengerRecord p, double later, double earlier, string what)
        {
            if (double.IsNaN(later)) return;
            Assert.That(later, Is.GreaterThanOrEqualTo(earlier), $"Passenger {p.Id} {what}.");
        }

        private static void AssertConservation(RunLog log, bool expectDrained)
        {
            int arrived = log.Passengers.Count;
            int exited = 0;
            int reneged = 0;
            foreach (PassengerRecord p in log.Passengers)
            {
                if (p.HasExited) exited++;
                if (p.HasReneged) reneged++;
            }
            int inside = arrived - exited - reneged;

            Assert.That(inside, Is.GreaterThanOrEqualTo(0), "More passengers left than arrived.");
            if (expectDrained)
                Assert.That(inside, Is.EqualTo(0), $"{inside} passengers never left (arrived {arrived}, exited {exited}, gave up {reneged}).");
        }

        private static void AssertStaffLimit(RunLog log, IReadOnlyDictionary<string, int> staffByStation)
        {
            foreach (LogEntry e in log.Entries)
            {
                if (e.Station == null) continue;
                Assert.That(staffByStation.ContainsKey(e.Station), Is.True, $"Unknown station '{e.Station}'.");
                Assert.That(e.Busy, Is.InRange(0, staffByStation[e.Station]),
                    $"Station '{e.Station}' had {e.Busy} busy staff (limit {staffByStation[e.Station]}) at {e.Time}.");
                Assert.That(e.QueueLength, Is.GreaterThanOrEqualTo(0), $"Negative queue at '{e.Station}'.");
            }
        }

        /// <summary>
        /// D8: a line fed by stations never holds more than its capacity. Only list lines fed by stations:
        /// a line fed straight from a Source takes everyone (they spill back into the hall, decision B4).
        /// </summary>
        private static void AssertLineCapacity(RunLog log, IReadOnlyDictionary<string, int> lineCapacity)
        {
            foreach (LogEntry e in log.Entries)
            {
                if (e.Station == null || !lineCapacity.TryGetValue(e.Station, out int cap)) continue;
                Assert.That(e.QueueLength, Is.LessThanOrEqualTo(cap),
                    $"Line at '{e.Station}' held {e.QueueLength} people (capacity {cap}) at {e.Time}.");
            }
        }

        /// <summary>
        /// FIFO: at each station, people start service in the order they joined the line.
        /// Average-wait tests can't see this (any work-conserving order gives the same M/M/1 mean).
        /// People who give up leave from anywhere in the line; everyone behind them keeps their order.
        /// </summary>
        private static void AssertFirstComeFirstServed(RunLog log)
        {
            var joined = new Dictionary<string, List<int>>();
            foreach (LogEntry e in log.Entries)
            {
                if (e.Station == null) continue;
                if (!joined.TryGetValue(e.Station, out List<int> line))
                {
                    line = new List<int>();
                    joined[e.Station] = line;
                }

                if (e.Kind == LogKind.JoinQueue)
                {
                    line.Add(e.PassengerId);
                }
                else if (e.Kind == LogKind.StartService)
                {
                    Assert.That(line.Count, Is.GreaterThan(0), $"Passenger {e.PassengerId} started at '{e.Station}' without joining its line.");
                    int expected = line[0];
                    line.RemoveAt(0);
                    Assert.That(e.PassengerId, Is.EqualTo(expected),
                        $"Not first come, first served at '{e.Station}' at {e.Time}: passenger {e.PassengerId} started before {expected}.");
                }
                else if (e.Kind == LogKind.Renege)
                {
                    Assert.That(line.Remove(e.PassengerId), Is.True,
                        $"Passenger {e.PassengerId} gave up at '{e.Station}' at {e.Time} but wasn't in its line.");
                }
            }
        }

        /// <summary>
        /// Little's Law on a run that starts and ends empty: the area under the engine's queue-length
        /// graph must equal the sum of everyone's wait (Lq × T = λ × Wq × T). The queue length comes from
        /// the Station itself; the waits from passenger timestamps, so this checks the two agree.
        /// </summary>
        private static void AssertLittlesLaw(RunLog log)
        {
            var area = new Dictionary<string, double>();
            var lastTime = new Dictionary<string, double>();
            var lastQueue = new Dictionary<string, int>();

            foreach (LogEntry e in log.Entries)
            {
                if (e.Station == null) continue;
                if (!area.ContainsKey(e.Station))
                {
                    area[e.Station] = 0.0;
                    lastTime[e.Station] = 0.0;
                    lastQueue[e.Station] = 0;
                }
                area[e.Station] += lastQueue[e.Station] * (e.Time - lastTime[e.Station]);
                lastTime[e.Station] = e.Time;
                lastQueue[e.Station] = e.QueueLength;
            }

            var waits = new Dictionary<string, double>();
            foreach (PassengerRecord p in log.Passengers)
                foreach (Visit v in p.Visits)
                {
                    waits.TryGetValue(v.Station, out double sum);
                    waits[v.Station] = sum + v.Wait;
                }

            foreach (KeyValuePair<string, double> kv in area)
            {
                Assert.That(lastQueue[kv.Key], Is.EqualTo(0), $"Line at '{kv.Key}' not empty at the end.");
                waits.TryGetValue(kv.Key, out double totalWait);
                double tolerance = 1e-9 * Math.Max(1.0, totalWait);
                Assert.That(kv.Value, Is.EqualTo(totalWait).Within(tolerance),
                    $"Little's Law broken at '{kv.Key}': queue area {kv.Value} vs total wait {totalWait}.");
            }
        }
    }
}
