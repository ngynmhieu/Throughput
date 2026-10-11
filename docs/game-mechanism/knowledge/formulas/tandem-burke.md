
# Burke's Theorem: Desks in a Row

**One line:** when random arrivals pass through an M/M/c desk, the people **leaving** it are also perfectly random (Poisson) at the same rate, so every desk in a chain behaves like its own simple M/M/1 or M/M/c.

## The question it answers

"ID check, then scanner. Does the scanner see a messy, bunched-up stream from the ID desk? How long is the wait at each?"

## The idea

You might expect the ID desk to "shape" the stream (people come out at the ID desk's pace). Burke's theorem says: if arrivals are Poisson and service is exponential, the output is **again Poisson with rate λ**. So:

$$
W_{q,\,\text{stage }k} = \text{the M/M/1 (or Erlang C) wait of stage } k \text{ on its own, with the same } \lambda
$$

$$
W_{q,\,\text{total}} = \sum_k W_{q,\,\text{stage }k}
$$

## Worked example

```
λ = 0.8 per min, ID desk μ = 1, scanner μ = 1 (1 min each, exponential)

ID:      Wq = 0.8 / (1 − 0.8) = 4 min
Scanner: Wq = 4 min (it sees Poisson arrivals at 0.8 too)
Total time in lines: 8 min
```

## What it assumes (when it's wrong)

- Poisson arrivals and **exponential** service at every stage. With regular service (CV < 1) the output is smoother, and the next stage waits **less** than its formula says.
- **Unlimited** room between stages. With a small area, blocking appears (D8) and nothing simple is exact any more.
- People don't leave between stages (no reneging).

## In Throughput

- It's how we check the engine moves people **between** stations correctly: walk-free chain, big areas, two independent stages.
- The game will break it on purpose: limited areas (blocking), regular machines and waves. Then only the simulation knows the answer.

## How we test it

- Setup: Poisson λ = 0.8 → ID (1 staff, exponential, mean 1) → scanner (same) → exit, unlimited lines.
- Measure the mean wait at **each** stage; both should equal 4.
- **Fast tier:** 1 seed, ±10%. **Strict tier:** 20 seeds, 99% CI contains 4 and error ≤ 3%, for each stage.
- Slice: **S5**.

## Questions and answers

*(Add the user's questions and the answers here.)*

## Status

- [ ] Read
- [ ] Questions done
- [x] Implemented (validation test)
- [ ] Strict test passes
