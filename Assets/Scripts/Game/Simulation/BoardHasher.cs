namespace NodeWar.Simulation
{
    /// <summary>
    /// An integer fingerprint of a board's identity: dimensions, terrain, the
    /// district slot mask, the fixed placements, both base draft pools, the
    /// starting numbers and the link and path tuning, in a fixed order. Two
    /// peers, a log and a server that agree on it agree on the map.
    ///
    /// The draft is deliberately absent: it is agreed after the board is, and
    /// is replicated by its own placements. Pure integer arithmetic; a null
    /// array and an empty one hash differently, so a board that lost its
    /// terrain can never pass for one that was authored empty.
    /// </summary>
    public static class BoardHasher
    {
        private const int Prime = 16777619;
        private const uint Offset = 2166136261u;

        public static int Hash(BoardConfigData board)
        {
            unchecked
            {
                int h = (int)Offset;

                h = Mix(h, board.gridCols);
                h = Mix(h, board.gridRows);
                h = Mix(h, board.defaultLinkWeight);
                h = Mix(h, board.startingVillagersPerPlayer);
                h = Mix(h, board.startingFood);
                h = Mix(h, board.startingMaterials);
                h = Mix(h, board.startingMetal);
                h = Mix(h, board.ownedMultiplier);
                h = Mix(h, board.partiallyOwnedMultiplier);
                h = Mix(h, board.unownedMultiplier);
                h = Mix(h, board.enemyPartiallyOwnedMultiplier);
                h = Mix(h, board.enemyOwnedMultiplier);

                BoardConfigData.InitialDistrictPlacement[] placements = board.initialPlacements;
                h = Mix(h, placements == null ? -1 : placements.Length);
                if (placements != null)
                    for (int i = 0; i < placements.Length; i++)
                    {
                        h = Mix(h, placements[i].gridX);
                        h = Mix(h, placements[i].gridZ);
                        h = Mix(h, (int)placements[i].districtType);
                        h = Mix(h, placements[i].ownerID);
                        h = Mix(h, placements[i].claimBar);
                    }

                h = Mix(h, board.terrain == null ? -1 : board.terrain.Length);
                if (board.terrain != null)
                    for (int i = 0; i < board.terrain.Length; i++) h = Mix(h, (int)board.terrain[i]);

                h = Mix(h, board.districtSlots == null ? -1 : board.districtSlots.Length);
                if (board.districtSlots != null)
                    for (int i = 0; i < board.districtSlots.Length; i++) h = Mix(h, board.districtSlots[i] ? 1 : 0);

                h = HashPool(h, board.baseDraftDistrictsP0);
                h = HashPool(h, board.baseDraftDistrictsP1);
                return h;
            }
        }

        private static int HashPool(int h, DistrictType[] pool)
        {
            unchecked
            {
                h = Mix(h, pool == null ? -1 : pool.Length);
                if (pool != null)
                    for (int i = 0; i < pool.Length; i++) h = Mix(h, (int)pool[i]);
                return h;
            }
        }

        // FNV-1a over whole integers. Changing any one value, with everything
        // else fixed, always changes the result: each step is a bijection.
        private static int Mix(int h, int value)
        {
            unchecked { return (h ^ value) * Prime; }
        }
    }
}
