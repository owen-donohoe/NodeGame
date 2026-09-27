using System;
using System.Threading.Tasks;
using NodeWar.Backend;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace NodeWar.Cloud
{
    public sealed class MatchReportingModule
    {
        private readonly IGameApiClient api;
        private static readonly InventoryRules Inventory = new InventoryRules(ServerCatalog.Items);

        public MatchReportingModule(IGameApiClient api) { this.api = api; }

        [CloudCodeFunction("ReportMatch")]
        public Task<MatchReportingResult> ReportMatch(IExecutionContext context, string matchId, string logBase64)
        {
            if (!MatchLogUpload.TryDecode(logBase64, out byte[] bytes, out string error))
                return Task.FromResult(new MatchReportingResult { message = error });
            var reporting = new MatchReporting(new CloudSaveMatchRecordStore(api, context),
                playerId => new CloudSavePlayerRecordStore(api, context, playerId),
                new Referee(BalanceCatalog.Embedded), Inventory);
            return reporting.Report(matchId, context.PlayerId, bytes, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }
    }
}
