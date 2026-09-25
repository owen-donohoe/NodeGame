using System;

namespace NodeWar.Progression
{
    /// <summary>These numbers are placeholders pending design.</summary>
    public sealed class RankConfig
    {
        public int[] ArenaThresholds { get; set; } = { 0, 300, 700, 1200, 1800, 2500 };
        public int BaseGain { get; set; } = 20;
        public int MinGain { get; set; } = 8;
        public int MaxGain { get; set; } = 40;
        public double Divergence { get; set; } = 0.04;
        public double ImpliedRatingAtZero { get; set; } = 1000;
        public double RatingPerRR { get; set; } = 0.5;
    }

    public static class RankPoints
    {
        /// <summary>
        /// Returns the signed match award. Apply it with a zero floor to the
        /// stored RR total; the award itself remains negative for every loss.
        /// </summary>
        public static int RRDelta(int currentRR, Rating hiddenAfterMatch, bool won, RankConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (cfg.MinGain <= 0) throw new ArgumentOutOfRangeException(nameof(cfg.MinGain));
            if (cfg.MaxGain < cfg.MinGain) throw new ArgumentOutOfRangeException(nameof(cfg.MaxGain));
            if (cfg.BaseGain < 0) throw new ArgumentOutOfRangeException(nameof(cfg.BaseGain));
            if (!Glicko2.IsFinite(cfg.Divergence) || cfg.Divergence < 0)
                throw new ArgumentOutOfRangeException(nameof(cfg.Divergence));
            if (!Glicko2.IsFinite(cfg.ImpliedRatingAtZero))
                throw new ArgumentOutOfRangeException(nameof(cfg.ImpliedRatingAtZero));
            if (!Glicko2.IsFinite(cfg.RatingPerRR) || cfg.RatingPerRR <= 0)
                throw new ArgumentOutOfRangeException(nameof(cfg.RatingPerRR));

            double implied = cfg.ImpliedRatingAtZero + cfg.RatingPerRR * Math.Max(0, currentRR);
            double correction = cfg.Divergence * (hiddenAfterMatch.R - implied);
            double magnitude = cfg.BaseGain + (won ? correction : -correction);
            magnitude = Math.Max(cfg.MinGain, Math.Min(cfg.MaxGain, magnitude));
            int rounded = (int)Math.Round(magnitude, MidpointRounding.AwayFromZero);
            return won ? rounded : -rounded;
        }

        /// <summary>Returns a zero-based arena index, treating negative RR as zero.</summary>
        public static int ArenaFor(int rr, RankConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            var thresholds = cfg.ArenaThresholds;
            if (thresholds == null || thresholds.Length == 0 || thresholds[0] != 0)
                throw new ArgumentException("Arena thresholds must start at zero.", nameof(cfg));
            for (int i = 1; i < thresholds.Length; i++)
            {
                if (thresholds[i] <= thresholds[i - 1])
                    throw new ArgumentException("Arena thresholds must be strictly increasing.", nameof(cfg));
            }

            rr = Math.Max(0, rr);
            for (int i = thresholds.Length - 1; i > 0; i--)
            {
                if (rr >= thresholds[i]) return i;
            }
            return 0;
        }
    }
}
