using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NodeWar.Backend;
using NUnit.Framework;
using Unity.Services.CloudCode.Core;

namespace NodeWar.Cloud.Tests
{
    [NonParallelizable]
    public class MatchRendezvousTests
    {
        private const string MatchId = "rendezvous-test";
        private HookMatchStore matches;
        private FakeSettlementPlayerStore[] players;
        private MatchRendezvous rendezvous;

        [SetUp]
        public void SetUp()
        {
            var states = new[] { MatchRecordTests.Player(), MatchRecordTests.Player() };
            foreach (var state in states) state.ActiveMatch = new ActiveMatchRecord
                { matchId = MatchId, expiresUnixSeconds = 1 + ActiveMatchClaims.LifetimeSeconds };
            players = states.Select(s => new FakeSettlementPlayerStore(s)).ToArray();
            var record = MatchRecords.Create(MatchId, new[] { "p0", "p1" }, states, 1, 1, 1, 1);
            matches = new HookMatchStore(new InMemoryMatchRecordStore(record));
            rendezvous = new MatchRendezvous(matches, id => players[id == "p0" ? 0 : 1],
                new MatchSettler(id => players[id == "p0" ? 0 : 1], new InventoryRules(ServerCatalog.Items)));
        }

        // -- Rendezvous ------------------------------------------------------

        [Test]
        public async Task ReturnsRosterAndSlotForBothPlayers()
        {
            var host = await rendezvous.Rendezvous(MatchId, "p0", null);
            Assert.That(host.state, Is.EqualTo(MatchRecordState.Open));
            Assert.That(host.playerIds, Is.EqualTo(new[] { "p0", "p1" }));
            Assert.That(host.slot, Is.EqualTo(0));
            Assert.That(host.connected, Is.False);

            var guest = await rendezvous.Rendezvous(MatchId, "p1", null);
            Assert.That(guest.slot, Is.EqualTo(1));
            Assert.That(guest.playerIds, Is.EqualTo(new[] { "p0", "p1" }));
        }

        [Test]
        public async Task SlotZeroPublishesJoinCodeAndSlotOnePicksItUp()
        {
            var host = await rendezvous.Rendezvous(MatchId, "p0", "ABC123");
            Assert.That(host.joinCode, Is.EqualTo("ABC123"));

            var guest = await rendezvous.Rendezvous(MatchId, "p1", null);
            Assert.That(guest.joinCode, Is.EqualTo("ABC123"));
        }

        [Test]
        public async Task SlotOneJoinCodeIsIgnoredNotAnError()
        {
            var result = await rendezvous.Rendezvous(MatchId, "p1", "should-be-ignored");
            Assert.That(result.message, Is.Null);
            Assert.That(result.joinCode, Is.Null);

            var record = await Record();
            Assert.That(record.joinCode, Is.Null);
        }

        [Test]
        public async Task NonMemberIsRefused()
        {
            var result = await rendezvous.Rendezvous(MatchId, "stranger", null);
            Assert.That(result.state, Is.Null);
            Assert.That(result.slot, Is.EqualTo(-1));
            Assert.That(result.message, Does.Contain("not in"));
        }

        [Test]
        public async Task MissingMatchIsRefused()
        {
            var result = await rendezvous.Rendezvous("unknown", "p0", null);
            Assert.That(result.state, Is.Null);
            Assert.That(result.message, Does.Contain("not found"));
        }

        [Test]
        public async Task PublishIsRefusedOnceConnected()
        {
            await Mutate(r => r.connectedUnixSeconds = 50);
            var result = await rendezvous.Rendezvous(MatchId, "p0", "TOO-LATE");
            Assert.That(result.joinCode, Is.Null);
            Assert.That(result.connected, Is.True);
            Assert.That((await Record()).joinCode, Is.Null);
        }

        [Test]
        public async Task JoinCodeOverThirtyTwoCharsIsRefused()
        {
            string tooLong = new string('a', 33);
            var result = await rendezvous.Rendezvous(MatchId, "p0", tooLong);
            Assert.That(result.state, Is.Null);
            Assert.That(result.message, Does.Contain("too long"));
            Assert.That((await Record()).joinCode, Is.Null);
        }

