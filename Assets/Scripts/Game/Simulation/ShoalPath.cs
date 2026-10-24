namespace NodeWar.Simulation
{
    /// <summary>Starting-board land connections. All scans and ties follow cell order.</summary>
    public static class ShoalPath
    {
        public static bool[] Derive(BoardConfigData board)
        {
            int cols = board.gridCols, rows = board.gridRows;
            long size = (long)cols * rows;
            if (cols <= 0 || rows <= 0 || size > int.MaxValue || board.terrain == null
                || board.terrain.Length != size) return null;
            int n = (int)size;
            var component = new int[n];
            var queue = new int[n];
            for (int i = 0; i < n; i++) component[i] = -1;
            int count = 0;
            for (int i = 0; i < n; i++)
            {
                if (board.terrain[i] != TerrainType.Land || component[i] >= 0) continue;
                int head = 0, tail = 0;
                queue[tail++] = i;
                component[i] = count;
                while (head < tail)
                {
                    int cell = queue[head++];
                    for (int d = 0; d < 4; d++)
                    {
                        int next = Neighbour(cell, d, cols, rows);
                        if (next < 0 || component[next] >= 0 || board.terrain[next] != TerrainType.Land) continue;
                        component[next] = count;
                        queue[tail++] = next;
                    }
                }
                count++;
            }

            var carved = new bool[n];
            if (count == 0) return carved;
            var connected = new bool[count];
            var core = MapAuthoringRules.HighCore(board);
            int start = core.gridZ * cols + core.gridX;
            if (start < 0 || start >= n || component[start] < 0) return null;
            connected[component[start]] = true;
            int remaining = count - 1;
            while (remaining > 0)
            {
                var cost = new int[n];
                var previous = new int[n];
                var visited = new bool[n];
                for (int i = 0; i < n; i++)
                {
                    cost[i] = component[i] >= 0 && connected[component[i]] ? 0 : int.MaxValue;
                    previous[i] = -1;
                }
                int target = -1;
                for (int step = 0; step < n; step++)
                {
                    int cell = -1;
                    for (int i = 0; i < n; i++)
                        if (!visited[i] && cost[i] != int.MaxValue
                            && (cell < 0 || cost[i] < cost[cell])) cell = i;
                    if (cell < 0) break;
                    visited[cell] = true;
                    if (component[cell] >= 0 && !connected[component[cell]])
                    {
                        target = cell;
                        break;
                    }
                    for (int d = 0; d < 4; d++)
                    {
                        int next = Neighbour(cell, d, cols, rows);
                        if (next < 0 || visited[next] || carved[next]) continue;
                        TerrainType terrain = board.terrain[next];
                        if (terrain != TerrainType.Land && terrain != TerrainType.Lake) continue;
                        int weight = terrain == TerrainType.Land ? 0 : 10 + (TouchesPath(next, carved, cols, rows) ? 40 : 0);
                        int candidate = cost[cell] + weight;
                        if (candidate < cost[next] || (candidate == cost[next]
                            && (previous[next] < 0 || cell < previous[next])))
                        {
                            cost[next] = candidate;
                            previous[next] = cell;
                        }
                    }
                }
                if (target < 0) return null;
                connected[component[target]] = true;
                remaining--;
                for (int cell = target; cell >= 0; cell = previous[cell])
                    if (board.terrain[cell] == TerrainType.Lake) carved[cell] = true;
            }

            var result = (bool[])carved.Clone();
            for (int i = 0; i < n; i++)
                if (carved[i])
                {
                    PremadeMaps.Mirror(board.symmetry, cols, rows, i % cols, i / cols, out int x, out int z);
                    int mirror = z * cols + x;
                    if (board.terrain[mirror] == TerrainType.Lake) result[mirror] = true;
                }
            return result;
        }

        private static int Neighbour(int cell, int direction, int cols, int rows)
        {
            int x = cell % cols, z = cell / cols;
            switch (direction)
            {
                case 0: return x > 0 ? cell - 1 : -1;
                case 1: return x + 1 < cols ? cell + 1 : -1;
                case 2: return z > 0 ? cell - cols : -1;
                default: return z + 1 < rows ? cell + cols : -1;
            }
        }

        private static bool TouchesPath(int cell, bool[] carved, int cols, int rows)
        {
            int x = cell % cols, z = cell / cols;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                    if (x + dx >= 0 && x + dx < cols && z + dz >= 0 && z + dz < rows
                        && carved[(z + dz) * cols + x + dx]) return true;
            return false;
        }
    }
}
