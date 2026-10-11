# Phase 1 Part 3: Reneging and Erlang A (S4) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Passengers in line can give up when their patience runs out (reneging, "missed flight"), and the engine is proven against Erlang A.

**Architecture:** A new event type `Renege`. A behaviour (`IBehaviour`, first module `Reneging`) schedules a `Renege` event when a passenger joins a line. The event is **cancelled lazily**: when it fires, the station checks whether the passenger is still in line; if they already started service, nothing happens. The line becomes a linked list so a passenger can leave from the middle. Observers get `OnRenege`; the run log records it; the invariant checker learns that "gave up" is a way to leave.

**Tech Stack:** Unity 6000.3.26f1, C#, NUnit (EditMode), Unity CLI via `scripts/run-tests.sh`.

**Spec:** [docs/superpowers/specs/2026-10-11-phase-1-core-design.md](../specs/2026-10-11-phase-1-core-design.md) (§5.2 invariants, §5.3 tiers, §6 S4, the S4 review note). Architecture: [docs/architecture/simulation-core.md](../../architecture/simulation-core.md) (Behaviours, D6). Formula: [erlang-a.md](../../game-mechanism/knowledge/formulas/erlang-a.md).

## Global Constraints

- `Throughput.Core` never references Unity. Tests live in `Throughput.Core.Tests`.
- Determinism: each source of randomness has its own named stream from `RngStreams` (patience gets `"patience:<station>"`). Never real time.
- Tie order (spec S-D4): lower `EventType` value first, then `Seq`.
- Fast tier ±10%, 1 seed. Strict tier `[Category("Strict")]`: 20 replications, 99% CI contains theory **and** error ≤ 3%.
- Conservation (spec 5.2): arrived = exited + reneged + still inside.
- Private fields `_camelCase`.
- The Unity editor must be closed to run `scripts/run-tests.sh`.
- Git: commit locally after each task; push only when the user asks. Messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Design decisions (made in this plan; the user reviews them)

| # | Decision | Why |
|---|---|---|
| R1 | **Tie order: `EndService` (0) → `Renege` (1) → `Arrive` (2).** | If patience runs out at the exact moment a desk frees, the passenger is **served**, not lost. It's kinder and matches "free the desk first" (S-D4). |
| R2 | **Lazy cancellation**: the `Renege` event stays in the list; when it fires, it does nothing if the passenger is no longer in line. | Removing an event from the middle of a heap is complicated. Ignoring it is simple and gives the same timeline. |
| R3 | **A reneger leaves the checkpoint** (not `OnExit`, but `OnRenege`). Their `Wait` is the time from joining the line to giving up. | Spec 5.2 counts reneges separately from exits. Erlang A's Wq averages over **everyone**, including people who gave up, so `Wait` must include them. This also fixes the S4 review note on Little's Law. |
| R4 | **`IBehaviour` has one method for now: `OnJoinQueue`.** | YAGNI. Later behaviours (balking, lane switching) add methods when they need them. |

## Review Focus

1. **Patience ends exactly when a desk frees.** Expected: served (R1). Pinned in Task 2, `GivesUp_ThenNextInLineServedAtTie`.
2. **A passenger who starts service at once still has a `Renege` event pending.** Expected: ignored, no renege. Pinned in Task 2, `GivesUp_WhenPatienceRunsOut` (p0).
3. **Leaving from the middle of the line.** Expected: the others keep FIFO order. Pinned in Task 2, `GivesUp_FromTheMiddleOfTheLine_OthersKeepTheirOrder`.
4. **Patience 0 with a busy desk.** Expected: gives up at once with wait 0, and the run doesn't break. Pinned in Task 2, `ZeroPatience_LeavesAtOnceWhenDeskBusy`.
5. **Overload (ρ > 1).** Without reneging the line grows forever; with it, the system settles. Expected: finite waits matching Erlang A. Pinned by the ρ = 1.2 validation case in Task 3.
6. **Little's Law with renegers.** The queue area must equal the total time in line, including people who gave up. Pinned by the invariants test in Task 3.

