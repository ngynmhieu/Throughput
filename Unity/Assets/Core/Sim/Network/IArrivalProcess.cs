using System;
using Throughput.Core.Sim.Randomness;

namespace Throughput.Core.Sim.Network
{
    /// <summary>Decides when the next passenger arrives at a Source.</summary>
    public interface IArrivalProcess
    {
        /// <summary>Returns false when no more passengers will come.</summary>
        bool TryNext(double now, out double time);
    }

    /// <summary>Gaps between arrivals drawn from a distribution (Exponential gaps = Poisson arrivals).</summary>
    public sealed class RenewalArrivals : IArrivalProcess
    {
        private readonly IDistribution _gap;
        private readonly Pcg32 _rng;

        public RenewalArrivals(IDistribution gap, Pcg32 rng)
        {
            _gap = gap ?? throw new ArgumentNullException(nameof(gap));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public bool TryNext(double now, out double time)
        {
            time = now + _gap.Sample(_rng);
            return true;
        }
    }

    /// <summary>A fixed list of arrival times, for hand-calculated tests and scripted groups.</summary>
    public sealed class FixedArrivals : IArrivalProcess
    {
        private readonly double[] _times;
        private int _next;

        public FixedArrivals(params double[] times)
        {
            if (times == null) throw new ArgumentNullException(nameof(times));
            for (int i = 0; i < times.Length; i++)
            {
                if (!(times[i] >= 0))
                    throw new ArgumentOutOfRangeException(nameof(times), times[i], "Arrival times can't be negative or NaN.");
                if (i > 0 && times[i] < times[i - 1])
                    throw new ArgumentException($"Arrival times must be in order: {times[i]} comes after {times[i - 1]}.", nameof(times));
            }
            _times = (double[])times.Clone();
        }

        public bool TryNext(double now, out double time)
        {
            if (_next >= _times.Length)
            {
                time = 0.0;
                return false;
            }
            time = _times[_next++];
            return true;
        }
    }
}
