namespace Throughput.Core.Engine.Events
{
    /// <summary>
    /// The kinds of scheduled event. The number is the tie-break priority when two events share
    /// a time: lower goes first (spec S-D4). EndService before Arrive, so a desk that frees up
    /// at time t can serve someone arriving at t.
    /// </summary>
    public enum EventType
    {
        EndService = 0,
        Arrive = 1,
    }
}
