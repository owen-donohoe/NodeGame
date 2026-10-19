using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NodeWar.Backend
{
    public sealed class PlayerStats
    {
        public int Matches;
        public int Wins;
        public int Losses;
        public int Streak;
        public int Best;
    }

    public sealed class MatchSummary
    {
        public string MatchId;
        public PlayerCard Opponent;
        public bool Won;
        public int RRDelta;
        public int Arena;
        public long CreatedUnix;
    }

    public sealed class PublicProfileView
    {
        public PlayerCard Card;
        public PlayerStats Stats; // Null means unknown, rather than zero matches.
        public string[] SuitIds = Array.Empty<string>();
        public string[] DistrictIds = Array.Empty<string>();
        public List<MatchSummary> Recent = new List<MatchSummary>();
    }

    public interface IProfileService
    {
        Task<PublicProfileView> GetAsync(string playerId);
    }

    public sealed class LocalProfileService : IProfileService
    {
        private readonly Func<string> selfId;
        private readonly Func<PlayerState> state;
        private readonly IMatchHistoryService history;
        private readonly Func<string[]> suits;
        private readonly Func<string[]> districts;

        public LocalProfileService(Func<string> selfId, Func<PlayerState> state, IMatchHistoryService history,
            Func<string[]> suits = null, Func<string[]> districts = null)
        {
            this.selfId = selfId;
            this.state = state;
            this.history = history;
            this.suits = suits;
            this.districts = districts;
        }

        public async Task<PublicProfileView> GetAsync(string playerId)
        {
            string account = selfId();
            if (playerId == account || playerId == "self" && string.IsNullOrEmpty(account))
            {
                PlayerState snapshot = state();
                var profile = new PublicProfileView
                {
                    Card = new PlayerCard { PlayerId = account ?? "self", Name = snapshot?.Profile?.Name ?? "Player",
                        Tag = snapshot?.Profile?.Tag ?? "", Arena = snapshot?.Rank?.Arena ?? 0,
                        RR = snapshot?.Rank?.RR ?? 0, Presence = PlayerPresence.Online },
                    Stats = null, SuitIds = Copy(suits?.Invoke()), DistrictIds = Copy(districts?.Invoke())
                };
                var entries = await history.GetAsync();
                if (!string.Equals(account, selfId(), StringComparison.Ordinal))
                    throw new InvalidOperationException("The signed-in player changed.");
                if (entries != null)
                    foreach (var entry in entries)
                    {
                        if (entry == null || entry.state != MatchRecordState.Settled || !entry.won.HasValue) continue;
                        profile.Recent.Add(new MatchSummary { MatchId = entry.matchId, Opponent = CardFor(entry.opponentPlayerId),
                            Won = entry.won.Value, RRDelta = entry.rrDelta ?? 0, Arena = entry.arenaAfter ?? 0,
                            CreatedUnix = entry.createdUnixSeconds });
                    }
                return profile;
            }
            PlayerCard card = CardFor(playerId);
            bool sample = LocalSocialService.Sample(playerId) != null;
            return new PublicProfileView
            {
                Card = card,
                Stats = sample ? new PlayerStats { Matches = 10, Wins = 6, Losses = 4, Streak = 2, Best = 4 } : null,
                SuitIds = sample ? new[] { "suit_guardian", "suit_warrior" } : Array.Empty<string>(),
                DistrictIds = sample ? new[] { "district_farm", "district_barracks" } : Array.Empty<string>(),
                Recent = sample ? new List<MatchSummary> { new MatchSummary { MatchId = "sample-history-1",
                    Opponent = LocalSocialService.Sample("sample-cedar"), Won = true, RRDelta = 12,
                    Arena = card.Arena, CreatedUnix = 1792281600 } } : new List<MatchSummary>()
            };
        }

        public static PlayerCard CardFor(string playerId) => LocalSocialService.Sample(playerId) ??
            new PlayerCard { PlayerId = playerId, Name = playerId ?? "Unknown player", Tag = "", Presence = PlayerPresence.Offline };

        private static string[] Copy(string[] values) => values == null ? Array.Empty<string>() : (string[])values.Clone();
    }
}
