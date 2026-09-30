using System;
using System.Threading.Tasks;

namespace NodeWar.Backend
{
    /// <summary>Where a disconnect hold stands (D15).</summary>
    public enum HoldStage
    {
        None,
        /// <summary>Stage 1, 0-10 s: waiting for the opponent; nobody can act.</summary>
        Waiting,
        /// <summary>Stage 2, 10-60 s: the player may claim the win (ranked) or leave (private).</summary>
        CanAct,
        /// <summary>Our own server calls fail: we are the side that dropped.</summary>
        Reconnecting,
        /// <summary>The player's claim is with the server.</summary>
        Claiming,
        /// <summary>The hold ended the match; see <see cref="HoldStatus.Ending"/>.</summary>
        Resolved
    }

    /// <summary>How a resolved hold ended the match, from this player's side.</summary>
    public enum HoldEnding
    {
        None,
        /// <summary>The opponent was gone and the server awarded the win.</summary>
        Won,
        /// <summary>The server settled it against us: we were away too long.</summary>
        Lost,
        /// <summary>Both sides were present but could not reach each other. No result.</summary>
        Voided,
        OpponentSurrendered,
        /// <summary>We had surrendered (or forfeited) this match.</summary>
        Surrendered,
        /// <summary>Private match: the opponent never came back, or we chose to leave.</summary>
        OpponentLeft,
        /// <summary>We could not reach the server for too long. The server decides later.</summary>
        ConnectionLost
    }

    public sealed class HoldStatus
    {
        public HoldStage Stage;
        public HoldEnding Ending;
        public string Title = "";
        public string Line = "";

        /// <summary>The one button's label, or null when there is no button.</summary>
        public string Action;

        /// <summary>The server's result for a ranked hold that resolved, otherwise null.</summary>
        public MatchResultView Result;
    }

    /// <summary>
    /// Runs one disconnect hold (8.2c: D15's stages, D12's presence): a local
    /// clock for the countdown, the server's word for every decision. Ranked:
    /// calls Presence about once a second, offers "Claim win" from 10 s, and
    /// asks ResolveHold itself at 60 s. Without a match record (a private
    /// match) the same stages run on the clock alone, with "Leave match".
    ///
    /// Holds no Unity time: the caller passes seconds into <see cref="Tick"/>,
    /// as PlayPopup does for RankedQueuePresenter. One server call is in
    /// flight at a time; an answer that lands after the hold stood down is
    /// dropped, except a claim's, which is always honoured (see Resume).
    /// </summary>
    public sealed class DisconnectHold
    {
        public const double ActAfterSeconds = 10;
        public const double EndAfterSeconds = 60;
        public const double PresenceIntervalSeconds = 1;
        public const double ResolveRetrySeconds = 2;
        /// <summary>Failed server calls in a row before we say the dropped connection is ours.</summary>
        public const int ReconnectingAfterFailures = 2;
        /// <summary>Hold length after which an unreachable server makes us give up from our side.</summary>
        public const double GiveUpOfflineSeconds = 90;
        /// <summary>How long a "your opponent is still here" note stays on the overlay.</summary>
        public const double NoteSeconds = 4;

        private readonly IRankedMatchService service;
        private readonly string matchId;

        private double startedAt;
        private double lastPresenceAt;
        private double lastResolveAt;
        private int failures;
        private bool inFlight;
        private bool claiming;
        private bool resumeAfterClaim;
        private string note;
        private double noteUntil;
        private int generation;

        public HoldStatus Current { get; private set; } = new HoldStatus { Stage = HoldStage.None };

        public bool IsHolding => Current.Stage != HoldStage.None && Current.Stage != HoldStage.Resolved;

        public bool IsRanked => service != null;

        public event Action<HoldStatus> Changed;

        /// <summary>A ranked hold needs both; a private match passes neither.</summary>
        public DisconnectHold(IRankedMatchService service, string matchId)
        {
            if ((service == null) != string.IsNullOrWhiteSpace(matchId))
                throw new ArgumentException("A ranked hold needs both a service and a match ID; a private one neither.");
            this.service = service;
            this.matchId = matchId;
        }

