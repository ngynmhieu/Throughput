namespace Throughput.Core.Tests.Support
{
    /// <summary>A small realistic level and plan (minutes). Two flight banks; the second overloads the ID check.</summary>
    public static class SampleLevels
    {
        public const string Level = @"{
  ""name"": ""Morning bank"",
  ""waves"": [
    { ""start"": 0,  ""end"": 30, ""perMinute"": 1.5 },
    { ""start"": 30, ""end"": 60, ""perMinute"": 3.0 }
  ],
  ""stages"": [
    { ""name"": ""id"",      ""service"": { ""mean"": 1.2, ""cv"": 0.5, ""min"": 0.3, ""max"": 5 }, ""maxStaff"": 4 },
    { ""name"": ""scanner"", ""service"": { ""mean"": 0.6, ""cv"": 0.4, ""min"": 0.2, ""max"": 3 }, ""maxStaff"": 3 }
  ],
  ""patience"": { ""mean"": 25, ""cv"": 0.6, ""min"": 5 },
  ""space"": 20,
  ""maxMissedFlights"": 5
}";

        public const string Plan = @"{
  ""stages"": [
    { ""type"": ""id"",      ""staff"": 3, ""walkToNext"": 0.5 },
    { ""type"": ""scanner"", ""staff"": 2, ""lineCapacity"": 12 }
  ]
}";
    }
}