---

## File Structure

| File | Change | Responsibility |
|---|---|---|
| `Unity/Assets/Core/Engine/Events/IRunObserver.cs` | Modify | Add `OnRenege` |
| `Unity/Assets/Core/Results/RunLog.cs` | Modify | `LogKind.Renege`, `PassengerRecord.Renege`, `HasReneged`, `Wait` covers renegers |
| `Unity/Assets/Core/Tests/Support/InvariantChecker.cs` | Modify | Renege counts as leaving; journey order; FIFO with removal |
| `Unity/Assets/Core/Tests/Invariants/InvariantCheckerTests.cs` | Modify | 2 new "must fail" tests |
| `Unity/Assets/Core/Engine/Events/EventType.cs` | Modify | `Renege = 1`, `Arrive = 2` |
| `Unity/Assets/Core/Engine/Events/Runner.cs` | Modify | Behaviours list, handle `Renege`, `NotifyRenege` |
| `Unity/Assets/Core/Engine/Network/Station.cs` | Modify | Linked-list line, `Renege(runner, p)` |
| `Unity/Assets/Core/Engine/Behaviours/IBehaviour.cs` | Create | The behaviour interface |
| `Unity/Assets/Core/Engine/Behaviours/Reneging.cs` | Create | The patience timer |
| `Unity/Assets/Core/Tests/Engine/RenegingTests.cs` | Create | Hand tests |
| `Unity/Assets/Core/Tests/Support/Experiments.cs` | Modify | Optional patience; `AbandonShare`; `MeasureErlangA` |
| `Unity/Assets/Core/Tests/Validation/ErlangAValidationTests.cs` | Create | Erlang A fast + strict + invariants |
| Spec, formula pages, roadmap, architecture | Modify | Tick and record (Task 4) |

Unity creates `.meta` files for new `.cs` files during the test run; commit them alongside.

---

### Task 1: Record reneging (observer, run log, invariant checker)

**Files:**
- Modify: `Unity/Assets/Core/Engine/Events/IRunObserver.cs`
- Modify: `Unity/Assets/Core/Results/RunLog.cs`
- Modify: `Unity/Assets/Core/Tests/Support/InvariantChecker.cs`
- Test: `Unity/Assets/Core/Tests/Invariants/InvariantCheckerTests.cs`

**Interfaces:**
- Produces: `IRunObserver.OnRenege(double time, Station station, Passenger passenger)`; `LogKind.Renege`; `PassengerRecord.Renege` (double, NaN until it happens), `PassengerRecord.HasReneged` (bool); `PassengerRecord.Wait` = (`Renege` if reneged, else `ServiceStart`) − `QueueJoin`.

- [ ] **Step 1: Write the failing checker tests**

Add to `InvariantCheckerTests` (after `Fails_WhenLineIsNotFirstComeFirstServed`):

```csharp
        [Test]
        public void Fails_WhenSomeoneIsServedAndAlsoGivesUp()
        {
            var desk = new Station("desk", 1, new Deterministic(1.0), new Pcg32(1UL, 1UL));
            var p0 = new Passenger(0, 0.0);
            var log = new RunLog();
            log.OnArrive(0.0, p0);
            log.OnJoinQueue(0.0, desk, p0);
            log.OnStartService(0.0, desk, p0);
            log.OnRenege(5.0, desk, p0);

            var staff = new Dictionary<string, int> { { "desk", 1 } };
            Assert.Throws<AssertionException>(() => InvariantChecker.AssertAll(log, staff, expectDrained: false));
        }

        [Test]
        public void Fails_WhenSomeoneGivesUpWithoutBeingInLine()
        {
            var desk = new Station("desk", 1, new Deterministic(1.0), new Pcg32(1UL, 1UL));
            var p0 = new Passenger(0, 0.0);
            var log = new RunLog();
            log.OnArrive(0.0, p0);
            log.OnRenege(5.0, desk, p0);

            var staff = new Dictionary<string, int> { { "desk", 1 } };
            Assert.Throws<AssertionException>(() => InvariantChecker.AssertAll(log, staff, expectDrained: false));
        }
```

