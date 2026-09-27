using System;
using System.Collections.Generic;

namespace NodeWar.Backend
{
    public static class InventoryClamp
    {
        /// <summary>Settlement-only demotion clamp; ownership and skins are retained.</summary>
        public static bool ClampToArena(InventoryRecord inventory, int arena)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            bool changed = PlayerStateLogic.NormalizeInventory(inventory);
            arena = Math.Max(0, arena);
            foreach (string baseId in new List<string>(inventory.Equipped.Variants.Keys))
            {
                if (!CatalogIds.TryParseVariant(inventory.Equipped.Variants[baseId], out string equippedBase, out int era) ||
                    equippedBase != baseId || era <= arena) continue;
                int highest = -1;
                foreach (string owned in inventory.OwnedVariants)
                    if (CatalogIds.TryParseVariant(owned, out string ownedBase, out int ownedEra) &&
                        ownedBase == baseId && ownedEra <= arena && ownedEra > highest)
                        highest = ownedEra;
                // Era zero is a starter grant, including for legacy inventories.
                if (highest < 0)
                {
                    highest = 0;
                    inventory.OwnedVariants.Add(CatalogIds.Variant(baseId, 0));
                }
                inventory.Equipped.Variants[baseId] = CatalogIds.Variant(baseId, highest);
                changed = true;
            }
            return changed;
        }
    }
}
