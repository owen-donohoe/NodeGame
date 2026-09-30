using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using NodeWar.Backend;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace NodeWar.Cloud
{
    /// <summary>
    /// A single-record locked read/write for Inventory. Equip only ever
    /// touches Inventory, never all four records, so it does not need (and must
    /// not take) ISettlementPlayerStore's four-record lock set. Reading the
    /// Inventory write lock with the read, and writing with it, means a
    /// concurrent settlement's clamp (which writes Inventory in its own batch)
    /// conflicts with a stale Equip instead of being silently overwritten by it.
    /// </summary>
    public interface ILockedPlayerRecordStore : IPlayerRecordStore
    {
        // Includes all state records so Get can initialize only missing records.
        Task<(PlayerState State, string InventoryWriteLock)> ReadInventoryLockedAsync();
        Task WriteInventoryLockedAsync(InventoryRecord inventory, string expectedWriteLock);
        Task WriteDefaultsLockedAsync(PlayerState records, string inventoryWriteLock);
    }

    /// <summary>
    /// Player records in Cloud Save's protected access class: the player can
    /// read them, and only a service token (this module) can write them.
    /// </summary>
    public sealed class CloudSavePlayerRecordStore : IPlayerRecordStore, ISettlementPlayerStore, ILockedPlayerRecordStore, IDisciplinePlayerStore
    {
        private readonly IGameApiClient api;
        private readonly IExecutionContext context;
        private readonly string playerId;

        public CloudSavePlayerRecordStore(IGameApiClient api, IExecutionContext context, string playerId = null)
        {
            this.api = api;
            this.context = context;
            this.playerId = playerId ?? context.PlayerId;
        }

        public async Task<PlayerState> ReadAsync()
        {
            return (await ReadForSettlementAsync()).State;
        }

        public Task<LockedPlayerState> ReadForSettlementAsync() => ReadLockedAsync(
            PlayerStateKeys.All.Concat(new[] { PlayerStateKeys.ActiveMatch, PlayerStateKeys.Discipline }).ToList());

        public Task<LockedPlayerState> ReadDisciplineAsync() => ReadLockedAsync(
            new List<string> { PlayerStateKeys.Discipline, PlayerStateKeys.Rating });

        private async Task<LockedPlayerState> ReadLockedAsync(List<string> keys)
        {
            var response = await api.CloudSaveData.GetProtectedItemsAsync(
                context, context.ServiceToken, context.ProjectId, playerId,
                keys);

            var state = new PlayerState();
            var locks = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Item item in response.Data.Results)
            {
                locks[item.Key] = item.WriteLock;
                switch (item.Key)
                {
                    case PlayerStateKeys.Rating: state.Rating = Convert<RatingRecord>(item.Value); break;
                    case PlayerStateKeys.Rank: state.Rank = Convert<RankRecord>(item.Value); break;
                    case PlayerStateKeys.Inventory: state.Inventory = Convert<InventoryRecord>(item.Value); break;
                    case PlayerStateKeys.History: state.History = Convert<HistoryRecord>(item.Value); break;
                    case PlayerStateKeys.ActiveMatch: state.ActiveMatch = Convert<ActiveMatchRecord>(item.Value); break;
                    case PlayerStateKeys.Discipline: state.Discipline = Convert<DisciplineRecord>(item.Value); break;
                }
            }
            return new LockedPlayerState(state, locks);
        }

        public async Task WriteActiveMatchAsync(ActiveMatchRecord claim, LockedPlayerState read)
        {
            if (read.State.Rating == null || !read.WriteLocks.TryGetValue(PlayerStateKeys.Rating, out string ratingLock) ||
                string.IsNullOrEmpty(ratingLock))
                throw new InvalidOperationException("Initialize player records before claiming a match.");
            read.WriteLocks.TryGetValue(PlayerStateKeys.ActiveMatch, out string claimLock);
            try
            {
                // Null locks bypass CAS in Cloud Save. Including the existing rating
                // key makes even the first claim a conditional, atomic batch.
                await api.CloudSaveData.SetProtectedItemBatchAsync(context, context.ServiceToken,
                    context.ProjectId, playerId, new SetItemBatchBody(new List<SetItemBody>
                    {
                        new SetItemBody(PlayerStateKeys.Rating, read.State.Rating, ratingLock),
                        new SetItemBody(PlayerStateKeys.ActiveMatch, claim, claimLock)
                    }));
            }
            catch (ApiException ex) when (ex.Response.StatusCode == HttpStatusCode.Conflict)
            { throw new RecordConflictException("Active match changed.", ex); }
        }

        public async Task WriteAsync(PlayerState records)
        {
            await WriteAsync(records, null);
            await WriteDisciplineDefault(records);
        }

        public async Task WriteDisciplineAsync(DisciplineRecord discipline, LockedPlayerState read)
        {
            read.WriteLocks.TryGetValue(PlayerStateKeys.Discipline, out string token);
            var items = new List<SetItemBody> { new SetItemBody(PlayerStateKeys.Discipline, discipline, token) };
            if (string.IsNullOrEmpty(token))
            {
                // Cloud Save has no create-if-absent. Fence first creation with
                // rating, as claims do; later discipline writes use only its key.
                if (read.State.Discipline != null || read.State.Rating == null ||
                    !read.WriteLocks.TryGetValue(PlayerStateKeys.Rating, out string ratingLock) || string.IsNullOrEmpty(ratingLock))
                    throw new InvalidOperationException("Initialize player records before writing discipline.");
                items.Add(new SetItemBody(PlayerStateKeys.Rating, read.State.Rating, ratingLock));
            }
            try
            {
                await api.CloudSaveData.SetProtectedItemBatchAsync(context, context.ServiceToken,
                    context.ProjectId, playerId, new SetItemBatchBody(items));
            }
            catch (ApiException ex) when (ex.Response.StatusCode == HttpStatusCode.Conflict)
            { throw new RecordConflictException("Discipline changed.", ex); }
        }

        private async Task WriteDisciplineDefault(PlayerState records)
        {
            if (records.Discipline == null) return;
            for (int attempt = 0; ; attempt++)
            {
                var read = await ReadDisciplineAsync();
                if (read.State.Discipline != null)
                {
                    records.Discipline = read.State.Discipline;
                    return;
                }
                try { await WriteDisciplineAsync(records.Discipline, read); return; }
                catch (RecordConflictException) when (attempt + 1 < 3) { }
            }
        }

        public async Task<(PlayerState State, string InventoryWriteLock)> ReadInventoryLockedAsync()
        {
            var read = await ReadForSettlementAsync();
            read.WriteLocks.TryGetValue(PlayerStateKeys.Inventory, out string inventoryLock);
            return (read.State, inventoryLock);
        }

        // Other records here are missing defaults only. Existing inventory must
        // use the lock from the same read as the normalization/grant decision.
        public async Task WriteDefaultsLockedAsync(PlayerState records, string inventoryWriteLock)
        {
            await WriteAsync(records, PlayerStateKeys.All.ToDictionary(key => key,
                key => key == PlayerStateKeys.Inventory ? inventoryWriteLock : null));
            await WriteDisciplineDefault(records);
        }

        // Writes Inventory alone, using only the Inventory write lock: a stale
        // lock (a settlement clamped the same key meanwhile) conflicts here.
        public Task WriteInventoryLockedAsync(InventoryRecord inventory, string expectedWriteLock) =>
            WriteAsync(new PlayerState { Inventory = inventory },
                new Dictionary<string, string> { [PlayerStateKeys.Inventory] = expectedWriteLock });

        public Task WriteForSettlementAsync(PlayerState state, IReadOnlyDictionary<string, string> expectedWriteLocks)
        {
            if (state?.Rating == null || state.Rank == null || state.Inventory == null || state.History == null)
                throw new ArgumentException("Settlement requires all four player records.", nameof(state));
            foreach (string key in PlayerStateKeys.All)
                if (expectedWriteLocks == null || !expectedWriteLocks.TryGetValue(key, out string token) || string.IsNullOrEmpty(token))
                    throw new ArgumentException("Settlement requires every player record's write lock.", nameof(expectedWriteLocks));
            return WriteAsync(state, expectedWriteLocks);
        }

        private async Task WriteAsync(PlayerState records, IReadOnlyDictionary<string, string> locks)
        {
            var items = new List<SetItemBody>();
            if (records.Rating != null) items.Add(new SetItemBody(PlayerStateKeys.Rating, records.Rating, locks?[PlayerStateKeys.Rating]));
            if (records.Rank != null) items.Add(new SetItemBody(PlayerStateKeys.Rank, records.Rank, locks?[PlayerStateKeys.Rank]));
            if (records.Inventory != null) items.Add(new SetItemBody(PlayerStateKeys.Inventory, records.Inventory, locks?[PlayerStateKeys.Inventory]));
            if (records.History != null) items.Add(new SetItemBody(PlayerStateKeys.History, records.History, locks?[PlayerStateKeys.History]));
            if (items.Count == 0) return;

            try
            {
                await api.CloudSaveData.SetProtectedItemBatchAsync(
                    context, context.ServiceToken, context.ProjectId, playerId, new SetItemBatchBody(items));
            }
            catch (ApiException ex) when (ex.Response.StatusCode == HttpStatusCode.Conflict)
            { throw new RecordConflictException("Player records changed.", ex); }
        }

        // Item.Value arrives as whatever the API client deserialized (a JObject
        // for JSON objects); a round trip through Newtonsoft gives the record type.
        private static T Convert<T>(object value)
        {
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
        }
    }
}
