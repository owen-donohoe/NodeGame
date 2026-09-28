using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Unity.Services.CloudCode;

namespace NodeWar.Backend
{
    /// <summary>
    /// Uploads a match log to the NodeWarCloud Cloud Code module
    /// (dotnet/NodeWarCloud), which is the referee: it replays the log,
    /// decides the result and settles both players' state.
    /// </summary>
    public sealed class UgsMatchReportService : IMatchReportService
    {
        public async Task<MatchReportingResult> ReportAsync(string matchId, byte[] log)
        {
            string requestedFor = BackendServices.Account.Current?.PlayerId;
            await GameServices.EnsureReadyAsync();
            string logBase64 = Convert.ToBase64String(log);
            MatchReportingResult result = await CloudCodeService.Instance.CallModuleEndpointAsync<MatchReportingResult>(
                BackendServices.CloudModule, "ReportMatch",
                new Dictionary<string, object> { { "matchId", matchId }, { "logBase64", logBase64 } });
            if (result?.playerState != null) BackendServices.Remember(result.playerState, requestedFor);
            return result;
        }
    }
}
