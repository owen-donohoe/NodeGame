using System.Collections.Generic;
using System.Threading.Tasks;
using NodeWar.Backend;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace NodeWar.Cloud
{
    public sealed class MatchHistoryModule
    {
        private readonly IGameApiClient api;

        public MatchHistoryModule(IGameApiClient api) { this.api = api; }

        // context.PlayerId only: a caller can never read another player's history.
        [CloudCodeFunction("GetMatchHistory")]
        public Task<List<MatchHistoryEntry>> GetMatchHistory(IExecutionContext context)
        {
            return MatchHistory.ForPlayer(context.PlayerId, new CloudSaveMatchRecordStore(api, context),
                new CloudSavePlayerRecordStore(api, context));
        }
    }
}
