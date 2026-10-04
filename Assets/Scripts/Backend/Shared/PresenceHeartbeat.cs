using System;
using System.Threading.Tasks;

namespace NodeWar.Backend
{
    /// <summary>
    /// Tells the server this player is still in the match, every few seconds
    /// for the whole of a ranked match (8.2c). The server only awards a hold
    /// claim against an opponent it has not heard from, so an honest player who
    /// is playing normally must be heard from: without this, anyone could claim
    /// a win mid-match against an opponent who simply had no reason to call.
    ///
    /// Paused during a hold, when DisconnectHold calls Presence itself once a
    /// second. An answer carrying a terminal result (a surrender, a claim the
    /// server granted the opponent) is raised as <see cref="Decided"/>.
    /// Failures never end anything: a hold is what notices a lost connection.
    /// They are counted, though, so the HUD can tell a player whose own link
    /// dropped from one whose opponent's did before a hold has begun.
    /// </summary>
    public sealed class PresenceHeartbeat
    {
        public const double IntervalSeconds = 4;
        public const double UrgentIntervalSeconds = 1;

        private readonly IRankedMatchService service;
        private readonly string matchId;
        private double lastAt = double.NegativeInfinity;
        private bool inFlight;
        private bool stopped;
        private int failures;

        /// <summary>
        /// Set while the connection pill is up: polls every second instead of
        /// every four, so the answer to "was it us?" comes quickly.
        /// </summary>
        public bool Urgent { get; set; }

        /// <summary>Enough calls in a row have failed that the dropped connection is ours.</summary>
        public bool Unreachable { get; private set; }

        /// <summary>The server has already ended this match.</summary>
        public event Action<MatchResultView> Decided;

        /// <summary>Raised only when <see cref="Unreachable"/> flips.</summary>
        public event Action<bool> ReachabilityChanged;

        public PresenceHeartbeat(IRankedMatchService service, string matchId)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            if (string.IsNullOrWhiteSpace(matchId)) throw new ArgumentException("matchId must not be blank.", nameof(matchId));
            this.matchId = matchId;
        }

        public void Tick(double now)
        {
            double interval = Urgent ? UrgentIntervalSeconds : IntervalSeconds;
            if (stopped || inFlight || now - lastAt < interval) return;
            lastAt = now;
            _ = BeatAsync();
        }

        /// <summary>
        /// Calls now, whatever the interval. The pill just went up: only the
        /// server can say whether the dropped connection is ours.
        /// </summary>
        public void ProbeNow(double now)
        {
            if (stopped || inFlight) return;
            lastAt = now;
            _ = BeatAsync();
        }

        /// <summary>The match is over on this side; later answers are dropped.</summary>
        public void Stop() => stopped = true;

        private void SetUnreachable(bool value)
        {
            if (Unreachable == value) return;
            Unreachable = value;
            ReachabilityChanged?.Invoke(value);
        }

        private async Task BeatAsync()
        {
            inFlight = true;
            PresenceResult answer;
            try { answer = await service.PresenceAsync(matchId, false); }
            catch (Exception)
            {
                inFlight = false;
                if (stopped) return;
                if (++failures >= DisconnectHold.ReconnectingAfterFailures) SetUnreachable(true);
                return;
            }
            inFlight = false;
            if (stopped) return;
            failures = 0;
            SetUnreachable(false);
            if (DisconnectHold.IsTerminal(answer?.result))
            {
                stopped = true;
                Decided?.Invoke(answer.result);
            }
        }
    }
}
