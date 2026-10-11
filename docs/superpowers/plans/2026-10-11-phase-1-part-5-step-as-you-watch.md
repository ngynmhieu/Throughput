# Phase 1 Part 5: Step as You Watch (S6) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The engine can run in small steps while the player watches, stop its clock exactly at "now", and report the true end of a run. Running in many small steps gives exactly the same run as one big run.

**Architecture:** Decision **A2 is revised** (user, 2026-10-11): instead of "simulate the whole wave first, then re-simulate from a snapshot when the player acts", the game **simulates as you watch**. Each frame calls `runner.Run(until: now)`. A manager action is applied to the live engine at the current time, and the run simply continues. No snapshots and no re-runs are needed. The `RunLog` recorded along the way **is** the timeline for the replay and the results. Determinism (P3) holds because a replay runs the same seed and applies the same actions at the same times.

**Tech Stack:** Unity 6000.3.26f1, C#, NUnit (EditMode), Unity CLI via `scripts/run-tests.sh`.

**Spec:** [docs/superpowers/specs/2026-10-11-phase-1-core-design.md](../specs/2026-10-11-phase-1-core-design.md) (§5.4 determinism, §6 S6, the S6 and S6/S9 notes). Architecture: [overview.md](../../architecture/overview.md) (A2), [viewer-playback.md](../../architecture/viewer-playback.md).

## Global Constraints

- `Throughput.Core` never references Unity. Never real time or frame time inside the engine: the viewer passes the game time in.
- Time never goes backward.
- Private fields `_camelCase`.
- The Unity editor must be closed to run `scripts/run-tests.sh`.
- Git: commit locally after each task; push only when the user asks. Messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Design decisions (the user reviews these)

