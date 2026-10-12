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
