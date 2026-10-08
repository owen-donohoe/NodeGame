namespace NodeWar.Simulation
{
    /// <summary>
    /// Everything about a board that does not change during a match: its grid,
    /// what each cell is made of, which cells may take a district, the fixed
    /// placements, each player's base draft pool, and the starting and path-cost
    /// numbers. Plain data with no Unity reference; <see cref="MatchFactory"/>
    /// turns it into a tick-0 <see cref="SimulationState"/>.
    ///
    /// <see cref="terrain"/> and <see cref="districtSlots"/> hold exactly
    /// gridCols * gridRows entries, row-major (cell = z * gridCols + x). A board
    /// without them is history data only: a new match refuses it.
    /// </summary>
    [System.Serializable]
    public struct BoardConfigData
    {
        public const int DefaultLinkWeight = 4;

        public int gridCols;
        public int gridRows;

        public int defaultLinkWeight;

        public int startingVillagersPerPlayer;
        public int startingFood;
        public int startingMaterials;
        public int startingMetal;

        public int ownedMultiplier;
        public int partiallyOwnedMultiplier;
        public int unownedMultiplier;
        public int enemyPartiallyOwnedMultiplier;
        public int enemyOwnedMultiplier;

        public InitialDistrictPlacement[] initialPlacements;

        /// <summary>What each cell is: Land, Lake or Ocean. Row-major.</summary>
        public TerrainType[] terrain;

        /// <summary>
        /// Which cells may take a drafted district: a Land slot takes any
        /// ordinary district, a Lake slot takes only a Pier. Row-major. Open
        /// lake and ocean are never slots, and a Core's cell is not one.
        /// </summary>
        public bool[] districtSlots;

        /// <summary>The districts each player drafts on this map before their loadout.</summary>
        public DistrictType[] baseDraftDistrictsP0;
        public DistrictType[] baseDraftDistrictsP1;

        [System.Serializable]
        public struct InitialDistrictPlacement
        {
            public int gridX;
            public int gridZ;
            public DistrictType districtType;
            public int ownerID; // -1 = unowned, 0 = P0, 1 = P1
            public int claimBar; // Use +/-10000 for fully owned.
        }
    }
}