        // -- ConfirmConnected --------------------------------------------------

        [Test]
        public async Task ConfirmConnectedSetsOnceAndIsIdempotent()
        {
            await rendezvous.ConfirmConnected(MatchId, "p1", 500);
            Assert.That((await Record()).connectedUnixSeconds, Is.EqualTo(500));

            await rendezvous.ConfirmConnected(MatchId, "p0", 999);
            Assert.That((await Record()).connectedUnixSeconds, Is.EqualTo(500));
        }

        // -- Leave -------------------------------------------------------------

        [Test]
        public async Task LeaveOfMissingMatchReleasesCallersDanglingClaim()
        {
            players[0].Mutate(s => s.ActiveMatch = new ActiveMatchRecord { matchId = "gone", expiresUnixSeconds = 99999 });
            var result = await rendezvous.Leave("gone", "p0", false, 10);
            Assert.That(result.outcome, Is.EqualTo(LeaveOutcome.Cleared));
            Assert.That((await State(0)).ActiveMatch.matchId, Is.Null);
        }

        [Test]
        public async Task LeaveByNonMemberIsRefused()
        {
            var result = await rendezvous.Leave(MatchId, "stranger", false, 10);
            Assert.That(result.outcome, Is.Null);
            Assert.That(result.message, Does.Contain("not in"));
        }

        [Test]
        public async Task LeaveOfTerminalMatchClearsAndReleasesBothClaims()
        {
            await Mutate(r => r.state = MatchRecordState.Settled);
            var result = await rendezvous.Leave(MatchId, "p0", false, 10);
            Assert.That(result.outcome, Is.EqualTo(LeaveOutcome.Cleared));
            Assert.That((await State(0)).ActiveMatch.matchId, Is.Null);
            Assert.That((await State(1)).ActiveMatch.matchId, Is.Null);
        }

        [Test]
        public async Task LeaveVoidsAnExpiredLease()
        {
            var result = await rendezvous.Leave(MatchId, "p0", false, 1 + ActiveMatchClaims.LifetimeSeconds);
            Assert.That(result.outcome, Is.EqualTo(LeaveOutcome.Cleared));
            Assert.That((await Record()).state, Is.EqualTo(MatchRecordState.Void));
            Assert.That((await State(0)).ActiveMatch.matchId, Is.Null);
            Assert.That((await State(1)).ActiveMatch.matchId, Is.Null);
        }

        [Test]
        public async Task LeaveVoidsWhenAClaimNoLongerNamesTheMatch()
        {
            players[1].Mutate(s => s.ActiveMatch = new ActiveMatchRecord { matchId = "other", expiresUnixSeconds = 99999 });
            var result = await rendezvous.Leave(MatchId, "p0", false, 10);
            Assert.That(result.outcome, Is.EqualTo(LeaveOutcome.Cleared));
            Assert.That((await Record()).state, Is.EqualTo(MatchRecordState.Void));
        }

        [Test]
        public async Task LeaveVoidsAfterPendingTimeout()
        {
            await Mutate(r =>
            {
                r.connectedUnixSeconds = 50;
                r.state = MatchRecordState.Pending;
                r.pendingUnixSeconds = 100;
            });
            var result = await rendezvous.Leave(MatchId, "p0", false, 100 + ActiveMatchClaims.PendingTimeoutSeconds + 1);
            Assert.That(result.outcome, Is.EqualTo(LeaveOutcome.Cleared));
            Assert.That((await Record()).state, Is.EqualTo(MatchRecordState.Void));
        }

        [Test]
        public async Task LeaveBeforeConnectingVoidsRatherThanForfeits()
        {
            var result = await rendezvous.Leave(MatchId, "p0", true, 10);
            Assert.That(result.outcome, Is.EqualTo(LeaveOutcome.Cleared));
            Assert.That((await Record()).state, Is.EqualTo(MatchRecordState.Void));
            AssertNoPlayerWrites();
        }

