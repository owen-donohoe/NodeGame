using System;
using System.Threading.Tasks;

namespace NodeWar.Backend
{
    /// <summary>
    /// UnityEngine-free view contract for the ranked queue. Swap the concrete
    /// implementation to restyle the queue without touching
    /// <see cref="RankedQueuePresenter"/>, <see cref="IRankedQueueService"/> or
    /// <see cref="IRankedMatchService"/>.
    /// </summary>
    public interface IRankedQueueView
    {
        void ShowIdle();
        void ShowSearching(int elapsedSeconds);
        void ShowFound(string matchId);
        void ShowFailed(string message);
        void ShowCancelled();

        /// <summary>The found ticket is confirmed and the join-code exchange has started.</summary>
        void ShowConnecting();

        /// <summary>A failed ticket or rendezvous is about to search again automatically.</summary>
        void ShowRequeueing(string message);

        /// <summary>The player has an active match that was already played; leaving it now forfeits it.</summary>
        void ShowForfeitPrompt();

        /// <summary>The player has an active match awaiting the opponent's report or a settlement timeout.</summary>
        void ShowWaitingForResult(int seconds);

        /// <summary>Offered after a long search: play a bot instead of continuing to wait.</summary>
        void ShowBotOffer();

        /// <summary>Raised when the player asks to stop searching, or declines a forfeit prompt.</summary>
        event Action CancelRequested;

        /// <summary>Raised when the player accepts a forfeit prompt shown by ShowForfeitPrompt.</summary>
        event Action ForfeitConfirmed;

        /// <summary>Raised when the player accepts a bot match shown by ShowBotOffer.</summary>
        event Action BotAccepted;
    }

    /// <summary>
    /// Drives one whole ranked-queue attempt: preflight (does the player already
    /// hold an active match?), enqueue and poll on a cadence, and - once a ticket
    /// is Found - the rendezvous that gets both peers connected. Holds no Unity
    /// time of its own; the caller supplies elapsed seconds into
    /// <see cref="Tick"/>, the same way <see cref="MatchLauncher"/> is driven
    /// from PlayPopup.Update().
    ///
    /// A failed ticket or a failed rendezvous re-queues automatically, up to
    /// three consecutive times, before giving up - most such failures are a
    /// transient opponent connection problem, not something the player caused.
    /// </summary>
    public sealed class RankedQueuePresenter
    {
        private const double PollIntervalSeconds = 2.0;
        private const double BotOfferSeconds = 90.0;
        private const int MaxAutoRequeues = 3;
        private const string RequeueMessage = "Opponent's connection failed — finding a new match…";

        private enum Phase { Idle, Preflight, ForfeitPrompt, WaitingForResult, Queue, Connecting, Rendezvous }

        private readonly IRankedQueueService service;
        private readonly IRankedQueueView view;
        private readonly IRankedMatchService rankedMatch;
        private readonly IPlayerStateService playerState;
        private readonly Func<IRankedConnection> connectionFactory;

        private Phase phase = Phase.Idle;
        private double lastTickNow;
        private int consecutiveAutoRequeues;

        // Queue state.
        private string ticketId;
        private bool pollInFlight;
        private bool botOfferShown;
        private double startedAtSeconds;
        private double lastPollAtSeconds;

        // WaitingForResult state.
        private int waitingSecondsLeft;
        private double waitingStartedAtSeconds;
        private bool waitingRecheckInFlight;

        // Rendezvous state.
        private RankedRendezvous rendezvous;

        /// <summary>Raised once the rendezvous connects. The connection itself loads the scene.</summary>
        public event Action<string> MatchFound;

        /// <summary>Raised once a bot offer is accepted and the ticket is cancelled.</summary>
        public event Action BotMatchAccepted;

        public RankedQueuePresenter(
            IRankedQueueService service,
            IRankedQueueView view,
            IRankedMatchService rankedMatch,
            IPlayerStateService playerState,
            Func<IRankedConnection> connectionFactory)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.rankedMatch = rankedMatch ?? throw new ArgumentNullException(nameof(rankedMatch));
            this.playerState = playerState ?? throw new ArgumentNullException(nameof(playerState));
            this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            this.view.CancelRequested += OnCancelRequested;
            this.view.BotAccepted += OnBotAccepted;
        }

        /// <summary>True while an attempt is in progress and Tick should be called.</summary>
        public bool IsActive => phase != Phase.Idle;

        public async Task StartAsync(double nowSeconds)
        {
            consecutiveAutoRequeues = 0;
            lastTickNow = nowSeconds;
            await RunPreflightAsync(nowSeconds);
        }

        /// <summary>Call every frame the queue view is on screen. No-ops in phases with nothing to poll.</summary>
        public void Tick(double nowSeconds)
        {
            lastTickNow = nowSeconds;
            switch (phase)
            {
                case Phase.Queue:
                    TickQueue(nowSeconds);
                    break;
                case Phase.WaitingForResult:
                    TickWaitingForResult(nowSeconds);
                    break;
                case Phase.Rendezvous:
                    rendezvous?.Tick(nowSeconds);
                    break;
            }
        }

