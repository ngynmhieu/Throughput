namespace Throughput.Core.Engine.Events
{
    /// <summary>
    /// The kinds of scheduled event. The number is the tie-break priority when two events share
    /// a time: lower goes first (spec S-D4). EndService first, so a desk that frees up at time t
    /// can serve someone whose patience ends at t, who walks in at t, or who arrives at t.
    /// </summary>
    public enum EventType
    {
        EndService = 0,
        Renege = 1,
        Reach = 2,
        Arrive = 3,
    }
}
