# Phase 1 Part 6: Level Data and the Layout Compiler (S7) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A level file (flight waves, desk types, patience, space, goal) plus a player's plan (which desks, how many staff, how big each waiting area, how far apart) is read from JSON and compiled into a network the engine runs. Bad plans get clear, plain-English errors, all at once.

**Architecture:** Plain C# data classes in `Throughput.Core.Levels`, read with Unity's Newtonsoft JSON package. The plan is **numbers, not a grid** (user's choice, option A): grid geometry comes with the plan editor in sub-project 2 and will produce these same numbers. `LayoutCompiler` (in `Throughput.Core.Layout`) checks level + plan, then builds `Station`s in a chain, a `Source` with **arrival waves** (time-varying rate), and per-station patience. Desk times can have a **minimum and a hard maximum** through two new distribution wrappers.

**Tech Stack:** Unity 6000.3.26f1, C#, NUnit (EditMode), `com.unity.nuget.newtonsoft-json` 3.2.1, Unity CLI via `scripts/run-tests.sh`.

**Spec:** [docs/superpowers/specs/2026-10-11-phase-1-core-design.md](../specs/2026-10-11-phase-1-core-design.md) (§6 S7, Q1, the S5/S7 review notes). Architecture: [overview.md](../../architecture/overview.md) (Level, Plan, Layout compiler).

## Global Constraints

- `Throughput.Core` never references `UnityEngine`/`UnityEditor`. Newtonsoft.Json is a plain .NET library and allowed.
- Engine code (`Throughput.Core.Engine.*`) never depends on `Levels` or `Layout`; the compiler depends on the engine, not the other way round.
- Determinism: one named random stream per source of randomness: `arrivals:entry`, `service:<station>`, `patience:<station>`.
- **Formula tests keep pure distributions** (no min/max wrappers).
- Private fields `_camelCase`.
- The Unity editor must be closed to run `scripts/run-tests.sh`. The first run after adding the package downloads it (needs internet).
- Git: commit locally after each task; push only when the user asks. Messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Design decisions (the user reviews these)

