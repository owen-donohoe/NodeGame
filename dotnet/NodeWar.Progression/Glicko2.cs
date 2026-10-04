using System;

namespace NodeWar.Progression
{
    public readonly struct Rating
    {
        // C# 9 zero-initializes structs, including array entries. An unset value
        // must still represent a new player, rather than a zero-volatility rating.
        private readonly bool initialized;
        private readonly double r;
        private readonly double rd;
        private readonly double sigma;

        public double R => initialized ? r : 1500;
        public double RD => initialized ? rd : 350;
        public double Sigma => initialized ? sigma : 0.06;

        public Rating(double r = 1500, double rd = 350, double sigma = 0.06)
        {
            if (!Glicko2.IsFinite(r)) throw new ArgumentOutOfRangeException(nameof(r));
            if (!Glicko2.IsFinite(rd) || rd <= 0) throw new ArgumentOutOfRangeException(nameof(rd));
            if (!Glicko2.IsFinite(sigma) || sigma <= 0) throw new ArgumentOutOfRangeException(nameof(sigma));
            this.r = r;
            this.rd = rd;
            this.sigma = sigma;
            initialized = true;
        }
    }

    public sealed class Glicko2Config
    {
        public double Tau { get; set; } = 0.5;
        public double Epsilon { get; set; } = 0.000001;
        public double MaxRD { get; set; } = 350;
        public double MinRD { get; set; } = 30;

        internal void Validate()
        {
            if (!Glicko2.IsFinite(Tau) || Tau <= 0) throw new ArgumentOutOfRangeException(nameof(Tau));
            if (!Glicko2.IsFinite(Epsilon) || Epsilon <= 0) throw new ArgumentOutOfRangeException(nameof(Epsilon));
            if (!Glicko2.IsFinite(MinRD) || MinRD <= 0) throw new ArgumentOutOfRangeException(nameof(MinRD));
            if (!Glicko2.IsFinite(MaxRD) || MaxRD < MinRD) throw new ArgumentOutOfRangeException(nameof(MaxRD));
        }
    }

    public static class Glicko2
    {
        private const double Scale = 173.7178;
        private const int IterationLimit = 1000;

        /// <summary>
        /// All opponents must be snapshots from the start of the rating period,
        /// so updating one player's rating cannot affect another player's result.
        /// </summary>
        public static Rating Update(Rating player,
            ReadOnlySpan<(Rating opponent, double score)> results, Glicko2Config cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            cfg.Validate();
            if (results.Length == 0) return DecayForInactivity(player, 1, cfg);

            double phi = player.RD / Scale;
            double information = 0;
            double improvement = 0;
            foreach (var result in results)
            {
                if (result.score != 0 && result.score != 0.5 && result.score != 1)
                    throw new ArgumentOutOfRangeException(nameof(results), "Scores must be 0, 0.5 or 1.");
                double g = G(result.opponent.RD / Scale);
                double expected = ExpectedScore(player, result.opponent);
                information += g * g * expected * (1 - expected);
                improvement += g * (result.score - expected);
            }

            // Reject numerical saturation instead of persisting an unusable rating.
            if (!IsFinite(information) || information <= 0)
                throw new ArithmeticException("Rating variance is outside the supported numerical range.");
            double variance = 1 / information;
            double delta = variance * improvement;
            double sigma = UpdatedVolatility(phi, player.Sigma, variance, delta, cfg);
            double prePeriodVariance = phi * phi + sigma * sigma;
            double updatedVariance = 1 / (1 / prePeriodVariance + information);
            return new Rating(player.R + Scale * updatedVariance * improvement,
                ClampRD(Scale * Math.Sqrt(updatedVariance), cfg), sigma);
        }

        public static Rating UpdateSingleMatch(Rating player, Rating opponent, double score, Glicko2Config cfg)
        {
            return Update(player, new[] { (opponent, score) }, cfg);
        }

        public static Rating DecayForInactivity(Rating r, int idlePeriods, Glicko2Config cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            cfg.Validate();
            if (idlePeriods < 0) throw new ArgumentOutOfRangeException(nameof(idlePeriods));
            if (idlePeriods == 0) return r;

            // Each idle period adds the same variance; summing it avoids a loop
            // proportional to an externally supplied absence duration.
            double phi = r.RD / Scale;
            double rd = Scale * Math.Sqrt(phi * phi + idlePeriods * r.Sigma * r.Sigma);
            return new Rating(r.R, ClampRD(rd, cfg), r.Sigma);
        }

        /// <summary>Uses the opponent's uncertainty, as in the paper's E function.</summary>
        public static double ExpectedScore(Rating a, Rating b)
        {
            return 1 / (1 + Math.Exp(-G(b.RD / Scale) * ((a.R - b.R) / Scale)));
        }

        private static double G(double phi) => 1 / Math.Sqrt(1 + 3 * phi * phi / (Math.PI * Math.PI));
        private static double ClampRD(double rd, Glicko2Config cfg) => Math.Max(cfg.MinRD, Math.Min(cfg.MaxRD, rd));
        internal static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static double UpdatedVolatility(double phi, double sigma, double variance,
            double delta, Glicko2Config cfg)
        {
            // Glickman, Example of the Glicko-2 system, steps 5.1-5.5:
            // https://glicko.net/glicko/glicko2.pdf
            // Illinois bracketing avoids the initial-guess sensitivity of Newton iteration.
            double a = Math.Log(sigma * sigma);
            double phiSquared = phi * phi;
            double deltaSquared = delta * delta;
            double F(double x)
            {
                double ex = Math.Exp(x);
                double denominator = phiSquared + variance + ex;
                return ex * (deltaSquared - denominator) / (2 * denominator * denominator)
                    - (x - a) / (cfg.Tau * cfg.Tau);
            }

            double lower = a;
            double upper;
            if (deltaSquared > phiSquared + variance)
                upper = Math.Log(deltaSquared - phiSquared - variance);
            else
            {
                int k = 1;
                while (F(a - k * cfg.Tau) < 0)
                {
                    if (++k > IterationLimit) throw new ArithmeticException("Volatility bracketing did not converge.");
                }
                upper = a - k * cfg.Tau;
            }

            double fLower = F(lower);
            double fUpper = F(upper);
            int iterations = 0;
            while (Math.Abs(upper - lower) > cfg.Epsilon)
            {
                if (++iterations > IterationLimit) throw new ArithmeticException("Volatility iteration did not converge.");
                double next = lower + (lower - upper) * fLower / (fUpper - fLower);
                double fNext = F(next);
                if (!IsFinite(next) || !IsFinite(fNext))
                    throw new ArithmeticException("Volatility is outside the supported numerical range.");
                if (fNext * fUpper <= 0)
                {
                    lower = upper;
                    fLower = fUpper;
                }
                else
                    fLower /= 2;
                upper = next;
                fUpper = fNext;
            }
            return Math.Exp(lower / 2);
        }
    }
}
