# Erlang C: Many Desks, One Shared Line

**One line:** the exact chance of waiting, and the average wait, when **c** desks serve **one shared line**.

## The question it answers

"I have c desks and one line feeding all of them. What's the chance a passenger has to wait, and for how long?"

## The setup (M/M/c)

- Poisson arrivals (λ), exponential service (μ per desk)
- **c** identical desks, **one** shared line: the next person goes to whichever desk frees up first
- Nobody gives up (that's [Erlang A](erlang-a.md))

## The formulas

The "offered load" *a* (how many desks the work needs on average) and how busy each desk is (must be below 1):

$$
a = \frac{\lambda}{\mu} \qquad \rho = \frac{a}{c}
$$

The chance that an arriving passenger has to wait:

$$
P(\text{wait}) = \frac{\dfrac{a^c}{c!} \cdot \dfrac{1}{1-\rho}}{\displaystyle\sum_{k=0}^{c-1} \frac{a^k}{k!} \;+\; \dfrac{a^c}{c!} \cdot \dfrac{1}{1-\rho}}
$$

The average wait in line (over everyone, including those who don't wait):

$$
W_q = \frac{P(\text{wait})}{c\,\mu - \lambda}
$$

*k!* = k factorial = 1 × 2 × … × k (and 0! = 1). The Σ means "add up the terms for k = 0, 1, …, c−1".

**Don't panic about the big fraction.** It's just "how likely is it that all c desks are busy". Top: the "all busy" part. Bottom: all possible cases added up.

## Worked example

```
c = 2 desks    λ = 1.8 per minute    μ = 1 per minute

a = 1.8            ρ = 1.8 / 2 = 0.9   (each desk busy 90%)

Top:     a² / 2! = 3.24 / 2 = 1.62     → × 1/(1 − 0.9) = 1.62 × 10 = 16.2
Bottom:  (a⁰/0! + a¹/1!) + 16.2 = (1 + 1.8) + 16.2 = 19.0

P(wait) = 16.2 / 19.0 = 0.853         → 85% of passengers wait
Wq      = 0.853 / (2 × 1 − 1.8) = 0.853 / 0.2 = 4.26 min
```

## Pooling: why one shared line wins

Same work, same staff (each desk busy 90%), three layouts:

| Layout | Wait Wq |
|---|---|
| **Two separate lines**, 2 desks (each is an M/M/1 with λ = 0.9) | **9.0 min** |
| **One shared line**, 2 desks (Erlang C, above) | **4.26 min** |
| One desk twice as fast (M/M/1, μ = 2, λ = 1.8) | 4.5 min |

```
Separate lines:            Shared line:
●●●●●●●● → [desk]          ●●●● ─┬→ [desk]
●●●●●●●● → [desk]                └→ [desk]
  9.0 min wait                4.26 min wait
```

Separate lines waste time: one desk can sit idle while the other line is full. A shared line never lets a desk go idle while someone waits.

## What it assumes (when it's wrong)

- Exponential service (CV = 1) and steady random arrivals, the same as M/M/1.
- Everyone waits forever. In real checkpoints, some people give up, which lowers the wait for others ([Erlang A](erlang-a.md)).
- All desks are identical.

## In Throughput

- **Pooling** (one shared line vs a line per lane) and the dedicated-lane trade-off (knowledge page, "Pooling").
- **Staffing:** "How many desks does this wave need?" Erlang C gives a fast first answer for level balancing.
- In the engine it tests a `Station` with **several staff** and the shared queue.

## How we test it

- Setup: 1 Poisson source, 1 queue, 1 station with c staff.
- Cases: c = 2, 3, 5 at ρ = 0.7 and 0.9.
- Compare both **P(wait)** (share of passengers who waited > 0) and **Wq**.
- Bonus test: the pooling table above (two separate M/M/1s vs one M/M/2) shows the real "shared line wins" result in our own engine.
- Tiers: fast ±10%; strict: confidence interval contains theory, error ≤ 3%.
- Slice: **S2**.

## Questions and answers

*(Add the user's questions and the answers here.)*

## Status

- [x] Read
- [x] Questions done
- [ ] Implemented (`Theory.ErlangC` + validation test)
- [ ] Strict test passes
