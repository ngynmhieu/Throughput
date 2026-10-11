# M/M/1: One Line, One Desk

**One line:** the exact average wait for one desk, when arrivals and service times are both fully random.

## The question it answers

"One desk, one line, people arrive at random and take a random time each. How long do they wait?"

## The setup

- **M** arrivals: Poisson (random, no pattern; the gaps between people have CV = 1)
- **M** service: exponential (most checks are quick, a few are very long; CV = 1)
- **1** desk, unlimited line, first come first served

## The formulas

How busy the desk is (must be below 1):

$$
\rho = \frac{\lambda}{\mu}
$$

Average wait in line (two ways to write the same thing):

$$
W_q = \frac{\rho}{\mu - \lambda} = \frac{\rho}{1 - \rho} \times E[S]
$$

Average number in line, time in the system, and the chance of having to wait:

$$
L_q = \frac{\rho^2}{1 - \rho} \qquad W = W_q + E[S] \qquad P(\text{wait} > 0) = \rho
$$

## Worked example

```
λ = 0.9 per minute     μ = 1 per minute (1 min each)

ρ  = 0.9 / 1                = 0.9   (busy 90%)
Wq = 0.9 / (1 − 0.9)        = 9 min
Lq = 0.81 / 0.1             = 8.1 people
W  = 9 + 1                  = 10 min
```

Little's Law check: Lq = λ × Wq = 0.9 × 9 = 8.1 ✔

## The hockey stick

Same desk (1 min per person), only λ changes:

| ρ (busy) | 50% | 80% | 90% | 95% | 99% |
|---|---|---|---|---|---|
| Wq (min) | 1 | 4 | 9 | 19 | 99 |

```
Wq
 │                              ╱
 │                            ╱
 │                         _╱
 │                    __—‾
 │_______________——‾‾
 └────────────────────────────── ρ
 0      0.5      0.8  0.9   1
```

Going from 80% to 90% busy more than **doubles** the wait. Near 100%, the wait explodes.

## What it assumes (when it's wrong)

- Arrivals are steady and random **all the time**. Real flights come in waves, so real levels break this.
- Service is exponential (CV = 1). Real checks are more regular, so the real wait is usually **lower** than M/M/1 says (see [P-K](pollaczek-khinchine.md)).
- It's a **long-run average**. A short run can be far off just by luck.

## In Throughput

- It's the **hockey stick**: the core feeling that a "nearly full" desk is a disaster (knowledge page, concept "Utilization").
- It's the simplest possible check of the engine: event loop, one queue, one desk, random numbers.

## How we test it

- Setup: 1 source (Poisson, λ), 1 queue, 1 station (1 staff, exponential, μ = 1).
- Cases: ρ = 0.5, 0.8, 0.9.
- Measure the average Wq (skip a warm-up period at the start, when the line starts empty).
- **Fast tier:** 1 seed, ~20k passengers, within ±10%.
- **Strict tier:** 10+ seeds, ~200k passengers each; the 95% confidence interval contains the theory value and the error is ≤ 3%.
- Note: at ρ = 0.9 the numbers jump around a lot (long, slow swings), which is why the strict tier needs many passengers.
- Slice: **S1**.

## Questions and answers

**Q (2026-10-11): What does "hockey stick" mean?**
A: The nickname for the shape of the wait-vs-busy graph. Like a hockey stick, it has a long flat **handle** (0–70% busy: the wait barely changes) and a **blade** that bends sharply up (90%+: the wait explodes, 9 → 19 → 99 min). It happens because a nearly-full desk has no spare time to catch up after a burst of arrivals. In Throughput: adding staff on the blade cuts the wait a lot; on the handle it does almost nothing, so the player must know where they are on the stick before paying for a fix (P1).

## Status

- [x] Read
- [x] Questions done
- [x] Implemented (`Theory.MM1` + validation test)
- [x] Strict test passes