        [Test]
        public async Task LeaveWithAnAcceptedReportWaitsOutThePendingTimeout()
        {
            await Mutate(r =>
            {
                r.connectedUnixSeconds = 50;
                r.state = MatchRecordState.Pending;
                r.pendingUnixSeconds = 100;
                r.reports.Add(new MatchReport { playerIndex = 0, accepted = true, winner = 0 });
            });
            var result = await rendezvous.Leave(MatchId, "p0", false, 250);
            Assert.That(result.outcome, Is.EqualTo(LeaveOutcome.Waiting));
            Assert.That(result.secondsLeft, Is.EqualTo(ActiveMatchClaims.PendingTimeoutSeconds - 150));
        }

        [Test]
        public async Task LeaveWithAnAcceptedReportOnAStillOpenRecordUsesTheFullTimeout()
        {
            await Mutate(r =>
            {
                r.connectedUnixSeconds = 50;
                r.reports.Add(new MatchReport { playerIndex = 0, accepted = true, winner = 0 });
            });
            var result = await rendezvous.Leave(MatchId, "p0", false, 999);
            Assert.That(result.outcome, Is.EqualTo(LeaveOutcome.Waiting));
            Assert.That(result.secondsLeft, Is.EqualTo(ActiveMatchClaims.PendingTimeoutSeconds));
        }

        [Test]
        public async Task LeaveWithoutForfeitNeedsForfeit()
        {
            await Mutate(r => r.connectedUnixSeconds = 50);
            var result = await rendezvous.Leave(MatchId, "p0", false, 100);
            Assert.That(result.outcome, Is.EqualTo(LeaveOutcome.NeedsForfeit));
            AssertNoPlayerWrites();
        }

        [Test]
        public async Task ForfeitSettlesCallerAsLoserAndReleasesBothClaims()
        {
            await Mutate(r => r.connectedUnixSeconds = 50);
            var result = await rendezvous.Leave(MatchId, "p0", true, 200);
            Assert.That(result.outcome, Is.EqualTo(LeaveOutcome.Cleared));

            var loser = await State(0);
            var winner = await State(1);
            Assert.That(loser.Rating.R, Is.LessThan(1500));
            Assert.That(winner.Rating.R, Is.GreaterThan(1500));

            var record = await Record();
            Assert.That(record.state, Is.EqualTo(MatchRecordState.Settled));
            Assert.That(record.outcomes[0].won, Is.False);
            Assert.That(record.outcomes[1].won, Is.True);

            Assert.That(result.playerState.Rating.R, Is.EqualTo(loser.Rating.R));
            Assert.That(loser.ActiveMatch.matchId, Is.Null);
            Assert.That(winner.ActiveMatch.matchId, Is.Null);
        }

        [Test]
        public async Task ConcurrentDuplicateForfeitCallsSettleExactlyOnce()
        {
            await Mutate(r => r.connectedUnixSeconds = 50);
            matches.BeforeWrite = async (_, __) =>
            {
                matches.BeforeWrite = null;
                var duplicate = await rendezvous.Leave(MatchId, "p0", true, 200);
                Assert.That(duplicate.outcome, Is.EqualTo(LeaveOutcome.Cleared));
            };
            var first = await rendezvous.Leave(MatchId, "p0", true, 200);
            Assert.That(first.outcome, Is.EqualTo(LeaveOutcome.Cleared));

            Assert.That((await Record()).state, Is.EqualTo(MatchRecordState.Settled));
            Assert.That((await Record()).outcomes[1].won, Is.True);
            Assert.That(players.Select(p => p.WriteCount), Is.EqualTo(new[] { 1, 1 }));
            Assert.That((await State(0)).ActiveMatch.matchId, Is.Null);
            Assert.That((await State(1)).ActiveMatch.matchId, Is.Null);
        }

        private async Task Mutate(Action<MatchRecord> mutate)
        {
            var read = await matches.ReadAsync(MatchId);
            mutate(read.Record);
            await matches.WriteAsync(read.Record, read.WriteLock);
        }

