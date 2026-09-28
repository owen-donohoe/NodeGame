using System;
using System.Threading.Tasks;

namespace NodeWar.Backend
{
    /// <summary>
    /// UnityEngine-free view contract for the ranked queue. Swap the concrete
    /// implementation to restyle the queue without touching
    /// <see cref="RankedQueuePresenter"/> or <see cref="IRankedQueueService"/>.
    /// </summary>
    public interface IRankedQueueView
    {
        void ShowIdle();
        void ShowSearching(int elapsedSeconds);
        void ShowFound(string matchId);
        void ShowFailed(string message);
        void ShowCancelled();

        /// <summary>Raised when the player asks to stop searching.</summary>
        event Action CancelRequested;
    }

    /// <summary>
    /// Drives one ranked-queue attempt: enqueue, poll on a cadence, map results
    /// to the view. Holds no Unity time of its own - the caller supplies
    /// elapsed seconds into <see cref="Tick"/>, the same way <see cref="MatchLauncher"/>
    /// is driven from PlayPopup.Update().
    ///
    /// Stops polling once a terminal state (Found, Failed, Cancelled) is
    /// reached. <see cref="MatchFound"/> exists for the future 8.2b rendezvous;
    /// this class does not implement rendezvous, lobby or draft.
    /// </summary>
    public sealed class RankedQueuePresenter
    {
        private const double PollIntervalSeconds = 2.0;

        private readonly IRankedQueueService service;
        private readonly IRankedQueueView view;

        private string ticketId;
        private bool active;
        private bool pollInFlight;
        private double startedAtSeconds;
        private double lastPollAtSeconds;

        public event Action<string> MatchFound;

        public RankedQueuePresenter(IRankedQueueService service, IRankedQueueView view)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.view.CancelRequested += OnCancelRequested;
        }

        /// <summary>True while an attempt is in progress and Tick should be called.</summary>
        public bool IsActive => active;

        public async Task StartAsync(double nowSeconds)
        {
            startedAtSeconds = nowSeconds;
            lastPollAtSeconds = nowSeconds;
            active = true;
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

        /// <summary>Call every frame the queue view is on screen. No-ops once inactive.</summary>
        public void Tick(double nowSeconds)
        {
            if (!active) return;

            int elapsed = (int)(nowSeconds - startedAtSeconds);
            if (elapsed < 0) elapsed = 0;
            view.ShowSearching(elapsed);

            if (pollInFlight) return;
            if (ticketId == null) return;
            if (nowSeconds - lastPollAtSeconds < PollIntervalSeconds) return;

            lastPollAtSeconds = nowSeconds;
            _ = PollOnceAsync();
        }

        private async Task PollOnceAsync()
        {
            pollInFlight = true;
            try
            {
                RankedQueueResult result = await service.PollAsync(ticketId);
                if (!active) return;

                switch (result.state)
                {
                    case RankedQueueState.Searching:
                        break;
                    case RankedQueueState.Found:
                        Finish();
                        view.ShowFound(result.matchId);
                        MatchFound?.Invoke(result.matchId);
                        break;
                    case RankedQueueState.Failed:
                        Fail(result.message ?? "Matchmaking failed.");
                        break;
                    case RankedQueueState.TimedOut:
                        Fail(result.message ?? "No match found in time.");
                        break;
                    case RankedQueueState.Cancelled:
                        Finish();
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

        private async void OnCancelRequested()
        {
            if (!active) return;
            string cancelling = ticketId;
            Finish();

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

        private void Fail(string message)
        {
            Finish();
            view.ShowFailed(message);
        }

        private void Finish()
        {
            active = false;
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
        }
    }
}
