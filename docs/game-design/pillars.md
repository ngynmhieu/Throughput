# Design Pillars

The rules every feature, art choice and line of code must serve. When unsure whether to add or change something, check it against these. If it breaks a pillar, drop it or redesign it.

Pillars are not the gameplay. They are the rules used to **judge** the gameplay, the style and the architecture.

---

## Active pillars

### P1. Every fix has a cost

Staff, space, money or fairness: every improvement the player makes costs something.

- **Rejects:** free upgrades, "just add more" solutions, tools with no downside.
- **In gameplay:** every tool in [mechanism-scopes.md](../game-mechanism/mechanism-scopes.md) has an entry in the Costs table.
- **In style:** costs are shown at placement time (e.g. "−1 staff", "uses 6 m² of floor").
- **In architecture:** each tool declares its cost as data, so the results screen and budget checks read it from one place.

### P2. The cause is always visible

If a queue gets worse, the player can see why, either by watching the run or by reading the results.

- **Rejects:** unexplained jumps, hidden random penalties, cluttered screens that hide the cause.
- **In gameplay:** the slowest stage glows after a run, busy gauges per stage, the results screen names the worst-off group.
- **In style:** colour only for meaning (green fine, yellow slowing, red problem), one overlay at a time.
- **In architecture:** the simulation records *why* things happened (e.g. "blocked: downstream full"), not just *what*, so the feedback layer can explain it.

### P3. Same plan, same result

The simulation is fair and repeatable. Retrying an unchanged design gives exactly the same outcome, so failure is the design's fault, not luck.

- **Rejects:** random events the player can't plan for, outcomes that swing on luck between retries.
- **In gameplay:** passengers look random, but they're the same passengers every retry. Breakdowns and other events are **scheduled in the level** and visible in Plan, not random surprises.
- **In style:** a level's events can be previewed on the flight board or timeline.
- **In architecture:**
  - a **fixed random seed per level**
  - the simulation never depends on frame rate or real time (guaranteed by the event-list design, D7)
  - same input + same seed = same event list, which can be checked by automated tests

### P4. Calm, not frantic

A thoughtful puzzle in a cozy miniature world. No time pressure while planning.

- **Rejects:** twitch reactions, timers during Plan, stressful alarms, punishing fail screens.
- **In gameplay:** Plan has no clock. A failed run is "edit and try again", not game over (D4).
- **In style:** soft colours, gentle music, satisfying clicks, subtle effects. Bad news is shown clearly but without alarm sounds or screen shake.
- **In architecture:** nothing to enforce directly. It mostly guides UI and audio choices.

---

## Considered, not adopted

| Candidate | Why not a pillar |
|---|---|
| True to real queues | Already guaranteed by the main subject (queueing theory) and the Erlang C/A validation tests. Kept as an engineering rule, not a pillar. |
| Small and readable | Mostly a scope rule; its readability part is covered by P2. Scope is guarded by the "ship-necessary razor" in the brainstorm notes. |

A candidate can be promoted later if it starts deciding things the active pillars don't.

---

## How to use the pillars

For any new idea, ask:

1. **P1:** what does it cost the player?
2. **P2:** when it affects the queue, can the player see why?
3. **P3:** does it give the same result on every retry?
4. **P4:** does it keep the game calm?

If any answer is "no", either redesign it or drop it.

**Example:** "A VIP passenger randomly appears mid-run and skips the line."
- P3 fails because it's random, so make it scheduled and shown in Plan.
- P2 needs the VIP to be clearly visible, so the player knows why waits jumped.

## How to change the pillars

- Keep **3–5 active pillars**. More than that and they stop deciding anything.
- To add a pillar, write it in the same format (statement, Rejects, In gameplay / style / architecture) and say what it decides that the others don't.
- To change or remove one, record it in the change log below with the reason.

## Change log

| Date | Change | Reason |
|---|---|---|
| 2026-10-10 | Adopted P1–P4. Considered "True to real queues" and "Small and readable" but did not adopt them. | Brainstorm debate, topic 2 |
