using System.Linq;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    /// <summary>
    /// Scripted stand-in for the handshake MatchLauncher will eventually drive.
    /// The rendezvous only reacts to Phase/JoinCode/FailureMessage and records
    /// what it was asked to do, so tests can script the connection independently
    /// of the server's LocalRankedMatchService script.
    /// </summary>
    internal sealed class FakeRankedConnection : IRankedConnection
    {
        public RankedConnectionPhase Phase { get; set; } = RankedConnectionPhase.Idle;
        public string JoinCode { get; set; }
        public string FailureMessage { get; set; } = "";

        public int HostCalls;
        public int JoinCalls;
        public int CancelCalls;
        public string HostedMatchId;
        public string[] HostedPlayerIds;
        public string JoinedMatchId;
        public string[] JoinedPlayerIds;
        public string JoinedCode;

        public void Host(string matchId, string[] playerIds)
        {
            HostCalls++;
            HostedMatchId = matchId;
            HostedPlayerIds = playerIds;
            Phase = RankedConnectionPhase.CreatingRoom;
        }

        public void Join(string matchId, string[] playerIds, string joinCode)
        {
            JoinCalls++;
            JoinedMatchId = matchId;
            JoinedPlayerIds = playerIds;
            JoinedCode = joinCode;
            Phase = RankedConnectionPhase.Connecting;
        }

        public void Cancel() => CancelCalls++;
    }

    public class RankedRendezvousTests
    {
        private static RendezvousResult OpenRoster(int slot) => new RendezvousResult
        {
            state = MatchRecordState.Open,
            playerIds = new[] { "playerA", "playerB" },
            slot = slot
        };

        [Test]
        public void Host_PublishesCodeAndConfirmsOnConnect()
        {
            var service = new LocalRankedMatchService();
            service.RendezvousResults.Add(new RendezvousResult { state = MatchRecordState.Open });
            var connection = new FakeRankedConnection();
            var rendezvous = new RankedRendezvous(service, connection, "match-1", OpenRoster(0));

            bool connected = false;
            rendezvous.Connected += () => connected = true;

            rendezvous.Start(0);
            Assert.That(connection.HostCalls, Is.EqualTo(1));
            Assert.That(connection.HostedMatchId, Is.EqualTo("match-1"));

            // No code yet: nothing published.
            rendezvous.Tick(0.5);
            Assert.That(service.Calls.Count, Is.EqualTo(0));

            connection.JoinCode = "ABC123";
            rendezvous.Tick(1.0);

            Assert.That(service.Calls.Count, Is.EqualTo(1));
            Assert.That(service.Calls[0].Method, Is.EqualTo(nameof(LocalRankedMatchService.RendezvousAsync)));
            Assert.That(service.Calls[0].JoinCode, Is.EqualTo("ABC123"));

            connection.Phase = RankedConnectionPhase.Connected;
            rendezvous.Tick(2.0);

            Assert.That(connected, Is.True);
            Assert.That(service.Calls.Count, Is.EqualTo(2));
            Assert.That(service.Calls[1].Method, Is.EqualTo(nameof(LocalRankedMatchService.ConfirmConnectedAsync)));
            Assert.That(rendezvous.IsActive, Is.False);
        }

        [Test]
        public void Host_RetriesPublishOnException()
        {
            var service = new LocalRankedMatchService(); // empty script -> RendezvousAsync returns a null-state (terminal) result...
            var connection = new FakeRankedConnection();
            var rendezvous = new RankedRendezvous(service, connection, "match-1", OpenRoster(0));
            string failure = null;
            rendezvous.Failed += r => failure = r;

            rendezvous.Start(0);
            connection.JoinCode = "CODE";
            rendezvous.Tick(0.1);

            // LocalRankedMatchService's empty script returns a message-only,
            // null-state result, which the rendezvous treats as a bad record.
            Assert.That(failure, Is.Not.Null);
        }

        [Test]
        public void Guest_PollsThenJoinsThenConfirms()
        {
            var service = new LocalRankedMatchService();
            service.RendezvousResults.Add(new RendezvousResult { state = MatchRecordState.Open, joinCode = null });
            service.RendezvousResults.Add(new RendezvousResult { state = MatchRecordState.Open, joinCode = "XYZ" });
            var connection = new FakeRankedConnection();
            var rendezvous = new RankedRendezvous(service, connection, "match-1", OpenRoster(1));

            bool connected = false;
            rendezvous.Connected += () => connected = true;

            rendezvous.Start(0);

            rendezvous.Tick(2.0); // first poll: no code yet
            Assert.That(service.Calls.Count, Is.EqualTo(1));
            Assert.That(connection.JoinCalls, Is.EqualTo(0));

            rendezvous.Tick(4.0); // second poll: code arrives
            Assert.That(service.Calls.Count, Is.EqualTo(2));
            Assert.That(connection.JoinCalls, Is.EqualTo(0));

            rendezvous.Tick(4.1); // acts on the discovered code
            Assert.That(connection.JoinCalls, Is.EqualTo(1));
            Assert.That(connection.JoinedMatchId, Is.EqualTo("match-1"));
            Assert.That(connection.JoinedCode, Is.EqualTo("XYZ"));

            connection.Phase = RankedConnectionPhase.Connected;
            rendezvous.Tick(5.0);

            Assert.That(connected, Is.True);
            Assert.That(service.Calls.Count, Is.EqualTo(3));
            Assert.That(service.Calls[2].Method, Is.EqualTo(nameof(LocalRankedMatchService.ConfirmConnectedAsync)));
        }

        [Test]
        public void Host_TimesOutIfNeverConnectsAfterPublish()
        {
            var service = new LocalRankedMatchService();
            service.RendezvousResults.Add(new RendezvousResult { state = MatchRecordState.Open });
            var connection = new FakeRankedConnection();
            var rendezvous = new RankedRendezvous(service, connection, "match-1", OpenRoster(0));
            string failure = null;
            rendezvous.Failed += r => failure = r;

            rendezvous.Start(0);
            connection.JoinCode = "CODE";
            rendezvous.Tick(1.0);   // publish confirmed
            rendezvous.Tick(1.1);   // deadline clock starts here (publishSucceededAtSeconds = 1.1)
            rendezvous.Tick(1.1 + 22.0 + 0.1); // never connected

            Assert.That(failure, Is.Not.Null);
            Assert.That(connection.CancelCalls, Is.EqualTo(1));
            Assert.That(service.Calls.Any(c => c.Method == nameof(LocalRankedMatchService.LeaveAsync) && !c.Forfeit), Is.True);
        }

        [Test]
        public void Guest_TimesOutWaitingForCode()
        {
            var service = new LocalRankedMatchService();
            service.RendezvousResults.Add(new RendezvousResult { state = MatchRecordState.Open, joinCode = null });
            var connection = new FakeRankedConnection();
            var rendezvous = new RankedRendezvous(service, connection, "match-1", OpenRoster(1));
            string failure = null;
            rendezvous.Failed += r => failure = r;

            rendezvous.Start(0);
            rendezvous.Tick(16.0); // past the 15s code-wait deadline

            Assert.That(failure, Is.Not.Null);
            Assert.That(connection.CancelCalls, Is.EqualTo(1));
            Assert.That(connection.JoinCalls, Is.EqualTo(0));
            Assert.That(service.Calls.Any(c => c.Method == nameof(LocalRankedMatchService.LeaveAsync)), Is.True);
        }

        [Test]
        public void Guest_TimesOutAfterJoinIfNeverConnects()
        {
            var service = new LocalRankedMatchService();
            service.RendezvousResults.Add(new RendezvousResult { state = MatchRecordState.Open, joinCode = "CODE" });
            var connection = new FakeRankedConnection();
            var rendezvous = new RankedRendezvous(service, connection, "match-1", OpenRoster(1));
            string failure = null;
            rendezvous.Failed += r => failure = r;

            rendezvous.Start(0);
            rendezvous.Tick(2.0); // discovers the code
            rendezvous.Tick(2.1); // joins; joinedAtSeconds = 2.1
            Assert.That(connection.JoinCalls, Is.EqualTo(1));

            rendezvous.Tick(2.1 + 27.0 + 0.1); // still not connected

            Assert.That(failure, Is.Not.Null);
            Assert.That(connection.CancelCalls, Is.EqualTo(1));
        }

        [Test]
        public void TerminalRecord_FailsMidPoll()
        {
            var service = new LocalRankedMatchService();
            service.RendezvousResults.Add(new RendezvousResult { state = MatchRecordState.Settled, message = "already settled" });
            var connection = new FakeRankedConnection();
            var rendezvous = new RankedRendezvous(service, connection, "match-1", OpenRoster(1));
            string failure = null;
            rendezvous.Failed += r => failure = r;

            rendezvous.Start(0);
            rendezvous.Tick(2.0);

            Assert.That(failure, Does.Contain("already settled"));
            Assert.That(connection.CancelCalls, Is.EqualTo(1));
            Assert.That(service.Calls.Any(c => c.Method == nameof(LocalRankedMatchService.LeaveAsync)), Is.True);
        }

        [Test]
        public void Start_TerminalRoster_FailsImmediately()
        {
            var service = new LocalRankedMatchService();
            var connection = new FakeRankedConnection();
            var roster = new RendezvousResult { state = MatchRecordState.Void, playerIds = new[] { "a", "b" }, slot = 0 };
            var rendezvous = new RankedRendezvous(service, connection, "match-1", roster);
            string failure = null;
            rendezvous.Failed += r => failure = r;

            rendezvous.Start(0);

            Assert.That(failure, Is.Not.Null);
            Assert.That(connection.HostCalls, Is.EqualTo(0));
        }

        [Test]
        public void ConnectionFailed_Fails()
        {
            var service = new LocalRankedMatchService();
            var connection = new FakeRankedConnection { FailureMessage = "relay dead" };
            var rendezvous = new RankedRendezvous(service, connection, "match-1", OpenRoster(0));
            string failure = null;
            rendezvous.Failed += r => failure = r;

            rendezvous.Start(0);
            connection.Phase = RankedConnectionPhase.Failed;
            rendezvous.Tick(1.0);

            Assert.That(failure, Is.EqualTo("relay dead"));
            Assert.That(connection.CancelCalls, Is.EqualTo(1));
        }

        [Test]
        public void Cancel_TearsDownSilently()
        {
            var service = new LocalRankedMatchService();
            var connection = new FakeRankedConnection();
            var rendezvous = new RankedRendezvous(service, connection, "match-1", OpenRoster(0));
            bool failedRaised = false;
            bool connectedRaised = false;
            rendezvous.Failed += _ => failedRaised = true;
            rendezvous.Connected += () => connectedRaised = true;

            rendezvous.Start(0);
            rendezvous.Cancel();

            Assert.That(connection.CancelCalls, Is.EqualTo(1));
            Assert.That(failedRaised, Is.False);
            Assert.That(connectedRaised, Is.False);
            Assert.That(rendezvous.IsActive, Is.False);
            Assert.That(service.Calls.Any(c => c.Method == nameof(LocalRankedMatchService.LeaveAsync)), Is.True);

            // Idempotent: further ticks and a second Cancel do nothing more.
            rendezvous.Tick(100.0);
            rendezvous.Cancel();
            Assert.That(connection.CancelCalls, Is.EqualTo(1));
        }
    }
}
