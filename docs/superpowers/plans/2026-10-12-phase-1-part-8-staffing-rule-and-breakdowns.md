# Phase 1 Part 8: Staffing Rule and Breakdowns (S8b) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A staffing rule opens and closes lanes by itself (with a setup delay), the manager can override it lane by lane, and the level's event deck breaks a scanner at a random time: its check pauses and continues after the repair.

**Architecture:** Builds on S8a's lanes. A `StaffingRule` (policy) is checked after every event and every manager action; it opens a lane through a new `LaneReady` event after the setup time, and closes lanes at once (drain). Any manager open/close **pins** a lane (manual) until `ReturnToRule`. A `Breakdown` (event deck entry) fires at a time drawn per attempt; the chosen lane stops starting checks, pauses the ones in progress (stale `EndService` events are ignored, like stale patience alarms), and the router skips it until `Repair`. The run log records every lane change with who caused it, so a replay knows which actions were the player's.

**Tech Stack:** Unity 6000.3.26f1, C#, NUnit (EditMode), Unity CLI via `scripts/run-tests.sh`.

**Spec:** [docs/superpowers/specs/2026-10-11-phase-1-core-design.md](../specs/2026-10-11-phase-1-core-design.md) (§6 S8b, decisions N1–N10). Game decisions: [decisions.md](../../game-mechanism/decisions.md) D4 (plan → manage), D12 (fixed event deck, random timing).

## Global Constraints

- `Throughput.Core` never references `UnityEngine`/`UnityEditor`.
- Engine code (`Throughput.Core.Engine.*`) never depends on `Levels` or `Layout`.
- Determinism: one named random stream per source of randomness. New: `breakdown:<i>` per event-deck entry (its time, its lane and its repair time).
- A plan with no rule and a level with no breakdowns must give exactly the same runs as before (strict tier unchanged).
- Manager actions are applied after `Run(t)`.
- Private fields `_camelCase`.
- The Unity editor must be closed to run `scripts/run-tests.sh`.
- Git: commit locally after each task; push only when the user asks. Messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Design decisions (the user reviews these)

| # | Decision | Why |
|---|---|---|
| K1 | **The staffing rule runs by itself** (user's choice A). The player sets it in the plan for a stage with lanes. | A planning tool: design the rule before Run (D4). |
| K2 | **The manager's hand wins, per lane** (user's choice A). Any manager open or close **pins** that lane (even if it was already in that state); the rule leaves pinned lanes alone until `ReturnToRule(lane)`. | Predictable: what you set is what happens (P2, P4). |
| K3 | **What the rule measures: people waiting per open lane** (people in lines, not at desks or walking, divided by open lanes). It opens a lane when this is **above** `openAbove`, closes one when it is **below** `closeBelow`; `openAbove > closeBelow ≥ 0`. | Easy to read on screen ("about 4 people per lane"). The gap between the two numbers stops flicker. |
| K4 | **Which lane:** the rule opens the **lowest-numbered** closed lane that isn't pinned or broken, **one at a time per stage**, ready after `setup` minutes (a `LaneReady` event). It closes the **highest-numbered** open lane that isn't pinned, at once (it drains, N5), never the last open lane. | Deterministic, no random stream. One-at-a-time stops it from opening every lane during one burst. |
| K5 | **When the rule is checked:** after every event that changed something and after every manager action. No polling timer. | Deterministic and exact. A rule with empty lines at the start closes lanes right away: that's what the rule says. |
| K6 | **Breakdown:** the check in progress **pauses and continues** after the repair with the time left (user's choice A); the line **waits** (user's choice A); the router sends nobody new to a broken lane, except people from the hall when every open lane is broken (they must stand somewhere). Desks upstream block if no working lane has room. | Fair and visible (P2). The answer to a breakdown is "open another lane" (pairing rule). |
| K7 | **Event deck in the level:** `breakdowns: [{ stage, from, to, repair }]`. Each attempt draws the time uniformly in `[from, to]` and, at that moment, picks one open working lane of that stage, from the stream `breakdown:<i>`. A stage the plan doesn't use is skipped. | D12: the same events every attempt, random timing. |
| K8 | **A shared-line stage breaks as a whole** (all its desks stop): it is one machine. | Simple and honest; lanes are the way to spread the risk (a real trade-off for the player). |
| K9 | **The log records every lane change and who did it:** `LaneOpening`, `LaneOpened`, `LaneClosed`, `LaneBroken`, `LaneRepaired`, `LaneReturnedToRule`, with `ByManager`. A replay re-applies only the manager's entries. | The recording is the replay (A2). |
| K10 | **Deferred:** a manager action "send the broken lane's line to other lanes" (user's choice C, later); several breakdowns of the same lane overlapping (a broken lane can't break again until repaired). | Scope. |

## Review Focus

1. **A pause, then a stale `EndService`.** Expected: the old end time is ignored and the check ends at the new time, once. Pinned by `Breakdown_PausesAndResumes`.
2. **The only lane breaks while desks upstream feed it.** Expected: they block until the repair and the line frees, in order. Pinned by `Breakdown_OnlyLane_BlocksUpstreamUntilRepaired`.
3. **The manager closes a lane during its setup.** Expected: setup cancelled, the old `LaneReady` ignored, the rule moves to the next lane. Pinned by `Rule_ManagerCloseCancelsSetup_AndPins`.
4. **Same seed, same breakdown; different seeds, different times.** Pinned by `Breakdown_RandomTimeEachAttempt_SameForSameSeed`.
5. **Rule, breakdowns and random manager actions together.** Expected: every invariant holds, the run ends. Pinned by `RuleBreakdownsAndActions_KeepEveryInvariant`.

---

## File Structure

| File | Change | Responsibility |
|---|---|---|
| `Unity/Assets/Core/Engine/Events/LaneChange.cs` | Create | What happened to a lane |
| `Unity/Assets/Core/Engine/Events/Breakdown.cs` | Create | One event-deck entry: time, stage, repair, stream; picks the lane |
| `Unity/Assets/Core/Engine/Policies/StaffingRule.cs` | Create | The rule (K3–K5) |
| `Unity/Assets/Core/Engine/Events/EventType.cs` | Modify | `LaneReady`, `Breakdown`, `Repair` |
| `Unity/Assets/Core/Engine/Events/IRunObserver.cs` | Modify | `OnLaneChanged` replaces `OnLaneSwitched` |
| `Unity/Assets/Core/Engine/Events/Runner.cs` | Modify | Breakdowns, rules, pinning, `ReturnToRule` |
| `Unity/Assets/Core/Engine/Network/Station.cs` | Modify | `IsBroken`, `IsManual`, `IsOpening`; pause and resume checks |
| `Unity/Assets/Core/Engine/Network/Router.cs` | Modify | Skips broken lanes |
| `Unity/Assets/Core/Results/RunLog.cs` | Modify | New lane kinds, `ByManager` |
| `Unity/Assets/Core/Levels/LevelDef.cs` | Modify | `RuleDef`, `BreakdownDef` |
| `Unity/Assets/Core/Layout/LayoutCompiler.cs` | Modify | Builds and checks rules and breakdowns |
| `Unity/Assets/Core/Tests/Engine/BreakdownTests.cs` | Create | Hand tests |
| `Unity/Assets/Core/Tests/Engine/StaffingRuleTests.cs` | Create | Hand tests |
| `Unity/Assets/Core/Tests/Engine/LaneStressTests.cs` | Modify | Rule + breakdowns + random actions |
| `Unity/Assets/Core/Tests/Engine/LaneTests.cs` | Modify | Pinning changes one count (K2) |
| `Unity/Assets/Core/Tests/Layout/LayoutCompilerTests.cs` | Modify | Compile, check and fairness tests |

