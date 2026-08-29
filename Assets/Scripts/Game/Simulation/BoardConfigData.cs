namespace NodeWar.Simulation
{
    [System.Serializable]
    public struct BoardConfigData
    {
        public int gridCols;
        public int gridRows;

        public int defaultEdgeWeight;

        public int startingVillagersPerPlayer;
        public int startingFood;
        public int startingMaterials;
        public int startingMetal;

        public int ownedMultiplier;
        public int partiallyOwnedMultiplier;
        public int unownedMultiplier;
        public int enemyPartiallyOwnedMultiplier;
        public int enemyOwnedMultiplier;

        public InitialNodePlacement[] initialPlacements;

        [System.Serializable]
        public struct InitialNodePlacement
        {
            public int gridX;
            public int gridZ;
            public DistrictType districtType;
            public int ownerID; // -1 = unowned, 0 = P0, 1 = P1
            public int claimBar; // Use +/-10000 for fully owned.
        }

        public static BoardConfigData Default()
        {
            return new BoardConfigData
            {
                gridCols = 4,
                gridRows = 7,
                defaultEdgeWeight = 4,
                startingVillagersPerPlayer = 3,
                startingFood = 0,
                startingMaterials = 0,
                startingMetal = 0,
                ownedMultiplier = 50,
                partiallyOwnedMultiplier = 75,
                unownedMultiplier = 100,
                enemyPartiallyOwnedMultiplier = 150,
                enemyOwnedMultiplier = 200,
                initialPlacements = new InitialNodePlacement[]
                {
                    new InitialNodePlacement { gridX = 1, gridZ = 6, districtType = DistrictType.Core, ownerID = 0, claimBar = 10000 },
                    new InitialNodePlacement { gridX = 2, gridZ = 0, districtType = DistrictType.Core, ownerID = 1, claimBar = -10000 }
                }
            };
        }
    }
}
