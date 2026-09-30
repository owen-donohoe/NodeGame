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
        private readonly Func<string, IDisciplinePlayerStore> players;
        private readonly ILogger<MatchDiscipline> logger;

        public MatchDiscipline(Func<string, IDisciplinePlayerStore> players, ILogger<MatchDiscipline> logger = null)
        {
            this.players = players ?? throw new ArgumentNullException(nameof(players));
            this.logger = logger ?? NullLogger<MatchDiscipline>.Instance;
        }

        public async Task Apply(MatchRecord record, long now)
        {
            try
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
                    // Even the first non-report consumes the match's idempotency key.
                    discipline.StruckMatchIds.Insert(0, record.matchId);
                    if (discipline.StruckMatchIds.Count > 20)
                        discipline.StruckMatchIds.RemoveRange(20, discipline.StruckMatchIds.Count - 20);
                    try { await store.WriteDisciplineAsync(discipline, read); return; }
                    catch (RecordConflictException) when (attempt + 1 < RetryLimit) { }
                }
            }
            catch (Exception ex)
            {
                // A failed strike must never reverse settlement or prevent cleanup.
                try { logger.LogWarning(ex, "Unable to apply match discipline for {MatchId}.", record?.matchId); }
                catch (Exception) { }
            }
        }
    }
}