- [ ] **Step 2: Run to see it fail**

Run: `bash scripts/run-tests.sh Throughput.Core.Tests.Invariants`
Expected: compile error, `'RunLog' does not contain a definition for 'OnRenege'`. The script reports that no results were written.

- [ ] **Step 3: Add `OnRenege` to the observer**

In `IRunObserver.cs`, after `OnEndService`:

```csharp
        /// <summary>The passenger's patience ran out and they left the line (and the checkpoint).</summary>
        void OnRenege(double time, Station station, Passenger passenger);
```

- [ ] **Step 4: Record it in `RunLog.cs`**

Add `Renege,` to `LogKind` after `EndService,`.

In `PassengerRecord`, replace the `Wait` and `HasExited` members with:

```csharp
        public double Renege { get; internal set; } = double.NaN;

        /// <summary>Time spent in the line: until service started, or until they gave up.</summary>
        public double Wait => (HasReneged ? Renege : ServiceStart) - QueueJoin;

        public bool HasExited => !double.IsNaN(Exit);

        public bool HasReneged => !double.IsNaN(Renege);
```

In `RunLog`, after `OnEndService`:

```csharp
        public void OnRenege(double time, Station station, Passenger passenger)
        {
            _passengers[passenger.Id].Renege = time;
            Add(time, LogKind.Renege, passenger, station);
        }
```

- [ ] **Step 5: Teach the checker**

In `InvariantChecker.cs`:

(a) In `AssertJourneysInOrder`, inside the `foreach`, after the four `AssertNotBefore` lines:

```csharp
                AssertNotBefore(p, p.Renege, p.QueueJoin, "gave up before joining the line");
                if (p.HasReneged)
                {
                    Assert.That(double.IsNaN(p.QueueJoin), Is.False, $"Passenger {p.Id} gave up without being in a line.");
                    Assert.That(double.IsNaN(p.ServiceStart), Is.True, $"Passenger {p.Id} was served and also gave up.");
                }
```

(b) In `AssertConservation`, replace the counting with:

```csharp
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
```

(c) In `AssertFirstComeFirstServed`, change the line type from `Queue<int>` to `List<int>` (both in the dictionary type and the `new`), and replace the `if / else if` block with:

```csharp
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
```

Update its summary comment to add: "People who give up leave from anywhere in the line; everyone behind them keeps their order."

`AssertLittlesLaw` needs no code change: it sums `p.Wait`, which now includes renegers (R3).

- [ ] **Step 6: Run the tests**

Run: `bash scripts/run-tests.sh`
Expected: **78** pass (76 + 2).

- [ ] **Step 7: Commit**

```bash
git add Unity/Assets/Core
git commit -m "Record reneging in observers, run log and invariant checker"
```

---

### Task 2: Reneging in the engine

**Files:**
- Modify: `Unity/Assets/Core/Engine/Events/EventType.cs`
- Modify: `Unity/Assets/Core/Engine/Events/Runner.cs`
- Modify: `Unity/Assets/Core/Engine/Network/Station.cs`
- Create: `Unity/Assets/Core/Engine/Behaviours/IBehaviour.cs`
- Create: `Unity/Assets/Core/Engine/Behaviours/Reneging.cs`
- Test: `Unity/Assets/Core/Tests/Engine/RenegingTests.cs`

