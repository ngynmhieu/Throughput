# Architecture: Overview

How Throughput's code is organised. These decisions come from the brainstorm debate (2026-10-10) and serve the [design pillars](../game-design/pillars.md) and the [mechanism decisions](../game-mechanism/decisions.md).

---

## Decisions

| # | Question | Decision |
|---|---|---|
| A1 | Simulation engine | Event list (discrete-event simulation), plain C#, a run seed per attempt, saved with the run (D7, P3, D12) |
| A2 | How simulation and viewer talk | **Simulate as you watch**: the engine runs step by step up to the playback time; manager actions apply to the live engine at the current time; the recording is the replay |
| A3 | What the simulation sees | **An abstract queueing network** compiled from the player's 3D layout, not the 3D space itself |
| A4 | Code boundaries | Three assemblies; the simulation never references Unity |

### A2. Simulate as you watch, record, replay

- When the player presses Run, the engine starts. Each frame, the playback controller calls `Run(until: game time)`: the engine handles everything up to that time and stops its clock exactly there.
- A **manager action** at time *t* (e.g. switch Lane 3 to express) is applied to the live engine at *t*, and the run simply continues. Actions are recorded as `(t, action)`.
- Everything that happens is **recorded** (the run log: every arrival, service start and end, block, renege, mood change… with its time). The recording is the replay and the input for the results.
- Same run seed + plan + actions → the same run: a replay runs the engine again and applies the recorded actions at their times (P3).
- **Playback is 1× only**, with pause. Rewinding during a run is not allowed, because it would undo actions. After the run, the **replay** can be scrubbed freely to review.
- **Why:**
  - No work is thrown away: the future is only computed as it's watched.
  - No snapshot copies of the engine to get wrong, and an action needs no re-run.
  - No result preview (D11) comes for free: the future doesn't exist yet.
  - The **post-run replay** can be scrubbed to see when a jam started (P2); scrubbing rebuilds the state at any time from the recording.
  - Tests drive the same engine without Unity; "many small steps = one run" is a test.
- **Cost:** the recording must hold everything the viewer needs, and memory grows with wave length. That's a few thousand events per wave, which is small.
- **History:** 2026-10-11: was "simulate the whole wave first, then re-simulate from a snapshot when the player acts". Changed because the future was thrown away on every action and copying the full engine state is easy to get wrong; stepping needs neither (user's choice, option C). Rewinding during a run isn't needed (D4).

### A3. Abstract queueing network

- Before a run, the **layout compiler** turns the 3D layout into a graph, e.g.:
  ```
  Entrance ──► Queue (cap 59) ──► Lane 1..3 (ID, scanner) ──► Waiting area (cap 8) ──► Bag search ──► Exit
  ```
  Each node is a queue, server or buffer with numbers: capacity (from floor area), walking time (from distance), staff, service times, rules.
- The simulation runs on this graph and knows nothing about 3D.
- **Why:** this is the queueing theory being studied (the main subject). It's fast, testable, and validates directly against Erlang C/A.
- **Accepted limits:** crowd bumping while walking isn't simulated, and walk times are fixed. The viewer still animates believable walking paths in 3D, but they don't affect the result.
- **Consequence:** the layout compiler is an important piece and must give clear errors ("Lane 3 isn't connected to anything").

---

## The big picture

```
 ┌─────────────── PLAN (player edits) ───────────────┐
 │  Level definition   +   Player's plan             │
 │  (flight board,          (barriers, lanes, staff,  │
 │   event deck, budget,     rules, signs on the      │
 │   locked parts,           0.5 m grid)              │
 │   enabled behaviours)                              │
 └───────────────────────┬───────────────────────────┘
                         ▼
              ┌─────────────────────┐   errors: "Lane 3 isn't
              │  Layout compiler    │──► connected", "Over budget"
              └──────────┬──────────┘
                         ▼  Network (stages, buffers + capacity,
                            routes, staff, rules, walk times)
              ┌─────────────────────┐
run seed ─► │  Simulation core    │  event list · behaviour modules
              │  (plain C#)         │  · routing / discipline policies
              └──────────┬──────────┘
                         ▼
              ┌─────────────────────┐      ┌──────────────────┐
              │  Timeline           │─────►│  Results         │
              │  recorded events    │      │  goal, extras,   │
              └──────────┬──────────┘      │  worst-off group │
                         ▼                 └──────────────────┘
 ┌─────────────── WATCH (Unity viewer) ──────────────┐
 │  Playback controller (time cursor, speed, scrub)  │
 │    ├─► Diorama view   (passengers walk the layout)│
 │    ├─► Blueprint map  (same data, map drawing)    │
 │    └─► HUD / labels / results panel               │
 └───────────────────────────────────────────────────┘
```

## The pieces

| Piece | Job | Knows Unity? |
|---|---|---|
| **Level definition** | What the level throws at you: arrival schedule (flight board), passenger mix, event deck (D12), budget, goals, locked elements, enabled passenger behaviours | No (data) |
| **Plan** | What the player built, as grid data: barriers, lanes, staff, rules, signs (phase 1: a chain of stages as numbers; the grid editor turns geometry into these numbers in sub-project 2) | No (data) |
| **Layout compiler** | Turns Plan + Level into a Network; checks everything and lists all problems at once | No |
| **Simulation core** | Runs the Network on the event list with the attempt's run seed (plus manager actions); produces the Timeline | No |
| **Timeline** | Every event with its time, recorded as the run is stepped; replay scrubbing rebuilds the state from it | No |
| **Results** | Reads the Timeline: main goal, optional extras, worst-off group, happiness (D9, D10) | No |
| **Playback controller** | Moves a time cursor over the Timeline at the chosen speed; pause, scrub, skip | Yes |
| **Diorama view** | Draws passengers and staff at the cursor's time, walking believable paths through the layout | Yes |
| **Blueprint map** | Draws the same state as the engineering map (Tab) | Yes |
| **HUD / labels / results panel** | World labels, gauges, signs, mood bubbles, results (design system components) | Yes |
| **Plan editor** | Mouse placement on the grid; writes to the Plan | Yes |

## Assemblies (code boundaries)

| Assembly | Contains | References |
|---|---|---|
| `Throughput.Core` | Level definition, Plan, Layout compiler, Simulation core, Timeline, Results | Nothing from Unity |
| `Throughput.Core.Tests` | Unit tests; Erlang C/A validation tests; "same seed + actions = same timeline" tests; many-seed balance tests | `Throughput.Core` |
| `Throughput.Game` | Everything Unity: views, playback, input, plan editor, UI | `Throughput.Core` |

**Rule:** `Throughput.Core` never references Unity. The assembly definitions enforce this, so the logic stays testable without opening Unity.

---

## Still to design

| Section | Covers |
|---|---|
| ~~Inside the simulation core~~ | Done: see [simulation-core.md](simulation-core.md) |
| ~~Viewer and playback~~ | Done: see [viewer-playback.md](viewer-playback.md) |
| Data formats | How levels and plans are stored |
| Testing | Validation against Erlang C/A, determinism tests, compiler tests |
