using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Model;

namespace NodeWar.Cloud
{
    public sealed class CloudSaveMatchRecordStore : IMatchRecordStore
    {
        private readonly IGameApiClient api;
        private readonly IExecutionContext context;

        public CloudSaveMatchRecordStore(IGameApiClient api, IExecutionContext context)
        { this.api = api; this.context = context; }

        public async Task<LockedMatchRecord> ReadAsync(string matchId)
        {
            var response = await api.CloudSaveData.GetPrivateCustomItemsAsync(context, context.ServiceToken,
                context.ProjectId, "match-" + matchId, new List<string> { "record" });
            var item = response.Data.Results.SingleOrDefault(i => i.Key == "record");
            return new LockedMatchRecord(item == null ? null :
                JsonConvert.DeserializeObject<MatchRecord>(JsonConvert.SerializeObject(item.Value)), item?.WriteLock);
        }

        public async Task WriteAsync(MatchRecord record, string expectedWriteLock)
        {
            if (expectedWriteLock == null)
            {
                // Cloud Save has no atomic create-if-absent: null bypasses write locks.
                // Re-check here to refuse a known existing record; cross-worker first
                // creation races still need validation in the live matchmaking spike.
                if ((await ReadAsync(record.matchId)).Record != null)
                    throw new RecordConflictException("Match record already exists.");
            }
            else if (expectedWriteLock.Length == 0)
                throw new ArgumentException("An expected write lock is required.");
            try
            {
                await api.CloudSaveData.SetPrivateCustomItemAsync(context, context.ServiceToken,
                    context.ProjectId, "match-" + record.matchId, new SetItemBody("record", record, expectedWriteLock));
            }
            catch (ApiException ex) when (ex.Response.StatusCode == HttpStatusCode.Conflict)
            { throw new RecordConflictException("Match record changed.", ex); }
        }

        public async Task SaveLog(string matchId, int playerIndex, string base64)
        {
            if (playerIndex != 0 && playerIndex != 1) throw new ArgumentOutOfRangeException(nameof(playerIndex));
            await api.CloudSaveData.SetPrivateCustomItemAsync(context, context.ServiceToken, context.ProjectId,
                "match-" + matchId, new SetItemBody(playerIndex == 0 ? "log-0" : "log-1", base64));
        }
    }
}
