using System.Collections.Generic;
using System.Threading.Tasks;

using Unity.Services.CloudCode;

namespace NodeWar.Backend
{
    /// <summary>
    /// The caller's ranked match history from the NodeWarCloud Cloud Code
    /// module (dotnet/NodeWarCloud). No parameters: the server reads the
    /// caller's own Player ID from the execution context, so nobody can ask
    /// for someone else's history.
    /// </summary>
    public sealed class UgsMatchHistoryService : IMatchHistoryService
    {
        public async Task<List<MatchHistoryEntry>> GetAsync()
        {
            await GameServices.EnsureReadyAsync();
            return await CloudCodeService.Instance.CallModuleEndpointAsync<List<MatchHistoryEntry>>(
                BackendServices.CloudModule, "GetMatchHistory");
        }
    }
}
