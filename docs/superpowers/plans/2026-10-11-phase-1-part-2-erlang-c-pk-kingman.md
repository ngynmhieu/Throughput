# Phase 1 Part 2: Several Desks and Service Variability (S2 + S3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prove the engine is right for several desks sharing one line (Erlang C) and for any service and arrival variability (P-K, Kingman), closing slices S2 and S3.

**Architecture:** The engine already supports both features: `Station` takes any `staff ≥ 1`, and `Station`/`RenewalArrivals` take any `IDistribution` (`Distribution.FromMeanCv`). So this part is almost all **tests**: one hand test, then validation tests against the formulas already in `Theory`. First we pull the shared "measure a mean wait" and "strict check" code out of `MM1ValidationTests` so every new validation reuses it.

**Tech Stack:** Unity 6000.3.26f1, C#, NUnit (EditMode), Unity CLI via `scripts/run-tests.sh`.

**Spec:** [docs/superpowers/specs/2026-10-11-phase-1-core-design.md](../specs/2026-10-11-phase-1-core-design.md) (§5.3 test tiers, §6 slices S2 and S3). Formula pages: [docs/game-mechanism/knowledge/formulas/](../../game-mechanism/knowledge/formulas/).

## Global Constraints

- `Throughput.Core` never references Unity (plain C#, `noEngineReferences`). Tests live in `Throughput.Core.Tests`.
- Fast tier: no category, runs every time, theory within **±10%**, 1 seed.
- Strict tier: `[Category("Strict")]`, **20** replications, the **99%** confidence interval contains the theory value **and** error ≤ **3%**, warm-up discarded.
- Kingman: **±15%**, only at ρ ≥ 0.8, plus a trend test.
- Every validation file also runs `InvariantChecker.AssertAll` on one random run.
- Private fields `_camelCase`. Namespaces `Throughput.Core.Tests.<Folder>`.
- The Unity editor must be **closed** to run `scripts/run-tests.sh` (batch mode locks the project).
- Git: commit locally after each task; **push only when the user asks**. Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Exact ties with several desks.** Deterministic service + passengers arriving together means two `EndService` events at the same time; FIFO and the staff limit must still hold. Pinned by the hand test in Task 2.
2. **More desks than people.** At low load with c = 5, most desks are idle; `Busy` must never go below 0 or above c, and Little's Law must still hold. Pinned by the invariants test in Task 2 (c = 5, ρ = 0.3).
3. **Deterministic service (Cs = 0) with random arrivals.** Every service ends at an exact multiple, so ties with arrivals are common. Pinned by the invariants test in Task 3.
4. **Smooth arrivals (Ca < 1) through `RenewalArrivals`.** The first gap and the stop time must still work with a gamma gap. Pinned by the invariants test in Task 4.
5. **Warm-up long enough at high load with several desks.** c = 5 at ρ = 0.9 starts empty and takes a while to fill; a too-short warm-up biases the mean low. Covered by the strict Erlang C case (Task 2). If it misses low, raise the warm-up, not the tolerance.

---

## File Structure

| File | Change | Responsibility |
|---|---|---|
| `Unity/Assets/Core/Tests/Support/Experiments.cs` | Modify | Add `MeasureMeanWait` (one run → mean wait after warm-up) |
| `Unity/Assets/Core/Tests/Support/StrictCheck.cs` | Create | The strict-tier rule in one place: replicate, CI, 3% |
| `Unity/Assets/Core/Tests/Validation/MM1ValidationTests.cs` | Modify | Use the two helpers (behaviour unchanged) |
| `Unity/Assets/Core/Tests/Engine/SingleStationTests.cs` | Modify | Hand test: 2 desks, 4 passengers |
| `Unity/Assets/Core/Tests/Validation/ErlangCValidationTests.cs` | Create | Erlang C fast + strict + invariants |
| `Unity/Assets/Core/Tests/Validation/PollaczekKhinchineValidationTests.cs` | Create | P-K fast + strict + invariants |
| `Unity/Assets/Core/Tests/Validation/KingmanValidationTests.cs` | Create | Kingman accuracy + trend + invariants |
| Spec, formula pages, roadmap | Modify | Tick boxes, status (Task 5) |

Unity creates the `.meta` file for each new `.cs` when it imports the project (the test run does this). Commit the `.meta` files with their `.cs` files.

---

### Task 1: Shared measurement helpers

**Files:**
- Modify: `Unity/Assets/Core/Tests/Support/Experiments.cs`
- Create: `Unity/Assets/Core/Tests/Support/StrictCheck.cs`
- Modify: `Unity/Assets/Core/Tests/Validation/MM1ValidationTests.cs`

**Interfaces:**
- Consumes: `Experiments.RunSingleStation(ulong seed, IDistribution gap, IDistribution service, int staff, double stopTime, bool keepEntries)`, `Experiments.MeanWait(RunLog, double warmup)`, `Stats.Mean(IReadOnlyList<double>)`, `Stats.ConfidenceInterval99(IReadOnlyList<double>)`.
- Produces:
  - `Experiments.MeasureMeanWait(ulong seed, IDistribution gap, IDistribution service, int staff, int passengers) → double`. Runs about `passengers` arrivals and skips the first ~1000 arrivals as warm-up.
  - `StrictCheck.AssertMeanMatches(Func<ulong, double> measure, double theory, string label)`. Runs 20 replications with seeds 1000…1019 and asserts that the CI contains the theory value and the error is ≤ 3%.

This is a refactor, so there is no new failing test. The existing M/M/1 tests are the safety net and must stay green with the **same numbers** (same seeds, same sizes).

- [ ] **Step 1: Add `MeasureMeanWait` to `Experiments`**

Add this method inside `Experiments` (after `MeanWait`):

```csharp
        /// <summary>
        /// One run of about <paramref name="passengers"/> arrivals; returns the mean wait in line,
        /// skipping the first ~1000 arrivals (the line starts empty, which would bias the mean low).
        /// </summary>
        public static double MeasureMeanWait(ulong seed, IDistribution gap, IDistribution service, int staff, int passengers)
        {
            var log = RunSingleStation(seed, gap, service, staff, stopTime: passengers * gap.Mean, keepEntries: false);
            return MeanWait(log, warmup: 1000.0 * gap.Mean);
        }
```

- [ ] **Step 2: Create `StrictCheck.cs`**

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Throughput.Core.Tests.Support
{
    /// <summary>The strict tier (spec 5.3): 20 seeds, the 99% CI contains the theory value, and error ≤ 3%.</summary>
    public static class StrictCheck
    {
        public const int Replications = 20;

        public static void AssertMeanMatches(Func<ulong, double> measure, double theory, string label)
        {
            var means = new List<double>();
            for (int i = 0; i < Replications; i++)
                means.Add(measure((ulong)(1000 + i)));

            double mean = Stats.Mean(means);
            var (low, high) = Stats.ConfidenceInterval99(means);
            string detail = $"{label}: measured {mean:F4} (99% CI {low:F4}–{high:F4}) vs theory {theory:F4}";
            TestContext.WriteLine(detail);

            Assert.That(theory, Is.InRange(low, high), detail);
            Assert.That(mean, Is.EqualTo(theory).Within(3).Percent, detail);
        }
    }
}
```

- [ ] **Step 3: Rewrite `MM1ValidationTests.cs` to use them**

Replace the whole file with:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Validation
{
    /// <summary>
    /// The engine against the M/M/1 formula Wq = ρ / (μ − λ) (docs/game-mechanism/knowledge/formulas/mm1.md).
    /// μ = 1 per time unit, so λ = ρ.
    /// </summary>
    public class MM1ValidationTests
    {
        private const double Mu = 1.0;

        private static double Measure(ulong seed, double rho, int passengers) =>
            Experiments.MeasureMeanWait(seed, new Exponential(1.0 / (rho * Mu)), new Exponential(1.0 / Mu), staff: 1, passengers);

        [TestCase(0.5)]
        [TestCase(0.8)]
        public void Fast_MeanWaitWithin10Percent(double rho)
        {
            double measured = Measure(7UL, rho, 200_000);
            double theory = Theory.MM1Wq(rho * Mu, Mu);
            string detail = $"ρ = {rho}: measured {measured:F3} vs theory {theory:F3}";
            TestContext.WriteLine(detail);
            Assert.That(measured, Is.EqualTo(theory).Within(10).Percent, detail);
        }

        [Test, Category("Strict")]
        [TestCase(0.5)]
        [TestCase(0.8)]
        [TestCase(0.9)]
        public void Strict_MeanWaitWithin3PercentAndInsideCI(double rho)
        {
            StrictCheck.AssertMeanMatches(seed => Measure(seed, rho, 300_000), Theory.MM1Wq(rho * Mu, Mu), $"M/M/1 ρ = {rho}");
        }

        [Test]
        public void Invariants_HoldOnRandomRun()
        {
            var log = Experiments.RunSingleStation(11UL, new Exponential(1.25), new Exponential(1.0),
                staff: 1, stopTime: 5_000.0, keepEntries: true);
            InvariantChecker.AssertAll(log, new Dictionary<string, int> { { "desk", 1 } });
        }
    }
}
```

The old code had `stopTime = passengers / λ` and `warmup = 1000 / λ`. With `gap.Mean = 1/λ` these are the same values, so the printed numbers must not change.

- [ ] **Step 4: Run the fast suite**

Run: `bash scripts/run-tests.sh`
Expected: `total 59, passed 59, failed 0`. The output prints `ρ = 0.5: measured 1.013 vs theory 1.000` and `ρ = 0.8: measured 3.978 vs theory 4.000`, the same as before the refactor.

- [ ] **Step 5: Run the strict suite**

Run: `bash scripts/run-tests.sh --strict`
Expected: 3 passed, with means 0.998 / 3.979 / 8.961 as before.

- [ ] **Step 6: Commit**

```bash
git add Unity/Assets/Core/Tests/Support Unity/Assets/Core/Tests/Validation/MM1ValidationTests.cs
git commit -m "Extract MeasureMeanWait and StrictCheck test helpers"
```

---

### Task 2: S2, several desks (Erlang C)

**Files:**
- Modify: `Unity/Assets/Core/Tests/Engine/SingleStationTests.cs`
- Create: `Unity/Assets/Core/Tests/Validation/ErlangCValidationTests.cs`

**Interfaces:**
- Consumes: `Experiments.MeasureMeanWait`, `StrictCheck.AssertMeanMatches` (Task 1), `Theory.ErlangCWq(double lambda, double mu, int c)`, the private `Build(int staff, double service, params double[] arrivals)` and `CheckInvariants(RunLog, int staff)` helpers in `SingleStationTests`.
- Produces: nothing new for later tasks.

Setup for Erlang C: μ = 1 per desk, c desks, λ = ρ·c (so each desk is busy ρ of the time).

- [ ] **Step 1: Write the hand test (2 desks, 4 passengers)**

Add to `SingleStationTests` (after `TwoStaff_ServeTwoAtOnce`):

```csharp
        [Test]
        public void TwoStaff_FourPassengers_HandTimeline()
        {
            // 2 desks, 100 s each. A, B, C arrive at 0; D at 50.
            // A, B: 0–100 (wait 0). Both desks free at 100 (two EndService at the same time):
            // the first frees a desk for C, the second for D, so C still goes before D (FIFO).
            // C: 100–200 (wait 100). D: 100–200 (wait 50).
            var (runner, log) = Build(2, 100.0, 0.0, 0.0, 0.0, 50.0);
            runner.Run();

            var p = log.Passengers;
            Assert.That(p.Select(r => r.ServiceStart), Is.EqualTo(new[] { 0.0, 0.0, 100.0, 100.0 }));
            Assert.That(p.Select(r => r.ServiceEnd), Is.EqualTo(new[] { 100.0, 100.0, 200.0, 200.0 }));
            Assert.That(p.Select(r => r.Wait), Is.EqualTo(new[] { 0.0, 0.0, 100.0, 50.0 }));
            Assert.That(runner.Now, Is.EqualTo(200.0));
            CheckInvariants(log, staff: 2);
        }
```

- [ ] **Step 2: Run it**

Run: `bash scripts/run-tests.sh Throughput.Core.Tests.Engine`
Expected: PASS. The engine already supports several staff, so this test pins behaviour that exists instead of driving new code. **Check that it can fail:** temporarily change the expected waits to `{ 0.0, 0.0, 50.0, 100.0 }`, run it, see it FAIL, then put the values back. If it fails with the correct numbers, stop and report: that's an engine bug, and fixing it is a separate conversation.

- [ ] **Step 3: Create `ErlangCValidationTests.cs`**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Validation
{
    /// <summary>
    /// The engine against Erlang C (M/M/c): c desks share one line (docs/game-mechanism/knowledge/formulas/erlang-c.md).
    /// μ = 1 per desk, λ = ρ × c.
    /// </summary>
    public class ErlangCValidationTests
    {
        private const double Mu = 1.0;

        private static double Measure(ulong seed, int c, double rho, int passengers) =>
            Experiments.MeasureMeanWait(seed, new Exponential(1.0 / (rho * c * Mu)), new Exponential(1.0 / Mu), staff: c, passengers);

        private static double TheoryWq(int c, double rho) => Theory.ErlangCWq(rho * c * Mu, Mu, c);

        [TestCase(2, 0.7)]
        [TestCase(3, 0.7)]
        [TestCase(5, 0.7)]
        public void Fast_MeanWaitWithin10Percent(int c, double rho)
        {
            double measured = Measure(7UL, c, rho, 200_000);
            double theory = TheoryWq(c, rho);
            string detail = $"c = {c}, ρ = {rho}: measured {measured:F4} vs theory {theory:F4}";
            TestContext.WriteLine(detail);
            Assert.That(measured, Is.EqualTo(theory).Within(10).Percent, detail);
        }

        [Test, Category("Strict")]
        [TestCase(2, 0.7)]
        [TestCase(3, 0.7)]
        [TestCase(5, 0.7)]
        [TestCase(2, 0.9)]
        [TestCase(3, 0.9)]
        [TestCase(5, 0.9)]
        public void Strict_MeanWaitWithin3PercentAndInsideCI(int c, double rho)
        {
            StrictCheck.AssertMeanMatches(seed => Measure(seed, c, rho, 300_000), TheoryWq(c, rho), $"Erlang C c = {c}, ρ = {rho}");
        }

        [TestCase(3, 0.8)]
        [TestCase(5, 0.3)] // more desks than people most of the time
        public void Invariants_HoldOnRandomRun(int c, double rho)
        {
            var log = Experiments.RunSingleStation(11UL, new Exponential(1.0 / (rho * c * Mu)), new Exponential(1.0 / Mu),
                staff: c, stopTime: 5_000.0, keepEntries: true);
            InvariantChecker.AssertAll(log, new Dictionary<string, int> { { "desk", c } });
        }
    }
}
```

Fast cases use ρ = 0.7 only. At 0.9 one seed swings too much for a fair ±10% check (the spec's strict tier covers 0.9).

- [ ] **Step 4: Run the fast suite**

Run: `bash scripts/run-tests.sh`
Expected: all pass (59 + 1 hand + 3 fast + 2 invariants = **65**). Read the printed `measured vs theory` lines; they should be within a few percent.

- [ ] **Step 5: Run the strict suite**

Run: `bash scripts/run-tests.sh --strict`
Expected: 3 M/M/1 + 6 Erlang C = **9** pass. If a ρ = 0.9 case misses **low**, the warm-up is too short (Review Focus 5): give `MeasureMeanWait` an optional `warmupArrivals = 1000` parameter and pass 5000 for that case. Never widen the 3% tolerance. If it misses for any other reason, stop and report the numbers.

- [ ] **Step 6: Commit**

```bash
git add Unity/Assets/Core/Tests
git commit -m "Validate several desks against Erlang C; S2 hand test"
```

---

### Task 3: S3, service variability (P-K)

**Files:**
- Create: `Unity/Assets/Core/Tests/Validation/PollaczekKhinchineValidationTests.cs`

**Interfaces:**
- Consumes: `Experiments.MeasureMeanWait`, `StrictCheck.AssertMeanMatches`, `Theory.PollaczekKhinchineWq(double lambda, double meanService, double cs)`, `Distribution.FromMeanCv(double mean, double cv)` (CV 0 → `Deterministic`, 1 → `Exponential`, otherwise `GammaDistribution`).
- Produces: nothing new.

Setup: Poisson arrivals (λ = ρ), 1 desk, mean service E[S] = 1, service CV = Cs.

- [ ] **Step 1: Create the test file**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Validation
{
    /// <summary>
    /// The engine against Pollaczek–Khinchine (M/G/1): random arrivals, any service CV
    /// (docs/game-mechanism/knowledge/formulas/pollaczek-khinchine.md). E[S] = 1, λ = ρ.
    /// </summary>
    public class PollaczekKhinchineValidationTests
    {
        private const double MeanService = 1.0;

        private static double Measure(ulong seed, double rho, double cs, int passengers) =>
            Experiments.MeasureMeanWait(seed, new Exponential(MeanService / rho), Distribution.FromMeanCv(MeanService, cs),
                staff: 1, passengers);

        private static double TheoryWq(double rho, double cs) => Theory.PollaczekKhinchineWq(rho / MeanService, MeanService, cs);

        [TestCase(0.0)]
        [TestCase(0.5)]
        [TestCase(1.0)]
        public void Fast_MeanWaitWithin10Percent(double cs)
        {
            const double rho = 0.8;
            double measured = Measure(7UL, rho, cs, 200_000);
            double theory = TheoryWq(rho, cs);
            string detail = $"Cs = {cs}, ρ = {rho}: measured {measured:F4} vs theory {theory:F4}";
            TestContext.WriteLine(detail);
            Assert.That(measured, Is.EqualTo(theory).Within(10).Percent, detail);
        }

        [Test, Category("Strict")]
        [TestCase(0.8, 0.0)]
        [TestCase(0.8, 0.5)]
        [TestCase(0.8, 1.0)]
        [TestCase(0.9, 0.0)]
        [TestCase(0.9, 0.5)]
        [TestCase(0.9, 1.0)]
        public void Strict_MeanWaitWithin3PercentAndInsideCI(double rho, double cs)
        {
            StrictCheck.AssertMeanMatches(seed => Measure(seed, rho, cs, 300_000), TheoryWq(rho, cs), $"P-K ρ = {rho}, Cs = {cs}");
        }

        [Test]
        public void Invariants_HoldWithDeterministicService()
        {
            // Cs = 0: every service is exactly 1, so many events land on the same instant.
            var log = Experiments.RunSingleStation(11UL, new Exponential(1.25), new Deterministic(1.0),
                staff: 1, stopTime: 5_000.0, keepEntries: true);
            InvariantChecker.AssertAll(log, new Dictionary<string, int> { { "desk", 1 } });
        }
    }
}
```

The formula says Cs = 0 gives exactly **half** the M/M/1 wait (at ρ = 0.8: 2.0 vs 4.0). Seeing 2.0 in the output is a useful sanity check.

- [ ] **Step 2: Run the fast suite**

Run: `bash scripts/run-tests.sh`
Expected: **69** pass (65 + 3 fast + 1 invariants). The printed lines show about 2.0 / 2.5 / 4.0 for Cs 0 / 0.5 / 1.

- [ ] **Step 3: Run the strict suite**

Run: `bash scripts/run-tests.sh --strict`
Expected: **15** pass (9 + 6). Same rule as Task 2 if one misses: warm-up first, never the tolerance, otherwise stop and report.

- [ ] **Step 4: Commit**

```bash
git add Unity/Assets/Core/Tests/Validation
git commit -m "Validate service variability against Pollaczek-Khinchine"
```

---

### Task 4: S3, arrival variability (Kingman)

**Files:**
- Create: `Unity/Assets/Core/Tests/Validation/KingmanValidationTests.cs`

**Interfaces:**
- Consumes: `Experiments.MeasureMeanWait`, `Theory.KingmanWq(double lambda, double meanService, double ca, double cs)`, `Distribution.FromMeanCv`, `Stats.Mean`.
- Produces: nothing new.

Kingman is an **approximation**, so there's no strict CI test. The tests use a tolerance of ±15%, run only at ρ ≥ 0.8, and average 5 seeds of 300k passengers so that noise doesn't eat the 15%. Kingman is most accurate when both CVs are near 1 and at high load. With very smooth arrivals (Ca = 0.5) at ρ = 0.8 it **overestimates** somewhat, which is a known property, not an engine bug.

- [ ] **Step 1: Create the test file**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Validation
{
    /// <summary>
    /// The engine against Kingman's G/G/1 approximation (docs/game-mechanism/knowledge/formulas/kingman.md).
    /// Arrival gaps have CV Ca, service has CV Cs, E[S] = 1, λ = ρ. Approximate: ±15% at ρ ≥ 0.8.
    /// </summary>
    public class KingmanValidationTests
    {
        private const double MeanService = 1.0;
        private const int Seeds = 5;
        private const int Passengers = 300_000;

        private static double AverageMeasured(double rho, double ca, double cs)
        {
            var means = new List<double>();
            for (int i = 0; i < Seeds; i++)
                means.Add(Experiments.MeasureMeanWait((ulong)(500 + i), Distribution.FromMeanCv(MeanService / rho, ca),
                    Distribution.FromMeanCv(MeanService, cs), staff: 1, Passengers));
            return Stats.Mean(means);
        }

        private static double TheoryWq(double rho, double ca, double cs) => Theory.KingmanWq(rho / MeanService, MeanService, ca, cs);

        [TestCase(0.9, 0.5, 0.5)]
        [TestCase(0.9, 0.5, 1.0)]
        [TestCase(0.9, 1.5, 0.5)]
        [TestCase(0.8, 1.0, 0.5)]
        [TestCase(0.8, 1.5, 1.0)]
        public void Accuracy_Within15Percent(double rho, double ca, double cs)
        {
            double measured = AverageMeasured(rho, ca, cs);
            double theory = TheoryWq(rho, ca, cs);
            string detail = $"ρ = {rho}, Ca = {ca}, Cs = {cs}: measured {measured:F4} vs Kingman {theory:F4}";
            TestContext.WriteLine(detail);
            Assert.That(measured, Is.EqualTo(theory).Within(15).Percent, detail);
        }

        [Test]
        public void Trend_HalvingServiceCvCutsWaitAsKingmanPredicts()
        {
            // Smooth arrivals (Ca = 0.5), ρ = 0.9. Cs 1 → 0.5:
            // Kingman ratio = (0.25 + 0.25) / (0.25 + 1) = 0.4, i.e. a 60% shorter wait.
            const double rho = 0.9, ca = 0.5;
            double ratioMeasured = AverageMeasured(rho, ca, 0.5) / AverageMeasured(rho, ca, 1.0);
            double ratioTheory = TheoryWq(rho, ca, 0.5) / TheoryWq(rho, ca, 1.0);
            string detail = $"wait ratio Cs 0.5 / Cs 1: measured {ratioMeasured:F3} vs Kingman {ratioTheory:F3}";
            TestContext.WriteLine(detail);
            Assert.That(ratioMeasured, Is.LessThan(1.0), "Smoother service must shorten the wait.");
            Assert.That(ratioMeasured, Is.EqualTo(ratioTheory).Within(15).Percent, detail);
        }

        [Test]
        public void Invariants_HoldWithSmoothArrivals()
        {
            var log = Experiments.RunSingleStation(11UL, Distribution.FromMeanCv(1.25, 0.5), Distribution.FromMeanCv(1.0, 0.5),
                staff: 1, stopTime: 5_000.0, keepEntries: true);
            InvariantChecker.AssertAll(log, new Dictionary<string, int> { { "desk", 1 } });
        }
    }
}
```

The 5 accuracy cases include Ca < 1, Ca = 1 and Ca > 1, so the arrival side is tested in all three directions. The Ca = 1 case is P-K exactly, which acts as a control.

These tests run 5 × 300k passengers each, so they're slower than other fast tests (expect a few seconds each). That's acceptable; if the fast suite goes above about 2 minutes, move `Accuracy_Within15Percent` to `[Category("Strict")]` and record that in the spec.

- [ ] **Step 2: Run the fast suite**

Run: `bash scripts/run-tests.sh`
Expected: **76** pass (69 + 5 accuracy + 1 trend + 1 invariants).

**If an accuracy case fails:**
1. Check the printed numbers. If measured is close to the P-K value in the Ca = 1 control case, the engine is fine and Kingman's approximation is off for that (Ca, Cs) pair.
2. Do **not** widen the 15% or delete the case silently. Stop and report the numbers. The user decides whether to drop the case or note it in [kingman.md](../../game-mechanism/knowledge/formulas/kingman.md) as a known limit.

- [ ] **Step 3: Commit**

```bash
git add Unity/Assets/Core/Tests/Validation
git commit -m "Validate arrival and service variability against Kingman, with trend test"
```

---

### Task 5: Close S2 and S3 in the docs

**Files:**
- Modify: `docs/superpowers/specs/2026-10-11-phase-1-core-design.md` (§6 S2, S3 checkboxes; Progress table rows S2, S3)
- Modify: `docs/game-mechanism/knowledge/formulas/erlang-c.md`, `pollaczek-khinchine.md`, `kingman.md` (Status checkboxes)
- Modify: `docs/game-mechanism/knowledge/formulas/README.md` (status table)
- Modify: `docs/roadmap.md` (status line)

- [ ] **Step 1: Run both suites one final time**

Run: `bash scripts/run-tests.sh && bash scripts/run-tests.sh --strict`
Expected: 76 fast pass, 15 strict pass. Copy the printed `measured vs theory` lines; you'll need them in Step 2.

- [ ] **Step 2: Tick the spec**

In §6, tick every box under S2 and S3. Next to "`Station` with c staff" and "Service time from any distribution", add: *(already supported by the S1 engine; pinned by tests in Part 2)*. In the Progress table, set S2 and S3 to `Done (2026-10-11)`. Under the review notes, add one line per strict case with its measured mean vs theory value.

- [ ] **Step 3: Tick the formula pages**

In `erlang-c.md`, `pollaczek-khinchine.md` and `kingman.md`, tick `Implemented` and `Strict test passes`. For Kingman, write it as `Accuracy (±15%) and trend tests pass`. Update the matching rows in the formulas `README.md` status table.

- [ ] **Step 4: Update the roadmap status**

In `docs/roadmap.md`, set the sub-project 1 status to: `S0–S3 done; next: S4 (reneging, Erlang A)`.

- [ ] **Step 5: Commit**

```bash
git add docs
git commit -m "Close S2 and S3: Erlang C, P-K and Kingman validated"
```

Push only when the user asks.
