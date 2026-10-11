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