**Interfaces:**
- Consumes: Task 1's `OnRenege`, `PassengerRecord.Renege`, `HasReneged`, `Wait`.
- Produces: `EventType.Renege`; `IBehaviour.OnJoinQueue(Runner runner, Station station, Passenger passenger)`; `Reneging(IDistribution patience, Pcg32 rng)`; `Runner.AddBehaviour(IBehaviour behaviour)`; internal `Station.Renege(Runner, Passenger)`, `Runner.NotifyRenege(Station, Passenger)`.

- [ ] **Step 1: Write the hand tests**

Create `Unity/Assets/Core/Tests/Engine/RenegingTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Throughput.Core.Engine.Behaviours;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Results;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Engine
{
    /// <summary>Passengers give up when their patience runs out. Fixed times, worked out by hand.</summary>
    public class RenegingTests
    {
        private static (Runner runner, RunLog log) Build(double service, double patience, params double[] arrivals)
        {
            var station = new Station("desk", 1, new Deterministic(service), new Pcg32(1UL, 1UL));
            var source = new Source("entry", new FixedArrivals(arrivals), station);
            var runner = new Runner(source);
            runner.AddBehaviour(new Reneging(new Deterministic(patience), new Pcg32(1UL, 3UL)));
            var log = new RunLog();
            runner.AddObserver(log);
            return (runner, log);
        }

        private static void CheckInvariants(RunLog log) =>
            InvariantChecker.AssertAll(log, new Dictionary<string, int> { { "desk", 1 } });

        [Test]
        public void GivesUp_WhenPatienceRunsOut()
        {
            // 1 desk, 120 s each, patience 60 s. A at 0, B at 30.
            // A starts at once (A's own timer at 60 is ignored). B would wait until 120 (90 s),
            // but gives up at 30 + 60 = 90.
            var (runner, log) = Build(120.0, 60.0, 0.0, 30.0);
            runner.Run();

            PassengerRecord a = log.Passengers[0], b = log.Passengers[1];
            Assert.That(a.HasReneged, Is.False);
            Assert.That(a.ServiceEnd, Is.EqualTo(120.0));
            Assert.That(b.HasReneged, Is.True);
            Assert.That(b.Renege, Is.EqualTo(90.0));
            Assert.That(b.Wait, Is.EqualTo(60.0));
            Assert.That(double.IsNaN(b.ServiceStart), Is.True);
            Assert.That(b.HasExited, Is.False);
            Assert.That(runner.Now, Is.EqualTo(120.0));
            CheckInvariants(log);
        }

        [Test]
        public void GivesUp_ThenNextInLineServedAtTie()
        {
            // 1 desk, 120 s each, patience 100 s. A at 0, B at 10, C at 20.
            // B gives up at 110 (first in line).
            // C's patience ends at 120, the same moment A finishes: EndService goes first (R1), so C is served.
            var (runner, log) = Build(120.0, 100.0, 0.0, 10.0, 20.0);
            runner.Run();

            var p = log.Passengers;
            Assert.That(p.Select(r => r.HasReneged), Is.EqualTo(new[] { false, true, false }));
            Assert.That(p[1].Renege, Is.EqualTo(110.0));
            Assert.That(p[2].ServiceStart, Is.EqualTo(120.0));
            Assert.That(p.Select(r => r.Wait), Is.EqualTo(new[] { 0.0, 100.0, 100.0 }));
            Assert.That(runner.Now, Is.EqualTo(240.0));
            CheckInvariants(log);
        }

        [Test]
        public void GivesUp_FromTheMiddleOfTheLine_OthersKeepTheirOrder()
        {
            // 1 desk, 300 s each. A at 0, B at 50, C at 60, D at 70. Patience: C 30 s, everyone else 1000 s.
            // At 90 the line is B, C, D and C gives up from the middle. A finishes at 300 → B (wait 250);
            // B finishes at 600 → D (wait 530). FIFO holds for those who stayed.
            var station = new Station("desk", 1, new Deterministic(300.0), new Pcg32(1UL, 1UL));
            var runner = new Runner(new Source("entry", new FixedArrivals(new[] { 0.0, 50.0, 60.0, 70.0 }), station));
            runner.AddBehaviour(new Reneging(new ListDistribution(1000.0, 1000.0, 30.0, 1000.0), new Pcg32(1UL, 3UL)));
            var log = new RunLog();
            runner.AddObserver(log);
            runner.Run();

            var p = log.Passengers;
            Assert.That(p.Select(r => r.HasReneged), Is.EqualTo(new[] { false, false, true, false }));
            Assert.That(p[2].Renege, Is.EqualTo(90.0));
            Assert.That(p.Select(r => r.Wait), Is.EqualTo(new[] { 0.0, 250.0, 30.0, 530.0 }));
            Assert.That(runner.Now, Is.EqualTo(900.0));
            CheckInvariants(log);
        }

        [Test]
        public void ZeroPatience_LeavesAtOnceWhenDeskBusy()
        {
            // Patience 0: A is served at once; B, arriving while the desk is busy, gives up the moment they join.
            var (runner, log) = Build(120.0, 0.0, 0.0, 30.0);
            runner.Run();

            Assert.That(log.Passengers[0].HasReneged, Is.False);
            Assert.That(log.Passengers[1].Renege, Is.EqualTo(30.0));
            Assert.That(log.Passengers[1].Wait, Is.EqualTo(0.0));
            CheckInvariants(log);
        }

        /// <summary>Hands out fixed values in order (one per passenger who joins the line), for hand tests.</summary>
        private sealed class ListDistribution : IDistribution
        {
            private readonly double[] _values;
            private int _next;

            public ListDistribution(params double[] values) => _values = values;

            public double Mean => _values.Average();

            public double Cv => double.NaN; // not used by hand tests

            public double Sample(Pcg32 rng) => _values[_next++];
        }
    }
}
```

