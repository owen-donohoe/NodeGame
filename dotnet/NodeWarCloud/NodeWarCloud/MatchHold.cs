using System;
using System.Linq;
using System.Threading.Tasks;
using NodeWar.Backend;

namespace NodeWar.Cloud
{
    /// <summary>
    /// Server-measured presence and disconnect holds. Presence touches only
    /// the caller's key; a winning claim commits its decision before settlement.
    /// </summary>
    public sealed class MatchHold
    {
        private const int RetryLimit = 3;
        private const int ClaimSeconds = 10;
        private const int SeenSeconds = 10;
        private const int VoidSeconds = 60;
        private readonly IMatchRecordStore matches;
        private readonly Func<string, ISettlementPlayerStore> players;
        private readonly MatchSettler settler;
        private readonly MatchDiscipline discipline;

        public MatchHold(IMatchRecordStore matches, Func<string, ISettlementPlayerStore> players,
            MatchSettler settler, MatchDiscipline discipline = null)
        {
            this.matches = matches ?? throw new ArgumentNullException(nameof(matches));
            this.players = players ?? throw new ArgumentNullException(nameof(players));
            this.settler = settler ?? throw new ArgumentNullException(nameof(settler));
            this.discipline = discipline;
        }

        public async Task<PresenceResult> Presence(string matchId, string callerId, bool holding, long now)
        {
            var record = (await matches.ReadAsync(matchId)).Record;
            if (record == null) return new PresenceResult { message = "Match not found." };
            int caller = Array.IndexOf(record.playerIds, callerId);
            if (caller < 0) return new PresenceResult { message = "Caller is not in this match." };

            var presence = await matches.ReadPresenceAsync(matchId);
            var own = presence[caller];
            own.lastSeenUnixSeconds = now;
            own.holdSinceUnixSeconds = holding ? (own.holdSinceUnixSeconds == 0 ? now : own.holdSinceUnixSeconds) : 0;
            await matches.WritePresenceAsync(matchId, caller, own);
            var opponent = presence[1 - caller];
            var result = new PresenceResult
            {
                state = record.state,
                opponentSeenSecondsAgo = opponent.lastSeenUnixSeconds == 0 ? -1 : SecondsSince(opponent.lastSeenUnixSeconds, now),
                holdSeconds = own.holdSinceUnixSeconds == 0 ? 0 : SecondsSince(own.holdSinceUnixSeconds, now)
            };
            if (Terminal(record))
            {
                await ReleaseBoth(record);
                result.result = await View(record, caller);
            }
            return result;
        }

