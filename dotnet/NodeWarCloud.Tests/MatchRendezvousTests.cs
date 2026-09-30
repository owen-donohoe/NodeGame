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

        [TestCase(0)]
        [TestCase(1)]
        public async Task OneConfirmationStillAllowsOpponentToLeaveWithoutForfeit(int slot)
        {
            await rendezvous.ConfirmConnected(MatchId, "p" + slot, 50);
            Assert.That((await Record()).confirmedUnixSeconds[slot], Is.EqualTo(50));
            Assert.That((await Record()).confirmedUnixSeconds[1 - slot], Is.Zero);
            Assert.That((await Record()).connectedUnixSeconds, Is.Zero);
            var published = await rendezvous.Rendezvous(MatchId, "p0", "STILL-ALLOWED");
            Assert.That(published.connected, Is.False);
            Assert.That(published.joinCode, Is.EqualTo("STILL-ALLOWED"));

            var result = await rendezvous.Leave(MatchId, "p" + (1 - slot), false, 100);
            Assert.That(result.outcome, Is.EqualTo(LeaveOutcome.Cleared));
            Assert.That((await Record()).state, Is.EqualTo(MatchRecordState.Void));
            Assert.That((await State(0)).ActiveMatch.matchId, Is.Null);
            Assert.That((await State(1)).ActiveMatch.matchId, Is.Null);
            AssertNoPlayerWrites();
        }

        [TestCase(500, 600)]
        [TestCase(600, 500)]
        public async Task ConfirmConnectedSetsBothOnceAndUsesLaterTimestamp(long first, long second)
        {
            await rendezvous.ConfirmConnected(MatchId, "p1", first);
            var firstLock = (await matches.ReadAsync(MatchId)).WriteLock;
            await rendezvous.ConfirmConnected(MatchId, "p1", 999);
            Assert.That((await matches.ReadAsync(MatchId)).WriteLock, Is.EqualTo(firstLock));
            Assert.That((await Record()).connectedUnixSeconds, Is.Zero);

            await rendezvous.ConfirmConnected(MatchId, "p0", second);
            var connectedLock = (await matches.ReadAsync(MatchId)).WriteLock;
            await rendezvous.ConfirmConnected(MatchId, "p0", 1000);
            await rendezvous.ConfirmConnected(MatchId, "p1", 1001);
            Assert.That((await matches.ReadAsync(MatchId)).WriteLock, Is.EqualTo(connectedLock));
            Assert.That((await Record()).confirmedUnixSeconds, Is.EqualTo(new[] { second, first }));
            Assert.That((await Record()).connectedUnixSeconds, Is.EqualTo(Math.Max(first, second)));
            Assert.That((await rendezvous.Rendezvous(MatchId, "p0", "TOO-LATE")).joinCode, Is.Null);
            Assert.That((await rendezvous.Rendezvous(MatchId, "p1", null)).connected, Is.True);
            Assert.That((await rendezvous.Leave(MatchId, "p0", false, 1100)).outcome,
                Is.EqualTo(LeaveOutcome.NeedsForfeit));
        }

        [Test]
        public async Task ConfirmConnectedTreatsLegacyNullConfirmationsAsUnconfirmed()
        {
            await Mutate(r => r.confirmedUnixSeconds = null);
            await rendezvous.ConfirmConnected(MatchId, "p0", 50);
            Assert.That((await Record()).confirmedUnixSeconds, Is.EqualTo(new long[] { 50, 0 }));
            Assert.That((await Record()).connectedUnixSeconds, Is.Zero);
            await rendezvous.ConfirmConnected(MatchId, "p1", 60);
            Assert.That((await Record()).confirmedUnixSeconds, Is.EqualTo(new long[] { 50, 60 }));
            Assert.That((await Record()).connectedUnixSeconds, Is.EqualTo(60));
        }

        [Test]
        public async Task ConcurrentConfirmationsPreserveBothSlots()
        {
            matches.BeforeWrite = async (_, __) =>
            {
                matches.BeforeWrite = null;
                await rendezvous.ConfirmConnected(MatchId, "p1", 60);
            };
            await rendezvous.ConfirmConnected(MatchId, "p0", 50);
            Assert.That((await Record()).confirmedUnixSeconds, Is.EqualTo(new long[] { 50, 60 }));
            Assert.That((await Record()).connectedUnixSeconds, Is.EqualTo(60));
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

        [TestCase(0)]
        [TestCase(50)]
        public async Task LeaveWithAnAcceptedReportWaitsOutThePendingTimeout(long connected)
        {
            await Mutate(r =>
            {
                r.connectedUnixSeconds = connected;
                r.state = MatchRecordState.Pending;
                r.pendingUnixSeconds = 100;
                r.reports.Add(new MatchReport { playerIndex = 0, accepted = true, winner = 0 });
            });
            var result = await rendezvous.Leave(MatchId, "p0", false, 250);
            Assert.That(result.outcome, Is.EqualTo(LeaveOutcome.Waiting));
            Assert.That(result.secondsLeft, Is.EqualTo(ActiveMatchClaims.PendingTimeoutSeconds - 150));
            Assert.That((await Record()).state, Is.EqualTo(MatchRecordState.Pending));
        }

        [TestCase(0)]
        [TestCase(50)]
        public async Task LeaveWithAnAcceptedReportOnAStillOpenRecordUsesTheFullTimeout(long connected)
        {
            await Mutate(r =>
            {
                r.connectedUnixSeconds = connected;
                r.reports.Add(new MatchReport { playerIndex = 0, accepted = true, winner = 0 });
            });
            var result = await rendezvous.Leave(MatchId, "p0", false, 999);
            Assert.That(result.outcome, Is.EqualTo(LeaveOutcome.Waiting));
            Assert.That(result.secondsLeft, Is.EqualTo(ActiveMatchClaims.PendingTimeoutSeconds));
        }

        [TestCase(false, LeaveOutcome.Cleared, MatchRecordState.Void)]
        [TestCase(true, LeaveOutcome.NeedsForfeit, MatchRecordState.Pending)]
        public async Task OnlyAnAcceptedOpponentReportProvesAnUnconfirmedMatchWasPlayed(
            bool accepted, LeaveOutcome expectedOutcome, MatchRecordState expectedState)
        {
            await Mutate(r =>
            {
                r.state = MatchRecordState.Pending;
                r.pendingUnixSeconds = 100;
                r.reports.Add(new MatchReport { playerIndex = 1, accepted = accepted, winner = 1 });
            });
            var result = await rendezvous.Leave(MatchId, "p0", false, 250);
            Assert.That(result.outcome, Is.EqualTo(expectedOutcome));
            Assert.That((await Record()).state, Is.EqualTo(expectedState));
            AssertNoPlayerWrites();
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

        [Test]
        public async Task OpposingConcurrentForfeitsSettleOneWinnerEverywhere()
        {
            await Mutate(r => r.connectedUnixSeconds = 50);
            matches.BeforeWrite = async (_, __) =>
            {
                matches.BeforeWrite = null;
                var other = await rendezvous.Leave(MatchId, "p1", true, 200);
                Assert.That(other.outcome, Is.EqualTo(LeaveOutcome.Cleared));
            };
            var first = await rendezvous.Leave(MatchId, "p0", true, 200);
            Assert.That(first.outcome, Is.EqualTo(LeaveOutcome.Cleared));

            // p1's forfeit committed first, so p0 won: in the record and in both players.
            var record = await Record();
            Assert.That(record.forfeitedBy, Is.EqualTo(1));
            Assert.That(record.outcomes[0].won, Is.True);
            Assert.That(record.outcomes[1].won, Is.False);
            Assert.That((await State(0)).Rating.R, Is.GreaterThan(1500));
            Assert.That((await State(1)).Rating.R, Is.LessThan(1500));
            Assert.That(players.Select(p => p.WriteCount), Is.EqualTo(new[] { 1, 1 }));
        }

        [Test]
        public async Task ACommittedForfeitIsFinishedByTheOtherPlayersLeave()
        {
            await Mutate(r => { r.connectedUnixSeconds = 50; r.forfeitedBy = 0; r.settlementUnixSeconds = 150; });
            var result = await rendezvous.Leave(MatchId, "p1", false, 200);
            Assert.That(result.outcome, Is.EqualTo(LeaveOutcome.Cleared));
            var record = await Record();
            Assert.That(record.state, Is.EqualTo(MatchRecordState.Settled));
            Assert.That(record.outcomes[1].won, Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        public async Task LeaveFinishesCommittedAgreementAfterPendingTimeout(int winner)
        {
            await CommitAgreement(winner);
            Assert.That((await Record()).outcomes, Is.Null);
            AssertNoPlayerWrites();

            var result = await rendezvous.Leave(MatchId, "p0", false,
                100 + ActiveMatchClaims.PendingTimeoutSeconds + 1);
            var record = await Record();
            Assert.That(result.outcome, Is.EqualTo(LeaveOutcome.Cleared));
            Assert.That(record.state, Is.EqualTo(MatchRecordState.Settled));
            Assert.That(record.settlementUnixSeconds, Is.EqualTo(150));
            Assert.That((await State(winner)).Rating.R, Is.GreaterThan(1500));
            Assert.That((await State(1 - winner)).Rating.R, Is.LessThan(1500));
            for (int slot = 0; slot < 2; slot++)
            {
                var state = await State(slot);
                Assert.That(record.outcomes[slot].won, Is.EqualTo(slot == winner));
                Assert.That(record.outcomes[slot].rrAfter, Is.EqualTo(state.Rank.RR));
                Assert.That(state.Rank.RR,
                    Is.EqualTo(Math.Max(0, record.players[slot].Rank.RR + record.outcomes[slot].rrDelta)));
                Assert.That(record.outcomes[slot].arenaAfter, Is.EqualTo(state.Rank.Arena));
                Assert.That(state.Rating.LastMatchUnixSeconds, Is.EqualTo(150));
                Assert.That(state.Rating.SettledMatchIds, Is.EqualTo(new[] { MatchId }));
                Assert.That(state.History.MatchIds, Is.EqualTo(new[] { MatchId }));
                Assert.That(state.ActiveMatch.matchId, Is.Null);
            }
            Assert.That(result.playerState.Rating.R, Is.EqualTo((await State(0)).Rating.R));
            await rendezvous.Leave(MatchId, "p1", false, 800);
            Assert.That(players.Select(p => p.WriteCount), Is.EqualTo(new[] { 1, 1 }));
        }

        [Test]
        public async Task CommittedForfeitOutranksAgreementOnLeave()
        {
            await CommitAgreement(0);
            await Mutate(r => r.forfeitedBy = 0);
            var result = await rendezvous.Leave(MatchId, "p1", false, 800);
            Assert.That(result.outcome, Is.EqualTo(LeaveOutcome.Cleared));
            Assert.That((await Record()).state, Is.EqualTo(MatchRecordState.Settled));
            Assert.That((await Record()).outcomes[1].won, Is.True);
            Assert.That((await State(1)).Rating.R, Is.GreaterThan(1500));
            Assert.That((await State(0)).Rating.R, Is.LessThan(1500));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task LeaveVoidsAgreementWhenLeaseOrClaimsAreLost(bool lostClaim)
        {
            await CommitAgreement(0);
            if (lostClaim)
                players[1].Mutate(s => s.ActiveMatch = new ActiveMatchRecord
                    { matchId = "other", expiresUnixSeconds = 99999 });
            var result = await rendezvous.Leave(MatchId, "p0", false,
                lostClaim ? 800 : 1 + ActiveMatchClaims.LifetimeSeconds);
            Assert.That(result.outcome, Is.EqualTo(LeaveOutcome.Cleared));
            Assert.That((await Record()).state, Is.EqualTo(MatchRecordState.Void));
            Assert.That((await Record()).outcomes, Is.Null);
            Assert.That((await State(0)).ActiveMatch.matchId, Is.Null);
            Assert.That((await State(1)).ActiveMatch.matchId, Is.EqualTo(lostClaim ? "other" : null));
            AssertNoPlayerWrites();
        }

        [Test]
        public async Task LeaveClearsOutcomesWhenAgreementSettlementLosesAClaim()
        {
            await CommitAgreement(0);
            players[0].BeforeWrite = () =>
            {
                players[0].BeforeWrite = null;
                players[0].Mutate(s => s.ActiveMatch = new ActiveMatchRecord
                    { matchId = "other", expiresUnixSeconds = 99999 });
            };
            var result = await rendezvous.Leave(MatchId, "p0", false, 800);
            Assert.That(result.outcome, Is.EqualTo(LeaveOutcome.Cleared));
            Assert.That(result.playerState, Is.Null);
            Assert.That((await Record()).state, Is.EqualTo(MatchRecordState.Void));
            Assert.That((await Record()).outcomes, Is.Null);
            Assert.That((await State(0)).ActiveMatch.matchId, Is.EqualTo("other"));
            Assert.That((await State(1)).ActiveMatch.matchId, Is.Null);
            AssertNoPlayerWrites();
        }

        private Task CommitAgreement(int winner) => Mutate(r =>
        {
            // ReportMatch committed both reports and their timestamp, then died
            // before settling either player. Connection confirmations may be absent.
            r.state = MatchRecordState.Pending;
            r.pendingUnixSeconds = 100;
            r.settlementUnixSeconds = 150;
            r.reports.Add(new MatchReport { playerIndex = 0, accepted = false, winner = 1 - winner });
            r.reports.Add(new MatchReport { playerIndex = 1, accepted = true, winner = winner,
                endTick = 1000, finalHash = 12345 });
            r.reports.Add(new MatchReport { playerIndex = 0, accepted = true, winner = winner,
                endTick = 1000, finalHash = 12345 });
        });

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
            public Task<MatchPresence[]> ReadPresenceAsync(string id) => Inner.ReadPresenceAsync(id);
            public Task WritePresenceAsync(string id, int slot, MatchPresence presence) => Inner.WritePresenceAsync(id, slot, presence);
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
            public Action BeforeWrite;
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
                BeforeWrite?.Invoke();
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

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public async Task HoldFunctionsRefuseBlankIdentityBeforeAccessingStorage(string playerId)
        {
            var module = new RankedMatchModule(null);
            var context = new FakeExecutionContext(playerId);
            var result = await module.GetMatchResult(context, "match");
            var presence = await module.Presence(context, "match", true);
            var resolution = await module.ResolveHold(context, "match");
            Assert.That(result.state, Is.Null);
            Assert.That(presence.state, Is.Null);
            Assert.That(resolution.outcome, Is.Null);
            Assert.That(result.message, Is.EqualTo(RankedMatchModule.NoIdentityRefused));
            Assert.That(presence.message, Is.EqualTo(RankedMatchModule.NoIdentityRefused));
            Assert.That(resolution.message, Is.EqualTo(RankedMatchModule.NoIdentityRefused));
        }

        [Test]
        public async Task HoldFunctionsRefuseNullContextBeforeAccessingStorage()
        {
            var module = new RankedMatchModule(null);
            Assert.That((await module.GetMatchResult(null, "match")).message, Is.EqualTo(RankedMatchModule.NoIdentityRefused));
            Assert.That((await module.Presence(null, "match", true)).message, Is.EqualTo(RankedMatchModule.NoIdentityRefused));
            Assert.That((await module.ResolveHold(null, "match")).message, Is.EqualTo(RankedMatchModule.NoIdentityRefused));
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
