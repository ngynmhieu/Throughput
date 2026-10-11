# Phase 1 Spec: Simulation Core + Validation

**Status:** APPROVED 2026-10-11. Next: implementation plan, then code (AGENTS.md).
**Started:** 2026-10-11 · **Sub-project:** 1 of 4 ([roadmap](../../roadmap.md))
**Code lives in:** `Unity/Assets/Core/` (`Throughput.Core`, no Unity) and `Unity/Assets/Core/Tests/` (`Throughput.Core.Tests`, EditMode)

**This file is also the phase 1 tracker.** Tick boxes as work is done, so nothing is lost between conversations. The [progress table](#progress) at the bottom is the quick view.

---

## 1. Goal

Build the queueing engine as plain C#, and **prove** it is right before any graphics exist.

"Proved" means four kinds of evidence (section 5):
1. exact hand-calculated timelines,
2. agreement with queueing theory,
3. rules that never break,
4. same seed → same timeline.

## 2. Scope

**In:**
- All the structure: network nodes, event loop, capacity and blocking, policy / behaviour / observer interfaces, random streams, service-time distributions with a chosen CV, timeline with snapshots, re-simulate from a snapshot, event deck, results.
- Starter modules: FIFO, shortest-line routing, reneging, threshold open/close rule with setup time, scanner breakdown event, open/close lane manager action.
- Level and plan data (JSON) and the layout compiler.

**Out (later phases):** anything Unity or visual, the plan editor, balking, lane switching, selfish route choice, perceived wait / mood, express and priority lanes, signs, staff moving between stages, costs beyond a simple total. These are added later as **modules**, without changing the core.

## 3. Decisions made for this spec

| # | Decision | Why |
|---|---|---|
| S-D1 | **Full proof ladder**: Little's Law on every run, plus M/M/1, Erlang C, Erlang A, Pollaczek–Khinchine (P-K), Kingman | Erlang alone only proves the CV = 1 world; the game is about variability (decided 2026-10-11) |
| S-D2 | **Two test tiers**: a fast loose tier on every run, a slow strict tier on demand | Fast feedback while coding, real proof before a slice is closed |
| S-D3 | **Own random generator** (small seeded PRNG, e.g. PCG32), never `System.Random` | `System.Random`'s output isn't guaranteed across .NET versions; determinism (P3) must not depend on Unity's runtime |
| S-D4 | **Event order** = (time, event-type priority, sequence number) | Ties always break the same way, so the same inputs give the same timeline |
| S-D5 | Time in **game-seconds as `double`** | Already decided in [simulation-core.md](../../architecture/simulation-core.md) |

## 4. Code layout

```
Assets/Core/
├── Levels/     LevelDef, PlanDef (plain data) + JSON reading
├── Layout/     LayoutCompiler: PlanDef + LevelDef → Network
├── Sim/
│   ├── Network/    Source, Queue, Station, Buffer, Router, Sink, Passenger
│   ├── Events/     Event, EventList (priority queue), Simulator (the loop)
│   ├── Policies/   IDiscipline, IRoutingPolicy, IStaffingRule + starters
│   ├── Behaviours/ IBehaviour + Reneging
│   └── Randomness/ Pcg32, RngStreams, distributions   ← new folder ("Random" would clash with System.Random)
├── Timeline/   TimelineRecord, Timeline, Snapshot
├── Results/    RunStats, Results (goal check)
└── Tests/      one folder per slice + Validation/ + Invariants/
```

## 5. How we prove it works

### 5.1 The proof ladder

Each formula has its own learning page in [knowledge/formulas/](../../game-mechanism/knowledge/formulas/README.md). **Workflow:** the user reads a page and asks questions; only when its "Questions done" box is ticked is that formula implemented and tested.

| Check | Exact? | Covers | Test |
|---|---|---|---|
| **Hand calculations** | Exact | Mechanics: order, blocking, reneging, breakdowns, actions | Exact timeline compare |
| **Little's Law** (L = λW) | Always | **Every** setup, including real levels | Invariant on every run |
| **M/M/1** | Exact | 1 desk, random service | Strict tier |
| **Erlang C** (M/M/c) | Exact | c desks, shared line | Strict tier |
| **Erlang A** (M/M/c+M) | Exact | + passengers giving up | Strict tier |
| **P-K** (M/G/1) | Exact | **Any** service variability, 1 desk | Strict tier |
| **Kingman** (G/G/1) | Approximate | Any arrivals and service | Loose % at high load + trend test |

### 5.2 Invariants (checked after every test run)

- [ ] Conservation: arrived = exited + reneged + still inside
- [ ] Time never goes backward in the timeline
- [ ] No queue or buffer ever exceeds its capacity (D8)
- [ ] A closed or broken station never starts a service
- [ ] Each station serves at most `staff` people at once
- [ ] Little's Law holds for each queue (within a small tolerance over a long run)

### 5.3 Test tiers

| Tier | NUnit category | Runs | Tolerance | Size |
|---|---|---|---|---|
| Fast | (none) | Every time, all tests | Theory within **±10%** | 1 seed, ~20k passengers |
| Strict | `[Category("Strict")]` | Before closing a slice | 99% confidence interval contains the theory value, and error ≤ **3%** | 10+ seeds, ~200k passengers each, warm-up discarded |

(99%, not 95%: with fixed seeds and several cases, a 95% interval would miss by pure luck about 1 time in 20 per case.)

Kingman: **±15%**, only at ρ ≥ 0.8, plus a trend test ("halving the service CV cuts the wait by about what Kingman predicts").

### 5.4 Determinism tests

- [ ] Same seed + plan + actions → **identical** timeline (compared record by record)
- [ ] Different seed → different timeline
- [ ] Re-simulating from a snapshot = the same run without stopping
- [ ] A manager action changes only what it affects (separate random streams)

### 5.5 How you can see it

- [ ] Optional: export a run to CSV, plus a **throwaway** HTML chart (`docs/**/prototypes/`) showing the sim's wait next to the theory curve (the hockey stick).

## 6. Build order: slices

Each slice is **test-first**: write the test from a hand calculation or a formula, watch it fail, make it pass. A slice is closed only when its strict-tier tests pass.

### S0: Foundations
- [ ] `Rng` (PCG32) + `RngStreams` (one named stream per source, derived from the run seed)
- [ ] Distributions: exponential, deterministic, and one with a chosen mean and CV (e.g. gamma or lognormal)
- [ ] Tests: same seed → same numbers; sample mean and CV match the target
- [ ] Shared test helpers: `InvariantChecker`, statistics (mean, confidence interval), theory formulas (M/M/1, Erlang C, Erlang A, P-K, Kingman)
- [ ] Tests for the formulas themselves against published worked examples

### S1: Event loop + 1 queue + 1 desk
- [ ] `EventList`, `Simulator`, `Source`, `Queue` (FIFO), `Station` (1 staff), `Sink`
- [ ] Hand tests: fixed times → exact timeline (e.g. arrivals 0:00, 0:30, 1:00; 2-min service)
- [ ] Event-order tie test (S-D4)
- [ ] Little's Law invariant wired in
- [ ] **Validation: M/M/1** (fast + strict)

### S2: Several desks
- [ ] `Station` with c staff
- [ ] Hand test: 2 desks, 4 passengers
- [ ] **Validation: Erlang C** for c = 2, 3, 5 at ρ = 0.7 and 0.9

### S3: Service variability
- [ ] Service time from any distribution with a chosen CV
- [ ] **Validation: P-K** for CS = 0 (deterministic), 0.5, 1
- [ ] **Validation: Kingman** at ρ ≥ 0.8 + trend test

### S4: Reneging (passengers give up)
- [ ] `IBehaviour` interface + `Reneging` (patience timer, cancelled when service starts)
- [ ] Hand test: patience 1:00, wait would be 1:30 → passenger leaves at 1:00
- [ ] **Validation: Erlang A**

### S5: Chain of stages + blocking
- [ ] `Buffer` with capacity, `Router`, walk time on edges
- [ ] Blocking after service (D8): `Blocked` / `Unblocked`
- [ ] Hand tests: full buffer stops the scanner; space frees → the chain restarts in the right order
- [ ] Invariants on a 3-stage chain (ID check → scanner → exit)

### S6: Timeline, snapshots, re-simulate from now
- [ ] `Timeline` of records; snapshot every 5 game-seconds (full state incl. random-stream states)
- [ ] Re-simulate from any snapshot
- [ ] All determinism tests in 5.4

### S7: Level + plan data and the layout compiler
- [ ] `LevelDef` (waves, passenger mix, event deck, space, goal) and `PlanDef` (placed objects, rules) as plain C# classes
- [ ] JSON reading (library choice: see open question Q1)
- [ ] `LayoutCompiler`: plan + level → network, with clear errors for invalid plans
- [ ] Arrival waves: time-varying arrival rate (flight banks)
- [ ] Tests: sample level JSON → expected network; invalid plan → helpful error

### S8: Starter policies, rules, events, actions
- [ ] Routing: shortest line (`IRoutingPolicy`)
- [ ] Staffing rule: open when queue > X, close when < Y, with setup time (`IStaffingRule`, `CheckRule` event)
- [ ] Event deck: scanner breakdown at a random time from the event-timing stream (`Breakdown` / `Repaired`)
- [ ] Manager action: open/close lane at time t, applied via re-simulate from now
- [ ] Hand tests for each; invariants on all

### S9: Results + fairness
- [ ] `RunStats` observer: waits per group, busy %, throughput, reneges (missed flights)
- [ ] `Results`: pass goal + optional extras (D9)
- [ ] **Fairness test (P3):** one sample level, 200 seeds → a good plan passes on ≥ 95%, a bad plan fails on most
- [ ] Optional: CSV export + throwaway chart (5.5)

## 7. Done when (phase 1 exit)

- [ ] All slices S0–S9 closed
- [ ] Strict tier passes: M/M/1, Erlang C, Erlang A, P-K within 3% (CI contains theory); Kingman within 15% at ρ ≥ 0.8
- [ ] Little's Law and all invariants pass on every test, including the sample level
- [ ] Determinism tests pass
- [ ] Fairness test passes on the sample level
- [ ] `Throughput.Core` still has no Unity references (`SetupCheckTests` green, not inconclusive)
- [ ] Roadmap updated: phase 1 done

## 8. Open questions (to settle before or during the plan)

| # | Question | Current lean |
|---|---|---|
| Q1 | JSON library for `Throughput.Core` (no Unity, so not `JsonUtility`) | Unity's Newtonsoft package (`com.unity.nuget.newtonsoft-json`) |
| Q2 | Which distribution for "any CV": gamma or lognormal? | Decided: gamma (2026-10-11). |
| Q3 | Private field style: `_camelCase` or Unity's `m_camelCase` | Decided: `_camelCase` (2026-10-11). |
| Q4 | Sample level for S7–S9: simple 2-stage checkpoint, one wave, one breakdown | Yes, keep it tiny |

## Progress

| Slice | What | Status |
|---|---|---|
| S0 | Foundations: random, distributions, test helpers | Not started |
| S1 | Event loop, 1 desk, M/M/1 | Not started |
| S2 | Several desks, Erlang C | Not started |
| S3 | Service variability, P-K, Kingman | Not started |
| S4 | Reneging, Erlang A | Not started |
| S5 | Stages, buffers, blocking | Not started |
| S6 | Timeline, snapshots, re-sim | Not started |
| S7 | Level/plan JSON, layout compiler | Not started |
| S8 | Policies, rules, breakdown, manager action | Not started |
| S9 | Results, fairness | Not started |

## Change log

| Date | Change |
|---|---|
| 2026-10-11 | First draft: proof ladder (S-D1), two test tiers (S-D2), slices S0–S9 |
| 2026-10-11 | Formula learning pages written and read; spec approved by the user |
| 2026-10-11 | Plan part 1 written: Randomness folder name, 99% CI for strict tier, Q2 and Q3 decided |
