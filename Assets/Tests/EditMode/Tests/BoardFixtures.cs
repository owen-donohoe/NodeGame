using NodeWar.Simulation;

namespace NodeWar.Tests
{
    /// <summary>
    /// Tiny explicit boards for tests that need a factory-built match but not
    /// the shipped map. No test relies on implicit geometry: a node's ID is its
    /// cell index on an all-land board, so <see cref="Cell"/> is the whole
    /// coordinate lookup. The shipped hourglass is exercised separately through
    /// <see cref="PremadeMaps"/>, never through these.
    ///
    /// One source file: Unity's EditMode assembly compiles it in place and the
    /// .NET test projects link it, so the fixture cannot drift between them.
    /// </summary>
    internal static class BoardFixtures
    {
        /// <summary>
        /// An all-land board whose every cell except the two Cores is a district
        /// slot. P0's Core is top-centre (high Z), P1's bottom-centre, so the map
        /// is mirrored top to bottom like the shipped one. Resources and path
        /// costs are the shipped board's.
        /// </summary>
        public static BoardConfigData LandGrid(int cols, int rows)
        {
            var terrain = new TerrainType[cols * rows];
            var slots = new bool[cols * rows];
            for (int i = 0; i < slots.Length; i++) slots[i] = true;

            int x = cols / 2;
            slots[Cell(cols, x, rows - 1)] = false;
            slots[Cell(cols, x, 0)] = false;

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
                    { gridX = x, gridZ = rows - 1, districtType = DistrictType.Core, ownerID = 0, claimBar = 10000 },
                    new BoardConfigData.InitialDistrictPlacement
                    { gridX = x, gridZ = 0, districtType = DistrictType.Core, ownerID = 1, claimBar = -10000 }
                },
                terrain = terrain,
                districtSlots = slots,
                baseDraftDistrictsP0 = new[] { DistrictType.Farm, DistrictType.Mine, DistrictType.Village },
                baseDraftDistrictsP1 = new[] { DistrictType.Farm, DistrictType.Mine, DistrictType.Village }
            };
        }

        /// <summary>
        /// 3x3, all land: nodes 0-8 are the cells in row-major order, P0's Core
        /// is node 7 at (1,2) and P1's is node 1 at (1,0).
        /// </summary>
        public static BoardConfigData LandGrid3x3()
        {
            return LandGrid(3, 3);
        }

        public const int P0Core3x3 = 7;
        public const int P1Core3x3 = 1;

        /// <summary>
        /// The map ID a fixture board is logged under: derived from its own
        /// fingerprint, so two different test boards never share one and none
        /// can be mistaken for a shipped map.
        /// </summary>
        public static string FixtureMapId(BoardConfigData board)
        {
            return "test-" + BoardHasher.Hash(board).ToString("x8");
        }

        /// <summary>
        /// The setup a recorder or log needs for a fixture board: its own hash,
        /// the current simulation version, and the given balance identity.
        /// </summary>
        public static MatchSetup SetupFor(BoardConfigData board, int balanceHash = 0)
        {
            return new MatchSetup(FixtureMapId(board), BoardHasher.Hash(board),
                (ushort)SimulationVersion.Current, balanceHash);
        }

        /// <summary>
        /// A catalog of test boards, for a referee that should vouch for fixtures.
        /// Register a board with <see cref="Add"/>; it is then found under
        /// <see cref="FixtureMapId"/>. Not the shipped catalog.
        /// </summary>
        public sealed class FixtureCatalog : IBoardCatalog
        {
            private readonly System.Collections.Generic.List<string> ids = new System.Collections.Generic.List<string>();
            private readonly System.Collections.Generic.List<BoardConfigData> boards =
                new System.Collections.Generic.List<BoardConfigData>();

            public void Add(BoardConfigData board)
            {
                string id = FixtureMapId(board);
                if (ids.Contains(id)) return;
                ids.Add(id);
                boards.Add(board);
            }

            public bool TryGet(string mapId, out BoardConfigData board)
            {
                int i = ids.IndexOf(mapId);
                board = i < 0 ? default : boards[i];
                return i >= 0;
            }
        }

        public static int Cell(int cols, int x, int z)
        {
            return z * cols + x;
        }

        public static int Cell(BoardConfigData board, int x, int z)
        {
            return z * board.gridCols + x;
        }
    }
}
