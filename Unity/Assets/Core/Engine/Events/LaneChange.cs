namespace Throughput.Core.Engine.Events
{
    /// <summary>What happened to a lane (K9).</summary>
    public enum LaneChange
    {
        Opened,
        Closed,

        /// <summary>The rule started opening it; it opens after the setup time.</summary>
        Opening,
        Broken,
        Repaired,

        /// <summary>The manager handed a pinned lane back to the rule.</summary>
        ReturnedToRule,
    }
}
