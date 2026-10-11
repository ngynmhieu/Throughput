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
