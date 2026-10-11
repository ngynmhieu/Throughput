# Phase 1 Part 4: Chain of Stages and Blocking (S5) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Passengers move through a chain of stations (ID check → scanner → exit) with a walk time between them. When the next line is full, the desk that just finished holds its passenger and stops (blocking, D8), and the chain restarts in the right order when space frees.

**Architecture:** A `Station` can be connected to a next station with a walk time. After service, the passenger reserves a spot in the next line and a new `Reach` event delivers them after the walk. If the next line is full (line + people walking there ≥ capacity), the desk stays occupied and joins the next station's "blocked upstream" list. When that line shrinks (someone starts service or gives up), it releases the first blocked desk, which moves its passenger on and starts its own next person, and the release can cascade upstream. Run records become **per visit**, so one passenger has one record per station.

**Tech Stack:** Unity 6000.3.26f1, C#, NUnit (EditMode), Unity CLI via `scripts/run-tests.sh`.

**Spec:** [docs/superpowers/specs/2026-10-11-phase-1-core-design.md](../specs/2026-10-11-phase-1-core-design.md) (§5.2 invariants incl. "no queue exceeds its capacity", §6 S5, the S5 review notes). Decisions: [D8](../../game-mechanism/decisions.md). Architecture: [simulation-core.md](../../architecture/simulation-core.md) (network nodes, blocking).

## Global Constraints

- `Throughput.Core` never references Unity. Tests in `Throughput.Core.Tests`.
- Determinism: separate named streams per source of randomness; never real time.
- Tie order (S-D4): lower `EventType` value first, then `Seq`.
- Fast tier ±10%, 1 seed. Strict tier `[Category("Strict")]`: 20 replications, 99% CI contains theory and error ≤ 3%.
- Validate against formulas "using areas large enough never to fill" (D8).
- Private fields `_camelCase`.
- The Unity editor must be closed to run `scripts/run-tests.sh`.
- Git: commit locally after each task; push only when the user asks. Messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Design decisions (the user reviews these)

| # | Decision | Why |
|---|---|---|
| B1 | **The "buffer" is the next station's line, with a capacity.** No separate `Buffer` node. | In the game a waiting area *is* the line in front of a desk; one capacity number on the station says it all. Fewer parts. |
| B2 | **`Router` moves to S8**, where routing policies (shortest line…) arrive. S5 is a straight chain. | A chain has no choice to make; a router with nothing to decide is untested code. |
| B3 | **Walkers reserve a spot.** When a passenger leaves a desk, the spot in the next line is theirs while they walk. "Full" = in line + walking there. | Otherwise two desks could both send someone "into the last spot" and the line would overflow on arrival. |
| B4 | **Arrivals from outside never block.** A line fed by a `Source` takes everyone; the extra people spill back into the terminal hall (D8: "arrivals spill back"). Capacity only stops **stations** that feed the line. | The entrance can't refuse to let passengers exist. The capacity check in the invariants applies only to lines fed by stations. |
| B5 | **A blocked desk counts as busy** and can't start the next person. Release order is **first blocked, first released**. Release happens whenever the line shrinks: service starts, or someone gives up. | Matches the blocking-after-service rule in the architecture; fair and deterministic. |
| B6 | **Tie order: `EndService` (0) → `Renege` (1) → `Reach` (2) → `Arrive` (3).** | Desks free first; a walker reaching a line at time t is handled before a brand-new arrival at t. |
| B7 | **Per-visit records.** `PassengerRecord.Visits` holds one `Visit` per station. The old fields (`Station`, `QueueJoin`, `ServiceStart`, `ServiceEnd`, `Wait`) stay as **first-visit shortcuts**, so single-station code and tests don't change. | The S4 review note. Shortcuts keep 80+ existing tests untouched. |

## Review Focus

