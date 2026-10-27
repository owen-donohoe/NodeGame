using System;
using NodeWar.Simulation;

namespace NodeWar.Lobby
{
    /// <summary>Axis-aligned XZ footprint in stage-local units.</summary>
    public readonly struct LobbyLandBounds
    {
        public readonly float minX, minZ, maxX, maxZ;
        public LobbyLandBounds(float minX, float minZ, float maxX, float maxZ)
        {
            this.minX = minX; this.minZ = minZ; this.maxX = maxX; this.maxZ = maxZ;
        }
    }

    public sealed class LobbyWaterGridData
    {
        public int cols, rows;
        public float originX, originZ, cellSize;
        public TerrainType[] terrain;
    }

    public static class LobbyWaterGrid
    {
        /// <summary>Only squares entirely covered by a primitive are land, so water cannot leave holes outside it.</summary>
        public static LobbyWaterGridData Build(LobbyLandBounds[] land, float cellSize = 1f)
        {
            if (land == null || land.Length == 0) throw new ArgumentException("At least one land footprint is required.", nameof(land));
            if (float.IsNaN(cellSize) || float.IsInfinity(cellSize) || cellSize <= 0) throw new ArgumentOutOfRangeException(nameof(cellSize));
            float minX = float.PositiveInfinity, minZ = minX, maxX = float.NegativeInfinity, maxZ = maxX;
            for (int i = 0; i < land.Length; i++)
            {
                LobbyLandBounds b = land[i];
                if (!Finite(b.minX) || !Finite(b.minZ) || !Finite(b.maxX) || !Finite(b.maxZ) || b.minX >= b.maxX || b.minZ >= b.maxZ)
                    throw new ArgumentException("Land footprints must have finite, positive extents.", nameof(land));
                minX = Math.Min(minX, b.minX); minZ = Math.Min(minZ, b.minZ);
                maxX = Math.Max(maxX, b.maxX); maxZ = Math.Max(maxZ, b.maxZ);
            }
            float left = (float)Math.Floor(minX / cellSize) * cellSize;
            float bottom = (float)Math.Floor(minZ / cellSize) * cellSize;
            var grid = new LobbyWaterGridData
            {
                cols = checked((int)Math.Ceiling((maxX - left) / cellSize)),
                rows = checked((int)Math.Ceiling((maxZ - bottom) / cellSize)),
                originX = left + cellSize * 0.5f, originZ = bottom + cellSize * 0.5f, cellSize = cellSize
            };
            grid.terrain = new TerrainType[checked(grid.cols * grid.rows)];
            for (int z = 0; z < grid.rows; z++)
                for (int x = 0; x < grid.cols; x++)
                {
                    float x0 = left + x * cellSize, z0 = bottom + z * cellSize;
                    TerrainType type = TerrainType.Ocean;
                    for (int i = 0; i < land.Length; i++)
                        if (x0 >= land[i].minX && x0 + cellSize <= land[i].maxX && z0 >= land[i].minZ && z0 + cellSize <= land[i].maxZ)
                        { type = TerrainType.Land; break; }
                    grid.terrain[z * grid.cols + x] = type;
                }
            return grid;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
