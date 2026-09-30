using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class RankedResultTrackerTests
    {
        /// <summary>GetResultAsync answers from a script; anything else is unused here.</summary>
        private sealed class ScriptedResults : IRankedMatchService
        {
            public readonly Queue<Func<Task<MatchResultView>>> Answers = new Queue<Func<Task<MatchResultView>>>();
            public int Calls;

            public Task<MatchResultView> GetResultAsync(string matchId)
            {
                Calls++;
                return Answers.Count > 0 ? Answers.Dequeue()() : Task.FromResult(Pending());
            }

            public Task<PresenceResult> PresenceAsync(string matchId, bool holding) => throw new NotSupportedException();
            public Task<ResolveHoldResult> ResolveHoldAsync(string matchId) => throw new NotSupportedException();
            public Task<RendezvousResult> RendezvousAsync(string matchId, string joinCode) => throw new NotSupportedException();
            public Task ConfirmConnectedAsync(string matchId) => throw new NotSupportedException();
            public Task<LeaveMatchResult> LeaveAsync(string matchId, bool forfeit) => throw new NotSupportedException();
        }

        private static MatchResultView Pending() => new MatchResultView { state = MatchRecordState.Pending };

        private static Func<Task<MatchResultView>> Answer(MatchResultView view) => () => Task.FromResult(view);

        private static Func<Task<MatchResultView>> Fail() =>
            () => Task.FromException<MatchResultView>(new InvalidOperationException("offline"));

        private static (RankedResultTracker tracker, ScriptedResults service, List<RankedResultStatus> seen) Make()
        {
            var service = new ScriptedResults();
            var tracker = new RankedResultTracker(service);
            var seen = new List<RankedResultStatus>();
            tracker.Changed += seen.Add;
            return (tracker, service, seen);
        }

        [Test]
        public void Start_ShowsConfirming_AndPollsOnFirstTick()
        {
            var (tracker, service, seen) = Make();
            tracker.Start("m1", 100);

            Assert.AreEqual(RankedResultPhase.Confirming, seen.Single().Phase);
            Assert.AreEqual(0, service.Calls);

            tracker.Tick(100);
            Assert.AreEqual(1, service.Calls);
        }

        [Test]
        public void PendingAnswers_KeepConfirming_AndPollEveryTwoSeconds()
        {
            var (tracker, service, _) = Make();
            tracker.Start("m1", 0);

            tracker.Tick(0);
            tracker.Tick(1.9);
            Assert.AreEqual(1, service.Calls);
            tracker.Tick(2.0);
            Assert.AreEqual(2, service.Calls);
            Assert.AreEqual(RankedResultPhase.Confirming, tracker.Current.Phase);
            Assert.IsTrue(tracker.IsActive);
        }

        [Test]
        public void Settled_FinishesWithTheRRChange()
        {
            var (tracker, service, _) = Make();
            service.Answers.Enqueue(Answer(new MatchResultView
            {
                state = MatchRecordState.Settled, won = true, rrDelta = 18, rrAfter = 318, arenaAfter = 1
            }));
            tracker.Start("m1", 0);
            tracker.Tick(0);

            Assert.IsFalse(tracker.IsActive);
            Assert.AreEqual(RankedResultPhase.Settled, tracker.Current.Phase);
            Assert.AreEqual("+18 RR", tracker.Current.Headline);
            Assert.AreEqual("Arena 2 · 318 RR", tracker.Current.Detail);

            tracker.Tick(10);
            Assert.AreEqual(1, service.Calls, "A final result stops polling.");
        }

        [Test]
        public void Settled_LossAndPromotionAndDemotionWording()
        {
            Assert.AreEqual("−12 RR", RankedResultStatus.For(RankedResultPhase.Settled,
                new MatchResultView { rrDelta = -12, arenaAfter = 0 }).Headline);
            Assert.AreEqual("Promoted to Arena 3", RankedResultStatus.For(RankedResultPhase.Settled,
                new MatchResultView { rrDelta = 20, arenaAfter = 2, promoted = true }).Detail);
            Assert.AreEqual("Dropped to Arena 1", RankedResultStatus.For(RankedResultPhase.Settled,
                new MatchResultView { rrDelta = -20, arenaAfter = 0, demoted = true }).Detail);
            Assert.AreEqual("Arena 6 · 3000 RR", RankedResultStatus.For(RankedResultPhase.Settled,
                new MatchResultView { rrDelta = 5, rrAfter = 3000, arenaAfter = 99 }).Detail,
                "An arena past the table clamps to the top one.");
        }

        [TestCase(MatchRecordState.Void, RankedResultPhase.Void)]
        [TestCase(MatchRecordState.Disputed, RankedResultPhase.Disputed)]
        public void TerminalWithoutOutcome_Finishes(MatchRecordState state, RankedResultPhase phase)
        {
            var (tracker, service, _) = Make();
            service.Answers.Enqueue(Answer(new MatchResultView { state = state }));
            tracker.Start("m1", 0);
            tracker.Tick(0);

            Assert.AreEqual(phase, tracker.Current.Phase);
            Assert.IsFalse(tracker.IsActive);
        }

        [Test]
        public void Refused_IsUnavailable_WithTheServersMessage()
        {
            var (tracker, service, _) = Make();
            service.Answers.Enqueue(Answer(new MatchResultView { message = "Caller is not in this match." }));
            tracker.Start("m1", 0);
            tracker.Tick(0);

            Assert.AreEqual(RankedResultPhase.Unavailable, tracker.Current.Phase);
            Assert.AreEqual("Caller is not in this match.", tracker.Current.Detail);
        }

        [Test]
        public void NoAnswerWithinTheWindow_IsStillWaiting()
        {
            var (tracker, service, _) = Make();
            tracker.Start("m1", 0);
            for (double t = 0; t < RankedResultTracker.TrackSeconds; t += 1) tracker.Tick(t);
            Assert.IsTrue(tracker.IsActive);

            tracker.Tick(RankedResultTracker.TrackSeconds);
            Assert.AreEqual(RankedResultPhase.StillWaiting, tracker.Current.Phase);
            Assert.IsFalse(tracker.IsActive);
        }

        [Test]
        public void ThreeFailuresInARow_AreOffline_ButOneFailureRetries()
        {
            var (tracker, service, _) = Make();
            service.Answers.Enqueue(Fail());
            service.Answers.Enqueue(Answer(Pending()));
            service.Answers.Enqueue(Fail());
            service.Answers.Enqueue(Fail());
            service.Answers.Enqueue(Fail());
            tracker.Start("m1", 0);

            tracker.Tick(0);
            tracker.Tick(2);
            tracker.Tick(4);
            tracker.Tick(6);
            Assert.AreEqual(RankedResultPhase.Confirming, tracker.Current.Phase,
                "A success resets the count, so two failures after it are not yet offline.");

            tracker.Tick(8);
            Assert.AreEqual(RankedResultPhase.Offline, tracker.Current.Phase);
            Assert.IsFalse(tracker.IsActive);
        }

        [Test]
        public void OneCallInFlight_AndAStaleAnswerAfterRestartIsDropped()
        {
            var (tracker, service, _) = Make();
            var slow = new TaskCompletionSource<MatchResultView>();
            service.Answers.Enqueue(() => slow.Task);
            tracker.Start("old", 0);
            tracker.Tick(0);
            tracker.Tick(5);
            Assert.AreEqual(1, service.Calls, "No second call while the first is unanswered.");

            tracker.Start("new", 10);
            slow.SetResult(new MatchResultView { state = MatchRecordState.Void });

            Assert.AreEqual(RankedResultPhase.Confirming, tracker.Current.Phase,
                "The old match's answer must not finish the new one.");
            tracker.Tick(10);
            Assert.AreEqual(2, service.Calls);
        }

        [Test]
        public void Stop_IgnoresAnInFlightAnswer()
        {
            var (tracker, service, seen) = Make();
            var slow = new TaskCompletionSource<MatchResultView>();
            service.Answers.Enqueue(() => slow.Task);
            tracker.Start("m1", 0);
            tracker.Tick(0);
            tracker.Stop();
            slow.SetResult(new MatchResultView { state = MatchRecordState.Settled, rrDelta = 10 });

            Assert.AreEqual(1, seen.Count, "Only the Confirming state was published.");
            Assert.IsFalse(tracker.IsActive);
        }
    }
}
