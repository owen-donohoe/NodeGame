using System.Threading.Tasks;
using NodeWar.Backend;

namespace NodeWar.Cloud
{
    /// <summary>One write for creation/grants, inventory-only writes for equip, no writes on refusal.</summary>
    public sealed class InventoryPlayerStateService : IPlayerStateService, IInventoryService
    {
        private readonly IPlayerRecordStore store;
        private readonly InventoryRules rules;

        public InventoryPlayerStateService(IPlayerRecordStore store, InventoryRules rules)
        {
            this.store = store;
            this.rules = rules;
        }

        public Task<PlayerState> GetAsync() => PlayerStateLogic.GetOrCreateAsync(store, rules.GrantDefaults);

        public async Task<PlayerState> EquipAsync(EquippedRecord changes)
        {
            // Do not get-or-create here: an invalid request must not cause even a defaults write.
            PlayerState state = await store.ReadAsync();
            if (rules.Equip(state, changes))
                await store.WriteAsync(new PlayerState { Inventory = state.Inventory });
            return state;
        }
    }
}
