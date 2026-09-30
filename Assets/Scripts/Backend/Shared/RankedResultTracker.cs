using System;
using System.Threading.Tasks;

namespace NodeWar.Backend
{
    /// <summary>Where a finished ranked match's result stands, as the end card shows it.</summary>
    public enum RankedResultPhase
    {
        /// <summary>Asking the server; the logs are still arriving.</summary>
        Confirming,
        Settled,
        Void,
        Disputed,
        /// <summary>No answer within the tracking window; the match will settle or void later.</summary>
        StillWaiting,
        /// <summary>The server could not be reached; the log upload retries from the lobby.</summary>
        Offline,
        /// <summary>The server refused to say (not in the match, no record).</summary>
        Unavailable
    }

    /// <summary>One state of the end card's rank block: a headline and a line under it.</summary>
    public sealed class RankedResultStatus
    {
        public RankedResultPhase Phase;
        public string Headline;
        public string Detail;

        /// <summary>The server's answer when Settled, otherwise null.</summary>
        public MatchResultView Result;

        public static RankedResultStatus For(RankedResultPhase phase, MatchResultView result = null)
        {
            var status = new RankedResultStatus { Phase = phase, Result = result };
            switch (phase)
            {
                case RankedResultPhase.Confirming:
                    status.Headline = "Confirming the result…";
                    status.Detail = "";
                    break;
                case RankedResultPhase.Settled:
                    int delta = result?.rrDelta ?? 0;
                    status.Headline = (delta >= 0 ? "+" : "−") + Math.Abs(delta) + " RR";
                    status.Detail = SettledDetail(result);
                    break;
                case RankedResultPhase.Void:
                    status.Headline = "Match voided";
                    status.Detail = "No rating change.";
                    break;
                case RankedResultPhase.Disputed:
                    status.Headline = "Result disputed";
                    status.Detail = "The two results did not match. No rating change.";
                    break;
                case RankedResultPhase.StillWaiting:
                    status.Headline = "Waiting for your opponent's result";
                    status.Detail = "It will appear in Match history.";
                    break;
                case RankedResultPhase.Offline:
                    status.Headline = "Couldn't reach the server";
                    status.Detail = "Your result will be sent when you're back online.";
                    break;
                default:
                    status.Headline = "Result unavailable";
                    status.Detail = string.IsNullOrEmpty(result?.message) ? "" : result.message;
                    break;
            }
            return status;
        }

        private static string SettledDetail(MatchResultView result)
        {
            int arena = Clamp(result?.arenaAfter ?? 0, 0, RankTable.Names.Count - 1);
            string name = RankTable.Names[arena];
            if (result != null && result.promoted) return "Promoted to " + name;
            if (result != null && result.demoted) return "Dropped to " + name;
            return name + " · " + (result?.rrAfter ?? 0) + " RR";
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }

    /// <summary>
    /// Follows one finished ranked match until the server has decided it, for
    /// the end card (7.4). The first report to arrive leaves a match Pending, so
    /// the uploader's own ReportMatch answer rarely carries the result; this
    /// asks GetMatchResult on a cadence instead, whichever side uploaded first.
    ///
    /// Holds no Unity time: the caller passes elapsed seconds into
    /// <see cref="Tick"/>, as PlayPopup does for RankedQueuePresenter. One call
    /// is in flight at a time, and answers for an older match are dropped.
    /// </summary>
    public sealed class RankedResultTracker
    {
        public const double PollIntervalSeconds = 2.0;
        public const double TrackSeconds = 40.0;
        public const int MaxConsecutiveFailures = 3;

        private readonly IRankedMatchService service;

        private string matchId;
        private double startedAt;
        private double lastPollAt;
        private bool inFlight;
        private int failures;
        private int generation;

        public RankedResultStatus Current { get; private set; }

        /// <summary>True until the result is final or tracking gives up.</summary>
        public bool IsActive { get; private set; }

        public event Action<RankedResultStatus> Changed;

        public RankedResultTracker(IRankedMatchService service)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>Starts following a match. The first poll goes out on the next Tick.</summary>
        public void Start(string matchId, double nowSeconds)
        {
            if (string.IsNullOrWhiteSpace(matchId)) throw new ArgumentException("matchId must not be blank.", nameof(matchId));
            generation++;
            this.matchId = matchId;
            startedAt = nowSeconds;
            lastPollAt = double.NegativeInfinity;
            inFlight = false;
            failures = 0;
            IsActive = true;
            Publish(RankedResultStatus.For(RankedResultPhase.Confirming));
        }

        /// <summary>Stops without a final state, e.g. when the player leaves the end card.</summary>
        public void Stop()
        {
            generation++;
            IsActive = false;
        }

        public void Tick(double nowSeconds)
        {
            if (!IsActive || inFlight) return;
            if (nowSeconds - startedAt >= TrackSeconds)
            {
                Finish(RankedResultStatus.For(RankedResultPhase.StillWaiting));
                return;
            }
            if (nowSeconds - lastPollAt < PollIntervalSeconds) return;

            lastPollAt = nowSeconds;
            _ = PollAsync(generation);
        }

        private async Task PollAsync(int attempt)
        {
            inFlight = true;
            MatchResultView result;
            try
            {
                result = await service.GetResultAsync(matchId);
            }
            catch (Exception)
            {
                if (attempt != generation) return;
                inFlight = false;
                if (++failures >= MaxConsecutiveFailures)
                    Finish(RankedResultStatus.For(RankedResultPhase.Offline));
                return;
            }

            if (attempt != generation) return;
            inFlight = false;
            failures = 0;

            if (result?.state == null)
            {
                Finish(RankedResultStatus.For(RankedResultPhase.Unavailable, result));
                return;
            }
            switch (result.state.Value)
            {
                case MatchRecordState.Settled:
                    Finish(RankedResultStatus.For(RankedResultPhase.Settled, result));
                    break;
                case MatchRecordState.Void:
                    Finish(RankedResultStatus.For(RankedResultPhase.Void, result));
                    break;
                case MatchRecordState.Disputed:
                    Finish(RankedResultStatus.For(RankedResultPhase.Disputed, result));
                    break;
                default:
                    // Open or Pending: the logs are still arriving.
                    break;
            }
        }

        private void Finish(RankedResultStatus status)
        {
            IsActive = false;
            Publish(status);
        }

        private void Publish(RankedResultStatus status)
        {
            Current = status;
            Changed?.Invoke(status);
        }
    }
}
