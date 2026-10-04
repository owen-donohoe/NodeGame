using System;

namespace NodeWar.Progression
{
    public sealed class SettlementPlayer
    {
        public Rating Rating { get; set; }
        public long LastMatchUnixSeconds { get; set; }
        public RankState Rank { get; set; }
    }

    public sealed class SettlementInput
    {
        public SettlementPlayer[] Players { get; set; }
        public int Winner { get; set; }
        public long NowUnixSeconds { get; set; }
    }

    public sealed class SettlementConfig
    {
        public Glicko2Config Glicko { get; set; } = new Glicko2Config();
        public RankConfig Rank { get; set; } = new RankConfig();
        public long InactivityPeriodSeconds { get; set; } = 7 * 24 * 3600;
    }

    public readonly struct PlayerSettlement
    {
        public Rating Rating { get; }
        public long LastMatchUnixSeconds { get; }
        public int RRDelta { get; }
        public RankState Rank { get; }
        public bool Promoted { get; }
        public bool Demoted { get; }

        public PlayerSettlement(Rating rating, long lastMatchUnixSeconds, int rrDelta,
            RankState rank, bool promoted, bool demoted)
        {
            Rating = rating;
            LastMatchUnixSeconds = lastMatchUnixSeconds;
            RRDelta = rrDelta;
            Rank = rank;
            Promoted = promoted;
            Demoted = demoted;
        }
    }

    public sealed class SettlementOutcome
    {
        public PlayerSettlement[] Players { get; }

        public SettlementOutcome(PlayerSettlement[] players)
        {
            Players = players;
        }
    }

    public static class MatchSettlement
    {
        /// <summary>Settles trusted server snapshots using the supplied server time, without I/O.</summary>
        public static SettlementOutcome Settle(SettlementInput input, SettlementConfig cfg)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (input.Players == null) throw new ArgumentNullException(nameof(input.Players));
            if (input.Players.Length != 2)
                throw new ArgumentException("Settlement requires exactly two players.", nameof(input.Players));
            if (input.Winner != 0 && input.Winner != 1)
                throw new ArgumentException("Winner must be 0 or 1.", nameof(input.Winner));
            if (cfg.Glicko == null) throw new ArgumentNullException(nameof(cfg.Glicko));
            if (cfg.Rank == null) throw new ArgumentNullException(nameof(cfg.Rank));
            if (cfg.InactivityPeriodSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(cfg.InactivityPeriodSeconds));

            var arenas = new ArenaConfig { Thresholds = cfg.Rank.ArenaThresholds };
            var before = new Rating[2];
            for (int i = 0; i < 2; i++)
            {
                var player = input.Players[i];
                if (player == null)
                    throw new ArgumentException("Players must not contain null entries.", nameof(input.Players));
                before[i] = player.Rating;
                if (player.LastMatchUnixSeconds > 0 && input.NowUnixSeconds > player.LastMatchUnixSeconds)
                {
                    long idle = (input.NowUnixSeconds - player.LastMatchUnixSeconds) / cfg.InactivityPeriodSeconds;
                    if (idle > int.MaxValue)
                        throw new ArgumentOutOfRangeException(nameof(input.NowUnixSeconds),
                            "Idle periods exceed the range supported by Glicko2.");
                    before[i] = Glicko2.DecayForInactivity(before[i], (int)idle, cfg.Glicko);
                }
            }

            var results = new PlayerSettlement[2];
            for (int i = 0; i < 2; i++)
            {
                var player = input.Players[i];
                bool won = input.Winner == i;
                var rating = Glicko2.UpdateSingleMatch(before[i], before[1 - i], won ? 1 : 0, cfg.Glicko);
                int delta = RankPoints.RRDelta(player.Rank.RR, rating, won, cfg.Rank);
                var rank = Arenas.ApplyRRDelta(player.Rank, delta, arenas);
                results[i] = new PlayerSettlement(rating, input.NowUnixSeconds, delta, rank,
                    rank.Arena > player.Rank.Arena, rank.Arena < player.Rank.Arena);
            }
            return new SettlementOutcome(results);
        }
    }
}
