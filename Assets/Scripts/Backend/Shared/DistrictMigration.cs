using System;
using System.Collections.Generic;

namespace NodeWar.Backend
{
    /// <summary>Exact saved-data conversion only. Current packets and equip requests never use aliases.</summary>
    public static class DistrictMigration
    {
        public static bool IsActive(int type)
        {
            switch (type)
            {
                case 0: case 1: case 2: case 3: case 4: case 5: case 6:
                case 13: case 14: case 15: case 16: case 17: return true;
                default: return false;
            }
        }

        public static int SourceType(string id)
        {
            for (int type = 1; type < CatalogKeys.DistrictTableLength; type++)
            {
                if (id == CatalogKeys.DistrictLobbyId(type) || id == CatalogKeys.DistrictBase(type)) return type;
                for (int era = 0; era < CatalogIds.EraCount; era++)
                    if (id == CatalogIds.Variant(CatalogKeys.DistrictBase(type), era)) return type;
            }
            return 0;
        }

        public static int CanonicalType(int source)
        {
            switch (source)
            {
                case 7: case 9: return 4;
                case 8: case 10: return 16;
                case 12: return 17;
                case 11: return 0;
                default: return IsActive(source) ? source : 0;
            }
        }

        public static string[] Deck(string[] source, int slots)
        {
            var result = new string[slots];
            var seen = new HashSet<int>();
            for (int i = 0; i < slots; i++)
            {
                int type = CanonicalType(source != null && i < source.Length ? SourceType(source[i]) : 0);
                result[i] = type != 0 && type != 5 && seen.Add(type) ? CatalogKeys.DistrictLobbyId(type) : "";
            }
            return result;
        }

        public static PlayerState Detached(PlayerState state)
        {
            if (state?.Inventory == null || state.Rank == null)
                throw new InventoryValidationException("Load player state before equipping items.");
            var source = state.Inventory;
            return new PlayerState
            {
                Rank = state.Rank,
                Inventory = new InventoryRecord
                {
                    OwnedVariants = source.OwnedVariants == null ? null : new List<string>(source.OwnedVariants),
                    OwnedSkins = source.OwnedSkins == null ? null : new List<string>(source.OwnedSkins),
                    EquippedSuitIDs = source.EquippedSuitIDs == null ? null : (string[])source.EquippedSuitIDs.Clone(),
                    EquippedNodeIDs = source.EquippedNodeIDs == null ? null : (string[])source.EquippedNodeIDs.Clone(),
                    Equipped = source.Equipped == null ? null : new EquippedRecord
                    {
                        Variants = source.Equipped.Variants == null ? null :
                            new Dictionary<string, string>(source.Equipped.Variants, StringComparer.Ordinal),
                        Skins = source.Equipped.Skins == null ? null :
                            new Dictionary<string, string>(source.Equipped.Skins, StringComparer.Ordinal)
                    }
                }
            };
        }

        public static bool Apply(PlayerState state)
        {
            if (state?.Inventory == null || state.Rank == null) return false;
            var inventory = state.Inventory;
            bool changed = PlayerStateLogic.NormalizeInventory(inventory);
            // Ownership is historical: keep every old ID and union the replacement at the same era.
            foreach (string id in inventory.OwnedVariants.ToArray())
            {
                if (!CatalogIds.TryParseVariant(id, out string baseId, out int era)) continue;
                int source = SourceType(baseId);
                int target = CanonicalType(source);
                if (source == target || target == 0) continue;
                string replacement = CatalogIds.Variant(CatalogKeys.DistrictBase(target), era);
                if (!inventory.OwnedVariants.Contains(replacement))
                { inventory.OwnedVariants.Add(replacement); changed = true; }
                string skin = CatalogIds.DefaultSkin(CatalogKeys.DistrictBase(target));
                if (!inventory.OwnedSkins.Contains(skin))
                { inventory.OwnedSkins.Add(skin); changed = true; }
            }

            // Only groups with equipped historical sources are migrated. A second pass is a no-op.
            foreach (int target in new[] { 4, 16, 17 })
            {
                string targetBase = CatalogKeys.DistrictBase(target);
                bool hasSource = false;
                for (int source = 7; source <= 12; source++)
                    if (CanonicalType(source) == target &&
                        (inventory.Equipped.Variants.ContainsKey(CatalogKeys.DistrictBase(source)) ||
                         inventory.Equipped.Skins.ContainsKey(CatalogKeys.DistrictBase(source)))) hasSource = true;
                if (!hasSource) continue;
                int bestEra = -1;
                int bestSource = int.MaxValue;
                for (int source = 1; source < CatalogKeys.DistrictTableLength; source++)
                {
                    if (CanonicalType(source) != target) continue;
                    string sourceBase = CatalogKeys.DistrictBase(source);
                    if (!inventory.Equipped.Variants.TryGetValue(sourceBase, out string equipped) ||
                        !CatalogIds.TryParseVariant(equipped, out string parsedBase, out int equippedEra) ||
                        parsedBase != sourceBase || !inventory.OwnedVariants.Contains(equipped)) continue;
                    int usable = -1;
                    for (int era = 0; era <= equippedEra && era <= state.Rank.Arena; era++)
                        if (inventory.OwnedVariants.Contains(CatalogIds.Variant(sourceBase, era))) usable = era;
                    int priority = source == target ? -1 : source;
                    if (usable > bestEra || (usable == bestEra && priority < bestSource))
                    { bestEra = usable; bestSource = priority; }
                }
                if (bestEra >= 0)
                    changed |= Put(inventory.Equipped.Variants, targetBase, CatalogIds.Variant(targetBase, bestEra));
                string defaultSkin = CatalogIds.DefaultSkin(targetBase);
                if (!inventory.OwnedSkins.Contains(defaultSkin))
                { inventory.OwnedSkins.Add(defaultSkin); changed = true; }
                if (!inventory.Equipped.Skins.ContainsKey(targetBase))
                    changed |= Put(inventory.Equipped.Skins, targetBase, defaultSkin);
            }
            // Retired and unknown district equipment is removed, ownership is retained.
            foreach (string baseId in new List<string>(inventory.Equipped.Variants.Keys))
                if (baseId.StartsWith("district.", StringComparison.Ordinal) &&
                    (!IsActive(SourceType(baseId)) || SourceType(baseId) == 0))
                { inventory.Equipped.Variants.Remove(baseId); changed = true; }
            foreach (string baseId in new List<string>(inventory.Equipped.Skins.Keys))
                if (baseId.StartsWith("district.", StringComparison.Ordinal) &&
                    (!IsActive(SourceType(baseId)) || SourceType(baseId) == 0))
                { inventory.Equipped.Skins.Remove(baseId); changed = true; }
            if (inventory.EquippedNodeIDs != null)
            {
                string[] deck = Deck(inventory.EquippedNodeIDs, 2);
                if (!Equal(inventory.EquippedNodeIDs, deck))
                { inventory.EquippedNodeIDs = deck; changed = true; }
            }
            return InventoryClamp.ClampToArena(inventory, state.Rank.Arena) | changed;
        }

        private static bool Put(Dictionary<string, string> entries, string key, string value)
        {
            if (entries.TryGetValue(key, out string old) && old == value) return false;
            entries[key] = value;
            return true;
        }

        public static bool Equal(string[] a, string[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