        // ===== PREFLIGHT =====

        private async Task RunPreflightAsync(double now)
        {
            phase = Phase.Preflight;

            PlayerState state;
            try
            {
                state = await playerState.GetAsync();
            }
            catch (Exception ex)
            {
                Fail(ShortMessage(ex));
                return;
            }

            string activeMatchId = state?.ActiveMatch?.matchId;
            if (string.IsNullOrWhiteSpace(activeMatchId))
            {
                await EnqueueAndPollAsync(now);
                return;
            }

            // No server clock is available here, so expiry is never judged
            // locally - the server is always asked what leaving means.
            await ResolveActiveMatchAsync(activeMatchId, now, forfeit: false);
        }

        private async Task ResolveActiveMatchAsync(string matchId, double now, bool forfeit)
        {
            LeaveMatchResult result;
            try
            {
                result = await rankedMatch.LeaveAsync(matchId, forfeit);
            }
            catch (Exception ex)
            {
                Fail(ShortMessage(ex));
                return;
            }

            if (result?.outcome == null)
            {
                Fail(result?.message ?? "Could not check your active match.");
                return;
            }

            switch (result.outcome.Value)
            {
                case LeaveOutcome.Cleared:
                    await EnqueueAndPollAsync(now);
                    break;
                case LeaveOutcome.NeedsForfeit:
                    await RunForfeitPromptAsync(matchId, now);
                    break;
                case LeaveOutcome.Waiting:
                    StartWaitingForResult(result.secondsLeft, now);
                    break;
            }
        }

        private async Task RunForfeitPromptAsync(string matchId, double now)
        {
            phase = Phase.ForfeitPrompt;
            view.ShowForfeitPrompt();

            bool confirmed = await WaitForForfeitDecisionAsync();
            if (confirmed)
            {
                await ResolveActiveMatchAsync(matchId, now, forfeit: true);
            }
            else
            {
                GoIdle();
                view.ShowCancelled();
            }
        }

        private Task<bool> WaitForForfeitDecisionAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            Action onConfirm = null;
            Action onCancel = null;
            onConfirm = () =>
            {
                view.ForfeitConfirmed -= onConfirm;
                view.CancelRequested -= onCancel;
                tcs.TrySetResult(true);
            };
            onCancel = () =>
            {
                view.ForfeitConfirmed -= onConfirm;
                view.CancelRequested -= onCancel;
                tcs.TrySetResult(false);
            };
            view.ForfeitConfirmed += onConfirm;
            view.CancelRequested += onCancel;
            return tcs.Task;
        }

        private void StartWaitingForResult(int secondsLeft, double now)
        {
            phase = Phase.WaitingForResult;
            waitingSecondsLeft = secondsLeft < 0 ? 0 : secondsLeft;
            waitingStartedAtSeconds = now;
            view.ShowWaitingForResult(waitingSecondsLeft);
        }

        private void TickWaitingForResult(double now)
        {
            int elapsed = (int)(now - waitingStartedAtSeconds);
            if (elapsed < 0) elapsed = 0;
            int remaining = waitingSecondsLeft - elapsed;
            if (remaining < 0) remaining = 0;
            view.ShowWaitingForResult(remaining);

            if (remaining <= 0 && !waitingRecheckInFlight)
            {
                waitingRecheckInFlight = true;
                _ = RecheckAfterWaitingAsync(now);
            }
        }

        private async Task RecheckAfterWaitingAsync(double now)
        {
            waitingRecheckInFlight = false;
            await RunPreflightAsync(now);
        }

        // ===== QUEUE =====

        private async Task EnqueueAndPollAsync(double now)
        {
            // The caller's time may be stale: a forfeit prompt or a server call
            // can sit between it and here. Tick keeps lastTickNow current, and
            // the search timer and bot offer must count from now.
            now = Math.Max(now, lastTickNow);
            phase = Phase.Queue;
            botOfferShown = false;
            startedAtSeconds = now;
            lastPollAtSeconds = now;
            view.ShowSearching(0);

            try
            {
                ticketId = await service.EnqueueAsync();
            }
            catch (Exception ex)
            {
                Fail(ShortMessage(ex));
            }
        }

        private void TickQueue(double now)
        {
            int elapsed = (int)(now - startedAtSeconds);
            if (elapsed < 0) elapsed = 0;
            view.ShowSearching(elapsed);

            if (!botOfferShown && elapsed >= BotOfferSeconds)
            {
                botOfferShown = true;
                view.ShowBotOffer();
            }

            if (pollInFlight) return;
            if (ticketId == null) return;
            if (now - lastPollAtSeconds < PollIntervalSeconds) return;

            lastPollAtSeconds = now;
            _ = PollOnceAsync(now);
        }

        private async Task PollOnceAsync(double now)
        {
            pollInFlight = true;
            try
            {
                RankedQueueResult result = await service.PollAsync(ticketId);
                if (phase != Phase.Queue) return;

                switch (result.state)
                {
                    case RankedQueueState.Searching:
                        break;
                    case RankedQueueState.Found:
                        await HandleFoundAsync(result.matchId, now);
                        break;
                    case RankedQueueState.Failed:
                        await RequeueOrFailAsync(
                            result.message ?? "Matchmaking failed.",
                            result.message ?? "Matchmaking failed — finding a new match…",
                            now);
                        break;
                    case RankedQueueState.TimedOut:
                        Fail(result.message ?? "No match found in time.");
                        break;
                    case RankedQueueState.Cancelled:
                        GoIdle();
                        view.ShowCancelled();
                        break;
                    default:
                        Fail("Unknown matchmaking result.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Fail(ShortMessage(ex));
            }
            finally
            {
                pollInFlight = false;
            }
        }

        private void OnBotAccepted()
        {
            if (phase != Phase.Queue) return;
            _ = AcceptBotAsync();
        }

        private async Task AcceptBotAsync()
        {
            string cancelling = ticketId;
            try
            {
                if (cancelling != null) await service.CancelAsync(cancelling);
            }
            catch
            {
                // Best-effort: the offer is accepted either way.
            }
            GoIdle();
            BotMatchAccepted?.Invoke();
        }

        // ===== RENDEZVOUS =====

        private async Task HandleFoundAsync(string matchId, double now)
        {
            ticketId = null;
            phase = Phase.Connecting;
            view.ShowConnecting();

            RendezvousResult roster;
            try
            {
                roster = await rankedMatch.RendezvousAsync(matchId, null);
            }
            catch (Exception ex)
            {
                await RequeueOrFailAsync(ShortMessage(ex), RequeueMessage, now);
                return;
            }

            if (IsBadRoster(roster))
            {
                await RequeueOrFailAsync(roster?.message ?? "Could not join the match.", RequeueMessage, now);
                return;
            }

            IRankedConnection connection = connectionFactory();
            var attempt = new RankedRendezvous(rankedMatch, connection, matchId, roster);
            rendezvous = attempt;
            phase = Phase.Rendezvous;

            attempt.Connected += () => OnRendezvousConnected(matchId);
            attempt.Failed += OnRendezvousFailed;

            attempt.Start(now);
        }

        private void OnRendezvousConnected(string matchId)
        {
            GoIdle();
            MatchFound?.Invoke(matchId);
        }

        private void OnRendezvousFailed(string reason)
        {
            rendezvous = null;
            _ = RequeueOrFailAsync(reason, RequeueMessage, lastTickNow);
        }

        private async Task RequeueOrFailAsync(string failureMessage, string requeueingMessage, double now)
        {
            if (consecutiveAutoRequeues < MaxAutoRequeues)
            {
                consecutiveAutoRequeues++;
                view.ShowRequeueing(requeueingMessage);
                await RunPreflightAsync(now);
            }
            else
            {
                Fail(failureMessage);
            }
        }

        private static bool IsBadRoster(RendezvousResult roster)
        {
            if (roster == null || roster.state == null) return true;
            switch (roster.state.Value)
            {
                case MatchRecordState.Settled:
                case MatchRecordState.Void:
                case MatchRecordState.Disputed:
                    return true;
                default:
                    return false;
            }
        }

        // ===== CANCEL =====

        private void OnCancelRequested()
        {
            _ = RequestCancelAsync();
        }

        /// <summary>
        /// Cancels whatever is active. Queue: deletes the ticket. Rendezvous:
        /// cancels the connection attempt (which leaves the match). Other
        /// phases either have their own CancelRequested handling (the forfeit
        /// prompt) or nothing worth cancelling.
        /// </summary>
        public async Task RequestCancelAsync()
        {
            switch (phase)
            {
                case Phase.Queue:
                    await CancelQueueAsync();
                    break;
                case Phase.Rendezvous:
                    CancelRendezvous();
                    break;
            }
        }

        private async Task CancelQueueAsync()
        {
            string cancelling = ticketId;
            GoIdle();

            if (cancelling == null)
            {
                view.ShowCancelled();
                return;
            }

            try
            {
                await service.CancelAsync(cancelling);
                view.ShowCancelled();
            }
            catch (Exception ex)
            {
                view.ShowFailed(ShortMessage(ex));
            }
        }

        private void CancelRendezvous()
        {
            RankedRendezvous current = rendezvous;
            GoIdle();
            current?.Cancel();
            view.ShowCancelled();
        }

        // ===== SHARED =====

        private void Fail(string message)
        {
            GoIdle();
            view.ShowFailed(message);
        }

        private void GoIdle()
        {
            phase = Phase.Idle;
            ticketId = null;
            rendezvous = null;
        }

        private static string ShortMessage(Exception ex)
        {
            string message = ex.Message;
            if (string.IsNullOrWhiteSpace(message)) return ex.GetType().Name;
            return message.Length > 140 ? message.Substring(0, 140) : message;
        }

        public void Dispose()
        {
            view.CancelRequested -= OnCancelRequested;
            view.BotAccepted -= OnBotAccepted;
        }
    }
}
