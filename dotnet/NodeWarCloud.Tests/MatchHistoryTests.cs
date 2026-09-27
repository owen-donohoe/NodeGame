using System.Linq;
using System.Threading.Tasks;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class MatchHistoryTests
    {
        [Test]
        public async Task SettledMatchGivesCorrectPerPlayerOutcomesForWinnerAndLoser()
        {
            var matches = new InMemoryMatchRecordStore(Settled("m1", "p0", "p1",
                winnerIndex: 0, rrDeltas: new[] { 25, -20 }, arenasAfter: new[] { 1, 0 }));
            var p0Store = HistoryStore("m1");
            var p1Store = HistoryStore("m1");

            var winnerHistory = await MatchHistory.ForPlayer("p0", matches, p0Store);
            var loserHistory = await MatchHistory.ForPlayer("p1", matches, p1Store);

            Assert.That(winnerHistory, Has.Count.EqualTo(1));
            Assert.That(winnerHistory[0].opponentPlayerId, Is.EqualTo("p1"));
            Assert.That(winnerHistory[0].state, Is.EqualTo(MatchRecordState.Settled));
            Assert.That(winnerHistory[0].won, Is.True);
            Assert.That(winnerHistory[0].rrDelta, Is.EqualTo(25));
            Assert.That(winnerHistory[0].arenaAfter, Is.EqualTo(1));

            Assert.That(loserHistory, Has.Count.EqualTo(1));
            Assert.That(loserHistory[0].opponentPlayerId, Is.EqualTo("p0"));
            Assert.That(loserHistory[0].won, Is.False);
            Assert.That(loserHistory[0].rrDelta, Is.EqualTo(-20));
            Assert.That(loserHistory[0].arenaAfter, Is.EqualTo(0));
        }

        [Test]
        public async Task HistoryOrderIsPreserved()
        {
            var matches = new InMemoryMatchRecordStore(
                Settled("m1", "p0", "p1", 0, new[] { 25, -20 }, new[] { 1, 0 }),
                Settled("m2", "p0", "p1", 1, new[] { -15, 15 }, new[] { 0, 1 }),
                Settled("m3", "p0", "p1", 0, new[] { 20, -18 }, new[] { 1, 0 }));
            // Newest first, deliberately not creation order.
            var store = HistoryStore("m3", "m1", "m2");

            var history = await MatchHistory.ForPlayer("p0", matches, store);

            Assert.That(history.Select(e => e.matchId), Is.EqualTo(new[] { "m3", "m1", "m2" }));
        }

        [Test]
        public async Task MissingRecordIsSkipped()
        {
            var matches = new InMemoryMatchRecordStore(Settled("m1", "p0", "p1", 0, new[] { 25, -20 }, new[] { 1, 0 }));
            var store = HistoryStore("m1", "gone", "also-gone");

            var history = await MatchHistory.ForPlayer("p0", matches, store);

            Assert.That(history.Select(e => e.matchId), Is.EqualTo(new[] { "m1" }));
        }

        [TestCase(MatchRecordState.Disputed)]
        [TestCase(MatchRecordState.Void)]
        [TestCase(MatchRecordState.Open)]
        [TestCase(MatchRecordState.Pending)]
        public async Task NonSettledEntriesCarryNoOutcome(MatchRecordState state)
        {
            var record = MatchRecords.Create("m1", new[] { "p0", "p1" },
                new[] { MatchRecordTests.Player(), MatchRecordTests.Player() }, 100, 1, 1, 1);
            record.state = state;
            var matches = new InMemoryMatchRecordStore(record);
            var store = HistoryStore("m1");

            var history = await MatchHistory.ForPlayer("p0", matches, store);

            Assert.That(history, Has.Count.EqualTo(1));
            Assert.That(history[0].state, Is.EqualTo(state));
            Assert.That(history[0].won, Is.Null);
            Assert.That(history[0].rrDelta, Is.Null);
            Assert.That(history[0].arenaAfter, Is.Null);
        }

        [Test]
        public async Task PlayerSeesOnlyMatchesInTheirOwnHistory()
        {
            // p0 and p1 played m1; a match p0 has never touched must never
            // surface just because some other list happens to name p0.
            var matches = new InMemoryMatchRecordStore(
                Settled("m1", "p0", "p1", 0, new[] { 25, -20 }, new[] { 1, 0 }),
                Settled("m2", "p2", "p3", 0, new[] { 25, -20 }, new[] { 1, 0 }));
            var store = HistoryStore("m1", "m2");

            var history = await MatchHistory.ForPlayer("p0", matches, store);

            Assert.That(history.Select(e => e.matchId), Is.EqualTo(new[] { "m1" }));
        }

        private static MatchRecord Settled(string matchId, string p0, string p1, int winnerIndex,
            int[] rrDeltas, int[] arenasAfter)
        {
            var record = MatchRecords.Create(matchId, new[] { p0, p1 },
                new[] { MatchRecordTests.Player(), MatchRecordTests.Player() }, 100, 1, 1, 1);
            record.state = MatchRecordState.Settled;
            record.outcomes = new[]
            {
                new MatchOutcome { won = winnerIndex == 0, rrDelta = rrDeltas[0], rrAfter = 1500 + rrDeltas[0], arenaAfter = arenasAfter[0] },
                new MatchOutcome { won = winnerIndex == 1, rrDelta = rrDeltas[1], rrAfter = 1500 + rrDeltas[1], arenaAfter = arenasAfter[1] }
            };
            return record;
        }

        private static InMemoryPlayerRecordStore HistoryStore(params string[] matchIdsNewestFirst)
        {
            var store = new InMemoryPlayerRecordStore();
            store.WriteAsync(new PlayerState { History = new HistoryRecord { MatchIds = matchIdsNewestFirst.ToList() } });
            return store;
        }
    }
}
