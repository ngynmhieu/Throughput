# Game Mechanism: Decisions

Decisions made during the brainstorm debate on the queueing mechanism behind Throughput (2026-10-10). Each one records the choice, why it was made, and what it means for the build.

Concept numbers like [7] refer to the 25 concepts in [knowledge/queueing-mechanisms.html](knowledge/queueing-mechanisms.html).

---

## Summary

| # | Question | Decision |
|---|---|---|
| D1 | What is the project for? | Learning first; shipping is a bonus |
| D2 | Main subjects | Queueing theory and game design (other skills may take shortcuts) |
| D3 | Who learns the theory? | The theory is the hidden engine; players are never pushed to learn it |
| D4 | Play style | Plan → Watch |
| D5 | How levels start | A mix: plain line early, broken designs to fix later |
| D6 | How smart are passengers? | Obedient by default; independent behaviours unlock per level |
| D7 | How simulated time moves | Event list (discrete-event simulation) |
| D8 | Floor space | Always real and limited; full areas block earlier stages |
| D9 | What counts as success | One pass goal + optional extra challenges |
| D10 | Perceived wait | A real measure (happiness), introduced mid-game |

---

## D1. Learning first, shipping is a bonus

- **Choice:** when learning and shipping conflict, learning wins.
- **Context:** the user works on it for fun with no deadline, and may spend time on Blender or Unity as they like.
- **Risk to watch:** with no deadline, the danger is a project with no finish line. Every milestone needs a clear "done" criterion instead of a date.

## D2. Main subjects: queueing theory + game design

- **Choice:** these two get the deep work. Unity polish, art and architecture purity may use shortcuts (asset packs, presets), though the user is free to explore them.

## D3. The theory is the engine, not the lesson

- **Choice:** no forced learning, no quizzes, no stars that test understanding. The result only answers "did the design get through the level?", however the player got there.
- **Means:** accuracy of the simulation is the *developer's* standard. Checking against Erlang C/A is a developer test, not a player feature. A "Why?" panel or prediction may exist as optional flavour.

## D4. Plan → Watch

- **Choice:** the player designs the system, presses Run, and watches. There are no changes during a run. If it fails, they edit and run again.
- **Means:**
  - "Fail" is a run that missed its goal, not a game-over.
  - Speed controls (1×/2×/4×, pause to inspect, skip to results) are essential.
  - Dynamic behaviour must be **set up before the run**, so open/close rules [18] become the core of the rule system, not a late-game extra.
  - The "choose 1 of 2 upgrades" idea from the original notes (a real-time roguelite pattern) no longer fits. Dropped unless revisited.
- **Parked:** how players write rules (settings on objects vs if-then cards vs text code). This will be decided with the game design pillars. The current leaning is settings on objects first, if-then cards later, no text code.

## D5. Level starts: a mix

- **Choice:** early levels start from one plain traditional line, and the player builds improvements. Later levels hand over a badly designed checkpoint to diagnose and fix.
- **Means:** "broken" levels need **locked elements** (e.g. "scanners can't move, only staff and rules"). Otherwise the player deletes everything and the diagnosis puzzle disappears.

## D6. Passengers: obedient by default, independent behaviours unlock

- **Choice:** early levels have passengers who follow the player's rules exactly. The only thing they do on their own is lose patience and leave [17]. Independent behaviours are introduced as level mechanics, each arriving with a tool that handles it:

  | Behaviour unlocked | Tool that answers it |
  |---|---|
  | Lane switching [15] | Shared line, routing rules |
  | Balking [16] | Wait-time signs |
  | Choosing own checkpoint [14] | Information signs, closing shortcuts |

- **Why:** early levels keep clean cause and effect and match textbook formulas. The crowd feels alive later, and paradoxes like Braess [14] become possible.
- **Means (technical):** each passenger behaviour is a **separate module** that a level can switch on.

## D7. Simulation time: event list (discrete-event)

- **Choice:** the simulation keeps a sorted list of future events and jumps from one to the next. It does not update every passenger every tick.
- **Why:**
  - Nearly every chosen mechanism is naturally an event: arrival, service finished, patience runs out, setup done, break starts, breakdown, re-scan.
  - It gives exact validation against Erlang C/A.
  - It runs a whole wave in milliseconds, which enables previews, skip-to-result and "run 20 times" averages.
  - It's the real tool of queueing theory, the user's main subject.
- **Means (technical):**
  - The simulation core is **plain C# with no Unity dependency**, testable headless.
  - Unity only **animates** what the event list says happened, interpolating between event times.
  - Walking (entrance → queue) is modelled as a travel-time delay event.
  - Open/close threshold rules [18] are re-checked whenever a queue length changes, not on a timer. A "for 30 seconds" condition schedules its own check event.

## D8. Floor space: always real and limited

- **Choice:** every waiting area has a capacity set by the barriers the player places. When an area is full, the stage feeding it **stops** (blocking [7]), and arrivals spill back and get unhappy.
- **Why:** it makes "barriers take space" a real trade-off, and it creates blocking, an "aha" only a simulation can show.
- **Means:**
  - Early levels are forgiving simply through generous space. No on/off switch is needed.
  - Before sending someone onward, a stage must check "is there room downstream?" (the blocking-after-service rule).
  - Validate against the formulas using areas large enough never to fill.

## D9. Success: one pass goal + optional extras

- **Choice:** each level has **one main goal** (e.g. "no more than 5 missed flights"). Optional extra challenges sit beside it as ticks, e.g. "…and stay under budget", "…and nobody waits over 10 min". **No stars.**
- **Measures available to levels:** missed flights · average wait · worst-off group · happiness · cost.
- **Why:** the main goal stays pressure-free, as the user wanted. Extras give curious players a reason to optimise and expose the trade-offs between speed, fairness and cost.

## D10. Perceived wait: a real measure, mid-game

- **Choice:** passengers have a happiness value driven by **how long the wait feels** (Maister's psychology [22]), not only the real wait. It's introduced after players master real waits.
- **Tools that affect it:** wait-time signs (less uncertainty), screens (filled time), hiding queue-jumping (fairness), bin pre-load areas (the wait "has started").
- **Why:** it's the game's most distinctive idea. Most queue games only measure speed, and "make it *feel* faster" is a fresh puzzle with a cheap tool set.

---

## Still open

| Topic | Status |
|---|---|
| How players write rules (UI) | Parked until the game design pillars |
| Which signature levels to build | Candidates: blocking [7], priority vs fairness [11], closing a lane helps [14], perceived-wait level [22] |
| Game design pillars and style | Next: topic 2 |
| Software architecture patterns | Next: topic 2, after the pillars |
| Check "Flowline" on Steam (app 5071610) | A close comparable game found in research; verify by hand and study it |
