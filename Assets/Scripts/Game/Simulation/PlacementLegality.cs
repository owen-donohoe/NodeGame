namespace NodeWar.Simulation
{
    /// <summary>
    /// The one spatial rule for putting a drafted district on a board: roster,
    /// bounds, terrain, slot and occupancy. Whose turn it is and which pick is
    /// being spent are a separate draft layer on top; they are not a second
    /// opinion about the map. Every path that places or previews a district
    /// (local, received, timeout, rig, highlight) asks this and nothing else.
    /// </summary>
    public static class PlacementLegality
    {
        /// <summary>
        /// May <paramref name="district"/> be built at (<paramref name="x"/>,
        /// <paramref name="z"/>)? <paramref name="occupied"/> is row-major like
        /// the board's own arrays, true where a district already stands; null
        /// means nothing is built yet. Board cells holding a fixed placement
        /// (the Cores) are always taken, whatever <paramref name="occupied"/> says.
        /// </summary>
        public static bool CanPlace(BoardConfigData board, bool[] occupied, DistrictType district, int x, int z)
        {
            if (!IsDraftable(district)) return false;
            if (x < 0 || x >= board.gridCols || z < 0 || z >= board.gridRows) return false;

            long cells = (long)board.gridCols * board.gridRows;
            if (board.terrain == null || board.districtSlots == null) return false;
            if (board.terrain.Length != cells || board.districtSlots.Length != cells) return false;

            int cell = z * board.gridCols + x;
            if (occupied != null && (occupied.Length != cells || occupied[cell])) return false;
            if (IsFixedCell(board, x, z)) return false;
            if (!board.districtSlots[cell]) return false;

            // A Pier needs water to stand in; everything else needs dry land.
            return district == DistrictType.Pier
                ? board.terrain[cell] == TerrainType.Lake
                : board.terrain[cell] == TerrainType.Land;
        }

        /// <summary>
        /// Whether a type can be drafted at all. None and Core are board
        /// furniture, and a number that is no district is nothing to place.
        /// </summary>
        public static bool IsDraftable(DistrictType district)
        {
            int value = (int)district;
            return value >= (int)DistrictType.Farm && value <= (int)DistrictType.Pier
                && district != DistrictType.Core;
        }

        private static bool IsFixedCell(BoardConfigData board, int x, int z)
        {
            if (board.initialPlacements == null) return false;
            for (int i = 0; i < board.initialPlacements.Length; i++)
                if (board.initialPlacements[i].gridX == x && board.initialPlacements[i].gridZ == z) return true;
            return false;
        }
    }
}
