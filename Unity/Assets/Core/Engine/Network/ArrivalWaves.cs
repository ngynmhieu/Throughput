using System;
using System.Collections.Generic;
using Throughput.Core.Engine.Randomness;

namespace Throughput.Core.Engine.Network
{
    /// <summary>One flight bank: people arrive at random at Rate per time unit between Start and End.</summary>
    public readonly struct Wave
    {
        public Wave(double start, double end, double rate)
        {
            Start = start;
            End = end;
            Rate = rate;
        }

        public double Start { get; }

        public double End { get; }

        public double Rate { get; }
    }

    /// <summary>
    /// Random (Poisson) arrivals whose rate changes from wave to wave. No arrivals between waves or after
    /// the last one. A gap drawn past a wave's end restarts at the boundary with the next wave's rate
    /// (Poisson arrivals have no memory, so this is exact).
    /// </summary>
    public sealed class ArrivalWaves : IArrivalProcess
    {
        private readonly Wave[] _waves;
        private readonly Pcg32 _rng;

        public ArrivalWaves(IReadOnlyList<Wave> waves, Pcg32 rng)
        {
            if (waves == null) throw new ArgumentNullException(nameof(waves));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _waves = new Wave[waves.Count];
            for (int i = 0; i < waves.Count; i++)
            {
                Wave w = waves[i];
                if (!(w.Start >= 0) || !(w.End > w.Start) || double.IsInfinity(w.End))
                    throw new ArgumentException($"Wave {i + 1}: must start at 0 or later and end after it starts ({w.Start}–{w.End}).", nameof(waves));
                if (!(w.Rate >= 0) || double.IsInfinity(w.Rate))
                    throw new ArgumentException($"Wave {i + 1}: the rate can't be negative ({w.Rate}).", nameof(waves));
                if (i > 0 && w.Start < waves[i - 1].End)
                    throw new ArgumentException($"Wave {i + 1} starts at {w.Start}, before wave {i} ends ({waves[i - 1].End}).", nameof(waves));
                _waves[i] = w;
            }
        }

        public bool TryNext(double now, out double time)
        {
            double t = now;
            foreach (Wave w in _waves)
            {
                if (t >= w.End || w.Rate <= 0) continue;
                if (t < w.Start) t = w.Start;
                double gap = -Math.Log(_rng.NextOpenDouble()) / w.Rate;
                if (t + gap < w.End)
                {
                    time = t + gap;
                    return true;
                }
                t = w.End;
            }
            time = 0.0;
            return false;
        }
    }
}
