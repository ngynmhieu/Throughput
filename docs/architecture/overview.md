# Architecture: Overview

How Throughput's code is organised. These decisions come from the brainstorm debate (2026-10-10) and serve the [design pillars](../game-design/pillars.md) and the [mechanism decisions](../game-mechanism/decisions.md).

---

## Decisions

| # | Question | Decision |
|---|---|---|
| A1 | Simulation engine | Event list (discrete-event simulation), plain C#, a run seed per attempt, saved with the run (D7, P3, D12) |
| A2 | How simulation and viewer talk | **Simulate first, then replay**, with **re-simulate from now** when the player acts during a run; Unity plays the timeline at 1× |
| A3 | What the simulation sees | **An abstract queueing network** compiled from the player's 3D layout, not the 3D space itself |
| A4 | Code boundaries | Three assemblies; the simulation never references Unity |

### A2. Simulate first, then replay

- When the player presses Run, the whole wave is simulated in milliseconds and produces a **timeline**: every arrival, service start and end, block, renege, mood change and so on, with its time.
- Unity **plays the timeline back** like a video.
- **Re-simulate from now (added with D4 revision):** when the player makes a manager action at time *t* (e.g. switches Lane 3 to express), the simulation restarts from the snapshot at *t* with the new setting and instantly produces a new future timeline. Playback continues smoothly. Actions are recorded with their times, so the run seed + plan + actions always reproduce the same timeline (P3).
- **Playback is 1× only**, with pause. Rewinding during a run is not allowed, because it would undo actions. After the run, a **replay** can be scrubbed freely to review.
- **Why:**
  - Playback, pause and the post-run replay only move a time cursor.
  - The **post-run replay** can be scrubbed to see when a jam started (P2).
  - Results and previews are available instantly.
  - The timeline *is* the result, so replays are always identical (P3).
  - No result is shown before the run (D11), even though it is computed instantly. The future part of the timeline stays hidden from the player.
  - Tests check the timeline without Unity.
  - Nothing changes during a run in Plan → Watch (D4), so nothing is lost by computing it first.
- **Cost:** the timeline must record everything the viewer needs, and memory grows with wave length. That's a few thousand events per wave, which is small.

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
              │  events + snapshots │      │  goal, extras,   │
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
| **Plan** | What the player built, as grid data: barriers, lanes, staff, rules, signs | No (data) |
| **Layout compiler** | Turns Plan + Level into a Network; validates it and reports clear errors | No |
| **Simulation core** | Runs the Network on the event list with the attempt's run seed (plus manager actions); produces the Timeline | No |
| **Timeline** | Every event with its time, plus a state snapshot every few seconds so scrubbing is instant | No |
| **Results** | Reads the Timeline: main goal, optional extras, worst-off group, happiness (D9, D10) | No |
| **Playback controller** | Moves a time cursor over the Timeline at the chosen speed; pause, scrub, skip | Yes |
| **Diorama view** | Draws passengers and staff at the cursor's time, walking believable paths through the layout | Yes |
| **Blueprint map** | Draws the same state as the engineering map (Tab) | Yes |
| **HUD / labels / results panel** | World labels, gauges, signs, mood bubbles, results (design system components) | Yes |
| **Plan editor** | Mouse placement on the grid; writes to the Plan | Yes |

## Assemblies (code boundaries)

| Assembly | Contains | References |
|---|---|---|
| `Throughput.Sim` | Level definition, Plan, Layout compiler, Simulation core, Timeline, Results | Nothing from Unity |
| `Throughput.Sim.Tests` | Unit tests; Erlang C/A validation tests; "same seed + actions = same timeline" tests; many-seed balance tests | `Throughput.Sim` |
| `Throughput.Game` | Everything Unity: views, playback, input, plan editor, UI | `Throughput.Sim` |

**Rule:** `Throughput.Sim` never references Unity. The assembly definitions enforce this, so the simulation stays testable without opening Unity.

---

## Still to design

| Section | Covers |
|---|---|
| ~~Inside the simulation core~~ | Done: see [simulation-core.md](simulation-core.md) |
| ~~Viewer and playback~~ | Done: see [viewer-playback.md](viewer-playback.md) |
| Data formats | How levels and plans are stored |
| Testing | Validation against Erlang C/A, determinism tests, compiler tests |
