using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NodeWar.Backend;
using NodeWar.MatchLog;
using NodeWar.Progression;
using NodeWar.Simulation;
using NUnit.Framework;
using Log = NodeWar.MatchLog.MatchLog;

namespace NodeWar.Cloud.Tests
{
    [NonParallelizable]
    public class MatchReportingTests
    {
        private const string MatchId = "referee-test";
        private GameBalanceData balance;
        private byte[] winningBytes;
        private byte[] differentBytes;
        private HookMatchStore matches;
        private FakeSettlementPlayerStore[] players;
        private MatchReporting reporting;

        [OneTimeSetUp]
        public void RecordFixtures()
        {
            balance = GameBalanceData.Default();
            var winner = RefereeTests.Record(balance, 3000, RefereeTests.Rush);
            Assert.That(winner.result.winner, Is.EqualTo(0));
            winningBytes = MatchLogFormat.Write(winner);
            var different = RefereeTests.Record(balance, 3000, state =>
                state.tickCount == 0 || state.tickCount >= 200 ? RefereeTests.Rush(state) : null);
            differentBytes = MatchLogFormat.Write(different);
            Assert.That(different.result.winner, Is.EqualTo(0));
            Assert.That(different.result.endTick, Is.Not.EqualTo(winner.result.endTick));
        }

        [SetUp]
        public void SetUp() => Initialize(1000, 1);

        private void Initialize(int firstRR, int secondRR)
        {
            var states = new[] { Player(firstRR), Player(secondRR) };
            players = states.Select(s => new FakeSettlementPlayerStore(s)).ToArray();
            var record = MatchRecords.Create(MatchId, new[] { "p0", "p1" }, states, 1,
                1, (ushort)SimulationVersion.Current, BalanceHasher.Hash(balance));
            matches = new HookMatchStore(new InMemoryMatchRecordStore(record));
            reporting = new MatchReporting(matches, id => players[id == "p0" ? 0 : 1],
                new Referee(RefereeTests.Catalog(balance)), new InventoryRules(ServerCatalog.Items));
        }

