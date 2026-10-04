using System.Threading.Tasks;

using Unity.Services.CloudCode;

namespace NodeWar.Backend
{
    /// <summary>
    /// Player state from the NodeWarCloud Cloud Code module
    /// (dotnet/NodeWarCloud). The module reads and writes Cloud Save's
    /// protected records; this client can only ask.
    /// </summary>
    public sealed class UgsPlayerStateService : IPlayerStateService
    {
        public async Task<PlayerState> GetAsync()
        {
            await GameServices.EnsureReadyAsync();
            return await CloudCodeService.Instance.CallModuleEndpointAsync<PlayerState>(
                BackendServices.CloudModule, "GetPlayerState");
        }
    }
}
