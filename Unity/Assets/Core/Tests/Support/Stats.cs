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