| # | Decision | Why |
|---|---|---|
| C1 | **A2 revised: simulate as you watch** (user's choice, option C). | No wasted future, no snapshot copies to get wrong, and actions need no re-run. Rewinding during a run isn't needed (D4: 1× with pause; scrubbing is for the replay). |
| C2 | **`Run(until)` leaves the clock exactly at `until`** (when `until` is finite). | An action applied after `Run(until: 4:30)` must happen at 4:30, not at the last event before it. Fixes the S6 spec note. |
| C3 | **`Run(until)` refuses a time in the past.** | Time never goes backward; calling it with an earlier time is always a bug in the caller. |
| C4 | **`LastActiveTime`**: the time of the last event that changed something. Ignored patience alarms don't count. `IsFinished` is true when nothing is left on the calendar. | The real end of a run, for replay length and throughput. Fixes the S4 review note. |
| C5 | **No separate `Timeline` class yet.** `RunLog` (entries + per-visit passenger records) is the recording. | It already holds everything the replay and results need. A `Timeline` type is added in phase 2 if the viewer needs a different shape. |
| C6 | **Manager actions arrive in S8.** S6 only guarantees the engine can be paused at an exact time and changed there. | Actions need the policies and staffing rules from S8. The test "an action changes only what it affects" moves to S8. |

## Review Focus

1. **An event exactly at `until`.** Expected: handled in this step (not the next). Pinned by `RunUntil_HandlesEventAtExactlyUntil`.
2. **A step that contains no events.** Expected: the clock still moves to `until`; nothing else changes. Pinned by `RunUntil_StopsClockExactly`.
3. **Steps that split ties.** Several events at the same time must all be handled in the same step, in the same order as one big run. Pinned by `ManySmallSteps_EqualOneRun` (the chain has same-time events from blocking and walking 0).
4. **Calling `Run(until)` after the run is finished.** Expected: the clock moves to `until`, nothing else happens, no error.
5. **A huge `until` vs infinity.** `Run()` (infinity) leaves `Now` at the last event, as before; only finite `until` moves the clock. Pinned by the existing tests (`runner.Now` after `Run()`).

---

## File Structure

| File | Change | Responsibility |
|---|---|---|
| `Unity/Assets/Core/Engine/Events/Runner.cs` | Modify | Clock stops at `until`; reject the past; `LastActiveTime`; `IsFinished` |
| `Unity/Assets/Core/Engine/Network/Station.cs` | Modify | `Renege` returns whether it acted |
| `Unity/Assets/Core/Tests/Engine/SteppingTests.cs` | Create | Hand tests + "many steps = one run" |
| Docs | Modify | Task 3 |

---

### Task 1: Exact stepping and the true end time

**Files:**
- Modify: `Unity/Assets/Core/Engine/Events/Runner.cs`
- Modify: `Unity/Assets/Core/Engine/Network/Station.cs`
- Create: `Unity/Assets/Core/Tests/Engine/SteppingTests.cs`

**Interfaces:**
- Produces: `Runner.Run(double until = +∞)` (finite `until` → `Now == until` afterwards; `until < Now` or NaN → `ArgumentOutOfRangeException`); `Runner.LastActiveTime` (double); `Runner.IsFinished` (bool); internal `bool Station.Renege(Runner, Passenger)`.

- [ ] **Step 1: Write the failing tests**

Create `Unity/Assets/Core/Tests/Engine/SteppingTests.cs`:

```csharp
using System;
using NUnit.Framework;
using Throughput.Core.Engine.Behaviours;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Results;

namespace Throughput.Core.Tests.Engine
{
    /// <summary>Running in steps while the player watches (A2 revised, decisions C2–C4).</summary>
    public class SteppingTests
    {
        private static (Runner runner, RunLog log) Build(double service, params double[] arrivals)
        {
            var station = new Station("desk", 1, new Deterministic(service), new Pcg32(1UL, 1UL));
            var runner = new Runner(new Source("entry", new FixedArrivals(arrivals), station));
            var log = new RunLog();
            runner.AddObserver(log);
            return (runner, log);
        }

        [Test]
        public void RunUntil_StopsClockExactly()
        {
            // 1 desk, 120 s each. A at 0, B at 30. Stop at 50: both arrived, A still being served.
            var (runner, log) = Build(120.0, 0.0, 30.0);
            runner.Run(50.0);

            Assert.That(runner.Now, Is.EqualTo(50.0));
            Assert.That(log.Passengers.Count, Is.EqualTo(2));
            Assert.That(double.IsNaN(log.Passengers[0].ServiceEnd), Is.True);
            Assert.That(runner.IsFinished, Is.False);

            runner.Run(55.0); // a step with no events: only the clock moves
            Assert.That(runner.Now, Is.EqualTo(55.0));

            runner.Run();
            Assert.That(runner.Now, Is.EqualTo(240.0));
            Assert.That(runner.IsFinished, Is.True);
        }

        [Test]
        public void RunUntil_HandlesEventAtExactlyUntil()
        {
            var (runner, log) = Build(120.0, 0.0, 30.0);
            runner.Run(30.0);
            Assert.That(log.Passengers.Count, Is.EqualTo(2));
        }

        [Test]
        public void RunUntil_RejectsThePast()
        {
            var (runner, _) = Build(120.0, 0.0, 30.0);
            runner.Run(50.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => runner.Run(10.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => runner.Run(double.NaN));
            runner.Run(50.0); // the same time is fine (e.g. paused)
        }

        [Test]
        public void LastActiveTime_IgnoresDeadPatienceAlarms()
        {
            // 1 desk, 300 s each, patience 1000 s. A at 0, B at 50. Both are served: A 0–300, B 300–600.
            // Their alarms (1000 and 1050) fire later and do nothing: Now ends at 1050, the run really ended at 600.
            var (runner, _) = Build(300.0, 0.0, 50.0);
            runner.AddBehaviour(new Reneging(new Deterministic(1000.0), new Pcg32(1UL, 3UL)));
            runner.Run();

            Assert.That(runner.Now, Is.EqualTo(1050.0));
            Assert.That(runner.LastActiveTime, Is.EqualTo(600.0));
            Assert.That(runner.IsFinished, Is.True);
        }

        [Test]
        public void AfterFinished_RunUntilOnlyMovesTheClock()
        {
            var (runner, log) = Build(120.0, 0.0);
            runner.Run();
            int entries = log.Entries.Count;
            runner.Run(1000.0);
            Assert.That(runner.Now, Is.EqualTo(1000.0));
            Assert.That(log.Entries.Count, Is.EqualTo(entries));
            Assert.That(runner.LastActiveTime, Is.EqualTo(120.0));
        }
    }
}
```

- [ ] **Step 2: Run to see it fail**

Run: `bash scripts/run-tests.sh Throughput.Core.Tests.Engine`
Expected: compile error, `'Runner' does not contain a definition for 'IsFinished'` (and `LastActiveTime`).

- [ ] **Step 3: `Station.Renege` reports whether it acted**

In `Station.cs`, change `Renege` to return `bool`:

```csharp
        /// <summary>A Renege event fired. Returns false (and does nothing) if the passenger already left the line.</summary>
        internal bool Renege(Runner runner, Passenger passenger)
        {
            if (!_inLine.TryGetValue(passenger, out LinkedListNode<Passenger> node)) return false;
            _line.Remove(node);
            _inLine.Remove(passenger);
            runner.NotifyRenege(this, passenger);
            ReleaseBlocked(runner);
            return true;
        }
```

- [ ] **Step 4: Change `Runner`**

In `Runner.cs`:

(a) Add after the `Now` property:

```csharp
        /// <summary>Time of the last event that changed something (ignored patience alarms don't count): the run's real end.</summary>
        public double LastActiveTime { get; private set; }

        /// <summary>Nothing is left on the calendar: no arrivals to come, nobody in the checkpoint.</summary>
        public bool IsFinished => _started && _events.Count == 0;
```

(b) Replace `Run` with:

```csharp
        /// <summary>
        /// Handles every event up to and including time <paramref name="until"/>, then stops the clock exactly
        /// at <paramref name="until"/> (when it's finite). Call it again with a later time to continue: this is
        /// how the game steps the run while the player watches. Run() with no argument runs to the end.
        /// </summary>
        public void Run(double until = double.PositiveInfinity)
        {
            if (double.IsNaN(until) || until < Now)
                throw new ArgumentOutOfRangeException(nameof(until), until, $"Can't run to {until}: the clock is already at {Now}.");

            if (!_started)
            {
                _started = true;
                foreach (Source source in _sources)
                    ScheduleNextArrival(source);
            }

            while (_events.Count > 0 && _events.Peek().Time <= until)
            {
                EngineEvent e = _events.Pop();
                Now = e.Time;
                if (Handle(e))
                    LastActiveTime = Now;
            }

            if (!double.IsPositiveInfinity(until))
                Now = until;
        }
```

(c) Change `Handle` to return whether the event changed something:

```csharp
        private bool Handle(EngineEvent e)
        {
            switch (e.Type)
            {
                case EventType.Arrive:
                    var source = (Source)e.Node;
                    var passenger = new Passenger(_nextPassengerId++, Now);
                    foreach (IRunObserver o in _observers) o.OnArrive(Now, passenger);
                    ScheduleNextArrival(source);
                    source.Target.Accept(this, passenger);
                    return true;
                case EventType.EndService:
                    ((Station)e.Node).Finish(this, e.Passenger);
                    return true;
                case EventType.Reach:
                    ((Station)e.Node).Reach(this, e.Passenger);
                    return true;
                case EventType.Renege:
                    return ((Station)e.Node).Renege(this, e.Passenger);
                default:
                    throw new InvalidOperationException($"Unhandled event type {e.Type}.");
            }
        }
```

- [ ] **Step 5: Run the tests**

Run: `bash scripts/run-tests.sh`
Expected: **101** pass (96 + 5). The existing `runner.Now` checks after `Run()` (e.g. `Is.EqualTo(240.0)`) still pass: `Run()` with no argument doesn't move the clock past the last event.

- [ ] **Step 6: Commit**

```bash
git add Unity/Assets/Core
git commit -m "Step the run exactly: clock stops at until, true end time, IsFinished"
```

---

### Task 2: Many small steps = one run

**Files:**
- Modify: `Unity/Assets/Core/Tests/Engine/DeterminismTests.cs`

**Interfaces:**
- Consumes: Task 1's `Run(until)`, `IsFinished`, `LastActiveTime`.

- [ ] **Step 1: Write the test**

Add to `DeterminismTests` (add `using Throughput.Core.Engine.Behaviours;`, `using Throughput.Core.Engine.Events;`, `using Throughput.Core.Engine.Network;`, `using Throughput.Core.Results;`):

```csharp
        /// <summary>A busy 3-stage chain with walking, blocking and giving up: every kind of same-time event.</summary>
        private static (Runner runner, RunLog log) BuildChain(ulong seed)
        {
            var streams = new RngStreams(seed);
            var id = new Station("id", 2, new Exponential(1.6), streams.Get("service:id"));
            var scan = new Station("scan", 1, Distribution.FromMeanCv(0.9, 0.5), streams.Get("service:scan"), lineCapacity: 3);
            var bags = new Station("bags", 1, new Deterministic(0.85), streams.Get("service:bags"), lineCapacity: 2);
            id.ConnectTo(scan, walkTime: 0.2);
            scan.ConnectTo(bags);
            var runner = new Runner(new Source("entry", new RenewalArrivals(new Exponential(1.0), streams.Get("arrivals:entry")),
                id, stopTime: 2_000.0));
            runner.AddBehaviour(new Reneging(new Exponential(20.0), streams.Get("patience")));
            var log = new RunLog();
            runner.AddObserver(log);
            return (runner, log);
        }

        [Test]
        public void ManySmallSteps_EqualOneRun()
        {
            // The game calls Run(until: now) every frame. That must give exactly the run you'd get in one go.
            var (whole, wholeLog) = BuildChain(42UL);
            whole.Run();

            var (stepped, steppedLog) = BuildChain(42UL);
            double t = 0.0;
            while (!stepped.IsFinished)
            {
                t += 0.37; // an awkward step size: steps land between, and sometimes exactly on, events
                stepped.Run(t);
            }

            Assert.That(steppedLog.Entries.Select(e => e.ToString()), Is.EqualTo(wholeLog.Entries.Select(e => e.ToString())));
            Assert.That(stepped.LastActiveTime, Is.EqualTo(whole.LastActiveTime));
        }
```

- [ ] **Step 2: Run it**

Run: `bash scripts/run-tests.sh`
Expected: **102** pass. **Make it fail once on purpose:** in `Runner.Run`, temporarily change `<= until` to `< until`, run, see `RunUntil_HandlesEventAtExactlyUntil` fail. Restore it. (If `ManySmallSteps_EqualOneRun` also fails there, good; if not, that's fine: with a step of 0.37 exact hits are rare.)

- [ ] **Step 3: Commit**

```bash
git add Unity/Assets/Core/Tests
git commit -m "Prove many small steps give exactly the same run as one"
```

---

### Task 3: Docs (A2 revised, S6 closed)

**Files:** `docs/architecture/overview.md`, `docs/architecture/viewer-playback.md`, `docs/architecture/simulation-core.md`, spec, roadmap.

- [ ] **Step 1: Revise A2 in `overview.md`**

- Summary table row A2: `**Simulate as you watch**: the engine runs step by step up to the playback time; manager actions apply to the live engine at the current time; the recording is the replay`.
- Rename the section heading `### A2. Simulate first, then replay` to `### A2. Simulate as you watch, record, replay` and rewrite its bullets:
  - Each frame, the playback controller calls `Run(until: game time)`. The engine handles everything up to that time and stops its clock there.
  - A manager action at time *t* is applied to the live engine at *t*; the run continues. Actions are recorded as `(t, action)`.
  - Everything that happens is recorded (`RunLog`), which is the replay and the input for results.
  - Same seed + plan + actions → same run: a replay runs the engine again and applies the recorded actions at their times (P3).
  - No result preview (D11) comes for free: the future isn't computed yet.
- Add a **History** line: "2026-10-11: was 'simulate the whole wave first, re-simulate from a snapshot when the player acts'. Changed because the future was thrown away on every action and snapshot copies are easy to get wrong; stepping needs neither (user's choice, option C). Rewinding during a run isn't needed (D4)."
- Fix any other line in `overview.md` that says the whole wave is simulated when Run is pressed (the "Cost" bullet and the Timeline row: "Every event with its time" stays; drop "plus a state snapshot every few seconds", and say scrubbing a replay rebuilds the state from the recording).

- [ ] **Step 2: `viewer-playback.md`**

Replace the diagram with:

```
Plan ──Run──► each frame: engine.Run(until: game time) ──► recording ──► shown at 1×
                         ▲                                         │
                         │  manager action at time t: applied to   │
                         └──── the live engine, run continues ◄────┘
                                                                   │
                                     everyone gone ──► results + replay (from the recording)
```

Rewrite steps 1 and 3: (1) **Run:** the compiler builds the network, a new run seed is drawn, and the engine starts; it advances as playback advances. (3) **Act:** the action is recorded as `(t, action)` and applied to the engine at *t*; nothing is recomputed. Step 5 (Replay): scrubbing rebuilds what the checkpoint looked like at any time from the recording.

- [ ] **Step 3: `simulation-core.md`**

Under the event loop, add: "**Stepping:** `Run(until)` handles every event up to and including `until` and leaves the clock exactly at `until`, so the viewer can advance it frame by frame and actions land at an exact time. `LastActiveTime` is the real end of a run (ignored alarms don't count)."

- [ ] **Step 4: Spec**

- §6 S6: rename to `### S6: Stepping (simulate as you watch)` and replace its boxes with:
  - `- [x] \`Run(until)\` stops the clock exactly at \`until\`; refuses the past (C2, C3)`
  - `- [x] \`LastActiveTime\` (true end) and \`IsFinished\` (C4)`
  - `- [x] Many small steps = one run (determinism)`
  - `- Snapshots and re-simulate-from-snapshot dropped: A2 revised to "simulate as you watch" (2026-10-11)`
- §5.4: replace `- [ ] Re-simulating from a snapshot = the same run without stopping` with `- [x] Running in many small steps = the same run in one go (A2 revised)`. Move `- [ ] A manager action changes only what it affects` to S8's list.
- The S6 notes: mark both **Done** (clock stops at `until`; `LastActiveTime`).
- Progress row S6: `| S6 | Stepping (simulate as you watch) | Done (2026-10-11) |`.
- Add C1–C6 as a short "S6 decisions" list after the S5 decisions.
- Change log row: `| 2026-10-11 | A2 revised: simulate as you watch (no snapshots, no re-sim); S6 rescoped and done |`.
- In §2 "In" scope (line 24), replace "timeline with snapshots, re-simulate from a snapshot" with "stepping while the player watches, the recording".

- [ ] **Step 5: Roadmap**

Sub-project 1 status: `S0–S6 done; next: S7 (level/plan data, layout compiler)`. If the roadmap's "Done when" for sub-project 2 mentions "re-simulate from now", change it to "manager actions applied live".

- [ ] **Step 6: Run both suites and commit**

Run: `bash scripts/run-tests.sh && bash scripts/run-tests.sh --strict` → 102 and 19 pass.

```bash
git add docs
git commit -m "Revise A2 to simulate-as-you-watch; close S6"
```

Push only when the user asks.
