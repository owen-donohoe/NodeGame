using System;
using System.Collections.Generic;
using NodeWar.Backend;

namespace NodeWar.Lobby
{
    /// <summary>Read-only inventory decisions, independent of Unity and draft slots.</summary>
    public static class EraChips
    {
        public sealed class Chip
        {
            public string ID { get; }
            public int Era { get; }
            public bool Owned { get; }
            public bool Usable { get; }
            public bool Equipped { get; }
            public bool Selectable => Owned && Usable && !Equipped;

            internal Chip(string id, int era, bool owned, bool usable, bool equipped)
            {
                ID = id;
                Era = era;
                Owned = owned;
                Usable = usable;
                Equipped = equipped;
            }
        }

        public static Chip[] ForItem(PlayerState state, string baseId)
        {
            if (string.IsNullOrEmpty(baseId)) return Array.Empty<Chip>();
            var result = new Chip[CatalogIds.EraCount];
            InventoryRecord inventory = state?.Inventory;
            for (int era = 0; era < result.Length; era++)
            {
                string id = CatalogIds.Variant(baseId, era);
                result[era] = new Chip(id, era, inventory?.OwnedVariants?.Contains(id) == true,
                    state?.Rank != null && era <= state.Rank.Arena,
                    IsEquipped(inventory?.Equipped?.Variants, baseId, id));
            }
            return result;
        }

        /// <summary>Skins have no arena gate. Hide the row unless there is a choice.</summary>
        public static Chip[] SkinsForItem(PlayerState state, string baseId)
        {
            if (string.IsNullOrEmpty(baseId) || state?.Inventory?.OwnedSkins == null)
                return Array.Empty<Chip>();
            var ids = new List<string>();
            foreach (string id in state.Inventory.OwnedSkins)
                if (CatalogIds.TryParseSkin(id, out string parsedBase) && parsedBase == baseId && !ids.Contains(id))
                    ids.Add(id);
            if (ids.Count < 2) return Array.Empty<Chip>();
            ids.Sort(StringComparer.Ordinal);
            var result = new Chip[ids.Count];
            for (int i = 0; i < ids.Count; i++)
                result[i] = new Chip(ids[i], -1, true, true,
                    IsEquipped(state.Inventory.Equipped?.Skins, baseId, ids[i]));
            return result;
        }

        private static bool IsEquipped(Dictionary<string, string> equipped, string baseId, string id)
        {
            return equipped != null && equipped.TryGetValue(baseId, out string current) && current == id;
        }
    }
}
