using System;
using System.Linq;
using System.Threading.Tasks;
using NodeWar.Backend;

namespace NodeWar.Cloud
{
    /// <summary>
    /// Server side of ranked step 8.2b: publishing the host's Relay join code,
    /// marking a match connected, and leaving one -- before a connection this
    /// voids the match, after it this is a forfeit. No match rules live in the
    /// network layer or the client; this is the one place that decides them.
    /// </summary>
    public sealed class MatchRendezvous
    {
        private const int RetryLimit = 3;
        private const int MaxJoinCodeLength = 32;
        private readonly IMatchRecordStore matches;
        private readonly Func<string, ISettlementPlayerStore> players;
        private readonly MatchSettler settler;

        public MatchRendezvous(IMatchRecordStore matches, Func<string, ISettlementPlayerStore> players,
            MatchSettler settler)
        {
            this.matches = matches ?? throw new ArgumentNullException(nameof(matches));
            this.players = players ?? throw new ArgumentNullException(nameof(players));
            this.settler = settler ?? throw new ArgumentNullException(nameof(settler));
        }

        /// <summary>
        /// Reads the caller's roster and slot. Slot 0 may publish its Relay join
        /// code while the match is Open and nobody has connected yet; a code
        /// from slot 1, or a slot-0 code once connected, is silently ignored
        /// rather than refused.
        /// </summary>
        public async Task<RendezvousResult> Rendezvous(string matchId, string callerId, string joinCode)
        {
            int conflicts = 0;
            while (true)
            {
                var read = await matches.ReadAsync(matchId);
                var record = read.Record;
                if (record == null) return new RendezvousResult { message = "Match not found." };
                int slot = Array.IndexOf(record.playerIds, callerId);
                if (slot < 0) return new RendezvousResult { message = "Caller is not in this match." };

                bool wantsPublish = !string.IsNullOrWhiteSpace(joinCode) && slot == 0 &&
                    record.state == MatchRecordState.Open && record.connectedUnixSeconds == 0;
                if (wantsPublish)
                {
                    if (joinCode.Length > MaxJoinCodeLength)
                        return new RendezvousResult { message = "Join code is too long." };
                    record.joinCode = joinCode;
                    try { await matches.WriteAsync(record, read.WriteLock); }
                    catch (RecordConflictException)
                    {
                        if (++conflicts >= RetryLimit) throw;
                        continue;
                    }
                }

                return new RendezvousResult
                {
                    state = record.state, playerIds = record.playerIds, slot = slot,
                    joinCode = record.joinCode, connected = record.connectedUnixSeconds > 0
                };
            }
        }

        /// <summary>
        /// Marks the match connected once, the first time either member calls
        /// it. Idempotent: a member calling again, or calling once the match has
        /// left Open/Pending, changes nothing.
        /// </summary>
        public async Task ConfirmConnected(string matchId, string callerId, long now)
        {
            int conflicts = 0;
            while (true)
            {
                var read = await matches.ReadAsync(matchId);
                var record = read.Record;
                if (record == null) return;
                if (Array.IndexOf(record.playerIds, callerId) < 0) return;
                if ((record.state != MatchRecordState.Open && record.state != MatchRecordState.Pending) ||
                    record.connectedUnixSeconds != 0)
                    return;

                record.connectedUnixSeconds = now;
                try { await matches.WriteAsync(record, read.WriteLock); return; }
                catch (RecordConflictException)
                {
                    if (++conflicts >= RetryLimit) throw;
                }
            }
        }

        /// <summary>
        /// Leaves the caller's match. A match that never connected, or whose
        /// claims lapsed, is voided rather than forfeited. Once connected,
        /// leaving needs an explicit forfeit unless the caller already has an
        /// accepted report, in which case they are already waiting it out.
        /// </summary>
        public async Task<LeaveMatchResult> Leave(string matchId, string callerId, bool forfeit, long now)
        {
            int conflicts = 0;
            while (true)
            {
                var read = await matches.ReadAsync(matchId);
                var record = read.Record;

                if (record == null)
                {
                    // Only releases if the claim still names this match.
                    await ActiveMatchClaims.Release(players(callerId), matchId);
                    return Cleared();
                }

                int caller = Array.IndexOf(record.playerIds, callerId);
                if (caller < 0) return new LeaveMatchResult { message = "Caller is not in this match." };

                if (record.state == MatchRecordState.Settled || record.state == MatchRecordState.Void ||
                    record.state == MatchRecordState.Disputed)
                {
                    await ReleaseBoth(record);
                    return Cleared();
                }

                LeaveMatchResult result;
                bool settledOk = true;

                if (now >= record.createdUnixSeconds + ActiveMatchClaims.LifetimeSeconds ||
                    !await ActiveMatchClaims.HoldsClaims(players, record, now))
                {
                    record.state = MatchRecordState.Void;
                    result = Cleared();
                }
                else if (record.state == MatchRecordState.Pending &&
                    now - record.pendingUnixSeconds > ActiveMatchClaims.PendingTimeoutSeconds)
                {
                    record.state = MatchRecordState.Void;
                    result = Cleared();
                }
                else if (record.connectedUnixSeconds == 0)
                {
                    // Never started: leaving voids it rather than forfeiting it.
                    record.state = MatchRecordState.Void;
                    result = Cleared();
                }
                else
                {
                    var acceptedPrevious = record.reports.FirstOrDefault(r => r.playerIndex == caller && r.accepted);
                    if (acceptedPrevious != null)
                    {
                        // Open should not be reachable once connected and reported
                        // (Report moves Open -> Pending on the first accepted
                        // report), but the full timeout is the correct fallback if
                        // it ever is.
                        long elapsed = record.state == MatchRecordState.Open ? 0 : now - record.pendingUnixSeconds;
                        int secondsLeft = (int)Math.Max(0, ActiveMatchClaims.PendingTimeoutSeconds - elapsed);
                        return new LeaveMatchResult { outcome = LeaveOutcome.Waiting, secondsLeft = secondsLeft };
                    }

                    if (!forfeit) return new LeaveMatchResult { outcome = LeaveOutcome.NeedsForfeit };

                    int other = 1 - caller;
                    record.settlementUnixSeconds = now;
                    settledOk = await settler.Settle(record, other, now);
                    record.state = settledOk ? MatchRecordState.Settled : MatchRecordState.Void;
                    if (!settledOk) record.outcomes = null;
                    result = Cleared();
                }

                try { await matches.WriteAsync(record, read.WriteLock); }
                catch (RecordConflictException)
                {
                    if (++conflicts >= RetryLimit) throw;
                    continue;
                }

                await ReleaseBoth(record);
                if (settledOk && record.state == MatchRecordState.Settled)
                    result.playerState = (await players(callerId).ReadForSettlementAsync()).State;
                return result;
            }
        }

        private async Task ReleaseBoth(MatchRecord record)
        {
            foreach (string id in record.playerIds)
                await ActiveMatchClaims.Release(players(id), record.matchId);
        }

        private static LeaveMatchResult Cleared() => new LeaveMatchResult { outcome = LeaveOutcome.Cleared };
    }
}
