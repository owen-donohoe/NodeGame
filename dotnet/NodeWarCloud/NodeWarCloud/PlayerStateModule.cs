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
        private static readonly InventoryRules Inventory = new InventoryRules(ServerCatalog.Items);

        public PlayerStateModule(IGameApiClient api)
        {
            this.api = api;
        }

        [CloudCodeFunction("GetPlayerState")]
        public Task<PlayerState> GetPlayerState(IExecutionContext context)
        {
            return Service(context).GetAsync();
        }

        [CloudCodeFunction("Equip")]
        public Task<PlayerState> Equip(IExecutionContext context, EquippedRecord changes)
        {
            return Service(context).EquipAsync(changes);
        }

        private InventoryPlayerStateService Service(IExecutionContext context) =>
            new InventoryPlayerStateService(new CloudSavePlayerRecordStore(api, context), Inventory);
    }
}
