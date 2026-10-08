namespace NodeWar.Backend
{
    /// <summary>
    /// The stable keys the catalog and the lobby spell each simulation type
    /// with, by the type's number. Spelled out here, not derived from the
    /// enum members' names: an enum member can be renamed for the domain
    /// without moving a stored ID, a catalog base or a lobby ID, and a
    /// number with no entry has no key at all.
    ///
    /// Indexed by (int)SuitType and (int)DistrictType. Shared never references
    /// the simulation, so the numbers are pinned by tests on the simulation
    /// side, not by a reference here. A new type appends a number and an entry;
    /// existing entries never change.
    /// </summary>
    public static class CatalogKeys
    {
        private static readonly string[] SuitKeys =
        {
            null, "farmer", "miner", "warrior", "smelter", "guardian", "scout",
            "berserker", "medic", "merchant", "acolyte", "watcher"
        };

        // Core (5) has a key because the lobby and the old enum-derived ID did,
        // but no catalog base: see CatalogDistrictTypes.
        private static readonly string[] DistrictKeys =
        {
            null, "farm", "mine", "village", "barracks", "core", "forge",
            "camp", "shrine", "arsenal", "sanctuary", "watchtower", "rampart", "market", "pier", "town", "infirmary", "fortress"
        };

        /// <summary>The active catalog district numbers, ascending. Historical keys remain available for decoding.</summary>
        public static readonly int[] CatalogDistrictTypes = { 1, 2, 3, 4, 6, 13, 14, 15, 16, 17 };

        /// <summary>One past the highest suit number with a key.</summary>
        public static int SuitTableLength => SuitKeys.Length;

        /// <summary>One past the highest district number with a key.</summary>
        public static int DistrictTableLength => DistrictKeys.Length;

        public static string SuitKey(int type) => type > 0 && type < SuitKeys.Length ? SuitKeys[type] : null;
        public static string DistrictKey(int type) => type > 0 && type < DistrictKeys.Length ? DistrictKeys[type] : null;

        /// <summary>"suit.warrior", or null for a number with no key.</summary>
        public static string SuitBase(int type) { string key = SuitKey(type); return key == null ? null : "suit." + key; }

        /// <summary>"district.rampart", or null for a number with no key.</summary>
        public static string DistrictBase(int type) { string key = DistrictKey(type); return key == null ? null : "district." + key; }

        /// <summary>"suit_warrior", the lobby's ID for the item, or null for a number with no key.</summary>
        public static string SuitLobbyId(int type) { string key = SuitKey(type); return key == null ? null : "suit_" + key; }

        /// <summary>"node_rampart", the lobby's frozen ID for the district, or null for a number with no key.</summary>
        public static string DistrictLobbyId(int type) { string key = DistrictKey(type); return key == null ? null : "node_" + key; }
    }
}
