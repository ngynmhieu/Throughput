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
