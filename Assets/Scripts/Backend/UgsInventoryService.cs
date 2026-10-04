using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode;

namespace NodeWar.Backend
{
    public sealed class UgsInventoryService : IInventoryService
    {
        public async Task<PlayerState> EquipAsync(EquippedRecord changes)
        {
            await GameServices.EnsureReadyAsync();
            return await CloudCodeService.Instance.CallModuleEndpointAsync<PlayerState>(
                BackendServices.CloudModule, "Equip", new Dictionary<string, object> { { "changes", changes } });
        }
    }
}