| # | Decision | Why |
|---|---|---|
| L1 | **The plan is numbers, not a grid** (user's choice, option A). Each stage: desk type, staff, waiting-area size, walk time to the next stage. | Geometry is designed with the editor in sub-project 2; the grid will compute these same numbers. |
| L2 | **A plan is a straight chain of stages; each desk type is used once.** Several lanes per stage (and routing) come in S8. | Matches the engine so far. It also makes the S5 review worries impossible by design: no loops, unique names, and no line fed by both the entrance and a desk. |
| L3 | **Q1 decided: Unity's Newtonsoft package** (`com.unity.nuget.newtonsoft-json`). Unknown fields are an **error** (catches typos like `"perMinit"`). | Official Unity package, plain .NET (no UnityEngine), handles lists and nulls well. `JsonUtility` needs UnityEngine. |
| L4 | **Game time unit: minutes.** Waves in "people per minute", desk times in minutes. | Flights, waits and patience are all naturally in minutes. The engine itself has no unit. |
| L5 | **Desk-time format: `mean`, `cv`, optional `min`, optional `max`.** With `min`: a check always takes at least `min`, plus a random part (so `mean` is the total average, `cv` describes the random part). `max` is a **safety net**: values above it are re-drawn. | The S7 "realistic time ranges" item. A level designer thinks "at least 20 s, about 1 min on average, never 5 min". |
| L6 | **The first waiting area has no size** (it's the terminal hall: B4). Setting one is an error. The level's `space` is the total size of all **other** waiting areas. | D8 and B4: arrivals always get in; space only limits areas between desks. |
| L7 | **Arrival waves: a list of `{start, end, perMinute}`, in order, not overlapping.** Gaps between waves have no arrivals. Random arrivals within each wave (Poisson). Arrivals stop after the last wave, so a level's run always ends. | Flight banks. Fixes the "no stop time never ends" note for real levels. |
| L8 | **The compiler collects every problem and reports them together** (`PlanException.Problems`), in plain English with the stage name. | A player (and a level designer) fixes everything in one go. |
| L9 | **Passenger mix, event deck and goal checks are not compiled yet.** The level stores `maxMissedFlights`; the mix and deck come with S8/S9 when something uses them. | YAGNI: no data without behaviour. |

## Review Focus

1. **A wave with rate 0, or a gap between waves.** Expected: no arrivals in it, and the next arrival is in the next wave. Pinned by `Waves_SkipQuietTimes`.
2. **An arrival "drawn" past a wave's end.** Expected: restart at the boundary with the next wave's rate (memoryless), never an arrival in a gap. Pinned by `Waves_RateMatchesEachWave` (the counts per wave) and `Waves_SkipQuietTimes`.
3. **A `max` that's almost never reachable** (e.g. a fixed 10-minute check with `max` 5). Expected: a clear error, never an endless loop. Pinned by `Bounded_ImpossibleRange_Throws` and the compiler test for a bad desk time.
4. **A plan that breaks many rules at once.** Expected: every problem listed. Pinned by `Compile_ListsEveryProblem`.
5. **JSON typos and broken JSON.** Expected: a `LevelFormatException` naming the problem, not a crash or a silently ignored field. Pinned by `Read_RejectsUnknownField` and `Read_RejectsBrokenJson`.

---

## File Structure

| File | Change | Responsibility |
|---|---|---|
| `Unity/Assets/Core/Engine/Randomness/Shifted.cs` | Create | min + random part |
| `Unity/Assets/Core/Engine/Randomness/Bounded.cs` | Create | re-draw outside [min, max] |
| `Unity/Assets/Core/Engine/Network/ArrivalWaves.cs` | Create | Time-varying Poisson arrivals |
| `Unity/Assets/Core/Engine/Behaviours/Reneging.cs` | Modify | Optional per-station patience streams |
| `Unity/Packages/manifest.json` | Modify | Newtonsoft package |
| `Unity/Assets/Core/Throughput.Core.asmdef` | Modify | Reference `Newtonsoft.Json.dll` |
| `Unity/Assets/Core/Levels/LevelDef.cs` | Create | Level + plan data classes |
| `Unity/Assets/Core/Levels/LevelJson.cs` | Create | JSON reading + `LevelFormatException` |
| `Unity/Assets/Core/Layout/LayoutCompiler.cs` | Create | Checks + build; `CompiledRun`, `PlanException` |
| `Unity/Assets/Core/Tests/Randomness/WrapperTests.cs` | Create | |
| `Unity/Assets/Core/Tests/Engine/ArrivalWavesTests.cs` | Create | |
| `Unity/Assets/Core/Tests/Levels/LevelJsonTests.cs` | Create | |
| `Unity/Assets/Core/Tests/Layout/LayoutCompilerTests.cs` | Create | |
| `Unity/Assets/Core/Tests/Support/SampleLevels.cs` | Create | Sample level + plan JSON |
| Docs | Modify | Task 5 |

---

### Task 1: Desk times with a minimum and a safety maximum

**Files:**
- Create: `Unity/Assets/Core/Engine/Randomness/Shifted.cs`, `Unity/Assets/Core/Engine/Randomness/Bounded.cs`
- Test: `Unity/Assets/Core/Tests/Randomness/WrapperTests.cs`

**Interfaces:**
- Produces: `Shifted(double min, IDistribution inner)` (Mean = min + inner.Mean; Cv = inner standard deviation ÷ Mean); `Bounded(double min, double max, IDistribution inner)` (re-draws outside [min, max]; after 10,000 misses throws `InvalidOperationException`; `Mean`/`Cv` report the inner distribution's, documented as approximate).

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using NUnit.Framework;
using Throughput.Core.Engine.Randomness;

namespace Throughput.Core.Tests.Randomness
{
    public class WrapperTests
    {
        [Test]
        public void Shifted_NeverBelowMin_AndMeanAddsUp()
        {
            var d = new Shifted(0.5, new Exponential(1.0));
            var rng = new Pcg32(1UL, 1UL);
            double sum = 0.0;
            const int n = 200_000;
            for (int i = 0; i < n; i++)
            {
                double x = d.Sample(rng);
                Assert.That(x, Is.GreaterThanOrEqualTo(0.5));
                sum += x;
            }
            Assert.That(d.Mean, Is.EqualTo(1.5));
            Assert.That(sum / n, Is.EqualTo(1.5).Within(1).Percent);
            Assert.That(d.Cv, Is.EqualTo(1.0 / 1.5).Within(1e-12)); // same spread (sd 1), bigger mean
        }

        [Test]
        public void Bounded_StaysInRange()
        {
            var d = new Bounded(0.2, 3.0, new Exponential(1.0));
            var rng = new Pcg32(1UL, 2UL);
            for (int i = 0; i < 100_000; i++)
                Assert.That(d.Sample(rng), Is.InRange(0.2, 3.0));
        }

        [Test]
        public void Bounded_ImpossibleRange_Throws()
        {
            var d = new Bounded(0.0, 5.0, new Deterministic(10.0));
            Assert.Throws<InvalidOperationException>(() => d.Sample(new Pcg32(1UL, 3UL)));
        }

        [Test]
        public void Wrappers_RejectBadArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Shifted(-1.0, new Exponential(1.0)));
            Assert.Throws<ArgumentNullException>(() => new Shifted(1.0, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Bounded(3.0, 3.0, new Exponential(1.0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Bounded(-1.0, 3.0, new Exponential(1.0)));
        }
    }
}
```

- [ ] **Step 2: Run to see it fail**

Run: `bash scripts/run-tests.sh Throughput.Core.Tests.Randomness`
Expected: compile error, `The type or namespace name 'Shifted' could not be found`.

- [ ] **Step 3: Write the wrappers**

`Shifted.cs`:

```csharp
using System;

namespace Throughput.Core.Engine.Randomness
{
    /// <summary>
    /// A check that always takes at least Min, plus a random part: Min + inner sample.
    /// For game levels (realistic times). Formula tests use pure distributions instead.
    /// </summary>
    public sealed class Shifted : IDistribution
    {
        private readonly IDistribution _inner;

        public Shifted(double min, IDistribution inner)
        {
            if (!(min >= 0) || double.IsInfinity(min))
                throw new ArgumentOutOfRangeException(nameof(min), min, "Min must be 0 or more.");
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            Min = min;
        }

        public double Min { get; }

        public double Mean => Min + _inner.Mean;

        /// <summary>The spread is the random part's; the mean is bigger, so the CV is smaller.</summary>
        public double Cv => _inner.Cv * _inner.Mean / Mean;

        public double Sample(Pcg32 rng) => Min + _inner.Sample(rng);
    }
}
```

`Bounded.cs`:

```csharp
using System;

namespace Throughput.Core.Engine.Randomness
{
    /// <summary>
    /// A safety net for game levels: samples outside [Min, Max] are drawn again. It should rarely trigger.
    /// Mean and Cv report the inner distribution's (approximate: the cut-off changes them slightly).
    /// </summary>
    public sealed class Bounded : IDistribution
    {
        private const int MaxTries = 10_000;
        private readonly IDistribution _inner;

        public Bounded(double min, double max, IDistribution inner)
        {
            if (!(min >= 0) || double.IsInfinity(min))
                throw new ArgumentOutOfRangeException(nameof(min), min, "Min must be 0 or more.");
            if (!(max > min))
                throw new ArgumentOutOfRangeException(nameof(max), max, $"Max must be above min ({min}).");
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            Min = min;
            Max = max;
        }

        public double Min { get; }

        public double Max { get; }

        public double Mean => _inner.Mean;

        public double Cv => _inner.Cv;

        public double Sample(Pcg32 rng)
        {
            for (int i = 0; i < MaxTries; i++)
            {
                double x = _inner.Sample(rng);
                if (x >= Min && x <= Max) return x;
            }
            throw new InvalidOperationException(
                $"No sample landed in [{Min}, {Max}] after {MaxTries} tries (mean {Mean}): the range is almost impossible.");
        }
    }
}
```

- [ ] **Step 4: Run the tests** — `bash scripts/run-tests.sh` → Expected **106** pass (102 + 4).

- [ ] **Step 5: Commit**

```bash
git add Unity/Assets/Core
git commit -m "Add Shifted and Bounded desk-time wrappers (minimum + safety maximum)"
```

---

### Task 2: Arrival waves

**Files:**
- Create: `Unity/Assets/Core/Engine/Network/ArrivalWaves.cs`
- Test: `Unity/Assets/Core/Tests/Engine/ArrivalWavesTests.cs`

**Interfaces:**
- Produces: `readonly struct Wave(double start, double end, double rate)` with `Start`, `End`, `Rate` (people per time unit); `ArrivalWaves(IReadOnlyList<Wave> waves, Pcg32 rng) : IArrivalProcess`.

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Randomness;

namespace Throughput.Core.Tests.Engine
{
    public class ArrivalWavesTests
    {
        private static List<double> AllArrivals(ArrivalWaves waves)
        {
            var times = new List<double>();
            double now = 0.0;
            while (waves.TryNext(now, out double t))
            {
                times.Add(t);
                now = t;
            }
            return times;
        }

        [Test]
        public void Waves_RateMatchesEachWave()
        {
            // 1 per minute for 10,000 min, then 3 per minute for 10,000 min (Poisson: sd ≈ 1% of the count).
            var waves = new ArrivalWaves(new[] { new Wave(0, 10_000, 1.0), new Wave(10_000, 20_000, 3.0) }, new Pcg32(5UL, 1UL));
            List<double> times = AllArrivals(waves);

            int first = times.FindAll(t => t < 10_000).Count;
            int second = times.Count - first;
            Assert.That(first, Is.EqualTo(10_000).Within(4).Percent);
            Assert.That(second, Is.EqualTo(30_000).Within(4).Percent);
            Assert.That(times[times.Count - 1], Is.LessThan(20_000));
        }

        [Test]
        public void Waves_SkipQuietTimes()
        {
            // Quiet 0–10 (rate 0), busy 10–20, gap 20–50 (no wave), busy 50–60. Nothing after 60.
            var waves = new ArrivalWaves(new[] { new Wave(0, 10, 0.0), new Wave(10, 20, 5.0), new Wave(50, 60, 5.0) },
                new Pcg32(5UL, 2UL));
            List<double> times = AllArrivals(waves);

            Assert.That(times.Count, Is.GreaterThan(0));
            foreach (double t in times)
                Assert.That((t >= 10 && t < 20) || (t >= 50 && t < 60), Is.True, $"Arrival at {t} is outside the waves.");
            Assert.That(times.Exists(t => t >= 50), Is.True, "The second wave must have arrivals too.");
        }

        [Test]
        public void Waves_RejectBadInput()
        {
            var rng = new Pcg32(1UL, 1UL);
            Assert.Throws<ArgumentException>(() => new ArrivalWaves(new[] { new Wave(10, 5, 1.0) }, rng));
            Assert.Throws<ArgumentException>(() => new ArrivalWaves(new[] { new Wave(0, 10, -1.0) }, rng));
            Assert.Throws<ArgumentException>(() => new ArrivalWaves(new[] { new Wave(0, 10, 1.0), new Wave(5, 20, 1.0) }, rng));
        }
    }
}
```

- [ ] **Step 2: Run to see it fail** — `bash scripts/run-tests.sh Throughput.Core.Tests.Engine` → compile error, `'Wave' could not be found`.

- [ ] **Step 3: Write `ArrivalWaves.cs`**

```csharp
using System;
using System.Collections.Generic;
using Throughput.Core.Engine.Randomness;

namespace Throughput.Core.Engine.Network
{
    /// <summary>One flight bank: people arrive at random at Rate per time unit between Start and End.</summary>
    public readonly struct Wave
    {
        public Wave(double start, double end, double rate)
        {
            Start = start;
            End = end;
            Rate = rate;
        }

        public double Start { get; }

        public double End { get; }

        public double Rate { get; }
    }

    /// <summary>
    /// Random (Poisson) arrivals whose rate changes from wave to wave. No arrivals between waves or after
    /// the last one. A gap drawn past a wave's end restarts at the boundary with the next wave's rate
    /// (Poisson arrivals have no memory, so this is exact).
    /// </summary>
    public sealed class ArrivalWaves : IArrivalProcess
    {
        private readonly Wave[] _waves;
        private readonly Pcg32 _rng;

        public ArrivalWaves(IReadOnlyList<Wave> waves, Pcg32 rng)
        {
            if (waves == null) throw new ArgumentNullException(nameof(waves));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _waves = new Wave[waves.Count];
            for (int i = 0; i < waves.Count; i++)
            {
                Wave w = waves[i];
                if (!(w.Start >= 0) || !(w.End > w.Start) || double.IsInfinity(w.End))
                    throw new ArgumentException($"Wave {i + 1}: must start at 0 or later and end after it starts ({w.Start}–{w.End}).", nameof(waves));
                if (!(w.Rate >= 0) || double.IsInfinity(w.Rate))
                    throw new ArgumentException($"Wave {i + 1}: the rate can't be negative ({w.Rate}).", nameof(waves));
                if (i > 0 && w.Start < waves[i - 1].End)
                    throw new ArgumentException($"Wave {i + 1} starts at {w.Start}, before wave {i} ends ({waves[i - 1].End}).", nameof(waves));
                _waves[i] = w;
            }
        }

        public bool TryNext(double now, out double time)
        {
            double t = now;
            foreach (Wave w in _waves)
            {
                if (t >= w.End || w.Rate <= 0) continue;
                if (t < w.Start) t = w.Start;
                double gap = -Math.Log(_rng.NextOpenDouble()) / w.Rate;
                if (t + gap < w.End)
                {
                    time = t + gap;
                    return true;
                }
                t = w.End;
            }
            time = 0.0;
            return false;
        }
    }
}
```

- [ ] **Step 4: Run the tests** — `bash scripts/run-tests.sh` → Expected **109** pass.

- [ ] **Step 5: Commit**

```bash
git add Unity/Assets/Core
git commit -m "Add arrival waves: random arrivals with a rate per flight bank"
```

---

### Task 3: Level and plan data, read from JSON

**Files:**
- Modify: `Unity/Packages/manifest.json`, `Unity/Assets/Core/Throughput.Core.asmdef`
- Create: `Unity/Assets/Core/Levels/LevelDef.cs`, `Unity/Assets/Core/Levels/LevelJson.cs`
- Create: `Unity/Assets/Core/Tests/Support/SampleLevels.cs`
- Test: `Unity/Assets/Core/Tests/Levels/LevelJsonTests.cs`

**Interfaces:**
- Produces (namespace `Throughput.Core.Levels`): `LevelDef { Name, Waves (List<WaveDef>), Stages (List<StageTypeDef>), Patience (DistributionDef, null = nobody gives up), Space (int), MaxMissedFlights (int) }`; `WaveDef { Start, End, PerMinute }`; `StageTypeDef { Name, Service (DistributionDef), MaxStaff }`; `DistributionDef { Mean, Cv = 1, Min = 0, Max (double?) }` with `IDistribution Build()`; `PlanDef { Stages (List<PlannedStage>) }`; `PlannedStage { Type, Staff, LineCapacity (int?), WalkToNext }`; `LevelJson.ReadLevel(string) → LevelDef`, `LevelJson.ReadPlan(string) → PlanDef`; `LevelFormatException : Exception`. `SampleLevels.Level` and `SampleLevels.Plan` (JSON strings).

- [ ] **Step 1: Add the package and the reference**

In `Unity/Packages/manifest.json`, add to `dependencies` (keep alphabetical order where it sits):

```json
    "com.unity.nuget.newtonsoft-json": "3.2.1",
```

In `Throughput.Core.asmdef`, set:

```json
    "overrideReferences": true,
    "precompiledReferences": ["Newtonsoft.Json.dll"],
```

(`noEngineReferences` stays `true`.)

- [ ] **Step 2: Write the sample data and the failing tests**

`Tests/Support/SampleLevels.cs`:

```csharp
namespace Throughput.Core.Tests.Support
{
    /// <summary>A small realistic level and plan (minutes). Two flight banks; the second overloads the ID check.</summary>
    public static class SampleLevels
    {
        public const string Level = @"{
  ""name"": ""Morning bank"",
  ""waves"": [
    { ""start"": 0,  ""end"": 30, ""perMinute"": 1.5 },
    { ""start"": 30, ""end"": 60, ""perMinute"": 3.0 }
  ],
  ""stages"": [
    { ""name"": ""id"",      ""service"": { ""mean"": 1.2, ""cv"": 0.5, ""min"": 0.3, ""max"": 5 }, ""maxStaff"": 4 },
    { ""name"": ""scanner"", ""service"": { ""mean"": 0.6, ""cv"": 0.4, ""min"": 0.2, ""max"": 3 }, ""maxStaff"": 3 }
  ],
  ""patience"": { ""mean"": 25, ""cv"": 0.6, ""min"": 5 },
  ""space"": 20,
  ""maxMissedFlights"": 5
}";

        public const string Plan = @"{
  ""stages"": [
    { ""type"": ""id"",      ""staff"": 3, ""walkToNext"": 0.5 },
    { ""type"": ""scanner"", ""staff"": 2, ""lineCapacity"": 12 }
  ]
}";
    }
}
```

`Tests/Levels/LevelJsonTests.cs`:

```csharp
using NUnit.Framework;
using Throughput.Core.Levels;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Levels
{
    public class LevelJsonTests
    {
        [Test]
        public void Read_SampleLevelAndPlan()
        {
            LevelDef level = LevelJson.ReadLevel(SampleLevels.Level);
            Assert.That(level.Name, Is.EqualTo("Morning bank"));
            Assert.That(level.Waves.Count, Is.EqualTo(2));
            Assert.That(level.Waves[1].PerMinute, Is.EqualTo(3.0));
            Assert.That(level.Stages[0].Service.Min, Is.EqualTo(0.3));
            Assert.That(level.Stages[0].Service.Max, Is.EqualTo(5.0));
            Assert.That(level.Stages[1].Service.Cv, Is.EqualTo(0.4));
            Assert.That(level.Patience.Max, Is.Null);
            Assert.That(level.Space, Is.EqualTo(20));

            PlanDef plan = LevelJson.ReadPlan(SampleLevels.Plan);
            Assert.That(plan.Stages.Count, Is.EqualTo(2));
            Assert.That(plan.Stages[0].LineCapacity, Is.Null);
            Assert.That(plan.Stages[1].LineCapacity, Is.EqualTo(12));
            Assert.That(plan.Stages[0].WalkToNext, Is.EqualTo(0.5));
        }

        [Test]
        public void Read_RejectsUnknownField()
        {
            var e = Assert.Throws<LevelFormatException>(() =>
                LevelJson.ReadLevel(@"{ ""name"": ""x"", ""waves"": [ { ""start"": 0, ""end"": 10, ""perMinit"": 1 } ] }"));
            StringAssert.Contains("perMinit", e.Message);
        }

        [Test]
        public void Read_RejectsBrokenJson()
        {
            Assert.Throws<LevelFormatException>(() => LevelJson.ReadPlan(@"{ ""stages"": [ { ""type"": ""id"", "));
            Assert.Throws<LevelFormatException>(() => LevelJson.ReadPlan(""));
        }

        [Test]
        public void DistributionDef_BuildsMinMeanMax()
        {
            var d = new DistributionDef { Mean = 1.0, Cv = 0.5, Min = 0.2, Max = 4.0 }.Build();
            Assert.That(d.Mean, Is.EqualTo(1.0).Within(1e-12)); // the safety net reports the shifted mean
            var plain = new DistributionDef { Mean = 2.0 }.Build(); // cv defaults to 1: exponential
            Assert.That(plain.Cv, Is.EqualTo(1.0));
        }
    }
}
```

- [ ] **Step 3: Run to see it fail** — `bash scripts/run-tests.sh Throughput.Core.Tests.Levels` → compile error, `The type or namespace name 'Levels' does not exist`. (The first run also imports the new package; it may take a minute.)

- [ ] **Step 4: Write `LevelDef.cs`**

```csharp
using System;
using System.Collections.Generic;
using Throughput.Core.Engine.Randomness;

namespace Throughput.Core.Levels
{
    /// <summary>What a level offers (L1–L9). Times in minutes (L4).</summary>
    public sealed class LevelDef
    {
        public string Name { get; set; }

        /// <summary>Flight banks, in order, not overlapping (L7).</summary>
        public List<WaveDef> Waves { get; set; } = new List<WaveDef>();

        /// <summary>The desk types the player may use.</summary>
        public List<StageTypeDef> Stages { get; set; } = new List<StageTypeDef>();

        /// <summary>How long people wait before giving up; null = nobody gives up.</summary>
        public DistributionDef Patience { get; set; }

        /// <summary>Total places in waiting areas between desks (the hall before the first desk doesn't count, L6).</summary>
        public int Space { get; set; }

        /// <summary>The main goal (D9). Checked in S9.</summary>
        public int MaxMissedFlights { get; set; }
    }

    public sealed class WaveDef
    {
        public double Start { get; set; }

        public double End { get; set; }

        public double PerMinute { get; set; }
    }

    public sealed class StageTypeDef
    {
        public string Name { get; set; }

        public DistributionDef Service { get; set; }

        public int MaxStaff { get; set; }
    }

    /// <summary>
    /// A duration (L5): Mean is the total average. With Min, it always takes at least Min plus a random part
    /// whose CV is Cv. Max is a safety net: values above it are drawn again.
    /// </summary>
    public sealed class DistributionDef
    {
        public double Mean { get; set; }

        public double Cv { get; set; } = 1.0;

        public double Min { get; set; }

        public double? Max { get; set; }

        /// <summary>Builds the distribution; throws ArgumentException (with a plain message) if the numbers don't make sense.</summary>
        public IDistribution Build()
        {
            if (!(Mean > Min))
                throw new ArgumentException($"mean ({Mean}) must be above min ({Min}).");
            IDistribution d = Distribution.FromMeanCv(Mean - Min, Cv);
            if (Min > 0) d = new Shifted(Min, d);
            if (Max.HasValue)
            {
                if (!(Max.Value > Mean))
                    throw new ArgumentException($"max ({Max.Value}) must be above the mean ({Mean}).");
                d = new Bounded(Min, Max.Value, d);
            }
            return d;
        }
    }

    /// <summary>What the player built (L1, L2): a chain of stages, first to last.</summary>
    public sealed class PlanDef
    {
        public List<PlannedStage> Stages { get; set; } = new List<PlannedStage>();
    }

    public sealed class PlannedStage
    {
        /// <summary>A desk type name from the level.</summary>
        public string Type { get; set; }

        public int Staff { get; set; }

        /// <summary>Waiting-area size in people; null = unlimited. Must be null for the first stage (L6).</summary>
        public int? LineCapacity { get; set; }

        /// <summary>Walk time to the next stage, in minutes. Ignored for the last stage.</summary>
        public double WalkToNext { get; set; }
    }
}
```

- [ ] **Step 5: Write `LevelJson.cs`**

```csharp
using System;
using Newtonsoft.Json;

namespace Throughput.Core.Levels
{
    /// <summary>The level or plan file can't be read (broken JSON, a typo in a field name, a wrong type).</summary>
    public sealed class LevelFormatException : Exception
    {
        public LevelFormatException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>Reads level and plan JSON (L3). Field names are matched ignoring case; unknown fields are errors.</summary>
    public static class LevelJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
        };

        public static LevelDef ReadLevel(string json) => Read<LevelDef>(json, "level");

        public static PlanDef ReadPlan(string json) => Read<PlanDef>(json, "plan");

        private static T Read<T>(string json, string what) where T : class
        {
            T result;
            try
            {
                result = JsonConvert.DeserializeObject<T>(json ?? "", Settings);
            }
            catch (JsonException e)
            {
                throw new LevelFormatException($"Can't read the {what} file: {e.Message}", e);
            }
            return result ?? throw new LevelFormatException($"The {what} file is empty.", null);
        }
    }
}
```

- [ ] **Step 6: Run the tests** — `bash scripts/run-tests.sh` → Expected **113** pass. `SetupCheckTests.CoreAssembly_DoesNotReferenceUnity` must still pass (Newtonsoft isn't UnityEngine).

- [ ] **Step 7: Commit**

```bash
git add Unity/Packages Unity/Assets/Core
git commit -m "Read level and plan JSON (Newtonsoft); desk times with min and max"
```

(`Unity/Packages/packages-lock.json` changes too; commit it.)

---

### Task 4: The layout compiler

**Files:**
- Modify: `Unity/Assets/Core/Engine/Behaviours/Reneging.cs`
- Create: `Unity/Assets/Core/Layout/LayoutCompiler.cs`
- Test: `Unity/Assets/Core/Tests/Layout/LayoutCompilerTests.cs`

**Interfaces:**
- Consumes: Tasks 1–3.
- Produces: `Reneging(IDistribution patience, RngStreams streams)` (one stream `patience:<station>` per station); `LayoutCompiler.Check(LevelDef, PlanDef) → IReadOnlyList<string>`; `LayoutCompiler.Compile(LevelDef, PlanDef, ulong runSeed) → CompiledRun` (throws `PlanException`); `CompiledRun { Runner Runner; IReadOnlyList<Station> Stations; IReadOnlyDictionary<string,int> StaffByStation; IReadOnlyDictionary<string,int> LineCapacity }` (only limited lines); `PlanException : Exception { IReadOnlyList<string> Problems }`.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Linq;
using NUnit.Framework;
using Throughput.Core.Layout;
using Throughput.Core.Levels;
using Throughput.Core.Results;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Layout
{
    public class LayoutCompilerTests
    {
        private static LevelDef Level => LevelJson.ReadLevel(SampleLevels.Level);

        [Test]
        public void Compile_SampleBuildsTheChain()
        {
            CompiledRun run = LayoutCompiler.Compile(Level, LevelJson.ReadPlan(SampleLevels.Plan), runSeed: 7UL);

            Assert.That(run.Stations.Select(s => s.Name), Is.EqualTo(new[] { "id", "scanner" }));
            Assert.That(run.Stations[0].Staff, Is.EqualTo(3));
            Assert.That(run.Stations[0].Next, Is.SameAs(run.Stations[1]));
            Assert.That(run.Stations[0].WalkTime, Is.EqualTo(0.5));
            Assert.That(run.Stations[1].LineCapacity, Is.EqualTo(12));
            Assert.That(run.Stations[1].Next, Is.Null);
            Assert.That(run.LineCapacity.Keys, Is.EqualTo(new[] { "scanner" }));
        }

        [Test]
        public void Compile_SampleRunsToTheEnd()
        {
            CompiledRun run = LayoutCompiler.Compile(Level, LevelJson.ReadPlan(SampleLevels.Plan), runSeed: 7UL);
            var log = new RunLog();
            run.Runner.AddObserver(log);
            run.Runner.Run();

            Assert.That(run.Runner.IsFinished, Is.True);
            Assert.That(log.Passengers.Count, Is.EqualTo(135).Within(25).Percent); // 30×1.5 + 30×3 = 135 expected
            Assert.That(log.Passengers.All(p => p.Arrive < 60.0), Is.True);
            InvariantChecker.AssertAll(log, run.StaffByStation, lineCapacity: run.LineCapacity);
        }

        [Test]
        public void Compile_SameSeedSameRun()
        {
            string[] Once()
            {
                CompiledRun run = LayoutCompiler.Compile(Level, LevelJson.ReadPlan(SampleLevels.Plan), runSeed: 9UL);
                var log = new RunLog();
                run.Runner.AddObserver(log);
                run.Runner.Run();
                return log.Entries.Select(e => e.ToString()).ToArray();
            }
            Assert.That(Once(), Is.EqualTo(Once()));
        }

        [Test]
        public void Compile_ListsEveryProblem()
        {
            var plan = new PlanDef();
            plan.Stages.Add(new PlannedStage { Type = "id", Staff = 9, LineCapacity = 5 });   // too many staff; first area has no size
            plan.Stages.Add(new PlannedStage { Type = "xray", Staff = 1 });                    // unknown desk type
            plan.Stages.Add(new PlannedStage { Type = "scanner", Staff = 1, LineCapacity = 30 }); // more than the level's space (20)
            plan.Stages.Add(new PlannedStage { Type = "scanner", Staff = 1, LineCapacity = 1 });  // used twice

            var e = Assert.Throws<PlanException>(() => LayoutCompiler.Compile(Level, plan, 1UL));
            string all = string.Join("\n", e.Problems);
            StringAssert.Contains("'id': 9 staff", all);
            StringAssert.Contains("first waiting area", all);
            StringAssert.Contains("'xray'", all);
            StringAssert.Contains("space", all);
            StringAssert.Contains("'scanner' is used twice", all);
            Assert.That(e.Problems.Count, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void Check_FindsLevelProblems()
        {
            LevelDef level = Level;
            level.Waves.Add(new WaveDef { Start = 50, End = 70, PerMinute = 1 }); // overlaps the second wave
            level.Stages[1].Service.Max = 0.5;                                    // max below the mean
            var problems = LayoutCompiler.Check(level, LevelJson.ReadPlan(SampleLevels.Plan));
            string all = string.Join("\n", problems);
            StringAssert.Contains("Wave 3", all);
            StringAssert.Contains("'scanner'", all);
        }

        [Test]
        public void Check_EmptyPlan()
        {
            StringAssert.Contains("at least one", string.Join("\n", LayoutCompiler.Check(Level, new PlanDef())));
        }
    }
}
```

- [ ] **Step 2: Run to see it fail** — `bash scripts/run-tests.sh Throughput.Core.Tests.Layout` → compile error, `'Layout' does not exist`.

- [ ] **Step 3: Per-station patience streams in `Reneging.cs`**

Replace the class body with:

```csharp
    public sealed class Reneging : IBehaviour
    {
        private readonly IDistribution _patience;
        private readonly Pcg32 _rng;
        private readonly RngStreams _streams;
        private readonly Dictionary<Station, Pcg32> _byStation = new Dictionary<Station, Pcg32>();

        /// <summary>One shared patience stream (tests and single-station runs).</summary>
        public Reneging(IDistribution patience, Pcg32 rng)
        {
            _patience = patience ?? throw new ArgumentNullException(nameof(patience));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        /// <summary>One stream per station ("patience:&lt;station&gt;"), so a change at one desk doesn't reshuffle the others.</summary>
        public Reneging(IDistribution patience, RngStreams streams)
        {
            _patience = patience ?? throw new ArgumentNullException(nameof(patience));
            _streams = streams ?? throw new ArgumentNullException(nameof(streams));
        }

        public void OnJoinQueue(Runner runner, Station station, Passenger passenger)
        {
            runner.Schedule(runner.Now + _patience.Sample(StreamFor(station)), EventType.Renege, station, passenger);
        }

        private Pcg32 StreamFor(Station station)
        {
            if (_streams == null) return _rng;
            if (!_byStation.TryGetValue(station, out Pcg32 rng))
            {
                rng = _streams.Get("patience:" + station.Name);
                _byStation[station] = rng;
            }
            return rng;
        }
    }
```

Add `using System.Collections.Generic;` at the top.

- [ ] **Step 4: Write `LayoutCompiler.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Throughput.Core.Engine.Behaviours;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Levels;

namespace Throughput.Core.Layout
{
    /// <summary>The plan can't be built. Problems lists every reason, in plain English (L8).</summary>
    public sealed class PlanException : Exception
    {
        public PlanException(IReadOnlyList<string> problems)
            : base("The plan can't be built:\n- " + string.Join("\n- ", problems))
        {
            Problems = problems;
        }

        public IReadOnlyList<string> Problems { get; }
    }

    /// <summary>A built network, ready to run.</summary>
    public sealed class CompiledRun
    {
        internal CompiledRun(Runner runner, IReadOnlyList<Station> stations)
        {
            Runner = runner;
            Stations = stations;
            StaffByStation = stations.ToDictionary(s => s.Name, s => s.Staff);
            LineCapacity = stations.Where(s => s.LineCapacity != int.MaxValue).ToDictionary(s => s.Name, s => s.LineCapacity);
        }

        public Runner Runner { get; }

        /// <summary>First to last.</summary>
        public IReadOnlyList<Station> Stations { get; }

        public IReadOnlyDictionary<string, int> StaffByStation { get; }

        /// <summary>Only the limited waiting areas.</summary>
        public IReadOnlyDictionary<string, int> LineCapacity { get; }
    }

    /// <summary>Level + plan → network (L1, L2). Checks everything first and reports all problems at once.</summary>
    public static class LayoutCompiler
    {
        public static CompiledRun Compile(LevelDef level, PlanDef plan, ulong runSeed)
        {
            IReadOnlyList<string> problems = Check(level, plan);
            if (problems.Count > 0) throw new PlanException(problems);

            var streams = new RngStreams(runSeed);
            var stations = new List<Station>();
            foreach (PlannedStage ps in plan.Stages)
            {
                StageTypeDef type = level.Stages.First(t => t.Name == ps.Type);
                var station = new Station(type.Name, ps.Staff, type.Service.Build(), streams.Get("service:" + type.Name),
                    ps.LineCapacity ?? int.MaxValue);
                if (stations.Count > 0)
                    stations[stations.Count - 1].ConnectTo(station, plan.Stages[stations.Count - 1].WalkToNext);
                stations.Add(station);
            }

            Wave[] waves = level.Waves.Select(w => new Wave(w.Start, w.End, w.PerMinute)).ToArray();
            var source = new Source("entry", new ArrivalWaves(waves, streams.Get("arrivals:entry")), stations[0]);
            var runner = new Runner(source);
            if (level.Patience != null)
                runner.AddBehaviour(new Reneging(level.Patience.Build(), streams));
            return new CompiledRun(runner, stations);
        }

        public static IReadOnlyList<string> Check(LevelDef level, PlanDef plan)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var problems = new List<string>();
            CheckLevel(level, problems);
            CheckPlan(level, plan, problems);
            return problems;
        }

        private static void CheckLevel(LevelDef level, List<string> problems)
        {
            if (level.Waves.Count == 0)
                problems.Add("The level has no arrival waves.");
            for (int i = 0; i < level.Waves.Count; i++)
            {
                WaveDef w = level.Waves[i];
                if (!(w.Start >= 0) || !(w.End > w.Start))
                    problems.Add($"Wave {i + 1} must start at 0 or later and end after it starts ({w.Start}–{w.End}).");
                if (!(w.PerMinute >= 0))
                    problems.Add($"Wave {i + 1} can't have a negative rate ({w.PerMinute} per minute).");
                if (i > 0 && w.Start < level.Waves[i - 1].End)
                    problems.Add($"Wave {i + 1} starts at {w.Start}, before wave {i} ends ({level.Waves[i - 1].End}).");
            }

            var names = new HashSet<string>();
            foreach (StageTypeDef t in level.Stages)
            {
                if (string.IsNullOrEmpty(t.Name)) { problems.Add("A desk type has no name."); continue; }
                if (!names.Add(t.Name)) problems.Add($"Desk type '{t.Name}' is defined twice.");
                if (t.MaxStaff < 1) problems.Add($"Desk type '{t.Name}': max staff must be at least 1.");
                CheckDistribution(t.Service, $"Desk type '{t.Name}' service time", problems);
            }
            if (level.Patience != null)
                CheckDistribution(level.Patience, "Patience", problems);
            if (level.Space < 0)
                problems.Add($"The level's space can't be negative ({level.Space}).");
        }

        private static void CheckDistribution(DistributionDef d, string what, List<string> problems)
        {
            if (d == null) { problems.Add($"{what} is missing."); return; }
            try
            {
                d.Build();
            }
            catch (ArgumentException e)
            {
                problems.Add($"{what}: {FirstLine(e.Message)}");
            }
        }

        private static void CheckPlan(LevelDef level, PlanDef plan, List<string> problems)
        {
            if (plan.Stages.Count == 0)
            {
                problems.Add("The plan needs at least one stage.");
                return;
            }

            var used = new HashSet<string>();
            int space = 0;
            for (int i = 0; i < plan.Stages.Count; i++)
            {
                PlannedStage ps = plan.Stages[i];
                string name = $"'{ps.Type}'";
                StageTypeDef type = level.Stages.FirstOrDefault(t => t.Name == ps.Type);
                if (type == null)
                {
                    problems.Add($"Stage {i + 1}: this level has no desk type {name}.");
                    continue;
                }
                if (!used.Add(ps.Type))
                    problems.Add($"Stage {i + 1}: {name} is used twice (one stage per desk type until lanes arrive).");
                if (ps.Staff < 1 || ps.Staff > type.MaxStaff)
                    problems.Add($"Stage {i + 1} {name}: {ps.Staff} staff (this level allows 1 to {type.MaxStaff}).");
                if (i == 0 && ps.LineCapacity.HasValue)
                    problems.Add($"Stage 1 {name}: the first waiting area has no size (people wait in the hall).");
                if (i > 0 && ps.LineCapacity.HasValue)
                {
                    if (ps.LineCapacity.Value < 1)
                        problems.Add($"Stage {i + 1} {name}: the waiting area must hold at least 1 person.");
                    else
                        space += ps.LineCapacity.Value;
                }
                if (!(ps.WalkToNext >= 0) || double.IsInfinity(ps.WalkToNext))
                    problems.Add($"Stage {i + 1} {name}: walk time must be 0 or more ({ps.WalkToNext}).");
            }
            if (space > level.Space)
                problems.Add($"Waiting areas use {space} places, but this level has space for {level.Space}.");
        }

        private static string FirstLine(string message)
        {
            int cut = message.IndexOf(Environment.NewLine, StringComparison.Ordinal);
            return cut < 0 ? message : message.Substring(0, cut);
        }
    }
}
```

Note: an unlimited waiting area (`lineCapacity` left out) after the first stage doesn't count against `space`. That's allowed in S7 for testing big areas; whether real levels must give every area a size is a level-design rule for sub-project 2 (record in the spec notes, Task 5).

- [ ] **Step 5: Run the tests** — `bash scripts/run-tests.sh` → Expected **119** pass (113 + 6). If `Compile_SampleRunsToTheEnd`'s passenger count is outside ±25% of 135, the waves are wrong: debug, don't widen.

- [ ] **Step 6: Run strict** — `bash scripts/run-tests.sh --strict` → **19** pass (the wrappers aren't used by formula tests).

- [ ] **Step 7: Commit**

```bash
git add Unity/Assets/Core
git commit -m "Add the layout compiler: level + plan → network, with every problem listed"
```

---

### Task 5: Docs

- [ ] **Step 1: Spec**
- §6 S7: tick every box. Change the first to `- [x] \`LevelDef\` (waves, desk types, patience, space, goal) and \`PlanDef\` (a chain of stages, as numbers: L1) as plain C# classes; passenger mix and event deck come with S8/S9 (L9)`. Tick JSON reading with "(Newtonsoft, L3)".
- Q1: change "Current lean" to `**Decided: Unity's Newtonsoft package (2026-10-11, L3).** Unknown fields are errors.`
- Notes: change the S7 review note's line to start with **Done by design (L2):** "a chain of stages with each desk type once: no loops, unique names, no mixed feeding. Revisit when S8 adds lanes and routing." Add a new note: **Sub-project 2 (plan editor):** "the grid must produce the plan numbers (L1). Decide whether every waiting area must have a size (S7 allows unlimited ones, which don't count against space)."
- The "any slice" note about `Run()` never ending: add "Real levels use waves (L7), which always end."
- Progress row S7: `Done (2026-10-11)`. Add L1–L9 as "S7 decisions". Change log row: `| 2026-10-11 | S7 done: level/plan JSON (Q1: Newtonsoft), arrival waves, min/max desk times, layout compiler; plan is numbers (grid in sub-project 2) |`.

- [ ] **Step 2: Architecture** — `overview.md`'s Plan row: add "(phase 1: a chain of stages as numbers; the grid editor turns geometry into these numbers in sub-project 2)". Layout compiler row: add "checks everything and lists all problems".

- [ ] **Step 3: Roadmap** — `S0–S7 done; next: S8 (policies, rules, breakdowns, manager actions)`.

- [ ] **Step 4: Run both suites and commit**

```bash
git add docs
git commit -m "Close S7: level data and the layout compiler"
```

Push only when the user asks.
