using System;
using System.Collections.Generic;
using NodeWar.Backend;
using NodeWar.Progression;

namespace NodeWar.Cloud
{
    /// <summary>Catalog ownership and equip rules, with no Cloud Save I/O.</summary>
    public sealed class InventoryRules
    {
        private readonly IReadOnlyList<CatalogItem> catalog;
        private readonly Dictionary<string, CatalogItem> items = new Dictionary<string, CatalogItem>(StringComparer.Ordinal);
        private readonly HashSet<string> bases = new HashSet<string>(StringComparer.Ordinal);

        public InventoryRules(IReadOnlyList<CatalogItem> catalog)
        {
            var errors = CatalogValidation.Validate(catalog, CatalogIds.EraCount);
            if (errors.Count > 0) throw new ArgumentException(string.Join("\n", errors), nameof(catalog));
            this.catalog = catalog;
            foreach (var item in catalog) { items.Add(item.Id, item); bases.Add(item.BaseId); }
        }

        public bool GrantDefaults(PlayerState state)
        {
            var inventory = state.Inventory;
            bool changed = PlayerStateLogic.NormalizeInventory(inventory);
            var grants = EraUnlocks.GrantsFor(catalog, state.Rank.HighestArena, inventory.OwnedVariants);
            if (grants.Count > 0) { inventory.OwnedVariants.AddRange(grants); changed = true; }
            foreach (var item in catalog)
            {
                if (item.Retired) continue;
                if (item.Kind == CatalogItemKind.Skin && item.Id == CatalogIds.DefaultSkin(item.BaseId))
                {
                    if (!inventory.OwnedSkins.Contains(item.Id))
                    { inventory.OwnedSkins.Add(item.Id); changed = true; }
                    if (!inventory.Equipped.Skins.ContainsKey(item.BaseId))
                    { inventory.Equipped.Skins.Add(item.BaseId, item.Id); changed = true; }
                }
                else if (item.Kind == CatalogItemKind.Variant && item.Era == 0 &&
                    inventory.OwnedVariants.Contains(item.Id) && !inventory.Equipped.Variants.ContainsKey(item.BaseId))
                { inventory.Equipped.Variants.Add(item.BaseId, item.Id); changed = true; }
            }
            return changed;
        }

        public bool Equip(PlayerState state, EquippedRecord changes)
        {
            return InventoryEquip.Apply(state, changes, (baseId, id, skin) =>
            {
                if (!bases.Contains(baseId)) throw new InventoryValidationException($"Unknown base '{baseId}'.");
                if (id == null || !items.TryGetValue(id, out var item))
                    throw new InventoryValidationException($"Unknown item '{id}'.");
                var expectedKind = skin ? CatalogItemKind.Skin : CatalogItemKind.Variant;
                if (item.Kind != expectedKind || item.BaseId != baseId)
                    throw new InventoryValidationException($"Item '{id}' is not a {expectedKind} for base '{baseId}'.");
                if (item.Retired) throw new InventoryValidationException($"Item '{id}' is retired.");
                var owned = skin ? state.Inventory.OwnedSkins : state.Inventory.OwnedVariants;
                if (owned == null || !owned.Contains(id)) throw new InventoryValidationException($"Item '{id}' is not owned.");
                if (!skin && !EraUnlocks.IsUsable(item, state.Rank.Arena))
                    throw new InventoryValidationException($"Variant '{id}' is locked at the current arena.");
            });
        }
    }
}
