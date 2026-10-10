using NodeWar.Backend;

namespace NodeWar.Lobby
{
    /// <summary>Display positions include globally granted items, outside the chosen deck.</summary>
    public static class LoadoutRules
    {
        public const int DisplaySuitSlots = 3;
        public const int DisplayDistrictSlots = 5;

        public static int ChosenSuitSlots(int arena) => LoadoutCapacityPolicy.SuitCapacity(arena);
        public static int ChosenDistrictSlots(int arena) => LoadoutCapacityPolicy.DistrictCapacity(arena);

        public static string[] LockedDistricts(int arena)
        {
            return LoadoutCapacityPolicy.GetsFreeGrants(arena)
                ? new[] { "node_farm", "node_mine", "node_village" } : new string[0];
        }

        public static string[] LockedSuits(int arena)
        {
            return LoadoutCapacityPolicy.GetsFreeGrants(arena) ? new[] { "suit_warrior" } : new string[0];
        }

        public static bool IsSuitOffered(string id, int arena)
        {
            for (int type = 1; type < CatalogKeys.SuitTableLength; type++)
                if (id == CatalogKeys.SuitLobbyId(type))
                    return id != "suit_warrior" || !LoadoutCapacityPolicy.GetsFreeGrants(arena);
            return false;
        }

        public static bool IsDistrictOffered(string id, int arena)
        {
            foreach (int type in CatalogKeys.CatalogDistrictTypes)
                if (id == CatalogKeys.DistrictLobbyId(type))
                    return !LoadoutCapacityPolicy.GetsFreeGrants(arena) ||
                        (id != "node_farm" && id != "node_mine" && id != "node_village");
            return false;
        }

        /// <summary>Detached local editor; remote loadouts never pass through this seam.</summary>
        public static LoadoutEditor LocalEditor(LoadoutData source, int arena)
        {
            var editor = new LoadoutEditor(source, arena);
            editor.DropUnavailable(id => IsSuitOffered(id, arena), id => IsDistrictOffered(id, arena));
            return editor;
        }
        public static int ArenaOrFallback(RankRecord rank)
        {
            return rank == null ? 1 : rank.Arena;
        }
    }
}