---

### Task 1: Breakdowns in the engine

**Files:**
- Create: `Unity/Assets/Core/Engine/Events/LaneChange.cs`, `Unity/Assets/Core/Engine/Events/Breakdown.cs`
- Modify: `EventType.cs`, `IRunObserver.cs`, `Runner.cs`, `Station.cs`, `Router.cs`, `RunLog.cs`
- Test: `Unity/Assets/Core/Tests/Engine/BreakdownTests.cs`

**Interfaces:**
- Produces: `enum LaneChange { Opened, Closed, Opening, Broken, Repaired, ReturnedToRule }`; `IRunObserver.OnLaneChanged(double time, Station lane, LaneChange change, bool byManager)`; `Runner.AddBreakdown(Router stage, double time, IDistribution repair, Pcg32 rng)`; `Station.IsBroken`; `LogKind.LaneOpening, LaneBroken, LaneRepaired, LaneReturnedToRule`; `LogEntry.ByManager`; private `Runner.Notify(Station, LaneChange, bool)` and `Runner.OpenNow(Station, bool)` (Task 2 uses them).

- [ ] **Step 1: Write the failing tests**

Create `Unity/Assets/Core/Tests/Engine/BreakdownTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Policies;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Results;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Engine
{
    /// <summary>The event deck's breakdowns (K6–K8). Fixed times, worked out by hand.</summary>
    public class BreakdownTests
    {
        private static Station Desk(string name, double service, int lineCapacity = int.MaxValue) =>
            new Station(name, 1, new Deterministic(service), new Pcg32(1UL, 1UL), lineCapacity);

        private static Router Lanes(string name, params Station[] lanes) => new Router(name, lanes, ShortestLine.Instance);

        private static (Runner runner, RunLog log) Start(Source source)
        {
            var runner = new Runner(source);
            var log = new RunLog();
            runner.AddObserver(log);
            return (runner, log);
        }

        private static IEnumerable<(double, LogKind, string)> LaneEntries(RunLog log) =>
            log.Entries.Where(e => e.PassengerId == -1).Select(e => (e.Time, e.Kind, e.Station));

        [Test]
        public void Breakdown_PausesAndResumes()
        {
            // One lane, 100 s. A at 0, B at 10. Breaks at 30 (A has 70 s left), repaired after 50 s at 80.
            // A ends at 150 (the old end at 100 is ignored). B waits, 150–250.
            Station s1 = Desk("s1", 100.0);
            Router stage = Lanes("scan", s1);
            var (runner, log) = Start(new Source("entry", new FixedArrivals(0.0, 10.0), stage));
            runner.AddBreakdown(stage, 30.0, new Deterministic(50.0), new Pcg32(1UL, 1UL));
            runner.Run();

            var p = log.Passengers;
            Assert.That(p[0].ServiceEnd, Is.EqualTo(150.0));
            Assert.That(p[1].ServiceStart, Is.EqualTo(150.0));
            Assert.That(log.Entries.Count(e => e.Kind == LogKind.EndService && e.PassengerId == 0), Is.EqualTo(1));
            Assert.That(LaneEntries(log), Is.EqualTo(new[] { (30.0, LogKind.LaneBroken, "s1"), (80.0, LogKind.LaneRepaired, "s1") }));
            Assert.That(runner.LastActiveTime, Is.EqualTo(250.0));
            Assert.That(s1.IsBroken, Is.False);
            InvariantChecker.AssertAll(log, new Dictionary<string, int> { { "s1", 1 } });
        }

        [Test]
        public void Breakdown_NewPeopleAvoidTheBrokenLane()
        {
            // Two lanes, 100 s. A → s1, B → s2 at 0. A stream is picked so the breakdown at 10 hits s1
            // (A has 90 s left; repaired after 50 s at 60, A ends at 150). C at 20: both lanes hold 1 person,
            // a tie that would go to s1, but s1 is broken → C joins s2 behind B, 100–200.
            ulong id = 1;
            while (new Pcg32(9UL, id).NextUInt() % 2 != 0) id++;
            Station s1 = Desk("s1", 100.0), s2 = Desk("s2", 100.0);
            Router stage = Lanes("scan", s1, s2);
            var (runner, log) = Start(new Source("entry", new FixedArrivals(0.0, 0.0, 20.0), stage));
            runner.AddBreakdown(stage, 10.0, new Deterministic(50.0), new Pcg32(9UL, id));
            runner.Run();

            var p = log.Passengers;
            Assert.That(LaneEntries(log).First(), Is.EqualTo((10.0, LogKind.LaneBroken, "s1")));
            Assert.That(p[0].ServiceEnd, Is.EqualTo(150.0));
            Assert.That(p[2].Station, Is.EqualTo("s2"));
            Assert.That(p[2].ServiceStart, Is.EqualTo(100.0));
            InvariantChecker.AssertAll(log, new Dictionary<string, int> { { "s1", 1 }, { "s2", 1 } });
        }

        [Test]
        public void Breakdown_OnlyLane_BlocksUpstreamUntilRepaired()
        {
            // ID 10 s → one scanner lane, 100 s, its line holds 1. Three people at 0.
            // A: ID 0–10, scanner from 10. B: ID 10–20, joins the scanner line at 20.
            // Breaks at 20 (after B's events at 20): A has 90 s left. C: ID 20–30 → no working lane has room → BLOCKED.
            // Repaired after 100 s at 120: A ends at 210 → B starts → C released at 210, scanner 310–410.
            Station id = Desk("id", 10.0), s1 = Desk("s1", 100.0, lineCapacity: 1);
            Router stage = Lanes("scan", s1);
            id.ConnectTo(stage);
            var (runner, log) = Start(new Source("entry", new FixedArrivals(0.0, 0.0, 0.0), id));
            runner.AddBreakdown(stage, 20.0, new Deterministic(100.0), new Pcg32(1UL, 1UL));
            runner.Run();

            var p = log.Passengers;
            Assert.That(p[1].Visits[1].QueueJoin, Is.EqualTo(20.0));
            Assert.That(p[2].Visits[0].ServiceEnd, Is.EqualTo(30.0));
            Assert.That(p[2].Visits[0].Unblocked, Is.EqualTo(210.0));
            Assert.That(p.Select(r => r.Exit), Is.EqualTo(new[] { 210.0, 310.0, 410.0 }));
            InvariantChecker.AssertAll(log, new Dictionary<string, int> { { "id", 1 }, { "s1", 1 } },
                lineCapacity: new Dictionary<string, int> { { "s1", 1 } });
        }

        [Test]
        public void AddBreakdown_Rules()
        {
            Station s1 = Desk("s1", 1.0);
            Router stage = Lanes("scan", s1);
            var runner = new Runner(new Source("entry", new FixedArrivals(0.0), stage));
            var rng = new Pcg32(1UL, 1UL);
            Assert.Throws<System.ArgumentNullException>(() => runner.AddBreakdown(null, 1.0, new Deterministic(1.0), rng));
            Assert.Throws<System.ArgumentNullException>(() => runner.AddBreakdown(stage, 1.0, null, rng));
            Assert.Throws<System.ArgumentNullException>(() => runner.AddBreakdown(stage, 1.0, new Deterministic(1.0), null));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => runner.AddBreakdown(stage, -1.0, new Deterministic(1.0), rng));
            runner.Run();
            Assert.Throws<System.InvalidOperationException>(() => runner.AddBreakdown(stage, 5.0, new Deterministic(1.0), rng));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `bash scripts/run-tests.sh BreakdownTests`
Expected: compile errors (`AddBreakdown`, `IsBroken`, `LogKind.LaneBroken` don't exist).

- [ ] **Step 3: New event types and lane changes**

Replace the enum body in `Unity/Assets/Core/Engine/Events/EventType.cs` (keep its file comments) so it reads:

```csharp
    public enum EventType
    {
        EndService = 0,
        Renege = 1,
        Reach = 2,
        Arrive = 3,
        LaneReady = 4,
        Breakdown = 5,
        Repair = 6,
    }
