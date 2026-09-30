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
    /// Failures are ignored: a hold is what notices a lost connection.
    /// </summary>
    public sealed class PresenceHeartbeat
    {
        public const double IntervalSeconds = 4;

        private readonly IRankedMatchService service;
        private readonly string matchId;
        private double lastAt = double.NegativeInfinity;
        private bool inFlight;
        private bool stopped;

        /// <summary>The server has already ended this match.</summary>
        public event Action<MatchResultView> Decided;

        public PresenceHeartbeat(IRankedMatchService service, string matchId)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            if (string.IsNullOrWhiteSpace(matchId)) throw new ArgumentException("matchId must not be blank.", nameof(matchId));
            this.matchId = matchId;
        }

        public void Tick(double now)
        {
            if (stopped || inFlight || now - lastAt < IntervalSeconds) return;
            lastAt = now;
            _ = BeatAsync();
        }

        /// <summary>The match is over on this side; later answers are dropped.</summary>
        public void Stop() => stopped = true;

        private async Task BeatAsync()
        {
            inFlight = true;
            PresenceResult answer;
            try { answer = await service.PresenceAsync(matchId, false); }
            catch (Exception) { inFlight = false; return; }
            inFlight = false;
            if (stopped) return;
            if (DisconnectHold.IsTerminal(answer?.result))
            {
                stopped = true;
                Decided?.Invoke(answer.result);
            }
        }
    }
}
