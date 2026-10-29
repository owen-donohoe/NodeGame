using System;
using System.Collections.Generic;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class WaterTests
    {
        [TestCase(1, 0)] [TestCase(3, 1)] [TestCase(8, 4)] [TestCase(16, 4)]
        public void ExactEdtMatchesBruteForceOnVariedBoards(int resolution, int padding)
        {
            var random = new Random(917);
            for (int run = 0; run < 12; run++)
            {
                int cols = 1 + random.Next(7), rows = 1 + random.Next(6);
                var terrain = new TerrainType[cols * rows];
                var kinds = new[] { TerrainType.Ocean, TerrainType.Land, TerrainType.Lake, TerrainType.DeepLake };
                for (int i = 0; i < terrain.Length; i++) terrain[i] = kinds[random.Next(4)];
                var actual = ShoreField.Bake(cols, rows, terrain, resolution, padding);
                var expected = ShoreField.BakeBruteForce(cols, rows, terrain, resolution, padding);
                for (int i = 0; i < actual.rgba.Length; i++)
                    Assert.That(Math.Abs(actual.rgba[i] - expected.rgba[i]), Is.LessThanOrEqualTo(i % 4 == 2 ? 0 : 1), $"run {run}, byte {i}");
            }
        }

        [TestCase(1)] [TestCase(3)] [TestCase(16)]
        public void IncrementalEqualsFullBakeAcrossRandomPlacementsAndBodyChanges(int resolution)
        {
            var random = new Random(532);
            var terrain = new TerrainType[36];
            var kinds = new[] { TerrainType.Ocean, TerrainType.Lake, TerrainType.DeepLake };
            for (int i = 0; i < terrain.Length; i++) terrain[i] = kinds[random.Next(3)];
            var workspace = new ShoreField.Workspace(6, 6, resolution);
            workspace.Bake(terrain);
            byte[] buffer = workspace.Field.rgba;
            for (int step = 0; step < 35; step++)
            {
                int cell = random.Next(terrain.Length);
                terrain[cell] = step == 17 ? TerrainType.DeepLake : TerrainType.Land;
                if (!workspace.TryAddLand(terrain)) workspace.Bake(terrain);
                Assert.AreSame(buffer, workspace.Field.rgba);
                CollectionAssert.AreEqual(ShoreField.Bake(6, 6, terrain, resolution).rgba, workspace.Field.rgba, $"step {step}");
            }
        }

        [Test]
        public void ReusedBakerHandlesNoSourcesAndRejectsLandRemoval()
        {
            var terrain = new[] { TerrainType.Land, TerrainType.Ocean, TerrainType.Ocean, TerrainType.Lake };
            var workspace = new ShoreField.Workspace(2, 2);
            workspace.Bake(terrain);
            terrain[0] = TerrainType.Ocean;
            Assert.IsFalse(workspace.TryAddLand(terrain));
            workspace.Bake(terrain);
            CollectionAssert.AreEqual(ShoreField.BakeBruteForce(2, 2, terrain).rgba, workspace.Field.rgba);
        }
        [TestCase(0, 441)] [TestCase(1, 4761)] [TestCase(2, 33489)] [TestCase(3, 59049)]
        public void TenByTenOpenBoardVertexBudgetIncludesPaddingAndSkirt(int tier, int count)
        {
            var terrain = new TerrainType[100];
            for (int i = 0; i < terrain.Length; i++) terrain[i] = TerrainType.Ocean;
            var mesh = WaterMeshBuilder.Build(10, 10, terrain, WaterMeshBuilder.SubdivisionsForTier(tier), WaterFeatures.PaddingFor(tier), 40);
            Assert.AreEqual(count, mesh.positions.Length / 3);
        }
        [Test]
        public void FarSubmeshBeginsExactlyAtPaddingEdge()
        {
            var m = WaterMeshBuilder.Build(2, 2, new[] { TerrainType.Ocean, TerrainType.Lake, TerrainType.DeepLake, TerrainType.Land }, 3, 6, 40);
            Assert.AreEqual(m.indices.Length, m.nearIndices.Length + m.farIndices.Length);
            CollectionAssert.AreEqual(m.nearIndices, new ArraySegment<int>(m.indices, 0, m.nearIndices.Length));
            CollectionAssert.AreEqual(m.farIndices, new ArraySegment<int>(m.indices, m.nearIndices.Length, m.farIndices.Length));
            foreach (int id in m.farIndices)
            {
                float x = m.positions[id * 3], z = m.positions[id * 3 + 2];
                Assert.IsTrue(x <= -6.5f || x >= 7.5f || z <= -6.5f || z >= 7.5f, "Far vertices must be on or beyond the fade boundary.");
            }
            foreach (int id in m.nearIndices)
            {
                Assert.That(m.positions[id * 3], Is.InRange(-6.5f, 7.5f));
                Assert.That(m.positions[id * 3 + 2], Is.InRange(-6.5f, 7.5f));
            }
        }

        [TestCase(TerrainType.DeepLake)]
        public void BodyMixIsContainedMonotonicAndClamped(TerrainType lake)
        {
            var terrain = new TerrainType[60];
            for (int z = 0; z < 6; z++)
                for (int x = 0; x < 10; x++) terrain[z * 10 + x] = x < 3 ? TerrainType.Ocean : lake;
            var f = ShoreField.Bake(10, 6, terrain, 16, 1);
            int row = 64;
            int Mix(int x) => f.rgba[(row * f.width + x) * 4 + 3];
            // Boundary x=2.5 lies between texels 63 and 64.
            for (int z = 0; z < f.height; z++)
                for (int x = 0; x < f.width; x++)
                {
                    int at = (z * f.width + x) * 4;
                    if (f.rgba[at + 2] == 0) Assert.AreEqual(0, f.rgba[at + 3]);
                }
            Assert.AreEqual(0, Mix(63));
            Assert.That(Mix(64), Is.InRange(1, 254));
            for (int x = 64; x < 88; x++)
                Assert.That(Mix(x), Is.LessThanOrEqualTo(Mix(x + 1)));
            Assert.AreEqual(255, Mix(80));
        }
        [Test]
        public void SignedBorderIsMonotonicAndFilteredMidpointIsHalf()
        {
            var terrain = new TerrainType[60];
            for (int z = 0; z < 6; z++)
                for (int x = 0; x < 10; x++) terrain[z * 10 + x] = x < 3 ? TerrainType.Ocean : TerrainType.Lake;
            var field = ShoreField.Bake(10, 6, terrain, 16, 1);
            int A(int x) => field.rgba[(64 * field.width + x) * 4 + 3];
            for (int x = 40; x < 88; x++) Assert.LessOrEqual(A(x), A(x + 1));
            Assert.AreEqual(0, A(47));
            Assert.AreEqual(255, A(80));
            Assert.That(A(63), Is.InRange(1, 127));
            Assert.That(A(64), Is.InRange(128, 254));
            Assert.AreEqual(0.5, (A(63) + A(64)) / 510.0, 1.0 / 255);
        }

        [Test]
        public void LandAdjacencyDoesNotMoveSignedLakeDistance()
        {
            var terrain = new TerrainType[49];
            for (int i = 0; i < terrain.Length; i++) terrain[i] = i % 7 == 0 ? TerrainType.Ocean : TerrainType.Lake;
            var before = ShoreField.Bake(7, 7, terrain);
            terrain[24] = TerrainType.Land;
            var after = ShoreField.Bake(7, 7, terrain);
            for (int z = 0; z < before.height; z++)
                for (int x = 0; x < before.width; x++)
                {
                    int at = (z * before.width + x) * 4;
                    int cx = x / 16 - 4, cz = z / 16 - 4;
                    if (cx > 0 && cx < 7 && cz >= 0 && cz < 7 && !(cx == 3 && cz == 3))
                        Assert.AreEqual(before.rgba[at + 3], after.rgba[at + 3]);
                }
        }

        [TestCase(1)] [TestCase(3)] [TestCase(16)]
        public void RemovingLastLakeRefreshesOceanBorderIncrementally(int resolution)
        {
            var terrain = new TerrainType[25];
            for (int i = 0; i < terrain.Length; i++) terrain[i] = TerrainType.Ocean;
            terrain[12] = TerrainType.Lake;
            var workspace = new ShoreField.Workspace(5, 5, resolution);
            workspace.Bake(terrain);
            terrain[12] = TerrainType.Land;
            Assert.IsTrue(workspace.TryAddLand(terrain));
            CollectionAssert.AreEqual(ShoreField.Bake(5, 5, terrain, resolution).rgba, workspace.Field.rgba);
        }

        [Test]
        public void DeepLakeBlendsFromNearestShallowerBodyOnItsOwnSide()
        {
            var terrain = new TerrainType[60];
            for (int z = 0; z < 6; z++)
                for (int x = 0; x < 10; x++)
                    terrain[z * 10 + x] = x < 2 ? TerrainType.Ocean : x < 4 ? TerrainType.Lake : TerrainType.DeepLake;
            var f = ShoreField.Bake(10, 6, terrain, 16, 1);
            int Channel(int x, int channel) => f.rgba[(64 * f.width + x) * 4 + channel];
            Assert.AreEqual(255, Channel(79, 2), "Lake side keeps its palette.");
            Assert.AreEqual(255, Channel(79, 3), "Deep lake does not fade the lake side.");
            Assert.AreEqual(128, Channel(80, 2), "Deep lake transitions from lake here.");
            Assert.That(Channel(80, 3), Is.InRange(1, 254));
            for (int x = 80; x < 104; x++)
                Assert.That(Channel(x, 3), Is.LessThanOrEqualTo(Channel(x + 1, 3)));
            Assert.AreEqual(255, Channel(96, 3));
            Assert.AreEqual(64, Channel(175, 2), "Deep lake at board edge transitions from exterior ocean.");
            Assert.That(Channel(175, 3), Is.InRange(1, 254));
            for (int z = 0; z < f.height; z++)
                for (int x = 0; x < f.width; x++)
                {
                    int at = (z * f.width + x) * 4;
                    if (f.rgba[at + 2] == 0 && x >= 176) Assert.AreEqual(0, f.rgba[at + 3]);
                }
        }

        [Test]
        public void FieldEncodesLandDistanceDampingAndBody()
        {
            var f = ShoreField.Bake(4, 1, new[] { TerrainType.Land, TerrainType.Ocean, TerrainType.Lake, TerrainType.Ocean }, 1, 1);
            int at = (f.width + 1) * 4;
            Assert.AreEqual(0, f.rgba[at]); Assert.AreEqual(0, f.rgba[at + 3]);
            Assert.AreEqual(42, f.rgba[at + 4]); Assert.AreEqual(64, f.rgba[at + 7]);
            Assert.Less(f.rgba[at + 4], f.rgba[at + 8]); Assert.Less(f.rgba[at + 8], f.rgba[at + 12]);
            Assert.AreEqual(0, f.rgba[at + 6]); Assert.AreEqual(255, f.rgba[at + 10]);
        }

        [Test]
        public void PhaseIsStaticPeriodicAndVariesAlongCoast()
        {
            var a = ShoreField.Bake(2, 2, new[] { TerrainType.Land, TerrainType.Ocean, TerrainType.Lake, TerrainType.Land });
            var b = ShoreField.Bake(2, 2, new[] { TerrainType.Ocean, TerrainType.Land, TerrainType.Land, TerrainType.Lake });
            byte min = 255, max = 0;
            for (int z = 0; z < a.height; z++)
                for (int x = 0; x < a.width; x++)
                {
                    int at = (z * a.width + x) * 4 + 1;
                    Assert.AreEqual(a.rgba[at], b.rgba[at], "Terrain changes must preserve local phase.");
                    min = Math.Min(min, a.rgba[at]); max = Math.Max(max, a.rgba[at]);
                    if (x == 0) Assert.AreEqual(a.rgba[at], a.rgba[(z * a.width + a.width - 1) * 4 + 1]);
                    if (z == 0) Assert.AreEqual(a.rgba[at], a.rgba[((a.height - 1) * a.width + x) * 4 + 1]);
                }
            Assert.Greater(max - min, 100);
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
            Assert.AreEqual(255, a.rgba[0]);
            Assert.AreEqual(0, a.rgba[2]); Assert.AreEqual(0, a.rgba[3]);
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
                    Assert.AreEqual(0, f.rgba[at + 2], "B: ocean palette");
                    Assert.AreEqual(0, f.rgba[at + 3], "A: ocean mix");
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

        [TestCase(0, 1)] [TestCase(1, 3)] [TestCase(2, 6)] [TestCase(3, 8)]
        public void MeshCountsMatchTierAndIncludeCoveredLand(int tier, int n)
        {
            Assert.AreEqual(n, WaterMeshBuilder.SubdivisionsForTier(tier));
            var m = WaterMeshBuilder.Build(2, 1, new[] { TerrainType.Ocean, TerrainType.Land }, n, 0, 0);
            Assert.AreEqual((2 * n + 1) * (n + 1) * 3, m.positions.Length);
            Assert.AreEqual(2 * n * n * 6, m.indices.Length);
                        var open = WaterMeshBuilder.Build(2, 1, new[] { TerrainType.Ocean, TerrainType.Ocean }, n, 0, 0);
            CollectionAssert.AreEqual(open.positions, m.positions);
            CollectionAssert.AreEqual(open.indices, m.indices);
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


