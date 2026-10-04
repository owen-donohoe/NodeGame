using System;
using System.Threading.Tasks;
using NUnit.Framework;
using NodeWar.Backend;

namespace NodeWar.Cloud.Tests
{
    public class MatchRecordStoreTests
    {
        [Test]
        public async Task ReadsAreDetachedAndStaleWritesCannotOverwriteTheWinner()
        {
            var record = MatchRecords.Create("m", new[] { "p0", "p1" },
                new[] { MatchRecordTests.Player(), MatchRecordTests.Player() }, 1, 1, 1, 1);
            var store = new InMemoryMatchRecordStore(record);
            var first = await store.ReadAsync("m");
            var stale = await store.ReadAsync("m");
            first.Record.state = MatchRecordState.Pending;
            Assert.That((await store.ReadAsync("m")).Record.state, Is.EqualTo(MatchRecordState.Open));
            await store.WriteAsync(first.Record, first.WriteLock);
            stale.Record.state = MatchRecordState.Void;
            Assert.ThrowsAsync<RecordConflictException>(() => store.WriteAsync(stale.Record, stale.WriteLock));
            Assert.That((await store.ReadAsync("m")).Record.state, Is.EqualTo(MatchRecordState.Pending));
            Assert.ThrowsAsync<RecordConflictException>(() => store.WriteAsync(first.Record, null));
            Assert.That((await store.ReadAsync("missing")).Record, Is.Null);
        }

        [Test]
        public async Task DuplicateCreationConflictsAndPreservesOriginalRecord()
        {
            var store = new InMemoryMatchRecordStore();
            await store.WriteAsync(new MatchRecord { matchId = "m", state = MatchRecordState.Pending }, null);
            Assert.ThrowsAsync<RecordConflictException>(() =>
                store.WriteAsync(new MatchRecord { matchId = "m", state = MatchRecordState.Open }, null));
            Assert.That((await store.ReadAsync("m")).Record.state, Is.EqualTo(MatchRecordState.Pending));
        }

        [Test]
        public void UpdateOfMissingRecordConflicts()
        {
            var store = new InMemoryMatchRecordStore();
            Assert.ThrowsAsync<RecordConflictException>(() =>
                store.WriteAsync(new MatchRecord { matchId = "missing" }, "stale-lock"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EmptyLockRemainsAnArgumentErrorLikeProduction(bool exists)
        {
            var record = new MatchRecord { matchId = "m" };
            var store = exists ? new InMemoryMatchRecordStore(record) : new InMemoryMatchRecordStore();
            Assert.ThrowsAsync<ArgumentException>(() => store.WriteAsync(record, ""));
        }

        [Test]
        public async Task LogsAreStoredSeparatelyForEachPlayerWithoutChangingRecordLock()
        {
            var store = new InMemoryMatchRecordStore(new MatchRecord { matchId = "m" });
            string token = (await store.ReadAsync("m")).WriteLock;
            await store.SaveLog("m", 0, "first");
            await store.SaveLog("m", 1, "second");
            Assert.That(store.ReadLog("m", 0), Is.EqualTo("first"));
            Assert.That(store.ReadLog("m", 1), Is.EqualTo("second"));
            Assert.That((await store.ReadAsync("m")).WriteLock, Is.EqualTo(token));
        }
    }
}
