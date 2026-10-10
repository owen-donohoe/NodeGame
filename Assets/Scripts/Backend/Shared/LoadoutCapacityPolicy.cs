namespace NodeWar.Backend
{
    /// <summary>
    /// Chosen capacities and free-grant mode. Canonical array lengths encode the mode.
    /// Integer-only, so the Shared assembly and Cloud Code need no lobby types;
    /// deck completeness lives in LoadoutRules, beside LoadoutData.
    /// </summary>
    public static class LoadoutCapacityPolicy
    {
        public static int SuitCapacity(int arena) => arena < 2 ? 2 : 3;
        public static int DistrictCapacity(int arena) => arena < 2 ? 2 : 5;
        public static bool GetsFreeGrants(int arena) => arena < 2;
        public static bool IsLowShape(int suits, int districts) => suits == 2 && districts == 2;
        public static bool IsHighShape(int suits, int districts) => suits == 3 && districts == 5;
    }
}