- [ ] **Step 2: Run to see it fail**

Run: `bash scripts/run-tests.sh Throughput.Core.Tests.Engine`
Expected: compile error, `The type or namespace name 'Behaviours' does not exist` (or `Reneging` not found).

- [ ] **Step 3: New event type**

Replace the enum body in `EventType.cs`, and update the summary:

```csharp
    /// <summary>
    /// The kinds of scheduled event. The number is the tie-break priority when two events share
    /// a time: lower goes first (spec S-D4). EndService first, so a desk that frees up at time t
    /// can serve someone whose patience ends at t, or who arrives at t.
    /// </summary>
    public enum EventType
    {
        EndService = 0,
        Renege = 1,
        Arrive = 2,
    }
```

- [ ] **Step 4: The behaviour interface and `Reneging`**

Create `Unity/Assets/Core/Engine/Behaviours/IBehaviour.cs`:

```csharp
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;

namespace Throughput.Core.Engine.Behaviours
{
    /// <summary>
    /// Something passengers do on their own (architecture: Behaviours, D6). It reacts to engine
    /// moments and may schedule events. Adding a behaviour never changes the core loop.
    /// </summary>
    public interface IBehaviour
    {
        void OnJoinQueue(Runner runner, Station station, Passenger passenger);
    }
}
```

Create `Unity/Assets/Core/Engine/Behaviours/Reneging.cs`:

```csharp
using System;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Randomness;

namespace Throughput.Core.Engine.Behaviours
{
    /// <summary>
    /// Each passenger who joins a line draws a patience time. If they're still in line when it
    /// runs out, they give up and leave (a missed flight). Starting service cancels it: the
    /// Renege event still fires but finds them gone from the line and does nothing.
    /// </summary>
    public sealed class Reneging : IBehaviour
    {
        private readonly IDistribution _patience;
        private readonly Pcg32 _rng;

        public Reneging(IDistribution patience, Pcg32 rng)
        {
            _patience = patience ?? throw new ArgumentNullException(nameof(patience));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public void OnJoinQueue(Runner runner, Station station, Passenger passenger)
        {
            runner.Schedule(runner.Now + _patience.Sample(_rng), EventType.Renege, station, passenger);
        }
    }
}
```

