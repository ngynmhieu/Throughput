# Kingman's Formula (VUT): Variability, Approximately

**One line:** a quick **estimate** of the wait when **both** arrivals and service can be any shape. Wait ≈ **V**ariability × **U**tilization × **T**ime.

## The question it answers

"Passengers arrive in a messy or a smooth way, and service is messy or smooth. Roughly how long is the wait?"

## The setup (G/G/1)

- **G** arrivals: any pattern, described by the CV of the gaps between people, **Ca**
- **G** service: any distribution, with CV **Cs**
- 1 desk

## The formula

$$
W_q \approx \underbrace{\frac{C_a^2 + C_s^2}{2}}_{V\text{: variability}} \times \underbrace{\frac{\rho}{1-\rho}}_{U\text{: utilization}} \times \underbrace{E[S]}_{T\text{: time}}
$$

Note the **≈**: this is an **approximation**, not an exact answer.

**How it relates to the others:**
- When Ca = 1 (random arrivals), Kingman = **P-K** exactly.
- When Ca = 1 and Cs = 1, Kingman = **M/M/1** exactly.

## Worked example

```
ρ = 0.9   E[S] = 1 min   → U × T = 9 / 1 × 1 = 9 min
```

| Arrivals | Ca | Service | Cs | V = (Ca² + Cs²)/2 | Wq ≈ |
|---|---|---|---|---|---|
| Random | 1 | Random | 1 | 1 | 9 min (= M/M/1) |
| Smoother (booked slots) | 0.5 | Regular | 0.5 | 0.25 | **2.25 min** |
| Bunched (a coach group) | 1.5 | Random | 1 | 1.625 | **14.6 min** |

## The three levers

```
      Wait  ≈   V   ×   U   ×   T
                │       │       └─ make each check faster
                │       └───────── add staff (lower ρ)
                └───────────────── make arrivals or service SMOOTHER
```

The interesting lesson: you can cut the wait **without** adding staff, just by reducing variability (smoother arrivals with booked slots, more regular service with an express lane).

## How accurate is it?

- Best when the desk is **busy** (ρ ≥ 0.8): usually within about 10%.
- At low load (ρ around 0.5), it can be off by much more.
- That's why our test is **looser** than the exact formulas.

## What it assumes (when it's wrong)

- One desk. (For c desks there is an extension, Allen–Cunneen, which isn't needed in phase 1.)
- Steady conditions over a long run.
- It's an estimate even when all assumptions hold.

## In Throughput

- The **VUT** idea is the whole game in one line: every player tool works on V, U or T (knowledge page, "Kingman").
- In the engine, it tests **non-random arrivals** (Ca ≠ 1), which the game needs for waves and booked slots.

## How we test it

- Setup: 1 source with gaps drawn from a "mean + CV" distribution (Ca), 1 queue, 1 station (Cs).
- **Accuracy test:** at ρ = 0.8 and 0.9, a few (Ca, Cs) pairs → within **±15%**.
- **Trend test:** halve Cs (Ca fixed) → the wait drops by roughly the ratio Kingman predicts. This checks the *direction and size* of the effect, which matters more for the game than the exact number.
- Slice: **S3**.

## Questions and answers

**Q (2026-10-11): So the Erlang formulas are for "steady" Poisson queues, and Kingman is for queues that don't follow any distribution, where arrivals and service can be messy or smooth?**
A: Mostly right, with two corrections:
1. **"Poisson" and "steady" are different ideas.** *Poisson/exponential* is the **shape** of randomness (fully random, CV = 1); both Erlangs need it for arrivals **and** service (Erlang A also for patience). *Steady* means the average rate doesn't change over time (no flight waves). **Every** formula needs steady, Kingman and P-K included.
2. **Kingman works for *any* distribution, not *no* distribution.** It only needs each side's average and CV, so arrivals and service can each be smooth (CV ≈ 0), random (CV = 1) or messy (CV > 1). The price: it's an estimate, and for one desk.

| Formula | Arrivals | Service | Desks | Exact? |
|---|---|---|---|---|
| M/M/1 | random only | random only | 1 | Exact |
| Erlang C | random only | random only | many | Exact |
| Erlang A | random only | random only (+ random patience) | many | Exact |
| P-K | random only | any | 1 | Exact |
| Kingman | any | any | 1 | Estimate |
| Little's Law | anything | anything | anything | Exact (links L, λ, W only) |

Waves, blocking, breakdowns and manager actions aren't covered by any formula; hand tests and invariants guard those.

**Q (2026-10-11): What does E[S] mean?**
A: The **average service time**. *S* = service time (different for each passenger); *E[…]* = "expected value", the maths name for the long-run average. Example: checks of 0.5, 1.5, 0.8, 1.2, 1.0 min → E[S] ≈ 1.0 min. It's the same information as μ, flipped: E[S] = 1/μ (2 min per check ↔ ½ passenger per minute). In Kingman it's the **T**: V × U is just a number, and × E[S] turns it into minutes. Double the check time at the same busy % and messiness, and the wait doubles. In Throughput: the "make each check faster" lever.

## Status

- [x] Read
- [x] Questions done
- [x] Implemented (`Theory.Kingman` + validation and trend tests)
- [x] Accuracy (±15%) and trend tests pass
