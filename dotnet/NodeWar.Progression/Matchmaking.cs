using System;
using System.Collections.Generic;

namespace NodeWar.Progression
{
    public sealed class MatchmakingConfig
    {
        public (double WaitSeconds, double Window)[] WindowKnots { get; set; } =
        {
            (0, 100), (20, 250), (60, 500), (120, double.PositiveInfinity)
        };
        public int MaxArenaGap { get; set; } = 1;
        public double BotOfferSeconds { get; set; } = 90;

        public void Validate()
        {
            if (WindowKnots == null || WindowKnots.Length == 0 || WindowKnots[0].WaitSeconds != 0)
                throw new ArgumentException("Window knots must start at zero seconds.", nameof(WindowKnots));
            for (int i = 0; i < WindowKnots.Length; i++)
            {
                var knot = WindowKnots[i];
                if (!Glicko2.IsFinite(knot.WaitSeconds) || knot.WaitSeconds < 0 ||
                    double.IsNaN(knot.Window) || knot.Window < 0 ||
                    (i > 0 && (knot.WaitSeconds <= WindowKnots[i - 1].WaitSeconds ||
                        knot.Window < WindowKnots[i - 1].Window)))
                    throw new ArgumentException("Knot times must increase and non-negative windows must not shrink.", nameof(WindowKnots));
            }
            // +/-1 is the hard cap (BACKEND-PLAN section 7): eras differ in power across arenas and MMR cannot
            // see it. A stricter 0 is allowed; anything wider is not a tuning choice.
            if (MaxArenaGap < 0 || MaxArenaGap > 1) throw new ArgumentOutOfRangeException(nameof(MaxArenaGap));
            ValidateWait(BotOfferSeconds, nameof(BotOfferSeconds));
        }

        internal static void ValidateWait(double wait, string name)
        {
            if (!Glicko2.IsFinite(wait) || wait < 0) throw new ArgumentOutOfRangeException(name);
        }
    }

    public readonly struct Ticket
    {
        public string TicketId { get; }
        public double Rating { get; }
        public int Arena { get; }
        public double WaitSeconds { get; }
        public int ProtocolVersion { get; }
        public int SimVersion { get; }
        public string ContentHash { get; }

        public Ticket(string ticketId, double rating, int arena, double waitSeconds,
            int protocolVersion, int simVersion, string contentHash)
        {
            TicketId = ticketId;
            Rating = rating;
            Arena = arena;
            WaitSeconds = waitSeconds;
            ProtocolVersion = protocolVersion;
            SimVersion = simVersion;
            ContentHash = contentHash;
            Validate();
        }

        internal void Validate()
        {
            if (string.IsNullOrWhiteSpace(TicketId)) throw new ArgumentException("A ticket ID is required.", nameof(TicketId));
            if (!Glicko2.IsFinite(Rating)) throw new ArgumentOutOfRangeException(nameof(Rating));
            if (Arena < 0) throw new ArgumentOutOfRangeException(nameof(Arena));
            MatchmakingConfig.ValidateWait(WaitSeconds, nameof(WaitSeconds));
            if (string.IsNullOrWhiteSpace(ContentHash)) throw new ArgumentException("A content hash is required.", nameof(ContentHash));
        }
    }

    public static class Matchmaking
    {
        public static double Window(double waitSeconds, MatchmakingConfig cfg)
        {
            ValidateConfig(cfg);
            MatchmakingConfig.ValidateWait(waitSeconds, nameof(waitSeconds));
            return WindowUnchecked(waitSeconds, cfg);
        }

        public static bool CanMatch(Ticket a, Ticket b, MatchmakingConfig cfg)
        {
            ValidateConfig(cfg);
            a.Validate();
            b.Validate();
            return CanMatchUnchecked(a, b, cfg);
        }

        /// <summary>Pool IDs must be unique so the final ordinal tiebreaker is a total order.</summary>
        public static Ticket? BestOpponent(Ticket seeker, IReadOnlyList<Ticket> pool, MatchmakingConfig cfg)
        {
            ValidateConfig(cfg);
            seeker.Validate();
            if (pool == null) throw new ArgumentNullException(nameof(pool));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            Ticket? best = null;
            foreach (var candidate in pool)
            {
                candidate.Validate();
                if (!ids.Add(candidate.TicketId))
                    throw new ArgumentException("Pool ticket IDs must be unique.", nameof(pool));
                if (string.Equals(seeker.TicketId, candidate.TicketId, StringComparison.Ordinal) ||
                    !CanMatchUnchecked(seeker, candidate, cfg)) continue;
                if (!best.HasValue || Compare(seeker, candidate, best.Value) < 0) best = candidate;
            }
            return best;
        }

        public static bool ShouldOfferBot(double waitSeconds, MatchmakingConfig cfg)
        {
            ValidateConfig(cfg);
            MatchmakingConfig.ValidateWait(waitSeconds, nameof(waitSeconds));
            return waitSeconds >= cfg.BotOfferSeconds;
        }

        private static void ValidateConfig(MatchmakingConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            cfg.Validate();
        }

        private static double WindowUnchecked(double waitSeconds, MatchmakingConfig cfg)
        {
            for (int i = cfg.WindowKnots.Length - 1; i > 0; i--)
                if (waitSeconds >= cfg.WindowKnots[i].WaitSeconds) return cfg.WindowKnots[i].Window;
            return cfg.WindowKnots[0].Window;
        }

        private static bool CanMatchUnchecked(Ticket a, Ticket b, MatchmakingConfig cfg)
        {
            return a.ProtocolVersion == b.ProtocolVersion && a.SimVersion == b.SimVersion &&
                string.Equals(a.ContentHash, b.ContentHash, StringComparison.Ordinal) &&
                Math.Abs((long)a.Arena - b.Arena) <= cfg.MaxArenaGap &&
                Math.Abs(a.Rating - b.Rating) <= Math.Min(
                    WindowUnchecked(a.WaitSeconds, cfg), WindowUnchecked(b.WaitSeconds, cfg));
        }

        private static int Compare(Ticket seeker, Ticket a, Ticket b)
        {
            int order = (a.Arena != seeker.Arena).CompareTo(b.Arena != seeker.Arena);
            if (order != 0) return order;
            order = Math.Abs(a.Rating - seeker.Rating).CompareTo(Math.Abs(b.Rating - seeker.Rating));
            if (order != 0) return order;
            order = b.WaitSeconds.CompareTo(a.WaitSeconds);
            return order != 0 ? order : StringComparer.Ordinal.Compare(a.TicketId, b.TicketId);
        }
    }
}
