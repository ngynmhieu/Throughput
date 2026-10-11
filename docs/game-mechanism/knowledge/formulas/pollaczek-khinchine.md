# Pollaczek–Khinchine (P-K): Service Variability, Exactly

**One line:** the exact wait for one desk when service times can be **any** shape, so we can see how **messy service** makes the line longer.

Pronounced roughly "po-LAH-chek KHIN-chin". Usually just "P-K".

## The question it answers

"Two desks are equally fast on average, but one is very regular and one is messy. How much longer is the line at the messy one?"

## The setup (M/G/1)

- Poisson arrivals (λ), still random
- **G**: service times of **any** distribution, described by their mean E[S] and their CV, Cs
- 1 desk, unlimited line, first come first served

## The formula

$$
W_q = \underbrace{\frac{\rho}{1-\rho}}_{\text{busy part}} \times \underbrace{\frac{1 + C_s^2}{2}}_{\text{messy part}} \times \underbrace{E[S]}_{\text{time part}}
$$

Compare with M/M/1:

$$
W_q = \frac{\rho}{1-\rho} \times E[S]
$$

P-K just adds the **messy part**. When Cs = 1 (exponential), the messy part is (1 + 1)/2 = 1, and P-K becomes exactly M/M/1.

## Worked example

```
λ = 0.9 per minute     E[S] = 1 min     ρ = 0.9     → busy part = 0.9 / 0.1 = 9
```

| Service | Cs | Messy part (1 + Cs²)/2 | Wq |
|---|---|---|---|
| Machine-like: always exactly 1 min (M/D/1) | 0 | 0.5 | **4.5 min** |
| Fairly regular (e.g. a scanner belt) | 0.5 | 0.625 | **5.6 min** |
| Fully random (M/M/1) | 1 | 1 | **9 min** |
| Very messy (e.g. some passengers with big problems) | 2 | 2.5 | **22.5 min** |

Same average speed, same busy %, and the wait still goes from **4.5 to 22.5 minutes**, only because of how *messy* the service is.

## Picture

```
Regular desk  (Cs = 0):   |■|■|■|■|■|■|     every check 1 min → short line
Messy desk    (Cs = 2):   |■|■■■■■■|■|■|    one long check blocks everyone behind it
```

## What it assumes (when it's wrong)

- Arrivals must still be **random** (Poisson). If arrivals are also regular or bunched, use [Kingman](kingman.md).
- One desk only.
- Long-run average, like the others.

## In Throughput

- **Service variability** is a core lesson (knowledge page, "Service variability"): fast and slow passenger types, a regular scanner vs a messy ID check, and why an express lane for "no bags" helps.
- In the engine, it tests the **service-time distributions** with a chosen CV (slice S3), which the game needs anyway, since a scanner and an ID desk shouldn't behave the same.

## How we test it

- Setup: 1 Poisson source, 1 queue, 1 station; service time from our "mean + CV" distribution.
- Cases: Cs = 0 (deterministic), 0.5, 1, at ρ = 0.8 and 0.9.
- **This one is exact**, so it gets the same strict check as M/M/1: CI contains theory, error ≤ 3%.
- It also tests the distribution itself: if the gamma (or lognormal) sampler had the wrong CV, P-K would fail.
- Slice: **S3**.

## Questions and answers

*(Add the user's questions and the answers here.)*

## Status

- [x] Read
- [x] Questions done
- [x] Implemented (`Theory.PollaczekKhinchine` + validation test)
- [x] Strict test passes
