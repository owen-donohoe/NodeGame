using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NodeWar.Backend;

namespace NodeWar.Cloud
{
    public sealed class RecordConflictException : Exception
    {
        public RecordConflictException(string message, Exception inner = null) : base(message, inner) { }
    }

    public sealed class LockedMatchRecord
    {
        public MatchRecord Record { get; }
        public string WriteLock { get; }
        public LockedMatchRecord(MatchRecord record, string writeLock) { Record = record; WriteLock = writeLock; }
    }

    public interface IMatchRecordStore
    {
        Task<LockedMatchRecord> ReadAsync(string matchId);
        // Updates an existing record; creation belongs to trusted matchmaking.
        Task WriteAsync(MatchRecord record, string expectedWriteLock);
        Task SaveLog(string matchId, int playerIndex, string base64);
    }

    public sealed class LockedPlayerState
    {
        public PlayerState State { get; }
        public IReadOnlyDictionary<string, string> WriteLocks { get; }
        public LockedPlayerState(PlayerState state, IReadOnlyDictionary<string, string> writeLocks)
        { State = state; WriteLocks = writeLocks; }
    }

    public interface ISettlementPlayerStore
    {
        Task<LockedPlayerState> ReadForSettlementAsync();
        // All four records, including history, must commit atomically or none do.
        Task WriteForSettlementAsync(PlayerState state, IReadOnlyDictionary<string, string> expectedWriteLocks);
    }

    public sealed class InMemoryMatchRecordStore : IMatchRecordStore
    {
        private readonly object gate = new object();
        private readonly Dictionary<string, string> records = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> versions = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<(string, int), string> logs = new Dictionary<(string, int), string>();

        public InMemoryMatchRecordStore(params MatchRecord[] initialRecords)
        {
            foreach (var record in initialRecords)
            {
                records.Add(record.matchId, JsonConvert.SerializeObject(record));
                versions.Add(record.matchId, 1);
            }
        }

        public Task<LockedMatchRecord> ReadAsync(string matchId)
        {
            lock (gate)
            {
                if (!records.TryGetValue(matchId, out string json))
                    return Task.FromResult(new LockedMatchRecord(null, null));
                return Task.FromResult(new LockedMatchRecord(JsonConvert.DeserializeObject<MatchRecord>(json),
                    versions[matchId].ToString(CultureInfo.InvariantCulture)));
            }
        }

        public Task WriteAsync(MatchRecord record, string expectedWriteLock)
        {
            if (string.IsNullOrEmpty(expectedWriteLock)) throw new ArgumentException("An expected write lock is required.");
            lock (gate)
            {
                if (!versions.TryGetValue(record.matchId, out int version) ||
                    version.ToString(CultureInfo.InvariantCulture) != expectedWriteLock)
                    throw new RecordConflictException("Match record changed.");
                records[record.matchId] = JsonConvert.SerializeObject(record);
                versions[record.matchId] = version + 1;
            }
            return Task.CompletedTask;
        }

        public Task SaveLog(string matchId, int playerIndex, string base64)
        {
            if (playerIndex != 0 && playerIndex != 1) throw new ArgumentOutOfRangeException(nameof(playerIndex));
            lock (gate) logs[(matchId, playerIndex)] = base64;
            return Task.CompletedTask;
        }

        public string ReadLog(string matchId, int playerIndex)
        {
            lock (gate) return logs.TryGetValue((matchId, playerIndex), out string log) ? log : null;
        }
    }
}
