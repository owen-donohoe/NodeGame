using System.Collections.Generic;
using NodeWar.Simulation;

namespace NodeWar.View
{
    public static class WaterMeshBuilder
    {
        public static int SubdivisionsForTier(int tier) => tier <= 0 ? 1 : tier == 1 ? 4 : tier == 2 ? 6 : 8;

        public static (float[] positions, int[] indices) Build(int cols, int rows, TerrainType[] resolved,
            int subdivisionsPerCell, int padCells)
        {
            ShoreField.Validate(cols, rows, resolved, subdivisionsPerCell, padCells);
            int n = subdivisionsPerCell, lattice = n * 2;
            var vertices = new Dictionary<(int, int), int>();
            var positions = new List<float>();
            var indices = new List<int>();
            int Vertex(int x, int z)
            {
                if (vertices.TryGetValue((x, z), out int existing)) return existing;
                int id = positions.Count / 3;
                vertices.Add((x, z), id);
                positions.Add((float)x / lattice - 0.5f); positions.Add(0); positions.Add((float)z / lattice - 0.5f);
                return id;
            }
            void Triangle(int a, int b, int c) { indices.Add(a); indices.Add(b); indices.Add(c); }
            bool Water(int x, int z) => x >= 0 && x < cols && z >= 0 && z < rows && resolved[z * cols + x] != TerrainType.Land;
            for (int z = -padCells; z < rows + padCells; z++)
                for (int x = -padCells; x < cols + padCells; x++)
                {
                    bool inside = x >= 0 && x < cols && z >= 0 && z < rows;
                    if (inside && !Water(x, z)) continue;
                    int ox = x * lattice, oz = z * lattice;
                    if (inside)
                    {
                        for (int v = 0; v < n; v++)
                            for (int u = 0; u < n; u++)
                            {
                                int a = Vertex(ox + u * 2, oz + v * 2), b = Vertex(ox + u * 2, oz + v * 2 + 2);
                                int c = Vertex(ox + u * 2 + 2, oz + v * 2 + 2), d = Vertex(ox + u * 2 + 2, oz + v * 2);
                                Triangle(a, b, c); Triangle(a, c, d);
                            }
                    }
                    else
                    {
                        // Only the edge touching the fine grid is split. The rest of the ring stays coarse.
                        var edge = new List<int>();
                        int[] counts = { Water(x - 1, z) ? n : 1, Water(x, z + 1) ? n : 1,
                            Water(x + 1, z) ? n : 1, Water(x, z - 1) ? n : 1 };
                        for (int side = 0; side < 4; side++)
                            for (int k = 0; k < counts[side]; k++)
                            {
                                int t = k * lattice / counts[side];
                                edge.Add(side == 0 ? Vertex(ox, oz + t) : side == 1 ? Vertex(ox + t, oz + lattice)
                                    : side == 2 ? Vertex(ox + lattice, oz + lattice - t) : Vertex(ox + lattice - t, oz));
                            }
                        if (edge.Count == 4) { Triangle(edge[0], edge[1], edge[2]); Triangle(edge[0], edge[2], edge[3]); }
                        else
                        {
                            int centre = Vertex(ox + n, oz + n);
                            for (int i = 0; i < edge.Count; i++) Triangle(centre, edge[i], edge[(i + 1) % edge.Count]);
                        }
                    }
                }
            return (positions.ToArray(), indices.ToArray());
        }
    }
}
