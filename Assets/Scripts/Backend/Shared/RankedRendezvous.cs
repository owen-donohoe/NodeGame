using System;
using System.Threading.Tasks;

namespace NodeWar.Backend
{
    /// <summary>
    /// Drives one ranked match from "ticket found" to "connected": the Relay
    /// join-code exchange through <see cref="IRankedMatchService"/>, and the
    /// peer handshake through <see cref="IRankedConnection"/>, with the
    /// deadlines neither of those two pieces enforces on its own.
    ///
    /// Ticked the same way <see cref="RankedQueuePresenter"/> is: the caller
    /// supplies elapsed seconds into <see cref="Tick"/>, this class holds no
    /// wall-clock of its own. Raises exactly one of <see cref="Connected"/> or
    /// <see cref="Failed"/>, once, and then stops driving (Tick after that is
    /// a no-op).
    ///
    /// Slot 0 (the host) calls <see cref="IRankedConnection.Host"/> immediately,
    /// waits for a join code to come out of the connection, then publishes it
    /// to the server once. Slot 1 (the guest) polls the server for that code,
    /// then joins the connection with it. Either side fails the attempt if the
    /// server ever reports the match record has gone terminal
    /// (Settled/Void/Disputed) or missing, or if the connection itself reports
    /// Failed.
    /// </summary>
    public sealed class RankedRendezvous
    {
        private const double PollIntervalSeconds = 2.0;

        /// <summary>Host: wall-clock budget from a successful code publish to Phase == Connected.</summary>
        private const double HostConnectDeadlineSeconds = 22.0;

        /// <summary>Guest: wall-clock budget from Start to discovering a join code.</summary>
        private const double CodeWaitDeadlineSeconds = 15.0;

        /// <summary>Guest: wall-clock budget from Join to Phase == Connected (handshake + relay setup).</summary>
        private const double PostJoinDeadlineSeconds = 15.0 + 12.0;

        private readonly IRankedMatchService service;
        private readonly IRankedConnection connection;
        private readonly string matchId;
        private readonly RendezvousResult roster;
        private readonly bool isHost;

        private bool active;
        private bool finished;
        private bool callInFlight;

        private double startedAtSeconds;

        // Host state.
        private string observedCode;
        private bool publishConfirmed;
        private double publishSucceededAtSeconds = -1;
        private double lastPublishAttemptAtSeconds = -1;

        // Guest state.
        private string discoveredCode;
        private bool joinCalled;
        private double joinedAtSeconds = -1;
        private double lastPollAtSeconds;

        /// <summary>Raised exactly once, when the connection is up. ConfirmConnectedAsync has already been fired.</summary>
        public event Action Connected;

        /// <summary>Raised exactly once, with a short reason, when the attempt cannot continue.</summary>
        public event Action<string> Failed;

        public RankedRendezvous(IRankedMatchService service, IRankedConnection connection, string matchId, RendezvousResult roster)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
            if (string.IsNullOrWhiteSpace(matchId))
                throw new ArgumentException("matchId must not be blank.", nameof(matchId));
            this.matchId = matchId;
            this.roster = roster ?? throw new ArgumentNullException(nameof(roster));
            isHost = roster.slot == 0;
        }

        /// <summary>True while an attempt is in progress and Tick should be called.</summary>
        public bool IsActive => active;

        public void Start(double now)
        {
            if (finished) return;

            if (IsTerminalOrMissing(roster))
            {
                Fail(FailureReason(roster));
                return;
            }
            if (roster.playerIds == null || roster.slot < 0)
            {
                Fail("Rendezvous roster missing player data.");
                return;
            }

            startedAtSeconds = now;
            lastPollAtSeconds = now;
            active = true;

            if (isHost)
                connection.Host(matchId, roster.playerIds);
            // The guest waits for Tick to begin polling for the join code.
        }

        /// <summary>Call every frame the rendezvous is in progress. No-ops once inactive.</summary>
        public void Tick(double now)
        {
            if (!active || finished) return;

            if (connection.Phase == RankedConnectionPhase.Failed)
            {
                Fail(Truncate(connection.FailureMessage));
                return;
            }

            if (isHost) TickHost(now);
            else TickGuest(now);
        }

        /// <summary>
        /// Tears the attempt down without a Failed event, for a host UI closing
        /// mid-rendezvous rather than the rendezvous itself going wrong. Same
        /// cleanup as Fail (cancel the connection, leave the match) but silent.
        /// </summary>
        public void Cancel()
        {
            if (finished) return;
            finished = true;
            active = false;
            connection.Cancel();
            _ = LeaveSwallowedAsync();
        }

