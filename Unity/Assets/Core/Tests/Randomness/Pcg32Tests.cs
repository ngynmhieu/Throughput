using NUnit.Framework;
using Throughput.Core.Engine.Randomness;

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