        [Test]
        public async Task AgreementSettlesBothPlayersFromSnapshotsAndReturnsOnlyCallerState()
        {
            var first = await Report(0);
            Assert.That(first.state, Is.EqualTo(MatchRecordState.Pending));
            Assert.That(first.playerState, Is.Null);
            AssertNoPlayerWrites();
            var secondLog = ReadLog(winningBytes);
            secondLog.header.localPlayer = 1;
            var second = await Report(1, MatchLogFormat.Write(secondLog), 110);

            Assert.That(second.state, Is.EqualTo(MatchRecordState.Settled));
            var winner = await State(0);
            var loser = await State(1);
            Assert.That(winner.Rating.R, Is.GreaterThan(1500));
            Assert.That(winner.Rank.RR, Is.GreaterThan(1000));
            Assert.That(loser.Rating.R, Is.LessThan(1500));
            Assert.That(loser.Rank.RR, Is.Zero);
            Assert.That(second.playerState.Rating.R, Is.EqualTo(loser.Rating.R));
            foreach (var player in new[] { winner, loser })
            {
                Assert.That(player.History.MatchIds, Is.EqualTo(new[] { MatchId }));
                Assert.That(player.Rating.LastMatchUnixSeconds, Is.EqualTo(110));
            }
            Assert.That(players.Select(p => p.WriteCount), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(matches.Inner.ReadLog(MatchId, 0), Is.EqualTo(Convert.ToBase64String(winningBytes)));
            Assert.That(matches.Inner.ReadLog(MatchId, 1), Is.EqualTo(Convert.ToBase64String(MatchLogFormat.Write(secondLog))));
            Assert.That((await Record()).reports.All(r => r.pendingLogBase64 == null), Is.True);
        }

        [Test]
        public async Task IndependentlyValidDisagreementIsDisputedWithoutPlayerWrites()
        {
            await Report(0);
            var result = await Report(1, differentBytes);
            Assert.That(result.state, Is.EqualTo(MatchRecordState.Disputed));
            Assert.That((await Record()).reports.All(r => r.accepted), Is.True);
            AssertNoPlayerWrites();
        }

        [Test]
        public async Task SamePlayerTwiceCannotSettleOrReplaceTheirFirstReport()
        {
            await Report(0);
            Assert.That((await Report(0, differentBytes)).state, Is.EqualTo(MatchRecordState.Pending));
            Assert.That((await Record()).reports, Has.Count.EqualTo(1));
            Assert.That(matches.Inner.ReadLog(MatchId, 0), Is.EqualTo(Convert.ToBase64String(winningBytes)));
            AssertNoPlayerWrites();
            await Report(1);
            string before = JsonConvert.SerializeObject(await State(0));
            await Report(0, differentBytes, 9999);
            Assert.That(JsonConvert.SerializeObject(await State(0)), Is.EqualTo(before));
            Assert.That(players.Select(p => p.WriteCount), Is.EqualTo(new[] { 1, 1 }));
        }

        [Test]
        public async Task StrangerAndUnknownMatchAreRefusedWithoutWrites()
        {
            Assert.That((await reporting.Report(MatchId, "stranger", winningBytes, 100)).message, Does.Contain("not in"));
            Assert.That((await reporting.Report("unknown", "p0", winningBytes, 100)).message, Does.Contain("not found"));
            Assert.That((await Record()).reports, Is.Empty);
            Assert.That(matches.Inner.ReadLog(MatchId, 0), Is.Null);
            AssertNoPlayerWrites();
        }

        [TestCase("matchId")]
        [TestCase("playerIds")]
        [TestCase("protocol")]
        [TestCase("sim")]
        [TestCase("content")]
        public async Task HeaderMustExactlyMatchTheServerRecord(string field)
        {
            var log = ReadLog(winningBytes);
            switch (field)
            {
                case "matchId": log.header.matchId = "other"; break;
                case "playerIds": Array.Reverse(log.header.playerIds); break;
                case "protocol": log.header.protocol++; break;
                case "sim": log.header.sim++; break;
                case "content": log.header.content++; break;
            }
            Assert.That((await Report(0, MatchLogFormat.Write(log))).message, Does.Contain("header"));
            Assert.That((await Record()).reports.Single().accepted, Is.False);
            AssertNoPlayerWrites();
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task IneligibleEraIsRecordedRefused(bool aboveArena)
        {
            var log = ReadLog(winningBytes);
            log.loadouts[1].suitEras = new int[NodeWar.Lobby.LoadoutTypes.SuitTypeCount];
            log.loadouts[1].suitEras[(int)SuitType.Warrior] = 1;
            var read = await matches.ReadAsync(MatchId);
            if (aboveArena) read.Record.players[1].OwnedVariants.Add("suit.warrior.e1");
            else read.Record.players[1].Rank.Arena = 1;
            await matches.WriteAsync(read.Record, read.WriteLock);
            var result = await Report(0, MatchLogFormat.Write(log));
            Assert.That(result.message, Does.Contain(aboveArena ? "snapshot arena" : "not owned"));
            Assert.That((await Record()).reports.Single().accepted, Is.False);
            AssertNoPlayerWrites();
        }

        [Test]
        public async Task RefusedReportDoesNotConsumeTheSlotAndCanRecoverToSettle()
        {
            var bad = ReadLog(winningBytes);
            bad.result.finalHash ^= 1;
            await Report(0, MatchLogFormat.Write(bad));
            Assert.That((await Record()).reports.Single().accepted, Is.False);
            AssertNoPlayerWrites();

            // The same player retries with a valid log: the earlier refusal must
            // not have consumed player 0's slot.
            Assert.That((await Report(0)).state, Is.EqualTo(MatchRecordState.Pending));
            Assert.That((await Record()).reports.Count(r => r.accepted), Is.EqualTo(1));
            Assert.That((await Record()).reports.Count(r => r.playerIndex == 0), Is.EqualTo(2));
            AssertNoPlayerWrites();

            Assert.That((await Report(1)).state, Is.EqualTo(MatchRecordState.Settled));
            Assert.That(players.Select(p => p.WriteCount), Is.EqualTo(new[] { 1, 1 }));
        }

        [Test]
        public async Task ConsistentUnfinishedReplayCannotBeAccepted()
        {
            byte[] unfinished = MatchLogFormat.Write(RefereeTests.Record(balance, 100, _ => null));
            var result = await Report(0, unfinished);
            Assert.That(result.message, Does.Contain("rated win"));
            Assert.That((await Record()).reports.Single().accepted, Is.False);
            AssertNoPlayerWrites();
        }

        [TestCase(700, MatchRecordState.Pending)]
        [TestCase(701, MatchRecordState.Void)]
        [TestCase(99, MatchRecordState.Pending)]
        public async Task PendingTimeoutIsStrictlyMoreThanTenMinutesFromFirstValidReport(long now, MatchRecordState expected)
        {
            await Report(0, now: 100);
            Assert.That((await Report(0, now: now)).state, Is.EqualTo(expected));
            AssertNoPlayerWrites();
        }

        [Test]
        public async Task LateOpponentCannotSettleExpiredPendingMatch()
        {
            await Report(0, now: 100);
            Assert.That((await Report(1, now: 701)).state, Is.EqualTo(MatchRecordState.Void));
            Assert.That((await Record()).reports, Has.Count.EqualTo(1));
            AssertNoPlayerWrites();
        }

        [Test]
        public async Task RecordConflictRereadsTerminalDecisionBeforeAnyPlayerWrites()
        {
            await Report(0);
            matches.BeforeWrite = async (record, _) =>
            {
                matches.BeforeWrite = null;
                var current = await matches.Inner.ReadAsync(MatchId);
                current.Record.state = MatchRecordState.Void;
                await matches.Inner.WriteAsync(current.Record, current.WriteLock);
            };
            Assert.That((await Report(1)).state, Is.EqualTo(MatchRecordState.Void));
            AssertNoPlayerWrites();
        }

        [Test]
        public async Task ConcurrentFirstUploadsRedecideAndSettleExactlyOnce()
        {
            matches.BeforeWrite = async (_, __) =>
            {
                matches.BeforeWrite = null;
                Assert.That((await Report(1)).state, Is.EqualTo(MatchRecordState.Pending));
            };
            Assert.That((await Report(0)).state, Is.EqualTo(MatchRecordState.Settled));
            Assert.That(players.Select(p => p.WriteCount), Is.EqualTo(new[] { 1, 1 }));
        }

        [Test]
        public async Task ConcurrentSamePlayerUploadCannotOverwriteWinningReportsLog()
        {
            matches.BeforeWrite = async (_, __) =>
            {
                matches.BeforeWrite = null;
                await Report(0, differentBytes);
            };
            await Report(0);
            Assert.That(matches.Inner.ReadLog(MatchId, 0), Is.EqualTo(Convert.ToBase64String(differentBytes)));
            Assert.That((await Report(1)).state, Is.EqualTo(MatchRecordState.Disputed));
            AssertNoPlayerWrites();
        }

        [Test]
        public async Task CrashAfterFirstPlayerWriteResumesOnlySecondEvenAfterTimeout()
        {
            await Report(0);
            players[1].BeforeWrite = () => throw new IOException("simulated crash");
            Assert.ThrowsAsync<IOException>(() => Report(1, now: 110));
            string first = JsonConvert.SerializeObject(await State(0));
            Assert.That(players.Select(p => p.WriteCount), Is.EqualTo(new[] { 1, 0 }));
            players[1].BeforeWrite = null;

            Assert.That((await Report(1, now: 5000)).state, Is.EqualTo(MatchRecordState.Settled));
            Assert.That(JsonConvert.SerializeObject(await State(0)), Is.EqualTo(first));
            Assert.That(players.Select(p => p.WriteCount), Is.EqualTo(new[] { 1, 1 }));
            Assert.That((await State(1)).Rating.LastMatchUnixSeconds, Is.EqualTo(110));
        }

        [Test]
        public async Task CrashAfterReportCommitBeforeLogCopyRecoversOriginalBytes()
        {
            matches.FailLog = true;
            Assert.ThrowsAsync<IOException>(() => Report(0));
            Assert.That((await Record()).reports.Single().pendingLogBase64, Is.Not.Null);
            matches.FailLog = false;
            await Report(0, differentBytes);
            Assert.That(matches.Inner.ReadLog(MatchId, 0), Is.EqualTo(Convert.ToBase64String(winningBytes)));
        }

        [Test]
        public async Task PlayerConflictRereadsHistoryAndPreservesConcurrentInventory()
        {
            await Report(0);
            players[0].BeforeWrite = () =>
            {
                players[0].BeforeWrite = null;
                players[0].Mutate(s => s.Inventory.OwnedSkins.Add("concurrent-skin"));
            };
            await Report(1);
            Assert.That((await State(0)).Inventory.OwnedSkins, Does.Contain("concurrent-skin"));
            Assert.That((await State(0)).History.MatchIds, Is.EqualTo(new[] { MatchId }));
            Assert.That(players[0].WriteAttempts, Is.EqualTo(2));
            Assert.That(players[0].WriteCount, Is.EqualTo(1));
        }

        [Test]
        public async Task PlayerConflictsAreBoundedAndRetryRemainsRecoverable()
        {
            await Report(0);
            players[0].BeforeWrite = () => players[0].Mutate(_ => { });
            Assert.ThrowsAsync<RecordConflictException>(() => Report(1));
            Assert.That(players[0].WriteAttempts, Is.EqualTo(3));
            AssertNoPlayerWrites();
            players[0].BeforeWrite = null;
            Assert.That((await Report(1)).state, Is.EqualTo(MatchRecordState.Settled));
        }

        [Test]
        public async Task PromotionGrantsNewEraVariantsAndDemotionClampsEquipped()
        {
            Initialize(299, 300);
            players[1].Mutate(s => s.Inventory.Equipped.Variants["suit.warrior"] = "suit.warrior.e1");
            await Report(0);
            await Report(1);
            var winner = await State(0);
            var loser = await State(1);
            Assert.That(winner.Rank.Arena, Is.EqualTo(1));
            Assert.That(winner.Rank.HighestArena, Is.EqualTo(1));
            Assert.That(winner.Inventory.OwnedVariants.Count(id => id.EndsWith(".e1")), Is.EqualTo(23));
            Assert.That(loser.Rank.Arena, Is.Zero);
            Assert.That(loser.Rank.HighestArena, Is.EqualTo(1));
            Assert.That(loser.Inventory.OwnedVariants, Does.Contain("suit.warrior.e1"));
            Assert.That(loser.Inventory.Equipped.Variants["suit.warrior"], Is.EqualTo("suit.warrior.e0"));
        }

        [Test]
        public async Task HistoryPrependsAndCapsTwentyWithoutRecomputingSnapshotRatings()
        {
            players[0].Mutate(s =>
            {
                s.History.MatchIds = Enumerable.Range(0, 20).Select(i => "old-" + i).ToList();
                s.Rating.R = 2000;
                s.Rank.RR = 2000;
            });
            await Report(0);
            await Report(1);
            var state = await State(0);
            Assert.That(state.History.MatchIds, Is.EqualTo(new[] { MatchId }.Concat(Enumerable.Range(0, 19).Select(i => "old-" + i))));
            Assert.That(state.Rating.R, Is.EqualTo(Glicko2.UpdateSingleMatch(default, default, 1, new Glicko2Config()).R));
            Assert.That(state.Rank.RR, Is.InRange(1001, 1040));
        }

        private Task<MatchReportingResult> Report(int player, byte[] bytes = null, long now = 100) =>
            reporting.Report(MatchId, "p" + player, bytes ?? winningBytes, now);
        private async Task<PlayerState> State(int player) => (await players[player].ReadForSettlementAsync()).State;
        private async Task<MatchRecord> Record() => (await matches.ReadAsync(MatchId)).Record;
        private void AssertNoPlayerWrites() => Assert.That(players.Select(p => p.WriteCount), Is.EqualTo(new[] { 0, 0 }));
        private static Log ReadLog(byte[] bytes)
        {
            Assert.That(MatchLogFormat.TryRead(bytes, out var log, out string error), Is.True, error);
            return log;
        }
        private static PlayerState Player(int rr)
        {
            var state = MatchRecordTests.Player();
            int arena = Arenas.ArenaForRR(rr, new ArenaConfig());
            state.Rank = new RankRecord { RR = rr, Arena = arena, HighestArena = arena };
            new InventoryRules(ServerCatalog.Items).GrantDefaults(state);
            return state;
        }

        private sealed class HookMatchStore : IMatchRecordStore
        {
            public readonly InMemoryMatchRecordStore Inner;
            public Func<MatchRecord, string, Task> BeforeWrite;
            public bool FailLog;
            public HookMatchStore(InMemoryMatchRecordStore inner) { Inner = inner; }
            public Task<LockedMatchRecord> ReadAsync(string id) => Inner.ReadAsync(id);
            public async Task WriteAsync(MatchRecord record, string token)
            {
                if (BeforeWrite != null) await BeforeWrite(record, token);
                await Inner.WriteAsync(record, token);
            }
            public Task SaveLog(string id, int player, string log)
            {
                if (FailLog) throw new IOException("simulated log storage failure");
                return Inner.SaveLog(id, player, log);
            }
        }

        private sealed class FakeSettlementPlayerStore : ISettlementPlayerStore
        {
            private PlayerState state;
            private int version = 1;
            public int WriteCount;
            public int WriteAttempts;
            public Action BeforeWrite;
            public FakeSettlementPlayerStore(PlayerState state) { this.state = Clone(state); }
            public Task<LockedPlayerState> ReadForSettlementAsync() => Task.FromResult(new LockedPlayerState(Clone(state),
                PlayerStateKeys.All.ToDictionary(k => k, _ => version.ToString())));
            public Task WriteForSettlementAsync(PlayerState value, IReadOnlyDictionary<string, string> tokens)
            {
                WriteAttempts++;
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