        public async Task<ResolveHoldResult> ResolveHold(string matchId, string callerId, long now)
        {
            int conflicts = 0;
            while (true)
            {
                var read = await matches.ReadAsync(matchId);
                var record = read.Record;
                if (record == null) return new ResolveHoldResult { message = "Match not found." };
                int caller = Array.IndexOf(record.playerIds, callerId);
                if (caller < 0) return new ResolveHoldResult { message = "Caller is not in this match." };
                if (Terminal(record))
                {
                    if (discipline != null) await discipline.Apply(record, now);
                    await ReleaseBoth(record);
                    return new ResolveHoldResult { outcome = HoldOutcome.AlreadyResolved, result = await View(record, caller) };
                }
                if (record.connectedUnixSeconds <= 0)
                    return new ResolveHoldResult { message = "Match has not started." };

                if (now >= record.createdUnixSeconds + ActiveMatchClaims.LifetimeSeconds ||
                    !await ActiveMatchClaims.HoldsClaims(players, record, now))
                {
                    record.state = MatchRecordState.Void;
                }
                else if (record.forfeitedBy == 0 || record.forfeitedBy == 1 || MatchSettler.Agreed(record))
                {
                    // A committed decision outranks another hold or the pending
                    // timeout, including retries after a partial settlement.
                    int winner = record.forfeitedBy == 0 || record.forfeitedBy == 1
                        ? 1 - record.forfeitedBy : record.reports.First(r => r.accepted).winner;
                    bool settled = await settler.Settle(record, winner, now);
                    record.state = settled ? MatchRecordState.Settled : MatchRecordState.Void;
                }
                else if (record.state == MatchRecordState.Pending &&
                    now - record.pendingUnixSeconds > ActiveMatchClaims.PendingTimeoutSeconds)
                {
                    record.state = MatchRecordState.Void;
                    record.pendingTimeoutVoid = true;
                }
                else
                {
                    var presence = await matches.ReadPresenceAsync(matchId);
                    long holdSince = presence[caller].holdSinceUnixSeconds;
                    if (holdSince == 0 || now - holdSince < ClaimSeconds)
                        return new ResolveHoldResult { outcome = HoldOutcome.TooEarly };
                    long lastSeen = presence[1 - caller].lastSeenUnixSeconds;
                    if (lastSeen != 0 && now - lastSeen <= SeenSeconds)
                    {
                        if (now - holdSince < VoidSeconds)
                            return new ResolveHoldResult { outcome = HoldOutcome.OpponentPresent };
                        record.state = MatchRecordState.Void;
                    }
                    else
                    {
                        // Like Leave's forfeit, commit the loser before writing
                        // either player. A conflicting claimant must re-read it.
                        record.forfeitedBy = 1 - caller;
                        record.abandonedBy = 1 - caller;
                        record.settlementUnixSeconds = now;
                        try { await matches.WriteAsync(record, read.WriteLock); }
                        catch (RecordConflictException)
                        {
                            if (++conflicts >= RetryLimit) throw;
                        }
                        continue;
                    }
                }

                if (record.state == MatchRecordState.Void) record.outcomes = null;
                try { await matches.WriteAsync(record, read.WriteLock); }
                catch (RecordConflictException)
                {
                    if (++conflicts >= RetryLimit) throw;
                    continue;
                }
                if (discipline != null) await discipline.Apply(record, now);
                await ReleaseBoth(record);
                return new ResolveHoldResult
                {
                    outcome = record.state == MatchRecordState.Void ? HoldOutcome.Voided :
                        record.abandonedBy == 1 - caller ? HoldOutcome.Won : HoldOutcome.AlreadyResolved,
                    result = await View(record, caller)
                };
            }
        }

        public async Task<MatchResultView> GetResult(string matchId, string callerId)
        {
            var record = (await matches.ReadAsync(matchId)).Record;
            if (record == null) return new MatchResultView { message = "Match not found." };
            int caller = Array.IndexOf(record.playerIds, callerId);
            if (caller < 0) return new MatchResultView { message = "Caller is not in this match." };
            return await View(record, caller);
        }

        private async Task<MatchResultView> View(MatchRecord record, int caller)
        {
            var result = new MatchResultView { state = record.state };
            if (record.state != MatchRecordState.Settled) return result;
            result.cause = record.forfeitedBy < 0 ? MatchEndCause.Played :
                record.abandonedBy >= 0 ? MatchEndCause.Abandoned : MatchEndCause.Forfeit;
            if (record.outcomes != null)
            {
                var outcome = record.outcomes[caller];
                result.won = outcome.won;
                result.rrDelta = outcome.rrDelta;
                result.rrAfter = outcome.rrAfter;
                result.arenaAfter = outcome.arenaAfter;
                result.promoted = outcome.promoted;
                result.demoted = outcome.demoted;
            }
            result.playerState = (await players(record.playerIds[caller]).ReadForSettlementAsync()).State;
            return result;
        }

        private async Task ReleaseBoth(MatchRecord record)
        {
            foreach (string id in record.playerIds)
                await ActiveMatchClaims.Release(players(id), record.matchId);
        }

        private static bool Terminal(MatchRecord record) => record.state == MatchRecordState.Settled ||
            record.state == MatchRecordState.Void || record.state == MatchRecordState.Disputed;

        private static int SecondsSince(long since, long now) => (int)Math.Min(int.MaxValue, Math.Max(0, now - since));
    }
}
