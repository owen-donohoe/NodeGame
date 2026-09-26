using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NodeWar.Backend;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace NodeWar.Cloud
{
    /// <summary>
    /// Player records in Cloud Save's protected access class: the player can
    /// read them, and only a service token (this module) can write them.
    /// </summary>
    public sealed class CloudSavePlayerRecordStore : IPlayerRecordStore
    {
        private readonly IGameApiClient api;
        private readonly IExecutionContext context;

        public CloudSavePlayerRecordStore(IGameApiClient api, IExecutionContext context)
        {
            this.api = api;
            this.context = context;
        }

        public async Task<PlayerState> ReadAsync()
        {
            var response = await api.CloudSaveData.GetProtectedItemsAsync(
                context, context.ServiceToken, context.ProjectId, context.PlayerId,
                PlayerStateKeys.All.ToList());

            var state = new PlayerState();
            foreach (Item item in response.Data.Results)
            {
                switch (item.Key)
                {
                    case PlayerStateKeys.Rating: state.Rating = Convert<RatingRecord>(item.Value); break;
                    case PlayerStateKeys.Rank: state.Rank = Convert<RankRecord>(item.Value); break;
                    case PlayerStateKeys.Inventory: state.Inventory = Convert<InventoryRecord>(item.Value); break;
                    case PlayerStateKeys.History: state.History = Convert<HistoryRecord>(item.Value); break;
                }
            }
            return state;
        }

        public async Task WriteAsync(PlayerState records)
        {
            var items = new List<SetItemBody>();
            if (records.Rating != null) items.Add(new SetItemBody(PlayerStateKeys.Rating, records.Rating));
            if (records.Rank != null) items.Add(new SetItemBody(PlayerStateKeys.Rank, records.Rank));
            if (records.Inventory != null) items.Add(new SetItemBody(PlayerStateKeys.Inventory, records.Inventory));
            if (records.History != null) items.Add(new SetItemBody(PlayerStateKeys.History, records.History));
            if (items.Count == 0) return;

            await api.CloudSaveData.SetProtectedItemBatchAsync(
                context, context.ServiceToken, context.ProjectId, context.PlayerId,
                new SetItemBatchBody(items));
        }

        // Item.Value arrives as whatever the API client deserialized (a JObject
        // for JSON objects); a round trip through Newtonsoft gives the record type.
        private static T Convert<T>(object value)
        {
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
        }
    }
}
