using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class DisconnectHoldTests
    {
        /// <summary>Presence and ResolveHold answer from scripts; each call is recorded.</summary>
        private sealed class ScriptedHoldService : IRankedMatchService
        {
            public readonly Queue<Func<Task<PresenceResult>>> PresenceAnswers = new Queue<Func<Task<PresenceResult>>>();
            public readonly Queue<Func<Task<ResolveHoldResult>>> ResolveAnswers = new Queue<Func<Task<ResolveHoldResult>>>();
            public readonly List<string> Calls = new List<string>();

            public Task<PresenceResult> PresenceAsync(string matchId, bool holding)
            {
                Calls.Add(holding ? "presence" : "resumed");
                if (!holding) return Task.FromResult(Seen());
                return PresenceAnswers.Count > 0 ? PresenceAnswers.Dequeue()() : Task.FromResult(Seen());
            }

            public Task<ResolveHoldResult> ResolveHoldAsync(string matchId)
            {
                Calls.Add("resolve");
                return ResolveAnswers.Count > 0
                    ? ResolveAnswers.Dequeue()()
                    : Task.FromResult(new ResolveHoldResult { outcome = HoldOutcome.OpponentPresent });
            }

            public int Count(string call) => Calls.Count(c => c == call);

            public Task<MatchResultView> GetResultAsync(string matchId) => throw new NotSupportedException();
            public Task<RendezvousResult> RendezvousAsync(string matchId, string joinCode) => throw new NotSupportedException();
            public Task ConfirmConnectedAsync(string matchId) => throw new NotSupportedException();
            public Task<LeaveMatchResult> LeaveAsync(string matchId, bool forfeit) => throw new NotSupportedException();
        }

        private static PresenceResult Seen() =>
            new PresenceResult { state = MatchRecordState.Open, opponentSeenSecondsAgo = 0 };

        private static Func<Task<T>> Answer<T>(T value) => () => Task.FromResult(value);

        private static Func<Task<T>> Fail<T>() => () => Task.FromException<T>(new InvalidOperationException("offline"));

        private static (DisconnectHold hold, ScriptedHoldService service) Ranked()
        {
            var service = new ScriptedHoldService();
            return (new DisconnectHold(service, "m1"), service);
        }

        private static MatchResultView Settled(bool won, MatchEndCause cause) =>
            new MatchResultView { state = MatchRecordState.Settled, won = won, cause = cause, rrDelta = won ? 15 : -15 };

        [Test]
        public void Constructor_NeedsBothOrNeither()
        {
            Assert.Throws<ArgumentException>(() => new DisconnectHold(new ScriptedHoldService(), null));
            Assert.Throws<ArgumentException>(() => new DisconnectHold(null, "m1"));
            Assert.IsFalse(new DisconnectHold(null, null).IsRanked);
        }

        [Test]
        public void Stages_WaitThenOfferTheClaim_WithACountdown()
        {
            var (hold, _) = Ranked();
            hold.Start(0);
            Assert.AreEqual(HoldStage.Waiting, hold.Current.Stage);
            Assert.AreEqual("Waiting for them to come back… 10", hold.Current.Line);
            Assert.IsNull(hold.Current.Action);

            hold.Tick(3.5);
            Assert.AreEqual("Waiting for them to come back… 7", hold.Current.Line);

            hold.Tick(10);
            Assert.AreEqual(HoldStage.CanAct, hold.Current.Stage);
            Assert.AreEqual("Claim win", hold.Current.Action);
            Assert.AreEqual("Claim the win, or keep waiting. The match ends in 50.", hold.Current.Line);
        }

        [Test]
        public void Presence_GoesOutOnceASecond_WithOneInFlight()
        {
            var (hold, service) = Ranked();
            var slow = new TaskCompletionSource<PresenceResult>();
            service.PresenceAnswers.Enqueue(() => slow.Task);
            hold.Start(0);

            hold.Tick(0);
            hold.Tick(2);
            Assert.AreEqual(1, service.Count("presence"), "No second call while the first is unanswered.");

            slow.SetResult(Seen());
            hold.Tick(2.5);
            hold.Tick(2.9);
            Assert.AreEqual(2, service.Count("presence"));
            hold.Tick(3.5);
            Assert.AreEqual(3, service.Count("presence"));
        }

        [Test]
        public void TwoFailedCalls_MeanWeAreReconnecting_AndAnAnswerRecovers()
        {
            var (hold, service) = Ranked();
            service.PresenceAnswers.Enqueue(Fail<PresenceResult>());
            service.PresenceAnswers.Enqueue(Fail<PresenceResult>());
            hold.Start(0);

            hold.Tick(0);
            Assert.AreEqual(HoldStage.Waiting, hold.Current.Stage, "One failure is not enough.");
            hold.Tick(1);
            Assert.AreEqual(HoldStage.Reconnecting, hold.Current.Stage);
            Assert.AreEqual("Reconnecting…", hold.Current.Title);

            hold.Tick(2);
            Assert.AreEqual(HoldStage.Waiting, hold.Current.Stage);
        }

        [TestCase(true, MatchEndCause.Forfeit, HoldEnding.OpponentSurrendered)]
        [TestCase(false, MatchEndCause.Forfeit, HoldEnding.Surrendered)]
        [TestCase(true, MatchEndCause.Abandoned, HoldEnding.Won)]
        [TestCase(false, MatchEndCause.Abandoned, HoldEnding.Lost)]
        [TestCase(true, MatchEndCause.Played, HoldEnding.Won)]
        public void ATerminalPresenceAnswer_ResolvesFromTheServersResult(bool won, MatchEndCause cause, HoldEnding ending)
        {
            var (hold, service) = Ranked();
            var result = Settled(won, cause);
            service.PresenceAnswers.Enqueue(Answer(new PresenceResult { state = MatchRecordState.Settled, result = result }));
            hold.Start(0);
            hold.Tick(0);

            Assert.AreEqual(HoldStage.Resolved, hold.Current.Stage);
            Assert.AreEqual(ending, hold.Current.Ending);
            Assert.AreSame(result, hold.Current.Result);
            Assert.IsFalse(hold.IsHolding);
        }

        [Test]
        public void AVoidedRecord_ResolvesAsVoided()
        {
            var (hold, service) = Ranked();
            service.PresenceAnswers.Enqueue(Answer(new PresenceResult
            {
                state = MatchRecordState.Void, result = new MatchResultView { state = MatchRecordState.Void }
            }));
            hold.Start(0);
            hold.Tick(0);
            Assert.AreEqual(HoldEnding.Voided, hold.Current.Ending);
        }

        [Test]
        public void Act_IsIgnoredBeforeTheClaimIsOffered()
        {
            var (hold, service) = Ranked();
            hold.Start(0);
            hold.Act(5);
            Assert.AreEqual(HoldStage.Waiting, hold.Current.Stage);
            Assert.AreEqual(0, service.Count("resolve"));
        }

        [Test]
        public void AGrantedClaim_ResolvesAsAWin()
        {
            var (hold, service) = Ranked();
            var result = Settled(true, MatchEndCause.Abandoned);
            service.ResolveAnswers.Enqueue(Answer(new ResolveHoldResult { outcome = HoldOutcome.Won, result = result }));
            hold.Start(0);
            hold.Tick(10);

            hold.Act(10.5);
            Assert.AreEqual(HoldStage.Resolved, hold.Current.Stage);
            Assert.AreEqual(HoldEnding.Won, hold.Current.Ending);
            Assert.AreSame(result, hold.Current.Result);
        }

        [Test]
        public void ARefusedClaim_ReturnsToTheOffer_WithANoteThatFades()
        {
            var (hold, service) = Ranked();
            service.ResolveAnswers.Enqueue(Answer(new ResolveHoldResult { outcome = HoldOutcome.OpponentPresent }));
            hold.Start(0);
            hold.Tick(10);
            hold.Act(11);

            Assert.AreEqual(HoldStage.CanAct, hold.Current.Stage);
            Assert.AreEqual("Your opponent is still connected. Keep waiting.", hold.Current.Line);
            hold.Tick(12);
            Assert.AreEqual("Your opponent is still connected. Keep waiting.", hold.Current.Line, "The next tick must not wipe it.");
            hold.Tick(11 + DisconnectHold.NoteSeconds);
            StringAssert.StartsWith("Claim the win", hold.Current.Line);
        }

        [Test]
        public void ATooEarlyClaim_StaysClaimed_AndGoesAgain()
        {
            var (hold, service) = Ranked();
            service.ResolveAnswers.Enqueue(Answer(new ResolveHoldResult { outcome = HoldOutcome.TooEarly }));
            service.ResolveAnswers.Enqueue(Answer(new ResolveHoldResult
            {
                outcome = HoldOutcome.Won, result = Settled(true, MatchEndCause.Abandoned)
            }));
            hold.Start(0);
            hold.Tick(10);
            hold.Act(10);
            Assert.AreEqual(HoldStage.Claiming, hold.Current.Stage);

            hold.Tick(11);
            Assert.AreEqual(1, service.Count("resolve"), "Retries wait ResolveRetrySeconds.");
            hold.Tick(12);
            Assert.AreEqual(2, service.Count("resolve"));
            Assert.AreEqual(HoldEnding.Won, hold.Current.Ending);
        }

        [Test]
        public void AtSixtySeconds_TheHoldAsksToResolve_AndAVoidEndsIt()
        {
            var (hold, service) = Ranked();
            service.ResolveAnswers.Enqueue(Answer(new ResolveHoldResult { outcome = HoldOutcome.OpponentPresent }));
            service.ResolveAnswers.Enqueue(Answer(new ResolveHoldResult { outcome = HoldOutcome.Voided }));
            hold.Start(0);
            hold.Tick(59);
            Assert.AreEqual(0, service.Count("resolve"));

            hold.Tick(60);
            Assert.AreEqual(1, service.Count("resolve"));
            Assert.IsTrue(hold.IsHolding, "The server's clock may be behind; keep asking.");
            int presenceBefore = service.Count("presence");
            hold.Tick(61);
            Assert.AreEqual(presenceBefore, service.Count("presence"), "Past 60 s only ResolveHold is called.");
            hold.Tick(62);
            Assert.AreEqual(HoldEnding.Voided, hold.Current.Ending);
        }

        [Test]
        public void Resume_StandsDown_TellsTheServer_AndDropsALateAnswer()
        {
            var (hold, service) = Ranked();
            var slow = new TaskCompletionSource<PresenceResult>();
            service.PresenceAnswers.Enqueue(() => slow.Task);
            hold.Start(0);
            hold.Tick(0);

            hold.Resume();
            Assert.AreEqual(HoldStage.None, hold.Current.Stage);
            Assert.AreEqual(1, service.Count("resumed"));

            slow.SetResult(new PresenceResult { state = MatchRecordState.Settled, result = Settled(true, MatchEndCause.Abandoned) });
            Assert.AreEqual(HoldStage.None, hold.Current.Stage, "An answer from the finished hold must not resolve the match.");
        }

        [Test]
        public void ResumeDuringAClaim_WaitsForItsAnswer_ThenStandsDownIfRefused()
        {
            var (hold, service) = Ranked();
            var slow = new TaskCompletionSource<ResolveHoldResult>();
            service.ResolveAnswers.Enqueue(() => slow.Task);
            hold.Start(0);
            hold.Tick(10);
            hold.Act(10);

            hold.Resume();
            Assert.AreEqual(HoldStage.Claiming, hold.Current.Stage, "The claim may already be settled on the server.");

            slow.SetResult(new ResolveHoldResult { outcome = HoldOutcome.OpponentPresent });
            Assert.AreEqual(HoldStage.None, hold.Current.Stage);
            Assert.AreEqual(1, service.Count("resumed"));
        }

        [Test]
        public void ResumeDuringAClaim_StillEndsTheMatchIfGranted()
        {
            var (hold, service) = Ranked();
            var slow = new TaskCompletionSource<ResolveHoldResult>();
            service.ResolveAnswers.Enqueue(() => slow.Task);
            hold.Start(0);
            hold.Tick(10);
            hold.Act(10);
            hold.Resume();

            slow.SetResult(new ResolveHoldResult { outcome = HoldOutcome.Won, result = Settled(true, MatchEndCause.Abandoned) });
            Assert.AreEqual(HoldEnding.Won, hold.Current.Ending);
        }

        [Test]
        public void UnreachableForTheWholeHold_GivesUpAtNinetySeconds()
        {
            var (hold, service) = Ranked();
            for (int i = 0; i < 200; i++)
            {
                service.PresenceAnswers.Enqueue(Fail<PresenceResult>());
                service.ResolveAnswers.Enqueue(Fail<ResolveHoldResult>());
            }
            hold.Start(0);
            for (double t = 0; t < DisconnectHold.GiveUpOfflineSeconds; t += 0.5) hold.Tick(t);
            Assert.IsTrue(hold.IsHolding);

            hold.Tick(DisconnectHold.GiveUpOfflineSeconds);
            Assert.AreEqual(HoldEnding.ConnectionLost, hold.Current.Ending);
        }

        [Test]
        public void PrivateMatch_RunsOnTheClock_WithLeave()
        {
            var hold = new DisconnectHold(null, null);
            hold.Start(0);
            hold.Tick(10);
            Assert.AreEqual("Leave match", hold.Current.Action);

            hold.Act(12);
            Assert.AreEqual(HoldEnding.OpponentLeft, hold.Current.Ending);

            var waited = new DisconnectHold(null, null);
            waited.Start(0);
            waited.Tick(60);
            Assert.AreEqual(HoldEnding.OpponentLeft, waited.Current.Ending);
        }

        [Test]
        public void Start_AfterTheMatchResolved_DoesNothing()
        {
            var hold = new DisconnectHold(null, null);
            hold.Start(0);
            hold.Tick(60);
            hold.Start(100);
            Assert.AreEqual(HoldStage.Resolved, hold.Current.Stage);
        }
    }
}
