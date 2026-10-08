using System.Threading.Tasks;
using NodeWar.Backend;

namespace NodeWar.Cloud
{
    /// <summary>One write for creation/grants, inventory-only writes for equip, no writes on refusal.</summary>
    public sealed class InventoryPlayerStateService : IPlayerStateService, IInventoryService
    {
        // Bounded so a live conflict storm cannot loop forever; matches
        // MatchReporting's retry bound for the same class of Cloud Save conflict.
        private const int RetryLimit = 3;

        private readonly ILockedPlayerRecordStore store;
        private readonly InventoryRules rules;

        public InventoryPlayerStateService(ILockedPlayerRecordStore store, InventoryRules rules)
        {
            this.store = store;
            this.rules = rules;
        }

        public async Task<PlayerState> GetAsync()
        {
            for (int attempt = 0; ; attempt++)
            {
                var (state, inventoryLock) = await store.ReadInventoryLockedAsync();
                state ??= new PlayerState();
                // Shared defaults migrate saved inventory before canonical grants and clamp.
                var changed = PlayerStateLogic.ApplyDefaults(state, rules.GrantDefaults);
                if (changed.Rating == null && changed.Rank == null && changed.Inventory == null && changed.History == null &&
                    changed.Discipline == null)
                    return state;
                try
                {
                    await store.WriteDefaultsLockedAsync(changed, inventoryLock);
                    if (changed.Discipline != null) state.Discipline = changed.Discipline;
                    return state;
                }
                // Re-read and re-apply: a settlement may have granted items or
                // clamped equipment since this read. Never replay a stale inventory.
                catch (RecordConflictException) when (attempt + 1 < RetryLimit) { }
            }
        }

        public async Task<PlayerState> EquipAsync(EquippedRecord changes)
        {
            // Do not get-or-create here: an invalid request must not cause even a defaults write.
            // Read and write Inventory with the same write lock, so a settlement
            // that clamps Inventory concurrently (e.g. a demotion) makes this
            // write conflict instead of silently overwriting the clamp. On
            // conflict, re-read fresh state and revalidate the same requested
            // change against it -- a change valid a moment ago (era now above the
            // new arena) must be refused, not blindly retried.
            for (int attempt = 0; ; attempt++)
            {
                var (state, inventoryLock) = await store.ReadInventoryLockedAsync();
                // Rules stage migration on a detached copy; refusal causes no migration write.
                if (!rules.Equip(state, changes)) return state;
                try
                {
                    await store.WriteInventoryLockedAsync(state.Inventory, inventoryLock);
                    return state;
                }
                catch (RecordConflictException) when (attempt + 1 < RetryLimit) { }
            }
        }
    }
}
