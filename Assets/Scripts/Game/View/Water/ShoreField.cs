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

        internal static ShoreFieldData BakeBruteForce(int cols, int rows, TerrainType[] resolved, int texelsPerCell = 16, int padCells = 4, float blendCells = 1f)
        {
            Validate(cols, rows, resolved, texelsPerCell, padCells);
            if (blendCells <= 0) throw new ArgumentOutOfRangeException(nameof(blendCells));
            var ocean = new List<int>();
            var lakes = new List<int>();

            var land = new List<int>();
            for (int i = 0; i < resolved.Length; i++)
                if (resolved[i] == TerrainType.Land) land.Add(i);
                else if (resolved[i] == TerrainType.Ocean) ocean.Add(i);
                else if (resolved[i] != TerrainType.DeepLake) lakes.Add(i);

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
                    // R: land distance; G: static swash phase; B: own body ID.
                    // A: signed ocean/lake distance (R=1); deep lake keeps its inward ramp.
                    // B: 0 ocean, 255 lake from ocean, 128 deep from lake, 64 deep from ocean.
                    bool isLake = terrain != TerrainType.Ocean && terrain != TerrainType.Land;
                    double bodyDistanceSquared = double.PositiveInfinity;
                    if (isLake)
                    {
                        for (int i = 0; i < ocean.Count; i++)
                            bodyDistanceSquared = Math.Min(bodyDistanceSquared,
                                DistanceSquaredToSquare(px, pz, ocean[i] % cols, ocean[i] / cols));
                        // The area outside the board is ocean, even without ocean cells.
                        double edgeDistance = Math.Min(Math.Min(px + 0.5, cols - 0.5 - px), Math.Min(pz + 0.5, rows - 0.5 - pz));
                        bodyDistanceSquared = Math.Min(bodyDistanceSquared, edgeDistance * edgeDistance);
                    }
                    if (terrain == TerrainType.DeepLake)
                    {
                        double lakeDistanceSquared = double.PositiveInfinity;
                        for (int i = 0; i < lakes.Count; i++)
                            lakeDistanceSquared = Math.Min(lakeDistanceSquared,
                                DistanceSquaredToSquare(px, pz, lakes[i] % cols, lakes[i] / cols));
                        bool fromLake = lakeDistanceSquared <= bodyDistanceSquared;
                        field.rgba[at + 2] = fromLake ? (byte)128 : (byte)64;
                        bodyDistanceSquared = Math.Min(bodyDistanceSquared, lakeDistanceSquared);
                    }
                    if (terrain == TerrainType.Ocean)
                    {
                        double lakeDistanceSquared = double.PositiveInfinity;
                        for (int i = 0; i < lakes.Count; i++)
                            lakeDistanceSquared = Math.Min(lakeDistanceSquared,
                                DistanceSquaredToSquare(px, pz, lakes[i] % cols, lakes[i] / cols));
                        field.rgba[at + 3] = EncodeBorder(-Math.Sqrt(lakeDistanceSquared));
                    }
                    else field.rgba[at + 3] = terrain == TerrainType.DeepLake
                        ? Encode(Math.Sqrt(bodyDistanceSquared) / blendCells)
                        : terrain == TerrainType.Land ? (byte)0 : EncodeBorder(Math.Sqrt(bodyDistanceSquared));
                }
            return field;
        }

        public static ShoreFieldData Bake(int cols, int rows, TerrainType[] resolved, int texelsPerCell = 16, int padCells = 4, float blendCells = 1f)
        {
            var workspace = new Workspace(cols, rows, texelsPerCell, padCells, blendCells);
            workspace.Bake(resolved);
            return workspace.Field;
        }

        /// <summary>Single-owner reusable baker. Never share an instance between worker and main threads.</summary>
        public sealed class Workspace
        {
            public ShoreFieldData Field { get; }
            private readonly int cols, rows, resolution, pad, gw, gh;
            private readonly double blendCells;
            private readonly double[] grid, scratch, line, output, cuts, ocean, lakes;
            private readonly int[] sites;
            private readonly TerrainType[] terrain;
            private bool initialized;
            private const double Infinity = 1e20;

            public Workspace(int cols, int rows, int resolution = 16, int pad = 4, double blendCells = 1)
            {
                Validate(cols, rows, new TerrainType[checked(cols * rows)], resolution, pad);
                if (blendCells <= 0) throw new ArgumentOutOfRangeException(nameof(blendCells));
                this.cols = cols; this.rows = rows; this.resolution = resolution; this.pad = pad; this.blendCells = blendCells;
                Field = new ShoreFieldData { width = (cols + 2 * pad) * resolution, height = (rows + 2 * pad) * resolution,
                    originX = -pad - 0.5f, originZ = -pad - 0.5f, worldPerTexel = 1f / resolution };
                Field.rgba = new byte[checked(Field.width * Field.height * 4)];
                terrain = new TerrainType[cols * rows];
                // Half-texel lattice includes closed square boundaries AND texel centres.
                // This makes the ordinary FH point-source EDT exact for distance to squares.
                gw = Field.width * 2 + 1; gh = Field.height * 2 + 1;
                grid = new double[checked(gw * gh)]; scratch = new double[grid.Length];
                int length = Math.Max(gw, gh);
                line = new double[length]; output = new double[length]; cuts = new double[length + 1]; sites = new int[length];
                ocean = new double[Field.width * Field.height]; lakes = new double[ocean.Length];
                for (int z = 0; z < Field.height; z++)
                    for (int x = 0; x < Field.width; x++)
                        Field.rgba[(z * Field.width + x) * 4 + 1] = Encode((FoamNoise.Fbm((double)x / Math.Max(1, Field.width - 1),
                            (double)z / Math.Max(1, Field.height - 1), 0x39a7, 2, 3) - 0.5) * 2.5 + 0.5);
            }

            public void Bake(TerrainType[] resolved)
            {
                Validate(cols, rows, resolved, resolution, pad);
                Transform(resolved, TerrainType.Land, null);
                for (int z = 0; z < Field.height; z++)
                    for (int x = 0; x < Field.width; x++)
                        Field.rgba[(z * Field.width + x) * 4] = Border(x, z) ? (byte)255 : Encode(Math.Sqrt(grid[(z * 2 + 1) * gw + x * 2 + 1]) / (2 * resolution * 3));
                BakeBodies(resolved);
                Array.Copy(resolved, terrain, terrain.Length);
                initialized = true;
            }

            public bool TryAddLand(TerrainType[] resolved)
            {
                Validate(cols, rows, resolved, resolution, pad);
                if (!initialized) return false;
                bool bodyInputsChanged = false, hasLake = false, hasDeep = false;
                for (int i = 0; i < resolved.Length; i++)
                {
                    if (resolved[i] != terrain[i] && (resolved[i] != TerrainType.Land || terrain[i] == TerrainType.DeepLake)) return false;
                    hasLake |= resolved[i] == TerrainType.Lake || resolved[i] == TerrainType.DeepLake;
                    hasDeep |= resolved[i] == TerrainType.DeepLake;
                }
                for (int i = 0; i < resolved.Length; i++)
                {
                    if (resolved[i] == terrain[i]) continue;
                    bodyInputsChanged |= (terrain[i] == TerrainType.Ocean && hasLake) || (terrain[i] == TerrainType.Lake && hasDeep);
                    if (terrain[i] == TerrainType.Lake && !hasDeep)
                        RefreshOceanBorder(resolved, i % cols, i / cols);
                    int cx = i % cols, cz = i / cols;
                    int x0 = Math.Max(1, (cx + pad - 3) * resolution), x1 = Math.Min(Field.width - 2, (cx + pad + 4) * resolution - 1);
                    int z0 = Math.Max(1, (cz + pad - 3) * resolution), z1 = Math.Min(Field.height - 2, (cz + pad + 4) * resolution - 1);
                    for (int z = z0; z <= z1; z++)
                        for (int x = x0; x <= x1; x++)
                        {
                            double px = Field.originX + (x + 0.5) / resolution, pz = Field.originZ + (z + 0.5) / resolution;
                            int at = (z * Field.width + x) * 4;
                            Field.rgba[at] = Math.Min(Field.rgba[at], Encode(Math.Sqrt(DistanceSquaredToSquare(px, pz, cx, cz)) / 3));
                            // The changed square now has land's own body encoding. Phase never changes.
                            if (x / resolution - pad == cx && z / resolution - pad == cz)
                            { Field.rgba[at + 2] = 255; Field.rgba[at + 3] = 0; }
                        }
                }
                // Removing a shallower source can change deep/lake mix beyond the new shore.
                // In that uncommon case recompute body EDTs; land distance remains incremental.
                if (bodyInputsChanged) BakeBodies(resolved);
                Array.Copy(resolved, terrain, terrain.Length);
                return true;
            }

            // Removed lake sources affect ocean A only within R=1 plus one filtered texel.
            private void RefreshOceanBorder(TerrainType[] resolved, int cx, int cz)
            {
                int reach = resolution + 1;
                int x0 = Math.Max(1, (cx + pad) * resolution - reach);
                int x1 = Math.Min(Field.width - 2, (cx + pad + 1) * resolution - 1 + reach);
                int z0 = Math.Max(1, (cz + pad) * resolution - reach);
                int z1 = Math.Min(Field.height - 2, (cz + pad + 1) * resolution - 1 + reach);
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                    {
                        int tx = x / resolution - pad, tz = z / resolution - pad;
                        if (tx >= 0 && tx < cols && tz >= 0 && tz < rows && resolved[tz * cols + tx] != TerrainType.Ocean) continue;
                        double px = Field.originX + (x + 0.5) / resolution, pz = Field.originZ + (z + 0.5) / resolution;
                        double nearest = double.PositiveInfinity;
                        for (int cell = 0; cell < resolved.Length; cell++)
                            if (resolved[cell] == TerrainType.Lake)
                                nearest = Math.Min(nearest, DistanceSquaredToSquare(px, pz, cell % cols, cell / cols));
                        Field.rgba[(z * Field.width + x) * 4 + 3] = EncodeBorder(-Math.Sqrt(nearest));
                    }
            }

            private bool Border(int x, int z) => x == 0 || z == 0 || x == Field.width - 1 || z == Field.height - 1;

            private void BakeBodies(TerrainType[] resolved)
            {
                bool hasLake = false;
                for (int i = 0; i < resolved.Length; i++) hasLake |= resolved[i] == TerrainType.Lake || resolved[i] == TerrainType.DeepLake;
                if (hasLake) Transform(resolved, TerrainType.Ocean, ocean);
                if (hasLake) Transform(resolved, TerrainType.Lake, lakes);
                for (int z = 0; z < Field.height; z++)
                    for (int x = 0; x < Field.width; x++)
                    {
                        int pixel = z * Field.width + x, at = pixel * 4;
                        int cx = x / resolution - pad, cz = z / resolution - pad;
                        TerrainType own = !Border(x, z) && cx >= 0 && cx < cols && cz >= 0 && cz < rows ? resolved[cz * cols + cx] : TerrainType.Ocean;
                        byte body = WaterBodyOf(own), mix = 0;
                        if (own != TerrainType.Ocean && own != TerrainType.Land)
                        {
                            double px = Field.originX + (x + 0.5) / resolution, pz = Field.originZ + (z + 0.5) / resolution;
                            double edge = Math.Min(Math.Min(px + 0.5, cols - 0.5 - px), Math.Min(pz + 0.5, rows - 0.5 - pz));
                            double distance = Math.Min(ocean[pixel], edge * edge);
                            if (own == TerrainType.DeepLake)
                            {
                                double lakeDistance = lakes[pixel];
                                // Preserve the old body-ID decision at floating-point ties (notably odd resolutions).
                                if (Math.Abs(lakeDistance - distance) < 1e-10)
                                {
                                    distance = edge * edge; lakeDistance = double.PositiveInfinity;
                                    for (int cell = 0; cell < resolved.Length; cell++)
                                    {
                                        if (resolved[cell] == TerrainType.Ocean)
                                            distance = Math.Min(distance, DistanceSquaredToSquare(px, pz, cell % cols, cell / cols));
                                        else if (resolved[cell] == TerrainType.Lake)
                                            lakeDistance = Math.Min(lakeDistance, DistanceSquaredToSquare(px, pz, cell % cols, cell / cols));
                                    }
                                }
                                body = lakeDistance <= distance ? (byte)128 : (byte)64;
                                distance = Math.Min(distance, lakeDistance);
                            }
                            mix = own == TerrainType.DeepLake ? Encode(Math.Sqrt(distance) / blendCells) : EncodeBorder(Math.Sqrt(distance));
                        }
                        if (own == TerrainType.Ocean && hasLake && !Border(x, z))
                            mix = EncodeBorder(-Math.Sqrt(lakes[pixel]));
                        Field.rgba[at + 2] = body; Field.rgba[at + 3] = mix;
                    }
            }

            private void Transform(TerrainType[] resolved, TerrainType source, double[] target)
            {
                for (int i = 0; i < grid.Length; i++) grid[i] = Infinity;
                for (int cell = 0; cell < resolved.Length; cell++)
                {
                    if (resolved[cell] != source) continue;
                    int left = (cell % cols + pad) * resolution * 2, bottom = (cell / cols + pad) * resolution * 2;
                    for (int z = bottom; z <= bottom + resolution * 2; z++)
                        for (int x = left; x <= left + resolution * 2; x++) grid[z * gw + x] = 0;
                }
                for (int z = 0; z < gh; z++)
                {
                    Array.Copy(grid, z * gw, line, 0, gw);
                    TransformLine(gw);
                    Array.Copy(output, 0, scratch, z * gw, gw);
                }
                for (int x = 0; x < gw; x++)
                {
                    for (int z = 0; z < gh; z++) line[z] = scratch[z * gw + x];
                    TransformLine(gh);
                    for (int z = 0; z < gh; z++) grid[z * gw + x] = output[z];
                }
                if (target == null) return;
                double unit = 4.0 * resolution * resolution;
                for (int z = 0; z < Field.height; z++)
                    for (int x = 0; x < Field.width; x++) target[z * Field.width + x] = grid[(z * 2 + 1) * gw + x * 2 + 1] / unit;
            }

            // Felzenszwalb-Huttenlocher lower envelope of parabolas, O(n).
            private void TransformLine(int length)
            {
                int last = -1;
                for (int q = 0; q < length; q++)
                {
                    if (line[q] >= Infinity) continue;
                    double cut = double.NegativeInfinity;
                    while (last >= 0)
                    {
                        int p = sites[last];
                        cut = ((line[q] + (double)q * q) - (line[p] + (double)p * p)) / (2 * (q - p));
                        if (cut > cuts[last]) break;
                        last--;
                    }
                    last++; sites[last] = q; cuts[last] = last == 0 ? double.NegativeInfinity : cut;
                    cuts[last + 1] = double.PositiveInfinity;
                }
                if (last < 0) { for (int q = 0; q < length; q++) output[q] = Infinity; return; }
                int k = 0;
                for (int q = 0; q < length; q++)
                {
                    while (cuts[k + 1] < q) k++;
                    double delta = q - sites[k]; output[q] = delta * delta + line[sites[k]];
                }
            }
        }
        internal static double DistanceSquaredToSquare(double x, double z, int landX, int landZ, double inset = 0)
        {
            double dx = Math.Max(Math.Abs(x - landX) - (0.5 - inset), 0);
            double dz = Math.Max(Math.Abs(z - landZ) - (0.5 - inset), 0);
            return dx * dx + dz * dz;
        }

        private static byte EncodeBorder(double signedDistance) => Encode(0.5 + Math.Min(1, Math.Max(-1, signedDistance)) / 2);

        private static byte Encode(double value) => (byte)Math.Round(Math.Min(1, Math.Max(0, value)) * 255);
    }
}