```

Create `Unity/Assets/Core/Engine/Events/LaneChange.cs`:

```csharp
namespace Throughput.Core.Engine.Events
{
    /// <summary>What happened to a lane (K9).</summary>
    public enum LaneChange
    {
        Opened,
        Closed,

        /// <summary>The rule started opening it; it opens after the setup time.</summary>
        Opening,
        Broken,
        Repaired,

        /// <summary>The manager handed a pinned lane back to the rule.</summary>
        ReturnedToRule,
    }
}
```

Create `Unity/Assets/Core/Engine/Events/Breakdown.cs`:

```csharp
using System;
using System.Collections.Generic;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Randomness;

namespace Throughput.Core.Engine.Events
{
    /// <summary>One event-deck entry (K7): at Time, one lane of Stage breaks until a repair drawn from Repair.</summary>
    internal sealed class Breakdown
    {
        private readonly List<Station> _choices = new List<Station>();

        internal Breakdown(Router stage, double time, IDistribution repair, Pcg32 rng)
        {
            Stage = stage;
            Time = time;
            Repair = repair;
            Rng = rng;
        }

        internal Router Stage { get; }

        internal double Time { get; }

        internal IDistribution Repair { get; }

        internal Pcg32 Rng { get; }

        /// <summary>An open working lane, or (if every open lane is broken) any working lane; null if all are broken.</summary>
        internal Station PickLane()
        {
            Collect(lane => lane.IsOpen && !lane.IsBroken);
            if (_choices.Count == 0) Collect(lane => !lane.IsBroken);
            if (_choices.Count == 0) return null;
            return _choices[(int)(Rng.NextUInt() % (uint)_choices.Count)];
        }

        private void Collect(Func<Station, bool> allowed)
        {
            _choices.Clear();
            foreach (Station lane in Stage.Lanes)
                if (allowed(lane)) _choices.Add(lane);
        }
    }
}
```

- [ ] **Step 4: One observer call for every lane change**

In `Unity/Assets/Core/Engine/Events/IRunObserver.cs`, replace `OnLaneSwitched` and its summary with:

```csharp
        /// <summary>
        /// A lane changed (K9). byManager: the player did it (a replay re-applies these); otherwise the staffing
        /// rule or the event deck did.
        /// </summary>
        void OnLaneChanged(double time, Station lane, LaneChange change, bool byManager);
```

In `Unity/Assets/Core/Results/RunLog.cs`:

Add at the end of `LogKind`, after `LaneClosed`:

```csharp
        LaneOpening,
        LaneBroken,
        LaneRepaired,
        LaneReturnedToRule,
```

Replace the `LogEntry` constructor and `ToString`, and add `ByManager`:

```csharp
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
```

```csharp
        /// <summary>A lane entry caused by the player (K9).</summary>
        public bool ByManager { get; }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "{0:R} {1} p{2} {3} q={4} b={5}{6}",
                Time, Kind, PassengerId, Station ?? "-", QueueLength, Busy, ByManager ? " manager" : "");
```

Replace `OnLaneSwitched` with:

```csharp
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
```

- [ ] **Step 5: Pause and resume in the station**

In `Unity/Assets/Core/Engine/Network/Station.cs`:

Add fields after `_inLine`:

```csharp
        private readonly List<(Passenger Passenger, double EndsAt)> _serving = new List<(Passenger, double)>();
        private readonly List<(Passenger Passenger, double Left)> _paused = new List<(Passenger, double)>();
```

After the `IsOpen` property add:

```csharp
        /// <summary>Broken (K6): starts no checks and pauses the ones in progress until repaired.</summary>
        public bool IsBroken { get; private set; }
```

Replace `Finish` with:

```csharp
        /// <summary>An EndService event fired. Returns false (and does nothing) if a breakdown moved this end time.</summary>
        internal bool Finish(Runner runner, Passenger passenger, double time)
        {
            int i = _serving.FindIndex(s => s.Passenger == passenger && s.EndsAt == time);
            if (i < 0) return false;
            _serving.RemoveAt(i);
            if (Next != null && !Next.HasRoom)
            {
                runner.NotifyEndService(this, passenger);
                Next.Block(this, passenger);
                runner.NotifyBlocked(this, passenger);
                return true;
            }
            _busy--;
            runner.NotifyEndService(this, passenger);
            MoveOn(runner, passenger);
            TryStart(runner);
            return true;
        }

        /// <summary>The desk breaks (K6): checks in progress freeze with the time they have left.</summary>
        internal void Break(Runner runner)
        {
            IsBroken = true;
            foreach ((Passenger passenger, double endsAt) in _serving)
                _paused.Add((passenger, endsAt - runner.Now));
            _serving.Clear(); // their EndService events are now stale and will be ignored
        }

        /// <summary>The desk is fixed: paused checks continue with the time left, then the line moves again.</summary>
        internal void Repair(Runner runner)
        {
            IsBroken = false;
            foreach ((Passenger passenger, double left) in _paused)
                StartTimer(runner, passenger, runner.Now + left);
            _paused.Clear();
            TryStart(runner);
        }
```

Replace `TryStart` with:

```csharp
        private void TryStart(Runner runner)
        {
            while (!IsBroken && _busy < Staff && _line.Count > 0)
            {
                Passenger next = _line.First.Value;
                _line.RemoveFirst();
                _inLine.Remove(next);
                _busy++;
                runner.NotifyStartService(this, next);
                StartTimer(runner, next, runner.Now + _service.Sample(_rng));
            }
            Stage?.ReleaseBlocked(runner);
        }

        private void StartTimer(Runner runner, Passenger passenger, double endsAt)
        {
            _serving.Add((passenger, endsAt));
            runner.Schedule(endsAt, EventType.EndService, this, passenger);
        }
```

- [ ] **Step 6: The router skips broken lanes**

In `Unity/Assets/Core/Engine/Network/Router.cs`:

Replace the `HasRoom` loop condition `if (lane.IsOpen && lane.HasRoom) return true;` with `if (Works(lane) && lane.HasRoom) return true;`, and update its summary to `/// <summary>Some open, working lane can take one more walker.</summary>`.

Replace `PickWithRoom`, `PickAny` and `Pick` with:

```csharp
        /// <summary>For a walker from a desk upstream: an open working lane with room (they reserve a place there), or null.</summary>
        internal Station PickWithRoom() => Pick(lane => Works(lane) && lane.HasRoom);

        /// <summary>
        /// For someone from the entrance: any open working lane (lines in the hall have no limit, B4). If every open
        /// lane is broken they still need somewhere to stand, so they join an open broken lane (K6). Null if none is open.
        /// </summary>
        internal Station PickAny() => Pick(Works) ?? Pick(lane => lane.IsOpen);
```

