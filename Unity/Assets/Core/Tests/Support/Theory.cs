using System;
using System.Collections.Generic;

namespace Throughput.Core.Tests.Support
{
    public readonly struct ErlangAResult
    {
        public ErlangAResult(double lq, double probAbandon, double wq)
        {
            Lq = lq;
            ProbAbandon = probAbandon;
            Wq = wq;
        }

        /// <summary>Average number waiting in line.</summary>
        public double Lq { get; }

        /// <summary>Share of all arrivals who give up.</summary>
        public double ProbAbandon { get; }

        /// <summary>Average wait in line, over all arrivals (Little's Law: Lq / λ).</summary>
        public double Wq { get; }
    }

    /// <summary>
    /// The queueing formulas the engine is checked against. Each one is explained in
    /// docs/game-mechanism/knowledge/formulas/. Symbols: λ arrival rate, μ service rate per desk,
    /// c desks, Cs / Ca coefficients of variation, θ abandonment rate (1 / mean patience).
    /// </summary>
    public static class Theory
    {
        public static double MM1Wq(double lambda, double mu)
        {
            double rho = Utilization(lambda, mu, 1);
            return rho / (mu - lambda);
        }

        public static double ErlangCProbWait(double lambda, double mu, int c)
        {
            double rho = Utilization(lambda, mu, c);
            double a = lambda / mu;

            // term = a^k / k!, built step by step to avoid huge factorials.
            double term = 1.0;
            double sum = 0.0;
            for (int k = 0; k < c; k++)
            {
                sum += term;
                term *= a / (k + 1);
            }
            double top = term / (1.0 - rho); // term is now a^c / c!
            return top / (sum + top);
        }

        public static double ErlangCWq(double lambda, double mu, int c)
        {
            return ErlangCProbWait(lambda, mu, c) / (c * mu - lambda);
        }

        public static double PollaczekKhinchineWq(double lambda, double meanService, double cs)
        {
            double rho = Utilization(lambda, 1.0 / meanService, 1);
            return rho / (1.0 - rho) * (1.0 + cs * cs) / 2.0 * meanService;
        }

        public static double KingmanWq(double lambda, double meanService, double ca, double cs)
        {
            double rho = Utilization(lambda, 1.0 / meanService, 1);
            return (ca * ca + cs * cs) / 2.0 * rho / (1.0 - rho) * meanService;
        }

        /// <summary>Exact Erlang A (M/M/c+M) by walking the state ladder (erlang-a.md).</summary>
        public static ErlangAResult ErlangA(double lambda, double mu, int c, double theta)
        {
            if (!(lambda > 0)) throw new ArgumentOutOfRangeException(nameof(lambda), lambda, "λ must be above 0.");
            if (!(mu > 0)) throw new ArgumentOutOfRangeException(nameof(mu), mu, "μ must be above 0.");
            if (c < 1) throw new ArgumentOutOfRangeException(nameof(c), c, "Need at least one desk.");
            if (!(theta > 0)) throw new ArgumentOutOfRangeException(nameof(theta), theta, "θ must be above 0 (use Erlang C for infinite patience).");

            var weights = new List<double> { 1.0 };
            double total = 1.0;
            for (int n = 1; n < 10_000_000; n++)
            {
                double down = Math.Min(n, c) * mu + Math.Max(n - c, 0) * theta;
                double next = weights[n - 1] * lambda / down;
                if (double.IsInfinity(next))
                    throw new ArgumentOutOfRangeException(nameof(theta), theta, "Patience too long for this overload; the ladder overflows.");
                weights.Add(next);
                total += next;
                if (n > c && next < 1e-16 * total)
                    break;
            }

            double lq = 0.0;
            for (int n = c + 1; n < weights.Count; n++)
                lq += weights[n] / total * (n - c);

            return new ErlangAResult(lq, theta * lq / lambda, lq / lambda);
        }

        private static double Utilization(double lambda, double mu, int c)
        {
            if (!(lambda > 0)) throw new ArgumentOutOfRangeException(nameof(lambda), lambda, "λ must be above 0.");
            if (!(mu > 0)) throw new ArgumentOutOfRangeException(nameof(mu), mu, "μ must be above 0.");
            if (c < 1) throw new ArgumentOutOfRangeException(nameof(c), c, "Need at least one desk.");
            double rho = lambda / (c * mu);
            if (rho >= 1.0)
                throw new ArgumentOutOfRangeException(nameof(lambda), lambda, $"ρ = {rho:F3}: the line grows forever, so this formula has no answer.");
            return rho;
        }
    }
}
