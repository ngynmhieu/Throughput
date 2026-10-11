# Phase 1 Part 1: Foundations and M/M/1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build slices **S0** (random numbers, distributions, test helpers, theory formulas) and **S1** (event loop, one queue, one desk) of the simulation core, and prove S1 against hand calculations, Little's Law and the M/M/1 formula.

**Architecture:** Plain C# in `Throughput.Core` (no Unity). A discrete-event `Simulator` pops `SimEvent`s from a binary-heap `EventList` ordered by (time, type priority, sequence). `Source`s create `Passenger`s, `Station`s hold a FIFO line and `Staff` servers. Observers (`ISimObserver`) watch; `RunLog` records every step with the engine's queue length and busy count, so tests can check invariants against engine state. Every random number comes from a named `Pcg32` stream derived from the run seed.

**Tech Stack:** Unity 6000.3.26f1, C# 9, .NET Standard 2.1 API surface, NUnit (Unity Test Framework 1.6, EditMode), Unity CLI 1.0.0-beta.13 for headless test runs, Git Bash + Python for the test wrapper.

**Spec:** [docs/superpowers/specs/2026-10-11-phase-1-core-design.md](../specs/2026-10-11-phase-1-core-design.md). Formula background: [docs/game-mechanism/knowledge/formulas/](../../game-mechanism/knowledge/formulas/README.md).

## Global Constraints

- `Throughput.Core` must never reference `UnityEngine` (its asmdef has `noEngineReferences: true`; `SetupCheckTests` must pass, not be inconclusive).
- Never use `System.Random` or `string.GetHashCode()` for anything that affects results (spec S-D3): their output differs between runtimes. Use `Pcg32` and the FNV-1a hash in `RngStreams`.
- No `PriorityQueue<,>`, `init` accessors or `record` types: Unity's runtime doesn't have them. C# 9 syntax is otherwise fine.
- Simulation time is a `double`, never frame time or real time. Tests use abstract time units (μ = 1 means "1 per time unit").
- Event ties break by (time, `EventType` value, sequence number) (spec S-D4).
- Namespaces match folders: `Unity/Assets/Core/Sim/Events/` → `Throughput.Core.Sim.Events`.
- Private fields: `_camelCase`. Always write access modifiers. One class per file unless a type is tiny and only used by its neighbour.
- Never write `.meta` files by hand for new `.cs` files or folders: Unity generates them on the next import (each test run does this). Commit the generated `.meta` files together with their `.cs` files.
- Slow statistical tests carry `[Category("Strict")]` and are excluded from normal runs.
- **Running tests:** `scripts/run-tests.sh` (created in Task 1) needs the **Unity editor closed** (batch mode locks the project). If the editor is open, run the same tests from *Window → General → Test Runner → EditMode* instead.
- **Commits:** the user's rule is "commit only when asked". Commit steps below run only if the user approved per-task commits when choosing the execution method; otherwise skip them and commit at the end. Commits go straight to `main` and end with the `Co-Authored-By` line.

## Review Focus

1. **Theory formulas called with ρ ≥ 1** (M/M/1, Erlang C, P-K, Kingman): a person expects an error, not a negative or meaningless wait. → `TheoryTests.Formulas_RejectOverloadedSystems` (Task 4).
2. **Bad distribution inputs** (mean ≤ 0, NaN, negative CV): expect a clear `ArgumentOutOfRangeException`, not silent NaN service times. → `DistributionTests.FromMeanCv_RejectsBadInputs` (Task 3).
3. **Fixed arrival lists that are unsorted or negative**: expect a clear error at construction, not a "time went backward" crash mid-run. → `SingleStationTests.FixedArrivals_RejectUnsortedOrNegativeTimes` (Task 6).
4. **A station with 0 staff**: expect a clear error (closing a lane is a later feature, done differently). → `SingleStationTests.Station_RejectsZeroStaff` (Task 6).
5. **Running in pieces** (`Run(until)` then `Run()`): must give exactly the same timeline as one `Run()`, since phase 2 pauses and resumes. → `SingleStationTests.RunInPieces_EqualsRunAtOnce` (Task 6).

---

### Task 1: Test runner script, PCG32 generator, spec housekeeping

**Files:**
- Create: `scripts/run-tests.sh`
- Create: `Unity/Assets/Core/Sim/Randomness/Pcg32.cs`
- Test: `Unity/Assets/Core/Tests/Randomness/Pcg32Tests.cs`
- Modify: `docs/superpowers/specs/2026-10-11-phase-1-core-design.md` (folder name, open questions, CI level)

**Interfaces:**
- Consumes: nothing.
- Produces: `Throughput.Core.Sim.Randomness.Pcg32` with `Pcg32(ulong seed, ulong stream)`, `uint NextUInt()`, `double NextDouble()` in [0, 1), `double NextOpenDouble()` in (0, 1). Script `scripts/run-tests.sh [filter] [--strict]`.

- [ ] **Step 1: Create the test runner script**

`scripts/run-tests.sh`:

```bash
#!/usr/bin/env bash
# Runs Throughput's EditMode tests headless through the Unity CLI and prints a short summary.
# The Unity editor must be CLOSED (batch mode locks the project). If it's open, use the Test Runner window.
# Usage: scripts/run-tests.sh [filter] [--strict]
#   filter    regex on the full test name, e.g. "Throughput.Core.Tests.Randomness"
#   --strict  run only [Category("Strict")] tests (default: everything except Strict)
set -u
cd "$(dirname "$0")/.."

UNITY="${UNITY_CLI:-$LOCALAPPDATA/Unity/bin/unity.exe}"
FILTER=""
CATEGORY='!Strict'
for arg in "$@"; do
  case "$arg" in
    --strict) CATEGORY='Strict' ;;
    *) FILTER="$arg" ;;
  esac
done

OUT="Unity/Temp/test-results.xml"
ARGS=(test Unity --mode EditMode --output "$OUT" --no-banner)
if [ -n "$FILTER" ]; then ARGS+=(--filter "$FILTER"); fi

UNITY_NO_CONSENT_PROMPT=1 "$UNITY" "${ARGS[@]}" -- -testCategory "$CATEGORY"
code=$?

if [ -f "$OUT" ]; then
  python - "$OUT" <<'PY'
import sys, xml.etree.ElementTree as ET
root = ET.parse(sys.argv[1]).getroot()
for case in root.iter('test-case'):
    if case.get('result') != 'Passed':
        print(f"{case.get('result').upper()}: {case.get('fullname')}")
        msg = case.find('./failure/message')
        if msg is None:
            msg = case.find('./reason/message')
        if msg is not None and msg.text:
            print("   " + msg.text.strip().replace("\n", "\n   "))
print(f"total {root.get('total')}, passed {root.get('passed')}, failed {root.get('failed')}, "
      f"inconclusive {root.get('inconclusive')}, skipped {root.get('skipped')}")
PY
fi
exit $code
```

Then make it executable:

```bash
chmod +x scripts/run-tests.sh
```

- [ ] **Step 2: Write the failing PCG32 tests**

`Unity/Assets/Core/Tests/Randomness/Pcg32Tests.cs`:

```csharp
using NUnit.Framework;
using Throughput.Core.Sim.Randomness;

namespace Throughput.Core.Tests.Randomness
{
    public class Pcg32Tests
    {
        [Test]
        public void MatchesReferenceOutput()
        {
            // Reference values from the official pcg32-demo (seed 42, stream 54).
            var rng = new Pcg32(42UL, 54UL);
            uint[] expected = { 0xa15c02b7u, 0x7b47f409u, 0xba1d3330u, 0x83d2f293u, 0xbfa4784bu, 0xcbed606eu };

            foreach (uint value in expected)
                Assert.That(rng.NextUInt(), Is.EqualTo(value));
        }

        [Test]
        public void SameSeedAndStream_GiveSameNumbers()
        {
            var a = new Pcg32(7UL, 3UL);
            var b = new Pcg32(7UL, 3UL);

            for (int i = 0; i < 1000; i++)
                Assert.That(a.NextUInt(), Is.EqualTo(b.NextUInt()));
        }

        [Test]
        public void DifferentStream_GivesDifferentNumbers()
        {
            var a = new Pcg32(7UL, 3UL);
            var b = new Pcg32(7UL, 4UL);
            int same = 0;

            for (int i = 0; i < 100; i++)
                if (a.NextUInt() == b.NextUInt()) same++;

            Assert.That(same, Is.LessThan(3));
        }

        [Test]
        public void Doubles_StayInRange()
        {
            var rng = new Pcg32(1UL, 1UL);

            for (int i = 0; i < 10_000; i++)
            {
                double half = rng.NextDouble();
                double open = rng.NextOpenDouble();
                Assert.That(half, Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
                Assert.That(open, Is.GreaterThan(0.0).And.LessThan(1.0));
            }
        }
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `scripts/run-tests.sh "Throughput.Core.Tests.Randomness"` (Unity editor closed)
Expected: the run fails to compile (exit 6) with an error like `The type or namespace name 'Randomness' does not exist in the namespace 'Throughput.Core.Sim'`.

- [ ] **Step 4: Implement PCG32**

`Unity/Assets/Core/Sim/Randomness/Pcg32.cs`:

```csharp
namespace Throughput.Core.Sim.Randomness
{
    /// <summary>
    /// PCG32 random number generator (Melissa O'Neill, pcg-random.org).
    /// Small, fast, and gives the same numbers on every platform. We never use System.Random,
    /// whose output can change between .NET versions (spec S-D3).
    /// </summary>
    public sealed class Pcg32
    {
        private const ulong Multiplier = 6364136223846793005UL;
        private const double ToUnit = 1.0 / 4294967296.0; // 2^-32

        private ulong _state;
        private readonly ulong _increment;

        /// <param name="seed">Starting point, e.g. the run seed.</param>
        /// <param name="stream">Which of 2^63 independent sequences to use.</param>
        public Pcg32(ulong seed, ulong stream)
        {
            _state = 0UL;
            _increment = (stream << 1) | 1UL;
            NextUInt();
            _state = unchecked(_state + seed);
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * Multiplier + _increment);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rotation = (int)(old >> 59);
            return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
        }

        /// <summary>A number in [0, 1).</summary>
        public double NextDouble() => NextUInt() * ToUnit;

