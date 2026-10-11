using System;
using Newtonsoft.Json;

namespace Throughput.Core.Levels
{
    /// <summary>The level or plan file can't be read (broken JSON, a typo in a field name, a wrong type).</summary>
    public sealed class LevelFormatException : Exception
    {
        public LevelFormatException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>Reads level and plan JSON (L3). Field names are matched ignoring case; unknown fields are errors.</summary>
    public static class LevelJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
        };

        public static LevelDef ReadLevel(string json) => Read<LevelDef>(json, "level");

        public static PlanDef ReadPlan(string json) => Read<PlanDef>(json, "plan");

        private static T Read<T>(string json, string what) where T : class
        {
            T result;
            try
            {
                result = JsonConvert.DeserializeObject<T>(json ?? "", Settings);
            }
            catch (JsonException e)
            {
                throw new LevelFormatException($"Can't read the {what} file: {e.Message}", e);
            }
            return result ?? throw new LevelFormatException($"The {what} file is empty.", null);
        }
    }
}
