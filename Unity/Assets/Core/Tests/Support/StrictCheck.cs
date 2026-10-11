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