        /// <summary>A number in (0, 1): never exactly 0 or 1, so it's safe inside Math.Log.</summary>
        public double NextOpenDouble() => (NextUInt() + 0.5) * ToUnit;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `scripts/run-tests.sh`
Expected: exit 0; summary shows all tests passed, **including `SetupCheckTests.CoreAssembly_DoesNotReferenceUnity` now Passed instead of Inconclusive** (Throughput.Core finally has code). Unity has created `.meta` files next to the new folders and files.

- [ ] **Step 6: Update the spec for decisions made while planning**

In `docs/superpowers/specs/2026-10-11-phase-1-core-design.md`:
- Section 4 code layout: replace `└── Random/     Rng (PCG32), RngStreams, Distributions   ← new folder` with `└── Randomness/ Pcg32, RngStreams, distributions   ← new folder ("Random" would clash with System.Random)`.
- Section 5.3 strict tier: replace `95% confidence interval contains the theory value` with `99% confidence interval contains the theory value` and add the line `(99%, not 95%: with fixed seeds and several cases, a 95% interval would miss by pure luck about 1 time in 20 per case.)` under the table.
- Section 8: Q2 → `Decided: gamma (2026-10-11).` Q3 → `Decided: _camelCase (2026-10-11).` Q1 stays open (needed in S7).
- Change log: add row `| 2026-10-11 | Plan part 1 written: Randomness folder name, 99% CI for strict tier, Q2 and Q3 decided |`.

- [ ] **Step 7: Commit**

```bash
git add scripts/run-tests.sh Unity/Assets/Core docs/superpowers/specs/2026-10-11-phase-1-core-design.md
git commit -m "Add test runner script and PCG32 random generator"
```

---

### Task 2: Named random streams

**Files:**
- Create: `Unity/Assets/Core/Sim/Randomness/RngStreams.cs`
- Test: `Unity/Assets/Core/Tests/Randomness/RngStreamsTests.cs`

**Interfaces:**
- Consumes: `Pcg32(ulong seed, ulong stream)`.
- Produces: `RngStreams(ulong runSeed)`, `ulong RunSeed { get; }`, `Pcg32 Get(string name)` (same name → same instance), `static ulong StreamId(string name)` (FNV-1a 64-bit).

- [ ] **Step 1: Write the failing tests**

`Unity/Assets/Core/Tests/Randomness/RngStreamsTests.cs`:

```csharp
using System;
using NUnit.Framework;
using Throughput.Core.Sim.Randomness;

namespace Throughput.Core.Tests.Randomness
{
    public class RngStreamsTests
    {
        [Test]
        public void SameName_ReturnsSameGenerator()
        {
            var streams = new RngStreams(1UL);
            Assert.That(streams.Get("arrivals"), Is.SameAs(streams.Get("arrivals")));
        }

        [Test]
        public void StreamDoesNotDependOnCreationOrder()
        {
            // P3: adding or using another stream must never change this one's numbers.
            var first = new RngStreams(99UL);
            var a1 = first.Get("arrivals");
            first.Get("service:desk").NextUInt();

            var second = new RngStreams(99UL);
            second.Get("service:desk").NextUInt();
            second.Get("patience").NextUInt();
            var a2 = second.Get("arrivals");

            for (int i = 0; i < 100; i++)
                Assert.That(a1.NextUInt(), Is.EqualTo(a2.NextUInt()));
        }

        [Test]
        public void DifferentNames_GiveDifferentNumbers()
        {
            var streams = new RngStreams(5UL);
            Assert.That(streams.Get("a").NextUInt(), Is.Not.EqualTo(streams.Get("b").NextUInt()));
        }

        [Test]
        public void StreamId_IsStableFnv1a()
        {
            // FNV-1a 64-bit of the empty string is the offset basis; of "a" is a published value.
            Assert.That(RngStreams.StreamId(""), Is.EqualTo(14695981039346656037UL));
            Assert.That(RngStreams.StreamId("a"), Is.EqualTo(0xaf63dc4c8601ec8cUL));
        }

        [Test]
        public void Get_RejectsEmptyName()
        {
            var streams = new RngStreams(1UL);
            Assert.Throws<ArgumentException>(() => streams.Get(""));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-tests.sh "Throughput.Core.Tests.Randomness"`
Expected: compile error, `The type or namespace name 'RngStreams' could not be found`.

- [ ] **Step 3: Implement RngStreams**

`Unity/Assets/Core/Sim/Randomness/RngStreams.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Throughput.Core.Sim.Randomness
{
    /// <summary>
    /// One independent random generator per named source of randomness ("arrivals:entry",
    /// "service:desk", ...), all derived from the run seed. Because each stream only depends on
    /// (run seed, name), using one stream more or less never shifts another (spec, Randomness).
    /// </summary>
    public sealed class RngStreams
    {
        private readonly Dictionary<string, Pcg32> _streams = new Dictionary<string, Pcg32>();

        public RngStreams(ulong runSeed)
        {
            RunSeed = runSeed;
        }

        public ulong RunSeed { get; }

        public Pcg32 Get(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("A random stream needs a name.", nameof(name));

            if (!_streams.TryGetValue(name, out Pcg32 rng))
            {
                rng = new Pcg32(RunSeed, StreamId(name));
                _streams[name] = rng;
            }
            return rng;
        }

        /// <summary>FNV-1a 64-bit hash. Never use string.GetHashCode: it changes between runs in .NET.</summary>
        public static ulong StreamId(string name)
        {
            ulong hash = 14695981039346656037UL;
            foreach (char c in name)
            {
                hash ^= c;
                hash = unchecked(hash * 1099511628211UL);
            }
            return hash;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `scripts/run-tests.sh "Throughput.Core.Tests.Randomness"`
Expected: exit 0, all Randomness tests passed.

- [ ] **Step 5: Commit**

```bash
git add Unity/Assets/Core
git commit -m "Add named random streams derived from the run seed"
```

---

### Task 3: Distributions (exponential, deterministic, gamma by mean and CV)

**Files:**
- Create: `Unity/Assets/Core/Sim/Randomness/IDistribution.cs`
- Create: `Unity/Assets/Core/Sim/Randomness/Exponential.cs`
- Create: `Unity/Assets/Core/Sim/Randomness/Deterministic.cs`
- Create: `Unity/Assets/Core/Sim/Randomness/GammaDistribution.cs`
- Create: `Unity/Assets/Core/Sim/Randomness/Distribution.cs`
- Test: `Unity/Assets/Core/Tests/Randomness/DistributionTests.cs`

**Interfaces:**
- Consumes: `Pcg32.NextOpenDouble()`, `Pcg32.NextDouble()`, `RngStreams.Get(string)`.
- Produces: `interface IDistribution { double Mean { get; } double Cv { get; } double Sample(Pcg32 rng); }`; classes `Exponential(double mean)`, `Deterministic(double value)`, `GammaDistribution(double mean, double cv)`; `static IDistribution Distribution.FromMeanCv(double mean, double cv)` (cv 0 → Deterministic, cv 1 → Exponential, else Gamma).

- [ ] **Step 1: Write the failing tests**

`Unity/Assets/Core/Tests/Randomness/DistributionTests.cs`:

```csharp
using System;
using NUnit.Framework;
using Throughput.Core.Sim.Randomness;

namespace Throughput.Core.Tests.Randomness
{
    public class DistributionTests
    {
        private const int Samples = 200_000;

        private static (double mean, double cv, double min) Measure(IDistribution d, ulong seed)
        {
            var rng = new RngStreams(seed).Get("test");
            double sum = 0, sumSq = 0, min = double.MaxValue;
            for (int i = 0; i < Samples; i++)
            {
                double x = d.Sample(rng);
                sum += x;
                sumSq += x * x;
                if (x < min) min = x;
            }
            double mean = sum / Samples;
            double variance = (sumSq - Samples * mean * mean) / (Samples - 1);
            return (mean, Math.Sqrt(variance) / mean, min);
        }

        [TestCase(2.0)]
        [TestCase(0.25)]
        public void Exponential_HasRightMeanAndCvOfOne(double mean)
        {
            var (m, cv, min) = Measure(new Exponential(mean), 1UL);
            Assert.That(m, Is.EqualTo(mean).Within(1).Percent);
            Assert.That(cv, Is.EqualTo(1.0).Within(2).Percent);
            Assert.That(min, Is.GreaterThanOrEqualTo(0.0));
        }

        [TestCase(1.0, 0.5)]
        [TestCase(3.0, 0.3)]
        [TestCase(1.0, 2.0)]
        public void Gamma_HasRequestedMeanAndCv(double mean, double cv)
        {
            var (m, measuredCv, min) = Measure(new GammaDistribution(mean, cv), 2UL);
            Assert.That(m, Is.EqualTo(mean).Within(1).Percent);
            Assert.That(measuredCv, Is.EqualTo(cv).Within(2).Percent);
            Assert.That(min, Is.GreaterThanOrEqualTo(0.0));
        }

        [Test]
        public void Deterministic_AlwaysReturnsTheValue()
        {
            var d = new Deterministic(120.0);
            var rng = new Pcg32(1UL, 1UL);
            for (int i = 0; i < 10; i++)
                Assert.That(d.Sample(rng), Is.EqualTo(120.0));
            Assert.That(d.Cv, Is.EqualTo(0.0));
        }

        [Test]
        public void FromMeanCv_PicksTheSimplestShape()
        {
            Assert.That(Distribution.FromMeanCv(1.0, 0.0), Is.TypeOf<Deterministic>());
            Assert.That(Distribution.FromMeanCv(1.0, 1.0), Is.TypeOf<Exponential>());
            Assert.That(Distribution.FromMeanCv(1.0, 0.5), Is.TypeOf<GammaDistribution>());

            var d = Distribution.FromMeanCv(4.0, 0.5);
            Assert.That(d.Mean, Is.EqualTo(4.0));
            Assert.That(d.Cv, Is.EqualTo(0.5));
        }

