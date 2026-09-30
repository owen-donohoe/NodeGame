using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NodeWar.Backend;
using NodeWar.Progression;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class MatchDisciplineTests
    {
        private const string MatchId = "discipline-test";
        private InMemoryMatchRecordStore matches;
        private PlayerStore[] players;
        private MatchDiscipline discipline;
        private MatchHold hold;
        private MatchRendezvous rendezvous;
        private MatchReporting reporting;
        private WarningLogger logger;

        [SetUp]
        public void SetUp()
        {
            var states = new[] { MatchRecordTests.Player(), MatchRecordTests.Player() };
            foreach (var state in states)
            {
                state.ActiveMatch = new ActiveMatchRecord { matchId = MatchId, expiresUnixSeconds = 7201 };
                state.Discipline = new DisciplineRecord();
            }
            players = states.Select(s => new PlayerStore(s)).ToArray();
            var record = MatchRecords.Create(MatchId, new[] { "p0", "p1" }, states, 1, 1, 1, 1);
            record.connectedUnixSeconds = 50;
            matches = new InMemoryMatchRecordStore(record);
            logger = new WarningLogger();
            discipline = new MatchDiscipline(id => players[id == "p0" ? 0 : 1], logger);
            var rules = new InventoryRules(ServerCatalog.Items);
            Func<string, ISettlementPlayerStore> stores = id => players[id == "p0" ? 0 : 1];
            var settler = new MatchSettler(stores, rules);
            hold = new MatchHold(matches, stores, settler, discipline);
            rendezvous = new MatchRendezvous(matches, stores, settler, discipline);
            reporting = new MatchReporting(matches, stores, new Referee(BalanceCatalog.Embedded), rules, discipline);
        }

        [Test]
        public async Task AbandonedHoldStrikesAfterTerminalWriteAndReturnsDiscipline()
        {
            players[1].BeforeDisciplineWrite = async () =>
            {
                Assert.That((await Record()).state, Is.EqualTo(MatchRecordState.Settled));
                Assert.That(players.Select(p => p.Settlements), Is.EqualTo(new[] { 1, 1 }));
            };
            await hold.Presence(MatchId, "p0", true, 100);
            Assert.That((await hold.ResolveHold(MatchId, "p0", 110)).outcome, Is.EqualTo(HoldOutcome.Won));
            var loser = await hold.GetResult(MatchId, "p1");
            Assert.That(loser.playerState.Discipline.Level, Is.EqualTo(1));
            Assert.That(loser.playerState.Discipline.LastStrikeUnixSeconds, Is.EqualTo(110));
            Assert.That(players[0].State.Discipline.Level, Is.Zero);
            Assert.That(players[1].State.Discipline.StruckMatchIds, Is.EqualTo(new[] { MatchId }));
            await AssertReleased();
        }

        [TestCase("hold")]
        [TestCase("leave")]
        [TestCase("report")]
        public async Task EverySettlementPathRecoversCommittedAbandonmentAndStrikesOnce(string path)
        {
            await Mutate(r => { r.forfeitedBy = r.abandonedBy = 1; r.settlementUnixSeconds = 100; });
            await Finish(path, 110);
            await Finish(path, 111);
            Assert.That((await Record()).state, Is.EqualTo(MatchRecordState.Settled));
            Assert.That(players[1].State.Discipline.Level, Is.EqualTo(1));
            Assert.That(players[1].DisciplineWrites, Is.EqualTo(1));
            Assert.That(players[0].DisciplineWrites, Is.Zero);
            Assert.That(players.Select(p => p.Settlements), Is.EqualTo(new[] { 1, 1 }));
            await AssertReleased();
        }

        [TestCase("hold")]
        [TestCase("leave")]
        [TestCase("report")]
        public async Task EveryPendingTimeoutPathCountsSilentSlotOnceAndSecondNonReportStrikes(string path)
        {
            players[1].State.Discipline.NonReports.Add(90);
            await Mutate(r =>
            {
                r.state = MatchRecordState.Pending;
                r.pendingUnixSeconds = 100;
                r.reports.Add(new MatchReport { playerIndex = 0, accepted = true });
                r.reports.Add(new MatchReport { playerIndex = 1, accepted = false });
            });
            await Finish(path, 701);
            await Finish(path, 702);
            var record = await Record();
            Assert.That(record.state, Is.EqualTo(MatchRecordState.Void));
            Assert.That(record.pendingTimeoutVoid, Is.True);
            Assert.That(players[1].State.Discipline.NonReports, Is.EqualTo(new long[] { 90, 701 }));
            Assert.That(players[1].State.Discipline.Level, Is.EqualTo(1));
            Assert.That(players[1].DisciplineWrites, Is.EqualTo(1));
            Assert.That(players[0].DisciplineWrites, Is.Zero);
            Assert.That(players.Select(p => p.Settlements), Is.EqualTo(new[] { 0, 0 }));
            await AssertReleased();
        }

        [Test]
        public async Task FirstNonReportConsumesIdempotencyKeyWithoutAStrike()
        {
            var record = await Record();
            record.state = MatchRecordState.Void;
            record.pendingTimeoutVoid = true;
            record.reports.Add(new MatchReport { playerIndex = 1, accepted = true });
            await discipline.Apply(record, 100);
            await discipline.Apply(record, 101);
            Assert.That(players[0].State.Discipline.Level, Is.Zero);
            Assert.That(players[0].State.Discipline.NonReports, Is.EqualTo(new long[] { 100 }));
            Assert.That(players[0].State.Discipline.StruckMatchIds, Is.EqualTo(new[] { MatchId }));
            Assert.That(players[0].DisciplineWrites, Is.EqualTo(1));
        }

        [TestCase(MatchRecordState.Settled, -1, false, 0)]
        [TestCase(MatchRecordState.Void, -1, false, 1)]
        [TestCase(MatchRecordState.Void, -1, true, 0)]
        [TestCase(MatchRecordState.Void, -1, true, 2)]
        [TestCase(MatchRecordState.Void, 1, false, 1)]
        [TestCase(MatchRecordState.Pending, 1, false, 1)]
        [TestCase(MatchRecordState.Disputed, 1, false, 2)]
        public async Task OtherOutcomesDoNotWriteDiscipline(MatchRecordState state, int abandoned, bool timeout, int reports)
        {
            var record = await Record();
            record.state = state;
            record.forfeitedBy = 1;
            record.abandonedBy = abandoned;
            record.pendingTimeoutVoid = timeout;
            for (int i = 0; i < reports; i++) record.reports.Add(new MatchReport { playerIndex = i, accepted = true });
            await discipline.Apply(record, 100);
            Assert.That(players.Select(p => p.DisciplineWrites), Is.EqualTo(new[] { 0, 0 }));
        }

        [Test]
        public async Task SurrenderDoesNotStrike()
        {
            await rendezvous.Leave(MatchId, "p1", true, 100);
            Assert.That((await Record()).state, Is.EqualTo(MatchRecordState.Settled));
            Assert.That(players.Select(p => p.DisciplineWrites), Is.EqualTo(new[] { 0, 0 }));
        }

        [Test]
        public async Task BothSeenHoldVoidDoesNotCountNonReportEvenWithOneReport()
        {
            await Mutate(r =>
            {
                r.state = MatchRecordState.Pending;
                r.pendingUnixSeconds = 100;
                r.reports.Add(new MatchReport { playerIndex = 0, accepted = true });
            });
            await hold.Presence(MatchId, "p0", true, 100);
            await hold.Presence(MatchId, "p1", true, 110);
            await hold.Presence(MatchId, "p0", true, 160);
            await hold.Presence(MatchId, "p1", true, 160);
            Assert.That((await hold.ResolveHold(MatchId, "p0", 160)).outcome, Is.EqualTo(HoldOutcome.Voided));
            Assert.That((await Record()).pendingTimeoutVoid, Is.False);
            Assert.That(players.Select(p => p.DisciplineWrites), Is.EqualTo(new[] { 0, 0 }));
        }

        [TestCase("hold")]
        [TestCase("leave")]
        [TestCase("report")]
        public async Task LifetimeVoidDoesNotCountAnOtherwiseOverdueNonReport(string path)
        {
            await Mutate(r =>
            {
                r.state = MatchRecordState.Pending;
                r.pendingUnixSeconds = 100;
                r.reports.Add(new MatchReport { playerIndex = 0, accepted = true });
            });
            await Finish(path, 7201);
            Assert.That((await Record()).pendingTimeoutVoid, Is.False);
            Assert.That(players.Select(p => p.DisciplineWrites), Is.EqualTo(new[] { 0, 0 }));
        }

        [Test]
        public async Task ConcurrentDuplicateStrikeReReadsIdempotencyKeyAfterConflict()
        {
            var record = await Abandoned();
            players[1].BeforeDisciplineWrite = () => discipline.Apply(record, 100);
            await discipline.Apply(record, 100);
            Assert.That(players[1].Attempts, Is.EqualTo(2));
            Assert.That(players[1].DisciplineWrites, Is.EqualTo(1));
            Assert.That(players[1].State.Discipline.Level, Is.EqualTo(1));
        }

        [Test]
        public async Task ConcurrentDifferentStrikeIsPreservedOnRetry()
        {
            var record = await Abandoned();
            var other = Clone(record);
            other.matchId = "other";
            players[1].BeforeDisciplineWrite = () => discipline.Apply(other, 100);
            await discipline.Apply(record, 101);
            Assert.That(players[1].State.Discipline.Level, Is.EqualTo(2));
            Assert.That(players[1].State.Discipline.StruckMatchIds, Is.EqualTo(new[] { MatchId, "other" }));
            Assert.That(players[1].DisciplineWrites, Is.EqualTo(2));
        }

        [Test]
        public async Task ConflictLimitLogsAndDoesNotEscapeAndLaterCallCanRecover()
        {
            var record = await Abandoned();
            players[1].FailConflict = true;
            await discipline.Apply(record, 100);
            Assert.That(players[1].Attempts, Is.EqualTo(3));
            Assert.That(logger.Warnings, Is.EqualTo(1));
            Assert.That(players[1].State.Discipline.Level, Is.Zero);
            players[1].FailConflict = false;
            await discipline.Apply(record, 101);
            Assert.That(players[1].State.Discipline.Level, Is.EqualTo(1));
        }

        [Test]
        public async Task StorageFailureCannotPreventSettlementOrClaimRelease()
        {
            players[1].FailRead = true;
            await hold.Presence(MatchId, "p0", true, 100);
            Assert.That((await hold.ResolveHold(MatchId, "p0", 110)).outcome, Is.EqualTo(HoldOutcome.Won));
            Assert.That((await Record()).state, Is.EqualTo(MatchRecordState.Settled));
            Assert.That(logger.Warnings, Is.EqualTo(1));
            await AssertReleased();
            players[1].FailRead = false;
            await rendezvous.Leave(MatchId, "p1", false, 111);
            Assert.That(players[1].State.Discipline.Level, Is.EqualTo(1));
        }

        [Test]
        public async Task MissingDisciplineAndLegacyListsAreInitializedAndMatchIdsAreCapped()
        {
            players[1].State.Discipline = null;
            var record = await Abandoned();
            await discipline.Apply(record, 100);
            Assert.That(players[1].State.Discipline.Level, Is.EqualTo(1));
            players[1].State.Discipline.NonReports = null;
            players[1].State.Discipline.StruckMatchIds = null;
            for (int i = 0; i < 21; i++)
            {
                record.matchId = "match-" + i;
                await discipline.Apply(record, 101 + i);
            }
            Assert.That(players[1].State.Discipline.NonReports, Is.Empty);
            Assert.That(players[1].State.Discipline.StruckMatchIds, Has.Count.EqualTo(20));
            Assert.That(players[1].State.Discipline.StruckMatchIds[0], Is.EqualTo("match-20"));
            Assert.That(players[1].State.Discipline.StruckMatchIds.Last(), Is.EqualTo("match-1"));
        }

        private async Task Finish(string path, long now)
        {
            if (path == "hold") await hold.ResolveHold(MatchId, "p0", now);
            else if (path == "leave") await rendezvous.Leave(MatchId, "p0", false, now);
            else await reporting.Report(MatchId, "p0", null, now);
        }

        private async Task<MatchRecord> Abandoned()
        {
            var record = await Record();
            record.state = MatchRecordState.Settled;
            record.forfeitedBy = record.abandonedBy = 1;
            return record;
        }

        private async Task Mutate(Action<MatchRecord> change)
        {
            var read = await matches.ReadAsync(MatchId);
            change(read.Record);
            await matches.WriteAsync(read.Record, read.WriteLock);
        }

        private async Task<MatchRecord> Record() => (await matches.ReadAsync(MatchId)).Record;
        private Task AssertReleased()
        {
            Assert.That(players.Select(p => p.State.ActiveMatch.matchId), Is.EqualTo(new string[] { null, null }));
            return Task.CompletedTask;
        }

        private static T Clone<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));

        private sealed class PlayerStore : ISettlementPlayerStore, IDisciplinePlayerStore
        {
            public PlayerState State;
            private int stateVersion = 1;
            private int disciplineVersion = 1;
            public int Settlements;
            public int DisciplineWrites;
            public int Attempts;
            public bool FailConflict;
            public bool FailRead;
            public Func<Task> BeforeDisciplineWrite;
            public PlayerStore(PlayerState state) { State = Clone(state); }

            public Task<LockedPlayerState> ReadForSettlementAsync() => Task.FromResult(new LockedPlayerState(Clone(State),
                PlayerStateKeys.All.ToDictionary(k => k, _ => stateVersion.ToString())));

            public Task WriteActiveMatchAsync(ActiveMatchRecord claim, LockedPlayerState read)
            {
                if (read.WriteLocks[PlayerStateKeys.Rating] != stateVersion.ToString()) throw new RecordConflictException("Claim changed.");
                State.ActiveMatch = Clone(claim);
                stateVersion++;
                return Task.CompletedTask;
            }

            public Task WriteForSettlementAsync(PlayerState state, IReadOnlyDictionary<string, string> tokens)
            {
                if (PlayerStateKeys.All.Any(k => tokens[k] != stateVersion.ToString())) throw new RecordConflictException("Player changed.");
                State.Rating = Clone(state.Rating);
                State.Rank = Clone(state.Rank);
                State.Inventory = Clone(state.Inventory);
                State.History = Clone(state.History);
                stateVersion++;
                Settlements++;
                return Task.CompletedTask;
            }

            public Task<LockedPlayerState> ReadDisciplineAsync()
            {
                if (FailRead) throw new InvalidOperationException("Storage unavailable.");
                return Task.FromResult(new LockedPlayerState(Clone(State),
                    new Dictionary<string, string> { [PlayerStateKeys.Discipline] = disciplineVersion.ToString() }));
            }

            public async Task WriteDisciplineAsync(DisciplineRecord value, LockedPlayerState read)
            {
                Attempts++;
                var hook = BeforeDisciplineWrite;
                BeforeDisciplineWrite = null;
                if (hook != null) await hook();
                if (FailConflict || read.WriteLocks[PlayerStateKeys.Discipline] != disciplineVersion.ToString())
                    throw new RecordConflictException("Discipline changed.");
                State.Discipline = Clone(value);
                disciplineVersion++;
                DisciplineWrites++;
            }
        }

        private sealed class WarningLogger : ILogger<MatchDiscipline>
        {
            public int Warnings;
            public IDisposable BeginScope<TState>(TState state) => null;
            public bool IsEnabled(LogLevel level) => true;
            public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception exception,
                Func<TState, Exception, string> formatter)
            {
                if (level == LogLevel.Warning) Warnings++;
            }
        }
    }
}
