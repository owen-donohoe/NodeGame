using System;
using System.Collections.Generic;
using NodeWar.Simulation;

namespace NodeWar.View
{
    public sealed class ShoreFieldData
    {
        public int width, height;
        public byte[] rgba;
        public float originX, originZ, worldPerTexel;
    }

    /// <summary>Cell centres are integer XZ; distance is to the closed land squares.</summary>
    public static class ShoreField
    {
        public static byte WaterBodyOf(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Ocean: return 0;
                case TerrainType.DeepLake: return 128;
                default: return 255;
            }
        }

        internal static void Validate(int cols, int rows, TerrainType[] resolved, int resolution, int pad)
        {
            if (cols <= 0 || rows <= 0 || resolution <= 0 || pad < 0)
                throw new ArgumentOutOfRangeException(nameof(cols));
            if (resolved == null || resolved.Length != checked(cols * rows))
                throw new ArgumentException("Terrain must contain exactly one entry per cell.", nameof(resolved));
        }

        public static ShoreFieldData Bake(int cols, int rows, TerrainType[] resolved, int texelsPerCell = 16, int padCells = 4, float blendCells = 1f)
        {
            Validate(cols, rows, resolved, texelsPerCell, padCells);
            if (blendCells <= 0) throw new ArgumentOutOfRangeException(nameof(blendCells));
            var ocean = new List<int>();
            var lakes = new List<int>();
            var land = new List<int>();
            for (int i = 0; i < resolved.Length; i++)
                if (resolved[i] == TerrainType.Land) land.Add(i);
                else if (resolved[i] == TerrainType.Ocean) ocean.Add(i);
                else lakes.Add(i);
            var field = new ShoreFieldData
            {
                width = checked((cols + 2 * padCells) * texelsPerCell),
                height = checked((rows + 2 * padCells) * texelsPerCell),
                originX = -padCells - 0.5f, originZ = -padCells - 0.5f,
                worldPerTexel = 1f / texelsPerCell
            };
            field.rgba = new byte[checked(field.width * field.height * 4)];
            for (int z = 0; z < field.height; z++)
                for (int x = 0; x < field.width; x++)
                {
                    int at = (z * field.width + x) * 4;
                    field.rgba[at + 1] = Encode((FoamNoise.Fbm((double)x / Math.Max(1, field.width - 1), (double)z / Math.Max(1, field.height - 1), 0x39a7, 2, 3) - 0.5) * 2.5 + 0.5);
                    // Clamp sampling beyond the baked padding must remain deep, fully waving ocean.
                    if (x == 0 || z == 0 || x == field.width - 1 || z == field.height - 1)
                    {
                        field.rgba[at] = 255;
                        field.rgba[at + 2] = 0; field.rgba[at + 3] = 0;
                        continue;
                    }
                    double px = field.originX + (x + 0.5) / texelsPerCell;
                    double pz = field.originZ + (z + 0.5) / texelsPerCell;
                    double distanceSquared = double.PositiveInfinity;
                    for (int i = 0; i < land.Count; i++)
                    {
                        distanceSquared = Math.Min(distanceSquared, DistanceSquaredToSquare(px, pz, land[i] % cols, land[i] / cols));
                    }
                    double distance = Math.Sqrt(distanceSquared);
                    int cx = x / texelsPerCell - padCells, cz = z / texelsPerCell - padCells;
                    TerrainType terrain = cx >= 0 && cx < cols && cz >= 0 && cz < rows
                        ? resolved[cz * cols + cx] : TerrainType.Ocean;
                    field.rgba[at] = Encode(distance / 3);
                    field.rgba[at + 2] = WaterBodyOf(terrain);
                    // R retains land distance; G holds static swash phase; B retains body ID. A is ocean/lake mix.
                    // Land is already identifiable by R=0 within the plinth, so no mask is lost.
                    bool isLake = terrain != TerrainType.Ocean && terrain != TerrainType.Land;
                    List<int> opposite = isLake ? ocean : lakes;
                    double bodyDistanceSquared = double.PositiveInfinity;
                    int nearestOpposite = -1;
                    for (int i = 0; i < opposite.Count; i++)
                    {
                        double candidate = DistanceSquaredToSquare(px, pz, opposite[i] % cols, opposite[i] / cols, 0.5 / texelsPerCell);
                        if (candidate < bodyDistanceSquared) { bodyDistanceSquared = candidate; nearestOpposite = opposite[i]; }
                    }
                    if (isLake)
                    {
                        // The area outside the board is ocean, including boards with no ocean cells.
                        double edgeDistance = Math.Min(Math.Min(px + 0.5, cols - 0.5 - px), Math.Min(pz + 0.5, rows - 0.5 - pz));
                        edgeDistance += 0.5 / texelsPerCell;
                        bodyDistanceSquared = Math.Min(bodyDistanceSquared, edgeDistance * edgeDistance);
                    }
                    // Half a texel centres the signed mix on the boundary between water texels.
                    double bodyDistance = Math.Max(0, Math.Sqrt(bodyDistanceSquared) - 0.5 / texelsPerCell);
                    // On the ocean half of a blend B identifies the destination lake palette;
                    // A < 0.5 still identifies ocean. This preserves DeepLake colour on both sides.
                    if (terrain == TerrainType.Ocean && bodyDistance < blendCells * 0.5 && nearestOpposite >= 0)
                        field.rgba[at + 2] = WaterBodyOf(resolved[nearestOpposite]);
                    field.rgba[at + 3] = terrain == TerrainType.Land ? (byte)0
                        : Encode(0.5 + (isLake ? bodyDistance : -bodyDistance) / blendCells);
                }
            return field;
        }

        internal static double DistanceSquaredToSquare(double x, double z, int landX, int landZ, double inset = 0)
        {
            double dx = Math.Max(Math.Abs(x - landX) - (0.5 - inset), 0);
            double dz = Math.Max(Math.Abs(z - landZ) - (0.5 - inset), 0);
            return dx * dx + dz * dz;
        }

        private static byte Encode(double value) => (byte)Math.Round(Math.Min(1, Math.Max(0, value)) * 255);
    }
}
