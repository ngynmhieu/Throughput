using Throughput.Core.Engine.Network;

namespace Throughput.Core.Engine.Events
{
    public sealed class EngineEvent
    {
        public EngineEvent(double time, EventType type, object node, Passenger passenger)
        {
            Time = time;
            Type = type;
            Node = node;
            Passenger = passenger;
        }

        public double Time { get; }

        public EventType Type { get; }

        /// <summary>The Source or Station this event belongs to.</summary>
        public object Node { get; }

        public Passenger Passenger { get; }

        /// <summary>Insertion number, set by EventList. Breaks the last ties.</summary>
        public long Seq { get; internal set; }
    }
}
