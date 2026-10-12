# Phase 1 Part 9: Results and the Fairness Test (S9) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A run produces a summary (missed flights, waits, busy share, throughput, cost in staff-minutes) and a result (pass the level's goal, plus optional extra ticks). A fairness test shows the sample level is fair: a good plan passes almost always, a bad plan fails almost always.

**Architecture:** `RunStats` is an observer (it only watches) given every station of the run; it integrates staffed and busy time per station and adds up each passenger's time in lines. `Summarize(end)` returns a `RunSummary`. `Scoring.Score(level, summary)` checks the level's main goal (missed flights, D9) and its optional extras (longest wait, staff-minutes) and returns a `RunResult`. The level gets an `extras` block.

**Tech Stack:** Unity 6000.3.26f1, C#, NUnit (EditMode), Unity CLI via `scripts/run-tests.sh`.

**Spec:** [docs/superpowers/specs/2026-10-11-phase-1-core-design.md](../specs/2026-10-11-phase-1-core-design.md) (§6 S9, §7 Done when). Game decisions: [decisions.md](../../game-mechanism/decisions.md) D9 (one pass goal + optional extras), P1 (every fix has a cost), P3 (fair, not lucky).

## Global Constraints

- `Throughput.Core` never references `UnityEngine`/`UnityEditor`. Engine code never depends on `Levels`, `Layout` or `Results`.
- Observers can't change a run: `RunStats` only reads.
- Runs stay deterministic; nothing here draws random numbers.
- Private fields `_camelCase`.
- The Unity editor must be closed to run `scripts/run-tests.sh`.
- Git: commit locally after each task; push only when the user asks. Messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Design decisions (the user reviews these)