1. **Release cascades through three stages.** A frees space → B unblocks and starts its next person → B's line shrinks → A unblocks. Pinned by `FullLine_BlocksUpstream_AndRestartsInOrder` (two-stage cascade) and the random 3-stage invariants test (Task 3).
2. **Someone giving up in a full line frees a spot.** The blocked desk upstream must be released at that moment. Pinned by `GiveUpInFullLine_ReleasesBlockedDesk` (Task 3).
3. **Walk time with a free desk behind.** The first desk is free the moment its passenger starts walking. Pinned by `WalkTime_DelaysArrival_DeskFreedAtOnce` (Task 2).
4. **A passenger who arrives at a station while a blocked desk is waiting for that same line** (B4: arrivals from a Source can overflow; walkers can't). Pinned by the capacity invariant in the random 3-stage test (Task 3).
5. **Little's Law per station with per-visit waits.** Pinned by every `CheckInvariants` call on chain runs.

---

## File Structure

| File | Change | Responsibility |
|---|---|---|
| `Unity/Assets/Core/Results/RunLog.cs` | Modify | `Visit` class, per-visit `PassengerRecord`, `Blocked`/`Unblocked` log kinds |
| `Unity/Assets/Core/Tests/Results/RunLogTests.cs` | Create | Per-visit recording tests |
| `Unity/Assets/Core/Tests/Support/InvariantChecker.cs` | Modify | Per-visit journeys and Little's Law; line capacity check |
| `Unity/Assets/Core/Tests/Invariants/InvariantCheckerTests.cs` | Modify | Move one test to `RunLogTests` |
| `Unity/Assets/Core/Engine/Events/EventType.cs` | Modify | `Reach = 2`, `Arrive = 3` |
| `Unity/Assets/Core/Engine/Events/IRunObserver.cs` | Modify | `OnBlocked`, `OnUnblocked` |
| `Unity/Assets/Core/Engine/Events/Runner.cs` | Modify | Handle `Reach`; `NotifyBlocked`, `NotifyUnblocked` |
| `Unity/Assets/Core/Engine/Network/Station.cs` | Modify | `ConnectTo`, line capacity, walking, blocking |
| `Unity/Assets/Core/Tests/Engine/ChainTests.cs` | Create | Hand tests |
| `Unity/Assets/Core/Tests/Validation/TandemValidationTests.cs` | Create | Two M/M/1 stages vs Burke's theorem + random chain invariants |
| `docs/game-mechanism/knowledge/formulas/tandem-burke.md` | Create | Formula page (the user reads it) |
| Docs | Modify | Task 4 |

Unity creates the `.meta` files (including for the new `Tests/Results` folder) during the test run; commit them alongside.

---

### Task 1: Per-visit records

**Files:**
- Modify: `Unity/Assets/Core/Results/RunLog.cs`
- Create: `Unity/Assets/Core/Tests/Results/RunLogTests.cs`
- Modify: `Unity/Assets/Core/Tests/Support/InvariantChecker.cs`
- Modify: `Unity/Assets/Core/Tests/Invariants/InvariantCheckerTests.cs`

**Interfaces:**
- Produces: `Visit` (public sealed, in `Throughput.Core.Results`) with `Station` (string), `QueueJoin`, `ServiceStart`, `ServiceEnd`, `Renege`, `Unblocked` (double, NaN until set), `HasReneged`, `WasBlocked` (bool), `Wait`, `BlockedTime`, `LeftAt` (double). `PassengerRecord.Visits` (`IReadOnlyList<Visit>`); first-visit shortcuts `Station`, `QueueJoin`, `ServiceStart`, `ServiceEnd`, `Wait`; `Renege`/`HasReneged` refer to the visit where they gave up (always the last). `LogKind.Blocked`, `LogKind.Unblocked`. RunLog throws `InvalidOperationException` when a step names a station the passenger isn't at. `InvariantChecker.AssertAll(RunLog log, IReadOnlyDictionary<string,int> staffByStation, bool expectDrained = true, IReadOnlyDictionary<string,int> lineCapacity = null)`.

- [ ] **Step 1: Write the failing tests**

Create `Unity/Assets/Core/Tests/Results/RunLogTests.cs`:

```csharp
using System;
using NUnit.Framework;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Results;

namespace Throughput.Core.Tests.Results
{
    public class RunLogTests
    {
        private static Station Desk(string name) => new Station(name, 1, new Deterministic(1.0), new Pcg32(1UL, 1UL));

        [Test]
        public void RecordsOneVisitPerStation()
        {
            Station id = Desk("id"), scan = Desk("scan");
            var p = new Passenger(0, 0.0);
            var log = new RunLog();
            log.OnArrive(0.0, p);
            log.OnJoinQueue(0.0, id, p);
            log.OnStartService(2.0, id, p);
            log.OnEndService(5.0, id, p);
            log.OnJoinQueue(7.0, scan, p);
            log.OnStartService(10.0, scan, p);
            log.OnEndService(11.0, scan, p);
            log.OnExit(11.0, p);

            PassengerRecord r = log.Passengers[0];
            Assert.That(r.Visits.Count, Is.EqualTo(2));
            Assert.That(r.Visits[1].Station, Is.EqualTo("scan"));
            Assert.That(r.Visits[1].Wait, Is.EqualTo(3.0));
            Assert.That(r.Visits[0].LeftAt, Is.EqualTo(5.0));
            // First-visit shortcuts
            Assert.That(r.Station, Is.EqualTo("id"));
            Assert.That(r.Wait, Is.EqualTo(2.0));
            Assert.That(r.ServiceEnd, Is.EqualTo(5.0));
        }

        [Test]
        public void Rejects_StepAtStationThePassengerIsNotAt()
        {
            Station id = Desk("id"), scan = Desk("scan");
            var p = new Passenger(0, 0.0);
            var log = new RunLog();
            log.OnArrive(0.0, p);
            log.OnJoinQueue(0.0, id, p);

            Assert.Throws<InvalidOperationException>(() => log.OnStartService(1.0, scan, p));
        }

        [Test]
        public void Rejects_GivingUpWithoutBeingInLine()
        {
            var p = new Passenger(0, 0.0);
            var log = new RunLog();
            log.OnArrive(0.0, p);

            Assert.Throws<InvalidOperationException>(() => log.OnRenege(5.0, Desk("desk"), p));
        }
    }
}
```

In `InvariantCheckerTests.cs`, **delete** the test `Fails_WhenSomeoneGivesUpWithoutBeingInLine` (it moves to `RunLogTests.Rejects_GivingUpWithoutBeingInLine`: the log itself now refuses that step).

- [ ] **Step 2: Run to see it fail**

Run: `bash scripts/run-tests.sh Throughput.Core.Tests.Results`
Expected: compile error, `'PassengerRecord' does not contain a definition for 'Visits'`.

- [ ] **Step 3: Per-visit `PassengerRecord` in `RunLog.cs`**

Add `Blocked,` and `Unblocked,` to `LogKind` after `Renege,`.

Replace the whole `PassengerRecord` class with these two classes:

```csharp
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

        /// <summary>Time spent in this line: until service started, or until they gave up.</summary>
        public double Wait => (HasReneged ? Renege : ServiceStart) - QueueJoin;

        /// <summary>Time held at the desk after service because the next line was full.</summary>
        public double BlockedTime => WasBlocked ? Unblocked - ServiceEnd : 0.0;

        /// <summary>When they left this station (gave up, moved on, or NaN if still here).</summary>
        public double LeftAt => HasReneged ? Renege : WasBlocked ? Unblocked : ServiceEnd;
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
```

In `RunLog`, replace the bodies of `OnJoinQueue`, `OnStartService`, `OnEndService`, `OnRenege` and add a helper and the two new handlers:

```csharp
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
            VisitAt(station, passenger); // only checks they're there; ServiceEnd already marks the moment
            Add(time, LogKind.Blocked, passenger, station);
        }

        public void OnUnblocked(double time, Station station, Passenger passenger)
        {
            VisitAt(station, passenger).Unblocked = time;
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
```

(`OnBlocked`/`OnUnblocked` are not on `IRunObserver` yet; Task 3 adds them. Being public methods on `RunLog` is fine meanwhile.)

- [ ] **Step 4: Per-visit checker**

In `InvariantChecker.cs`:

(a) Change the `AssertAll` signature and add the capacity check call:

```csharp
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
```

(b) Replace `AssertJourneysInOrder` with:

```csharp
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
```

(c) Add after `AssertStaffLimit`:

```csharp
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
```

(d) In `AssertLittlesLaw`, replace the waits loop with:

```csharp
            var waits = new Dictionary<string, double>();
            foreach (PassengerRecord p in log.Passengers)
                foreach (Visit v in p.Visits)
                {
                    waits.TryGetValue(v.Station, out double sum);
                    waits[v.Station] = sum + v.Wait;
                }
```

- [ ] **Step 5: Run the tests**

Run: `bash scripts/run-tests.sh`
Expected: **88** pass (86 − 1 moved + 3 new). The M/M/1 fast lines print the same numbers as before (`measured 1.013`, `3.978`).

- [ ] **Step 6: Commit**

```bash
git add Unity/Assets/Core
git commit -m "Record one visit per station; per-visit invariants and line capacity check"
```

---

### Task 2: Chain of stations with walk time

**Files:**
- Modify: `Unity/Assets/Core/Engine/Events/EventType.cs`
- Modify: `Unity/Assets/Core/Engine/Events/Runner.cs`
- Modify: `Unity/Assets/Core/Engine/Network/Station.cs`
- Create: `Unity/Assets/Core/Tests/Engine/ChainTests.cs`
- Create: `docs/game-mechanism/knowledge/formulas/tandem-burke.md`
- Create: `Unity/Assets/Core/Tests/Validation/TandemValidationTests.cs`

**Interfaces:**
- Consumes: Task 1's `Visit`, `PassengerRecord.Visits`, `InvariantChecker.AssertAll(..., lineCapacity)`.
- Produces: `EventType.Reach`; `Station.ConnectTo(Station next, double walkTime = 0.0)`, `Station.Next`, `Station.WalkTime`; internal `Station.Reach(Runner, Passenger)`.

This task builds the chain **without** capacity limits (no blocking yet), so it can be validated against Burke's theorem first.

- [ ] **Step 1: Write the hand tests**

Create `Unity/Assets/Core/Tests/Engine/ChainTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Results;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Engine
{
    /// <summary>Two stations in a row (ID check → scanner → exit). Fixed times, worked out by hand.</summary>
    public class ChainTests
    {
        private static Station Desk(string name, double service, int lineCapacity = int.MaxValue) =>
            new Station(name, 1, new Deterministic(service), new Pcg32(1UL, 1UL), lineCapacity);

        private static (Runner runner, RunLog log) Run(Station first, params double[] arrivals)
        {
            var runner = new Runner(new Source("entry", new FixedArrivals(arrivals), first));
            var log = new RunLog();
            runner.AddObserver(log);
            runner.Run();
            return (runner, log);
        }

        private static readonly Dictionary<string, int> OneEach = new Dictionary<string, int> { { "id", 1 }, { "scan", 1 } };

        [Test]
        public void TwoStages_HandTimeline()
        {
            // ID 60 s, scanner 100 s, no walk. A at 0, B at 10.
            // A: ID 0–60, scanner 60–160, exits 160.
            // B: ID 60–120 (wait 50), scanner busy until 160 → 160–260 (wait 40), exits 260.
            Station id = Desk("id", 60.0), scan = Desk("scan", 100.0);
            id.ConnectTo(scan);
            var (runner, log) = Run(id, 0.0, 10.0);

            var p = log.Passengers;
            Assert.That(p.Select(r => r.Visits.Count), Is.EqualTo(new[] { 2, 2 }));
            Assert.That(p.Select(r => r.Visits[1].ServiceStart), Is.EqualTo(new[] { 60.0, 160.0 }));
            Assert.That(p.Select(r => r.Visits[0].Wait), Is.EqualTo(new[] { 0.0, 50.0 }));
            Assert.That(p.Select(r => r.Visits[1].Wait), Is.EqualTo(new[] { 0.0, 40.0 }));
            Assert.That(p.Select(r => r.Exit), Is.EqualTo(new[] { 160.0, 260.0 }));
            InvariantChecker.AssertAll(log, OneEach);
        }

        [Test]
        public void WalkTime_DelaysArrival_DeskFreedAtOnce()
        {
            // ID 60 s, walk 30 s, scanner 100 s. A at 0, B at 60.
            // A: ID 0–60, walks, reaches the scanner at 90, scanner 90–190.
            // The ID desk is free while A walks: B starts at 60 with no wait, ID 60–120, reaches at 150,
            // scanner busy until 190 → 190–290.
            Station id = Desk("id", 60.0), scan = Desk("scan", 100.0);
            id.ConnectTo(scan, walkTime: 30.0);
            var (runner, log) = Run(id, 0.0, 60.0);

            var p = log.Passengers;
            Assert.That(p.Select(r => r.Visits[1].QueueJoin), Is.EqualTo(new[] { 90.0, 150.0 }));
            Assert.That(p[1].Visits[0].Wait, Is.EqualTo(0.0));
            Assert.That(p.Select(r => r.Visits[1].ServiceStart), Is.EqualTo(new[] { 90.0, 190.0 }));
            Assert.That(p.Select(r => r.Exit), Is.EqualTo(new[] { 190.0, 290.0 }));
            InvariantChecker.AssertAll(log, OneEach);
        }

        [Test]
        public void ConnectTo_RejectsBadLinks()
        {
            Station id = Desk("id", 1.0), scan = Desk("scan", 1.0);
            Assert.Throws<System.ArgumentException>(() => id.ConnectTo(id));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => id.ConnectTo(scan, walkTime: -1.0));
            id.ConnectTo(scan);
            Assert.Throws<System.InvalidOperationException>(() => id.ConnectTo(scan));
        }
    }
}
```

- [ ] **Step 2: Run to see it fail**

Run: `bash scripts/run-tests.sh Throughput.Core.Tests.Engine`
Expected: compile error, `'Station' does not contain a definition for 'ConnectTo'` (and the 5-argument constructor).

- [ ] **Step 3: New event type**

In `EventType.cs`, replace the enum body and update the summary's last sentence:

```csharp
    /// <summary>
    /// The kinds of scheduled event. The number is the tie-break priority when two events share
    /// a time: lower goes first (spec S-D4). EndService first, so a desk that frees up at time t
    /// can serve someone whose patience ends at t, who walks in at t, or who arrives at t.
    /// </summary>
    public enum EventType
    {
        EndService = 0,
        Renege = 1,
        Reach = 2,
        Arrive = 3,
    }
```

- [ ] **Step 4: Replace `Station.cs`**

Replace the whole file with (this already includes the capacity and blocking parts that Task 3 tests; with the default capacity they never trigger):

```csharp
using System;
using System.Collections.Generic;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Randomness;

namespace Throughput.Core.Engine.Network
{
    /// <summary>
    /// A stage with one FIFO line and Staff servers, each serving one passenger at a time.
    /// People may leave the line early (reneging). After service a passenger walks to Next, or exits
    /// if there is none. If Next's line is full, the desk keeps the passenger and stops (blocking, D8)
    /// until a spot frees up.
    /// </summary>
    public sealed class Station
    {
        private readonly LinkedList<Passenger> _line = new LinkedList<Passenger>();
        private readonly Dictionary<Passenger, LinkedListNode<Passenger>> _inLine = new Dictionary<Passenger, LinkedListNode<Passenger>>();
        private readonly LinkedList<(Station From, Passenger Passenger)> _blockedUpstream = new LinkedList<(Station, Passenger)>();
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

        /// <summary>The station passengers go to after this one; null means they exit.</summary>
        public Station Next { get; private set; }

        public double WalkTime { get; private set; }

        /// <summary>Staff serving someone, or holding a finished passenger who can't move on yet (blocked).</summary>
        public int Busy => _busy;

        /// <summary>People waiting in line (not counting those being served or walking here).</summary>
        public int QueueLength => _line.Count;

        private bool HasRoom => _line.Count + _walkingHere < LineCapacity;

        public void ConnectTo(Station next, double walkTime = 0.0)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            if (next == this) throw new ArgumentException("A station can't feed itself.", nameof(next));
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
                Next._blockedUpstream.AddLast((this, passenger));
                runner.NotifyBlocked(this, passenger);
                return;
            }
            _busy--;
            runner.NotifyEndService(this, passenger);
            MoveOn(runner, passenger);
            TryStart(runner);
        }

        /// <summary>A Renege event fired. Does nothing if the passenger already left the line (service started).</summary>
        internal void Renege(Runner runner, Passenger passenger)
        {
            if (!_inLine.TryGetValue(passenger, out LinkedListNode<Passenger> node)) return;
            _line.Remove(node);
            _inLine.Remove(passenger);
            runner.NotifyRenege(this, passenger);
            ReleaseBlocked(runner);
        }

        private void Unblock(Runner runner, Passenger passenger)
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
            Next._walkingHere++;
            runner.Schedule(runner.Now + WalkTime, EventType.Reach, Next, passenger);
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
            ReleaseBlocked(runner);
        }

        /// <summary>Space opened in this line: blocked desks upstream move their passengers on, first blocked first.</summary>
        private void ReleaseBlocked(Runner runner)
        {
            while (_blockedUpstream.Count > 0 && HasRoom)
            {
                (Station from, Passenger passenger) = _blockedUpstream.First.Value;
                _blockedUpstream.RemoveFirst();
                from.Unblock(runner, passenger);
            }
        }
    }
}
```

- [ ] **Step 5: `Runner` and observer**

In `IRunObserver.cs`, after `OnEndService`, add:

```csharp
        /// <summary>Service ended but the next line is full: the desk holds the passenger and stops (D8).</summary>
        void OnBlocked(double time, Station station, Passenger passenger);

        /// <summary>Space opened downstream: the blocked desk lets the passenger move on.</summary>
        void OnUnblocked(double time, Station station, Passenger passenger);
```

(`RunLog` already has both methods from Task 1.)

In `Runner.cs`, after `NotifyEndService` add:

```csharp
        internal void NotifyBlocked(Station station, Passenger p)
        {
            foreach (IRunObserver o in _observers) o.OnBlocked(Now, station, p);
        }

        internal void NotifyUnblocked(Station station, Passenger p)
        {
            foreach (IRunObserver o in _observers) o.OnUnblocked(Now, station, p);
        }
```

and in `Handle`, before `case EventType.Renege:`:

```csharp
                case EventType.Reach:
                    ((Station)e.Node).Reach(this, e.Passenger);
                    break;
```

- [ ] **Step 6: Run the hand tests**

Run: `bash scripts/run-tests.sh`
Expected: **91** pass (88 + 3).

- [ ] **Step 7: Write the formula page**

Create `docs/game-mechanism/knowledge/formulas/tandem-burke.md`:

````markdown
# Burke's Theorem: Desks in a Row

**One line:** when random arrivals pass through an M/M/c desk, the people **leaving** it are also perfectly random (Poisson) at the same rate, so every desk in a chain behaves like its own simple M/M/1 or M/M/c.

## The question it answers

"ID check, then scanner. Does the scanner see a messy, bunched-up stream from the ID desk? How long is the wait at each?"

## The idea

You might expect the ID desk to "shape" the stream (people come out at the ID desk's pace). Burke's theorem says: if arrivals are Poisson and service is exponential, the output is **again Poisson with rate λ**. So:

$$
W_{q,\,\text{stage }k} = \text{the M/M/1 (or Erlang C) wait of stage } k \text{ on its own, with the same } \lambda
$$

$$
W_{q,\,\text{total}} = \sum_k W_{q,\,\text{stage }k}
$$

## Worked example

```
λ = 0.8 per min, ID desk μ = 1, scanner μ = 1 (1 min each, exponential)

ID:      Wq = 0.8 / (1 − 0.8) = 4 min
Scanner: Wq = 4 min (it sees Poisson arrivals at 0.8 too)
Total time in lines: 8 min
```

## What it assumes (when it's wrong)

- Poisson arrivals and **exponential** service at every stage. With regular service (CV < 1) the output is smoother, and the next stage waits **less** than its formula says.
- **Unlimited** room between stages. With a small area, blocking appears (D8) and nothing simple is exact any more.
- People don't leave between stages (no reneging).

## In Throughput

- It's how we check the engine moves people **between** stations correctly: walk-free chain, big areas, two independent stages.
- The game will break it on purpose: limited areas (blocking), regular machines and waves. Then only the simulation knows the answer.

## How we test it

- Setup: Poisson λ = 0.8 → ID (1 staff, exponential, mean 1) → scanner (same) → exit, unlimited lines.
- Measure the mean wait at **each** stage; both should equal 4.
- **Fast tier:** 1 seed, ±10%. **Strict tier:** 20 seeds, 99% CI contains 4 and error ≤ 3%, for each stage.
- Slice: **S5**.

## Questions and answers

*(Add the user's questions and the answers here.)*

## Status

- [ ] Read
- [ ] Questions done
- [x] Implemented (validation test)
- [ ] Strict test passes
````

In `formulas/README.md`, add a row after row 6 (adjust the table's columns to match):

```markdown
| 7 | Burke (tandem) | [tandem-burke.md](tandem-burke.md) | Moving between desks in a row | S5 | [ ] | [ ] | [x] | [ ] |
```

- [ ] **Step 8: Write the tandem validation**

Create `Unity/Assets/Core/Tests/Validation/TandemValidationTests.cs`:

```csharp
using NUnit.Framework;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Results;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Validation
{
    /// <summary>
    /// Two M/M/1 stages in a row with unlimited lines. Burke's theorem: each stage waits like its own
    /// M/M/1 with the same λ (docs/game-mechanism/knowledge/formulas/tandem-burke.md). λ = 0.8, μ = 1.
    /// </summary>
    public class TandemValidationTests
    {
        private const double Lambda = 0.8;
        private const double Mu = 1.0;

        private static (double Id, double Scan) Measure(ulong seed, int passengers)
        {
            var streams = new RngStreams(seed);
            var id = new Station("id", 1, new Exponential(1.0 / Mu), streams.Get("service:id"));
            var scan = new Station("scan", 1, new Exponential(1.0 / Mu), streams.Get("service:scan"));
            id.ConnectTo(scan);
            double gapMean = 1.0 / Lambda;
            var source = new Source("entry", new RenewalArrivals(new Exponential(gapMean), streams.Get("arrivals:entry")),
                id, stopTime: passengers * gapMean);
            var runner = new Runner(source);
            var log = new RunLog(keepEntries: false);
            runner.AddObserver(log);
            runner.Run();

            double warmup = 1000.0 * gapMean;
            double sumId = 0.0, sumScan = 0.0;
            int count = 0;
            foreach (PassengerRecord p in log.Passengers)
            {
                if (p.QueueJoin < warmup) continue;
                sumId += p.Visits[0].Wait;
                sumScan += p.Visits[1].Wait;
                count++;
            }
            return (sumId / count, sumScan / count);
        }

        [Test]
        public void Fast_EachStageWaitsLikeMM1()
        {
            var (id, scan) = Measure(7UL, 200_000);
            double theory = Theory.MM1Wq(Lambda, Mu);
            string detail = $"ID {id:F3}, scanner {scan:F3} vs theory {theory:F3} each";
            TestContext.WriteLine(detail);
            Assert.That(id, Is.EqualTo(theory).Within(10).Percent, detail);
            Assert.That(scan, Is.EqualTo(theory).Within(10).Percent, detail);
        }

        [Test, Category("Strict")]
        public void Strict_EachStageWithin3PercentAndInsideCI()
        {
            double theory = Theory.MM1Wq(Lambda, Mu);
            StrictCheck.AssertMeanMatches(seed => Measure(seed, 300_000).Id, theory, "Tandem stage 1 (ID)");
            StrictCheck.AssertMeanMatches(seed => Measure(seed, 300_000).Scan, theory, "Tandem stage 2 (scanner)");
        }
    }
}
```

- [ ] **Step 9: Run both suites**

Run: `bash scripts/run-tests.sh` → Expected **92** pass, about 4.0 printed for both stages.
Run: `bash scripts/run-tests.sh --strict` → Expected **19** pass.

- [ ] **Step 10: Commit**

```bash
git add Unity/Assets/Core docs/game-mechanism/knowledge/formulas
git commit -m "Chain stations with walk time; validate a tandem against Burke's theorem"
```

---

### Task 3: Blocking (limited lines)

**Files:**
- Modify: `Unity/Assets/Core/Tests/Engine/ChainTests.cs`
- Modify: `Unity/Assets/Core/Tests/Validation/TandemValidationTests.cs`

**Interfaces:**
- Consumes: everything from Task 2 (blocking code already exists in `Station`; this task proves it).
- Produces: nothing new.

- [ ] **Step 1: Write the blocking hand tests**

Add to `ChainTests` (and add `using Throughput.Core.Engine.Behaviours;` at the top):

```csharp
        [Test]
        public void FullLine_BlocksUpstream_AndRestartsInOrder()
        {
            // ID 10 s → scanner 100 s, scanner line holds 1. Four people arrive at 0.
            // A: ID 0–10, scanner 10–110.            B: ID 10–20, waits in the scanner line (now full).
            // C: ID 20–30, line full → ID BLOCKED holding C. D waits at ID.
            // 110: A leaves; scanner starts B; the line has room → C released, ID starts D (110–120).
            // 120: D done, line full (C) → blocked again. 210: B done → scanner starts C → D released.
            // Scanner starts 10, 110, 210, 310; exits 110, 210, 310, 410.
            Station id = Desk("id", 10.0), scan = Desk("scan", 100.0, lineCapacity: 1);
            id.ConnectTo(scan);
            var (runner, log) = Run(id, 0.0, 0.0, 0.0, 0.0);

            var p = log.Passengers;
            Assert.That(p.Select(r => r.Visits[1].ServiceStart), Is.EqualTo(new[] { 10.0, 110.0, 210.0, 310.0 }));
            Assert.That(p.Select(r => r.Exit), Is.EqualTo(new[] { 110.0, 210.0, 310.0, 410.0 }));
            Assert.That(p.Select(r => r.Visits[0].BlockedTime), Is.EqualTo(new[] { 0.0, 0.0, 80.0, 90.0 }));
            Assert.That(p[3].Visits[0].ServiceStart, Is.EqualTo(110.0));
            Assert.That(log.Entries.Count(e => e.Kind == LogKind.Blocked), Is.EqualTo(2));
            InvariantChecker.AssertAll(log, OneEach, lineCapacity: new Dictionary<string, int> { { "scan", 1 } });
        }

        [Test]
        public void GiveUpInFullLine_ReleasesBlockedDesk()
        {
            // ID 10 s → scanner 100 s, scanner line holds 1, patience 50 s at every line.
            // A: ID 0–10, scanner 10–110. B: ID 10–20, joins the scanner line at 20 (full).
            // C: ID 20–30, blocked. B gives up at 20 + 50 = 70 → the line has room → C released at 70.
            // (C's own ID-line timer from 0 + 50 = 50 is ignored: C started at 20.)
            Station id = Desk("id", 10.0), scan = Desk("scan", 100.0, lineCapacity: 1);
            id.ConnectTo(scan);
            var runner = new Runner(new Source("entry", new FixedArrivals(new[] { 0.0, 0.0, 0.0 }), id));
            runner.AddBehaviour(new Reneging(new Deterministic(50.0), new Pcg32(1UL, 3UL)));
            var log = new RunLog();
            runner.AddObserver(log);
            runner.Run();

            var p = log.Passengers;
            Assert.That(p[1].Renege, Is.EqualTo(70.0));
            Assert.That(p[2].Visits[0].Unblocked, Is.EqualTo(70.0));
            Assert.That(p[2].Visits[1].QueueJoin, Is.EqualTo(70.0));
            InvariantChecker.AssertAll(log, OneEach, lineCapacity: new Dictionary<string, int> { { "scan", 1 } });
        }
```

Check the second test by hand before running. C joins the scanner line at 70 with patience 50, so C would give up at 120 — but A finishes at 110 and the scanner then starts C (wait 40). So C is served, which the invariants confirm.

- [ ] **Step 2: Run them**

Run: `bash scripts/run-tests.sh Throughput.Core.Tests.Engine`
Expected: PASS (the blocking code came with Task 2). **Make them fail once on purpose:** in `Station.Finish`, temporarily change `!Next.HasRoom` to `false` (blocking switched off), run, see both new tests FAIL (capacity invariant and the timeline), then restore it. If they pass with blocking switched off, the tests are wrong: stop and fix them.

- [ ] **Step 3: Random 3-stage invariants**

Add to `TandemValidationTests` (and add `using System.Collections.Generic;` and `using Throughput.Core.Engine.Behaviours;`):

```csharp
        [Test]
        public void Invariants_HoldOnBusyChainWithBlockingAndGivingUp()
        {
            // entry → ID (2 staff) → walk 0.2 → scanner (1 staff, line holds 3) → walk 0.1 → bag check (1 staff, line holds 2) → exit.
            // Busy enough that the small lines fill and block, with patience so people also give up.
            var streams = new RngStreams(11UL);
            var id = new Station("id", 2, new Exponential(1.6), streams.Get("service:id"));
            var scan = new Station("scan", 1, Distribution.FromMeanCv(0.9, 0.5), streams.Get("service:scan"), lineCapacity: 3);
            var bags = new Station("bags", 1, Distribution.FromMeanCv(0.85, 1.0), streams.Get("service:bags"), lineCapacity: 2);
            id.ConnectTo(scan, walkTime: 0.2);
            scan.ConnectTo(bags, walkTime: 0.1);
            var runner = new Runner(new Source("entry", new RenewalArrivals(new Exponential(1.0), streams.Get("arrivals:entry")),
                id, stopTime: 5_000.0));
            runner.AddBehaviour(new Reneging(new Exponential(20.0), streams.Get("patience")));
            var log = new RunLog();
            runner.AddObserver(log);
            runner.Run();

            Assert.That(log.Entries.Count(e => e.Kind == LogKind.Blocked), Is.GreaterThan(0), "The test should actually block.");
            InvariantChecker.AssertAll(log,
                new Dictionary<string, int> { { "id", 2 }, { "scan", 1 }, { "bags", 1 } },
                lineCapacity: new Dictionary<string, int> { { "scan", 3 }, { "bags", 2 } });
        }
```

(Add `using System.Linq;` too, for `Count`.)

- [ ] **Step 4: Run both suites**

Run: `bash scripts/run-tests.sh` → Expected **95** pass (92 + 2 + 1).
Run: `bash scripts/run-tests.sh --strict` → Expected **19** pass.

If the "should actually block" assert fails, the chain is not busy enough: lower the scanner line capacity to 2. Do not remove the assert.

- [ ] **Step 5: Commit**

```bash
git add Unity/Assets/Core/Tests
git commit -m "Prove blocking: full line stops the desk upstream and restarts in order"
```

---

### Task 4: Close S5 in the docs

**Files:** spec, `docs/architecture/simulation-core.md`, `docs/roadmap.md`, `docs/game-mechanism/knowledge/formulas/tandem-burke.md`, `formulas/README.md`.

- [ ] **Step 1: Spec**

- §6 S5: tick the boxes. Rewrite the first box as `- [x] Line capacity (the "buffer", B1), walk time on edges` and add a line `- Router moved to S8 (B2)`. Tick "Blocking after service", the hand tests and the 3-stage invariants (the test used ID → scanner → bag check → exit).
- In S8's list, add `- [ ] \`Router\` + routing policies (moved from S5)`.
- §5.2: tick `- [x] No queue or buffer ever exceeds its capacity (D8)`, with the note "(lines fed by stations; arrivals from a Source spill back, B4)".
- Progress row S5: `Done (2026-10-11: tandem strict ID/scanner …; blocking hand tests)` with the measured numbers.
- Notes for later slices: mark the S5 line **Done** (per-visit records, `Finish` hands passengers on). Keep the S5 review note about re-visiting the same station, and change its prefix to "**S8 (routing):**", because only a router can send someone back.
- Add B1–B7 as a short "S5 decisions" list after the S4 decisions.

- [ ] **Step 2: Architecture**

In `simulation-core.md`'s network table, change the `Queue` and `Buffer` rows to one row: `| Line (inside \`Station\`) | The line in front of a desk; its capacity is the waiting area (B1) | Capacity; when full, the station feeding it **blocks** (D8). Arrivals from a Source always join and spill back (B4) |`. Add under the table: "Walkers reserve their spot when they leave a desk (B3). Release after blocking is first blocked, first released, and can cascade upstream (B5)." Update the same-time order line to `EndService → Renege → Reach → Arrive`.

- [ ] **Step 3: Formula page and roadmap**

`tandem-burke.md`: tick `Strict test passes`. README row 7: tick the last column. Roadmap: `S0–S5 done; next: S6 (timeline, snapshots, re-simulate)`.

- [ ] **Step 4: Run both suites and commit**

Run: `bash scripts/run-tests.sh && bash scripts/run-tests.sh --strict` → 95 and 19 pass.

```bash
git add docs
git commit -m "Close S5: chain of stages, walking and blocking"
```

Push only when the user asks.
