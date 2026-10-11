using System;

namespace Throughput.Core.Engine.Network
{
    /// <summary>Where passengers appear. Sends each new passenger to its target station.</summary>
    public sealed class Source
    {
        public Source(string name, IArrivalProcess arrivals, Station target, double stopTime = double.PositiveInfinity)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A source needs a name.", nameof(name));
            Name = name;
            Arrivals = arrivals ?? throw new ArgumentNullException(nameof(arrivals));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            StopTime = stopTime;
        }

        public string Name { get; }

        public IArrivalProcess Arrivals { get; }

        public Station Target { get; }

        /// <summary>No arrivals at or after this time (the end of the wave).</summary>
        public double StopTime { get; }
    }
}
