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
