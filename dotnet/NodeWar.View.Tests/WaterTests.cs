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
            var f = ShoreField.Bake(4, 1, new[] { TerrainType.Land, TerrainType.Ocean, TerrainType.Lake, TerrainType.Ocean }, 1, 0);
            Assert.AreEqual(0, f.rgba[0]); Assert.AreEqual(0, f.rgba[1]); Assert.AreEqual(0, f.rgba[3]);
            Assert.AreEqual(42, f.rgba[4]); Assert.AreEqual(85, f.rgba[5]); Assert.AreEqual(255, f.rgba[7]);
            Assert.Less(f.rgba[4], f.rgba[8]); Assert.Less(f.rgba[8], f.rgba[12]);
            Assert.AreEqual(0, f.rgba[6]); Assert.AreEqual(255, f.rgba[10]);
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

        [TestCase(0, 1)] [TestCase(1, 4)] [TestCase(2, 6)] [TestCase(3, 8)]
        public void MeshCountsMatchTierAndOmitLand(int tier, int n)
        {
            Assert.AreEqual(n, WaterMeshBuilder.SubdivisionsForTier(tier));
            var m = WaterMeshBuilder.Build(2, 1, new[] { TerrainType.Ocean, TerrainType.Land }, n, 0);
            Assert.AreEqual((n + 1) * (n + 1) * 3, m.positions.Length);
            Assert.AreEqual(n * n * 6, m.indices.Length);
            for (int i = 0; i < m.positions.Length; i += 3) Assert.LessOrEqual(m.positions[i], 0.5f);
        }

        [TestCase(1)] [TestCase(4)] [TestCase(6)] [TestCase(8)]
        public void SharedEdgesHaveNoDuplicateVerticesOrTJunctions(int n)
        {
            var m = WaterMeshBuilder.Build(2, 1, new[] { TerrainType.Ocean, TerrainType.Lake }, n, 1);
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
                    bool outside = m.positions[a] == m.positions[b] && (m.positions[a] == -1.5f || m.positions[a] == 2.5f)
                        || m.positions[a + 2] == m.positions[b + 2] && (m.positions[a + 2] == -1.5f || m.positions[a + 2] == 1.5f);
                    Assert.IsTrue(outside, "Every interior edge must have two incident triangles.");
                }
        }
    }
}
