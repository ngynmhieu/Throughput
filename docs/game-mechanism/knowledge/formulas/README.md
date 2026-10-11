# Queueing Formulas

The formulas we use to **prove the simulation engine is right** in phase 1 ([spec](../../../superpowers/specs/2026-10-11-phase-1-core-design.md), section 5).

**Remember:** the formulas don't run in the game. They are the **referee**: on simple setups where the right answer is known, the engine must match them. Then we can trust the engine on real levels, where no formula exists.

## Shared symbols

Every file uses the same letters.

| Symbol | Say it | Meaning | Example |
|---|---|---|---|
| λ | lambda | **Arrival rate**: passengers arriving per minute | 0.9 / min |
| μ | mu | **Service rate** of one desk: passengers served per minute | 1 / min (= 1 min each) |
| E[S] | "expected S" | **Average service time** = 1/μ. *S* = service time, *E[…]* = expected value (long-run average) | 1 min |
| c | — | Number of desks (servers) sharing one line | 2 |
| ρ | rho | **Utilization**: how busy each desk is = λ / (c·μ). Must be below 1, or the line grows forever | 0.9 = busy 90% of the time |
| Wq | — | Average **wait in line** (before service starts) | 9 min |
| W | — | Average **time in the system** = Wq + E[S] | 10 min |
| Lq | — | Average **number waiting in line** | 8.1 people |
| L | — | Average **number in the system** (line + at desks) | 9 people |
| CV | — | **Coefficient of variation** = standard deviation ÷ mean. How messy a time is | 0 = always the same, 1 = very random |
| Ca, Cs | — | CV of the gaps between arrivals (a) and of service times (s) | Cs = 0.5 |
| θ | theta | **Abandonment rate** = 1 / average patience | 1 / min |

**Kendall notation** (the A/B/c names): `M/M/1` = arrival pattern / service pattern / number of desks.
- **M** = "memoryless" random (Poisson arrivals, exponential times, CV = 1)
- **G** = general: any distribution
- **D** = deterministic: always the same (CV = 0)
- `+M` at the end = people give up after a random (exponential) patience

## Learning order and status

Read them in this order; each one builds on the one before.

| # | Formula | File | What it proves | Slice | Read | Questions done | Implemented | Strict test passes |
|---|---|---|---|---|---|---|---|---|
| 1 | Little's Law | [littles-law.md](littles-law.md) | The bookkeeping, on **every** setup | S1 | [x] | [x] | [x] | [x] |
| 2 | M/M/1 | [mm1.md](mm1.md) | One line, one desk, randomness | S1 | [x] | [x] | [x] | [x] |
| 3 | Erlang C | [erlang-c.md](erlang-c.md) | Many desks sharing one line | S2 | [x] | [x] | [x] | [x] |
| 4 | Pollaczek–Khinchine | [pollaczek-khinchine.md](pollaczek-khinchine.md) | Service variability, exactly | S3 | [x] | [x] | [x] | [x] |
| 5 | Kingman | [kingman.md](kingman.md) | Arrival + service variability, approximately | S3 | [x] | [x] | [x] | [x] |
| 6 | Erlang A | [erlang-a.md](erlang-a.md) | People giving up | S4 | [x] | [x] | [x] | [x] |
| 7 | Burke (tandem) | [tandem-burke.md](tandem-burke.md) | Moving between desks in a row | S5 | [ ] | [ ] | [x] | [x] |

**Workflow (agreed 2026-10-11):** read a file → ask questions (answers get added to its "Questions and answers" section) → when it's clear, tick "Questions done" → then implement its logic and tests.

**Related:** the concept pages in [../queueing-mechanisms.html](../queueing-mechanisms.html) (utilization, variability, Kingman, Little's Law, Erlang C/A) explain the *ideas*; these files go into the *math and the tests*.
