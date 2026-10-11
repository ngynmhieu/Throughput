# Architecture: Viewer and Playback

Inside `Throughput.Game` (Unity): how the timeline becomes what the player sees, and how the player acts during a run. Accepted on 2026-10-10.

See [overview.md](overview.md) (A2: simulate as you watch) and [decisions D4, D11, D12](../game-mechanism/decisions.md).

---

## The run, step by step

```
Plan ──Run──► each frame: engine.Run(until: game time) ──► recording ──► shown at 1×
                         ▲                                         │
                         │  manager action at time t: applied to   │
                         └──── the live engine, run continues ◄────┘
                                                                   │
                                     everyone gone ──► results + replay (from the recording)
```

1. **Run:** the compiler builds the network, a new run seed is drawn, and the engine starts. It advances as playback advances.
2. **Play:** the playback controller moves a time cursor at **1× only**. **Pause** is allowed, and manager actions still work while paused.
3. **Act:** a manager action at time *t* (change routing, open/close a lane, move staff, toggle lane rules, signs on/off) is recorded as `(t, action)` and applied to the engine at *t*. Nothing is recomputed; the run continues.
4. **End:** results are computed from the final timeline. The run (seed + plan + actions) is saved, so it can be replayed exactly.
5. **Replay:** after the run, the player can watch it again and **scrub** freely to see when and why problems started (P2). Scrubbing rebuilds what the checkpoint looked like at any time from the recording.

## Rules

- **No result preview (D11).** The timeline's future is computed but never shown before it happens.
- **No rewind during a live run.** Going back would undo actions. Scrubbing exists only in the post-run replay.
- **Layout is frozen during a run.** Only manager actions change the run.
- **The viewer never decides anything.** If the timeline says "arrives at Lane 2 at 132.4 s", the animation makes that true.

## Pieces

| Piece | Job |
|---|---|
| **Playback controller** | Owns the time cursor (game-seconds), play/pause, and replay scrubbing |
| **Run session** | Holds the run seed, plan, list of manager actions and the live engine; steps it each frame and applies actions at the current time |
| **State reader** | Gives the state at any time *t* for the replay, rebuilt from the recording (it can cache states every few seconds for fast scrubbing) |
| **Diorama view** | Draws passengers and staff from pooled **PassengerView** objects; queues are slots (`duration-slot` easing); walking between nodes follows a visual path (NavMesh or spline) timed to the timeline |
| **Blueprint map** | Draws the network itself with live state (the map *is* the network, A3) |
| **HUD** | World labels, busy gauges, wait signs, mood bubbles, event alerts, manager-action bar, results (design system components) |
| **Plan editor** | Grid placement with ghost preview, writes to Plan data; the 3D scene is rebuilt from the Plan; the compiler runs live to show errors |

## Random events in the viewer (D12)

- The level card shows the *kinds* of trouble to expect ("This shift: 1 breakdown, 1 delay"), never the times.
- When an event fires, it shows in the world (e.g. the broken scanner stops with a ■ mark and a calm label "Scanner 2 broken · repair 3 min"), plus an edge alert if it's off screen.
- No alarms or shaking (P4).