        /// <summary>The runner stopped advancing. Starts a hold unless one is running or the match already resolved.</summary>
        public void Start(double now)
        {
            if (IsHolding || Current.Stage == HoldStage.Resolved) return;
            generation++;
            startedAt = now;
            lastPresenceAt = double.NegativeInfinity;
            lastResolveAt = double.NegativeInfinity;
            failures = 0;
            inFlight = false;
            claiming = false;
            resumeAfterClaim = false;
            note = null;
            Publish(StageFor(now));
        }

        /// <summary>
        /// The connection came back. Tells the server once, then stands down.
        /// A claim already sent is not abandoned: the server may have settled
        /// it, and dropping that answer would leave both clients playing a
        /// match the server has already decided. The hold stands down when the
        /// claim is refused, or resolves if it was granted.
        /// </summary>
        public void Resume()
        {
            if (!IsHolding) return;
            if (claiming)
            {
                resumeAfterClaim = true;
                return;
            }
            StandDown();
        }

        /// <summary>The overlay's button: claim the win (ranked) or leave (private).</summary>
        public void Act(double now)
        {
            if (Current.Stage != HoldStage.CanAct) return;
            if (!IsRanked)
            {
                Resolve(HoldEnding.OpponentLeft, null);
                return;
            }
            claiming = true;
            note = null;
            Publish(Status(HoldStage.Claiming, "Claiming the win…", "", null));
            if (!inFlight) _ = ResolveAsync(generation, now);
        }

        public void Tick(double now)
        {
            if (!IsHolding) return;
            double elapsed = now - startedAt;

            if (!IsRanked)
            {
                if (elapsed >= EndAfterSeconds) Resolve(HoldEnding.OpponentLeft, null);
                else Publish(StageFor(now));
                return;
            }

            if (failures >= ReconnectingAfterFailures && elapsed >= GiveUpOfflineSeconds)
            {
                Resolve(HoldEnding.ConnectionLost, null);
                return;
            }

            if (!inFlight)
            {
                bool resolveDue = claiming || elapsed >= EndAfterSeconds;
                if (resolveDue && now - lastResolveAt >= ResolveRetrySeconds)
                    _ = ResolveAsync(generation, now);
                else if (!resolveDue && now - lastPresenceAt >= PresenceIntervalSeconds)
                    _ = PresenceAsync(generation, now);
            }

            // An answer can land synchronously and end the hold; the countdown
            // must not be published over it.
            if (!IsHolding) return;
            if (!claiming && Current.Stage != HoldStage.Reconnecting) Publish(StageFor(now));
        }

        private async Task PresenceAsync(int attempt, double now)
        {
            inFlight = true;
            lastPresenceAt = now;
            PresenceResult answer;
            try { answer = await service.PresenceAsync(matchId, true); }
            catch (Exception)
            {
                if (attempt != generation) return;
                inFlight = false;
                OnServerUnreachable();
                return;
            }
            if (attempt != generation) return;
            inFlight = false;
            OnServerAnswered(now);

            if (IsTerminal(answer?.result)) ResolveFrom(answer.result);
        }

        private async Task ResolveAsync(int attempt, double now)
        {
            inFlight = true;
            lastResolveAt = now;
            ResolveHoldResult answer;
            try { answer = await service.ResolveHoldAsync(matchId); }
            catch (Exception)
            {
                if (attempt != generation) return;
                inFlight = false;
                OnServerUnreachable();
                return;
            }
            if (attempt != generation) return;
            inFlight = false;

            switch (answer?.outcome)
            {
                case HoldOutcome.Won:
                    Resolve(HoldEnding.Won, answer.result);
                    return;
                case HoldOutcome.Voided:
                    Resolve(HoldEnding.Voided, answer.result);
                    return;
                case HoldOutcome.AlreadyResolved:
                    ResolveFrom(answer.result);
                    return;
                case HoldOutcome.TooEarly:
                    // The server's hold started a moment after ours. A claim stays
                    // claimed and goes again after ResolveRetrySeconds.
                    OnServerAnswered(now);
                    return;
            }

            // OpponentPresent, or refused: keep holding. A player's claim is
            // answered with a note; the automatic one at 60 s simply retries.
            if (claiming)
            {
                claiming = false;
                if (answer?.outcome == HoldOutcome.OpponentPresent)
                {
                    note = "Your opponent is still connected. Keep waiting.";
                    noteUntil = now + NoteSeconds;
                }
                if (resumeAfterClaim)
                {
                    StandDown();
                    return;
                }
                failures = 0;
                Publish(StageFor(now));
                return;
            }
            OnServerAnswered(now);
        }

