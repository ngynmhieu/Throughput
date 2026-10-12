# Phase 1 Part 7: Lanes, Routing and the First Manager Action (S8a) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A stage can be one shared line (as today) or several separate lanes, each with its own line and desk. A router picks the shortest lane for each passenger. The manager can open or close a lane during a run; a closed lane drains.

**Architecture:** A **lane is a `Station`** (one line + its desk), so lines, blocking, giving up and records are reused unchanged. A new **`Router`** sits at the entrance of every stage (a shared line is a stage with one lane). A station now feeds a `Router`, not a `Station`; a `Source` targets a `Router`. Blocked desks wait at the router and are released to whichever lane gets room. `Runner.OpenLane` / `CloseLane` are the manager action, called after `Run(t)`; the run log records them.

**Tech Stack:** Unity 6000.3.26f1, C#, NUnit (EditMode), Unity CLI via `scripts/run-tests.sh`.

**Spec:** [docs/superpowers/specs/2026-10-11-phase-1-core-design.md](../specs/2026-10-11-phase-1-core-design.md) (§6 S8, the S4/S6/S7 review notes on S8). Architecture: [simulation-core.md](../../architecture/simulation-core.md), [overview.md](../../architecture/overview.md) (A2).

## Global Constraints

- `Throughput.Core` never references `UnityEngine`/`UnityEditor`.
- Engine code (`Throughput.Core.Engine.*`) never depends on `Levels` or `Layout`.
- Determinism: one named random stream per source of randomness: `arrivals:entry`, `service:<station>`, `patience:<station>`. Lanes are stations, so each lane gets its own (`service:scanner 2`).
- A shared-line stage keeps its old station name (the desk type name), so old plans give exactly the same runs as before.
- Manager actions are applied **after** `Run(t)` (S6 review note).
- Private fields `_camelCase`.
- The Unity editor must be closed to run `scripts/run-tests.sh`.
- Git: commit locally after each task; push only when the user asks. Messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Design decisions (the user reviews these)

