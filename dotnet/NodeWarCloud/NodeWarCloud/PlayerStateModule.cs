using System.Threading.Tasks;
using NodeWar.Backend;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace NodeWar.Cloud
{
    /// <summary>
    /// Endpoints over the calling player's own state. The player is always the
    /// one the request is authenticated as; no endpoint takes a player ID.
    /// </summary>
    public class PlayerStateModule
    {
        private readonly IGameApiClient api;

        public PlayerStateModule(IGameApiClient api)
        {
            this.api = api;
        }

        [CloudCodeFunction("GetPlayerState")]
        public Task<PlayerState> GetPlayerState(IExecutionContext context)
        {
            return PlayerStateLogic.GetOrCreateAsync(new CloudSavePlayerRecordStore(api, context));
        }
    }
}
