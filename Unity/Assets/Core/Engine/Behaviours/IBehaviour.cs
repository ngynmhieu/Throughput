using Throughput.Core.Engine.Events;
using Throughput.Core.Engine.Network;

namespace Throughput.Core.Engine.Behaviours
{
    /// <summary>
    /// Something passengers do on their own (architecture: Behaviours, D6). It reacts to engine
    /// moments and may schedule events. Adding a behaviour never changes the core loop.
    /// </summary>
    public interface IBehaviour
    {
        void OnJoinQueue(Runner runner, Station station, Passenger passenger);
    }
}
