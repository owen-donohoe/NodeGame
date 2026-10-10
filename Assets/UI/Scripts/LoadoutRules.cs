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

        public static int ArenaOrFallback(RankRecord rank)
        {
            return rank == null ? 1 : rank.Arena;
        }
    }
}
