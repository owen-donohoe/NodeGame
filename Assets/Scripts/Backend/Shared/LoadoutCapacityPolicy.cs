using NodeWar.Lobby;

namespace NodeWar.Backend
{
    /// <summary>Chosen capacities and free-grant mode. Canonical array lengths encode the mode.</summary>
    public static class LoadoutCapacityPolicy
    {
        public static int SuitCapacity(int arena) => arena < 2 ? 2 : 3;
        public static int DistrictCapacity(int arena) => arena < 2 ? 2 : 5;
        public static bool GetsFreeGrants(int arena) => arena < 2;
        public static bool IsLowShape(int suits, int districts) => suits == 2 && districts == 2;
        public static bool IsHighShape(int suits, int districts) => suits == 3 && districts == 5;

        /// <summary>Every chosen slot at the local arena's capacity must be filled.</summary>
        public static bool IsComplete(LoadoutData loadout, int arena)
        {
            loadout = LoadoutData.Normalized(loadout, arena);
            for (int i = 0; i < loadout.suitIDs.Length; i++)
                if (string.IsNullOrEmpty(loadout.suitIDs[i])) return false;
            for (int i = 0; i < loadout.districtIDs.Length; i++)
                if (string.IsNullOrEmpty(loadout.districtIDs[i])) return false;
            return true;
        }
    }
}