| # | Decision | Why |
|---|---|---|
| N1 | **A stage is either one shared line** (one `Station`, several desks) **or separate lanes** (several `Station`s, one desk each). The player picks in the plan (user's choice A). | A shared line is faster; lanes give control (express lanes later, closing one lane). A real queueing choice for the player (P1). |
| N2 | **A lane is a `Station`** (user's choice). A **`Router`** is the entrance of every stage; a shared line is a stage with one lane. | Reuses everything S4/S5 tested. One code path for both kinds of stage. |
| N3 | **Routing policy: shortest line** = fewest people waiting + walking there + at the desk. Ties go to the lowest lane number. It's the only policy in S8a (`IRoutingPolicy` lets later policies plug in). | What people do. Counting the desk means an idle desk beats a busy one with an empty line. Lowest number on ties keeps it deterministic with no random stream. |
| N4 | **The lane is chosen when the passenger leaves the previous desk** (user's choice A); arrivals from the entrance choose on arrival. Walkers reserve a place in the chosen lane. | Keeps the S5 blocking rules correct; "shortest" sees walkers, so two walkers don't rush the same empty lane. |
| N5 | **Closing a lane drains it** (user's choice A): the router sends nobody new; people in its line, at its desk, and already walking there are still served. **The last open lane of a stage can't be closed.** | Real checkpoints; calm (P4); the delay is the action's cost (P1). Nobody moves between lines, so fewer bugs. |
| N6 | **Opening a lane takes effect at once** and releases blocked desks upstream. A setup delay comes with the staffing rule in S8b. | YAGNI for S8a. |
| N7 | **Manager action = `Runner.OpenLane(lane)` / `Runner.CloseLane(lane)`**, called after `Run(t)`. It is recorded in the run log (`LaneOpened` / `LaneClosed`) at the engine's exact `Now`. Opening an open lane or closing a closed one does nothing. | S6 review note. The log is the recording, so a replay has the action times. |
| N8 | **Plan format:** a stage may set `lanes`. Then `staff` = lanes open at the start (the first ones; the rest start closed), `1 ≤ staff ≤ lanes ≤ maxStaff`, and `lineCapacity` is **per lane** (space counts every lane). Lanes are named `"<type> 1"`, `"<type> 2"`, … | One desk per lane, so a desk type's `maxStaff` is also its lane limit. Closed lanes are ready for the manager to open. |
| N9 | **Blocked desks wait at the stage's router**, first blocked first released, and go to whichever open lane has room. | With several lanes "the next line" is any lane; one shared waiting list keeps the S5 order rule. |
| N10 | **No line switching (jockeying)** in S8a: once in a line, a passenger stays. | Scope. Can come later as a behaviour. |

## Review Focus

1. **Closing a lane while people walk to it.** Expected: they still arrive and are served (they chose before the close). Pinned by the `AssertClosedLanesGetNoNewPeople` invariant (allows only choices made at or before the close) and the stress test.
2. **Every open lane full, with a desk blocked, then a lane opens or frees.** Expected: the blocked desk is released to the lane with room, in blocked order. Pinned by `Lanes_AllFull_BlockAtTheStage` and `OpenLane_ReleasesBlockedDesk`.
3. **An action at exactly the time of an event.** Expected: the action sees the state after that event. Pinned by `Action_AtAnEventTime_SeesThatEvent`.
4. **Closing the last open lane, or closing twice.** Expected: a clear error for the last lane; nothing happens on a repeat. Pinned by `CloseLane_Rules`.
5. **An action changes only what it affects.** Expected: before the action the runs are identical; arrival times and ID desk times are identical throughout. Pinned by `ManagerAction_ChangesOnlyWhatItAffects`.

---

## File Structure

| File | Change | Responsibility |
|---|---|---|
| `Unity/Assets/Core/Engine/Policies/IRoutingPolicy.cs` | Create | Picks a lane index from the candidate lanes |
| `Unity/Assets/Core/Engine/Policies/ShortestLine.cs` | Create | The shortest-line policy (N3) |
| `Unity/Assets/Core/Engine/Network/Router.cs` | Create | A stage's entrance: lanes, picking, blocked desks (N2, N9) |
| `Unity/Assets/Core/Engine/Network/Station.cs` | Modify | Feeds a `Router`; `Stage`, `IsOpen`, `WalkingHere`; blocked list moves to the router |
| `Unity/Assets/Core/Engine/Network/Source.cs` | Modify | Targets a `Router` |
| `Unity/Assets/Core/Engine/Events/Runner.cs` | Modify | Arrivals go through the router; `OpenLane` / `CloseLane` |
| `Unity/Assets/Core/Engine/Events/IRunObserver.cs` | Modify | `OnLaneSwitched` |
| `Unity/Assets/Core/Results/RunLog.cs` | Modify | `LaneOpened` / `LaneClosed` entries |
| `Unity/Assets/Core/Levels/LevelDef.cs` | Modify | `PlannedStage.Lanes` |
| `Unity/Assets/Core/Layout/LayoutCompiler.cs` | Modify | Builds lanes, checks lane rules, `CompiledRun.Stages` / `Lane(name)` |
| `Unity/Assets/Core/Tests/Engine/LaneTests.cs` | Create | Hand tests: routing, blocking, open/close, action timing |
| `Unity/Assets/Core/Tests/Engine/LaneStressTests.cs` | Create | Random runs with random open/close, all invariants |
| `Unity/Assets/Core/Tests/Support/InvariantChecker.cs` | Modify | Closed lanes get no new people |
| `Unity/Assets/Core/Tests/Support/SampleLevels.cs` | Modify | `LanesPlan` |
| `Unity/Assets/Core/Tests/Layout/LayoutCompilerTests.cs` | Modify | Lanes compile, check and action tests |

Unity creates the `.meta` files for new `.cs` files when the test run imports them; commit them with the code.

---

### Task 1: Router, shortest-line routing and lanes in the engine

**Files:**
- Create: `Unity/Assets/Core/Engine/Policies/IRoutingPolicy.cs`, `Unity/Assets/Core/Engine/Policies/ShortestLine.cs`, `Unity/Assets/Core/Engine/Network/Router.cs`
- Modify: `Unity/Assets/Core/Engine/Network/Station.cs`, `Unity/Assets/Core/Engine/Network/Source.cs`, `Unity/Assets/Core/Engine/Events/Runner.cs`, `Unity/Assets/Core/Tests/Layout/LayoutCompilerTests.cs:21,24`
- Test: `Unity/Assets/Core/Tests/Engine/LaneTests.cs`

**Interfaces:**
- Produces: `IRoutingPolicy.Choose(IReadOnlyList<Station> lanes) : int`; `ShortestLine.Instance`, `ShortestLine.PeopleAt(Station) : int`; `Router(string name, IReadOnlyList<Station> lanes, IRoutingPolicy policy)`, `Router.Name`, `Router.Lanes`, `Router.OpenLanes`, internal `Router.For(Station)` (tests reach it through `Station.ConnectTo(Station)` and `Source(..., Station)`), `HasRoom`, `PickWithRoom()`, `PickAny()`, `Block(...)`, `ReleaseBlocked(Runner)`; `Station.Stage : Router`, `Station.Next : Router` (was `Station`), `Station.IsOpen`, internal `Station.SetOpen(bool)`, `Station.WalkingHere`, `Station.ConnectTo(Router, double)`; `Source(string, IArrivalProcess, Router, double)` and `Source.Target : Router`.

- [ ] **Step 1: Write the failing tests**

Create `Unity/Assets/Core/Tests/Engine/LaneTests.cs`:

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
    /// <summary>Stages with separate lanes (N1–N10). Fixed times, worked out by hand.</summary>
    public class LaneTests
    {
        private static Station Desk(string name, double service, int staff = 1, int lineCapacity = int.MaxValue) =>
            new Station(name, staff, new Deterministic(service), new Pcg32(1UL, 1UL), lineCapacity);

        private static Router Lanes(string name, params Station[] lanes) => new Router(name, lanes, ShortestLine.Instance);

        private static (Runner runner, RunLog log) Start(Router first, params double[] arrivals) =>
            Start(new Source("entry", new FixedArrivals(arrivals), first));

        private static (Runner runner, RunLog log) Start(Station first, params double[] arrivals) =>
            Start(new Source("entry", new FixedArrivals(arrivals), first));

        private static (Runner runner, RunLog log) Start(Source source)
        {
            var runner = new Runner(source);
            var log = new RunLog();
            runner.AddObserver(log);
            return (runner, log);
        }

        private static Dictionary<string, int> Staff(params Station[] stations) => stations.ToDictionary(s => s.Name, s => s.Staff);

        [Test]
        public void ShortestLine_FromTheEntrance_TiesGoToTheFirstLane()
        {
            // Two lanes, 100 s each. A at 0 → s1 (tie 0–0). B at 10 → s2 (s1 has A at the desk).
            // C at 20 → tie 1–1 → s1, waits for A. Starts 0, 10, 100; exits 100, 110, 200.
            Station s1 = Desk("s1", 100.0), s2 = Desk("s2", 100.0);
            var (runner, log) = Start(Lanes("scan", s1, s2), 0.0, 10.0, 20.0);
            runner.Run();

            var p = log.Passengers;
            Assert.That(p.Select(r => r.Station), Is.EqualTo(new[] { "s1", "s2", "s1" }));
            Assert.That(p.Select(r => r.ServiceStart), Is.EqualTo(new[] { 0.0, 10.0, 100.0 }));
            Assert.That(p.Select(r => r.Exit), Is.EqualTo(new[] { 100.0, 110.0, 200.0 }));
            InvariantChecker.AssertAll(log, Staff(s1, s2));
        }

        [Test]
        public void ShortestLine_CountsPeopleWalkingThere()
        {
            // ID has 2 desks, 10 s. A and B arrive at 0, both finish at 10 and walk 30 s to two scanner lanes.
            // A picks s1 (tie). B must see A walking to s1 and pick s2.
            Station id = Desk("id", 10.0, staff: 2), s1 = Desk("s1", 100.0), s2 = Desk("s2", 100.0);
            id.ConnectTo(Lanes("scan", s1, s2), walkTime: 30.0);
            var (runner, log) = Start(id, 0.0, 0.0);
            runner.Run();

            var p = log.Passengers;
            Assert.That(p.Select(r => r.Visits[1].Station), Is.EqualTo(new[] { "s1", "s2" }));
            Assert.That(p.Select(r => r.Visits[1].ServiceStart), Is.EqualTo(new[] { 40.0, 40.0 }));
            InvariantChecker.AssertAll(log, Staff(id, s1, s2));
        }

        [Test]
        public void Lanes_AllFull_BlockAtTheStage()
        {
            // ID 10 s → two scanner lanes, 100 s, each line holds 1. Five people arrive at 0.
            // A: ID 0–10 → s1, served 10–110.  B: ID 10–20 → s2 (s1 busy), served 20–120.
            // C: ID 20–30 → tie 1–1 → s1's line.  D: ID 30–40 → s2's line (s1 now has 2 people).
            // E: ID 40–50 → both lines full → ID BLOCKED at the stage.
            // 110: s1 finishes A, starts C → s1's line has room → E released to s1 (joins at 110).
            // 120: s2 starts D. 210: s1 starts E. Exits 110, 120, 210, 220, 310.
            Station id = Desk("id", 10.0);
            Station s1 = Desk("s1", 100.0, lineCapacity: 1), s2 = Desk("s2", 100.0, lineCapacity: 1);
            id.ConnectTo(Lanes("scan", s1, s2));
            var (runner, log) = Start(id, 0.0, 0.0, 0.0, 0.0, 0.0);
            runner.Run();

            var p = log.Passengers;
            Assert.That(p.Select(r => r.Visits[1].Station), Is.EqualTo(new[] { "s1", "s2", "s1", "s2", "s1" }));
            Assert.That(p[4].Visits[0].Unblocked, Is.EqualTo(110.0));
            Assert.That(p[4].Visits[1].QueueJoin, Is.EqualTo(110.0));
            Assert.That(p.Select(r => r.Exit), Is.EqualTo(new[] { 110.0, 120.0, 210.0, 220.0, 310.0 }));
            InvariantChecker.AssertAll(log, Staff(id, s1, s2),
                lineCapacity: new Dictionary<string, int> { { "s1", 1 }, { "s2", 1 } });
        }

        [Test]
        public void Router_RejectsBadLanes()
        {
            Station a = Desk("a", 1.0), b = Desk("b", 1.0);
            Assert.Throws<System.ArgumentException>(() => Lanes("x"));
            Assert.Throws<System.ArgumentException>(() => Lanes("x", a, a));
            Assert.Throws<System.ArgumentNullException>(() => new Router("x", new[] { a }, null));
            Lanes("x", a);
            Assert.Throws<System.ArgumentException>(() => Lanes("y", a, b)); // a is already a lane of x
            Assert.That(b.Stage, Is.Null, "a failed router must not claim any lane");
        }

        [Test]
        public void SharedLine_IsAStageWithOneLane()
        {
            Station id = Desk("id", 1.0), scan = Desk("scan", 1.0);
            id.ConnectTo(scan);
            Assert.That(id.Next.Lanes, Is.EqualTo(new[] { scan }));
            Assert.That(scan.Stage, Is.SameAs(id.Next));
            new Source("entry", new FixedArrivals(0.0), scan);
            Assert.That(scan.Stage, Is.SameAs(id.Next), "a source feeding scan reuses its stage");
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `bash scripts/run-tests.sh LaneTests`
Expected: compile errors (`Router`, `ShortestLine`, `Throughput.Core.Engine.Policies` don't exist).

- [ ] **Step 3: Write the routing policy**

Create `Unity/Assets/Core/Engine/Policies/IRoutingPolicy.cs`:

```csharp
using System.Collections.Generic;
using Throughput.Core.Engine.Network;

namespace Throughput.Core.Engine.Policies
{
    /// <summary>Picks which lane a passenger joins (N3). It only ever sees open lanes (with room, for walkers).</summary>
    public interface IRoutingPolicy
    {
        /// <summary>Returns an index into <paramref name="lanes"/>, which is never empty.</summary>
        int Choose(IReadOnlyList<Station> lanes);
    }
}
```

Create `Unity/Assets/Core/Engine/Policies/ShortestLine.cs`:

```csharp
using System.Collections.Generic;
using Throughput.Core.Engine.Network;

namespace Throughput.Core.Engine.Policies
{
    /// <summary>The lane with the fewest people; ties go to the first lane (N3).</summary>
    public sealed class ShortestLine : IRoutingPolicy
    {
        public static readonly ShortestLine Instance = new ShortestLine();

        public int Choose(IReadOnlyList<Station> lanes)
        {
            int best = 0;
            for (int i = 1; i < lanes.Count; i++)
                if (PeopleAt(lanes[i]) < PeopleAt(lanes[best]))
                    best = i;
            return best;
        }

        /// <summary>Waiting in line + walking there + at the desk (being served or blocked).</summary>
        public static int PeopleAt(Station lane) => lane.QueueLength + lane.WalkingHere + lane.Busy;
    }
}
```

- [ ] **Step 4: Write the router**

Create `Unity/Assets/Core/Engine/Network/Router.cs`:

```csharp
using System;
using System.Collections.Generic;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Policies;

namespace Throughput.Core.Engine.Network
{
    /// <summary>
    /// The entrance of a stage (N2): picks one of its lanes for each passenger. A shared line is a stage
    /// with one lane. Desks upstream that can't send anyone (every open lane full) wait here and are
    /// released first blocked, first out, to whichever lane gets room (N9).
    /// </summary>
    public sealed class Router
    {
        private readonly Station[] _lanes;
        private readonly IRoutingPolicy _policy;
        private readonly LinkedList<(Station From, Passenger Passenger)> _blocked = new LinkedList<(Station, Passenger)>();
        private readonly List<Station> _choices = new List<Station>();

        public Router(string name, IReadOnlyList<Station> lanes, IRoutingPolicy policy)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A stage needs a name.", nameof(name));
            if (lanes == null || lanes.Count == 0) throw new ArgumentException("A stage needs at least one lane.", nameof(lanes));
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _lanes = new Station[lanes.Count];
            for (int i = 0; i < lanes.Count; i++)
            {
                Station lane = lanes[i] ?? throw new ArgumentException("Lanes can't be null.", nameof(lanes));
                if (Array.IndexOf(_lanes, lane, 0, i) >= 0)
                    throw new ArgumentException($"Station '{lane.Name}' is listed twice.", nameof(lanes));
                if (lane.Stage != null)
                    throw new ArgumentException($"Station '{lane.Name}' is already a lane of stage '{lane.Stage.Name}'.", nameof(lanes));
                _lanes[i] = lane;
            }
            foreach (Station lane in _lanes)
                lane.Stage = this; // only after every check passed, so a failed router claims nothing
            Name = name;
        }

        public string Name { get; }

        public IReadOnlyList<Station> Lanes => _lanes;

        public int OpenLanes
        {
            get
            {
                int open = 0;
                foreach (Station lane in _lanes)
                    if (lane.IsOpen) open++;
                return open;
            }
        }

        /// <summary>Some open lane can take one more walker.</summary>
        internal bool HasRoom
        {
            get
            {
                foreach (Station lane in _lanes)
                    if (lane.IsOpen && lane.HasRoom) return true;
                return false;
            }
        }

        /// <summary>The station's own stage, or a new one-lane stage for it (a shared line).</summary>
        internal static Router For(Station station) =>
            station.Stage ?? new Router(station.Name, new[] { station }, ShortestLine.Instance);

        /// <summary>For a walker from a desk upstream: an open lane with room (they reserve a place there), or null.</summary>
        internal Station PickWithRoom() => Pick(needRoom: true);

        /// <summary>For someone from the entrance: any open lane (lines in the hall have no limit, B4), or null.</summary>
        internal Station PickAny() => Pick(needRoom: false);

        internal void Block(Station from, Passenger passenger) => _blocked.AddLast((from, passenger));

        /// <summary>Room opened in a lane: blocked desks upstream move their passengers on, first blocked first.</summary>
        internal void ReleaseBlocked(Runner runner)
        {
            while (_blocked.Count > 0 && HasRoom)
            {
                (Station from, Passenger passenger) = _blocked.First.Value;
                _blocked.RemoveFirst();
                from.Unblock(runner, passenger);
            }
        }

        private Station Pick(bool needRoom)
        {
            _choices.Clear();
            foreach (Station lane in _lanes)
                if (lane.IsOpen && (!needRoom || lane.HasRoom))
                    _choices.Add(lane);
            if (_choices.Count == 0) return null;
            int i = _policy.Choose(_choices);
            if (i < 0 || i >= _choices.Count)
                throw new InvalidOperationException($"The routing policy chose lane {i} of {_choices.Count} at '{Name}'.");
            return _choices[i];
        }
    }
}
```

- [ ] **Step 5: Change `Station` to feed a router**

In `Unity/Assets/Core/Engine/Network/Station.cs`:

Replace the class summary with:

```csharp
    /// <summary>
    /// One line and Staff desks, each serving one passenger at a time: a shared line, or one lane of a
    /// stage (N2). People may leave the line early (reneging). After service a passenger walks to a lane
    /// of the Next stage, or exits if there is none. If every open lane there is full, the desk keeps the
    /// passenger and stops (blocking, D8) until a spot frees up.
    /// </summary>
```

Delete the field `_blockedUpstream`.

After the `LineCapacity` property, replace the `Next` property with:

```csharp
        /// <summary>The stage this station is a lane of (set by the Router); null until something feeds it.</summary>
        public Router Stage { get; internal set; }

        /// <summary>The stage passengers go to after this one; null means they exit.</summary>
        public Router Next { get; private set; }

        /// <summary>A closed lane gets nobody new but still serves its line (N5).</summary>
        public bool IsOpen { get; private set; } = true;
```

Replace `public int QueueLength => _line.Count;` and the `HasRoom` line with:

```csharp
        public int QueueLength => _line.Count;

        /// <summary>People who chose this line and are walking here.</summary>
        public int WalkingHere => _walkingHere;

        internal bool HasRoom => _line.Count + _walkingHere < LineCapacity;

        internal void SetOpen(bool open) => IsOpen = open;
```

Replace `ConnectTo` with:

```csharp
        /// <summary>Feed a single station (a shared line).</summary>
        public void ConnectTo(Station next, double walkTime = 0.0)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            if (next == this) throw new ArgumentException("A station can't feed itself.", nameof(next));
            ConnectTo(Router.For(next), walkTime);
        }

        /// <summary>Feed a stage; each passenger walks to the lane its router picks (N4).</summary>
        public void ConnectTo(Router next, double walkTime = 0.0)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            if (next == Stage) throw new ArgumentException("A station can't feed its own stage.", nameof(next));
            if (!(walkTime >= 0) || double.IsInfinity(walkTime))
                throw new ArgumentOutOfRangeException(nameof(walkTime), walkTime, "Walk time must be 0 or more.");
            if (Next != null) throw new InvalidOperationException($"Station '{Name}' already feeds '{Next.Name}'.");
            Next = next;
            WalkTime = walkTime;
        }
```

In `Finish`, replace `Next._blockedUpstream.AddLast((this, passenger));` with `Next.Block(this, passenger);`.

In `Renege`, replace `ReleaseBlocked(runner);` with `Stage?.ReleaseBlocked(runner);`.

Change `private void Unblock(` to `internal void Unblock(`.

Replace `MoveOn` with:

```csharp
        private void MoveOn(Runner runner, Passenger passenger)
        {
            if (Next == null)
            {
                runner.NotifyExit(passenger);
                return;
            }
            Station lane = Next.PickWithRoom(); // Finish and ReleaseBlocked only get here when there is room
            lane._walkingHere++;
            runner.Schedule(runner.Now + WalkTime, EventType.Reach, lane, passenger);
        }
```

In `TryStart`, replace the last line `ReleaseBlocked(runner);` with `Stage?.ReleaseBlocked(runner);`, and delete the old private `ReleaseBlocked` method.

- [ ] **Step 6: Make the source target a router**

Replace the body of `Unity/Assets/Core/Engine/Network/Source.cs`'s class with:

```csharp
    /// <summary>Where passengers appear. Sends each new passenger to a lane of its target stage.</summary>
    public sealed class Source
    {
        public Source(string name, IArrivalProcess arrivals, Station target, double stopTime = double.PositiveInfinity)
            : this(name, arrivals, target == null ? null : Router.For(target), stopTime)
        {
        }

        public Source(string name, IArrivalProcess arrivals, Router target, double stopTime = double.PositiveInfinity)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A source needs a name.", nameof(name));
            Name = name;
            Arrivals = arrivals ?? throw new ArgumentNullException(nameof(arrivals));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            StopTime = stopTime;
        }

        public string Name { get; }

        public IArrivalProcess Arrivals { get; }

        public Router Target { get; }

        /// <summary>No arrivals at or after this time (the end of the wave).</summary>
        public double StopTime { get; }
    }
```

- [ ] **Step 7: Route arrivals in the runner**

In `Unity/Assets/Core/Engine/Events/Runner.cs`, in `Handle`, replace `source.Target.Accept(this, passenger);` with:

```csharp
                    Station lane = source.Target.PickAny()
                        ?? throw new InvalidOperationException($"Every lane of '{source.Target.Name}' is closed.");
                    lane.Accept(this, passenger);
```

- [ ] **Step 8: Update the compiler test for the new `Next` type**

In `Unity/Assets/Core/Tests/Layout/LayoutCompilerTests.cs`, change line 21 to:

```csharp
            Assert.That(run.Stations[0].Next.Lanes, Is.EqualTo(new[] { run.Stations[1] }));
```

(Line 24, `run.Stations[1].Next, Is.Null`, stays as it is.)

- [ ] **Step 9: Run all tests**

Run: `bash scripts/run-tests.sh`
Expected: all pass (121 old + 5 new = 126). The old chain and blocking tests must pass unchanged: a shared line is a one-lane stage.

- [ ] **Step 10: Sabotage check**

Temporarily change `ShortestLine.PeopleAt` to leave out `lane.WalkingHere`, run `bash scripts/run-tests.sh LaneTests`, and confirm `ShortestLine_CountsPeopleWalkingThere` fails. Undo it.

- [ ] **Step 11: Commit**

```bash
git add Unity/Assets/Core/Engine Unity/Assets/Core/Tests
git commit -m "Add lanes: a Router picks the shortest lane; blocked desks wait at the stage

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Open and close a lane (the manager action)

**Files:**
- Modify: `Unity/Assets/Core/Engine/Events/Runner.cs`, `Unity/Assets/Core/Engine/Events/IRunObserver.cs`, `Unity/Assets/Core/Results/RunLog.cs`, `Unity/Assets/Core/Tests/Support/InvariantChecker.cs`
- Test: `Unity/Assets/Core/Tests/Engine/LaneTests.cs`, `Unity/Assets/Core/Tests/Engine/LaneStressTests.cs`

**Interfaces:**
- Consumes: `Router`, `Station.IsOpen`, `Station.SetOpen`, `Router.OpenLanes`, `Router.ReleaseBlocked` (Task 1).
- Produces: `Runner.OpenLane(Station)`, `Runner.CloseLane(Station)`; `IRunObserver.OnLaneSwitched(double time, Station lane, bool open)`; `LogKind.LaneOpened`, `LogKind.LaneClosed` (entries with `PassengerId == -1`).

- [ ] **Step 1: Write the failing hand tests**

Add to `LaneTests`:

```csharp
        [Test]
        public void CloseLane_Drains()
        {
            // Two lanes, 100 s. Four people at 0: A s1, B s2, C s1, D s2. Close s2 at 50.
            // E at 60 and F at 70 must go to s1. s2 still serves D (100–200): it drains.
            // s1: A 0–100, C 100–200, E 200–300, F 300–400.
            Station s1 = Desk("s1", 100.0), s2 = Desk("s2", 100.0);
            var (runner, log) = Start(Lanes("scan", s1, s2), 0.0, 0.0, 0.0, 0.0, 60.0, 70.0);
            runner.Run(50.0);
            runner.CloseLane(s2);
            runner.Run();

            var p = log.Passengers;
            Assert.That(p.Select(r => r.Station), Is.EqualTo(new[] { "s1", "s2", "s1", "s2", "s1", "s1" }));
            Assert.That(p[3].ServiceEnd, Is.EqualTo(200.0));
            Assert.That(p.Select(r => r.Exit), Is.EqualTo(new[] { 100.0, 100.0, 200.0, 200.0, 300.0, 400.0 }));
            LogEntry closed = log.Entries.Single(e => e.Kind == LogKind.LaneClosed);
            Assert.That((closed.Time, closed.Station, closed.PassengerId), Is.EqualTo((50.0, "s2", -1)));
            Assert.That(s2.IsOpen, Is.False);
            InvariantChecker.AssertAll(log, Staff(s1, s2));
        }

        [Test]
        public void OpenLane_TakesNewPeopleAtOnce()
        {
            // s2 starts closed. A, B, C at 0 all go to s1. Open s2 at 50; D at 60 goes to s2 (empty).
            // Nobody already in s1's line moves (no jockeying, N10).
            Station s1 = Desk("s1", 100.0), s2 = Desk("s2", 100.0);
            var (runner, log) = Start(Lanes("scan", s1, s2), 0.0, 0.0, 0.0, 60.0);
            runner.CloseLane(s2);
            runner.Run(50.0);
            runner.OpenLane(s2);
            runner.Run();

            var p = log.Passengers;
            Assert.That(p.Select(r => r.Station), Is.EqualTo(new[] { "s1", "s1", "s1", "s2" }));
            Assert.That(p.Select(r => r.ServiceStart), Is.EqualTo(new[] { 0.0, 100.0, 200.0, 60.0 }));
            Assert.That(log.Entries.Where(e => e.PassengerId == -1).Select(e => (e.Time, e.Kind)),
                Is.EqualTo(new[] { (0.0, LogKind.LaneClosed), (50.0, LogKind.LaneOpened) }));
            InvariantChecker.AssertAll(log, Staff(s1, s2));
        }

        [Test]
        public void OpenLane_ReleasesBlockedDesk()
        {
            // ID 10 s → s1 (open) and s2 (closed), 100 s, each line holds 1. Three people at 0.
            // A: ID 0–10, s1 10–110. B: ID 10–20, s1's line (full). C: ID 20–30 → BLOCKED.
            // Open s2 at 50 → C released at 50, joins s2 and starts at once.
            Station id = Desk("id", 10.0);
            Station s1 = Desk("s1", 100.0, lineCapacity: 1), s2 = Desk("s2", 100.0, lineCapacity: 1);
            id.ConnectTo(Lanes("scan", s1, s2));
            var (runner, log) = Start(id, 0.0, 0.0, 0.0);
            runner.CloseLane(s2);
            runner.Run(50.0);
            runner.OpenLane(s2);
            runner.Run();

            var c = log.Passengers[2];
            Assert.That(c.Visits[0].Unblocked, Is.EqualTo(50.0));
            Assert.That(c.Visits[1].Station, Is.EqualTo("s2"));
            Assert.That(c.Visits[1].ServiceStart, Is.EqualTo(50.0));
            InvariantChecker.AssertAll(log, Staff(id, s1, s2),
                lineCapacity: new Dictionary<string, int> { { "s1", 1 }, { "s2", 1 } });
        }

        [Test]
        public void Action_AtAnEventTime_SeesThatEvent()
        {
            // Two lanes, 100 s. A at 0 → s1. B arrives at exactly 50 and picks s2.
            // Closing s2 after Run(50) comes after B's arrival: B stays in s2 and is served there.
            Station s1 = Desk("s1", 100.0), s2 = Desk("s2", 100.0);
            var (runner, log) = Start(Lanes("scan", s1, s2), 0.0, 50.0);
            runner.Run(50.0);
            runner.CloseLane(s2);
            runner.Run();

            Assert.That(log.Passengers[1].Station, Is.EqualTo("s2"));
            Assert.That(log.Passengers[1].Exit, Is.EqualTo(150.0));
            InvariantChecker.AssertAll(log, Staff(s1, s2));
        }

        [Test]
        public void CloseLane_Rules()
        {
            Station s1 = Desk("s1", 100.0), s2 = Desk("s2", 100.0);
            var (runner, log) = Start(Lanes("scan", s1, s2), 0.0);
            runner.CloseLane(s2);
            runner.CloseLane(s2); // already closed: nothing happens
            var e = Assert.Throws<System.InvalidOperationException>(() => runner.CloseLane(s1));
            StringAssert.Contains("last open lane", e.Message);
            runner.OpenLane(s1);  // already open: nothing happens
            Assert.Throws<System.ArgumentNullException>(() => runner.CloseLane(null));
            Assert.That(log.Entries.Count(x => x.PassengerId == -1), Is.EqualTo(1));
            Assert.That(s1.IsOpen, Is.True);
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `bash scripts/run-tests.sh LaneTests`
Expected: compile errors (`CloseLane`, `OpenLane`, `LogKind.LaneClosed` don't exist).

- [ ] **Step 3: Add the observer method**

In `Unity/Assets/Core/Engine/Events/IRunObserver.cs`, before `OnExit`, add:

```csharp
        /// <summary>The manager opened or closed a lane (N7). A closed lane still serves its line.</summary>
        void OnLaneSwitched(double time, Station lane, bool open);
```

- [ ] **Step 4: Add the action to the runner**

In `Unity/Assets/Core/Engine/Events/Runner.cs`, after `AddBehaviour`, add:

```csharp
        /// <summary>
        /// Manager action (N6, N7): the lane takes new people from now on and blocked desks upstream move on.
        /// Call it after Run(t), so it sees every event at or before t. Opening an open lane does nothing.
        /// </summary>
        public void OpenLane(Station lane)
        {
            if (lane == null) throw new ArgumentNullException(nameof(lane));
            if (lane.IsOpen) return;
            lane.SetOpen(true);
            foreach (IRunObserver o in _observers) o.OnLaneSwitched(Now, lane, true);
            lane.Stage?.ReleaseBlocked(this);
        }

        /// <summary>
        /// Manager action (N5, N7): the lane gets nobody new but serves everyone already in its line, at its
        /// desk, or walking to it (it drains). The last open lane of a stage can't be closed.
        /// </summary>
        public void CloseLane(Station lane)
        {
            if (lane == null) throw new ArgumentNullException(nameof(lane));
            if (!lane.IsOpen) return;
            if (lane.Stage != null && lane.Stage.OpenLanes == 1)
                throw new InvalidOperationException($"'{lane.Name}' is the last open lane of '{lane.Stage.Name}'.");
            lane.SetOpen(false);
            foreach (IRunObserver o in _observers) o.OnLaneSwitched(Now, lane, false);
        }
```

- [ ] **Step 5: Record it in the run log**

In `Unity/Assets/Core/Results/RunLog.cs`, add two values at the **end** of `LogKind` (after `Exit`):

```csharp
        LaneOpened,
        LaneClosed,
```

Change the `LogEntry.PassengerId` property to carry a summary:

```csharp
        /// <summary>-1 for lane actions (LaneOpened, LaneClosed).</summary>
        public int PassengerId { get; }
```

After `OnExit`, add:

```csharp
        public void OnLaneSwitched(double time, Station lane, bool open)
        {
            if (!_keepEntries) return;
            _entries.Add(new LogEntry(time, open ? LogKind.LaneOpened : LogKind.LaneClosed, -1, lane.Name,
                lane.QueueLength, lane.Busy));
        }
```

- [ ] **Step 6: Run the hand tests**

Run: `bash scripts/run-tests.sh LaneTests`
Expected: all 10 pass.

- [ ] **Step 7: Add the "closed lanes get no new people" invariant**

In `Unity/Assets/Core/Tests/Support/InvariantChecker.cs`, in `AssertAll`, after `AssertFirstComeFirstServed(log);` add:

```csharp
            AssertClosedLanesGetNoNewPeople(log);
```

and add the method before `AssertLittlesLaw`:

```csharp
        /// <summary>
        /// N5: nobody chooses a closed lane. People who chose it before it closed (still walking) may arrive later.
        /// A passenger chooses when they leave the previous desk, or on arrival for the first stage (N4).
        /// </summary>
        private static void AssertClosedLanesGetNoNewPeople(RunLog log)
        {
            var closedAt = new Dictionary<string, double>();
            foreach (LogEntry e in log.Entries)
            {
                if (e.Kind == LogKind.LaneClosed) closedAt[e.Station] = e.Time;
                else if (e.Kind == LogKind.LaneOpened) closedAt.Remove(e.Station);
                else if (e.Kind == LogKind.JoinQueue && closedAt.TryGetValue(e.Station, out double closed))
                {
                    PassengerRecord p = log.Passengers[e.PassengerId];
                    int i = p.Visits.Count - 1;
                    while (i >= 0 && !(p.Visits[i].Station == e.Station && p.Visits[i].QueueJoin == e.Time)) i--;
                    double chose = i <= 0 ? p.Arrive : p.Visits[i - 1].LeftAt;
                    Assert.That(chose, Is.LessThanOrEqualTo(closed),
                        $"Passenger {e.PassengerId} chose '{e.Station}' at {chose}, after it closed at {closed}.");
                }
            }
        }
```

- [ ] **Step 8: Write the stress test**

Create `Unity/Assets/Core/Tests/Engine/LaneStressTests.cs`:

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
    /// <summary>Random runs with lanes, full lines, giving up and random open/close actions: every invariant must hold.</summary>
    public class LaneStressTests
    {
        [Test]
        public void RandomActions_KeepEveryInvariant([Range(1, 20)] int seed)
        {
            var streams = new RngStreams((ulong)seed);
            var id = new Station("id", 2, Distribution.FromMeanCv(1.0, 1.0), streams.Get("service:id"));
            Station[] lanes = Enumerable.Range(1, 3)
                .Select(n => new Station($"scan {n}", 1, Distribution.FromMeanCv(1.4, 1.0), streams.Get($"service:scan {n}"), 3))
                .ToArray();
            id.ConnectTo(new Router("scan", lanes, ShortestLine.Instance), walkTime: 0.2);
            var source = new Source("entry", new RenewalArrivals(Distribution.FromMeanCv(0.6, 1.0), streams.Get("arrivals:entry")),
                id, stopTime: 200.0);
            var runner = new Runner(source);
            runner.AddBehaviour(new Reneging(Distribution.FromMeanCv(15.0, 1.0), streams));
            var log = new RunLog();
            runner.AddObserver(log);

            Pcg32 manager = streams.Get("test:manager");
            for (double t = 5.0; t < 200.0; t += 5.0)
            {
                runner.Run(t);
                Station lane = lanes[manager.NextUInt() % 3];
                if (lane.IsOpen && lane.Stage.OpenLanes > 1) runner.CloseLane(lane);
                else runner.OpenLane(lane);
            }
            runner.Run();

            Assert.That(runner.IsFinished, Is.True);
            Assert.That(log.Entries.Count(e => e.Kind == LogKind.LaneClosed), Is.GreaterThan(0));
            var staff = new Dictionary<string, int> { { "id", 2 } };
            foreach (Station s in lanes) staff[s.Name] = 1;
            InvariantChecker.AssertAll(log, staff, lineCapacity: lanes.ToDictionary(s => s.Name, s => 3));
        }
    }
}
```

- [ ] **Step 9: Run all tests**

Run: `bash scripts/run-tests.sh`
Expected: all pass (126 + 5 hand + 20 stress = 151).

- [ ] **Step 10: Sabotage check**

Temporarily change `Router.Pick` to ignore `lane.IsOpen`. Run `bash scripts/run-tests.sh Lane` and confirm `CloseLane_Drains` and the stress test (through the new invariant) fail. Undo it.

- [ ] **Step 11: Commit**

```bash
git add Unity/Assets/Core
git commit -m "Manager action: open and close a lane live; closed lanes drain

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Lanes in the plan and the compiler

**Files:**
- Modify: `Unity/Assets/Core/Levels/LevelDef.cs`, `Unity/Assets/Core/Layout/LayoutCompiler.cs`, `Unity/Assets/Core/Tests/Support/SampleLevels.cs`
- Test: `Unity/Assets/Core/Tests/Layout/LayoutCompilerTests.cs`

**Interfaces:**
- Consumes: `Router`, `ShortestLine.Instance`, `Station.SetOpen`, `Station.ConnectTo(Router, double)`, `Source(string, IArrivalProcess, Router, double)` (Task 1); `Runner.OpenLane` (Task 2).
- Produces: `PlannedStage.Lanes : int?`; `CompiledRun.Stages : IReadOnlyList<Router>`; `CompiledRun.Lane(string name) : Station`; `SampleLevels.LanesPlan`.

- [ ] **Step 1: Write the failing tests**

In `Unity/Assets/Core/Tests/Support/SampleLevels.cs`, after `Plan`, add:

```csharp
        /// <summary>The scanner as 3 separate lanes, 2 open at the start, 4 places in each line.</summary>
        public const string LanesPlan = @"{
  ""stages"": [
    { ""type"": ""id"",      ""staff"": 3, ""walkToNext"": 0.5 },
    { ""type"": ""scanner"", ""staff"": 2, ""lanes"": 3, ""lineCapacity"": 4 }
  ]
}";
```

Add to `LayoutCompilerTests` (add `using System;`, `using System.Collections.Generic;` and `using Throughput.Core.Engine.Network;` at the top):

```csharp
        [Test]
        public void Compile_Lanes_OneStationPerLane()
        {
            CompiledRun run = LayoutCompiler.Compile(Level, LevelJson.ReadPlan(SampleLevels.LanesPlan), runSeed: 7UL);

            Assert.That(run.Stations.Select(s => s.Name), Is.EqualTo(new[] { "id", "scanner 1", "scanner 2", "scanner 3" }));
            Assert.That(run.Stages.Select(s => s.Name), Is.EqualTo(new[] { "id", "scanner" }));
            Assert.That(run.Stages[1].Lanes.Select(s => s.Name), Is.EqualTo(new[] { "scanner 1", "scanner 2", "scanner 3" }));
            Assert.That(run.Stations.Select(s => s.IsOpen), Is.EqualTo(new[] { true, true, true, false }));
            Assert.That(run.Stations.Skip(1).Select(s => s.Staff), Is.All.EqualTo(1));
            Assert.That(run.Stations[0].Next, Is.SameAs(run.Stages[1]));
            Assert.That(run.LineCapacity, Is.EqualTo(new Dictionary<string, int> { { "scanner 1", 4 }, { "scanner 2", 4 }, { "scanner 3", 4 } }));
            Assert.That(run.Lane("scanner 3"), Is.SameAs(run.Stations[3]));
            Assert.Throws<System.ArgumentException>(() => run.Lane("xray"));
        }

        [Test]
        public void Compile_Lanes_RunsToTheEnd()
        {
            CompiledRun run = LayoutCompiler.Compile(Level, LevelJson.ReadPlan(SampleLevels.LanesPlan), runSeed: 7UL);
            var log = new RunLog();
            run.Runner.AddObserver(log);
            run.Runner.Run();

            Assert.That(run.Runner.IsFinished, Is.True);
            Assert.That(log.Passengers.SelectMany(p => p.Visits).Any(v => v.Station == "scanner 3"), Is.False, "scanner 3 is closed");
            InvariantChecker.AssertAll(log, run.StaffByStation, lineCapacity: run.LineCapacity);
        }

        [Test]
        public void Check_LaneRules()
        {
            var plan = new PlanDef();
            plan.Stages.Add(new PlannedStage { Type = "id", Staff = 1, Lanes = 0 });                       // at least 1 lane
            plan.Stages.Add(new PlannedStage { Type = "scanner", Staff = 3, Lanes = 2, LineCapacity = 8 }); // 3 staff for 2 lanes
            string all = string.Join("\n", LayoutCompiler.Check(Level, plan));
            StringAssert.Contains("Stage 1 'id': needs at least 1 lane", all);
            StringAssert.Contains("Stage 2 'scanner': 3 staff for 2 lanes", all);

            plan.Stages[0].Lanes = 5;                                     // id allows 4 desks
            plan.Stages[1] = new PlannedStage { Type = "scanner", Staff = 1, Lanes = 3, LineCapacity = 8 }; // 3 × 8 = 24 > 20 space
            all = string.Join("\n", LayoutCompiler.Check(Level, plan));
            StringAssert.Contains("Stage 1 'id': 5 lanes (this level allows up to 4)", all);
            StringAssert.Contains("use 24 places", all);
        }

        [Test]
        public void ManagerAction_ChangesOnlyWhatItAffects()
        {
            // Same seed, with and without opening scanner 3 at minute 20.
            RunLog Once(bool act)
            {
                CompiledRun run = LayoutCompiler.Compile(Level, LevelJson.ReadPlan(SampleLevels.LanesPlan), runSeed: 11UL);
                var log = new RunLog();
                run.Runner.AddObserver(log);
                run.Runner.Run(20.0);
                if (act) run.Runner.OpenLane(run.Lane("scanner 3"));
                run.Runner.Run();
                return log;
            }
            RunLog without = Once(false), with = Once(true), again = Once(true);

            Assert.That(again.Entries.Select(e => e.ToString()), Is.EqualTo(with.Entries.Select(e => e.ToString())), "same actions, same run");
            Assert.That(with.Entries.TakeWhile(e => e.Time < 20.0).Select(e => e.ToString()),
                Is.EqualTo(without.Entries.TakeWhile(e => e.Time < 20.0).Select(e => e.ToString())), "nothing before the action changes");
            Assert.That(with.Passengers.Select(p => p.Arrive), Is.EqualTo(without.Passengers.Select(p => p.Arrive)), "arrivals don't change");
            // The k-th ID check always draws the k-th number from "service:id", whoever it is (people giving up
            // at the ID line can differ between runs), so the ID desk times in start order must match.
            double[] withId = IdDeskTimes(with), withoutId = IdDeskTimes(without);
            int n = Math.Min(withId.Length, withoutId.Length);
            Assert.That(withId.Take(n), Is.EqualTo(withoutId.Take(n)), "ID desk times don't change");
            Assert.That(with.Passengers.SelectMany(p => p.Visits).Any(v => v.Station == "scanner 3"), Is.True, "the action did something");
        }

        private static double[] IdDeskTimes(RunLog log) =>
            log.Entries.Where(e => e.Kind == LogKind.StartService && e.Station == "id")
                .Select(e => log.Passengers[e.PassengerId].Visits[0])
                .Select(v => v.ServiceEnd - v.ServiceStart)
                .ToArray();
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `bash scripts/run-tests.sh LayoutCompilerTests`
Expected: compile errors (`PlannedStage.Lanes`, `CompiledRun.Stages`, `CompiledRun.Lane` don't exist).

- [ ] **Step 3: Add `Lanes` to the plan**

In `Unity/Assets/Core/Levels/LevelDef.cs`, in `PlannedStage`, replace the `Staff` property with:

```csharp
        /// <summary>Shared line: the number of desks. Lanes: how many lanes are open at the start (the first ones, N8).</summary>
        public int Staff { get; set; }

        /// <summary>Separate lanes (N1, N8): this many lanes, each with one desk and its own line. Null = one shared line.</summary>
        public int? Lanes { get; set; }
```

and change the `LineCapacity` summary to:

```csharp
        /// <summary>Waiting-area size in people (per lane, with lanes); null = unlimited. Must be null for the first stage (L6).</summary>
```

- [ ] **Step 4: Build lanes in the compiler**

In `Unity/Assets/Core/Layout/LayoutCompiler.cs`, add `using Throughput.Core.Engine.Policies;`.

Replace `CompiledRun` with:

```csharp
    /// <summary>A built network, ready to run.</summary>
    public sealed class CompiledRun
    {
        internal CompiledRun(Runner runner, IReadOnlyList<Router> stages)
        {
            Runner = runner;
            Stages = stages;
            Stations = stages.SelectMany(s => s.Lanes).ToList();
            StaffByStation = Stations.ToDictionary(s => s.Name, s => s.Staff);
            LineCapacity = Stations.Where(s => s.LineCapacity != int.MaxValue).ToDictionary(s => s.Name, s => s.LineCapacity);
        }

        public Runner Runner { get; }

        /// <summary>First to last; each has one lane (a shared line) or several.</summary>
        public IReadOnlyList<Router> Stages { get; }

        /// <summary>Every station (lane), first stage to last.</summary>
        public IReadOnlyList<Station> Stations { get; }

        public IReadOnlyDictionary<string, int> StaffByStation { get; }

        /// <summary>Only the limited waiting areas.</summary>
        public IReadOnlyDictionary<string, int> LineCapacity { get; }

        /// <summary>A station by name, e.g. "scanner 2", for manager actions.</summary>
        public Station Lane(string name) =>
            Stations.FirstOrDefault(s => s.Name == name) ?? throw new ArgumentException($"There is no lane '{name}'.", nameof(name));
    }
```

In `Compile`, replace everything from `var stations = new List<Station>();` to the end of the `foreach` loop with:

```csharp
            var stages = new List<Router>();
            for (int i = 0; i < plan.Stages.Count; i++)
            {
                PlannedStage ps = plan.Stages[i];
                StageTypeDef type = level.Stages.First(t => t.Name == ps.Type);
                int capacity = ps.LineCapacity ?? int.MaxValue;
                var lanes = new List<Station>();
                if (ps.Lanes == null)
                {
                    lanes.Add(new Station(type.Name, ps.Staff, type.Service.Build(), streams.Get("service:" + type.Name), capacity));
                }
                else
                {
                    for (int n = 1; n <= ps.Lanes.Value; n++)
                    {
                        string name = $"{type.Name} {n}";
                        var lane = new Station(name, 1, type.Service.Build(), streams.Get("service:" + name), capacity);
                        if (n > ps.Staff) lane.SetOpen(false); // staff open the first lanes; the rest wait closed (N8)
                        lanes.Add(lane);
                    }
                }
                var stage = new Router(type.Name, lanes, ShortestLine.Instance);
                if (stages.Count > 0)
                    foreach (Station previous in stages[stages.Count - 1].Lanes)
                        previous.ConnectTo(stage, plan.Stages[i - 1].WalkToNext);
                stages.Add(stage);
            }
```

Then change the source and the return:

```csharp
            var source = new Source("entry", new ArrivalWaves(waves, streams.Get("arrivals:entry")), stages[0]);
```

```csharp
            return new CompiledRun(runner, stages);
```

- [ ] **Step 5: Check the lane rules**

In `CheckPlan`, replace the staff check

```csharp
                if (ps.Staff < 1 || ps.Staff > type.MaxStaff)
                    problems.Add($"Stage {i + 1} {name}: {ps.Staff} staff (this level allows 1 to {type.MaxStaff}).");
```

with:

```csharp
                if (ps.Lanes == null)
                {
                    if (ps.Staff < 1 || ps.Staff > type.MaxStaff)
                        problems.Add($"Stage {i + 1} {name}: {ps.Staff} staff (this level allows 1 to {type.MaxStaff}).");
                }
                else if (ps.Lanes.Value < 1)
                    problems.Add($"Stage {i + 1} {name}: needs at least 1 lane.");
                else
                {
                    if (ps.Lanes.Value > type.MaxStaff)
                        problems.Add($"Stage {i + 1} {name}: {ps.Lanes.Value} lanes (this level allows up to {type.MaxStaff}).");
                    if (ps.Staff < 1 || ps.Staff > ps.Lanes.Value)
                        problems.Add($"Stage {i + 1} {name}: {ps.Staff} staff for {ps.Lanes.Value} lanes (each open lane needs 1; at least 1 open).");
                }
```

and change `space += ps.LineCapacity.Value;` to:

```csharp
                        space += ps.LineCapacity.Value * Math.Max(1, ps.Lanes ?? 1);
```

- [ ] **Step 6: Run all tests**

Run: `bash scripts/run-tests.sh`
Expected: all pass (151 + 4 = 155). `Compile_SameSeedSameRun` must still pass unchanged: a shared-line plan builds the same stations and streams as before.

- [ ] **Step 7: Run the strict tier**

Run: `bash scripts/run-tests.sh --strict`
Expected: 19/19 pass (the formula tests use shared lines, which must behave exactly as before).

- [ ] **Step 8: Commit**

```bash
git add Unity/Assets/Core
git commit -m "Plans can split a stage into lanes; the compiler builds and checks them

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Docs

**Files:**
- Modify: `docs/superpowers/specs/2026-10-11-phase-1-core-design.md`, `docs/architecture/simulation-core.md`, `docs/roadmap.md`

- [ ] **Step 1: Update the spec**

In the spec:
- Split `### S8: Starter policies, rules, events, actions` into `### S8a: Lanes, routing, manager action` (tick: `Router` + routing policies, shortest line, open/close lane applied live at t, an action changes only what it affects, hand tests with invariants) and `### S8b: Staffing rule and breakdown` (the staffing rule and the event deck items, unticked).
- Progress table: replace the S8 row with `| S8a | Lanes, routing, manager action | Done (<date>) |` and `| S8b | Staffing rule, breakdown | Not started |`.
- Add `**S8a decisions (2026-10-12, plan part 7):**` with N1–N10 from this plan, one line each, after the S7 decisions.
- Review notes: mark the S6 note on actions "Done (N7, `Action_AtAnEventTime_SeesThatEvent`)", and the S7 note on lane stream names "Done (`service:<lane>`)". Add: the S4 note on visit numbers for re-visits is still open (lanes don't create loops).
- Append to the change log: `| 2026-10-12 | S8 split into S8a/S8b; S8a done: lanes as stations, Router with shortest line, open/close lane live (closed lanes drain), lanes in the plan |`.

- [ ] **Step 2: Update the architecture and roadmap**

- `docs/architecture/simulation-core.md`: add a **Router** entry: the entrance of a stage, picks a lane with `IRoutingPolicy` when a passenger leaves the previous desk (or arrives), holds blocked desks; a shared line is a one-lane stage; `OpenLane` / `CloseLane` are the first manager actions.
- `docs/roadmap.md`: `S0–S7 done; next: S8 …` → `S0–S8a done; next: S8b (staffing rule, breakdown)`.

- [ ] **Step 3: Commit**

```bash
git add docs
git commit -m "Close S8a: lanes, routing and the first manager action

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