- [ ] **Step 5: Wire behaviours into `Runner`**

In `Runner.cs`:
- add `using Throughput.Core.Engine.Behaviours;`
- add the field `private readonly List<IBehaviour> _behaviours = new List<IBehaviour>();`
- after `AddObserver`, add:

```csharp
        public void AddBehaviour(IBehaviour behaviour)
        {
            _behaviours.Add(behaviour ?? throw new ArgumentNullException(nameof(behaviour)));
        }
```

- replace `NotifyJoinQueue` with:

```csharp
        internal void NotifyJoinQueue(Station station, Passenger p)
        {
            foreach (IRunObserver o in _observers) o.OnJoinQueue(Now, station, p);
            foreach (IBehaviour b in _behaviours) b.OnJoinQueue(this, station, p);
        }
```

- after `NotifyEndService`, add:

```csharp
        internal void NotifyRenege(Station station, Passenger p)
        {
            foreach (IRunObserver o in _observers) o.OnRenege(Now, station, p);
        }
```

- in `Handle`, before `default:`, add:

```csharp
                case EventType.Renege:
                    ((Station)e.Node).Renege(this, e.Passenger);
                    break;
```

- [ ] **Step 6: Let `Station` remove someone from the middle of the line**

In `Station.cs`, replace the `_line` field with:

```csharp
        private readonly LinkedList<Passenger> _line = new LinkedList<Passenger>();
        private readonly Dictionary<Passenger, LinkedListNode<Passenger>> _inLine = new Dictionary<Passenger, LinkedListNode<Passenger>>();
```

Replace the first line of `Accept` (`_line.Enqueue(passenger);`) with:

```csharp
            _inLine[passenger] = _line.AddLast(passenger);
```

Replace `Passenger next = _line.Dequeue();` in `TryStart` with:

```csharp
                Passenger next = _line.First.Value;
                _line.RemoveFirst();
                _inLine.Remove(next);
```

After `Finish`, add:

```csharp
        /// <summary>A Renege event fired. Does nothing if the passenger already left the line (service started).</summary>
        internal void Renege(Runner runner, Passenger passenger)
        {
            if (!_inLine.TryGetValue(passenger, out LinkedListNode<Passenger> node)) return;
            _line.Remove(node);
            _inLine.Remove(passenger);
            runner.NotifyRenege(this, passenger);
        }
```

Update the class summary to: "A stage with one FIFO line and Staff servers, each serving one passenger at a time. People may leave the line early (reneging)."

- [ ] **Step 7: Run the tests**

Run: `bash scripts/run-tests.sh`
Expected: **82** pass (78 + 4). `EventListTests` still pass: they only use `EventType` names, not numbers.

- [ ] **Step 8: Commit**

```bash
git add Unity/Assets/Core
git commit -m "Add reneging: Renege event, IBehaviour, patience timer, hand tests"
```

---

### Task 3: Validation against Erlang A

**Files:**
- Modify: `Unity/Assets/Core/Tests/Support/Experiments.cs`
- Create: `Unity/Assets/Core/Tests/Validation/ErlangAValidationTests.cs`

**Interfaces:**
- Consumes: `Reneging(IDistribution, Pcg32)`, `Runner.AddBehaviour`, `PassengerRecord.HasReneged`, `StrictCheck.AssertMeanMatches`, `Theory.ErlangA(double lambda, double mu, int c, double theta) → ErlangAResult { Lq, ProbAbandon, Wq }`.
- Produces: `Experiments.RunSingleStation(..., IDistribution patience = null)`; `Experiments.AbandonShare(RunLog log, double warmup) → double`; `Experiments.MeasureErlangA(ulong seed, double lambda, double mu, int c, double theta, int passengers) → (double Wait, double Abandon)`.

