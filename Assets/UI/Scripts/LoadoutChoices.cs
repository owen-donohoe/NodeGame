using System;
using System.Collections.Generic;
using NodeWar.Backend;

namespace NodeWar.Lobby
{
    public readonly struct LoadoutChoice
    {
        public string ID { get; }
        public string BaseID { get; }
        public string Label { get; }
        /// <summary>Original variant index, preserved for balance lookup.</summary>
        public int OrderKey { get; }
        public UnlockState UnlockState { get; }
        public bool Equipped { get; }
        public bool PreviewOnly { get; }
        public bool Selectable => !PreviewOnly && UnlockState.CanUse && !Equipped;

        public LoadoutChoice(string id, string baseId, string label, int orderKey, UnlockState unlockState,
            bool equipped, bool previewOnly = false)
        {
            ID = id;
            BaseID = baseId;
            Label = label;
            OrderKey = orderKey;
            UnlockState = previewOnly ? new UnlockState(unlockState.Status, unlockState.UnlockEra, false) : unlockState;
            Equipped = equipped;
            PreviewOnly = previewOnly;
        }
    }

    public static class LoadoutChoices
    {
        public static LoadoutChoice[] Variants(string baseId, PlayerState state)
        {
            var choices = new List<LoadoutChoice>();
            // Only real suit sidegrades belong in the variant picker. Era upgrades do not.
            if (!SuitTree.IsTreeSuit(baseId)) return Array.Empty<LoadoutChoice>();
            foreach (var node in SuitTree.Nodes)
                if (node.BaseId == baseId)
                    choices.Add(new LoadoutChoice(node.Id, baseId, "Variant " + node.Variant, node.Variant,
                        UnlockStates.ForVariant(baseId, node.Id, state), IsEquipped(state?.Inventory?.Equipped?.Variants, baseId, node.Id)));
            choices.Sort(Compare);
            return choices.ToArray();
        }

        public static LoadoutChoice[] Skins(string baseId, string[] knownSkinIds, PlayerState state)
        {
            string defaultId = CatalogIds.DefaultSkin(baseId);
            if (!CatalogIds.TryParseSkin(defaultId, out string parsedBase) || parsedBase != baseId)
                return Array.Empty<LoadoutChoice>();
            var ids = new List<string>();
            AddSkin(ids, baseId, defaultId);
            if (knownSkinIds != null)
                foreach (string id in knownSkinIds) AddSkin(ids, baseId, id);
            if (state?.Inventory?.OwnedSkins != null)
                foreach (string id in state.Inventory.OwnedSkins) AddSkin(ids, baseId, id);
            var choices = new List<LoadoutChoice>();
            foreach (string id in ids)
                choices.Add(new LoadoutChoice(id, baseId, id.Substring(id.LastIndexOf('.') + 1), 0,
                    UnlockStates.ForSkin(baseId, id, state), IsEquipped(state?.Inventory?.Equipped?.Skins, baseId, id)));
            choices.Sort(Compare);
            return choices.ToArray();
        }

        public static int Compare(LoadoutChoice a, LoadoutChoice b)
        {
            int comparison = a.UnlockState.Status.CompareTo(b.UnlockState.Status);
            if (comparison == 0) comparison = a.OrderKey.CompareTo(b.OrderKey);
            if (comparison == 0) comparison = StringComparer.Ordinal.Compare(a.Label, b.Label);
            return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(a.ID, b.ID);
        }

        private static void AddSkin(List<string> ids, string baseId, string id)
        {
            if (CatalogIds.TryParseSkin(id, out string parsedBase) && parsedBase == baseId && !ids.Contains(id)) ids.Add(id);
        }

        private static bool IsEquipped(Dictionary<string, string> map, string baseId, string id) =>
            map != null && map.TryGetValue(baseId, out string equipped) && equipped == id;
    }
}
