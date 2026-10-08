using NodeWar.Simulation;

namespace NodeWar.View
{
    /// <summary>What the board shows for one cell. Plain data: no Unity object exists yet.</summary>
    public struct CellDescriptor
    {
        public int x;
        public int z;
        public int cell;
        public TerrainType terrain;

        /// <summary>The board marks this cell as somewhere a district can be drafted.</summary>
        public bool isSlot;

        /// <summary>A district already stands here: a Core, or a placement made so far.</summary>
        public bool occupied;

        /// <summary>
        /// A node exists on this cell, so it can be tapped in the match: every Land cell, and a
        /// Lake cell once a Pier is built on it. Ocean and open lake never are.
        /// </summary>
        public bool hasNodeTarget;

        /// <summary>The piece in hand may legally be placed here (<see cref="PlacementLegality"/>).</summary>
        public bool legalForPick;

        /// <summary>The fill cue for a legal cell.</summary>
        public bool tint;

        /// <summary>
        /// The shape cue for a legal cell. Always set with <see cref="tint"/>: a highlight must
        /// never depend on colour alone, for colour-blind players.
        /// </summary>
        public bool outline;

        /// <summary>A Pier stands here and is drawn as a bridge to the nodes beside it.</summary>
        public bool bridge;
    }

    /// <summary>
    /// The terrain view's decisions, kept free of UnityEngine so they are tested: which cells
    /// can be targeted, which are highlighted for the piece in hand, and which carry a bridge.
    /// BoardTerrainView does the Unity half and draws exactly what this says.
    /// </summary>
    public static class TerrainPresentation
    {
        /// <summary>
        /// Describes every cell, row-major. <paramref name="placed"/> are the draft placements so
        /// far (they occupy cells, and a Pier on a Lake slot makes it a node target); the board's
        /// Cores are occupied from the start. With a piece in hand, the legal cells are those
        /// <see cref="PlacementLegality"/> allows, the same rule every draft path asks.
        /// </summary>
        public static CellDescriptor[] Describe(BoardConfigData board, DraftPlacement[] placed,
            bool hasPick, DistrictType pick)
        {
            int cells = board.gridCols * board.gridRows;
            var occupied = new bool[cells];
            var pier = new bool[cells];

            if (board.initialPlacements != null)
                for (int i = 0; i < board.initialPlacements.Length; i++)
                    Mark(board, occupied, board.initialPlacements[i].gridX, board.initialPlacements[i].gridZ);
            if (placed != null)
                for (int i = 0; i < placed.Length; i++)
                {
                    Mark(board, occupied, placed[i].gridX, placed[i].gridZ);
                    if (placed[i].districtType == DistrictType.Pier) Mark(board, pier, placed[i].gridX, placed[i].gridZ);
                }

            var result = new CellDescriptor[cells];
            for (int z = 0; z < board.gridRows; z++)
                for (int x = 0; x < board.gridCols; x++)
                {
                    int cell = z * board.gridCols + x;
                    TerrainType terrain = board.terrain != null && cell < board.terrain.Length
                        ? board.terrain[cell] : TerrainType.Ocean;
                    bool legal = hasPick && PlacementLegality.CanPlace(board, occupied, pick, x, z);

                    result[cell] = new CellDescriptor
                    {
                        x = x,
                        z = z,
                        cell = cell,
                        terrain = terrain,
                        isSlot = board.districtSlots != null && cell < board.districtSlots.Length && board.districtSlots[cell],
                        occupied = occupied[cell],
                        hasNodeTarget = terrain == TerrainType.Land || (terrain == TerrainType.Lake && pier[cell]),
                        legalForPick = legal,
                        tint = legal,
                        outline = legal,
                        bridge = terrain == TerrainType.Lake && pier[cell]
                    };
                }
            return result;
        }

        public static int NodeTargetCount(CellDescriptor[] cells)
        {
            int count = 0;
            for (int i = 0; i < cells.Length; i++)
                if (cells[i].hasNodeTarget) count++;
            return count;
        }

        private static void Mark(BoardConfigData board, bool[] flags, int x, int z)
        {
            if (x < 0 || x >= board.gridCols || z < 0 || z >= board.gridRows) return;
            flags[z * board.gridCols + x] = true;
        }
    }
}
