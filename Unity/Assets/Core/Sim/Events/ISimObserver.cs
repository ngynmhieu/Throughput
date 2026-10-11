using Throughput.Core.Sim.Network;

namespace Throughput.Core.Sim.Events
{
    /// <summary>Watches the simulation. Observers can't change it (spec, Observers).</summary>
    public interface ISimObserver
    {
        void OnArrive(double time, Passenger passenger);

        void OnJoinQueue(double time, Station station, Passenger passenger);

        void OnStartService(double time, Station station, Passenger passenger);

        void OnEndService(double time, Station station, Passenger passenger);

        void OnExit(double time, Passenger passenger);
    }
}
