using Throughput.Core.Engine.Network;

namespace Throughput.Core.Engine.Events
{
    /// <summary>Watches a run. Observers can't change it (spec, Observers).</summary>
    public interface IRunObserver
    {
        void OnArrive(double time, Passenger passenger);

        void OnJoinQueue(double time, Station station, Passenger passenger);

        void OnStartService(double time, Station station, Passenger passenger);

        void OnEndService(double time, Station station, Passenger passenger);

        /// <summary>Service ended but the next line is full: the desk holds the passenger and stops (D8).</summary>
        void OnBlocked(double time, Station station, Passenger passenger);

        /// <summary>Space opened downstream: the blocked desk lets the passenger move on.</summary>
        void OnUnblocked(double time, Station station, Passenger passenger);

        /// <summary>
        /// The passenger's patience ran out and they left the line and the checkpoint (a missed flight).
        /// No OnExit follows: viewers must treat this as leaving.
        /// </summary>
        void OnRenege(double time, Station station, Passenger passenger);

        /// <summary>
        /// A lane changed (K9). byManager: the player did it (a replay re-applies these); otherwise the staffing
        /// rule or the event deck did.
        /// </summary>
        void OnLaneChanged(double time, Station lane, LaneChange change, bool byManager);

        void OnExit(double time, Passenger passenger);
    }
}
