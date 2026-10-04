using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NodeWar.Backend;

namespace NodeWar.Cloud
{
    /// <summary>
    /// Read-only history for the caller's own matches. Never accepts another
    /// player's ID: the ID this reads by is always the one the history record
    /// itself belongs to, and every match returned must list that ID as a
    /// participant or it is skipped rather than trusted.
    /// </summary>
    public static class MatchHistory
    {
        public static async Task<List<MatchHistoryEntry>> ForPlayer(string playerId,
            IMatchRecordStore matches, IPlayerRecordStore player)
        {
            if (string.IsNullOrWhiteSpace(playerId)) throw new ArgumentException("Player ID is required.", nameof(playerId));
            if (matches == null) throw new ArgumentNullException(nameof(matches));
            if (player == null) throw new ArgumentNullException(nameof(player));

            var state = await player.ReadAsync();
            var matchIds = state?.History?.MatchIds;
            var entries = new List<MatchHistoryEntry>();
            if (matchIds == null) return entries;

            // History is already newest-first and capped at 20; preserve that order.
            foreach (string matchId in matchIds)
            {
                var read = await matches.ReadAsync(matchId);
                var record = read.Record;
                if (record == null) continue;
                int self = Array.IndexOf(record.playerIds, playerId);
                if (self < 0) continue;
                int opponent = 1 - self;

                var entry = new MatchHistoryEntry
                {
                    matchId = record.matchId,
                    opponentPlayerId = record.playerIds[opponent],
                    state = record.state,
                    createdUnixSeconds = record.createdUnixSeconds
                };
                if (record.state == MatchRecordState.Settled && record.outcomes != null)
                {
                    var outcome = record.outcomes[self];
                    entry.won = outcome.won;
                    entry.rrDelta = outcome.rrDelta;
                    entry.arenaAfter = outcome.arenaAfter;
                }
                entries.Add(entry);
            }
            return entries;
        }
    }
}
