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
        // The initial save defaults also supply the era locks. Return detached arrays.
        private readonly struct DefaultLock
        {
            public readonly string Id;
            public readonly bool Suit;
            public readonly int LastLockedArena;
            public DefaultLock(string id, bool suit, int lastLockedArena)
            { Id = id; Suit = suit; LastLockedArena = lastLockedArena; }
        }
        private static readonly DefaultLock[] defaults = {
            new DefaultLock("suit_warrior", true, 0),
            new DefaultLock("node_farm", false, 1),
            new DefaultLock("node_mine", false, 1),
            new DefaultLock("node_town", false, 0)
        };
        private static string[] Defaults(bool suits, int arena)
        {
            var ids = new System.Collections.Generic.List<string>();
            foreach (var item in defaults)
                if (item.Suit == suits && arena <= item.LastLockedArena) ids.Add(item.Id);
            return ids.ToArray();
        }
        public static string[] DefaultSuits() => Defaults(true, 0);
        public static string[] DefaultDistricts() => Defaults(false, 0);
        public static string[] LockedSuits(int arena) => Defaults(true, arena);
        public static string[] LockedDistricts(int arena) => Defaults(false, arena);
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
