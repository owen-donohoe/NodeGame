using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NodeWar.Backend;
using NodeWar.MatchLog;
using NodeWar.Progression;

namespace NodeWar.Cloud
{
    public sealed class MatchReportingResult
    {
        public MatchRecordState? state;
        public string message;
        public PlayerState playerState;
    }

    public sealed class MatchReporting
    {
        private const int RetryLimit = 3;
        private const long PendingTimeoutSeconds = 10 * 60;
        private readonly IMatchRecordStore matches;
        private readonly Func<string, ISettlementPlayerStore> players;
        private readonly Referee referee;
        private readonly InventoryRules inventory;

        public MatchReporting(IMatchRecordStore matches, Func<string, ISettlementPlayerStore> players,
            Referee referee, InventoryRules inventory)
        {
            this.matches = matches ?? throw new ArgumentNullException(nameof(matches));
            this.players = players ?? throw new ArgumentNullException(nameof(players));
            this.referee = referee ?? throw new ArgumentNullException(nameof(referee));
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        }

        // Caps how many refused attempts we keep per player once they have an
        // accepted report or keep retrying: audit trail only, not decision state.
        private const int MaxRefusalsPerPlayer = 3;

        public async Task<MatchReportingResult> Report(string matchId, string callerId, byte[] logBytes, long now)
        {
            if (string.IsNullOrWhiteSpace(matchId) || string.IsNullOrWhiteSpace(callerId))
                return new MatchReportingResult { message = "Match and authenticated caller are required." };
            int conflicts = 0;
            // Set once this call has added its own report to the in-flight record.
            // A write conflict discards the record and this flag together, so the
            // retry re-verifies against fresh state rather than re-reading a report
            // object that no longer exists in the freshly read record.
            bool haveOwnReport = false;
            while (true)
            {
                var read = await matches.ReadAsync(matchId);
                var record = read.Record;
                if (record == null) return new MatchReportingResult { message = "Match not found." };
                int caller = Array.IndexOf(record.playerIds, callerId);
                if (caller < 0) return new MatchReportingResult { message = "Caller is not in this match." };

                if (record.reports.Any(r => r.pendingLogBase64 != null))
                {
                    // Only the CAS-winning report owns log-N. Retaining the bytes
                    // until this copy completes also recovers a crash before SaveLog.
                    foreach (var report in record.reports)
                        if (report.pendingLogBase64 != null)
                        {
                            await matches.SaveLog(matchId, report.playerIndex, report.pendingLogBase64);
                            report.pendingLogBase64 = null;
                        }
                }
                else if (record.state == MatchRecordState.Settled || record.state == MatchRecordState.Void ||
                    record.state == MatchRecordState.Disputed)
                {
                    return new MatchReportingResult
                    {
                        state = record.state, message = record.state.ToString(),
                        playerState = record.state == MatchRecordState.Settled
                            ? (await players(callerId).ReadForSettlementAsync()).State : null
                    };
                }
                else if (Agreed(record))
                {
                    // Agreement and its server timestamp were committed before
                    // either player write. A partial settlement must finish, not expire.
                    await SettlePlayers(record);
                    record.state = MatchRecordState.Settled;
                }
                else if (record.state == MatchRecordState.Pending && now > record.pendingUnixSeconds &&
                    now - record.pendingUnixSeconds > PendingTimeoutSeconds)
                {
                    record.state = MatchRecordState.Void;
                }
                else
                {
                    if (haveOwnReport)
                    {
                        // This call already added its report on an earlier pass
                        // through the loop (log copy, agreement or timeout took
                        // priority first). Report the outcome of that report
                        // rather than verifying the same bytes again.
                        var mine = record.reports.LastOrDefault(r => r.playerIndex == caller);
                        return new MatchReportingResult { state = record.state,
                            message = mine != null && mine.accepted ? "Awaiting opponent report." : mine?.error };
                    }

                    // Only an ACCEPTED report is authoritative and blocks a retry.
                    // A refused report never consumes the player's slot: keep it for
                    // audit (capped) and let a later valid upload from the same
                    // player still settle the match.
                    var acceptedPrevious = record.reports.FirstOrDefault(r => r.playerIndex == caller && r.accepted);
                    if (acceptedPrevious != null)
                        return new MatchReportingResult { state = record.state, message = "Awaiting opponent report." };

                    var report = Verify(record, caller, logBytes);
                    record.reports.Add(report);
                    haveOwnReport = true;
                    if (!report.accepted)
                    {
                        TrimRefusals(record, caller);
                    }
                    else
                    {
                        if (record.state == MatchRecordState.Open)
                        {
                            record.state = MatchRecordState.Pending;
                            record.pendingUnixSeconds = now;
                        }
                        if (record.reports.Count(r => r.accepted) == 2)
                        {
                            if (Agreed(record)) record.settlementUnixSeconds = now;
                            else record.state = MatchRecordState.Disputed;
                        }
                    }
                }

                try { await matches.WriteAsync(record, read.WriteLock); }
                catch (RecordConflictException)
                {
                    if (++conflicts >= RetryLimit) throw;
                    // Discard this decision and all its mutable objects. Re-read
                    // membership, reports, timeout and terminal state on every retry.
                    haveOwnReport = false;
                }
            }
        }

