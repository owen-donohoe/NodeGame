using NodeWar.Backend;

namespace NodeWar.Lobby
{
    /// <summary>Display positions include globally granted items, outside the chosen deck.</summary>
    public static class LoadoutRules
    {
        public const int DisplaySuitSlots = 3;
        public const int DisplayDistrictSlots = 5;

        // Phase 1 keeps the arena-1 layout at every rank. Arena is a seam for
        // a later coordinated change to locks and chosen-slot capacities.
        public static string[] LockedDistricts(int arena)
        {
            return new[] { "node_farm", "node_mine", "node_village" };
        }

        public static string[] LockedSuits(int arena)
        {
            return new[] { "suit_warrior" };
        }

        public static int ArenaOrFallback(RankRecord rank)
        {
            return rank == null ? 1 : rank.Arena;
        }
    }
}
