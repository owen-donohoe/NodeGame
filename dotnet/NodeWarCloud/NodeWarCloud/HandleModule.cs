using System;
using System.Threading.Tasks;
using NodeWar.Backend;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace NodeWar.Cloud
{
    public class HandleModule
    {
        private readonly IGameApiClient api;
        public HandleModule(IGameApiClient api) { this.api = api; }

        [CloudCodeFunction("SetHandle")]
        public Task<PlayerState> SetHandle(IExecutionContext context, string name, string tag, string requestId) =>
            Service(context).SetHandleAsync(context.PlayerId, name, tag, requestId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        [CloudCodeFunction("GetPublicProfile")]
        public Task<ProfileRecord> GetPublicProfile(IExecutionContext context, string playerId) =>
            Service(context).GetPublicProfileAsync(playerId);

        private HandleService Service(IExecutionContext context) => new HandleService(
            new CloudSaveHandleRegistryStore(api, context),
            target => new CloudSavePlayerRecordStore(api, context, target), EmbeddedBlockedNames.Names);
    }
}
