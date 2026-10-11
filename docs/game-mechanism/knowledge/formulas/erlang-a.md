# Erlang A: When People Give Up

**One line:** the exact share of people who **give up and leave**, and the wait for the rest, when passengers have limited patience.

The "A" stands for **Abandonment**. Also written **M/M/c+M**.

## The question it answers

"Passengers will only wait so long before leaving (a missed flight). How many give up, and how long do the others wait?"

## The setup (M/M/c+M)

- Poisson arrivals (λ), exponential service (μ per desk), **c** desks, one shared line
- **+M:** each person in the line has a random **patience**, exponential with average 1/θ. If they haven't started service by then, they leave.
- Once someone reaches a desk, they stay until done.

## The big difference from Erlang C

With abandonment, **the line can't explode**, even if the desks are overloaded (ρ ≥ 1). The longer the line, the faster people leave, so it settles at some size. Erlang C would say "infinite wait"; Erlang A gives a real number.

## How it's calculated: the state ladder

Erlang A has no short formula like the others. Instead, we compute it **exactly** with a simple method: think of each possible number of people in the system as a step on a ladder.

```
 state n = number of people in the system (in line + at desks)

   n=0 ⇄ n=1 ⇄ n=2 ⇄ n=3 ⇄ …
       →   up rate:   λ                         (someone arrives)
       ←   down rate: (desks working) × μ       (someone finishes)
                    + (people in line) × θ      (someone gives up)
```

The up rate is always λ. The down rate at step *n* (desks working × μ, plus people in line × θ):

$$
d_n = \min(n, c)\,\mu \;+\; \max(n - c,\, 0)\,\theta
$$

In the long run, the flow up between two steps equals the flow down:

$$
P(n)\,\lambda = P(n+1)\,d_{n+1} \quad\Longrightarrow\quad P(n+1) = P(n) \cdot \frac{\lambda}{d_{n+1}}
$$

So start with any number for P(0), walk up the ladder until the numbers are tiny, then scale so they all add up to 1.

From the step chances we get everything:

$$
L_q = \sum_{n} P(n)\,\max(n - c,\, 0) \qquad \text{(average number in line)}
$$

$$
\text{give-up rate} = \theta\,L_q \qquad P(\text{give up}) = \frac{\theta\,L_q}{\lambda} \qquad W_q = \frac{L_q}{\lambda}
$$

The last one is Little's Law, averaged over all arrivals.

This is exact, not an approximation. The computer just walks the ladder (a few hundred steps).

## Worked example (a neat special case)

```
c = 1 desk    λ = 1 per minute    μ = 1 per minute    θ = 1 per minute (average patience 1 min)

ρ = 1: the desk is 100% loaded. Without giving up, the line would grow forever.
```

Down rate at step n: `1 × 1 + (n − 1) × 1 = n`. So each step = the one before × 1/n:

$$
P(n) = \frac{P(0)}{n!} \qquad P(0) = e^{-1} \approx 0.368
$$

This is a Poisson distribution with mean 1.

| Result | Value |
|---|---|
| Lq (average in line) | E[n] − 1 + P(0) = 1 − 1 + 0.368 = **0.368 people** |
| Give-up rate | θ × Lq = **0.368 per minute** |
| P(give up) | 0.368 / 1 = **36.8% of passengers leave** |
| Wq (over everyone) | 0.368 / 1 = **0.37 min** |
| Check: served per minute | 1 − 0.368 = 0.632 = chance the desk is busy, 1 − P(0) ✔ |

So: the line stays short (under 1 person on average), but more than a third of passengers give up. **A short line isn't always a good line.**

## What it assumes (when it's wrong)

- Patience is exponential (anyone can give up at any moment, equally likely). Real patience is more like "fine for 10 minutes, then nervous". The engine can model that later; the test uses exponential so the answer is exact.
- People who reach a desk never leave.
- Same Poisson/exponential assumptions as Erlang C.

## In Throughput

- **Reneging = missed flights** (D6, "always on"), the main way a level fails.
- It's the "hidden cost" lesson: a line that looks short can still be failing, because people are leaving it.
- In the engine, it tests the **Reneging behaviour**: the patience timer starts on `JoinQueue` and is cancelled on `StartService`.

## How we test it

- Setup: 1 Poisson source, 1 queue, 1 station with c staff, Reneging on with exponential patience.
- Cases: the special case above (c = 1, λ = μ = θ = 1), plus c = 3 at ρ = 0.9 and ρ = 1.2 (overloaded) with average patience 2 min.
- Compare **P(give up)** and **Wq**.
- Tiers: fast ±10%; strict: CI contains theory, error ≤ 3%.
- Slice: **S4**.

## Questions and answers

**Q (2026-10-11): I don't understand "the up rate is always λ; the down rate at step n is desks working × μ plus people in line × θ".**
A: Step *n* = how many people are inside right now (waiting + at a desk). Only three things change it: an **arrival** (+1), a **finish** at a desk (−1), a **give-up** in the line (−1). "Rate" = how often per minute.
- **Up rate = λ** at every step: people keep arriving from outside no matter how many are inside.
- **Down rate** = finishes + give-ups. Only desks with someone at them work: `min(n, c)` desks, each finishing μ per minute. Only people in the line can give up: `max(n − c, 0)` of them, each at θ per minute.

Example: c = 2, μ = 1, θ = 0.5, λ = 3.

| n | Working desks | Waiting | Down rate | Up rate |
|---|---|---|---|---|
| 1 | 1 | 0 | 1 | 3 |
| 2 | 2 | 0 | 2 | 3 |
| 5 | 2 | 3 | 2 + 1.5 = 3.5 | 3 |
| 10 | 2 | 8 | 2 + 4 = 6 | 3 |

Below n ≈ 4 the crowd grows (up > down); above it, it shrinks (down > up). The desks alone (2/min) could never handle 3 arrivals/min, but giving up keeps the crowd around 4. That's why the line can't explode.

## Status

- [x] Read
- [x] Questions done
- [x] Implemented (`Theory.ErlangA` state ladder + validation test)
- [x] Strict test passes
