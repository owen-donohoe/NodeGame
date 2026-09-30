using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    [NonParallelizable]
    public class MatchHoldTests
    {
        private const string MatchId = "hold-test";
        private HookMatchStore matches;
        private FakeSettlementPlayerStore[] players;
        private MatchHold hold;

        [SetUp]
        public void SetUp()
        {
            var states = new[] { MatchRecordTests.Player(), MatchRecordTests.Player() };
            foreach (var state in states) state.ActiveMatch = new ActiveMatchRecord
                { matchId = MatchId, expiresUnixSeconds = 1 + ActiveMatchClaims.LifetimeSeconds };
            players = states.Select(s => new FakeSettlementPlayerStore(s)).ToArray();
            var record = MatchRecords.Create(MatchId, new[] { "p0", "p1" }, states, 1, 1, 1, 1);
            record.connectedUnixSeconds = 50;
            matches = new HookMatchStore(new InMemoryMatchRecordStore(record));
            hold = new MatchHold(matches, id => players[id == "p0" ? 0 : 1],
                new MatchSettler(id => players[id == "p0" ? 0 : 1], new InventoryRules(ServerCatalog.Items)));
        }

        [Test]
        public async Task PresenceStartsKeepsClearsAndRestartsHoldWithoutChangingRecordLock()
        {
            string token = (await matches.ReadAsync(MatchId)).WriteLock;
            var first = await hold.Presence(MatchId, "p0", true, 100);
            Assert.That(first.state, Is.EqualTo(MatchRecordState.Open));
            Assert.That(first.opponentSeenSecondsAgo, Is.EqualTo(-1));
            Assert.That(first.holdSeconds, Is.Zero);
            Assert.That(first.result, Is.Null);
            await hold.Presence(MatchId, "p1", false, 105);
            var kept = await hold.Presence(MatchId, "p0", true, 110);
            Assert.That(kept.holdSeconds, Is.EqualTo(10));
            Assert.That(kept.opponentSeenSecondsAgo, Is.EqualTo(5));
            var slots = await matches.ReadPresenceAsync(MatchId);
            Assert.That(slots[0].lastSeenUnixSeconds, Is.EqualTo(110));
            Assert.That(slots[0].holdSinceUnixSeconds, Is.EqualTo(100));
            Assert.That(slots[1].lastSeenUnixSeconds, Is.EqualTo(105));
            Assert.That(slots[1].holdSinceUnixSeconds, Is.Zero);
            slots[0].holdSinceUnixSeconds = 1;
            Assert.That((await matches.ReadPresenceAsync(MatchId))[0].holdSinceUnixSeconds, Is.EqualTo(100));
            var cleared = await hold.Presence(MatchId, "p0", false, 111);
            Assert.That(cleared.holdSeconds, Is.Zero);
            Assert.That((await matches.ReadPresenceAsync(MatchId))[0].holdSinceUnixSeconds, Is.Zero);
            await hold.Presence(MatchId, "p0", true, 120);
            Assert.That((await matches.ReadPresenceAsync(MatchId))[0].holdSinceUnixSeconds, Is.EqualTo(120));
            Assert.That((await matches.ReadAsync(MatchId)).WriteLock, Is.EqualTo(token));
            AssertNoPlayerWrites();
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void PresenceStoreRejectsInvalidSlots(int slot)
        {
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => matches.WritePresenceAsync(MatchId, slot, new MatchPresence()));
        }

        [TestCase(0)]
        [TestCase(100)]
        [TestCase(101)]
        public async Task ResolveRequiresTenSecondsOfServerHold(long holdSince)
        {
            if (holdSince != 0) await hold.Presence(MatchId, "p0", true, holdSince);
            var result = await hold.ResolveHold(MatchId, "p0", 109);
            Assert.That(result.outcome, Is.EqualTo(HoldOutcome.TooEarly));
            Assert.That(result.result, Is.Null);
            Assert.That((await Record()).forfeitedBy, Is.EqualTo(-1));
            AssertNoPlayerWrites();
        }

        [TestCase(110, 100)]
        [TestCase(110, 110)]
        [TestCase(159, 149)]
        public async Task SeenWithinTenSecondsPreventsAWinBeforeSixtySeconds(long now, long seen)
        {
            await hold.Presence(MatchId, "p0", true, 100);
            await hold.Presence(MatchId, "p1", false, seen);
            var result = await hold.ResolveHold(MatchId, "p0", now);
            Assert.That(result.outcome, Is.EqualTo(HoldOutcome.OpponentPresent));
            Assert.That((await Record()).state, Is.EqualTo(MatchRecordState.Open));
            AssertNoPlayerWrites();
        }

        [TestCase(160)]
        [TestCase(161)]
        public async Task SixtySecondHoldWithPresentOpponentVoidsWithoutSettlement(long now)
        {
            await hold.Presence(MatchId, "p0", true, 100);
            await hold.Presence(MatchId, "p1", true, 110);
            await hold.Presence(MatchId, "p0", true, now);
            await hold.Presence(MatchId, "p1", true, now - 10);
            var result = await hold.ResolveHold(MatchId, "p0", now);
            Assert.That(result.outcome, Is.EqualTo(HoldOutcome.Voided));
            Assert.That(result.result.state, Is.EqualTo(MatchRecordState.Void));
            Assert.That(result.result.cause, Is.EqualTo(MatchEndCause.Unknown));
            Assert.That((await Record()).forfeitedBy, Is.EqualTo(-1));
            Assert.That((await Record()).abandonedBy, Is.EqualTo(-1));
            await AssertReleased();
            AssertNoPlayerWrites();
        }

        [TestCase(14, HoldOutcome.TooEarly)]
        [TestCase(15, HoldOutcome.Won)]
        public async Task ClaimWaitsFifteenSecondsFromConnection(int elapsed, HoldOutcome expected)
        {
            await Mutate(r => r.connectedUnixSeconds = 100);
            await hold.Presence(MatchId, "p0", true, 100);
            var result = await hold.ResolveHold(MatchId, "p0", 100 + elapsed);
            Assert.That(result.outcome, Is.EqualTo(expected));
            if (expected == HoldOutcome.TooEarly)
            {
                Assert.That((await Record()).forfeitedBy, Is.EqualTo(-1));
                AssertNoPlayerWrites();
            }
        }

        [TestCase(110)]
        [TestCase(160)]
        [TestCase(220)]
        public async Task HeartbeatingOpponentWithoutAHoldCannotLoseOrVoid(long now)
        {
            await hold.Presence(MatchId, "p0", true, 100);
            for (long time = 100; time <= now; time += 4)
                await hold.Presence(MatchId, "p1", false, time);
            await hold.Presence(MatchId, "p0", true, now);
            Assert.That((await hold.ResolveHold(MatchId, "p0", now)).outcome, Is.EqualTo(HoldOutcome.OpponentPresent));
            Assert.That((await Record()).state, Is.EqualTo(MatchRecordState.Open));
            AssertNoPlayerWrites();
        }

        [TestCase(111, 160)]
        [TestCase(110, 149)]
        public async Task VoidRequiresFiftySecondsOfOpponentHoldAndRecentCallerPresence(long opponentStart, long callerSeen)
        {
            await hold.Presence(MatchId, "p0", true, 100);
            await hold.Presence(MatchId, "p1", true, opponentStart);
            await hold.Presence(MatchId, "p0", true, callerSeen);
            await hold.Presence(MatchId, "p1", true, 160);
            Assert.That((await hold.ResolveHold(MatchId, "p0", 160)).outcome, Is.EqualTo(HoldOutcome.OpponentPresent));
            Assert.That((await Record()).state, Is.EqualTo(MatchRecordState.Open));
            AssertNoPlayerWrites();
        }

        [TestCase(0, 0, 110)]
        [TestCase(1, 0, 110)]
        [TestCase(0, 99, 110)]
        [TestCase(1, 99, 110)]
        [TestCase(0, 0, 160)]
        public async Task UnseenOpponentLosesAndBothPlayersSettleOnce(int caller, long seen, long now)
        {
            await hold.Presence(MatchId, "p" + caller, true, 100);
            if (seen != 0) await hold.Presence(MatchId, "p" + (1 - caller), false, seen);
            var result = await hold.ResolveHold(MatchId, "p" + caller, now);
            Assert.That(result.outcome, Is.EqualTo(HoldOutcome.Won));
            Assert.That(result.result.won, Is.True);
            Assert.That(result.result.cause, Is.EqualTo(MatchEndCause.Abandoned));
            Assert.That(result.result.playerState.Rating.R, Is.EqualTo((await State(caller)).Rating.R));
            var record = await Record();
            Assert.That(record.forfeitedBy, Is.EqualTo(1 - caller));
            Assert.That(record.abandonedBy, Is.EqualTo(1 - caller));
            Assert.That(record.settlementUnixSeconds, Is.EqualTo(now));
            await AssertWinner(caller);
            await AssertReleased();
            var second = await hold.ResolveHold(MatchId, "p" + caller, now + 1);
            Assert.That(second.outcome, Is.EqualTo(HoldOutcome.AlreadyResolved));
            Assert.That(second.result.won, Is.True);
            Assert.That(second.result.playerState.Rating.R, Is.EqualTo(result.result.playerState.Rating.R));
            Assert.That(players.Select(p => p.WriteCount), Is.EqualTo(new[] { 1, 1 }));
        }

        [Test]
        public async Task OpposingConcurrentClaimsSettleTheFirstCommittedWinner()
        {
            await hold.Presence(MatchId, "p0", true, 100);
            await hold.Presence(MatchId, "p1", true, 100);
            matches.BeforeWrite = async (_, __) =>
            {
                matches.BeforeWrite = null;
                var other = await hold.ResolveHold(MatchId, "p1", 111);
                Assert.That(other.outcome, Is.EqualTo(HoldOutcome.Won));
            };
            var result = await hold.ResolveHold(MatchId, "p0", 111);
            Assert.That(result.outcome, Is.EqualTo(HoldOutcome.AlreadyResolved));
            Assert.That(result.result.won, Is.False);
            Assert.That((await Record()).abandonedBy, Is.Zero);
            Assert.That((await Record()).forfeitedBy, Is.Zero);
            await AssertWinner(1);
            await AssertReleased();
        }

        [Test]
        public async Task OpposingClaimAfterDecisionCommitCannotChangeWinnerBeforeSettlement()
        {
            await hold.Presence(MatchId, "p0", true, 100);
            await hold.Presence(MatchId, "p1", true, 100);
            matches.BeforeWrite = async (record, token) =>
            {
                matches.BeforeWrite = null;
                Assert.That(record.forfeitedBy, Is.EqualTo(1));
                Assert.That(record.abandonedBy, Is.EqualTo(1));
                Assert.That(record.settlementUnixSeconds, Is.EqualTo(111));
                AssertNoPlayerWrites();
                await matches.Inner.WriteAsync(record, token);
                var other = await hold.ResolveHold(MatchId, "p1", 111);
                Assert.That(other.outcome, Is.EqualTo(HoldOutcome.AlreadyResolved));
                Assert.That(other.result.won, Is.False);
            };
            var result = await hold.ResolveHold(MatchId, "p0", 111);
            Assert.That(result.outcome, Is.EqualTo(HoldOutcome.AlreadyResolved));
            Assert.That(result.result.won, Is.True);
            await AssertWinner(0);
            await AssertReleased();
        }

        [Test]
        public async Task FinalRecordConflictRetriesWithoutSettlingPlayersTwice()
        {
            await hold.Presence(MatchId, "p0", true, 100);
            matches.BeforeWrite = async (record, _) =>
            {
                if (record.state != MatchRecordState.Settled) return;
                matches.BeforeWrite = null;
                await Mutate(r => r.joinCode = "changed");
            };
            Assert.That((await hold.ResolveHold(MatchId, "p0", 110)).outcome, Is.EqualTo(HoldOutcome.Won));
            await AssertWinner(0);
            await AssertReleased();
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task ExpiredLifetimeOrLostClaimVoidsWithoutSettlement(bool lostClaim)
        {
            await hold.Presence(MatchId, "p0", true, 100);
            if (lostClaim) players[1].Mutate(s => s.ActiveMatch = new ActiveMatchRecord
                { matchId = "other", expiresUnixSeconds = 99999 });
            var result = await hold.ResolveHold(MatchId, "p0", lostClaim ? 110 : 1 + ActiveMatchClaims.LifetimeSeconds);
            Assert.That(result.outcome, Is.EqualTo(HoldOutcome.Voided));
            Assert.That(result.result.state, Is.EqualTo(MatchRecordState.Void));
            Assert.That((await State(0)).ActiveMatch.matchId, Is.Null);
            Assert.That((await State(1)).ActiveMatch.matchId, Is.EqualTo(lostClaim ? "other" : null));
            AssertNoPlayerWrites();
        }

        [Test]
        public async Task PendingTimeoutUsesLeaveRules()
        {
            await Mutate(r => { r.state = MatchRecordState.Pending; r.pendingUnixSeconds = 100; });
            var result = await hold.ResolveHold(MatchId, "p0", 101 + ActiveMatchClaims.PendingTimeoutSeconds);
            Assert.That(result.outcome, Is.EqualTo(HoldOutcome.Voided));
            await AssertReleased();
            AssertNoPlayerWrites();
        }

        [Test]
        public async Task SettlementClaimLossClearsOutcomesAndPreservesNewClaim()
        {
            await hold.Presence(MatchId, "p0", true, 100);
            players[0].BeforeWrite = () =>
            {
                players[0].BeforeWrite = null;
                players[0].Mutate(s => s.ActiveMatch = new ActiveMatchRecord
                    { matchId = "other", expiresUnixSeconds = 99999 });
            };
            var result = await hold.ResolveHold(MatchId, "p0", 110);
            Assert.That(result.outcome, Is.EqualTo(HoldOutcome.Voided));
            Assert.That(result.result.playerState, Is.Null);
            Assert.That((await Record()).outcomes, Is.Null);
            Assert.That((await State(0)).ActiveMatch.matchId, Is.EqualTo("other"));
            Assert.That((await State(1)).ActiveMatch.matchId, Is.Null);
            AssertNoPlayerWrites();
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task CommittedDecisionOutranksHoldAndPendingTimeout(bool forfeit)
        {
            await Mutate(r =>
            {
                r.state = MatchRecordState.Pending;
                r.pendingUnixSeconds = 100;
                r.settlementUnixSeconds = 150;
                if (forfeit) r.forfeitedBy = 0;
                r.reports.Add(new MatchReport { playerIndex = 0, accepted = true, winner = 0 });
                r.reports.Add(new MatchReport { playerIndex = 1, accepted = true, winner = 0 });
            });
            var result = await hold.ResolveHold(MatchId, "p0", 101 + ActiveMatchClaims.PendingTimeoutSeconds);
            Assert.That(result.outcome, Is.EqualTo(HoldOutcome.AlreadyResolved));
            Assert.That(result.result.cause, Is.EqualTo(forfeit ? MatchEndCause.Forfeit : MatchEndCause.Played));
            Assert.That((await Record()).settlementUnixSeconds, Is.EqualTo(150));
            await AssertWinner(forfeit ? 1 : 0);
            await AssertReleased();
        }

        [TestCase(MatchRecordState.Open)]
        [TestCase(MatchRecordState.Pending)]
        [TestCase(MatchRecordState.Void)]
        [TestCase(MatchRecordState.Disputed)]
        public async Task GetResultIsReadOnlyAndHasNoOutcomeBeforeSettlement(MatchRecordState state)
        {
            await Mutate(r => r.state = state);
            string token = (await matches.ReadAsync(MatchId)).WriteLock;
            var result = await hold.GetResult(MatchId, "p0");
            Assert.That(result.state, Is.EqualTo(state));
            Assert.That(result.cause, Is.EqualTo(MatchEndCause.Unknown));
            Assert.That(result.won, Is.Null);
            Assert.That(result.rrDelta, Is.Null);
            Assert.That(result.rrAfter, Is.Null);
            Assert.That(result.arenaAfter, Is.Null);
            Assert.That(result.playerState, Is.Null);
            Assert.That((await matches.ReadAsync(MatchId)).WriteLock, Is.EqualTo(token));
            Assert.That((await State(0)).ActiveMatch.matchId, Is.EqualTo(MatchId));
            Assert.That((await State(1)).ActiveMatch.matchId, Is.EqualTo(MatchId));
            AssertNoPlayerWrites();
        }

        [TestCase(-1, -1, MatchEndCause.Played)]
        [TestCase(1, -1, MatchEndCause.Forfeit)]
        [TestCase(1, 1, MatchEndCause.Abandoned)]
        public async Task SettledResultReturnsStoredOutcomeAndCurrentCallerState(int forfeited, int abandoned, MatchEndCause cause)
        {
            await Mutate(r =>
            {
                r.state = MatchRecordState.Settled;
                r.forfeitedBy = forfeited;
                r.abandonedBy = abandoned;
                r.outcomes = new[]
                {
                    new MatchOutcome { won = true, rrDelta = 21, rrAfter = 301, arenaAfter = 1, promoted = true },
                    new MatchOutcome { won = false, rrDelta = -17, rrAfter = 299, arenaAfter = 0, demoted = true }
                };
            });
            players[0].Mutate(s => s.Rank.RR = 400);
            players[1].Mutate(s => s.Rank.RR = 500);
            string token = (await matches.ReadAsync(MatchId)).WriteLock;
            for (int caller = 0; caller < 2; caller++)
            {
                var result = await hold.GetResult(MatchId, "p" + caller);
                var outcome = (await Record()).outcomes[caller];
                Assert.That(result.state, Is.EqualTo(MatchRecordState.Settled));
                Assert.That(result.cause, Is.EqualTo(cause));
                Assert.That(result.won, Is.EqualTo(outcome.won));
                Assert.That(result.rrDelta, Is.EqualTo(outcome.rrDelta));
                Assert.That(result.rrAfter, Is.EqualTo(outcome.rrAfter));
                Assert.That(result.arenaAfter, Is.EqualTo(outcome.arenaAfter));
                Assert.That(result.promoted, Is.EqualTo(outcome.promoted));
                Assert.That(result.demoted, Is.EqualTo(outcome.demoted));
                Assert.That(result.playerState.Rank.RR, Is.EqualTo(caller == 0 ? 400 : 500));
                Assert.That((await State(caller)).ActiveMatch.matchId, Is.EqualTo(MatchId));
            }
            Assert.That((await matches.ReadAsync(MatchId)).WriteLock, Is.EqualTo(token));
            AssertNoPlayerWrites();
        }

        [TestCase(MatchRecordState.Settled)]
        [TestCase(MatchRecordState.Void)]
        [TestCase(MatchRecordState.Disputed)]
        public async Task TerminalPresenceAndResolutionCarryResultAndReleaseClaims(MatchRecordState state)
        {
            await Mutate(r => r.state = state);
            string token = (await matches.ReadAsync(MatchId)).WriteLock;
            var presence = await hold.Presence(MatchId, "p0", true, 100);
            Assert.That(presence.result.state, Is.EqualTo(state));
            Assert.That(presence.result.playerState != null, Is.EqualTo(state == MatchRecordState.Settled));
            await AssertReleased();
            foreach (var player in players) player.Mutate(s => s.ActiveMatch = new ActiveMatchRecord
                { matchId = MatchId, expiresUnixSeconds = 99999 });
            var result = await hold.ResolveHold(MatchId, "p1", 101);
            Assert.That(result.outcome, Is.EqualTo(HoldOutcome.AlreadyResolved));
            Assert.That(result.result.state, Is.EqualTo(state));
            Assert.That(result.result.playerState != null, Is.EqualTo(state == MatchRecordState.Settled));
            Assert.That((await matches.ReadAsync(MatchId)).WriteLock, Is.EqualTo(token));
            await AssertReleased();
            AssertNoPlayerWrites();
        }

        [TestCase(MatchId, "stranger", "not in")]
        [TestCase("missing", "p0", "not found")]
        public async Task AllCallsRefuseMissingMatchOrNonMember(string id, string caller, string message)
        {
            var result = await hold.GetResult(id, caller);
            var presence = await hold.Presence(id, caller, true, 100);
            var resolution = await hold.ResolveHold(id, caller, 110);
            Assert.That(result.state, Is.Null);
            Assert.That(presence.state, Is.Null);
            Assert.That(resolution.outcome, Is.Null);
            Assert.That(result.message, Does.Contain(message));
            Assert.That(presence.message, Does.Contain(message));
            Assert.That(resolution.message, Does.Contain(message));
            Assert.That((await matches.ReadPresenceAsync(MatchId)).All(p => p.lastSeenUnixSeconds == 0), Is.True);
            Assert.That((await State(0)).ActiveMatch.matchId, Is.EqualTo(MatchId));
            AssertNoPlayerWrites();
        }

        [Test]
        public async Task UnstartedMatchRefusesResolution()
        {
            await Mutate(r => r.connectedUnixSeconds = 0);
            await hold.Presence(MatchId, "p0", true, 100);
            var result = await hold.ResolveHold(MatchId, "p0", 110);
            Assert.That(result.outcome, Is.Null);
            Assert.That(result.message, Does.Contain("not started"));
            Assert.That((await Record()).state, Is.EqualTo(MatchRecordState.Open));
            AssertNoPlayerWrites();
        }

        private async Task AssertWinner(int winner)
        {
            var record = await Record();
            Assert.That(record.state, Is.EqualTo(MatchRecordState.Settled));
            Assert.That(record.outcomes[winner].won, Is.True);
            Assert.That(record.outcomes[1 - winner].won, Is.False);
            Assert.That((await State(winner)).Rating.R, Is.GreaterThan(1500));
            Assert.That((await State(1 - winner)).Rating.R, Is.LessThan(1500));
            for (int slot = 0; slot < 2; slot++)
            {
                Assert.That((await State(slot)).Rating.SettledMatchIds, Is.EqualTo(new[] { MatchId }));
                Assert.That((await State(slot)).History.MatchIds, Is.EqualTo(new[] { MatchId }));
                Assert.That((await State(slot)).Rank.RR, Is.EqualTo(record.outcomes[slot].rrAfter));
            }
            Assert.That(players.Select(p => p.WriteCount), Is.EqualTo(new[] { 1, 1 }));
        }

        private async Task AssertReleased()
        {
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
}