        private void OnServerAnswered(double now)
        {
            bool wasOffline = Current.Stage == HoldStage.Reconnecting;
            failures = 0;
            if (wasOffline && !claiming) Publish(StageFor(now));
        }

        private void OnServerUnreachable()
        {
            if (++failures >= ReconnectingAfterFailures && !claiming)
                Publish(Status(HoldStage.Reconnecting, "Reconnecting…", "Your connection dropped.", null));
        }

        private void StandDown()
        {
            generation++;
            inFlight = false;
            claiming = false;
            resumeAfterClaim = false;
            if (IsRanked) _ = TellServerResumed();
            Publish(new HoldStatus { Stage = HoldStage.None });
        }

        private async Task TellServerResumed()
        {
            try { await service.PresenceAsync(matchId, false); }
            catch (Exception) { /* A stale hold on the server only shortens the next claim's wait. */ }
        }

        private static bool IsTerminal(MatchResultView result) =>
            result?.state == MatchRecordState.Settled || result?.state == MatchRecordState.Void ||
            result?.state == MatchRecordState.Disputed;

        /// <summary>A terminal record decides the ending, whoever resolved it.</summary>
        private void ResolveFrom(MatchResultView result)
        {
            if (result?.state != MatchRecordState.Settled)
            {
                Resolve(HoldEnding.Voided, result);
                return;
            }
            bool won = result.won == true;
            HoldEnding ending = result.cause == MatchEndCause.Forfeit
                ? (won ? HoldEnding.OpponentSurrendered : HoldEnding.Surrendered)
                : (won ? HoldEnding.Won : HoldEnding.Lost);
            Resolve(ending, result);
        }

        private void Resolve(HoldEnding ending, MatchResultView result)
        {
            generation++;
            inFlight = false;
            claiming = false;
            resumeAfterClaim = false;
            Publish(new HoldStatus { Stage = HoldStage.Resolved, Ending = ending, Result = result });
        }

        private HoldStatus StageFor(double now)
        {
            double elapsed = now - startedAt;
            if (elapsed < ActAfterSeconds)
            {
                int left = (int)Math.Ceiling(ActAfterSeconds - elapsed);
                return Status(HoldStage.Waiting, "Opponent disconnected",
                    "Waiting for them to come back… " + left, null);
            }
            int end = Math.Max(0, (int)Math.Ceiling(EndAfterSeconds - elapsed));
            string line = note != null && now < noteUntil
                ? note
                : (IsRanked ? "Claim the win, or keep waiting. The match ends in " : "Leave, or keep waiting. The match ends in ") + end + ".";
            return Status(HoldStage.CanAct, "Opponent disconnected", line, IsRanked ? "Claim win" : "Leave match");
        }

        private static HoldStatus Status(HoldStage stage, string title, string line, string action) =>
            new HoldStatus { Stage = stage, Title = title, Line = line, Action = action };

        private void Publish(HoldStatus status)
        {
            // The countdown line changes once a second; publishing the same text
            // every frame would rebuild the overlay for nothing.
            if (Current.Stage == status.Stage && Current.Ending == status.Ending && Current.Title == status.Title &&
                Current.Line == status.Line && Current.Action == status.Action && Current.Result == status.Result)
                return;
            Current = status;
            Changed?.Invoke(status);
        }
    }
}
