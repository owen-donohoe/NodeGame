using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NodeWar.Backend
{
    public interface IInventoryService
    {
        /// <summary>Partial update; may fail. Display the returned state, never an optimistic guess.</summary>
        Task<PlayerState> EquipAsync(EquippedRecord changes);
    }

    /// <summary>Reported consistently by the server and offline fake for refused equip requests.</summary>
    public sealed class InventoryValidationException : Exception
    {
        public InventoryValidationException(string message) : base(message) { }
    }

    public static class InventoryEquip
    {
        /// <summary>Validate the entire patch before touching state, including legacy null fields.</summary>
        public static bool Apply(PlayerState state, EquippedRecord changes, Action<string, string, bool> validate)
        {
            if (changes == null) throw new InventoryValidationException("Equip changes must not be null.");
            if (state?.Inventory == null || state.Rank == null)
                throw new InventoryValidationException("Load player state before equipping items.");
            if (changes.Variants != null)
                foreach (var entry in changes.Variants) validate(entry.Key, entry.Value, false);
            if (changes.Skins != null)
                foreach (var entry in changes.Skins) validate(entry.Key, entry.Value, true);

            bool changed = PlayerStateLogic.NormalizeInventory(state.Inventory);
            changed |= Merge(state.Inventory.Equipped.Variants, changes.Variants);
            changed |= Merge(state.Inventory.Equipped.Skins, changes.Skins);
            return changed;
        }

        private static bool Merge(Dictionary<string, string> target, Dictionary<string, string> changes)
        {
            bool changed = false;
            if (changes == null) return false;
            foreach (var entry in changes)
                if (!target.TryGetValue(entry.Key, out string previous) || previous != entry.Value)
                {
                    target[entry.Key] = entry.Value;
                    changed = true;
                }
            return changed;
        }
    }

    /// <summary>
    /// Offline inventory over the same record store as LocalPlayerStateService.
    /// The caller supplies known bases; Shared never references simulation enums.
    /// Only generated variants and default skins exist in this fake catalog.
    /// </summary>
    public sealed class LocalInventoryService : IInventoryService
    {
        private readonly IPlayerRecordStore store;
        private readonly HashSet<string> bases;

        public LocalInventoryService(IPlayerRecordStore store, IEnumerable<string> baseIds)
        {
            this.store = store;
            bases = new HashSet<string>(baseIds, StringComparer.Ordinal);
        }

        public bool GrantDefaults(PlayerState state)
        {
            bool changed = PlayerStateLogic.NormalizeInventory(state.Inventory);
            var inventory = state.Inventory;
            foreach (string baseId in bases)
            {
                for (int era = 0; era < CatalogIds.EraCount && era <= state.Rank.HighestArena; era++)
                {
                    string id = CatalogIds.Variant(baseId, era);
                    if (!inventory.OwnedVariants.Contains(id)) { inventory.OwnedVariants.Add(id); changed = true; }
                }
                string skin = CatalogIds.DefaultSkin(baseId);
                if (!inventory.OwnedSkins.Contains(skin)) { inventory.OwnedSkins.Add(skin); changed = true; }
                if (!inventory.Equipped.Variants.ContainsKey(baseId))
                { inventory.Equipped.Variants.Add(baseId, CatalogIds.Variant(baseId, 0)); changed = true; }
                if (!inventory.Equipped.Skins.ContainsKey(baseId))
                { inventory.Equipped.Skins.Add(baseId, skin); changed = true; }
            }
            return changed;
        }

        public async Task<PlayerState> EquipAsync(EquippedRecord changes)
        {
            PlayerState state = await store.ReadAsync();
            bool changed = InventoryEquip.Apply(state, changes, (baseId, id, skin) =>
            {
                if (!bases.Contains(baseId)) throw new InventoryValidationException($"Unknown base '{baseId}'.");
                string parsedBase;
                int era = -1;
                bool known = skin
                    ? CatalogIds.TryParseSkin(id, out parsedBase) && id == CatalogIds.DefaultSkin(parsedBase)
                    : CatalogIds.TryParseVariant(id, out parsedBase, out era);
                if (!known) throw new InventoryValidationException($"Unknown item '{id}'.");
                if (parsedBase != baseId) throw new InventoryValidationException($"Item '{id}' is not for base '{baseId}'.");
                var owned = skin ? state.Inventory.OwnedSkins : state.Inventory.OwnedVariants;
                if (owned == null || !owned.Contains(id)) throw new InventoryValidationException($"Item '{id}' is not owned.");
                if (!skin && era > state.Rank.Arena) throw new InventoryValidationException($"Variant '{id}' is locked at the current arena.");
            });
            if (changed) await store.WriteAsync(new PlayerState { Inventory = state.Inventory });
            return state;
        }
    }
}
