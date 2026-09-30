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

        private sealed class SlowPresence : IRankedMatchService
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
