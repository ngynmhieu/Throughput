# Architecture: Simulation Core

Inside `Throughput.Core`, the `Core/Engine/` part: how the compiled network is run. (Folder was `Core/Sim/` and the loop class `Simulator` until 2026-10-11; renamed to `Engine` / `Runner` because the user prefers a "working machinery" name. "Discrete-event simulation" stays as the theory term.) Accepted as the starting design on 2026-10-10; expect refinements once real code exists.

See [overview.md](overview.md) for where this sits (decisions A1–A4).

---

## The network (output of the layout compiler)

| Node | What it is | Key numbers |
|---|---|---|
| `Source` | Where passengers appear | Arrival schedule (flight waves), passenger mix |
| `Queue` | A line that holds people in order | Capacity (from floor area), discipline (FIFO / express / priority) |
| `Station` | A stage with staff serving one person each | Staff count, service time per passenger type, setup time |
| `Buffer` | A limited waiting space between stages | Capacity; when full, the stage feeding it **blocks** (D8) |
| `Router` | A point where a choice is made | Policy: shortest line / least work / random / by type / passenger choice |
| `Sink` | Exit | — |

Edges between nodes carry a **walk time** (from distance in the layout).

## The event loop

```
while events remain and time < wave end:
    take the earliest event (ties broken by a fixed order → the same seed and actions always give the same result, P3)
    handle it → change state, schedule new events, write to the timeline
```

**Event types:** `Arrive`, `JoinQueue`, `StartService`, `EndService`, `Blocked`, `Unblocked`, `Renege`, `Balk`, `SwitchLane`, `StaffOpen`, `StaffClose`, `SetupDone`, `MoodChange`, `Exit`, `CheckRule` (re-checks a timed rule condition such as "queue over 20 for 30 s").

Same-time order (built so far): `EndService` → `Renege` → `Arrive`.

- Threshold rules are re-checked whenever a queue length changes. A "for N seconds" condition schedules its own `CheckRule` event.
- **Blocking (D8):** before a station sends a passenger onward, it checks for room downstream. If there is none, the station stays occupied (`Blocked`) until space frees up (`Unblocked`). This is the blocking-after-service rule.

## Three kinds of pluggable parts

The core loop stays small; everything game-specific plugs in.

### 1. Policies: decisions the player configures

| Policy | Question it answers | Examples |
|---|---|---|
| Discipline | Who's next in this queue? | FIFO, express (≤1 bag), priority class |
| Routing | Which lane does this passenger go to? | Shortest line, least work, random, by type, power of two |
| Staffing rule | When does a lane open or close? | "Open when queue > 20, close when < 5", with setup time (F1, F2) |

Each policy is a small class behind an interface, chosen by the plan.

### 2. Behaviours: what passengers do on their own (D6)

| Behaviour | When on | Reacts to |
|---|---|---|
| Reneging (patience runs out → missed flight) | Always | On `JoinQueue`, schedules `Renege` at patience time; cancelled on `StartService` |
| Balking | Unlockable per level | On arrival at a queue: refuse if it looks too long (signs change this) |
| Lane switching | Unlockable | On queue changes: move to a shorter-looking line |
| Route choice (selfish routing) | Unlockable, late | At a `Router`: pick what looks fastest for the passenger |
| Perceived wait / happiness | Unlockable, mid-game (D10) | Tracks felt wait using modifiers (signs, screens, fairness) and emits `MoodChange` |

- Each behaviour reacts to events and may schedule new ones.
- **Adding a behaviour means adding one module.** The core loop never changes.
- **How cancelling works:** the `Renege` event is not removed when service starts; when it fires, the station sees the passenger is no longer in line and ignores it (lazy cancellation). `IBehaviour` has one method today (`OnJoinQueue`); later behaviours add methods as they need them.
- The level definition lists which behaviours are on.

### 3. Observers: things that only watch

- The **timeline recorder** and **statistics** (busy %, waits per group, happiness, throughput).
- Observers listen to events and **can't change** the simulation.

## Randomness (P3: fair, not lucky)

- Each attempt gets a **run seed**, saved with the run.
- One seeded random generator **per source of randomness**: arrivals, service times, patience, passenger types, **event timing**, and so on. All are derived from the run seed.
- **Event deck (D12):** at the start of a run, each event in the level's deck (e.g. one breakdown) gets a random time and target from the event-timing stream. Events become ordinary entries in the event list (`Breakdown`, `Repaired`, `DelayedFlight`, `GroupArrives`).
- **Why separate streams:** within one run, a manager action (e.g. opening a lane) doesn't reshuffle unrelated random numbers, so re-simulating from now changes only what the action affects.

## Time

- The simulation clock is in **game-seconds**, stored as a `double`, and never tied to frames or real time.
- The wave has a fixed end time from the level definition.