Setup: Poisson arrivals λ, exponential service μ = 1, c desks, exponential patience with mean 1/θ. Erlang A's Wq averages time in line over **all** arrivals, including people who gave up (R3), which is exactly `MeanWait`.

- [ ] **Step 1: Extend `Experiments`**

Add `using Throughput.Core.Engine.Behaviours;`.

Change `RunSingleStation`'s signature and body to accept an optional patience:

```csharp
        public static RunLog RunSingleStation(ulong seed, IDistribution gap, IDistribution service, int staff,
            double stopTime, bool keepEntries, IDistribution patience = null)
        {
            var streams = new RngStreams(seed);
            var station = new Station("desk", staff, service, streams.Get("service:desk"));
            var source = new Source("entry", new RenewalArrivals(gap, streams.Get("arrivals:entry")), station, stopTime);
            var runner = new Runner(source);
            if (patience != null)
                runner.AddBehaviour(new Reneging(patience, streams.Get("patience:desk")));
            var log = new RunLog(keepEntries);
            runner.AddObserver(log);
            runner.Run();
            return log;
        }
```

Add after `MeasureMeanWait`:

```csharp
        /// <summary>Share of passengers who joined at or after <paramref name="warmup"/> and gave up.</summary>
        public static double AbandonShare(RunLog log, double warmup)
        {
            int gaveUp = 0;
            int count = 0;
            foreach (PassengerRecord p in log.Passengers)
            {
                if (p.QueueJoin < warmup) continue;
                if (p.HasReneged) gaveUp++;
                count++;
            }
            return count == 0 ? double.NaN : (double)gaveUp / count;
        }

        /// <summary>One Erlang A run (M/M/c+M): mean time in line over everyone, and the share who gave up.</summary>
        public static (double Wait, double Abandon) MeasureErlangA(ulong seed, double lambda, double mu, int c, double theta, int passengers)
        {
            var gap = new Exponential(1.0 / lambda);
            var log = RunSingleStation(seed, gap, new Exponential(1.0 / mu), c, stopTime: passengers * gap.Mean,
                keepEntries: false, patience: new Exponential(1.0 / theta));
            double warmup = 1000.0 * gap.Mean;
            return (MeanWait(log, warmup), AbandonShare(log, warmup));
        }
```