        // ===== HOST =====

        private void TickHost(double now)
        {
            if (connection.Phase == RankedConnectionPhase.Connected)
            {
                Succeed();
                return;
            }

            if (publishConfirmed)
            {
                if (publishSucceededAtSeconds < 0) publishSucceededAtSeconds = now;
                if (now - publishSucceededAtSeconds > HostConnectDeadlineSeconds)
                    Fail("Timed out waiting for the opponent to connect.");
                return;
            }

            string code = connection.JoinCode;
            if (code == null) return;
            observedCode = code;

            if (callInFlight) return;
            if (lastPublishAttemptAtSeconds >= 0 && now - lastPublishAttemptAtSeconds < PollIntervalSeconds) return;

            lastPublishAttemptAtSeconds = now;
            callInFlight = true;
            _ = PublishAsync(code);
        }

        private async Task PublishAsync(string code)
        {
            try
            {
                RendezvousResult result = await service.RendezvousAsync(matchId, code);
                if (finished) return;
                if (IsTerminalOrMissing(result))
                {
                    Fail(FailureReason(result));
                    return;
                }
                publishConfirmed = true;
            }
            catch
            {
                // Retried on the next 2s poll by TickHost's cadence gate.
            }
            finally
            {
                callInFlight = false;
            }
        }

        // ===== GUEST =====

        private void TickGuest(double now)
        {
            if (joinCalled)
            {
                if (connection.Phase == RankedConnectionPhase.Connected)
                {
                    Succeed();
                    return;
                }
                if (now - joinedAtSeconds > PostJoinDeadlineSeconds)
                    Fail("Timed out waiting for the connection after joining.");
                return;
            }

            if (discoveredCode != null)
            {
                connection.Join(matchId, roster.playerIds, discoveredCode);
                joinCalled = true;
                joinedAtSeconds = now;
                return;
            }

            if (now - startedAtSeconds > CodeWaitDeadlineSeconds)
            {
                Fail("Timed out waiting for the host's join code.");
                return;
            }

            if (callInFlight) return;
            if (now - lastPollAtSeconds < PollIntervalSeconds) return;

            lastPollAtSeconds = now;
            callInFlight = true;
            _ = PollForCodeAsync();
        }

        private async Task PollForCodeAsync()
        {
            try
            {
                RendezvousResult result = await service.RendezvousAsync(matchId, null);
                if (finished) return;
                if (IsTerminalOrMissing(result))
                {
                    Fail(FailureReason(result));
                    return;
                }
                if (result.joinCode != null) discoveredCode = result.joinCode;
            }
            catch
            {
                // Retried on the next 2s poll by TickGuest's cadence gate.
            }
            finally
            {
                callInFlight = false;
            }
        }

        // ===== TERMINAL STATES =====

        private void Succeed()
        {
            if (finished) return;
            finished = true;
            active = false;
            _ = ConfirmConnectedSwallowedAsync();
            Connected?.Invoke();
        }

        private void Fail(string reason)
        {
            if (finished) return;
            finished = true;
            active = false;
            connection.Cancel();
            _ = LeaveSwallowedAsync();
            Failed?.Invoke(reason);
        }

        private async Task ConfirmConnectedSwallowedAsync()
        {
            try { await service.ConfirmConnectedAsync(matchId); }
            catch { /* recording only; never surfaces into Tick. */ }
        }

        private async Task LeaveSwallowedAsync()
        {
            try { await service.LeaveAsync(matchId, false); }
            catch { /* best-effort cleanup; never surfaces into Tick. */ }
        }

        private static bool IsTerminalOrMissing(RendezvousResult result)
        {
            if (result == null || result.state == null) return true;
            switch (result.state.Value)
            {
                case MatchRecordState.Settled:
                case MatchRecordState.Void:
                case MatchRecordState.Disputed:
                    return true;
                default:
                    return false;
            }
        }

        private static string FailureReason(RendezvousResult result)
        {
            if (!string.IsNullOrWhiteSpace(result?.message)) return Truncate(result.message);
            if (result?.state == null) return "Match record is no longer available.";
            return "Match record is " + result.state.Value + ".";
        }

        private static string Truncate(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return "Rendezvous failed.";
            return message.Length > 140 ? message.Substring(0, 140) : message;
        }
    }
}
