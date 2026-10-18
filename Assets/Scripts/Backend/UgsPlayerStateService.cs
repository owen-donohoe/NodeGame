using System.Threading.Tasks;

using Unity.Services.CloudCode;

namespace NodeWar.Backend
{
    public sealed class UgsHandleService : IHandleService
    {
        public async Task<PlayerState> SetHandleAsync(string name, string tag, string requestId)
        {
            await GameServices.EnsureReadyAsync();
            var args = new System.Collections.Generic.Dictionary<string, object>
            {
                { "name", name }, { "tag", tag }, { "requestId", requestId }
            };
            return await CloudCodeService.Instance.CallModuleEndpointAsync<PlayerState>(
                BackendServices.CloudModule, "SetHandle", args);
        }
    }

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
