namespace NodeWar.Simulation
{
    /// <summary>How a map maps onto itself when the two players trade places.</summary>
    public enum MapSymmetry
    {
        /// <summary>(x, z) trades with (x, rows-1-z): the players face each other across the middle row.</summary>
        FlipZ = 0,
        /// <summary>(x, z) trades with (cols-1-x, rows-1-z).</summary>
        HalfTurn = 1
    }
    /// <summary>
    /// The shipped map definitions, as plain data. The one map today is
    /// <see cref="Hourglass01Id"/>; the live game, the rig and the Cloud Code
    /// referee all build their board here, so there is a single copy of the
    /// terrain to disagree with.
    ///
    /// Each call returns a fresh board, so a caller may mutate what it gets.
    /// </summary>
    public static class PremadeMaps
    {
        public const string Hourglass01Id = "hourglass-01";

        // Row z=0 first. O ocean, L land slot, C land under a Core (not a slot),
        // W open lake, P lake with a district slot (empty until a Pier is drafted).
        // Mirrored top to bottom: (x,z) <-> (x,6-z). P0's core is the high-Z one.
        //
        //   x 0 1 2 3 4 5 6
        // z=0 O O O O O O O
        // z=1 O L L C L L O   P1
        // z=2 O L W W L L O
        // z=3 O P W W L L O
        // z=4 O L W W L L O
        // z=5 O L L C L L O   P0
        // z=6 O O O O O O O
        private static readonly string[] Hourglass01Rows =
        {
            "OOOOOOO",
            "OLLCLLO",
            "OLWWLLO",
            "OPWWLLO",
            "OLWWLLO",
            "OLLCLLO",
            "OOOOOOO"
        };

        /// <summary>The shipped maps as a catalog: the one authority a peer, a log or the server checks against.</summary>
        public static readonly IBoardCatalog Catalog = new ShippedCatalog();

        private sealed class ShippedCatalog : IBoardCatalog
        {
            public bool TryGet(string mapId, out BoardConfigData board)
            {
                return PremadeMaps.TryGet(mapId, out board);
            }
        }

        /// <summary>
        /// How a map maps onto itself when the two players trade places. Declared
        /// per map: hourglass-01 is mirrored top to bottom, not rotated a half turn.
        /// </summary>
        public static MapSymmetry SymmetryOf(string mapId)
        {
            if (mapId == Hourglass01Id) return MapSymmetry.FlipZ;
            throw new System.ArgumentException("Unknown map \"" + mapId + "\".", nameof(mapId));
        }

        /// <summary>The cell a cell trades places with under a symmetry, on a cols x rows grid.</summary>
        public static void Mirror(MapSymmetry symmetry, int cols, int rows, int x, int z, out int mirroredX, out int mirroredZ)
        {
            mirroredX = symmetry == MapSymmetry.HalfTurn ? cols - 1 - x : x;
            mirroredZ = rows - 1 - z;
        }
        public static bool TryGet(string mapId, out BoardConfigData board)
        {
            if (mapId == Hourglass01Id)
            {
                board = Hourglass01();
                return true;
            }
            board = default;
            return false;
        }

        public static BoardConfigData Hourglass01()
        {
            int rows = Hourglass01Rows.Length;
            int cols = Hourglass01Rows[0].Length;
            var terrain = new TerrainType[cols * rows];
            var slots = new bool[cols * rows];
            for (int z = 0; z < rows; z++)
                for (int x = 0; x < cols; x++)
                {
                    int cell = z * cols + x;
                    switch (Hourglass01Rows[z][x])
                    {
                        case 'O': terrain[cell] = TerrainType.Ocean; break;
                        case 'L': terrain[cell] = TerrainType.Land; slots[cell] = true; break;
                        case 'C': terrain[cell] = TerrainType.Land; break;
                        case 'W': terrain[cell] = TerrainType.Lake; break;
                        case 'P': terrain[cell] = TerrainType.Lake; slots[cell] = true; break;
                    }
                }

            return new BoardConfigData
            {
                gridCols = cols,
                gridRows = rows,
                defaultLinkWeight = BoardConfigData.DefaultLinkWeight,
                startingVillagersPerPlayer = 3,
                startingFood = 0,
                startingMaterials = 0,
                startingMetal = 0,
                ownedMultiplier = 50,
                partiallyOwnedMultiplier = 75,
                unownedMultiplier = 100,
                enemyPartiallyOwnedMultiplier = 150,
                enemyOwnedMultiplier = 200,
                initialPlacements = new[]
                {
                    new BoardConfigData.InitialDistrictPlacement
                    { gridX = 3, gridZ = 5, districtType = DistrictType.Core, ownerID = 0, claimBar = 10000 },
                    new BoardConfigData.InitialDistrictPlacement
                    { gridX = 3, gridZ = 1, districtType = DistrictType.Core, ownerID = 1, claimBar = -10000 }
                },
                terrain = terrain,
                districtSlots = slots,
                baseDraftDistrictsP0 = new[] { DistrictType.Farm, DistrictType.Mine, DistrictType.Village },
                baseDraftDistrictsP1 = new[] { DistrictType.Farm, DistrictType.Mine, DistrictType.Village }
            };
        }
    }
}