        private async Task<PlayerState> State(int player) => (await players[player].ReadForSettlementAsync()).State;
        private async Task<MatchRecord> Record() => (await matches.ReadAsync(MatchId)).Record;
        private void AssertNoPlayerWrites() => Assert.That(players.Select(p => p.WriteCount), Is.EqualTo(new[] { 0, 0 }));

        private sealed class HookMatchStore : IMatchRecordStore
        {
            public readonly InMemoryMatchRecordStore Inner;
            public Func<MatchRecord, string, Task> BeforeWrite;
            public HookMatchStore(InMemoryMatchRecordStore inner) { Inner = inner; }
            public Task<LockedMatchRecord> ReadAsync(string id) => Inner.ReadAsync(id);
            public async Task WriteAsync(MatchRecord record, string token)
            {
                if (BeforeWrite != null) await BeforeWrite(record, token);
                await Inner.WriteAsync(record, token);
            }
            public Task SaveLog(string id, int player, string log) => Inner.SaveLog(id, player, log);
        }

        private sealed class FakeSettlementPlayerStore : ISettlementPlayerStore
        {
            private PlayerState state;
            private int version = 1;
            public int WriteCount;
            public FakeSettlementPlayerStore(PlayerState state) { this.state = Clone(state); }
            public Task<LockedPlayerState> ReadForSettlementAsync() => Task.FromResult(new LockedPlayerState(Clone(state),
                PlayerStateKeys.All.ToDictionary(k => k, _ => version.ToString())));
            public Task WriteActiveMatchAsync(ActiveMatchRecord claim, LockedPlayerState read)
            {
                if (read.WriteLocks[PlayerStateKeys.Rating] != version.ToString())
                    throw new RecordConflictException("Claim changed.");
                state.ActiveMatch = claim;
                version++;
                return Task.CompletedTask;
            }
            public Task WriteForSettlementAsync(PlayerState value, IReadOnlyDictionary<string, string> tokens)
            {
                if (PlayerStateKeys.All.Any(k => !tokens.TryGetValue(k, out string token) || token != version.ToString()))
                    throw new RecordConflictException("Player records changed.");
                state = Clone(value);
                version++;
                WriteCount++;
                return Task.CompletedTask;
            }
            public void Mutate(Action<PlayerState> action) { action(state); version++; }
            private static PlayerState Clone(PlayerState value) => JsonConvert.DeserializeObject<PlayerState>(JsonConvert.SerializeObject(value));
        }
    }

    /// <summary>
    /// The identity refusal RankedMatchModule applies before touching any
    /// store: a service call (no player identity) has no roster entry to be.
    /// The module itself needs a real IGameApiClient to construct, so this
    /// exercises the pure predicate it guards every function with.
    /// </summary>
    public class RankedMatchModuleIdentityTests
    {
        [TestCase(null)]
        [TestCase("")]
        public void ServiceCallsHaveNoIdentity(string playerId)
        {
            Assert.That(RankedMatchModule.HasNoIdentity(new FakeExecutionContext(playerId)), Is.True);
        }

        [Test]
        public void PlayerCallsHaveAnIdentity()
        {
            Assert.That(RankedMatchModule.HasNoIdentity(new FakeExecutionContext("p0")), Is.False);
        }

        [Test]
        public void NullContextHasNoIdentity()
        {
            Assert.That(RankedMatchModule.HasNoIdentity(null), Is.True);
        }

        private sealed class FakeExecutionContext : IExecutionContext
        {
            public FakeExecutionContext(string playerId) { PlayerId = playerId; }
            public string ProjectId => "project";
            public string PlayerId { get; }
            public string EnvironmentId => "env";
            public string EnvironmentName => "development";
            public string AccessToken => "access";
            public string UserId => "user";
            public string Issuer => "issuer";
            public string ServiceToken => "service";
            public string AnalyticsUserId => "analytics";
            public string UnityInstallationId => "install";
            public string CorrelationId => "correlation";
            public string ScopeId => "scope";
        }
    }
}
