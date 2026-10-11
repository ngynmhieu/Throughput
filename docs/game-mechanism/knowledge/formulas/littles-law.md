# Little's Law

**One line:** the number of people inside = how fast they arrive × how long they stay.

## The question it answers

"If I know two of these three numbers (how many are inside, how fast they come, how long they stay), what is the third?"

## The formula

$$
L = \lambda \, W \qquad \text{(whole system: line + desks)}
$$

$$
L_q = \lambda \, W_q \qquad \text{(just the line)}
$$

| Symbol | Meaning |
|---|---|
| L | Average number of people in the system |
| λ | Arrival rate (people per minute) |
| W | Average time each person spends in the system |

## Worked example

```
Passengers arrive at 0.8 per minute.
Each one spends 5 minutes in the line on average.

Lq = 0.8 × 5 = 4 people in the line, on average.
```

Check it backwards: if you count an average of 4 people in the line and 0.8 arrive per minute, each one must wait 4 ÷ 0.8 = 5 minutes.

## Picture

```
 arrive 0.8/min          stay 5 min each           leave
 ──────────────►   [ ● ● ● ● ]  (about 4 inside)   ──────►
```

Think of a bathtub: water in per minute × how long each drop stays = how much water is in the tub.

## Why it's special

**It assumes almost nothing.** It is true for:
- any arrival pattern (random, waves, scheduled),
- any service time distribution,
- any number of desks,
- any part of the system you draw a box around (one line, one desk, the whole checkpoint).

The only conditions: measure over a long enough period, and the system must not grow forever (people who come in eventually leave).

One more useful form, applied to the **desks** only:

$$
\text{average number of busy desks} = \lambda \cdot E[S]
$$

## In Throughput

- It links what the player **sees** (how long the line is) to what they **feel** (how long they wait). The wait sign and the line on screen must agree.
- It's the only check that works on **real game levels** with waves, blocking and breakdowns, where no other formula applies.

## How we test it

- **Invariant**, checked after **every** test run (spec 5.2), for each queue and for the whole system.
- The engine records: arrivals (→ λ), each person's wait (→ Wq), and the area under the queue-length graph over time (→ Lq).
- Pass if `Lq` and `λ × Wq` agree within a small tolerance (e.g. 2%) over a long run.
- **Careful at the edges:** people still inside when the run ends haven't finished their wait. The test counts only a window where the system starts and ends (nearly) empty, or measures over a long run so the edge effect is small.
- Slice: **S1**.

## Questions and answers

*(Add the user's questions and the answers here.)*

## Status

- [x] Read
- [x] Questions done
- [ ] Implemented (invariant in `InvariantChecker`)
- [ ] Passes on every test
