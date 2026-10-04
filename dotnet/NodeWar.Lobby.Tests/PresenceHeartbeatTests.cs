using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class PresenceHeartbeatTests
    {
        private static (PresenceHeartbeat beat, LocalRankedMatchService service) Make()
        {
            var service = new LocalRankedMatchService();
            return (new PresenceHeartbeat(service, "m1"), service);
        }

        private static int Beats(LocalRankedMatchService service) =>
            service.Calls.Count(c => c.Method == nameof(LocalRankedMatchService.PresenceAsync) && !c.Holding);

        [Test]
        public void Beats_OnTheFirstTick_ThenEveryFourSeconds_NotHolding()
        {
            var (beat, service) = Make();
            beat.Tick(0);
            beat.Tick(3.9);
            Assert.AreEqual(1, Beats(service));
            beat.Tick(4);
            Assert.AreEqual(2, Beats(service));
            Assert.IsTrue(service.Calls.All(c => !c.Holding), "A heartbeat never declares a hold.");
        }

        [Test]
        public void ATerminalAnswer_RaisesDecidedOnce_AndStops()
        {
            var (beat, service) = Make();
            var result = new MatchResultView { state = MatchRecordState.Settled, won = true, cause = MatchEndCause.Forfeit };
            service.Presence = new PresenceResult { state = MatchRecordState.Settled, result = result };
            var decided = new List<MatchResultView>();
            beat.Decided += decided.Add;

            beat.Tick(0);
            beat.Tick(10);

            Assert.AreEqual(1, decided.Count);
            Assert.AreSame(result, decided[0]);
            Assert.AreEqual(1, Beats(service));
        }

        [Test]
        public void Stop_SilencesItAndDropsALateAnswer()
        {
            var service = new SlowPresence();
            var beat = new PresenceHeartbeat(service, "m1");
            int decided = 0;
            beat.Decided += _ => decided++;
            beat.Tick(0);
            beat.Stop();
            service.Answer.SetResult(new PresenceResult
            {
                state = MatchRecordState.Void, result = new MatchResultView { state = MatchRecordState.Void }
            });
            beat.Tick(10);

            Assert.AreEqual(0, decided);
            Assert.AreEqual(1, service.Calls);
        }

        [Test]
        public void AFailedBeat_IsIgnored_AndTheNextOneStillGoes()
        {
            var service = new SlowPresence();
            var beat = new PresenceHeartbeat(service, "m1");
            beat.Tick(0);
            service.Answer.SetException(new InvalidOperationException("offline"));
            service.Answer = new TaskCompletionSource<PresenceResult>();
            beat.Tick(4);
            Assert.AreEqual(2, service.Calls);
        }

        [Test]
        public void ProbeNow_CallsAtOnce_IgnoringTheInterval_ButNotWhileOneIsInFlight()
        {
            var service = new SlowPresence();
            var beat = new PresenceHeartbeat(service, "m1");
            beat.Tick(0);
            service.Answer.SetResult(new PresenceResult());
            service.Answer = new TaskCompletionSource<PresenceResult>();
            beat.ProbeNow(0.5);
            Assert.AreEqual(2, service.Calls);
            beat.ProbeNow(0.6);
            Assert.AreEqual(2, service.Calls, "One call at a time.");
        }

        [Test]
        public void ProbeNow_AfterStop_DoesNothing()
        {
            var service = new SlowPresence();
            var beat = new PresenceHeartbeat(service, "m1");
            beat.Stop();
            beat.ProbeNow(0);
            Assert.AreEqual(0, service.Calls);
        }

        [Test]
        public void Urgent_PollsEverySecond()
        {
            var service = new SlowPresence();
            service.Answer.SetResult(new PresenceResult());
            var beat = new PresenceHeartbeat(service, "m1") { Urgent = true };
            beat.Tick(0);
            beat.Tick(0.9);
            Assert.AreEqual(1, service.Calls);
            beat.Tick(1);
            Assert.AreEqual(2, service.Calls);
            beat.Urgent = false;
            beat.Tick(2);
            Assert.AreEqual(2, service.Calls, "Back to the four second interval.");
        }

        [Test]
        public void Unreachable_AfterTwoFailuresInARow_ClearsOnSuccess_AndRaisesOnlyOnFlips()
        {
            var service = new FlakyPresence { Fail = true };
            var beat = new PresenceHeartbeat(service, "m1");
            var flips = new List<bool>();
            beat.ReachabilityChanged += flips.Add;

            beat.ProbeNow(0);
            Assert.IsFalse(beat.Unreachable, "One failure is not enough.");
            beat.ProbeNow(1);
            Assert.IsTrue(beat.Unreachable);
            beat.ProbeNow(2);
            Assert.AreEqual(new[] { true }, flips, "A third failure does not raise again.");

            service.Fail = false;
            beat.ProbeNow(3);
            Assert.IsFalse(beat.Unreachable);
            Assert.AreEqual(new[] { true, false }, flips);
        }

        [Test]
        public void AFailureBetweenSuccesses_NeverMakesItUnreachable()
        {
            var service = new FlakyPresence();
            var beat = new PresenceHeartbeat(service, "m1");
            beat.ProbeNow(0);
            service.Fail = true;
            beat.ProbeNow(1);
            service.Fail = false;
            beat.ProbeNow(2);
            service.Fail = true;
            beat.ProbeNow(3);
            Assert.IsFalse(beat.Unreachable);
        }

        private sealed class FlakyPresence : IRankedMatchService
        {
            public bool Fail;
            public Task<PresenceResult> PresenceAsync(string matchId, bool holding) =>
                Fail ? Task.FromException<PresenceResult>(new InvalidOperationException("offline"))
                     : Task.FromResult(new PresenceResult());
            public Task<MatchResultView> GetResultAsync(string matchId) => throw new NotSupportedException();
            public Task<ResolveHoldResult> ResolveHoldAsync(string matchId) => throw new NotSupportedException();
            public Task<RendezvousResult> RendezvousAsync(string matchId, string joinCode) => throw new NotSupportedException();
            public Task ConfirmConnectedAsync(string matchId) => throw new NotSupportedException();
            public Task<LeaveMatchResult> LeaveAsync(string matchId, bool forfeit) => throw new NotSupportedException();
        }

        private sealed class SlowPresence: IRankedMatchService
        {
            public TaskCompletionSource<PresenceResult> Answer = new TaskCompletionSource<PresenceResult>();
            public int Calls;
            public Task<PresenceResult> PresenceAsync(string matchId, bool holding) { Calls++; return Answer.Task; }
            public Task<MatchResultView> GetResultAsync(string matchId) => throw new NotSupportedException();
            public Task<ResolveHoldResult> ResolveHoldAsync(string matchId) => throw new NotSupportedException();
            public Task<RendezvousResult> RendezvousAsync(string matchId, string joinCode) => throw new NotSupportedException();
            public Task ConfirmConnectedAsync(string matchId) => throw new NotSupportedException();
            public Task<LeaveMatchResult> LeaveAsync(string matchId, bool forfeit) => throw new NotSupportedException();
        }
    }
}
