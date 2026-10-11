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

        void OnExit(double time, Passenger passenger);
    }
}
