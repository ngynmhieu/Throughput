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

### P3. Fair, not lucky

Every attempt faces the **same amount of trouble**; only the timing and placement change. A good plan with good management passes, whatever the dice say. Any run can be replayed exactly.

- **Rejects:** luck deciding pass or fail; unbounded randomness (two breakdowns one run, none the next); outcomes that can't be explained afterwards.
- **In gameplay:**
  - Each level has an **event deck**: a fixed list of trouble such as "1 scanner breakdown, 1 delayed flight, 1 tour group". When and where each one hits is random per attempt (D12).
  - Passengers and service times also vary per attempt, within the level's fixed mix and rates.
  - No result preview in Plan (D11). You find out by running and managing.
- **In style:** the level card says what *kind* of trouble to expect ("This shift: 1 breakdown, 1 delay"), never when.
- **In architecture:**
  - each attempt gets its own **run seed**, saved with the run
  - run seed + plan + manager actions with their times = exactly the same timeline, so replays are exact and automated tests can check it
  - the simulation never depends on frame rate or real time (guaranteed by the event-list design, D7)
  - level balance is tested by simulating many seeds headless: a plan's pass rate must be stable, not luck-driven

### P4. Calm, not frantic

A thoughtful puzzle in a cozy miniature world. No time pressure while planning.

- **Rejects:** twitch reactions, timers during Plan, stressful alarms, punishing fail screens.
- **In gameplay:** Plan has no clock. Runs play at 1× but can be paused at any time, and manager actions work while paused. A failed run is "edit and try again", not game over (D4).
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
3. **P3:** is it fair: does every attempt face the same amount of trouble, and can the run be replayed exactly?
4. **P4:** does it keep the game calm?

If any answer is "no", either redesign it or drop it.

**Example:** "A VIP passenger randomly appears mid-run and skips the line."
- P3 is fine only if the VIP is part of the level's event deck (fixed count, random timing), not a random extra.
- P2 needs the VIP to be clearly visible, so the player knows why waits jumped.

## How to change the pillars

- Keep **3–5 active pillars**. More than that and they stop deciding anything.
- To add a pillar, write it in the same format (statement, Rejects, In gameplay / style / architecture) and say what it decides that the others don't.
- To change or remove one, record it in the change log below with the reason.

## Change log

| Date | Change | Reason |
|---|---|---|
| 2026-10-10 | P3 rewritten from "Same plan, same result" to "Fair, not lucky". | Random events (event deck) added so live management matters (D12). |
| 2026-10-10 | Adopted P1–P4. Considered "True to real queues" and "Small and readable" but did not adopt them. | Brainstorm debate, topic 2 |
