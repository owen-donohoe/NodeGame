namespace NodeWar.Simulation
{
    /// <summary>
    /// Pure checks on a board, in two strengths. <see cref="ValidateBoard"/> and
    /// <see cref="ValidateDraft"/> are what <see cref="MatchFactory"/> needs
    /// before it will allocate anything: a board that can be built at all.
    /// <see cref="ValidateAuthoredMap"/> is the stricter bar a shipped map
    /// clears: mirrored, sized for the game, connected by land. Tiny test
    /// fixtures meet the first and are not expected to meet the second.
    ///
    /// Every method returns false and an error naming the cell rather than
    /// throwing, so a server can refuse a hostile log without an exception.
    /// </summary>
    public static class MapAuthoringRules
    {
        /// <summary>Fewest and most nodes an authored map may reach, counting every legal Pier.</summary>
        public const int MinNodes = 14;
        public const int MaxNodes = 20;

        // --- what the factory needs ---

        public static bool ValidateBoard(BoardConfigData board, out string error)
        {
            if (board.gridCols <= 0 || board.gridRows <= 0)
                return Fail("Board has no cells.", out error);
            long cells = (long)board.gridCols * board.gridRows;

            if (board.terrain == null || board.terrain.Length != cells)
                return Fail("Board terrain must hold one entry per cell (" + cells + ").", out error);
            if (board.districtSlots == null || board.districtSlots.Length != cells)
                return Fail("Board district slots must hold one entry per cell (" + cells + ").", out error);
            for (int i = 0; i < board.terrain.Length; i++)
                if (board.terrain[i] != TerrainType.Land && board.terrain[i] != TerrainType.Lake
                    && board.terrain[i] != TerrainType.Ocean)
                    return Fail("Unknown terrain " + (int)board.terrain[i] + " at " + CellName(board, i) + ".", out error);

            BoardConfigData.InitialDistrictPlacement[] placements = board.initialPlacements;
            if (placements == null) return Fail("Board has no fixed placements.", out error);

            var taken = new bool[(int)cells];
            int coreCount = 0;
            int firstCore = -1;
            int secondCore = -1;
            for (int i = 0; i < placements.Length; i++)
            {
                BoardConfigData.InitialDistrictPlacement p = placements[i];
                if (p.gridX < 0 || p.gridX >= board.gridCols || p.gridZ < 0 || p.gridZ >= board.gridRows)
                    return Fail("Fixed placement " + i + " at (" + p.gridX + "," + p.gridZ + ") is off the board.", out error);
                int cell = p.gridZ * board.gridCols + p.gridX;
                if (board.terrain[cell] != TerrainType.Land)
                    return Fail("Fixed placement " + i + " at (" + p.gridX + "," + p.gridZ + ") is not on land.", out error);
                if (taken[cell])
                    return Fail("Two fixed placements share (" + p.gridX + "," + p.gridZ + ").", out error);
                taken[cell] = true;
                if ((int)p.districtType < 0 || (int)p.districtType > (int)DistrictType.Pier)
                    return Fail("Fixed placement " + i + " is an unknown district.", out error);
                if (p.ownerID < -1 || p.ownerID > 1)
                    return Fail("Fixed placement " + i + " has owner " + p.ownerID + ".", out error);

                if (p.districtType == DistrictType.Core)
                {
                    coreCount++;
                    if (firstCore < 0) firstCore = i; else if (secondCore < 0) secondCore = i;
                }
            }

            if (coreCount != 2)
                return Fail("A board needs exactly two Cores, found " + coreCount + ".", out error);
            if (placements[firstCore].gridZ == placements[secondCore].gridZ)
                return Fail("The two Cores share row " + placements[firstCore].gridZ
                    + ", so neither is the high-Z one.", out error);

            error = null;
            return true;
        }

        /// <summary>
        /// Every draft placement legal against the board and the placements
        /// before it, through <see cref="PlacementLegality"/>. The board must
        /// already satisfy <see cref="ValidateBoard"/>.
        /// </summary>
        public static bool ValidateDraft(BoardConfigData board, DraftPlacement[] draft, out string error)
        {
            error = null;
            if (draft == null || draft.Length == 0) return true;

            var occupied = new bool[board.gridCols * board.gridRows];
            for (int i = 0; i < draft.Length; i++)
            {
                DraftPlacement dp = draft[i];
                if (!PlacementLegality.CanPlace(board, occupied, dp.districtType, dp.gridX, dp.gridZ))
                    return Fail("Draft placement " + i + " (" + dp.districtType + " at " + dp.gridX + "," + dp.gridZ
                        + ") is not legal on this board.", out error);
                occupied[dp.gridZ * board.gridCols + dp.gridX] = true;
            }
            return true;
        }

        // --- what a shipped map must also be ---

        public static bool ValidateAuthoredMap(BoardConfigData board, out string error)
        {
            if (!ValidateBoard(board, out error)) return false;
            int cols = board.gridCols;
            int rows = board.gridRows;

            for (int cell = 0; cell < board.terrain.Length; cell++)
            {
                int x = cell % cols;
                int z = cell / cols;
                if (board.districtSlots[cell] && board.terrain[cell] == TerrainType.Ocean)
                    return Fail("Ocean at " + CellName(board, cell) + " is marked as a district slot.", out error);

                int mirror = (rows - 1 - z) * cols + x;
                if (board.terrain[cell] != board.terrain[mirror])
                    return Fail("Terrain at " + CellName(board, cell) + " differs from its mirror "
                        + CellName(board, mirror) + ".", out error);
                if (board.districtSlots[cell] != board.districtSlots[mirror])
                    return Fail("District slot at " + CellName(board, cell) + " differs from its mirror "
                        + CellName(board, mirror) + ".", out error);
            }

            BoardConfigData.InitialDistrictPlacement[] placements = board.initialPlacements;
            for (int i = 0; i < placements.Length; i++)
            {
                BoardConfigData.InitialDistrictPlacement p = placements[i];
                if (board.districtSlots[p.gridZ * cols + p.gridX])
                    return Fail("Fixed placement at (" + p.gridX + "," + p.gridZ + ") is marked as a district slot.", out error);

                int mirrored = -1;
                for (int j = 0; j < placements.Length && mirrored < 0; j++)
                    if (placements[j].gridX == p.gridX && placements[j].gridZ == rows - 1 - p.gridZ) mirrored = j;
                if (mirrored < 0)
                    return Fail("Fixed placement at (" + p.gridX + "," + p.gridZ + ") has no mirror.", out error);

                BoardConfigData.InitialDistrictPlacement q = placements[mirrored];
                int swappedOwner = p.ownerID == 0 ? 1 : p.ownerID == 1 ? 0 : p.ownerID;
                if (q.districtType != p.districtType || q.ownerID != swappedOwner || q.claimBar != -p.claimBar)
                    return Fail("Fixed placement at (" + p.gridX + "," + p.gridZ
                        + ") and its mirror do not exchange players.", out error);
            }

            // The high-Z Core is P0's, as MatchFactory assigns it.
            BoardConfigData.InitialDistrictPlacement high = HighCore(board);
            if (high.ownerID != 0)
                return Fail("The Core at (" + high.gridX + "," + high.gridZ + ") is the high-Z one and must belong to P0.", out error);

            if (!SamePool(board.baseDraftDistrictsP0, board.baseDraftDistrictsP1))
                return Fail("The two players' base draft pools differ.", out error);

            int landNodes = 0;
            int pierSlots = 0;
            for (int cell = 0; cell < board.terrain.Length; cell++)
            {
                if (board.terrain[cell] == TerrainType.Land) landNodes++;
                else if (board.terrain[cell] == TerrainType.Lake && board.districtSlots[cell]) pierSlots++;
            }
            if (landNodes < MinNodes || landNodes + pierSlots > MaxNodes)
                return Fail("The board reaches " + landNodes + " to " + (landNodes + pierSlots)
                    + " nodes; an authored map needs " + MinNodes + " to " + MaxNodes + ".", out error);

            if (!HasLandRoute(board))
                return Fail("No land route joins the two Cores; a Pier must never be the only way across.", out error);

            error = null;
            return true;
        }

        /// <summary>
        /// Can one Core reach the other over land alone, one orthogonal step at
        /// a time? Lake never counts, Pier slots included: the route has to
        /// exist before anyone drafts a bridge.
        /// </summary>
        public static bool HasLandRoute(BoardConfigData board)
        {
            if (!ValidateBoard(board, out _)) return false;

            int cols = board.gridCols;
            int rows = board.gridRows;
            BoardConfigData.InitialDistrictPlacement from = HighCore(board);
            BoardConfigData.InitialDistrictPlacement to = LowCore(board);
            int start = from.gridZ * cols + from.gridX;
            int goal = to.gridZ * cols + to.gridX;

            var seen = new bool[cols * rows];
            var queue = new int[cols * rows];
            int head = 0;
            int tail = 0;
            queue[tail++] = start;
            seen[start] = true;
            while (head < tail)
            {
                int cell = queue[head++];
                if (cell == goal) return true;
                int x = cell % cols;
                int z = cell / cols;
                if (x > 0) tail = Visit(board, seen, queue, tail, cell - 1);
                if (x < cols - 1) tail = Visit(board, seen, queue, tail, cell + 1);
                if (z > 0) tail = Visit(board, seen, queue, tail, cell - cols);
                if (z < rows - 1) tail = Visit(board, seen, queue, tail, cell + cols);
            }
            return false;
        }

        private static int Visit(BoardConfigData board, bool[] seen, int[] queue, int tail, int cell)
        {
            if (seen[cell] || board.terrain[cell] != TerrainType.Land) return tail;
            seen[cell] = true;
            queue[tail] = cell;
            return tail + 1;
        }

        /// <summary>The Core placement on the highest Z row: P0's. The board must satisfy <see cref="ValidateBoard"/>.</summary>
        internal static BoardConfigData.InitialDistrictPlacement HighCore(BoardConfigData board)
        {
            return OrderedCores(board, true);
        }

        /// <summary>The Core placement on the lowest Z row: P1's.</summary>
        internal static BoardConfigData.InitialDistrictPlacement LowCore(BoardConfigData board)
        {
            return OrderedCores(board, false);
        }

        private static BoardConfigData.InitialDistrictPlacement OrderedCores(BoardConfigData board, bool high)
        {
            BoardConfigData.InitialDistrictPlacement best = default;
            bool found = false;
            for (int i = 0; i < board.initialPlacements.Length; i++)
            {
                BoardConfigData.InitialDistrictPlacement p = board.initialPlacements[i];
                if (p.districtType != DistrictType.Core) continue;
                if (!found || (high ? p.gridZ > best.gridZ : p.gridZ < best.gridZ))
                {
                    best = p;
                    found = true;
                }
            }
            return best;
        }

        private static bool SamePool(DistrictType[] a, DistrictType[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        private static string CellName(BoardConfigData board, int cell)
        {
            return "(" + (cell % board.gridCols) + "," + (cell / board.gridCols) + ")";
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
