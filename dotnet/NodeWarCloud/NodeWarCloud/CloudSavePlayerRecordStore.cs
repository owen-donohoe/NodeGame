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
    /// Player records in Cloud Save's protected access class: the player can
    /// read them, and only a service token (this module) can write them.
    /// </summary>
    public sealed class CloudSavePlayerRecordStore : IPlayerRecordStore, ISettlementPlayerStore
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

        public async Task<LockedPlayerState> ReadForSettlementAsync()
        {
            var response = await api.CloudSaveData.GetProtectedItemsAsync(
                context, context.ServiceToken, context.ProjectId, playerId,
                PlayerStateKeys.All.ToList());

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
                }
            }
            return new LockedPlayerState(state, locks);
        }

        public Task WriteAsync(PlayerState records) => WriteAsync(records, null);

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
