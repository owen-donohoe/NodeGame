using NodeWar.Simulation;

namespace NodeWar.View
{
    /// <summary>Presentation-only sample for the shipped hourglass, in replay order.</summary>
    public static class BoardVisualsBenchDraft
    {
        public static DraftPlacement[] Create()
        {
            return new[]
            {
                Place(1, DistrictType.Village, 1, 1), Place(0, DistrictType.Village, 1, 5),
                Place(1, DistrictType.Farm, 2, 1), Place(0, DistrictType.Farm, 2, 5),
                Place(1, DistrictType.Mine, 4, 1), Place(0, DistrictType.Mine, 4, 5),
                Place(0, DistrictType.Pier, 1, 3)
            };
        }

        /// <summary>A runtime-only cluster, excluding every draft slot and sample placement.</summary>
        public static BoardConfigData IncludeDeepLake(BoardConfigData board)
        {
            board.terrain = (TerrainType[])board.terrain.Clone();
            var chosen = new System.Collections.Generic.List<int>(4);
            DraftPlacement[] sample = Create();
            bool[] shoal = ShoalPath.Derive(board);
            bool Candidate(int cell)
            {
                int x = cell % board.gridCols, z = cell / board.gridCols;
                if (x <= 0 || z <= 0 || x >= board.gridCols - 1 || z >= board.gridRows - 1
                    || (board.terrain[cell] != TerrainType.Lake && board.terrain[cell] != TerrainType.DeepLake)
                    || board.districtSlots[cell] || shoal == null || shoal[cell]) return false;
                for (int i = 0; i < sample.Length; i++)
                    if (sample[i].gridX == x && sample[i].gridZ == z) return false;
                return true;
            }
            // Row-major seed and expansion give the same connected cluster on every replay.
            for (int seed = 0; seed < board.terrain.Length; seed++)
            {
                if (!Candidate(seed)) continue;
                chosen.Clear(); chosen.Add(seed);
                for (int head = 0; head < chosen.Count && chosen.Count < 4; head++)
                    for (int cell = 0; cell < board.terrain.Length && chosen.Count < 4; cell++)
                    {
                        if (!Candidate(cell) || chosen.Contains(cell)) continue;
                        int dx = System.Math.Abs(cell % board.gridCols - chosen[head] % board.gridCols);
                        int dz = System.Math.Abs(cell / board.gridCols - chosen[head] / board.gridCols);
                        if (dx + dz == 1) chosen.Add(cell);
                    }
                int additions = 0;
                for (int i = 0; i < chosen.Count; i++) if (board.terrain[chosen[i]] == TerrainType.Lake) additions++;
                if (additions < 2) continue;
                for (int i = 0; i < chosen.Count; i++) board.terrain[chosen[i]] = TerrainType.DeepLake;
                return board;
            }
            // The shipped hourglass has a suitable cluster; other boards retain their authored water.
            return board;
        }

        private static DraftPlacement Place(int player, DistrictType district, int x, int z)
        {
            return new DraftPlacement { playerID = player, districtType = district, gridX = x, gridZ = z };
        }
    }
}


