using System.Collections.Generic;
using NodeWar.Simulation;

namespace NodeWar.View
{
    public static class WaterMeshBuilder
    {
        public static int SubdivisionsForTier(int tier, float waveScale = 1f, float cellSize = 6f) => WaterFeatures.SubdivisionsFor(tier, WaterFeatures.WavesFor(tier), waveScale, cellSize);

        public static (float[] positions, int[] indices, int[] nearIndices, int[] farIndices) Build(int cols, int rows, TerrainType[] resolved,
            int subdivisionsPerCell, int padCells, int skirtCells)
        {
            ShoreField.Validate(cols, rows, resolved, subdivisionsPerCell, padCells);
            if (skirtCells < padCells) throw new System.ArgumentOutOfRangeException(nameof(skirtCells));
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
            void Quad(int x0, int z0, int x1, int z1)
            {
                int a = Vertex(x0, z0), b = Vertex(x0, z1), c = Vertex(x1, z1), d = Vertex(x1, z0);
                Triangle(a, b, c); Triangle(a, c, d);
            }
            for (int z = -padCells; z < rows + padCells; z++)
                for (int x = -padCells; x < cols + padCells; x++)
                {
                    int ox = x * lattice, oz = z * lattice;
                    // Padding carries waves too, so retain the same sampling density to the fade edge.
                    for (int v = 0; v < n; v++)
                        for (int u = 0; u < n; u++)
                        {
                            int a = Vertex(ox + u * 2, oz + v * 2), b = Vertex(ox + u * 2, oz + v * 2 + 2);
                            int c = Vertex(ox + u * 2 + 2, oz + v * 2 + 2), d = Vertex(ox + u * 2 + 2, oz + v * 2);
                            Triangle(a, b, c); Triangle(a, c, d);
                        }
                }
            int nearCount = indices.Count;
            if (skirtCells > padCells)
            {
                int left = -padCells * lattice, right = (cols + padCells) * lattice;
                int bottom = -padCells * lattice, top = (rows + padCells) * lattice;
                int outerLeft = -skirtCells * lattice, outerRight = (cols + skirtCells) * lattice;
                int outerBottom = -skirtCells * lattice, outerTop = (rows + skirtCells) * lattice;
                // Long, unsubdivided strips share every padding-edge vertex. Four corner quads
                // close the ring; cost depends on the inner perimeter, never the skirt's area.
                int step = 2;
                for (int z = bottom; z < top; z += step)
                {
                    Quad(outerLeft, z, left, z + step);
                    Quad(right, z, outerRight, z + step);
                }
                for (int x = left; x < right; x += step)
                {
                    Quad(x, outerBottom, x + step, bottom);
                    Quad(x, top, x + step, outerTop);
                }
                Quad(outerLeft, outerBottom, left, bottom);
                Quad(right, outerBottom, outerRight, bottom);
                Quad(outerLeft, top, left, outerTop);
                Quad(right, top, outerRight, outerTop);
            }
            return (positions.ToArray(), indices.ToArray(), indices.GetRange(0, nearCount).ToArray(), indices.GetRange(nearCount, indices.Count - nearCount).ToArray());
        }
    }
}