| # | Decision | Why |
|---|---|---|
| G1 | **Extras in S9: "nobody waits over X minutes" and "spend at most Y staff-minutes"** (user's choice A). | Both measurable now; cost makes every lane a real decision (P1). |
| G2 | **A missed flight = a passenger who gave up.** Runs always end with everyone out, so that's the only way to miss one. | Matches reneging (S4). |
| G3 | **Cost = staff-minutes:** each desk costs 1 per minute it is **staffed**: open, in setup, or still serving after it closed (draining). A shared line is staffed the whole run. The run ends at `LastActiveTime`. | Simple and fair: closing a lane saves money only once it's empty, setup costs money (P1). |
| G4 | **Waits:** a passenger's wait is their total time in lines over all stages (until service or giving up). The average is over everyone who arrived; the longest is the worst passenger. | "Nobody waits over X" means the whole trip. |
| G5 | **Busy share** per station = busy minutes ÷ staffed minutes, where busy includes holding a blocked passenger. | Splitting serving and blocked time stays a deferred note. |
| G6 | **Pass = missed flights ≤ the level's `maxMissedFlights`.** Extras are ticks beside it, no stars (D9). | D9. |
| G7 | **Fairness test:** the sample level, seeds 1–200. The sample plan (3 ID desks, 2 scanners) must pass on ≥ 190 (measured: 199); a short-staffed plan (2 ID desks) must fail on ≥ 160 (measured: 182). | P3 and the phase-1 exit check. Measured before writing this plan with a throwaway probe. |
| G8 | **Deferred:** waits per passenger group (no groups yet), CSV export and charts (optional 5.5). | Nothing uses them yet. |

## Review Focus

1. **A lane closed while still serving.** Expected: it keeps costing until empty, then stops. Pinned by `Stats_ClosedLaneCostsUntilItDrains`.
2. **A lane that is never used but open.** Expected: it costs every minute it is open. Pinned by `Stats_OpenedLaneCostsFromThen` (s2 is open 250 min, busy 100).
3. **People who give up.** Expected: counted as missed, their wait counted until they gave up. Pinned by `Stats_GivingUpIsAMissedFlight`.
4. **A station the stats weren't given.** Expected: a clear error, not silent wrong numbers. Pinned by `Stats_UnknownStation_Throws`.
5. **Summarize before the last change.** Expected: an error. Pinned by `Stats_UnknownStation_Throws` (second assert).

---

## File Structure

| File | Change | Responsibility |
|---|---|---|
| `Unity/Assets/Core/Results/RunStats.cs` | Create | Observer that measures a run; `RunSummary` |
| `Unity/Assets/Core/Results/Scoring.cs` | Create | `Scoring.Score`, `RunResult`, `ExtraResult` |
| `Unity/Assets/Core/Levels/LevelDef.cs` | Modify | `ExtrasDef` |
| `Unity/Assets/Core/Layout/LayoutCompiler.cs` | Modify | Checks goal and extras numbers |
| `Unity/Assets/Core/Tests/Results/RunStatsTests.cs` | Create | Hand tests |
| `Unity/Assets/Core/Tests/Results/ScoringTests.cs` | Create | Pass/fail and extras |
| `Unity/Assets/Core/Tests/Validation/FairnessTests.cs` | Create | 200 seeds, good and bad plan |
| `Unity/Assets/Core/Tests/Support/SampleLevels.cs` | Modify | `ShortStaffedPlan` |
| `Unity/Assets/Core/Tests/Layout/LayoutCompilerTests.cs` | Modify | Extras JSON and checks |

---

### Task 1: RunStats

**Files:**
- Create: `Unity/Assets/Core/Results/RunStats.cs`
- Test: `Unity/Assets/Core/Tests/Results/RunStatsTests.cs`

**Interfaces:**
- Produces: `RunStats(IEnumerable<Station> stations)` (an `IRunObserver`), `RunStats.Summarize(double end) : RunSummary`; `RunSummary` with `Arrived`, `Exited`, `MissedFlights`, `AverageWait`, `LongestWait`, `StaffMinutes`, `End`, `Throughput` (people per minute), `BusyShare : IReadOnlyDictionary<string, double>`, and a public constructor `RunSummary(int arrived, int exited, int missedFlights, double averageWait, double longestWait, double staffMinutes, double end, IReadOnlyDictionary<string, double> busyShare)`.

- [ ] **Step 1: Write the failing tests**

Create `Unity/Assets/Core/Tests/Results/RunStatsTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Throughput.Core.Engine.Behaviours;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;
using Throughput.Core.Engine.Policies;
using Throughput.Core.Engine.Randomness;
using Throughput.Core.Results;

namespace Throughput.Core.Tests.Results
{
    /// <summary>What a run measured (G2–G5). Fixed times, worked out by hand.</summary>
    public class RunStatsTests
    {
        private static Station Desk(string name) => new Station(name, 1, new Deterministic(100.0), new Pcg32(1UL, 1UL));

        private static (Runner runner, RunStats stats, Station s1, Station s2) TwoLanes(params double[] arrivals)
        {
            Station s1 = Desk("s1"), s2 = Desk("s2");
            var stage = new Router("scan", new[] { s1, s2 }, ShortestLine.Instance);
            var runner = new Runner(new Source("entry", new FixedArrivals(arrivals), stage));
            var stats = new RunStats(new[] { s1, s2 });
            runner.AddObserver(stats);
            return (runner, stats, s1, s2);
        }

        [Test]
        public void Stats_OpenedLaneCostsFromThen()
        {
            // s2 closed at 0, opened at 50. A, B, C at 0 all join s1: 0–100, 100–200, 200–300 (waits 0, 100, 200).
            // D at 60 → s2 (empty), 60–160. Run ends at 300.
            // Cost: s1 300 + s2 (300 − 50) = 550. Busy share: s1 300/300, s2 100/250.
            var (runner, stats, s1, s2) = TwoLanes(0.0, 0.0, 0.0, 60.0);
            runner.CloseLane(s2);
            runner.Run(50.0);
            runner.OpenLane(s2);
            runner.Run();
            RunSummary s = stats.Summarize(runner.LastActiveTime);

            Assert.That((s.Arrived, s.Exited, s.MissedFlights), Is.EqualTo((4, 4, 0)));
            Assert.That(s.End, Is.EqualTo(300.0));
            Assert.That(s.AverageWait, Is.EqualTo(75.0));
            Assert.That(s.LongestWait, Is.EqualTo(200.0));
            Assert.That(s.StaffMinutes, Is.EqualTo(550.0));
            Assert.That(s.BusyShare["s1"], Is.EqualTo(1.0));
            Assert.That(s.BusyShare["s2"], Is.EqualTo(0.4));
            Assert.That(s.Throughput, Is.EqualTo(4.0 / 300.0));
        }

        [Test]
        public void Stats_ClosedLaneCostsUntilItDrains()
        {
            // As above, but s2 is closed again at 120 while D is still at its desk (until 160): it costs 50–160 = 110.
            var (runner, stats, s1, s2) = TwoLanes(0.0, 0.0, 0.0, 60.0);
            runner.CloseLane(s2);
            runner.Run(50.0);
            runner.OpenLane(s2);
            runner.Run(120.0);
            runner.CloseLane(s2);
            runner.Run();
            RunSummary s = stats.Summarize(runner.LastActiveTime);

            Assert.That(s.StaffMinutes, Is.EqualTo(410.0));
            Assert.That(s.BusyShare["s2"], Is.EqualTo(100.0 / 110.0));
        }

        [Test]
        public void Stats_GivingUpIsAMissedFlight()
        {
            // One desk, 100 s. A and B at 0; patience 50. A starts at once; B gives up at 50.
            Station s1 = Desk("s1");
            var runner = new Runner(new Source("entry", new FixedArrivals(0.0, 0.0), s1));
            runner.AddBehaviour(new Reneging(new Deterministic(50.0), new Pcg32(1UL, 3UL)));
            var stats = new RunStats(new[] { s1 });
            runner.AddObserver(stats);
            runner.Run();
            RunSummary s = stats.Summarize(runner.LastActiveTime);

            Assert.That((s.Arrived, s.Exited, s.MissedFlights), Is.EqualTo((2, 1, 1)));
            Assert.That(s.AverageWait, Is.EqualTo(25.0));
            Assert.That(s.LongestWait, Is.EqualTo(50.0));
            Assert.That(s.StaffMinutes, Is.EqualTo(100.0));
        }

        [Test]
        public void Stats_UnknownStation_Throws()
        {
            Station s1 = Desk("s1"), other = Desk("other");
            var runner = new Runner(new Source("entry", new FixedArrivals(0.0), s1));
            var stats = new RunStats(new[] { other });
            runner.AddObserver(stats);
            Assert.Throws<System.InvalidOperationException>(() => runner.Run());

            var (runner2, stats2, _, _) = TwoLanes(0.0);
            runner2.Run();
            Assert.Throws<System.ArgumentOutOfRangeException>(() => stats2.Summarize(50.0)); // the last change was at 100
            Assert.Throws<System.ArgumentNullException>(() => new RunStats(null));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `bash scripts/run-tests.sh RunStatsTests`
Expected: compile errors (`RunStats`, `RunSummary` don't exist).

- [ ] **Step 3: Write `RunStats`**

Create `Unity/Assets/Core/Results/RunStats.cs`:

```csharp
using System;
using System.Collections.Generic;
using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;

namespace Throughput.Core.Results
{
    /// <summary>What a run did, summed up (S9, G2–G5). Times in the run's unit (minutes in game levels).</summary>
    public sealed class RunSummary
    {
        public RunSummary(int arrived, int exited, int missedFlights, double averageWait, double longestWait,
            double staffMinutes, double end, IReadOnlyDictionary<string, double> busyShare)
        {
            Arrived = arrived;
            Exited = exited;
            MissedFlights = missedFlights;
            AverageWait = averageWait;
            LongestWait = longestWait;
            StaffMinutes = staffMinutes;
            End = end;
            BusyShare = busyShare ?? new Dictionary<string, double>();
        }

        public int Arrived { get; }

        public int Exited { get; }

        /// <summary>People who gave up (G2).</summary>
        public int MissedFlights { get; }

        /// <summary>Time in lines per passenger, over all stages, averaged over everyone who arrived (G4).</summary>
        public double AverageWait { get; }

        /// <summary>The worst passenger's total time in lines (G4).</summary>
        public double LongestWait { get; }

        /// <summary>Desks × minutes staffed: open, in setup, or still serving after closing (G3).</summary>
        public double StaffMinutes { get; }

        public double End { get; }

        /// <summary>People through per minute.</summary>
        public double Throughput => End > 0 ? Exited / End : 0.0;

        /// <summary>Per station: busy minutes (serving or holding a blocked passenger) ÷ staffed minutes (G5).</summary>
        public IReadOnlyDictionary<string, double> BusyShare { get; }
    }

    /// <summary>
    /// Watches a run and measures it (S9). Give it every station of the run before the run starts; it only reads.
    /// Call Summarize with the run's end (Runner.LastActiveTime) when the run is over.
    /// </summary>
    public sealed class RunStats : IRunObserver
    {
        private sealed class Desk
        {
            public Station Station;
            public double Since;
            public bool Staffed;
            public int Busy;
            public double StaffedMinutes;
            public double BusyMinutes;
        }

        private readonly Dictionary<Station, Desk> _desks = new Dictionary<Station, Desk>();
        private readonly List<Desk> _order = new List<Desk>();
        private readonly Dictionary<int, double> _joined = new Dictionary<int, double>();
        private readonly Dictionary<int, double> _waited = new Dictionary<int, double>();
        private int _arrived;
        private int _exited;
        private int _missed;

        public RunStats(IEnumerable<Station> stations)
        {
            if (stations == null) throw new ArgumentNullException(nameof(stations));
            foreach (Station station in stations)
            {
                if (station == null) throw new ArgumentException("Stations can't be null.", nameof(stations));
                if (_desks.ContainsKey(station)) continue;
                var desk = new Desk { Station = station, Staffed = IsStaffed(station), Busy = station.Busy };
                _desks[station] = desk;
                _order.Add(desk);
            }
        }

        public void OnArrive(double time, Passenger passenger) => _arrived++;

        public void OnJoinQueue(double time, Station station, Passenger passenger)
        {
            _joined[passenger.Id] = time;
            Update(time, station);
        }

        public void OnStartService(double time, Station station, Passenger passenger)
        {
            AddWait(passenger, time);
            Update(time, station);
        }

        public void OnEndService(double time, Station station, Passenger passenger) => Update(time, station);

        public void OnBlocked(double time, Station station, Passenger passenger) => Update(time, station);

        public void OnUnblocked(double time, Station station, Passenger passenger) => Update(time, station);

        public void OnRenege(double time, Station station, Passenger passenger)
        {
            AddWait(passenger, time);
            _missed++;
            Update(time, station);
        }

        public void OnLaneChanged(double time, Station lane, LaneChange change, bool byManager) => Update(time, lane);

        public void OnExit(double time, Passenger passenger) => _exited++;

        /// <summary>The totals up to <paramref name="end"/>, which must not be before the last change seen.</summary>
        public RunSummary Summarize(double end)
        {
            var busyShare = new Dictionary<string, double>();
            double staffMinutes = 0.0;
            foreach (Desk d in _order)
            {
                if (!(end >= d.Since))
                    throw new ArgumentOutOfRangeException(nameof(end), end, $"'{d.Station.Name}' changed at {d.Since}, after {end}.");
                double staffed = d.StaffedMinutes + (d.Staffed ? (end - d.Since) * d.Station.Staff : 0.0);
                double busy = d.BusyMinutes + (end - d.Since) * d.Busy;
                staffMinutes += staffed;
                busyShare[d.Station.Name] = staffed > 0 ? busy / staffed : 0.0;
            }

            double total = 0.0, longest = 0.0;
            foreach (double w in _waited.Values)
            {
                total += w;
                if (w > longest) longest = w;
            }
            double average = _arrived > 0 ? total / _arrived : 0.0;
            return new RunSummary(_arrived, _exited, _missed, average, longest, staffMinutes, end, busyShare);
        }

        /// <summary>Someone must be at the desk: it's open, being set up, or still has people after closing (G3).</summary>
        private static bool IsStaffed(Station s) => s.IsOpen || s.IsOpening || s.QueueLength + s.Busy + s.WalkingHere > 0;

        private void AddWait(Passenger passenger, double time)
        {
            _waited.TryGetValue(passenger.Id, out double sum);
            _waited[passenger.Id] = sum + (time - _joined[passenger.Id]);
        }

        /// <summary>Adds the time since the last change at its old state, then reads the new state.</summary>
        private void Update(double time, Station station)
        {
            if (!_desks.TryGetValue(station, out Desk d))
                throw new InvalidOperationException($"RunStats wasn't given station '{station.Name}'.");
            double dt = time - d.Since;
            if (d.Staffed) d.StaffedMinutes += dt * station.Staff;
            d.BusyMinutes += dt * d.Busy;
            d.Since = time;
            d.Staffed = IsStaffed(station);
            d.Busy = station.Busy;
        }
    }
}
```

- [ ] **Step 4: Run all tests**

Run: `bash scripts/run-tests.sh`
Expected: all pass (193 + 4 = 197).

- [ ] **Step 5: Commit**

```bash
git add Unity/Assets/Core
git commit -m "RunStats: missed flights, waits, busy share and staff-minutes for a run

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Scoring, extras in the level, and the fairness test

**Files:**
- Create: `Unity/Assets/Core/Results/Scoring.cs`
- Modify: `Unity/Assets/Core/Levels/LevelDef.cs`, `Unity/Assets/Core/Layout/LayoutCompiler.cs`, `Unity/Assets/Core/Tests/Support/SampleLevels.cs`
- Test: `Unity/Assets/Core/Tests/Results/ScoringTests.cs`, `Unity/Assets/Core/Tests/Validation/FairnessTests.cs`, `Unity/Assets/Core/Tests/Layout/LayoutCompilerTests.cs`

**Interfaces:**
- Consumes: `RunStats`, `RunSummary` (Task 1).
- Produces: `ExtrasDef { double? MaxWait; double? MaxStaffMinutes }`, `LevelDef.Extras`; `Scoring.Score(LevelDef level, RunSummary summary) : RunResult`; `RunResult { bool Passed; int MissedFlights; int MaxMissedFlights; IReadOnlyList<ExtraResult> Extras }`; `ExtraResult { string Name; double Target; double Actual; bool Met }`; `SampleLevels.ShortStaffedPlan`.

- [ ] **Step 1: Write the failing tests**

Create `Unity/Assets/Core/Tests/Results/ScoringTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Throughput.Core.Levels;
using Throughput.Core.Results;

namespace Throughput.Core.Tests.Results
{
    /// <summary>One pass goal plus optional extra ticks (D9, G6).</summary>
    public class ScoringTests
    {
        private static RunSummary Summary(int missed, double longestWait, double staffMinutes) =>
            new RunSummary(100, 100 - missed, missed, 3.0, longestWait, staffMinutes, 60.0, new Dictionary<string, double>());

        [Test]
        public void Pass_WhenMissedFlightsWithinTheGoal()
        {
            var level = new LevelDef { MaxMissedFlights = 5 };
            Assert.That(Scoring.Score(level, Summary(5, 1.0, 1.0)).Passed, Is.True);
            RunResult fail = Scoring.Score(level, Summary(6, 1.0, 1.0));
            Assert.That((fail.Passed, fail.MissedFlights, fail.MaxMissedFlights), Is.EqualTo((false, 6, 5)));
            Assert.That(fail.Extras, Is.Empty);
        }

        [Test]
        public void Extras_AreTicksBesideTheGoal()
        {
            var level = new LevelDef { MaxMissedFlights = 5, Extras = new ExtrasDef { MaxWait = 10.0, MaxStaffMinutes = 300.0 } };
            RunResult r = Scoring.Score(level, Summary(9, 10.0, 301.0));

            Assert.That(r.Passed, Is.False, "extras never change the pass");
            Assert.That(r.Extras.Select(e => (e.Name, e.Target, e.Actual, e.Met)), Is.EqualTo(new[]
            {
                ("Nobody waits over 10 min", 10.0, 10.0, true),
                ("Spend at most 300 staff-minutes", 300.0, 301.0, false),
            }));
        }

        [Test]
        public void Score_RejectsNulls()
        {
            Assert.Throws<System.ArgumentNullException>(() => Scoring.Score(null, Summary(0, 0, 0)));
            Assert.Throws<System.ArgumentNullException>(() => Scoring.Score(new LevelDef(), null));
        }
    }
}
```

Create `Unity/Assets/Core/Tests/Validation/FairnessTests.cs`:

```csharp
using System.Linq;
using NUnit.Framework;
using Throughput.Core.Layout;
using Throughput.Core.Levels;
using Throughput.Core.Results;
using Throughput.Core.Tests.Support;

namespace Throughput.Core.Tests.Validation
{
    /// <summary>
    /// P3, fair not lucky (G7): on the sample level a good plan passes on almost every seed and a bad plan fails on
    /// almost every seed, so the result depends on the plan, not the dice.
    /// </summary>
    public class FairnessTests
    {
        private const int Seeds = 200;

        private static int Passes(string planJson)
        {
            int passes = 0;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                LevelDef level = LevelJson.ReadLevel(SampleLevels.Level);
                CompiledRun run = LayoutCompiler.Compile(level, LevelJson.ReadPlan(planJson), (ulong)seed);
                var stats = new RunStats(run.Stations);
                run.Runner.AddObserver(stats);
                run.Runner.Run();
                if (Scoring.Score(level, stats.Summarize(run.Runner.LastActiveTime)).Passed) passes++;
            }
            return passes;
        }

        [Test]
        public void GoodPlan_PassesAlmostAlways()
        {
            int passes = Passes(SampleLevels.Plan);
            TestContext.Out.WriteLine($"Good plan passed {passes}/{Seeds}");
            Assert.That(passes, Is.GreaterThanOrEqualTo(190));
        }

        [Test]
        public void ShortStaffedPlan_FailsAlmostAlways()
        {
            int passes = Passes(SampleLevels.ShortStaffedPlan);
            TestContext.Out.WriteLine($"Short-staffed plan passed {passes}/{Seeds}");
            Assert.That(Seeds - passes, Is.GreaterThanOrEqualTo(160));
        }
    }
}
```

In `Unity/Assets/Core/Tests/Support/SampleLevels.cs`, after `Plan`, add:

```csharp
        /// <summary>Only 2 ID desks: the second flight bank overloads it (fails on most seeds, G7).</summary>
        public const string ShortStaffedPlan = @"{
  ""stages"": [
    { ""type"": ""id"",      ""staff"": 2, ""walkToNext"": 0.5 },
    { ""type"": ""scanner"", ""staff"": 2, ""lineCapacity"": 12 }
  ]
}";
```

Add to `LayoutCompilerTests`:

```csharp
        [Test]
        public void Json_ReadsExtras()
        {
            LevelDef level = LevelJson.ReadLevel(@"{ ""name"": ""x"", ""waves"": [], ""stages"": [], ""space"": 0,
                ""maxMissedFlights"": 5, ""extras"": { ""maxWait"": 15, ""maxStaffMinutes"": 600 } }");
            Assert.That((level.Extras.MaxWait, level.Extras.MaxStaffMinutes), Is.EqualTo(((double?)15.0, (double?)600.0)));
        }

        [Test]
        public void Check_GoalAndExtrasProblems()
        {
            LevelDef level = Level;
            level.MaxMissedFlights = -1;
            level.Extras = new ExtrasDef { MaxWait = 0, MaxStaffMinutes = double.PositiveInfinity };
            string all = string.Join(" | ", LayoutCompiler.Check(level, LevelJson.ReadPlan(SampleLevels.Plan)));
            StringAssert.Contains("missed flights", all);
            StringAssert.Contains("longest wait", all);
            StringAssert.Contains("staff-minutes", all);
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `bash scripts/run-tests.sh "ScoringTests|FairnessTests|LayoutCompilerTests"`
Expected: compile errors (`Scoring`, `RunResult`, `ExtrasDef`, `ShortStaffedPlan` don't exist).

- [ ] **Step 3: Extras in the level**

In `Unity/Assets/Core/Levels/LevelDef.cs`, after `MaxMissedFlights` in `LevelDef`, add:

```csharp
        /// <summary>Optional extra challenges beside the goal (D9, G1); null = none.</summary>
        public ExtrasDef Extras { get; set; }
```

After `BreakdownDef`, add:

```csharp
    /// <summary>Optional ticks (G1): nobody waits over MaxWait minutes; at most MaxStaffMinutes staff-minutes.</summary>
    public sealed class ExtrasDef
    {
        public double? MaxWait { get; set; }

        public double? MaxStaffMinutes { get; set; }
    }
```

In `Unity/Assets/Core/Layout/LayoutCompiler.cs`, in `CheckLevel`, before `if (level.Space < 0)`, add:

```csharp
            if (level.MaxMissedFlights < 0)
                problems.Add($"The goal can't allow fewer than 0 missed flights ({level.MaxMissedFlights}).");
            if (level.Extras != null)
            {
                if (level.Extras.MaxWait.HasValue && (!(level.Extras.MaxWait.Value > 0) || double.IsInfinity(level.Extras.MaxWait.Value)))
                    problems.Add($"The longest wait extra needs a number above 0 ({level.Extras.MaxWait.Value}).");
                if (level.Extras.MaxStaffMinutes.HasValue && (!(level.Extras.MaxStaffMinutes.Value > 0) || double.IsInfinity(level.Extras.MaxStaffMinutes.Value)))
                    problems.Add($"The staff-minutes extra needs a number above 0 ({level.Extras.MaxStaffMinutes.Value}).");
            }
```

- [ ] **Step 4: Write `Scoring`**

Create `Unity/Assets/Core/Results/Scoring.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using Throughput.Core.Levels;

namespace Throughput.Core.Results
{
    /// <summary>One optional extra challenge and how the run did (D9, G1).</summary>
    public sealed class ExtraResult
    {
        public ExtraResult(string name, double target, double actual, bool met)
        {
            Name = name;
            Target = target;
            Actual = actual;
            Met = met;
        }

        public string Name { get; }

        public double Target { get; }

        public double Actual { get; }

        public bool Met { get; }
    }

    /// <summary>Pass or fail on the level's one goal, plus the extra ticks (D9, G6). No stars.</summary>
    public sealed class RunResult
    {
        public RunResult(bool passed, int missedFlights, int maxMissedFlights, IReadOnlyList<ExtraResult> extras)
        {
            Passed = passed;
            MissedFlights = missedFlights;
            MaxMissedFlights = maxMissedFlights;
            Extras = extras;
        }

        public bool Passed { get; }

        public int MissedFlights { get; }

        public int MaxMissedFlights { get; }

        public IReadOnlyList<ExtraResult> Extras { get; }
    }

    public static class Scoring
    {
        public static RunResult Score(LevelDef level, RunSummary summary)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (summary == null) throw new ArgumentNullException(nameof(summary));

            var extras = new List<ExtraResult>();
            if (level.Extras?.MaxWait is double maxWait)
                extras.Add(new ExtraResult($"Nobody waits over {Number(maxWait)} min", maxWait, summary.LongestWait,
                    summary.LongestWait <= maxWait));
            if (level.Extras?.MaxStaffMinutes is double maxCost)
                extras.Add(new ExtraResult($"Spend at most {Number(maxCost)} staff-minutes", maxCost, summary.StaffMinutes,
                    summary.StaffMinutes <= maxCost));

            bool passed = summary.MissedFlights <= level.MaxMissedFlights;
            return new RunResult(passed, summary.MissedFlights, level.MaxMissedFlights, extras);
        }

        private static string Number(double x) => x.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
```

- [ ] **Step 5: Run all tests and the strict tier**

Run: `bash scripts/run-tests.sh` then `bash scripts/run-tests.sh --strict`
Expected: all pass (197 + 3 + 2 + 2 = 204); the fairness output shows about 199/200 and 18/200 passes; strict 19/19.

- [ ] **Step 6: Commit**

```bash
git add Unity/Assets/Core
git commit -m "Score runs against the level goal and extras; fairness test over 200 seeds

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Docs and the phase-1 exit check

**Files:**
- Modify: `docs/superpowers/specs/2026-10-11-phase-1-core-design.md`, `docs/architecture/simulation-core.md`, `docs/roadmap.md`, `AGENTS.md`

- [ ] **Step 1: Update the spec**

- Tick the S9 items: `RunStats`, `Results` (pass goal + extras), fairness test. Mark "waits per group" and the optional CSV as deferred (G8).
- Progress table: `| S9 | Results + fairness | Done (2026-10-12) |`.
- Add `**S9 decisions (2026-10-12, plan part 9):**` with G1–G8, one line each.
- §7 Done when: tick each item only after checking it in this session: all slices closed; the strict tier output (19/19); the invariant and determinism tests in the normal run; the fairness test; `SetupCheckTests` passed (not inconclusive) in the normal run.
- Append to the change log: `| 2026-10-12 | S9 done: RunStats, scoring with extras, fairness test (199/200 vs 18/200); phase 1 exit checks |`.

- [ ] **Step 2: Architecture, roadmap, AGENTS**

- `docs/architecture/simulation-core.md`: under Observers, add `RunStats` (missed flights, waits, busy share, staff-minutes) and `Scoring.Score` (goal + extras).
- `docs/roadmap.md`: sub-project 1 status → `Done (2026-10-12)`; point "next" at sub-project 2.
- `AGENTS.md`: update "Current phase" to say sub-project 1 is done and the next is sub-project 2.

- [ ] **Step 3: Commit**

```bash
git add docs AGENTS.md
git commit -m "Close S9 and phase 1: results, scoring and the fairness test

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
