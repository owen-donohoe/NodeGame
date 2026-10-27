using System;
using System.Collections.Generic;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class WaterTests
    {
        [Test]
        public void FieldEncodesLandDistanceDampingAndBody()
        {
            var f = ShoreField.Bake(4, 1, new[] { TerrainType.Land, TerrainType.Ocean, TerrainType.Lake, TerrainType.Ocean }, 1, 1);
            int at = (f.width + 1) * 4;
            Assert.AreEqual(0, f.rgba[at]); Assert.AreEqual(0, f.rgba[at + 1]); Assert.AreEqual(0, f.rgba[at + 3]);
            Assert.AreEqual(42, f.rgba[at + 4]); Assert.AreEqual(85, f.rgba[at + 5]); Assert.AreEqual(255, f.rgba[at + 7]);
            Assert.Less(f.rgba[at + 4], f.rgba[at + 8]); Assert.Less(f.rgba[at + 8], f.rgba[at + 12]);
            Assert.AreEqual(0, f.rgba[at + 6]); Assert.AreEqual(255, f.rgba[at + 10]);
        }

        [Test]
        public void DistanceToCornerIsEuclidean()
        {
            var terrain = new TerrainType[9];
            for (int i = 0; i < terrain.Length; i++) terrain[i] = TerrainType.Ocean;
            terrain[0] = TerrainType.Land;
            var f = ShoreField.Bake(3, 3, terrain, 3, 0);
            Assert.AreEqual(Math.Sqrt(2), Math.Sqrt(ShoreField.DistanceSquaredToSquare(1.5, 1.5, 0, 0)));
            // Texel (4,4) is (1,1): half a cell from each edge of the land square.
            int at = (4 * f.width + 4) * 4;
            Assert.AreEqual(Math.Round(Math.Sqrt(0.5) / 3 * 255), f.rgba[at]);
            Assert.AreEqual(Math.Round(Math.Sqrt(0.5) / 1.5 * 255), f.rgba[at + 1]);
        }

        [Test]
        public void PaddingIsOceanAndBakeIsRepeatable()
        {
            var terrain = new[] { TerrainType.Lake };
            var a = ShoreField.Bake(1, 1, terrain);
            var b = ShoreField.Bake(1, 1, terrain);
            Assert.AreEqual(144, a.width); Assert.AreEqual(144, a.height);
            Assert.AreEqual(-4.5f, a.originX); Assert.AreEqual(-4.5f, a.originZ);
            Assert.AreEqual(1f / 16, a.worldPerTexel);
            Assert.AreEqual(255, a.rgba[0]); Assert.AreEqual(255, a.rgba[1]);
            Assert.AreEqual(0, a.rgba[2]); Assert.AreEqual(255, a.rgba[3]);
            CollectionAssert.AreEqual(a.rgba, b.rgba);
        }

        [TestCase(0, 4)] [TestCase(1, 1)] [TestCase(1, 16)] [TestCase(4, 16)]
        public void EveryOutermostTexelIsOpenOceanForClampSampling(int pad, int resolution)
        {
            var f = ShoreField.Bake(2, 2, new[] { TerrainType.Land, TerrainType.DeepLake, TerrainType.Lake, TerrainType.Land }, resolution, pad);
            for (int z = 0; z < f.height; z++)
                for (int x = 0; x < f.width; x++)
                {
                    if (x != 0 && z != 0 && x != f.width - 1 && z != f.height - 1) continue;
                    int at = (z * f.width + x) * 4;
                    Assert.AreEqual(255, f.rgba[at], "R: saturated distance");
                    Assert.AreEqual(255, f.rgba[at + 1], "G: full wave amplitude");
                    Assert.AreEqual(0, f.rgba[at + 2], "B: ocean palette");
                    Assert.AreEqual(255, f.rgba[at + 3], "A: water");
                }
        }

        [TestCase(1)] [TestCase(4)] [TestCase(6)] [TestCase(8)]
        public void SkirtHasExactExtentBeyondPaddingAndConstantCost(int n)
        {
            var terrain = new[] { TerrainType.Land, TerrainType.Ocean, TerrainType.Lake, TerrainType.DeepLake, TerrainType.Ocean, TerrainType.Land };
            var match = WaterMeshBuilder.Build(3, 2, terrain, n, 4, 40);
            var lobby = WaterMeshBuilder.Build(3, 2, terrain, n, 4, 80);
            Assert.AreEqual(match.positions.Length, lobby.positions.Length, "Skirt radius must not increase vertex cost.");
            Assert.AreEqual(match.indices.Length, lobby.indices.Length, "Skirt radius must not increase triangle cost.");
            void CheckExtent(float[] positions, int skirt)
            {
                float minX = float.PositiveInfinity, minZ = minX, maxX = float.NegativeInfinity, maxZ = maxX;
                for (int i = 0; i < positions.Length; i += 3)
                {
                    minX = Math.Min(minX, positions[i]); maxX = Math.Max(maxX, positions[i]);
                    minZ = Math.Min(minZ, positions[i + 2]); maxZ = Math.Max(maxZ, positions[i + 2]);
                    Assert.AreEqual(0, positions[i + 1], "The CPU skirt stays flat.");
                }
                Assert.AreEqual(-skirt - 0.5f, minX); Assert.AreEqual(-skirt - 0.5f, minZ);
                Assert.AreEqual(3 + 2 * skirt, maxX - minX); Assert.AreEqual(2 + 2 * skirt, maxZ - minZ);
                Assert.Less(minX, -4.5f); Assert.Greater(maxX, 3 + 4 - 0.5f);
            }
            CheckExtent(match.positions, 40); CheckExtent(lobby.positions, 80);
        }

        [TestCase(0, 1)] [TestCase(1, 4)] [TestCase(2, 8)] [TestCase(3, 8)]
        public void MeshCountsMatchTierAndOmitLand(int tier, int n)
        {
            Assert.AreEqual(n, WaterMeshBuilder.SubdivisionsForTier(tier));
            var m = WaterMeshBuilder.Build(2, 1, new[] { TerrainType.Ocean, TerrainType.Land }, n, 0, 0);
            Assert.AreEqual((n + 1) * (n + 1) * 3, m.positions.Length);
            Assert.AreEqual(n * n * 6, m.indices.Length);
            for (int i = 0; i < m.positions.Length; i += 3) Assert.LessOrEqual(m.positions[i], 0.5f);
        }

        [TestCase(1, 1)] [TestCase(4, 1)] [TestCase(6, 1)] [TestCase(8, 1)]
        [TestCase(1, 40)] [TestCase(4, 40)] [TestCase(6, 40)] [TestCase(8, 40)]
        [TestCase(8, 80)]
        public void SharedEdgesHaveNoDuplicateVerticesOrTJunctions(int n, int skirt)
        {
            var m = WaterMeshBuilder.Build(2, 1, new[] { TerrainType.Ocean, TerrainType.Lake }, n, 1, skirt);
            var seen = new HashSet<(float, float)>();
            for (int i = 0; i < m.positions.Length; i += 3)
                Assert.IsTrue(seen.Add((m.positions[i], m.positions[i + 2])));
            var edges = new Dictionary<(int, int), int>();
            for (int i = 0; i < m.indices.Length; i += 3)
                for (int j = 0; j < 3; j++)
                {
                    int a = m.indices[i + j], b = m.indices[i + (j + 1) % 3];
                    var edge = (Math.Min(a, b), Math.Max(a, b));
                    edges.TryGetValue(edge, out int count); edges[edge] = count + 1;
                }
            foreach (var pair in edges)
                if (pair.Value == 1)
                {
                    int a = pair.Key.Item1 * 3, b = pair.Key.Item2 * 3;
                    bool outside = m.positions[a] == m.positions[b] && (m.positions[a] == -skirt - 0.5f || m.positions[a] == 2 + skirt - 0.5f)
                        || m.positions[a + 2] == m.positions[b + 2] && (m.positions[a + 2] == -skirt - 0.5f || m.positions[a + 2] == 1 + skirt - 0.5f);
                    Assert.IsTrue(outside, "Every interior edge must have two incident triangles.");
                }
        }
    }
}
