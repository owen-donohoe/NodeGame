namespace NodeWar.Backend
{
    /// <summary>
    /// Complete loadout capacities and fixed items at each arena (era) index.
    /// Integer-only, so the Shared assembly and Cloud Code need no lobby types;
    /// deck completeness lives in LoadoutRules, beside LoadoutData.
    /// </summary>
    public static class LoadoutCapacityPolicy
    {
        public static int SuitCapacity(int arena) => 3;
        public static int DistrictCapacity(int arena) => 5;
        public static string[] LockedSuits(int arena) => arena <= 0 ? new[] { "suit_warrior" } : new string[0];
        public static string[] LockedDistricts(int arena) => arena <= 0
            ? new[] { "node_farm", "node_mine", "node_town" }
            : arena == 1 ? new[] { "node_farm", "node_mine" } : new string[0];

        public static bool IsBaseUsable(string baseId, int arena) =>
            ContentTable.DebutEra(baseId) >= 0 && ContentTable.DebutEra(baseId) <= arena;

        public static bool IsVariantUsable(string baseId, string id, int arena) =>
            IsBaseUsable(baseId, arena) && (SuitTree.IsTreeSuit(baseId)
                ? SuitTree.IsAvailable(id, arena)
                : CatalogIds.TryParseVariant(id, out string parsed, out int era) && parsed == baseId && era <= arena);
        public static bool IsLowShape(int suits, int districts) => suits == 2 && districts == 2;
        public static bool IsHighShape(int suits, int districts) => suits == 3 && districts == 5;
    }
}
