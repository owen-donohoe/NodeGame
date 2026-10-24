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

        public static ShoreFieldData Bake(int cols, int rows, TerrainType[] resolved, int texelsPerCell = 16, int padCells = 4)
        {
            Validate(cols, rows, resolved, texelsPerCell, padCells);
            var land = new List<int>();
            for (int i = 0; i < resolved.Length; i++)
                if (resolved[i] == TerrainType.Land) land.Add(i);
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
                    // Clamp sampling beyond the baked padding must remain deep, fully waving ocean.
                    if (x == 0 || z == 0 || x == field.width - 1 || z == field.height - 1)
                    {
                        field.rgba[at] = 255; field.rgba[at + 1] = 255;
                        field.rgba[at + 2] = 0; field.rgba[at + 3] = 255;
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
                    field.rgba[at + 1] = Encode(distance / 1.5);
                    field.rgba[at + 2] = WaterBodyOf(terrain);
                    field.rgba[at + 3] = terrain == TerrainType.Land ? (byte)0 : (byte)255;
                }
            return field;
        }

        internal static double DistanceSquaredToSquare(double x, double z, int landX, int landZ)
        {
            double dx = Math.Max(Math.Abs(x - landX) - 0.5, 0);
            double dz = Math.Max(Math.Abs(z - landZ) - 0.5, 0);
            return dx * dx + dz * dz;
        }

        private static byte Encode(double value) => (byte)Math.Round(Math.Min(1, Math.Max(0, value)) * 255);
    }
}
