using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NodeWar.Backend;
using NodeWar.Progression;

namespace NodeWar.Cloud
{
    public interface IDisciplinePlayerStore
    {
        Task<LockedPlayerState> ReadDisciplineAsync();
        Task WriteDisciplineAsync(DisciplineRecord discipline, LockedPlayerState read);
    }

    /// <summary>Best-effort discipline after a durable terminal match decision.</summary>
    public sealed class MatchDiscipline
    {
        private const int RetryLimit = 3;
        private readonly IMatchRecordStore matches;
        private readonly Func<string, IDisciplinePlayerStore> players;
        private readonly ILogger<MatchDiscipline> logger;

        public MatchDiscipline(IMatchRecordStore matches, Func<string, IDisciplinePlayerStore> players,
            ILogger<MatchDiscipline> logger = null)
        {
            this.matches = matches ?? throw new ArgumentNullException(nameof(matches));
            this.players = players ?? throw new ArgumentNullException(nameof(players));
            this.logger = logger ?? NullLogger<MatchDiscipline>.Instance;
        }

        // False leaves claims in place; a later terminal call retries. Always
        // re-read the durable guard, even if the caller holds an older snapshot.
        public async Task<bool> Apply(MatchRecord record, long now)
        {
            try
            {
                for (int attempt = 0; ; attempt++)
                {
                    var read = await matches.ReadAsync(record.matchId);
                    var current = read.Record;
                    if (current == null || (current.state != MatchRecordState.Settled &&
                        current.state != MatchRecordState.Void && current.state != MatchRecordState.Disputed)) return false;
                    if (current.disciplineApplied) return true;

                    await ApplyPlayer(current, now);
                    current.disciplineApplied = true;
                    try { await matches.WriteAsync(current, read.WriteLock); return true; }
                    catch (RecordConflictException) when (attempt + 1 < RetryLimit) { }
                }
            }
            catch (Exception ex)
            {
                // Settlement stays terminal, but cleanup waits for a successful retry.
                try { logger.LogWarning(ex, "Unable to apply match discipline for {MatchId}.", record?.matchId); }
                catch (Exception) { }
                return false;
            }
        }

        private async Task ApplyPlayer(MatchRecord record, long now)
        {
            int slot;
            bool nonReport = false;
            if (record.state == MatchRecordState.Settled && record.abandonedBy >= 0)
                slot = record.abandonedBy;
            else if (record.state == MatchRecordState.Void && record.pendingTimeoutVoid &&
                record.reports.Count(r => r.accepted) == 1)
            {
                slot = 1 - record.reports.Single(r => r.accepted).playerIndex;
                nonReport = true;
            }
            else return;

            var store = players(record.playerIds[slot]);
            for (int attempt = 0; ; attempt++)
            {
                var read = await store.ReadDisciplineAsync();
                var discipline = read.State.Discipline ?? new DisciplineRecord();
                discipline.StruckMatchIds ??= new List<string>();
                if (discipline.StruckMatchIds.Contains(record.matchId)) return;
                var state = new DisconnectPenaltyState
                {
                    Level = discipline.Level, LastStrikeUnixSeconds = discipline.LastStrikeUnixSeconds,
                    LastDecayUnixSeconds = discipline.LastDecayUnixSeconds,
                    BlockedUntilUnixSeconds = discipline.BlockedUntilUnixSeconds, NonReports = discipline.NonReports
                };
                state = nonReport ? DisconnectPenalty.NonReport(state, now) : DisconnectPenalty.Strike(state, now);
                discipline.Level = state.Level;
                discipline.LastStrikeUnixSeconds = state.LastStrikeUnixSeconds;
                discipline.LastDecayUnixSeconds = state.LastDecayUnixSeconds;
                discipline.BlockedUntilUnixSeconds = state.BlockedUntilUnixSeconds;
                discipline.NonReports = state.NonReports ?? new List<long>();
                // Covers a crash between the player write and the durable match flag.
                discipline.StruckMatchIds.Insert(0, record.matchId);
                if (discipline.StruckMatchIds.Count > 20)
                    discipline.StruckMatchIds.RemoveRange(20, discipline.StruckMatchIds.Count - 20);
                try { await store.WriteDisciplineAsync(discipline, read); return; }
                catch (RecordConflictException) when (attempt + 1 < RetryLimit) { }
            }
        }
    }
}
