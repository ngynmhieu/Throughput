namespace Throughput.Core.Sim.Randomness
{
    /// <summary>A source of non-negative random durations (service times, gaps between arrivals).</summary>
    public interface IDistribution
    {
        double Mean { get; }

        /// <summary>Coefficient of variation: standard deviation divided by the mean. 0 = always the same.</summary>
        double Cv { get; }

        double Sample(Pcg32 rng);
    }
}