```csharp
        private static bool Works(Station lane) => lane.IsOpen && !lane.IsBroken;

        private Station Pick(Func<Station, bool> allowed)
        {
            _choices.Clear();
            foreach (Station lane in _lanes)
                if (allowed(lane))
                    _choices.Add(lane);
            if (_choices.Count == 0) return null;
            int i = _policy.Choose(_choices);
            if (i < 0 || i >= _choices.Count)
                throw new InvalidOperationException($"The routing policy chose lane {i} of {_choices.Count} at '{Name}'.");
            return _choices[i];
        }
```

- [ ] **Step 7: Breakdowns in the runner**

In `Unity/Assets/Core/Engine/Events/Runner.cs`:

Add `using Throughput.Core.Engine.Randomness;` and a field after `_behaviours`:

```csharp
        private readonly List<Breakdown> _breakdowns = new List<Breakdown>();
```

After `AddBehaviour`, add:

```csharp
        /// <summary>
        /// Event deck (K7): at <paramref name="time"/> one open working lane of the stage breaks, picked with
        /// <paramref name="rng"/>, and is repaired after a time drawn from <paramref name="repair"/>. Add before the run starts.
        /// </summary>
        public void AddBreakdown(Router stage, double time, IDistribution repair, Pcg32 rng)
        {
            if (stage == null) throw new ArgumentNullException(nameof(stage));
            if (repair == null) throw new ArgumentNullException(nameof(repair));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (!(time >= 0) || double.IsInfinity(time))
                throw new ArgumentOutOfRangeException(nameof(time), time, "A breakdown needs a time of 0 or more.");
            if (_started) throw new InvalidOperationException("Add breakdowns before the run starts.");
            _breakdowns.Add(new Breakdown(stage, time, repair, rng));
        }
```

In `Run`, in the `if (!_started)` block, after the arrivals loop, add:

```csharp
                foreach (Breakdown breakdown in _breakdowns)
                    Schedule(breakdown.Time, EventType.Breakdown, breakdown, null);
```

Replace the bodies of `OpenLane` and `CloseLane`'s notification lines (`foreach (IRunObserver o in _observers) o.OnLaneSwitched(Now, lane, true);` and `... false);`) with `Notify(lane, LaneChange.Opened, byManager: true);` and `Notify(lane, LaneChange.Closed, byManager: true);`.

Add these private methods before `Handle`:

```csharp
        private void Notify(Station lane, LaneChange change, bool byManager)
        {
            foreach (IRunObserver o in _observers) o.OnLaneChanged(Now, lane, change, byManager);
        }
```

In `Handle`, replace the `EndService` case with:

```csharp
                case EventType.EndService:
                    return ((Station)e.Node).Finish(this, e.Passenger, e.Time);
```

and add before `default:`:

```csharp
                case EventType.Breakdown:
                    var breakdown = (Breakdown)e.Node;
                    Station broken = breakdown.PickLane();
                    if (broken == null) return false;
                    Notify(broken, LaneChange.Broken, byManager: false);
                    broken.Break(this);
                    Schedule(Now + breakdown.Repair.Sample(breakdown.Rng), EventType.Repair, broken, null);
                    return true;
                case EventType.Repair:
                    var repaired = (Station)e.Node;
                    Notify(repaired, LaneChange.Repaired, byManager: false);
                    repaired.Repair(this);
                    return true;
```

- [ ] **Step 8: Run all tests**

Run: `bash scripts/run-tests.sh`
Expected: all pass (157 + 4 = 161).

- [ ] **Step 9: Sabotage check**

Temporarily change `Station.Finish`'s lookup to `_serving.FindIndex(s => s.Passenger == passenger)` (ignore the time). Run `bash scripts/run-tests.sh BreakdownTests` and confirm `Breakdown_PausesAndResumes` fails. Undo it.

- [ ] **Step 10: Commit**

