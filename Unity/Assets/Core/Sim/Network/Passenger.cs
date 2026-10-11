namespace Throughput.Core.Sim.Network
{
    public sealed class Passenger
    {
        public Passenger(int id, double arrivalTime)
        {
            Id = id;
            ArrivalTime = arrivalTime;
        }

        /// <summary>0, 1, 2, ... in arrival order across the whole run.</summary>
        public int Id { get; }

        public double ArrivalTime { get; }
    }
}