- [ ] **Step 2: Create `ErlangAValidationTests.cs`**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Validation
{
    /// <summary>
    /// The engine against Erlang A (M/M/c+M): c desks, exponential patience with rate θ
    /// (docs/game-mechanism/knowledge/formulas/erlang-a.md). μ = 1. Checks both the mean time in line
    /// (over everyone, including those who gave up) and the share who gave up.
    /// </summary>
    public class ErlangAValidationTests
    {
        private const double Mu = 1.0;

        [TestCase(2, 1.8, 0.5)]  // ρ = 0.9
        [TestCase(3, 3.6, 1.0)]  // ρ = 1.2: overloaded, only reneging keeps it finite
        [TestCase(1, 1.0, 0.2)]  // ρ = 1.0, very patient
        public void Fast_WaitAndAbandonWithin10Percent(int c, double lambda, double theta)
        {
            var (wait, abandon) = Experiments.MeasureErlangA(7UL, lambda, Mu, c, theta, 200_000);
            ErlangAResult theory = Theory.ErlangA(lambda, Mu, c, theta);
            string detail = $"c = {c}, λ = {lambda}, θ = {theta}: wait {wait:F4} vs {theory.Wq:F4}, gave up {abandon:F4} vs {theory.ProbAbandon:F4}";
            TestContext.WriteLine(detail);
            Assert.That(wait, Is.EqualTo(theory.Wq).Within(10).Percent, detail);
            Assert.That(abandon, Is.EqualTo(theory.ProbAbandon).Within(10).Percent, detail);
        }

        [Test, Category("Strict")]
        [TestCase(2, 1.8, 0.5)]
        [TestCase(3, 3.6, 1.0)]
        [TestCase(1, 1.0, 0.2)]
        public void Strict_WaitAndAbandonWithin3PercentAndInsideCI(int c, double lambda, double theta)
        {
            ErlangAResult theory = Theory.ErlangA(lambda, Mu, c, theta);
            string label = $"Erlang A c = {c}, λ = {lambda}, θ = {theta}";
            StrictCheck.AssertMeanMatches(seed => Experiments.MeasureErlangA(seed, lambda, Mu, c, theta, 300_000).Wait,
                theory.Wq, label + " wait");
            StrictCheck.AssertMeanMatches(seed => Experiments.MeasureErlangA(seed, lambda, Mu, c, theta, 300_000).Abandon,
                theory.ProbAbandon, label + " gave up");
        }

        [Test]
        public void Invariants_HoldWithReneging()
        {
            // Includes Little's Law with renegers: queue area = total time in line, givers-up included.
            var log = Experiments.RunSingleStation(11UL, new Exponential(1.0 / 2.4), new Exponential(1.0), staff: 2,
                stopTime: 5_000.0, keepEntries: true, patience: new Exponential(2.0));
            InvariantChecker.AssertAll(log, new Dictionary<string, int> { { "desk", 2 } });
        }
    }
}
```

The invariants case is overloaded (λ = 2.4 on 2 desks), so many people give up. That gives the checker plenty of renege events to check.

- [ ] **Step 3: Run the fast suite**

Run: `bash scripts/run-tests.sh`
Expected: **86** pass (82 + 3 fast + 1 invariants). Read the printed lines.

- [ ] **Step 4: Run the strict suite**

Run: `bash scripts/run-tests.sh --strict`
Expected: **18** pass (15 + 3). If a case misses: warm-up first, never the tolerance; otherwise stop and report the numbers.

- [ ] **Step 5: Commit**

```bash
git add Unity/Assets/Core/Tests
git commit -m "Validate reneging against Erlang A (wait and share who gave up)"
```

---

### Task 4: Close S4 in the docs

**Files:**
- Modify: `docs/superpowers/specs/2026-10-11-phase-1-core-design.md`
- Modify: `docs/game-mechanism/knowledge/formulas/erlang-a.md`, `docs/game-mechanism/knowledge/formulas/README.md`
- Modify: `docs/architecture/simulation-core.md`
- Modify: `docs/roadmap.md`

- [ ] **Step 1: Spec**

Tick the three S4 boxes. Set the Progress row S4 to `Done (2026-10-11: strict Erlang A …)` with the measured wait and gave-up numbers vs theory. In the "Notes for later slices" list, change the S4 line to start with **Done:** and say that `Wait` now counts time in line until giving up, and that Little's Law passes with renegers. Add R1–R4 from this plan as a short "S4 decisions" list under the Progress table.

- [ ] **Step 2: Formula pages**

`erlang-a.md`: tick `Implemented` and `Strict test passes`. `README.md`: row 6, tick the last two columns.

- [ ] **Step 3: Architecture**

In `simulation-core.md`, Behaviours section, add under the table: "**How cancelling works:** the `Renege` event is not removed when service starts; when it fires, the station sees the passenger is no longer in line and ignores it (lazy cancellation). `IBehaviour` has one method today (`OnJoinQueue`); later behaviours add methods as they need them." Add a line under event types: "Same-time order: `EndService` → `Renege` → `Arrive`."

- [ ] **Step 4: Roadmap**

Set sub-project 1's status to `S0–S4 done; next: S5 (stages, buffers, blocking)`.

- [ ] **Step 5: Run both suites and commit**

Run: `bash scripts/run-tests.sh && bash scripts/run-tests.sh --strict`
Expected: 86 and 18 pass.

```bash
git add docs
git commit -m "Close S4: reneging validated against Erlang A"
```

Push only when the user asks.