        [Test]
        public void FromMeanCv_RejectsBadInputs()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Distribution.FromMeanCv(0.0, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Distribution.FromMeanCv(-1.0, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Distribution.FromMeanCv(double.NaN, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Distribution.FromMeanCv(1.0, -0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Distribution.FromMeanCv(1.0, double.NaN));
        }

        [Test]
        public void SameSeed_GivesSameSamples()
        {
            var d = new GammaDistribution(1.0, 0.5);
            var a = new RngStreams(3UL).Get("s");
            var b = new RngStreams(3UL).Get("s");
            for (int i = 0; i < 1000; i++)
                Assert.That(d.Sample(a), Is.EqualTo(d.Sample(b)));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-tests.sh "Throughput.Core.Tests.Randomness"`
Expected: compile error, `The type or namespace name 'IDistribution' could not be found`.

- [ ] **Step 3: Implement the distributions**

`Unity/Assets/Core/Sim/Randomness/IDistribution.cs`:

```csharp
namespace Throughput.Core.Sim.Randomness
{
    /// <summary>A source of non-negative random durations (service times, gaps between arrivals).</summary>
    public interface IDistribution
    {
        double Mean { get; }

        /// <summary>Coefficient of variation: standard deviation divided by the mean. 0 = always the same.</summary>
        double Cv { get; }

        double Sample(Pcg32 rng);
    }
}
```

`Unity/Assets/Core/Sim/Randomness/Exponential.cs`:

```csharp
using System;

namespace Throughput.Core.Sim.Randomness
{
    /// <summary>Fully random ("memoryless") durations, CV = 1. The "M" in M/M/1.</summary>
    public sealed class Exponential : IDistribution
    {
        public Exponential(double mean)
        {
            if (!(mean > 0))
                throw new ArgumentOutOfRangeException(nameof(mean), mean, "Mean must be above 0.");
            Mean = mean;
        }

        public double Mean { get; }

        public double Cv => 1.0;

        public double Sample(Pcg32 rng) => -Mean * Math.Log(rng.NextOpenDouble());
    }
}
```

`Unity/Assets/Core/Sim/Randomness/Deterministic.cs`:

```csharp
using System;

namespace Throughput.Core.Sim.Randomness
{
    /// <summary>Always the same duration, CV = 0. The "D" in M/D/1. Uses no random numbers.</summary>
    public sealed class Deterministic : IDistribution
    {
        public Deterministic(double value)
        {
            if (!(value > 0))
                throw new ArgumentOutOfRangeException(nameof(value), value, "Value must be above 0.");
            Mean = value;
        }

        public double Mean { get; }

        public double Cv => 0.0;

        public double Sample(Pcg32 rng) => Mean;
    }
}
```

`Unity/Assets/Core/Sim/Randomness/GammaDistribution.cs`:

```csharp
using System;

namespace Throughput.Core.Sim.Randomness
{
    /// <summary>
    /// Durations with any chosen mean and CV (spec Q2: gamma). Shape k = 1/CV², scale = mean/k.
    /// CV below 1 = more regular than exponential (a scanner belt), above 1 = messier.
    /// </summary>
    public sealed class GammaDistribution : IDistribution
    {
        private readonly double _shape;
        private readonly double _scale;

        public GammaDistribution(double mean, double cv)
        {
            if (!(mean > 0))
                throw new ArgumentOutOfRangeException(nameof(mean), mean, "Mean must be above 0.");
            if (!(cv > 0))
                throw new ArgumentOutOfRangeException(nameof(cv), cv, "Gamma needs a CV above 0. Use Deterministic for CV 0.");
            Mean = mean;
            Cv = cv;
            _shape = 1.0 / (cv * cv);
            _scale = mean / _shape;
        }

        public double Mean { get; }

        public double Cv { get; }

        public double Sample(Pcg32 rng) => SampleStandard(_shape, rng) * _scale;

        // Marsaglia & Tsang (2000). For shape < 1: Gamma(k) = Gamma(k + 1) * U^(1/k).
        private static double SampleStandard(double shape, Pcg32 rng)
        {
            if (shape < 1.0)
            {
                double boosted = SampleStandard(shape + 1.0, rng);
                return boosted * Math.Pow(rng.NextOpenDouble(), 1.0 / shape);
            }

            double d = shape - 1.0 / 3.0;
            double c = 1.0 / Math.Sqrt(9.0 * d);
            while (true)
            {
                double x = StandardNormal(rng);
                double v = 1.0 + c * x;
                if (v <= 0.0)
                    continue;
                v = v * v * v;
                double u = rng.NextOpenDouble();
                if (Math.Log(u) < 0.5 * x * x + d - d * v + d * Math.Log(v))
                    return d * v;
            }
        }

        // Box–Muller, using one of the pair (keeps the generator stateless between samples).
        private static double StandardNormal(Pcg32 rng)
        {
            double u1 = rng.NextOpenDouble();
            double u2 = rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }
    }
}
```

`Unity/Assets/Core/Sim/Randomness/Distribution.cs`:

```csharp
using System;

namespace Throughput.Core.Sim.Randomness
{
    public static class Distribution
    {
        /// <summary>The simplest distribution with this mean and CV: 0 → Deterministic, 1 → Exponential, else Gamma.</summary>
        public static IDistribution FromMeanCv(double mean, double cv)
        {
            if (!(mean > 0))
                throw new ArgumentOutOfRangeException(nameof(mean), mean, "Mean must be above 0.");
            if (!(cv >= 0))
                throw new ArgumentOutOfRangeException(nameof(cv), cv, "CV can't be negative.");

            if (cv == 0.0) return new Deterministic(mean);
            if (cv == 1.0) return new Exponential(mean);
            return new GammaDistribution(mean, cv);
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `scripts/run-tests.sh "Throughput.Core.Tests.Randomness"`
Expected: exit 0, all tests passed. If a Gamma CV case misses by a little, do **not** widen the tolerance: check the shape/scale formulas and the `shape < 1` boost first.

- [ ] **Step 5: Commit**

```bash
git add Unity/Assets/Core
git commit -m "Add exponential, deterministic and gamma distributions"
```

---

### Task 4: Test helpers: statistics and theory formulas

**Files:**
- Create: `Unity/Assets/Core/Tests/Support/Stats.cs`
- Create: `Unity/Assets/Core/Tests/Support/Theory.cs`
- Test: `Unity/Assets/Core/Tests/Support/StatsTests.cs`
- Test: `Unity/Assets/Core/Tests/Support/TheoryTests.cs`

**Interfaces:**
- Consumes: nothing from Core.
- Produces (namespace `Throughput.Core.Tests.Support`):
  - `Stats.Mean(IReadOnlyList<double>)`, `Stats.StdDev(IReadOnlyList<double>)` (sample, n−1), `Stats.ConfidenceInterval99(IReadOnlyList<double>)` → `(double Low, double High)`.
  - `Theory.MM1Wq(double lambda, double mu)`, `Theory.ErlangCProbWait(double lambda, double mu, int c)`, `Theory.ErlangCWq(double lambda, double mu, int c)`, `Theory.PollaczekKhinchineWq(double lambda, double meanService, double cs)`, `Theory.KingmanWq(double lambda, double meanService, double ca, double cs)`, `Theory.ErlangA(double lambda, double mu, int c, double theta)` → `ErlangAResult { double Lq; double ProbAbandon; double Wq; }`.

- [ ] **Step 1: Write the failing tests**

The expected numbers are the worked examples in `docs/game-mechanism/knowledge/formulas/` (already checked by script).

`Unity/Assets/Core/Tests/Support/StatsTests.cs`:

```csharp
using NUnit.Framework;

namespace Throughput.Core.Tests.Support
{
    public class StatsTests
    {
        [Test]
        public void MeanAndStdDev()
        {
            double[] xs = { 2, 4, 4, 4, 5, 5, 7, 9 };
            Assert.That(Stats.Mean(xs), Is.EqualTo(5.0));
            Assert.That(Stats.StdDev(xs), Is.EqualTo(System.Math.Sqrt(32.0 / 7.0)).Within(1e-12));
        }

        [Test]
        public void ConfidenceInterval99_UsesStudentT()
        {
            // n = 5, df = 4, t(0.995) = 4.604; sd = 1.58114; half width = 4.604 * 1.58114 / sqrt(5) = 3.2556
            var (low, high) = Stats.ConfidenceInterval99(new double[] { 1, 2, 3, 4, 5 });
            Assert.That(low, Is.EqualTo(-0.2556).Within(1e-3));
            Assert.That(high, Is.EqualTo(6.2556).Within(1e-3));
        }
    }
}
```

`Unity/Assets/Core/Tests/Support/TheoryTests.cs`:

```csharp
using System;
using NUnit.Framework;

namespace Throughput.Core.Tests.Support
{
    public class TheoryTests
    {
        [TestCase(0.5, 1.0)]
        [TestCase(0.8, 4.0)]
        [TestCase(0.9, 9.0)]
        [TestCase(0.95, 19.0)]
        public void MM1_HockeyStickTable(double lambda, double expectedWq)
        {
            Assert.That(Theory.MM1Wq(lambda, 1.0), Is.EqualTo(expectedWq).Within(1e-9));
        }

        [Test]
        public void ErlangC_WorkedExample()
        {
            // c = 2, λ = 1.8, μ = 1 (erlang-c.md)
            Assert.That(Theory.ErlangCProbWait(1.8, 1.0, 2), Is.EqualTo(0.8526315789).Within(1e-9));
            Assert.That(Theory.ErlangCWq(1.8, 1.0, 2), Is.EqualTo(4.2631578947).Within(1e-9));
        }

        [Test]
        public void ErlangC_WithOneDesk_EqualsMM1()
        {
            Assert.That(Theory.ErlangCWq(0.9, 1.0, 1), Is.EqualTo(Theory.MM1Wq(0.9, 1.0)).Within(1e-9));
        }

        [TestCase(0.0, 4.5)]
        [TestCase(0.5, 5.625)]
        [TestCase(1.0, 9.0)]
        [TestCase(2.0, 22.5)]
        public void PollaczekKhinchine_WorkedExample(double cs, double expectedWq)
        {
            Assert.That(Theory.PollaczekKhinchineWq(0.9, 1.0, cs), Is.EqualTo(expectedWq).Within(1e-9));
        }

        [Test]
        public void Kingman_WorkedExamples()
        {
            Assert.That(Theory.KingmanWq(0.9, 1.0, 0.5, 0.5), Is.EqualTo(2.25).Within(1e-9));
            Assert.That(Theory.KingmanWq(0.9, 1.0, 1.5, 1.0), Is.EqualTo(14.625).Within(1e-9));
        }

        [Test]
        public void Kingman_WithRandomArrivals_EqualsPK()
        {
            Assert.That(Theory.KingmanWq(0.9, 1.0, 1.0, 0.5), Is.EqualTo(Theory.PollaczekKhinchineWq(0.9, 1.0, 0.5)).Within(1e-9));
        }

        [Test]
        public void ErlangA_SpecialCase()
        {
            // c = 1, λ = μ = θ = 1 → Poisson(1) ladder, everything equals e^-1 (erlang-a.md)
            var r = Theory.ErlangA(1.0, 1.0, 1, 1.0);
            Assert.That(r.Lq, Is.EqualTo(Math.Exp(-1)).Within(1e-9));
            Assert.That(r.ProbAbandon, Is.EqualTo(Math.Exp(-1)).Within(1e-9));
            Assert.That(r.Wq, Is.EqualTo(Math.Exp(-1)).Within(1e-9));
        }

        [Test]
        public void ErlangA_WithAlmostInfinitePatience_ApproachesErlangC()
        {
            var r = Theory.ErlangA(1.8, 1.0, 2, 1e-7);
            Assert.That(r.Wq, Is.EqualTo(Theory.ErlangCWq(1.8, 1.0, 2)).Within(1e-3));
            Assert.That(r.ProbAbandon, Is.LessThan(1e-5));
        }

        [Test]
        public void ErlangA_OverloadedStillFinite()
        {
            var r = Theory.ErlangA(3.0, 1.0, 2, 0.5); // ρ = 1.5: Erlang C would be infinite
            Assert.That(double.IsFinite(r.Wq), Is.True);
            Assert.That(r.ProbAbandon, Is.GreaterThan(0.0).And.LessThan(1.0));
        }

        [Test]
        public void Formulas_RejectOverloadedSystems()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Theory.MM1Wq(1.0, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Theory.ErlangCWq(2.0, 1.0, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => Theory.PollaczekKhinchineWq(1.2, 1.0, 0.5));
            Assert.Throws<ArgumentOutOfRangeException>(() => Theory.KingmanWq(1.0, 1.0, 1.0, 1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Theory.ErlangA(1.0, 1.0, 1, 0.0));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-tests.sh "Throughput.Core.Tests.Support"`
Expected: compile error, `The name 'Stats' does not exist in the current context`.

- [ ] **Step 3: Implement Stats**

`Unity/Assets/Core/Tests/Support/Stats.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Throughput.Core.Tests.Support
{
    /// <summary>Small statistics helpers for validation tests.</summary>
    public static class Stats
    {
        // Student t, two-sided 99% (t at 0.995) for df = 1..30.
        private static readonly double[] T995 =
        {
            63.657, 9.925, 5.841, 4.604, 4.032, 3.707, 3.499, 3.355, 3.250, 3.169,
            3.106, 3.055, 3.012, 2.977, 2.947, 2.921, 2.898, 2.878, 2.861, 2.845,
            2.831, 2.819, 2.807, 2.797, 2.787, 2.779, 2.771, 2.763, 2.756, 2.750,
        };

        public static double Mean(IReadOnlyList<double> xs)
        {
            if (xs.Count == 0) throw new ArgumentException("Need at least one value.", nameof(xs));
            double sum = 0;
            foreach (double x in xs) sum += x;
            return sum / xs.Count;
        }

        public static double StdDev(IReadOnlyList<double> xs)
        {
            if (xs.Count < 2) throw new ArgumentException("Need at least two values.", nameof(xs));
            double mean = Mean(xs);
            double sumSq = 0;
            foreach (double x in xs) sumSq += (x - mean) * (x - mean);
            return Math.Sqrt(sumSq / (xs.Count - 1));
        }

        /// <summary>99% confidence interval for the mean of independent replications.</summary>
        public static (double Low, double High) ConfidenceInterval99(IReadOnlyList<double> xs)
        {
            int df = xs.Count - 1;
            double t = df <= T995.Length ? T995[df - 1] : 2.576;
            double half = t * StdDev(xs) / Math.Sqrt(xs.Count);
            double mean = Mean(xs);
            return (mean - half, mean + half);
        }
    }
}
```

- [ ] **Step 4: Implement Theory**

`Unity/Assets/Core/Tests/Support/Theory.cs`:

```csharp
using System;

namespace Throughput.Core.Tests.Support
{
    public readonly struct ErlangAResult
    {
        public ErlangAResult(double lq, double probAbandon, double wq)
        {
            Lq = lq;
            ProbAbandon = probAbandon;
            Wq = wq;
        }

        /// <summary>Average number waiting in line.</summary>
        public double Lq { get; }

        /// <summary>Share of all arrivals who give up.</summary>
        public double ProbAbandon { get; }

        /// <summary>Average wait in line, over all arrivals (Little's Law: Lq / λ).</summary>
        public double Wq { get; }
    }

    /// <summary>
    /// The queueing formulas the engine is checked against. Each one is explained in
    /// docs/game-mechanism/knowledge/formulas/. Symbols: λ arrival rate, μ service rate per desk,
    /// c desks, Cs / Ca coefficients of variation, θ abandonment rate (1 / mean patience).
    /// </summary>
    public static class Theory
    {
        public static double MM1Wq(double lambda, double mu)
        {
            double rho = Utilization(lambda, mu, 1);
            return rho / (mu - lambda);
        }

        public static double ErlangCProbWait(double lambda, double mu, int c)
        {
            double rho = Utilization(lambda, mu, c);
            double a = lambda / mu;

            // term = a^k / k!, built step by step to avoid huge factorials.
            double term = 1.0;
            double sum = 0.0;
            for (int k = 0; k < c; k++)
            {
                sum += term;
                term *= a / (k + 1);
            }
            double top = term / (1.0 - rho); // term is now a^c / c!
            return top / (sum + top);
        }

        public static double ErlangCWq(double lambda, double mu, int c)
        {
            return ErlangCProbWait(lambda, mu, c) / (c * mu - lambda);
        }

        public static double PollaczekKhinchineWq(double lambda, double meanService, double cs)
        {
            double rho = Utilization(lambda, 1.0 / meanService, 1);
            return rho / (1.0 - rho) * (1.0 + cs * cs) / 2.0 * meanService;
        }

        public static double KingmanWq(double lambda, double meanService, double ca, double cs)
        {
            double rho = Utilization(lambda, 1.0 / meanService, 1);
            return (ca * ca + cs * cs) / 2.0 * rho / (1.0 - rho) * meanService;
        }

        /// <summary>Exact Erlang A (M/M/c+M) by walking the state ladder (erlang-a.md).</summary>
        public static ErlangAResult ErlangA(double lambda, double mu, int c, double theta)
        {
            if (!(lambda > 0)) throw new ArgumentOutOfRangeException(nameof(lambda), lambda, "λ must be above 0.");
            if (!(mu > 0)) throw new ArgumentOutOfRangeException(nameof(mu), mu, "μ must be above 0.");
            if (c < 1) throw new ArgumentOutOfRangeException(nameof(c), c, "Need at least one desk.");
            if (!(theta > 0)) throw new ArgumentOutOfRangeException(nameof(theta), theta, "θ must be above 0 (use Erlang C for infinite patience).");

            var weights = new System.Collections.Generic.List<double> { 1.0 };
            double total = 1.0;
            for (int n = 1; n < 10_000_000; n++)
            {
                double down = Math.Min(n, c) * mu + Math.Max(n - c, 0) * theta;
                double next = weights[n - 1] * lambda / down;
                if (double.IsInfinity(next))
                    throw new ArgumentOutOfRangeException(nameof(theta), theta, "Patience too long for this overload; the ladder overflows.");
                weights.Add(next);
                total += next;
                if (n > c && next < 1e-16 * total)
                    break;
            }

            double lq = 0.0;
            for (int n = c + 1; n < weights.Count; n++)
                lq += weights[n] / total * (n - c);

            return new ErlangAResult(lq, theta * lq / lambda, lq / lambda);
        }

        private static double Utilization(double lambda, double mu, int c)
        {
            if (!(lambda > 0)) throw new ArgumentOutOfRangeException(nameof(lambda), lambda, "λ must be above 0.");
            if (!(mu > 0)) throw new ArgumentOutOfRangeException(nameof(mu), mu, "μ must be above 0.");
            if (c < 1) throw new ArgumentOutOfRangeException(nameof(c), c, "Need at least one desk.");
            double rho = lambda / (c * mu);
            if (rho >= 1.0)
                throw new ArgumentOutOfRangeException(nameof(lambda), lambda, $"ρ = {rho:F3}: the line grows forever, so this formula has no answer.");
            return rho;
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `scripts/run-tests.sh "Throughput.Core.Tests.Support"`
Expected: exit 0, all StatsTests and TheoryTests passed.

- [ ] **Step 6: Commit**

```bash
git add Unity/Assets/Core
git commit -m "Add statistics helpers and queueing theory formulas for validation"
```

---

### Task 5: Event list with fixed tie-breaking

**Files:**
- Create: `Unity/Assets/Core/Sim/Events/EventType.cs`
- Create: `Unity/Assets/Core/Sim/Events/SimEvent.cs`
- Create: `Unity/Assets/Core/Sim/Events/EventList.cs`
- Create: `Unity/Assets/Core/Sim/Network/Passenger.cs`
- Test: `Unity/Assets/Core/Tests/Events/EventListTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `enum EventType { EndService = 0, Arrive = 1 }` (value = tie-break priority, lower first).
  - `Passenger(int id, double arrivalTime)` with `int Id`, `double ArrivalTime` (namespace `Throughput.Core.Sim.Network`).
  - `SimEvent(double time, EventType type, object node, Passenger passenger)` with `Time`, `Type`, `Node`, `Passenger`, `long Seq` (set by `EventList`).
  - `EventList` with `int Count`, `void Push(SimEvent)`, `SimEvent Peek()`, `SimEvent Pop()` (both throw `InvalidOperationException` when empty).

- [ ] **Step 1: Write the failing tests**

`Unity/Assets/Core/Tests/Events/EventListTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Throughput.Core.Sim.Events;

namespace Throughput.Core.Tests.Events
{
    public class EventListTests
    {
        private static SimEvent At(double time, EventType type = EventType.Arrive, string tag = null)
            => new SimEvent(time, type, tag, null);

        [Test]
        public void PopsInTimeOrder()
        {
            var list = new EventList();
            foreach (double t in new[] { 5.0, 1.0, 4.0, 2.0, 3.0, 0.5 })
                list.Push(At(t));

            var popped = new List<double>();
            while (list.Count > 0)
                popped.Add(list.Pop().Time);

            Assert.That(popped, Is.EqualTo(new[] { 0.5, 1.0, 2.0, 3.0, 4.0, 5.0 }));
        }

        [Test]
        public void SameTime_EndServiceBeforeArrive()
        {
            // A desk that frees up at t can serve someone arriving at t (spec S-D4).
            var list = new EventList();
            list.Push(At(10.0, EventType.Arrive));
            list.Push(At(10.0, EventType.EndService));

            Assert.That(list.Pop().Type, Is.EqualTo(EventType.EndService));
            Assert.That(list.Pop().Type, Is.EqualTo(EventType.Arrive));
        }

        [Test]
        public void SameTimeAndType_KeepsInsertionOrder()
        {
            var list = new EventList();
            for (int i = 0; i < 50; i++)
                list.Push(At(7.0, EventType.Arrive, "e" + i));

            for (int i = 0; i < 50; i++)
                Assert.That(list.Pop().Node, Is.EqualTo("e" + i));
        }

        [Test]
        public void Empty_PeekAndPopThrow()
        {
            var list = new EventList();
            Assert.Throws<InvalidOperationException>(() => list.Peek());
            Assert.Throws<InvalidOperationException>(() => list.Pop());
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-tests.sh "Throughput.Core.Tests.Events"`
Expected: compile error, `The type or namespace name 'Events' does not exist in the namespace 'Throughput.Core.Sim'`.

- [ ] **Step 3: Implement**

`Unity/Assets/Core/Sim/Network/Passenger.cs`:

```csharp
namespace Throughput.Core.Sim.Network
{
    public sealed class Passenger
    {
        public Passenger(int id, double arrivalTime)
        {
            Id = id;
            ArrivalTime = arrivalTime;
        }

        /// <summary>0, 1, 2, ... in arrival order across the whole run.</summary>
        public int Id { get; }

        public double ArrivalTime { get; }
    }
}
```

`Unity/Assets/Core/Sim/Events/EventType.cs`:

```csharp
namespace Throughput.Core.Sim.Events
{
    /// <summary>
    /// The kinds of scheduled event. The number is the tie-break priority when two events share
    /// a time: lower goes first (spec S-D4). EndService before Arrive, so a desk that frees up
    /// at time t can serve someone arriving at t.
    /// </summary>
    public enum EventType
    {
        EndService = 0,
        Arrive = 1,
    }
}
```

`Unity/Assets/Core/Sim/Events/SimEvent.cs`:

```csharp
using Throughput.Core.Sim.Network;

namespace Throughput.Core.Sim.Events
{
    public sealed class SimEvent
    {
        public SimEvent(double time, EventType type, object node, Passenger passenger)
        {
            Time = time;
            Type = type;
            Node = node;
            Passenger = passenger;
        }

        public double Time { get; }

        public EventType Type { get; }

        /// <summary>The Source or Station this event belongs to.</summary>
        public object Node { get; }

        public Passenger Passenger { get; }

        /// <summary>Insertion number, set by EventList. Breaks the last ties.</summary>
        public long Seq { get; internal set; }
    }
}
```

`Unity/Assets/Core/Sim/Events/EventList.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Throughput.Core.Sim.Events
{
    /// <summary>Binary min-heap of events ordered by (Time, Type, Seq). Same inputs → same order, always.</summary>
    public sealed class EventList
    {
        private readonly List<SimEvent> _heap = new List<SimEvent>();
        private long _nextSeq;

        public int Count => _heap.Count;

        public void Push(SimEvent e)
        {
            e.Seq = _nextSeq++;
            _heap.Add(e);
            SiftUp(_heap.Count - 1);
        }

        public SimEvent Peek()
        {
            if (_heap.Count == 0)
                throw new InvalidOperationException("The event list is empty.");
            return _heap[0];
        }

        public SimEvent Pop()
        {
            SimEvent top = Peek();
            int last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);
            if (_heap.Count > 0)
                SiftDown(0);
            return top;
        }

        private static bool Before(SimEvent a, SimEvent b)
        {
            if (a.Time != b.Time) return a.Time < b.Time;
            if (a.Type != b.Type) return a.Type < b.Type;
            return a.Seq < b.Seq;
        }

        private void SiftUp(int i)
        {
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (!Before(_heap[i], _heap[parent])) break;
                Swap(i, parent);
                i = parent;
            }
        }

        private void SiftDown(int i)
        {
            while (true)
            {
                int left = 2 * i + 1;
                int right = left + 1;
                int smallest = i;
                if (left < _heap.Count && Before(_heap[left], _heap[smallest])) smallest = left;
                if (right < _heap.Count && Before(_heap[right], _heap[smallest])) smallest = right;
                if (smallest == i) break;
                Swap(i, smallest);
                i = smallest;
            }
        }

        private void Swap(int a, int b)
        {
            SimEvent tmp = _heap[a];
            _heap[a] = _heap[b];
            _heap[b] = tmp;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `scripts/run-tests.sh "Throughput.Core.Tests.Events"`
Expected: exit 0, all EventListTests passed.

- [ ] **Step 5: Commit**

```bash
git add Unity/Assets/Core
git commit -m "Add event list with fixed tie-breaking order"
```

---

### Task 6: Simulator, source, station and run log (hand-calculated tests)

**Files:**
- Create: `Unity/Assets/Core/Sim/Network/IArrivalProcess.cs` (interface + `RenewalArrivals` + `FixedArrivals`: three tiny types used only together)
- Create: `Unity/Assets/Core/Sim/Network/Source.cs`
- Create: `Unity/Assets/Core/Sim/Network/Station.cs`
- Create: `Unity/Assets/Core/Sim/Events/ISimObserver.cs`
- Create: `Unity/Assets/Core/Sim/Events/Simulator.cs`
- Create: `Unity/Assets/Core/Results/RunLog.cs` (with `LogKind`, `LogEntry`, `PassengerRecord`)
- Test: `Unity/Assets/Core/Tests/Sim/SingleStationTests.cs`

**Interfaces:**
- Consumes: `EventList`, `SimEvent`, `EventType`, `Passenger`, `IDistribution`, `Deterministic`, `Pcg32`.
- Produces:
  - `interface IArrivalProcess { bool TryNext(double now, out double time); }`; `RenewalArrivals(IDistribution gap, Pcg32 rng)`; `FixedArrivals(params double[] times)` (throws `ArgumentOutOfRangeException` on negative/NaN, `ArgumentException` when out of order).
  - `Source(string name, IArrivalProcess arrivals, Station target, double stopTime = double.PositiveInfinity)`; no arrival at or after `StopTime`.
  - `Station(string name, int staff, IDistribution service, Pcg32 rng)` with `Name`, `Staff`, `Busy`, `QueueLength`; throws `ArgumentOutOfRangeException` when `staff < 1`.
  - `interface ISimObserver { OnArrive(double, Passenger); OnJoinQueue(double, Station, Passenger); OnStartService(double, Station, Passenger); OnEndService(double, Station, Passenger); OnExit(double, Passenger); }`
  - `Simulator(params Source[] sources)` with `double Now`, `void AddObserver(ISimObserver)`, `void Run(double until = double.PositiveInfinity)` (resumable).
  - `RunLog(bool keepEntries = true) : ISimObserver` with `IReadOnlyList<PassengerRecord> Passengers`, `IReadOnlyList<LogEntry> Entries`.
  - `PassengerRecord`: `Id`, `Arrive`, `Station`, `QueueJoin`, `ServiceStart`, `ServiceEnd`, `Exit` (NaN until set), `Wait` (= ServiceStart − QueueJoin), `HasExited`.
  - `LogEntry`: `Time`, `Kind` (`LogKind { Arrive, JoinQueue, StartService, EndService, Exit }`), `PassengerId`, `Station` (null for Arrive/Exit), `QueueLength`, `Busy` (engine state right after the step), `ToString()` with round-trip time formatting.

- [ ] **Step 1: Write the failing hand-calculation tests**

`Unity/Assets/Core/Tests/Sim/SingleStationTests.cs`:

```csharp
using System;
using System.Linq;
using NUnit.Framework;
using Throughput.Core.Results;
using Throughput.Core.Sim.Events;
using Throughput.Core.Sim.Network;
using Throughput.Core.Sim.Randomness;

namespace Throughput.Core.Tests.Sim
{
    /// <summary>No randomness: fixed arrivals, fixed service times, timelines worked out by hand.</summary>
    public class SingleStationTests
    {
        private static (Simulator sim, RunLog log) Build(int staff, double service, params double[] arrivals)
        {
            var station = new Station("desk", staff, new Deterministic(service), new Pcg32(1UL, 1UL));
            var source = new Source("entry", new FixedArrivals(arrivals), station);
            var sim = new Simulator(source);
            var log = new RunLog();
            sim.AddObserver(log);
            return (sim, log);
        }

        [Test]
        public void OneDesk_Fifo_HandTimeline()
        {
            // 1 desk, 120 s each, arrivals at 0, 30, 60.
            // A: 0–120 (wait 0). B: 120–240 (wait 90). C: 240–360 (wait 180).
            var (sim, log) = Build(1, 120.0, 0.0, 30.0, 60.0);
            sim.Run();

            var p = log.Passengers;
            Assert.That(p.Select(r => r.ServiceStart), Is.EqualTo(new[] { 0.0, 120.0, 240.0 }));
            Assert.That(p.Select(r => r.ServiceEnd), Is.EqualTo(new[] { 120.0, 240.0, 360.0 }));
            Assert.That(p.Select(r => r.Wait), Is.EqualTo(new[] { 0.0, 90.0, 180.0 }));
            Assert.That(p.Select(r => r.Exit), Is.EqualTo(new[] { 120.0, 240.0, 360.0 }));
            Assert.That(sim.Now, Is.EqualTo(360.0));
        }

        [Test]
        public void SameTime_DeskFreesBeforeNextArrival()
        {
            // B arrives exactly when A finishes: EndService goes first, so B doesn't wait.
            var (sim, log) = Build(1, 120.0, 0.0, 120.0);
            sim.Run();

            Assert.That(log.Passengers[1].Wait, Is.EqualTo(0.0));
            var at120 = log.Entries.Where(e => e.Time == 120.0).Select(e => e.Kind).ToArray();
            Assert.That(at120, Is.EqualTo(new[]
            {
                LogKind.EndService, LogKind.Exit, LogKind.Arrive, LogKind.JoinQueue, LogKind.StartService,
            }));
        }

        [Test]
        public void IdleDesk_ServesLateArrivalImmediately()
        {
            var (sim, log) = Build(1, 120.0, 0.0, 500.0);
            sim.Run();

            Assert.That(log.Passengers[1].ServiceStart, Is.EqualTo(500.0));
            Assert.That(log.Passengers[1].Wait, Is.EqualTo(0.0));
        }

        [Test]
        public void TwoStaff_ServeTwoAtOnce()
        {
            // 3 arrive together, 2 staff, 100 s each: two start at 0, the third at 100.
            var (sim, log) = Build(2, 100.0, 0.0, 0.0, 0.0);
            sim.Run();

            Assert.That(log.Passengers.Select(r => r.ServiceStart), Is.EqualTo(new[] { 0.0, 0.0, 100.0 }));
            Assert.That(log.Entries.Max(e => e.Busy), Is.EqualTo(2));
        }

        [Test]
        public void StopTime_EndsArrivals()
        {
            // Every 10 s, stop at 35 → arrivals at 10, 20, 30 only.
            var station = new Station("desk", 1, new Deterministic(1.0), new Pcg32(1UL, 1UL));
            var source = new Source("entry", new RenewalArrivals(new Deterministic(10.0), new Pcg32(1UL, 2UL)), station, stopTime: 35.0);
            var sim = new Simulator(source);
            var log = new RunLog();
            sim.AddObserver(log);
            sim.Run();

            Assert.That(log.Passengers.Select(r => r.Arrive), Is.EqualTo(new[] { 10.0, 20.0, 30.0 }));
        }

        [Test]
        public void RunInPieces_EqualsRunAtOnce()
        {
            var (whole, wholeLog) = Build(1, 120.0, 0.0, 30.0, 60.0);
            whole.Run();

            var (pieces, piecesLog) = Build(1, 120.0, 0.0, 30.0, 60.0);
            pieces.Run(until: 150.0);
            Assert.That(pieces.Now, Is.LessThanOrEqualTo(150.0));
            pieces.Run();

            Assert.That(piecesLog.Entries.Select(e => e.ToString()), Is.EqualTo(wholeLog.Entries.Select(e => e.ToString())));
        }

        [Test]
        public void FixedArrivals_RejectUnsortedOrNegativeTimes()
        {
            Assert.Throws<ArgumentException>(() => new FixedArrivals(0.0, 50.0, 20.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedArrivals(-1.0, 5.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedArrivals(double.NaN));
        }

        [Test]
        public void Station_RejectsZeroStaff()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Station("desk", 0, new Deterministic(1.0), new Pcg32(1UL, 1UL)));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-tests.sh "Throughput.Core.Tests.Sim"`
Expected: compile error, `The type or namespace name 'Results' does not exist in the namespace 'Throughput.Core'`.

- [ ] **Step 3: Implement arrivals, source and station**

`Unity/Assets/Core/Sim/Network/IArrivalProcess.cs`:

```csharp
using System;
using Throughput.Core.Sim.Randomness;

namespace Throughput.Core.Sim.Network
{
    /// <summary>Decides when the next passenger arrives at a Source.</summary>
    public interface IArrivalProcess
    {
        /// <summary>Returns false when no more passengers will come.</summary>
        bool TryNext(double now, out double time);
    }

    /// <summary>Gaps between arrivals drawn from a distribution (Exponential gaps = Poisson arrivals).</summary>
    public sealed class RenewalArrivals : IArrivalProcess
    {
        private readonly IDistribution _gap;
        private readonly Pcg32 _rng;

        public RenewalArrivals(IDistribution gap, Pcg32 rng)
        {
            _gap = gap ?? throw new ArgumentNullException(nameof(gap));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public bool TryNext(double now, out double time)
        {
            time = now + _gap.Sample(_rng);
            return true;
        }
    }

    /// <summary>A fixed list of arrival times, for hand-calculated tests and scripted groups.</summary>
    public sealed class FixedArrivals : IArrivalProcess
    {
        private readonly double[] _times;
        private int _next;

        public FixedArrivals(params double[] times)
        {
            if (times == null) throw new ArgumentNullException(nameof(times));
            for (int i = 0; i < times.Length; i++)
            {
                if (!(times[i] >= 0))
                    throw new ArgumentOutOfRangeException(nameof(times), times[i], "Arrival times can't be negative or NaN.");
                if (i > 0 && times[i] < times[i - 1])
                    throw new ArgumentException($"Arrival times must be in order: {times[i]} comes after {times[i - 1]}.", nameof(times));
            }
            _times = (double[])times.Clone();
        }

        public bool TryNext(double now, out double time)
        {
            if (_next >= _times.Length)
            {
                time = 0.0;
                return false;
            }
            time = _times[_next++];
            return true;
        }
    }
}
```

`Unity/Assets/Core/Sim/Network/Source.cs`:

```csharp
using System;

namespace Throughput.Core.Sim.Network
{
    /// <summary>Where passengers appear. Sends each new passenger to its target station.</summary>
    public sealed class Source
    {
        public Source(string name, IArrivalProcess arrivals, Station target, double stopTime = double.PositiveInfinity)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A source needs a name.", nameof(name));
            Name = name;
            Arrivals = arrivals ?? throw new ArgumentNullException(nameof(arrivals));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            StopTime = stopTime;
        }

        public string Name { get; }

        public IArrivalProcess Arrivals { get; }

        public Station Target { get; }

        /// <summary>No arrivals at or after this time (the end of the wave).</summary>
        public double StopTime { get; }
    }
}
```

`Unity/Assets/Core/Sim/Network/Station.cs`:

```csharp
using System;
using System.Collections.Generic;
using Throughput.Core.Sim.Events;
using Throughput.Core.Sim.Randomness;

namespace Throughput.Core.Sim.Network
{
    /// <summary>A stage with one FIFO line and Staff servers, each serving one passenger at a time.</summary>
    public sealed class Station
    {
        private readonly Queue<Passenger> _line = new Queue<Passenger>();
        private readonly IDistribution _service;
        private readonly Pcg32 _rng;
        private int _busy;

        public Station(string name, int staff, IDistribution service, Pcg32 rng)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A station needs a name.", nameof(name));
            if (staff < 1) throw new ArgumentOutOfRangeException(nameof(staff), staff, "A station needs at least 1 staff.");
            Name = name;
            Staff = staff;
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public string Name { get; }

        public int Staff { get; }

        /// <summary>Staff currently serving someone.</summary>
        public int Busy => _busy;

        /// <summary>People waiting in line (not counting those being served).</summary>
        public int QueueLength => _line.Count;

        internal void Accept(Simulator sim, Passenger passenger)
        {
            _line.Enqueue(passenger);
            sim.NotifyJoinQueue(this, passenger);
            TryStart(sim);
        }

        internal void Finish(Simulator sim, Passenger passenger)
        {
            _busy--;
            sim.NotifyEndService(this, passenger);
            sim.NotifyExit(passenger);
            TryStart(sim);
        }

        private void TryStart(Simulator sim)
        {
            while (_busy < Staff && _line.Count > 0)
            {
                Passenger next = _line.Dequeue();
                _busy++;
                sim.NotifyStartService(this, next);
                sim.Schedule(sim.Now + _service.Sample(_rng), EventType.EndService, this, next);
            }
        }
    }
}
```

- [ ] **Step 4: Implement the observer interface and the simulator**

`Unity/Assets/Core/Sim/Events/ISimObserver.cs`:

```csharp
using Throughput.Core.Sim.Network;

namespace Throughput.Core.Sim.Events
{
    /// <summary>Watches the simulation. Observers can't change it (spec, Observers).</summary>
    public interface ISimObserver
    {
        void OnArrive(double time, Passenger passenger);

        void OnJoinQueue(double time, Station station, Passenger passenger);

        void OnStartService(double time, Station station, Passenger passenger);

        void OnEndService(double time, Station station, Passenger passenger);

        void OnExit(double time, Passenger passenger);
    }
}
```

`Unity/Assets/Core/Sim/Events/Simulator.cs`:

```csharp
using System;
using System.Collections.Generic;
using Throughput.Core.Sim.Network;

namespace Throughput.Core.Sim.Events
{
    /// <summary>
    /// The event loop: take the earliest event, handle it, repeat. Time only moves by jumping
    /// to the next event; it never depends on frames or real time.
    /// </summary>
    public sealed class Simulator
    {
        private readonly EventList _events = new EventList();
        private readonly List<ISimObserver> _observers = new List<ISimObserver>();
        private readonly Source[] _sources;
        private int _nextPassengerId;
        private bool _started;

        public Simulator(params Source[] sources)
        {
            if (sources == null || sources.Length == 0)
                throw new ArgumentException("A simulation needs at least one source.", nameof(sources));
            foreach (Source s in sources)
                if (s == null) throw new ArgumentException("Sources can't be null.", nameof(sources));
            _sources = (Source[])sources.Clone();
        }

        public double Now { get; private set; }

        public void AddObserver(ISimObserver observer)
        {
            _observers.Add(observer ?? throw new ArgumentNullException(nameof(observer)));
        }

        /// <summary>Handles every event up to and including time <paramref name="until"/>. Can be called again to continue.</summary>
        public void Run(double until = double.PositiveInfinity)
        {
            if (!_started)
            {
                _started = true;
                foreach (Source source in _sources)
                    ScheduleNextArrival(source);
            }

            while (_events.Count > 0 && _events.Peek().Time <= until)
            {
                SimEvent e = _events.Pop();
                Now = e.Time;
                Handle(e);
            }
        }

        internal void Schedule(double time, EventType type, object node, Passenger passenger)
        {
            if (!(time >= Now))
                throw new InvalidOperationException($"Can't schedule an event at {time}: the clock is already at {Now}.");
            _events.Push(new SimEvent(time, type, node, passenger));
        }

        internal void NotifyJoinQueue(Station station, Passenger p)
        {
            foreach (ISimObserver o in _observers) o.OnJoinQueue(Now, station, p);
        }

        internal void NotifyStartService(Station station, Passenger p)
        {
            foreach (ISimObserver o in _observers) o.OnStartService(Now, station, p);
        }

        internal void NotifyEndService(Station station, Passenger p)
        {
            foreach (ISimObserver o in _observers) o.OnEndService(Now, station, p);
        }

        internal void NotifyExit(Passenger p)
        {
            foreach (ISimObserver o in _observers) o.OnExit(Now, p);
        }

        private void Handle(SimEvent e)
        {
            switch (e.Type)
            {
                case EventType.Arrive:
                    var source = (Source)e.Node;
                    var passenger = new Passenger(_nextPassengerId++, Now);
                    foreach (ISimObserver o in _observers) o.OnArrive(Now, passenger);
                    ScheduleNextArrival(source);
                    source.Target.Accept(this, passenger);
                    break;
                case EventType.EndService:
                    ((Station)e.Node).Finish(this, e.Passenger);
                    break;
                default:
                    throw new InvalidOperationException($"Unhandled event type {e.Type}.");
            }
        }

        private void ScheduleNextArrival(Source source)
        {
            if (source.Arrivals.TryNext(Now, out double time) && time < source.StopTime)
                Schedule(time, EventType.Arrive, source, null);
        }
    }
}
```

- [ ] **Step 5: Implement the run log**

`Unity/Assets/Core/Results/RunLog.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using Throughput.Core.Sim.Events;
using Throughput.Core.Sim.Network;

namespace Throughput.Core.Results
{
    public enum LogKind
    {
        Arrive,
        JoinQueue,
        StartService,
        EndService,
        Exit,
    }

    /// <summary>One step of the run, with the engine's own queue length and busy count right after it.</summary>
    public readonly struct LogEntry
    {
        public LogEntry(double time, LogKind kind, int passengerId, string station, int queueLength, int busy)
        {
            Time = time;
            Kind = kind;
            PassengerId = passengerId;
            Station = station;
            QueueLength = queueLength;
            Busy = busy;
        }

        public double Time { get; }

        public LogKind Kind { get; }

        public int PassengerId { get; }

        /// <summary>Null for Arrive and Exit.</summary>
        public string Station { get; }

        public int QueueLength { get; }

        public int Busy { get; }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "{0:R} {1} p{2} {3} q={4} b={5}",
                Time, Kind, PassengerId, Station ?? "-", QueueLength, Busy);
    }

    /// <summary>One passenger's journey. Times are NaN until that step happens.</summary>
    public sealed class PassengerRecord
    {
        public PassengerRecord(int id, double arrive)
        {
            Id = id;
            Arrive = arrive;
        }

        public int Id { get; }

        public double Arrive { get; }

        public string Station { get; internal set; }

        public double QueueJoin { get; internal set; } = double.NaN;

        public double ServiceStart { get; internal set; } = double.NaN;

        public double ServiceEnd { get; internal set; } = double.NaN;

        public double Exit { get; internal set; } = double.NaN;

        /// <summary>Time spent in the line before service started.</summary>
        public double Wait => ServiceStart - QueueJoin;

        public bool HasExited => !double.IsNaN(Exit);
    }

    /// <summary>Records every passenger and (optionally) every step. Big runs pass keepEntries: false to save memory.</summary>
    public sealed class RunLog : ISimObserver
    {
        private readonly List<PassengerRecord> _passengers = new List<PassengerRecord>();
        private readonly List<LogEntry> _entries = new List<LogEntry>();
        private readonly bool _keepEntries;

        public RunLog(bool keepEntries = true)
        {
            _keepEntries = keepEntries;
        }

        public IReadOnlyList<PassengerRecord> Passengers => _passengers;

        public IReadOnlyList<LogEntry> Entries => _entries;

        public void OnArrive(double time, Passenger passenger)
        {
            if (passenger.Id != _passengers.Count)
                throw new InvalidOperationException($"Passenger ids must be 0, 1, 2, ... (got {passenger.Id}, expected {_passengers.Count}).");
            _passengers.Add(new PassengerRecord(passenger.Id, time));
            Add(time, LogKind.Arrive, passenger, null);
        }

        public void OnJoinQueue(double time, Station station, Passenger passenger)
        {
            PassengerRecord r = _passengers[passenger.Id];
            r.Station = station.Name;
            r.QueueJoin = time;
            Add(time, LogKind.JoinQueue, passenger, station);
        }

        public void OnStartService(double time, Station station, Passenger passenger)
        {
            _passengers[passenger.Id].ServiceStart = time;
            Add(time, LogKind.StartService, passenger, station);
        }

        public void OnEndService(double time, Station station, Passenger passenger)
        {
            _passengers[passenger.Id].ServiceEnd = time;
            Add(time, LogKind.EndService, passenger, station);
        }

        public void OnExit(double time, Passenger passenger)
        {
            _passengers[passenger.Id].Exit = time;
            Add(time, LogKind.Exit, passenger, null);
        }

        private void Add(double time, LogKind kind, Passenger passenger, Station station)
        {
            if (!_keepEntries) return;
            _entries.Add(new LogEntry(time, kind, passenger.Id, station?.Name,
                station?.QueueLength ?? 0, station?.Busy ?? 0));
        }
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `scripts/run-tests.sh "Throughput.Core.Tests.Sim"`
Expected: exit 0, all 8 SingleStationTests passed. If `SameTime_DeskFreesBeforeNextArrival` fails, check the `EventType` values and `EventList.Before`, not the test.

- [ ] **Step 7: Commit**

```bash
git add Unity/Assets/Core
git commit -m "Add event loop, source, station and run log with hand-calculated tests"
```

---

### Task 7: Invariant checker (Little's Law) and determinism tests

**Files:**
- Create: `Unity/Assets/Core/Tests/Support/InvariantChecker.cs`
- Create: `Unity/Assets/Core/Tests/Support/Experiments.cs`
- Test: `Unity/Assets/Core/Tests/Invariants/InvariantCheckerTests.cs`
- Test: `Unity/Assets/Core/Tests/Sim/DeterminismTests.cs`
- Modify: `Unity/Assets/Core/Tests/Sim/SingleStationTests.cs` (call the checker in every hand test)

**Interfaces:**
- Consumes: `RunLog`, `LogEntry`, `PassengerRecord`, `Simulator`, `Source`, `Station`, `RenewalArrivals`, `RngStreams`, `IDistribution`.
- Produces (namespace `Throughput.Core.Tests.Support`):
  - `InvariantChecker.AssertAll(RunLog log, IReadOnlyDictionary<string, int> staffByStation, bool expectDrained = true)`; fails via NUnit `Assert` with a message naming the broken rule.
  - `Experiments.RunSingleStation(ulong seed, IDistribution gap, IDistribution service, int staff, double stopTime, bool keepEntries)` → `RunLog` (station named `"desk"`, streams `"arrivals:entry"` and `"service:desk"`).
  - `Experiments.MeanWait(RunLog log, double warmup)` → mean `Wait` of passengers who joined the line at or after `warmup`.

- [ ] **Step 1: Write the failing tests**

`Unity/Assets/Core/Tests/Invariants/InvariantCheckerTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Throughput.Core.Results;
using Throughput.Core.Sim.Network;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Invariants
{
    /// <summary>The checker must actually catch broken runs, or a green test means nothing.</summary>
    public class InvariantCheckerTests
    {
        private static readonly Dictionary<string, int> NoStations = new Dictionary<string, int>();

        [Test]
        public void Fails_WhenTimeGoesBackward()
        {
            var log = new RunLog();
            log.OnArrive(5.0, new Passenger(0, 5.0));
            log.OnArrive(3.0, new Passenger(1, 3.0));

            Assert.Throws<AssertionException>(() => InvariantChecker.AssertAll(log, NoStations, expectDrained: false));
        }

        [Test]
        public void Fails_WhenSomeoneNeverLeaves()
        {
            var log = new RunLog();
            log.OnArrive(0.0, new Passenger(0, 0.0));

            Assert.Throws<AssertionException>(() => InvariantChecker.AssertAll(log, NoStations, expectDrained: true));
        }

        [Test]
        public void Passes_OnEmptyRun()
        {
            InvariantChecker.AssertAll(new RunLog(), NoStations);
        }
    }
}
```

`Unity/Assets/Core/Tests/Sim/DeterminismTests.cs`:

```csharp
using System.Linq;
using NUnit.Framework;
using Throughput.Core.Sim.Randomness;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Sim
{
    /// <summary>P3: same seed + same setup → exactly the same run.</summary>
    public class DeterminismTests
    {
        private static string[] Run(ulong seed, double meanService)
        {
            var log = Experiments.RunSingleStation(seed, new Exponential(1.0 / 0.8), new Exponential(meanService),
                staff: 1, stopTime: 500.0, keepEntries: true);
            return log.Entries.Select(e => e.ToString()).ToArray();
        }

        [Test]
        public void SameSeed_IdenticalTimeline()
        {
            Assert.That(Run(42UL, 1.0), Is.EqualTo(Run(42UL, 1.0)));
        }

        [Test]
        public void DifferentSeed_DifferentTimeline()
        {
            Assert.That(Run(42UL, 1.0), Is.Not.EqualTo(Run(43UL, 1.0)));
        }

        [Test]
        public void ChangingService_DoesNotMoveArrivals()
        {
            // Separate random streams: a faster desk must not reshuffle who arrives when.
            var slow = Experiments.RunSingleStation(42UL, new Exponential(1.25), new Exponential(1.0), 1, 500.0, false);
            var fast = Experiments.RunSingleStation(42UL, new Exponential(1.25), new Exponential(0.5), 1, 500.0, false);

            Assert.That(fast.Passengers.Select(p => p.Arrive), Is.EqualTo(slow.Passengers.Select(p => p.Arrive)));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `scripts/run-tests.sh "Throughput.Core.Tests"`
Expected: compile error, `The name 'InvariantChecker' does not exist in the current context`.

- [ ] **Step 3: Implement the invariant checker**

`Unity/Assets/Core/Tests/Support/InvariantChecker.cs`:

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Throughput.Core.Results;

namespace Throughput.Core.Tests.Support
{
    /// <summary>
    /// Rules that must hold after every run (spec 5.2). Needs a RunLog with entries kept.
    /// </summary>
    public static class InvariantChecker
    {
        public static void AssertAll(RunLog log, IReadOnlyDictionary<string, int> staffByStation, bool expectDrained = true)
        {
            AssertTimeNeverGoesBackward(log);
            AssertJourneysInOrder(log);
            AssertConservation(log, expectDrained);
            AssertStaffLimit(log, staffByStation);
            if (expectDrained)
                AssertLittlesLaw(log);
        }

        private static void AssertTimeNeverGoesBackward(RunLog log)
        {
            for (int i = 1; i < log.Entries.Count; i++)
                Assert.That(log.Entries[i].Time, Is.GreaterThanOrEqualTo(log.Entries[i - 1].Time),
                    $"Time went backward at entry {i}: {log.Entries[i - 1]} → {log.Entries[i]}");
        }

        private static void AssertJourneysInOrder(RunLog log)
        {
            foreach (PassengerRecord p in log.Passengers)
            {
                AssertNotBefore(p, p.QueueJoin, p.Arrive, "joined the line before arriving");
                AssertNotBefore(p, p.ServiceStart, p.QueueJoin, "started service before joining the line");
                AssertNotBefore(p, p.ServiceEnd, p.ServiceStart, "finished before starting");
                AssertNotBefore(p, p.Exit, p.ServiceEnd, "left before finishing");
            }
        }

        private static void AssertNotBefore(PassengerRecord p, double later, double earlier, string what)
        {
            if (double.IsNaN(later)) return;
            Assert.That(later, Is.GreaterThanOrEqualTo(earlier), $"Passenger {p.Id} {what}.");
        }

        private static void AssertConservation(RunLog log, bool expectDrained)
        {
            int arrived = log.Passengers.Count;
            int exited = 0;
            foreach (PassengerRecord p in log.Passengers)
                if (p.HasExited) exited++;
            int inside = arrived - exited;

            Assert.That(inside, Is.GreaterThanOrEqualTo(0), "More passengers left than arrived.");
            if (expectDrained)
                Assert.That(inside, Is.EqualTo(0), $"{inside} passengers never left (arrived {arrived}, exited {exited}).");
        }

        private static void AssertStaffLimit(RunLog log, IReadOnlyDictionary<string, int> staffByStation)
        {
            foreach (LogEntry e in log.Entries)
            {
                if (e.Station == null) continue;
                Assert.That(staffByStation.ContainsKey(e.Station), Is.True, $"Unknown station '{e.Station}'.");
                Assert.That(e.Busy, Is.InRange(0, staffByStation[e.Station]),
                    $"Station '{e.Station}' had {e.Busy} busy staff (limit {staffByStation[e.Station]}) at {e.Time}.");
                Assert.That(e.QueueLength, Is.GreaterThanOrEqualTo(0), $"Negative queue at '{e.Station}'.");
            }
        }

        /// <summary>
        /// Little's Law on a run that starts and ends empty: the area under the engine's queue-length
        /// graph must equal the sum of everyone's wait (Lq × T = λ × Wq × T). The queue length comes from
        /// the Station itself; the waits from passenger timestamps, so this checks the two agree.
        /// </summary>
        private static void AssertLittlesLaw(RunLog log)
        {
            var area = new Dictionary<string, double>();
            var lastTime = new Dictionary<string, double>();
            var lastQueue = new Dictionary<string, int>();

            foreach (LogEntry e in log.Entries)
            {
                if (e.Station == null) continue;
                if (!area.ContainsKey(e.Station))
                {
                    area[e.Station] = 0.0;
                    lastTime[e.Station] = 0.0;
                    lastQueue[e.Station] = 0;
                }
                area[e.Station] += lastQueue[e.Station] * (e.Time - lastTime[e.Station]);
                lastTime[e.Station] = e.Time;
                lastQueue[e.Station] = e.QueueLength;
            }

            var waits = new Dictionary<string, double>();
            foreach (PassengerRecord p in log.Passengers)
            {
                if (p.Station == null) continue;
                waits.TryGetValue(p.Station, out double sum);
                waits[p.Station] = sum + p.Wait;
            }

            foreach (KeyValuePair<string, double> kv in area)
            {
                Assert.That(lastQueue[kv.Key], Is.EqualTo(0), $"Line at '{kv.Key}' not empty at the end.");
                waits.TryGetValue(kv.Key, out double totalWait);
                double tolerance = 1e-9 * Math.Max(1.0, totalWait);
                Assert.That(kv.Value, Is.EqualTo(totalWait).Within(tolerance),
                    $"Little's Law broken at '{kv.Key}': queue area {kv.Value} vs total wait {totalWait}.");
            }
        }
    }
}
```

- [ ] **Step 4: Implement the experiment helper**

`Unity/Assets/Core/Tests/Support/Experiments.cs`:

```csharp
using Throughput.Core.Results;
using Throughput.Core.Sim.Events;
using Throughput.Core.Sim.Network;
using Throughput.Core.Sim.Randomness;

namespace Throughput.Core.Tests.Support
{
    /// <summary>Builds and runs the standard test setups.</summary>
    public static class Experiments
    {
        /// <summary>One source → one station ("desk") → exit. Runs until everyone has left.</summary>
        public static RunLog RunSingleStation(ulong seed, IDistribution gap, IDistribution service, int staff,
            double stopTime, bool keepEntries)
        {
            var streams = new RngStreams(seed);
            var station = new Station("desk", staff, service, streams.Get("service:desk"));
            var source = new Source("entry", new RenewalArrivals(gap, streams.Get("arrivals:entry")), station, stopTime);
            var sim = new Simulator(source);
            var log = new RunLog(keepEntries);
            sim.AddObserver(log);
            sim.Run();
            return log;
        }

        /// <summary>Mean wait in line of passengers who joined at or after <paramref name="warmup"/> (skips the empty start).</summary>
        public static double MeanWait(RunLog log, double warmup)
        {
            double sum = 0.0;
            int count = 0;
            foreach (PassengerRecord p in log.Passengers)
            {
                if (p.QueueJoin < warmup) continue;
                sum += p.Wait;
                count++;
            }
            return count == 0 ? double.NaN : sum / count;
        }
    }
}
```

- [ ] **Step 5: Add the invariant check to every hand test**

In `Unity/Assets/Core/Tests/Sim/SingleStationTests.cs`, add `using System.Collections.Generic;` and `using Throughput.Core.Tests.Support;`, then a helper and one call at the end of each test that runs a simulation (`OneDesk_Fifo_HandTimeline`, `SameTime_DeskFreesBeforeNextArrival`, `IdleDesk_ServesLateArrivalImmediately`, `TwoStaff_ServeTwoAtOnce`, `StopTime_EndsArrivals`, `RunInPieces_EqualsRunAtOnce` using `piecesLog`):

```csharp
        private static void CheckInvariants(RunLog log, int staff) =>
            InvariantChecker.AssertAll(log, new Dictionary<string, int> { { "desk", staff } });
```

For example, the end of `OneDesk_Fifo_HandTimeline` becomes:

```csharp
            Assert.That(sim.Now, Is.EqualTo(360.0));
            CheckInvariants(log, staff: 1);
```

and `TwoStaff_ServeTwoAtOnce` ends with `CheckInvariants(log, staff: 2);`.

- [ ] **Step 6: Run all tests to verify they pass**

Run: `scripts/run-tests.sh`
Expected: exit 0; InvariantCheckerTests, DeterminismTests and all SingleStationTests pass.

- [ ] **Step 7: Commit**

```bash
git add Unity/Assets/Core
git commit -m "Add invariant checker with Little's Law and determinism tests"
```

---

### Task 8: M/M/1 validation (fast and strict) and tracker update

**Files:**
- Test: `Unity/Assets/Core/Tests/Validation/MM1ValidationTests.cs`
- Modify: `docs/superpowers/specs/2026-10-11-phase-1-core-design.md` (tick S0 and S1 boxes, progress table)
- Modify: `docs/game-mechanism/knowledge/formulas/README.md`, `littles-law.md`, `mm1.md` (status)

**Interfaces:**
- Consumes: `Experiments.RunSingleStation`, `Experiments.MeanWait`, `InvariantChecker.AssertAll`, `Theory.MM1Wq`, `Stats.Mean`, `Stats.ConfidenceInterval99`, `Exponential`.
- Produces: nothing new.

- [ ] **Step 1: Write the validation tests**

`Unity/Assets/Core/Tests/Validation/MM1ValidationTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Throughput.Core.Sim.Randomness;
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

        private static double SimulatedMeanWait(ulong seed, double rho, int passengers)
        {
            double lambda = rho * Mu;
            var log = Experiments.RunSingleStation(seed, new Exponential(1.0 / lambda), new Exponential(1.0 / Mu),
                staff: 1, stopTime: passengers / lambda, keepEntries: false);
            return Experiments.MeanWait(log, warmup: 1000.0 / lambda); // skip ~1000 arrivals: the line starts empty
        }

        [TestCase(0.5)]
        [TestCase(0.8)]
        public void Fast_MeanWaitWithin10Percent(double rho)
        {
            double sim = SimulatedMeanWait(7UL, rho, 200_000);
            double theory = Theory.MM1Wq(rho * Mu, Mu);
            Assert.That(sim, Is.EqualTo(theory).Within(10).Percent, $"ρ = {rho}: sim {sim:F3} vs theory {theory:F3}");
        }

        [Test, Category("Strict")]
        [TestCase(0.5)]
        [TestCase(0.8)]
        [TestCase(0.9)]
        public void Strict_MeanWaitWithin3PercentAndInsideCI(double rho)
        {
            const int replications = 20;
            const int passengers = 300_000;
            var means = new List<double>();
            for (int i = 0; i < replications; i++)
                means.Add(SimulatedMeanWait((ulong)(1000 + i), rho, passengers));

            double theory = Theory.MM1Wq(rho * Mu, Mu);
            double mean = Stats.Mean(means);
            var (low, high) = Stats.ConfidenceInterval99(means);
            string detail = $"ρ = {rho}: sim {mean:F4} (99% CI {low:F4}–{high:F4}) vs theory {theory:F4}";

            Assert.That(theory, Is.InRange(low, high), detail);
            Assert.That(mean, Is.EqualTo(theory).Within(3).Percent, detail);
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

- [ ] **Step 2: Run the fast tier**

Run: `scripts/run-tests.sh`
Expected: exit 0; `Fast_MeanWaitWithin10Percent(0.5)` and `(0.8)` and `Invariants_HoldOnRandomRun` pass; Strict tests are not run. (These tests pass straight away if Tasks 1–7 are right; that's expected here, since the engine already exists. If one fails, it's a real engine bug: use superpowers:systematic-debugging, don't change the tolerance.)

- [ ] **Step 3: Run the strict tier**

Run: `scripts/run-tests.sh --strict`
Expected: exit 0; all three `Strict_MeanWaitWithin3PercentAndInsideCI` cases pass. This takes longer (about 18 million passengers in total; expect up to a minute or two). NUnit only prints the `sim … vs theory …` message when a case fails, so report the pass/fail summary as shown. If a case fails, its message has the numbers: debug with superpowers:systematic-debugging; never loosen the 3% or the 99% interval to make it pass.

- [ ] **Step 4: Update the tracker and formula status**

In `docs/superpowers/specs/2026-10-11-phase-1-core-design.md`:
- Tick every S0 and S1 checkbox in section 6, plus in 5.2 "Conservation", "Time never goes backward", "Each station serves at most `staff` people at once", "Little's Law holds for each queue"; in 5.4 "Same seed + plan + actions → identical timeline" and "Different seed → different timeline".
- Progress table: S0 → `Done`, S1 → `Done (strict M/M/1 passed <date>)`.
- Change log: `| <date> | S0 and S1 done: PCG32, streams, distributions, event loop, invariants, M/M/1 validated |`.

In `docs/game-mechanism/knowledge/formulas/README.md`: Little's Law and M/M/1 rows → tick "Implemented" and "Strict test passes".
In `littles-law.md` and `mm1.md` Status: tick "Implemented" and the last box.

- [ ] **Step 5: Commit**

```bash
git add Unity/Assets/Core docs
git commit -m "Validate the engine against M/M/1; S0 and S1 done"
```
