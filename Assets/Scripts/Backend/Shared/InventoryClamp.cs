using System;
using System.Collections.Generic;

namespace NodeWar.Backend
{
    public static class InventoryClamp
    {
        /// <summary>
        /// Settlement-only demotion clamp; ownership and skins are retained. A tree suit
        /// must have an equipped node available at the arena, else it drops to the root.
        /// Everything else (districts, non-tree suits) keeps the era rule.
        /// </summary>
        public static bool ClampToArena(InventoryRecord inventory, int arena)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            bool changed = PlayerStateLogic.NormalizeInventory(inventory);
            arena = Math.Max(0, arena);
            foreach (string baseId in new List<string>(inventory.Equipped.Variants.Keys))
            {
                if (SuitTree.IsTreeSuit(baseId)) { changed |= ClampSuit(inventory, baseId, arena); continue; }
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

        private static bool ClampSuit(InventoryRecord inventory, string baseId, int arena)
        {
            string equipped = inventory.Equipped.Variants[baseId];
            if (SuitTree.TryGet(equipped, out var node) && node.BaseId == baseId && SuitTree.IsAvailable(node, arena))
                return false;
            // The root is the floor: a root before its debut arena stays as it is, since
            // there is nothing lower to demote to (the equip check refuses it, the clamp does not).
            string root = SuitTree.Root(baseId).Id;
            if (equipped == root) return false;
            // The root is a starter grant, including for legacy inventories.
            if (!inventory.OwnedVariants.Contains(root)) inventory.OwnedVariants.Add(root);
            inventory.Equipped.Variants[baseId] = root;
            return true;
        }
    }
}