```bash
git add Unity/Assets/Core
git commit -m "Breakdowns: a lane stops, its check pauses and continues after the repair

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: The staffing rule and pinned lanes

**Files:**
- Create: `Unity/Assets/Core/Engine/Policies/StaffingRule.cs`
- Modify: `Runner.cs`, `Station.cs`, `Unity/Assets/Core/Tests/Engine/LaneTests.cs:203-208`, `Unity/Assets/Core/Tests/Engine/LaneStressTests.cs`
- Test: `Unity/Assets/Core/Tests/Engine/StaffingRuleTests.cs`

**Interfaces:**
- Consumes: `Runner.Notify`, `LaneChange`, `EventType.LaneReady`, `Station.IsBroken` (Task 1).
- Produces: `StaffingRule(Router stage, double openAbove, double closeBelow, double setupTime)` with `Stage`, `OpenAbove`, `CloseBelow`, `SetupTime`; `Runner.AddRule(StaffingRule)`, `Runner.ReturnToRule(Station)`; `Station.IsManual`, `Station.IsOpening`.

- [ ] **Step 1: Write the failing tests**

Create `Unity/Assets/Core/Tests/Engine/StaffingRuleTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Policies;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Results;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Engine
{
    /// <summary>The staffing rule (K1–K5) and the manager's pins. Fixed times, worked out by hand.</summary>
    public class StaffingRuleTests
    {
        private static Station Desk(string name) => new Station(name, 1, new Deterministic(100.0), new Pcg32(1UL, 1UL));

        private static (Runner runner, RunLog log, Station s1, Station s2, Station s3, Router stage) ThreeLanes(params double[] arrivals)
        {
            Station s1 = Desk("s1"), s2 = Desk("s2"), s3 = Desk("s3");
            var stage = new Router("scan", new[] { s1, s2, s3 }, ShortestLine.Instance);
            var runner = new Runner(new Source("entry", new FixedArrivals(arrivals), stage));
            var log = new RunLog();
            runner.AddObserver(log);
            return (runner, log, s1, s2, s3, stage);
        }

        private static IEnumerable<(double, LogKind, string)> LaneEntriesAfterStart(RunLog log) =>
            log.Entries.Where(e => e.PassengerId == -1 && e.Time > 0).Select(e => (e.Time, e.Kind, e.Station));

        private static readonly Dictionary<string, int> Staff = new Dictionary<string, int> { { "s1", 1 }, { "s2", 1 }, { "s3", 1 } };

        [Test]
        public void Rule_OpensAfterSetup_AndClosesWhenQuiet()
        {
            // Three lanes, 100 s; only s1 open (s2, s3 closed and handed to the rule). Rule: open above 2 waiting
            // per open lane, close below 0.5, setup 30 s. People at 0, 1, 2, 3, 4 all join s1.
            // At 3: B, C, D wait → 3 per lane > 2 → s2 starts opening, ready at 33. At 4: 4 waiting, but s2 is
            // already opening (one at a time). At 33 s2 opens: 4 / 2 = 2, not above 2. F at 40 → s2 (empty), 40–140.
            // s1 serves A 0–100, B –200, C –300, D –400, E –500. At 400 nobody waits: 0 < 0.5 → close s2.
            var (runner, log, s1, s2, s3, stage) = ThreeLanes(0.0, 1.0, 2.0, 3.0, 4.0, 40.0);
            runner.CloseLane(s2);
            runner.ReturnToRule(s2);
            runner.CloseLane(s3);
            runner.ReturnToRule(s3);
            runner.AddRule(new StaffingRule(stage, openAbove: 2.0, closeBelow: 0.5, setupTime: 30.0));
            runner.Run();

            Assert.That(LaneEntriesAfterStart(log), Is.EqualTo(new[]
            {
                (3.0, LogKind.LaneOpening, "s2"), (33.0, LogKind.LaneOpened, "s2"), (400.0, LogKind.LaneClosed, "s2"),
            }));
            Assert.That(log.Entries.Where(e => e.Time > 0 && e.PassengerId == -1).All(e => !e.ByManager), Is.True);
            Assert.That(log.Passengers[5].Station, Is.EqualTo("s2"));
            Assert.That(log.Passengers[5].ServiceStart, Is.EqualTo(40.0));
            Assert.That(log.Passengers[4].Station, Is.EqualTo("s1"));
            InvariantChecker.AssertAll(log, Staff);
        }

        [Test]
        public void Rule_ManagerCloseCancelsSetup_AndPins()
        {
            // As above, but at 3.5 (s2 is in setup) the manager closes s2: setup cancelled and s2 pinned closed.
            // The rule checks at once: 3 waiting per lane > 2 → it opens s3 instead, ready at 33.5.
            // The old LaneReady for s2 at 33 is ignored. F at 40 → s3. At 400 the rule closes s3 (s2 is pinned).
            var (runner, log, s1, s2, s3, stage) = ThreeLanes(0.0, 1.0, 2.0, 3.0, 4.0, 40.0);
            runner.CloseLane(s2);
            runner.ReturnToRule(s2);
            runner.CloseLane(s3);
            runner.ReturnToRule(s3);
            runner.AddRule(new StaffingRule(stage, 2.0, 0.5, 30.0));
            runner.Run(3.5);
            runner.CloseLane(s2);
            runner.Run();

            Assert.That(LaneEntriesAfterStart(log), Is.EqualTo(new[]
            {
                (3.0, LogKind.LaneOpening, "s2"), (3.5, LogKind.LaneClosed, "s2"), (3.5, LogKind.LaneOpening, "s3"),
                (33.5, LogKind.LaneOpened, "s3"), (400.0, LogKind.LaneClosed, "s3"),
            }));
            Assert.That(log.Entries.Single(e => e.Time == 3.5 && e.Kind == LogKind.LaneClosed).ByManager, Is.True);
            Assert.That(s2.IsManual, Is.True);
            Assert.That(s2.IsOpening, Is.False);
            Assert.That(log.Passengers[5].Station, Is.EqualTo("s3"));
            InvariantChecker.AssertAll(log, Staff);
        }

        [Test]
        public void Rule_LeavesPinnedLanes_UntilReturned()
        {
            // Two lanes, both pinned open by the manager. Nobody waits, so the rule would close one, but both are pinned.
            // At 10 the manager returns s2 to the rule → the rule closes it at once.
            Station s1 = Desk("s1"), s2 = Desk("s2");
            var stage = new Router("scan", new[] { s1, s2 }, ShortestLine.Instance);
            var runner = new Runner(new Source("entry", new FixedArrivals(0.0), stage));
            var log = new RunLog();
            runner.AddObserver(log);
            runner.OpenLane(s1);
            runner.OpenLane(s2);
            runner.AddRule(new StaffingRule(stage, 5.0, 0.5, 0.0));
            runner.Run(10.0);
            Assert.That(s2.IsOpen, Is.True);
            runner.ReturnToRule(s2);
            runner.Run();

            Assert.That(log.Entries.Where(e => e.PassengerId == -1).Select(e => (e.Time, e.Kind, e.Station, e.ByManager)), Is.EqualTo(new[]
            {
                (0.0, LogKind.LaneOpened, "s1", true), (0.0, LogKind.LaneOpened, "s2", true),
                (10.0, LogKind.LaneReturnedToRule, "s2", true), (10.0, LogKind.LaneClosed, "s2", false),
            }));
            Assert.That(s1.IsManual && !s2.IsManual, Is.True);
        }

        [Test]
        public void StaffingRule_RejectsBadNumbers()
        {
            var stage = new Router("scan", new[] { Desk("s1") }, ShortestLine.Instance);
            Assert.Throws<System.ArgumentNullException>(() => new StaffingRule(null, 2.0, 1.0, 0.0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new StaffingRule(stage, 1.0, 1.0, 0.0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new StaffingRule(stage, 2.0, -1.0, 0.0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new StaffingRule(stage, double.NaN, 1.0, 0.0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new StaffingRule(stage, double.PositiveInfinity, 1.0, 0.0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new StaffingRule(stage, 2.0, 1.0, -1.0));
        }
    }
}
```

In `Unity/Assets/Core/Tests/Engine/LaneTests.cs`, in `CloseLane_Rules`, change

```csharp
            runner.OpenLane(s1);  // already open: nothing happens
```
to
```csharp
            runner.OpenLane(s1);  // already open: now pinned by the manager (K2), which is logged
```
and `Is.EqualTo(1)` on the next-but-one line to `Is.EqualTo(2)`.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `bash scripts/run-tests.sh "StaffingRuleTests|LaneTests"`
Expected: compile errors (`StaffingRule`, `ReturnToRule`, `IsManual` don't exist).

- [ ] **Step 3: Pin and setup state on the station**

In `Unity/Assets/Core/Engine/Network/Station.cs`, after `IsBroken` add:

```csharp
        /// <summary>The manager pinned this lane (K2): the staffing rule leaves it alone until it's returned.</summary>
        public bool IsManual { get; internal set; }

        /// <summary>The rule is opening this lane; it opens at ReadyAt (K4).</summary>
        public bool IsOpening => !double.IsNaN(ReadyAt);

        internal double ReadyAt { get; set; } = double.NaN;
```

- [ ] **Step 4: Write the rule**

Create `Unity/Assets/Core/Engine/Policies/StaffingRule.cs`:

```csharp
using System;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;

namespace Throughput.Core.Engine.Policies
{
    /// <summary>
    /// Opens a lane when more than OpenAbove people wait per open lane, closes one when fewer than CloseBelow wait
    /// (K3). It opens the lowest-numbered closed lane after SetupTime, one at a time, and closes the
    /// highest-numbered open lane at once (it drains), never the last. Lanes the manager pinned are left alone (K2, K4).
    /// </summary>
    public sealed class StaffingRule
    {
        public StaffingRule(Router stage, double openAbove, double closeBelow, double setupTime)
        {
            Stage = stage ?? throw new ArgumentNullException(nameof(stage));
            if (!(closeBelow >= 0) || double.IsInfinity(closeBelow))
                throw new ArgumentOutOfRangeException(nameof(closeBelow), closeBelow, "Close below must be 0 or more.");
            if (!(openAbove > closeBelow) || double.IsInfinity(openAbove))
                throw new ArgumentOutOfRangeException(nameof(openAbove), openAbove, "Open above must be more than close below.");
            if (!(setupTime >= 0) || double.IsInfinity(setupTime))
                throw new ArgumentOutOfRangeException(nameof(setupTime), setupTime, "Setup time must be 0 or more.");
            OpenAbove = openAbove;
            CloseBelow = closeBelow;
            SetupTime = setupTime;
        }

        public Router Stage { get; }

        public double OpenAbove { get; }

        public double CloseBelow { get; }

        public double SetupTime { get; }

        internal void Check(Runner runner)
        {
            int open = 0, waiting = 0;
            bool opening = false;
            foreach (Station lane in Stage.Lanes)
            {
                if (lane.IsOpen)
                {
                    open++;
                    waiting += lane.QueueLength;
                }
                if (lane.IsOpening) opening = true;
            }
            if (open == 0) return;
            double perLane = (double)waiting / open;

            if (perLane > OpenAbove)
            {
                if (opening) return;
                foreach (Station lane in Stage.Lanes)
                    if (!lane.IsOpen && !lane.IsManual && !lane.IsBroken)
                    {
                        runner.StartOpening(lane, SetupTime);
                        return;
                    }
            }
            else if (perLane < CloseBelow && open > 1)
            {
                for (int i = Stage.Lanes.Count - 1; i >= 0; i--)
                {
                    Station lane = Stage.Lanes[i];
                    if (lane.IsOpen && !lane.IsManual)
                    {
                        runner.CloseByRule(lane);
                        return;
                    }
                }
            }
        }
    }
}
```

- [ ] **Step 5: Rules, pins and `ReturnToRule` in the runner**

In `Unity/Assets/Core/Engine/Events/Runner.cs`:

Add `using Throughput.Core.Engine.Policies;` and a field after `_breakdowns`:

```csharp
        private readonly List<StaffingRule> _rules = new List<StaffingRule>();
```

After `AddBreakdown`, add:

```csharp
        public void AddRule(StaffingRule rule)
        {
            _rules.Add(rule ?? throw new ArgumentNullException(nameof(rule)));
        }
```

Replace `OpenLane` and `CloseLane` (with their summaries) by:

```csharp
        /// <summary>
        /// Manager action (N6, N7, K2): open the lane now and pin it, so the staffing rule leaves it alone. Opening a
        /// lane the rule had opened just pins it. Call it after Run(t), so it sees every event at or before t.
        /// </summary>
        public void OpenLane(Station lane)
        {
            if (lane == null) throw new ArgumentNullException(nameof(lane));
            if (lane.IsOpen && lane.IsManual) return;
            lane.IsManual = true;
            if (lane.IsOpen) Notify(lane, LaneChange.Opened, byManager: true);
            else OpenNow(lane, byManager: true);
            CheckRules();
        }

        /// <summary>
        /// Manager action (N5, N7, K2): the lane gets nobody new but serves everyone already in its line, at its desk,
        /// or walking to it (it drains). It is pinned closed, and a setup in progress is cancelled. The last open
        /// lane of a stage can't be closed.
        /// </summary>
        public void CloseLane(Station lane)
        {
            if (lane == null) throw new ArgumentNullException(nameof(lane));
            if (!lane.IsOpen && lane.IsManual) return;
            if (lane.IsOpen && lane.Stage != null && lane.Stage.OpenLanes == 1)
                throw new InvalidOperationException($"'{lane.Name}' is the last open lane of '{lane.Stage.Name}'.");
            lane.IsManual = true;
            lane.ReadyAt = double.NaN; // a setup in progress is cancelled; its LaneReady event will be ignored
            lane.SetOpen(false);
            Notify(lane, LaneChange.Closed, byManager: true);
            CheckRules();
        }

        /// <summary>Manager action (K2): hand a pinned lane back to the staffing rule, which checks at once.</summary>
        public void ReturnToRule(Station lane)
        {
            if (lane == null) throw new ArgumentNullException(nameof(lane));
            if (!lane.IsManual) return;
            lane.IsManual = false;
            Notify(lane, LaneChange.ReturnedToRule, byManager: true);
            CheckRules();
        }

        internal void StartOpening(Station lane, double setupTime)
        {
            lane.ReadyAt = Now + setupTime;
            Notify(lane, LaneChange.Opening, byManager: false);
            Schedule(lane.ReadyAt, EventType.LaneReady, lane, null);
        }

        internal void CloseByRule(Station lane)
        {
            lane.SetOpen(false);
            Notify(lane, LaneChange.Closed, byManager: false);
        }
```

Add before `Notify`:

```csharp
        private void OpenNow(Station lane, bool byManager)
        {
            lane.ReadyAt = double.NaN;
            lane.SetOpen(true);
            Notify(lane, LaneChange.Opened, byManager);
            lane.Stage?.ReleaseBlocked(this);
        }

        private void CheckRules()
        {
            foreach (StaffingRule rule in _rules) rule.Check(this);
        }
```

In `Run`, replace

```csharp
                if (Handle(e))
                    LastActiveTime = Now;
```
with
```csharp
                if (Handle(e))
                {
                    LastActiveTime = Now;
                    CheckRules();
                }
```

In `Handle`, add before `case EventType.Breakdown:`:

```csharp
                case EventType.LaneReady:
                    var ready = (Station)e.Node;
                    if (ready.ReadyAt != Now) return false; // the manager cancelled this setup
                    OpenNow(ready, byManager: false);
                    return true;
```

- [ ] **Step 6: Run the hand tests**

Run: `bash scripts/run-tests.sh "StaffingRuleTests|LaneTests|BreakdownTests"`
Expected: all pass (4 + 10 + 4 = 18).

- [ ] **Step 7: Add the stress test**

Replace the contents of `Unity/Assets/Core/Tests/Engine/LaneStressTests.cs` with:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Throughput.Core.Engine.Behaviours;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Policies;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Results;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Engine
{
    /// <summary>Random runs with lanes, full lines, giving up, rules, breakdowns and random actions: every invariant must hold.</summary>
    public class LaneStressTests
    {
        private sealed class Setup
        {
            public Runner Runner;
            public RunLog Log;
            public Router Stage;
            public Station[] Lanes;
            public RngStreams Streams;
        }

        private static Setup Build(int seed)
        {
            var streams = new RngStreams((ulong)seed);
            var id = new Station("id", 2, Distribution.FromMeanCv(1.0, 1.0), streams.Get("service:id"));
            Station[] lanes = Enumerable.Range(1, 3)
                .Select(n => new Station($"scan {n}", 1, Distribution.FromMeanCv(1.4, 1.0), streams.Get($"service:scan {n}"), 3))
                .ToArray();
            var stage = new Router("scan", lanes, ShortestLine.Instance);
            id.ConnectTo(stage, walkTime: 0.2);
            var source = new Source("entry", new RenewalArrivals(Distribution.FromMeanCv(0.6, 1.0), streams.Get("arrivals:entry")),
                id, stopTime: 200.0);
            var runner = new Runner(source);
            runner.AddBehaviour(new Reneging(Distribution.FromMeanCv(15.0, 1.0), streams));
            var log = new RunLog();
            runner.AddObserver(log);
            return new Setup { Runner = runner, Log = log, Stage = stage, Lanes = lanes, Streams = streams };
        }

        private static void AssertInvariants(Setup s)
        {
            Assert.That(s.Runner.IsFinished, Is.True);
            var staff = new Dictionary<string, int> { { "id", 2 } };
            foreach (Station lane in s.Lanes) staff[lane.Name] = 1;
            InvariantChecker.AssertAll(s.Log, staff, lineCapacity: s.Lanes.ToDictionary(l => l.Name, l => 3));
        }

        [Test]
        public void RandomActions_KeepEveryInvariant([Range(1, 20)] int seed)
        {
            Setup s = Build(seed);
            Pcg32 manager = s.Streams.Get("test:manager");
            for (double t = 5.0; t < 200.0; t += 5.0)
            {
                s.Runner.Run(t);
                Station lane = s.Lanes[manager.NextUInt() % 3];
                if (lane.IsOpen && lane.Stage.OpenLanes > 1) s.Runner.CloseLane(lane);
                else s.Runner.OpenLane(lane);
            }
            s.Runner.Run();

            Assert.That(s.Log.Entries.Count(e => e.Kind == LogKind.LaneClosed), Is.GreaterThan(0));
            AssertInvariants(s);
        }

        [Test]
        public void RuleBreakdownsAndActions_KeepEveryInvariant([Range(1, 20)] int seed)
        {
            Setup s = Build(seed);
            s.Runner.AddRule(new StaffingRule(s.Stage, openAbove: 2.0, closeBelow: 0.5, setupTime: 1.0));
            s.Runner.AddBreakdown(s.Stage, 30.0 + seed, Distribution.FromMeanCv(8.0, 0.5), s.Streams.Get("breakdown:0"));
            s.Runner.AddBreakdown(s.Stage, 120.0, Distribution.FromMeanCv(8.0, 0.5), s.Streams.Get("breakdown:1"));
            Pcg32 manager = s.Streams.Get("test:manager");
            for (double t = 7.0; t < 200.0; t += 7.0)
            {
                s.Runner.Run(t);
                Station lane = s.Lanes[manager.NextUInt() % 3];
                switch (manager.NextUInt() % 3)
                {
                    case 0: s.Runner.OpenLane(lane); break;
                    case 1: if (!lane.IsOpen || lane.Stage.OpenLanes > 1) s.Runner.CloseLane(lane); break;
                    default: s.Runner.ReturnToRule(lane); break;
                }
            }
            s.Runner.Run();

            Assert.That(s.Log.Entries.Count(e => e.Kind == LogKind.LaneBroken), Is.EqualTo(2));
            Assert.That(s.Log.Entries.Count(e => e.Kind == LogKind.LaneRepaired), Is.EqualTo(2));
            Assert.That(s.Log.Entries.Any(e => e.PassengerId == -1 && !e.ByManager && e.Kind == LogKind.LaneClosed), Is.True, "the rule acted");
            AssertInvariants(s);
        }
    }
}
```

- [ ] **Step 8: Run all tests**

Run: `bash scripts/run-tests.sh`
Expected: all pass (161 + 4 + 20 = 185).

- [ ] **Step 9: Sabotage check**

Temporarily delete the `if (opening) return;` line in `StaffingRule.Check`. Run `bash scripts/run-tests.sh StaffingRuleTests` and confirm `Rule_OpensAfterSetup_AndClosesWhenQuiet` fails (s3 would also open at 4). Undo it.

- [ ] **Step 10: Commit**

```bash
git add Unity/Assets/Core
git commit -m "Staffing rule opens and closes lanes by itself; the manager can pin lanes

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Rules and breakdowns in the level, plan and compiler

**Files:**
- Modify: `Unity/Assets/Core/Levels/LevelDef.cs`, `Unity/Assets/Core/Layout/LayoutCompiler.cs`
- Test: `Unity/Assets/Core/Tests/Layout/LayoutCompilerTests.cs`

**Interfaces:**
- Consumes: `StaffingRule`, `Runner.AddRule`, `Runner.AddBreakdown` (Tasks 1–2).
- Produces: `PlannedStage.Rule : RuleDef { OpenAbove, CloseBelow, Setup }`; `LevelDef.Breakdowns : List<BreakdownDef { Stage, From, To, Repair }>`.

- [ ] **Step 1: Write the failing tests**

Add to `LayoutCompilerTests` (it already has `using System;`, `using System.Collections.Generic;`; add `using Throughput.Core.Engine.Events;` if missing — not needed for these tests):

```csharp
        private static LevelDef LevelWithBreakdown()
        {
            LevelDef level = Level;
            level.Breakdowns.Add(new BreakdownDef { Stage = "scanner", From = 20, To = 40, Repair = new DistributionDef { Mean = 5, Cv = 0.5 } });
            return level;
        }

        private static PlanDef LanesPlanWithRule()
        {
            PlanDef plan = LevelJson.ReadPlan(SampleLevels.LanesPlan);
            plan.Stages[1].Rule = new RuleDef { OpenAbove = 3, CloseBelow = 0.5, Setup = 2 };
            return plan;
        }

        [Test]
        public void Json_ReadsRuleAndBreakdowns()
        {
            LevelDef level = LevelJson.ReadLevel(@"{ ""name"": ""x"", ""waves"": [], ""stages"": [], ""space"": 0,
                ""breakdowns"": [ { ""stage"": ""scanner"", ""from"": 20, ""to"": 40, ""repair"": { ""mean"": 5 } } ] }");
            PlanDef plan = LevelJson.ReadPlan(@"{ ""stages"": [ { ""type"": ""scanner"", ""staff"": 1, ""lanes"": 2,
                ""rule"": { ""openAbove"": 3, ""closeBelow"": 1, ""setup"": 2 } } ] }");

            Assert.That((level.Breakdowns[0].Stage, level.Breakdowns[0].From, level.Breakdowns[0].To, level.Breakdowns[0].Repair.Mean),
                Is.EqualTo(("scanner", 20.0, 40.0, 5.0)));
            Assert.That((plan.Stages[0].Rule.OpenAbove, plan.Stages[0].Rule.CloseBelow, plan.Stages[0].Rule.Setup), Is.EqualTo((3.0, 1.0, 2.0)));
        }

        [Test]
        public void Compile_RuleAndBreakdown_RunsToTheEnd()
        {
            CompiledRun run = LayoutCompiler.Compile(LevelWithBreakdown(), LanesPlanWithRule(), runSeed: 7UL);
            var log = new RunLog();
            run.Runner.AddObserver(log);
            run.Runner.Run();

            Assert.That(run.Runner.IsFinished, Is.True);
            LogEntry broken = log.Entries.Single(e => e.Kind == LogKind.LaneBroken);
            Assert.That(broken.Time, Is.InRange(20.0, 40.0));
            Assert.That(broken.Station, Does.StartWith("scanner "));
            Assert.That(log.Entries.Single(e => e.Kind == LogKind.LaneRepaired).Time, Is.GreaterThan(broken.Time));
            Assert.That(log.Entries.Any(e => e.PassengerId == -1 && !e.ByManager && e.Kind == LogKind.LaneClosed), Is.True, "the rule acted");
            InvariantChecker.AssertAll(log, run.StaffByStation, lineCapacity: run.LineCapacity, closedAtStart: run.ClosedAtStart);
        }

        [Test]
        public void Breakdown_RandomTimeEachAttempt_SameForSameSeed()
        {
            double BrokeAt(ulong seed)
            {
                CompiledRun run = LayoutCompiler.Compile(LevelWithBreakdown(), LevelJson.ReadPlan(SampleLevels.LanesPlan), seed);
                var log = new RunLog();
                run.Runner.AddObserver(log);
                run.Runner.Run();
                return log.Entries.Single(e => e.Kind == LogKind.LaneBroken).Time;
            }

            Assert.That(BrokeAt(3UL), Is.EqualTo(BrokeAt(3UL)));
            Assert.That(new ulong[] { 1, 2, 3, 4, 5 }.Select(BrokeAt).Distinct().Count(), Is.GreaterThan(1));
        }

        [Test]
        public void Breakdown_OfAStageThePlanDoesntUse_IsSkipped()
        {
            LevelDef level = Level;
            level.Stages.Add(new StageTypeDef { Name = "xray", Service = new DistributionDef { Mean = 1 }, MaxStaff = 1 });
            level.Breakdowns.Add(new BreakdownDef { Stage = "xray", From = 5, To = 10, Repair = new DistributionDef { Mean = 5 } });
            CompiledRun run = LayoutCompiler.Compile(level, LevelJson.ReadPlan(SampleLevels.Plan), 1UL);
            var log = new RunLog();
            run.Runner.AddObserver(log);
            run.Runner.Run();
            Assert.That(log.Entries.Any(e => e.Kind == LogKind.LaneBroken), Is.False);
        }

        [Test]
        public void Check_RuleAndBreakdownProblems()
        {
            LevelDef level = Level;
            level.Breakdowns.Add(new BreakdownDef { Stage = "xray", From = 30, To = 10, Repair = new DistributionDef { Mean = 5 } });
            level.Breakdowns.Add(null);
            PlanDef plan = LevelJson.ReadPlan(SampleLevels.Plan); // shared lines
            plan.Stages[1].Rule = new RuleDef { OpenAbove = 1, CloseBelow = 2, Setup = -1 };

            string all = string.Join(" | ", LayoutCompiler.Check(level, plan));
            StringAssert.Contains("Breakdown 1: this level has no desk type 'xray'", all);
            StringAssert.Contains("Breakdown 1 must happen between", all);
            StringAssert.Contains("Breakdown 2 is empty", all);
            StringAssert.Contains("a staffing rule needs lanes", all);
            StringAssert.Contains("open above", all);
            StringAssert.Contains("setup time must be 0 or more", all);
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `bash scripts/run-tests.sh LayoutCompilerTests`
Expected: compile errors (`BreakdownDef`, `RuleDef`, `Breakdowns`, `Rule` don't exist).

- [ ] **Step 3: Level and plan data**

In `Unity/Assets/Core/Levels/LevelDef.cs`, in `LevelDef` after `Patience`, add:

```csharp
        /// <summary>The event deck (K7, D12): the same breakdowns every attempt, at random times.</summary>
        public List<BreakdownDef> Breakdowns { get; set; } = new List<BreakdownDef>();
```

After `StageTypeDef`, add:

```csharp
    /// <summary>One desk of this type breaks at a random time between From and To (minutes) and is repaired after Repair.</summary>
    public sealed class BreakdownDef
    {
        public string Stage { get; set; }

        public double From { get; set; }

        public double To { get; set; }

        public DistributionDef Repair { get; set; }
    }
```

In `PlannedStage`, after `WalkToNext`, add:

```csharp
        /// <summary>Staffing rule for a stage with lanes (K1–K5); null = lanes change only by the manager.</summary>
        public RuleDef Rule { get; set; }
```

After `PlannedStage`, add:

```csharp
    /// <summary>Open a lane above OpenAbove people waiting per open lane, close one below CloseBelow; Setup in minutes (K3, K4).</summary>
    public sealed class RuleDef
    {
        public double OpenAbove { get; set; }

        public double CloseBelow { get; set; }

        public double Setup { get; set; }
    }
```

- [ ] **Step 4: Build rules and breakdowns**

In `Unity/Assets/Core/Layout/LayoutCompiler.cs`, in `Compile`:

Before `var stages = new List<Router>();` add `var rules = new List<StaffingRule>();`. After `var stage = new Router(type.Name, lanes, ShortestLine.Instance);` add:

```csharp
                if (ps.Rule != null)
                    rules.Add(new StaffingRule(stage, ps.Rule.OpenAbove, ps.Rule.CloseBelow, ps.Rule.Setup));
```

After the `if (level.Patience != null) ...` lines, add:

```csharp
            foreach (StaffingRule rule in rules)
                runner.AddRule(rule);
            List<BreakdownDef> breakdowns = level.Breakdowns ?? new List<BreakdownDef>();
            for (int i = 0; i < breakdowns.Count; i++)
            {
                BreakdownDef b = breakdowns[i];
                Router target = stages.FirstOrDefault(s => s.Name == b.Stage);
                if (target == null) continue; // the plan has no such desk: nothing to break (K7)
                Pcg32 rng = streams.Get("breakdown:" + i);
                runner.AddBreakdown(target, b.From + rng.NextDouble() * (b.To - b.From), b.Repair.Build(), rng);
            }
```

- [ ] **Step 5: Check them**

In `CheckLevel`, before the `if (level.Space < 0)` line, add:

```csharp
            List<BreakdownDef> breakdowns = level.Breakdowns ?? new List<BreakdownDef>();
            for (int i = 0; i < breakdowns.Count; i++)
            {
                BreakdownDef b = breakdowns[i];
                string what = $"Breakdown {i + 1}";
                if (b == null) { problems.Add($"{what} is empty."); continue; }
                if (string.IsNullOrEmpty(b.Stage) || !(level.Stages ?? new List<StageTypeDef>()).Any(t => t != null && t.Name == b.Stage))
                    problems.Add($"{what}: this level has no desk type '{b.Stage}'.");
                if (!(b.From >= 0) || !(b.To >= b.From) || double.IsInfinity(b.To))
                    problems.Add($"{what} must happen between two times, 0 or later, in order ({b.From}–{b.To}).");
                CheckDistribution(b.Repair, $"{what} repair time", problems);
            }
```

In `CheckPlan`, after the walk-time check (the last check in the loop), add:

```csharp
                if (ps.Rule != null)
                {
                    if (ps.Lanes == null)
                        problems.Add($"Stage {i + 1} {name}: a staffing rule needs lanes (it opens and closes them).");
                    if (!(ps.Rule.CloseBelow >= 0) || !(ps.Rule.OpenAbove > ps.Rule.CloseBelow) || double.IsInfinity(ps.Rule.OpenAbove))
                        problems.Add($"Stage {i + 1} {name}: the rule must open above a bigger number than it closes below, both 0 or more ({ps.Rule.OpenAbove} / {ps.Rule.CloseBelow}).");
                    if (!(ps.Rule.Setup >= 0) || double.IsInfinity(ps.Rule.Setup))
                        problems.Add($"Stage {i + 1} {name}: setup time must be 0 or more ({ps.Rule.Setup}).");
                }
```

- [ ] **Step 6: Run all tests and the strict tier**

Run: `bash scripts/run-tests.sh` then `bash scripts/run-tests.sh --strict`
Expected: all pass (185 + 5 = 190); strict 19/19 with the same numbers as before.

- [ ] **Step 7: Commit**

```bash
git add Unity/Assets/Core
git commit -m "Levels list breakdowns, plans set staffing rules; the compiler builds and checks both

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Docs

**Files:**
- Modify: `docs/superpowers/specs/2026-10-11-phase-1-core-design.md`, `docs/architecture/simulation-core.md`, `docs/roadmap.md`

- [ ] **Step 1: Update the spec**

- Tick the S8b items (staffing rule, breakdown, hand tests) and add `- [x] Manager pins and "return to rule" (K2)`.
- Progress table: `| S8b | Staffing rule, breakdown | Done (2026-10-12) |`.
- Add `**S8b decisions (2026-10-12, plan part 8):**` with K1–K10, one line each, after the S8a decisions.
- Add to the review notes: `- **Deferred idea (S8b, K10):** a manager action that sends a broken lane's line to the other lanes.`
- Append to the change log: `| 2026-10-12 | S8b done: staffing rule with setup time, manager pins and return-to-rule, breakdowns from the event deck (pause and resume) |`.

- [ ] **Step 2: Update the architecture and roadmap**

- `docs/architecture/simulation-core.md`: after the Router paragraph, add: the `StaffingRule` is checked after every event and manager action, opens lanes after a setup time (`LaneReady` event) and closes at once; manager actions pin lanes until `ReturnToRule`; breakdowns from the event deck pause a lane's checks (stale `EndService` events are ignored) and the router skips broken lanes; every lane change is logged with who caused it.
- `docs/roadmap.md`: `S0–S8a done; next: S8b (staffing rule, breakdown)` → `S0–S8 done; next: S9 (results, fairness test)`.

- [ ] **Step 3: Commit**

```bash
git add docs
git commit -m "Close S8b: staffing rule, pinned lanes and breakdowns

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