        private MatchReport Verify(MatchRecord record, int caller, byte[] bytes)
        {
            var report = new MatchReport { playerIndex = caller };
            if (bytes != null && bytes.Length > Referee.MaxLogBytes)
            { report.error = "log exceeds 512 KB"; return report; }
            if (bytes != null) report.pendingLogBase64 = Convert.ToBase64String(bytes);
            if (!MatchLogFormat.TryRead(bytes, out var log, out string error))
            { report.error = error; return report; }
            error = MatchEligibility.Check(record, log);
            if (error != null) { report.error = error; return report; }

            var verdict = referee.Verify(bytes);
            report.winner = verdict.winner;
            report.endTick = verdict.endTick;
            report.finalHash = verdict.finalHash;
            report.accepted = verdict.ok && verdict.gameOver && (verdict.winner == 0 || verdict.winner == 1);
            report.error = report.accepted ? null : verdict.error ?? "Replay did not end in a rated win.";
            return report;
        }

        private static bool Agreed(MatchRecord record)
        {
            // Refusals may sit alongside accepted reports for audit; only the
            // accepted ones are ever compared for agreement or settlement.
            var accepted = record.reports.Where(r => r.accepted).ToList();
            if (accepted.Count != 2) return false;
            var first = accepted[0];
            var second = accepted[1];
            return first.playerIndex != second.playerIndex && first.winner == second.winner &&
                first.endTick == second.endTick && first.finalHash == second.finalHash;
        }

        // Keeps only the last MaxRefusalsPerPlayer refused reports for the given
        // player. Accepted reports are never trimmed by this.
        private static void TrimRefusals(MatchRecord record, int caller)
        {
            var refusals = record.reports.Where(r => r.playerIndex == caller && !r.accepted).ToList();
            while (refusals.Count > MaxRefusalsPerPlayer)
            {
                record.reports.Remove(refusals[0]);
                refusals.RemoveAt(0);
            }
        }

        // Arena thresholds come from the one table the client also displays.
        private static readonly SettlementConfig Config = new SettlementConfig
        {
            Rank = new RankConfig { ArenaThresholds = RankTable.Thresholds.ToArray() }
        };

        private async Task SettlePlayers(MatchRecord record)
        {
            var inputs = record.players.Select(p => new SettlementPlayer
            {
                Rating = new Rating(p.Rating.R, p.Rating.Rd, p.Rating.Sigma),
                LastMatchUnixSeconds = p.Rating.LastMatchUnixSeconds,
                Rank = new RankState(p.Rank.RR, p.Rank.Arena, p.Rank.HighestArena)
            }).ToArray();
            var outcome = MatchSettlement.Settle(new SettlementInput
            {
                Players = inputs, Winner = record.reports.First(r => r.accepted).winner,
                NowUnixSeconds = record.settlementUnixSeconds
            }, Config);

            for (int p = 0; p < 2; p++)
            {
                var store = players(record.playerIds[p]);
                for (int attempt = 0; attempt < RetryLimit; attempt++)
                {
                    var read = await store.ReadForSettlementAsync();
                    var state = read.State;
                    if (state?.History?.MatchIds == null || state.Inventory == null || state.Rating == null)
                        throw new InvalidOperationException("Match players must have initialized server records.");
                    // The idempotency guard is this bounded, 200-entry list on the
                    // rating record, not the 20-entry history: a player who plays
                    // enough matches to evict this one from history must still not
                    // be settled twice for it.
                    var settledIds = state.Rating.SettledMatchIds ?? new List<string>();
                    if (settledIds.Contains(record.matchId)) break;
                    var settled = outcome.Players[p];
                    settledIds.Insert(0, record.matchId);
                    if (settledIds.Count > 200) settledIds.RemoveRange(200, settledIds.Count - 200);
                    state.Rating = new RatingRecord { R = settled.Rating.R, Rd = settled.Rating.RD,
                        Sigma = settled.Rating.Sigma, LastMatchUnixSeconds = settled.LastMatchUnixSeconds,
                        SettledMatchIds = settledIds };
                    state.Rank = new RankRecord { RR = settled.Rank.RR, Arena = settled.Rank.Arena,
                        HighestArena = settled.Rank.HighestArena };
                    inventory.GrantDefaults(state);
                    InventoryClamp.ClampToArena(state.Inventory, state.Rank.Arena);
                    state.History.MatchIds.Insert(0, record.matchId);
                    if (state.History.MatchIds.Count > 20)
                        state.History.MatchIds.RemoveRange(20, state.History.MatchIds.Count - 20);
                    try
                    {
                        await store.WriteForSettlementAsync(state, read.WriteLocks);
                        break;
                    }
                    catch (RecordConflictException) when (attempt + 1 < RetryLimit) { }
                }
            }
        }
    }
}
