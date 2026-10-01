using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NodeWar.Backend
{
    /// <summary>
    /// What the Rendezvous Cloud Code function returns. Shared so the client
    /// reads the shape the module writes. A null <see cref="state"/> means the
    /// call was refused before it reached a match record; <see cref="message"/>
    /// says why.
    /// </summary>
    public sealed class RendezvousResult
    {
        public MatchRecordState? state;
        public string message;

        /// <summary>The record's player IDs. Slot 0 hosts as simulation player 0.</summary>
        public string[] playerIds;

        /// <summary>The caller's index in <see cref="playerIds"/>, or -1 when refused.</summary>
        public int slot = -1;

        /// <summary>The host's Relay join code once published, otherwise null.</summary>
        public string joinCode;

        /// <summary>True once either peer has confirmed the connection.</summary>
        public bool connected;
    }

    /// <summary>
    /// The result of leaving a match. Stored nowhere and returned to the client
    /// by value, so never reorder or remove a member.
    /// </summary>
    public enum LeaveOutcome
    {
        /// <summary>The caller holds no active match and may queue.</summary>
        Cleared,
        /// <summary>The match was played; leaving it now is a forfeit, and the caller must confirm.</summary>
        NeedsForfeit,
        /// <summary>The caller has reported and waits for the opponent's report or the timeout.</summary>
        Waiting
    }

    public sealed class LeaveMatchResult
    {
        /// <summary>Null when the call was refused (no identity, bad arguments).</summary>
        public LeaveOutcome? outcome;
        public string message;

        /// <summary>For <see cref="LeaveOutcome.Waiting"/>: seconds until the pending timeout.</summary>
        public int secondsLeft;

        /// <summary>The caller's state after a forfeit settled, otherwise null.</summary>
        public PlayerState playerState;
    }

    /// <summary>
    /// How a settled match was decided. Returned by value, so never reorder
    /// or remove a member.
    /// </summary>
    public enum MatchEndCause
    {
        Unknown,
        /// <summary>Both logs arrived and agreed.</summary>
        Played,
        /// <summary>A player gave the match up (surrender, or forfeiting to queue again).</summary>
        Forfeit,
        /// <summary>A disconnect hold resolved against a player the server could not see.</summary>
        Abandoned,
        /// <summary>Both players left before the end; decided on Core health, no strikes.</summary>
        BothLeft
    }

    /// <summary>
    /// A ranked match's result as the caller sees it (GetMatchResult). A null
    /// <see cref="state"/> means the call was refused; <see cref="message"/>
    /// says why. The outcome fields are the caller's and are set only when
    /// the match is Settled.
    /// </summary>
    public sealed class MatchResultView
    {
        public MatchRecordState? state;
        public string message;
        public MatchEndCause cause;
        public bool? won;
        public int? rrDelta;
        public int? rrAfter;
        public int? arenaAfter;
        public bool promoted;
        public bool demoted;

        /// <summary>The caller's server state once Settled, otherwise null.</summary>
        public PlayerState playerState;
    }

    /// <summary>
    /// What Presence returns during a disconnect hold. The server only reports
    /// what it saw; the hold's decisions are ResolveHold's.
    /// </summary>
    public sealed class PresenceResult
    {
        /// <summary>Null when the call was refused.</summary>
        public MatchRecordState? state;
        public string message;

        /// <summary>Seconds since the opponent last called Presence; -1 if never.</summary>
        public int opponentSeenSecondsAgo = -1;

        /// <summary>How long the server has seen the caller holding; 0 when not holding.</summary>
        public int holdSeconds;

        /// <summary>The caller's result once the match is Settled, Void or Disputed.</summary>
        public MatchResultView result;
    }

    /// <summary>What ResolveHold decided. Returned by value, so never reorder or remove a member.</summary>
    public enum HoldOutcome
    {
        /// <summary>The opponent was not seen for 10 s: the caller wins, the opponent abandoned.</summary>
        Won,
        /// <summary>The opponent is still calling Presence; keep holding.</summary>
        OpponentPresent,
        /// <summary>The caller's hold is under 10 s on the server's clock.</summary>
        TooEarly,
        /// <summary>Both were seen for the whole 60 s: the peer link broke. Void, no strike.</summary>
        Voided,
        /// <summary>The match had already ended; see the result.</summary>
        AlreadyResolved
    }

    public sealed class ResolveHoldResult
    {
        /// <summary>Null when the call was refused.</summary>
        public HoldOutcome? outcome;
        public string message;
        public MatchResultView result;
    }

    /// <summary>
    /// The server calls that take a found ranked match into play. Calls may
    /// fail (offline, refused); the caller decides what a failure means.
    /// </summary>
    public interface IRankedMatchService
    {
        /// <summary>The caller's view of the match's result. Read-only.</summary>
        Task<MatchResultView> GetResultAsync(string matchId);

        /// <summary>
        /// Tells the server the caller is alive, and whether it is holding for a
        /// silent opponent. Called about once a second during a hold, and once
        /// with holding false when the connection comes back.
        /// </summary>
        Task<PresenceResult> PresenceAsync(string matchId, bool holding);

        /// <summary>Asks the server to end a hold: a win if the opponent is gone, a void after 60 s if both are present.</summary>
        Task<ResolveHoldResult> ResolveHoldAsync(string matchId);

        /// <summary>
        /// Reads the match's roster and join code. Slot 0 passes its Relay join
        /// code to publish it; everyone else passes null.
        /// </summary>
        Task<RendezvousResult> RendezvousAsync(string matchId, string joinCode);

        /// <summary>Marks the match as started. Both peers call it once connected.</summary>
        Task ConfirmConnectedAsync(string matchId);

        /// <summary>
        /// Leaves the caller's active match. Before the connection this voids
        /// it; after, it answers NeedsForfeit unless <paramref name="forfeit"/>
        /// is true, which settles it as a loss for the caller.
        /// </summary>
        Task<LeaveMatchResult> LeaveAsync(string matchId, bool forfeit);
    }

    /// <summary>Where one ranked connection attempt stands.</summary>
    public enum RankedConnectionPhase
    {
        Idle,
        /// <summary>Relay allocation or join in flight.</summary>
        CreatingRoom,
        /// <summary>The host has a code and is waiting for the guest.</summary>
        WaitingForOpponent,
        /// <summary>The guest is handshaking.</summary>
        Connecting,
        /// <summary>Handshake done; the scene is loading.</summary>
        Connected,
        Failed
    }

    /// <summary>
    /// The peer connection a ranked rendezvous drives, with no Unity types.
    /// The Unity side implements it over MatchLauncher, which owns the
    /// transport, the handshake and its deadlines; the rendezvous owns only
    /// the server exchange around it.
    /// </summary>
    public interface IRankedConnection
    {
        /// <summary>Hosts a Relay room as simulation player 0 for the given match.</summary>
        void Host(string matchId, string[] playerIds);

        /// <summary>Joins the host's room as simulation player 1 for the given match.</summary>
        void Join(string matchId, string[] playerIds, string joinCode);

        RankedConnectionPhase Phase { get; }

        /// <summary>The host's Relay join code once the room exists, otherwise null.</summary>
        string JoinCode { get; }

        /// <summary>Why the attempt failed. Empty unless Phase is Failed.</summary>
        string FailureMessage { get; }

        /// <summary>Tears the attempt down. Safe to call in any phase.</summary>
        void Cancel();
    }

    /// <summary>
    /// Offline stand-in for <see cref="IRankedMatchService"/>: scripted results
    /// and a record of every call, for tests and the Editor's local fakes. No
    /// match rules live here; those are the server's (MatchRendezvous).
    /// </summary>
    public sealed class LocalRankedMatchService : IRankedMatchService
    {
        public sealed class Call
        {
            public string Method;
            public string MatchId;
            public string JoinCode;
            public bool Forfeit;
            public bool Holding;
        }

        private readonly List<Call> calls = new List<Call>();
        public IReadOnlyList<Call> Calls => calls;

        /// <summary>
        /// Returned by RendezvousAsync in order; the last repeats. Empty returns
        /// a refused result.
        /// </summary>
        public List<RendezvousResult> RendezvousResults { get; } = new List<RendezvousResult>();
        private int nextRendezvous;

        /// <summary>Returned by LeaveAsync for every call. Defaults to Cleared.</summary>
        public LeaveMatchResult LeaveResult { get; set; } = new LeaveMatchResult { outcome = LeaveOutcome.Cleared };

        /// <summary>Returned by GetResultAsync for every call. Defaults to a Pending match.</summary>
        public MatchResultView Result { get; set; } = new MatchResultView { state = MatchRecordState.Pending };

        /// <summary>Returned by PresenceAsync for every call. Defaults to an opponent seen just now.</summary>
        public PresenceResult Presence { get; set; } = new PresenceResult
        {
            state = MatchRecordState.Open, opponentSeenSecondsAgo = 0
        };

        /// <summary>Returned by ResolveHoldAsync for every call. Defaults to OpponentPresent.</summary>
        public ResolveHoldResult Resolution { get; set; } = new ResolveHoldResult { outcome = HoldOutcome.OpponentPresent };

        public Task<MatchResultView> GetResultAsync(string matchId)
        {
            RequireMatchId(matchId);
            calls.Add(new Call { Method = nameof(GetResultAsync), MatchId = matchId });
            return Task.FromResult(Result);
        }

        public Task<PresenceResult> PresenceAsync(string matchId, bool holding)
        {
            RequireMatchId(matchId);
            calls.Add(new Call { Method = nameof(PresenceAsync), MatchId = matchId, Holding = holding });
            return Task.FromResult(Presence);
        }

        public Task<ResolveHoldResult> ResolveHoldAsync(string matchId)
        {
            RequireMatchId(matchId);
            calls.Add(new Call { Method = nameof(ResolveHoldAsync), MatchId = matchId });
            return Task.FromResult(Resolution);
        }

        public Task<RendezvousResult> RendezvousAsync(string matchId, string joinCode)
        {
            RequireMatchId(matchId);
            calls.Add(new Call { Method = nameof(RendezvousAsync), MatchId = matchId, JoinCode = joinCode });
            if (RendezvousResults.Count == 0)
                return Task.FromResult(new RendezvousResult { message = "local fake" });
            RendezvousResult result = RendezvousResults[nextRendezvous];
            if (nextRendezvous < RendezvousResults.Count - 1) nextRendezvous++;
            return Task.FromResult(result);
        }

        public Task ConfirmConnectedAsync(string matchId)
        {
            RequireMatchId(matchId);
            calls.Add(new Call { Method = nameof(ConfirmConnectedAsync), MatchId = matchId });
            return Task.CompletedTask;
        }

        public Task<LeaveMatchResult> LeaveAsync(string matchId, bool forfeit)
        {
            RequireMatchId(matchId);
            calls.Add(new Call { Method = nameof(LeaveAsync), MatchId = matchId, Forfeit = forfeit });
            return Task.FromResult(LeaveResult);
        }

        private static void RequireMatchId(string matchId)
        {
            if (string.IsNullOrWhiteSpace(matchId))
                throw new ArgumentException("matchId must not be blank.", nameof(matchId));
        }
    }
}
