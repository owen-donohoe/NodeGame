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

    public sealed class MatchPresence
    {
        public long lastSeenUnixSeconds;
        public long holdSinceUnixSeconds;
    }

    public interface IMatchRecordStore
    {
        Task<LockedMatchRecord> ReadAsync(string matchId);
        // Null lock creates a missing record for trusted matchmaking. Updates require a lock.
        Task WriteAsync(MatchRecord record, string expectedWriteLock);
        Task SaveLog(string matchId, int playerIndex, string base64);
        Task<MatchPresence[]> ReadPresenceAsync(string matchId);
        // Each slot writes its own key, without a record write lock.
        Task WritePresenceAsync(string matchId, int slot, MatchPresence presence);
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
        // Atomically checks the rating lock even when the claim key is missing.
        Task WriteActiveMatchAsync(ActiveMatchRecord claim, LockedPlayerState read);
        // All four records, including history, must commit atomically or none do.
        Task WriteForSettlementAsync(PlayerState state, IReadOnlyDictionary<string, string> expectedWriteLocks);
    }

    public static class ActiveMatchClaims
    {
        public const long LifetimeSeconds = 2 * 60 * 60;
        // How long a Pending match (one accepted report, or one forfeiter waiting
        // on the other side) may sit before it voids. Shared by MatchReporting
        // and MatchRendezvous so a known tag never means two different windows.
        public const long PendingTimeoutSeconds = 10 * 60;
        private const int RetryLimit = 3;

        /// <summary>
        /// True while both players in <paramref name="record"/> still hold an
        /// unexpired claim naming this match. Shared by MatchReporting and
        /// MatchRendezvous: whichever caller notices a lost claim first voids
        /// the record.
        /// </summary>
        public static async Task<bool> HoldsClaims(Func<string, ISettlementPlayerStore> players, MatchRecord record, long now)
        {
            foreach (string id in record.playerIds)
            {
                var claim = (await players(id).ReadForSettlementAsync()).State.ActiveMatch;
                if (claim?.matchId != record.matchId || claim.expiresUnixSeconds <= now) return false;
            }
            return true;
        }

        public static async Task<ActiveMatchRecord> Claim(ISettlementPlayerStore store, string matchId, long now,
            long? expiresUnixSeconds = null)
        {
            for (int attempt = 0; ; attempt++)
            {
                var read = await store.ReadForSettlementAsync();
                var claim = read.State.ActiveMatch;
                if (claim != null && claim.expiresUnixSeconds > now)
                    return claim.matchId == matchId ? claim : null;
                // An expired retry must not revive a snapshot's old lease.
                if (claim?.matchId == matchId) return null;
                claim = new ActiveMatchRecord { matchId = matchId,
                    expiresUnixSeconds = expiresUnixSeconds ?? now + LifetimeSeconds };
                try { await store.WriteActiveMatchAsync(claim, read); return claim; }
                catch (RecordConflictException) when (attempt + 1 < RetryLimit) { }
            }
        }

        public static async Task Release(ISettlementPlayerStore store, string matchId)
        {
            try
            {
                for (int attempt = 0; ; attempt++)
                {
                    var read = await store.ReadForSettlementAsync();
                    if (read.State.ActiveMatch?.matchId != matchId) return;
                    try { await store.WriteActiveMatchAsync(new ActiveMatchRecord(), read); return; }
                    catch (RecordConflictException) when (attempt + 1 < RetryLimit) { }
                }
            }
            // Cleanup never reverses a terminal record. The lease bounds a failed release.
            catch (Exception) { }
        }
    }

    public sealed class InMemoryMatchRecordStore : IMatchRecordStore
    {
        private readonly object gate = new object();
        private readonly Dictionary<string, string> records = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> versions = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<(string, int), string> logs = new Dictionary<(string, int), string>();
        private readonly Dictionary<(string, int), string> presence = new Dictionary<(string, int), string>();

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
            lock (gate)
            {
                if (expectedWriteLock == null && !records.ContainsKey(record.matchId))
                {
                    records.Add(record.matchId, JsonConvert.SerializeObject(record));
                    versions.Add(record.matchId, 1);
                    return Task.CompletedTask;
                }
                if (expectedWriteLock == null) throw new RecordConflictException("Match record already exists.");
                if (expectedWriteLock.Length == 0) throw new ArgumentException("An expected write lock is required.");
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

        public Task<MatchPresence[]> ReadPresenceAsync(string matchId)
        {
            lock (gate)
            {
                var slots = new MatchPresence[2];
                for (int slot = 0; slot < 2; slot++)
                    slots[slot] = presence.TryGetValue((matchId, slot), out string json)
                        ? JsonConvert.DeserializeObject<MatchPresence>(json) : new MatchPresence();
                return Task.FromResult(slots);
            }
        }

        public Task WritePresenceAsync(string matchId, int slot, MatchPresence value)
        {
            if (slot != 0 && slot != 1) throw new ArgumentOutOfRangeException(nameof(slot));
            lock (gate) presence[(matchId, slot)] = JsonConvert.SerializeObject(value);
            return Task.CompletedTask;
        }

        public string ReadLog(string matchId, int playerIndex)
        {
            lock (gate) return logs.TryGetValue((matchId, playerIndex), out string log) ? log : null;
        }
    }
}
