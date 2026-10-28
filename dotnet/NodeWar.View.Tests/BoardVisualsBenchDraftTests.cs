using System.Linq;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class BoardVisualsBenchDraftTests
    {
        [Test]
        public void RuntimeDeepLakePreservesPlacementsAndDraftMatchResolution()
        {
            var authored = PremadeMaps.Hourglass01();
            var before = (TerrainType[])authored.terrain.Clone();
            var board = BoardVisualsBenchDraft.IncludeDeepLake(authored);
            CollectionAssert.AreEqual(before, authored.terrain);
            CollectionAssert.AreEqual(board.terrain, BoardVisualsBenchDraft.IncludeDeepLake(authored).terrain);
            var sample = BoardVisualsBenchDraft.Create();
            var occupied = new bool[board.terrain.Length];
            foreach (var p in sample)
            {
                Assert.IsTrue(PlacementLegality.CanPlace(board, occupied, p.districtType, p.gridX, p.gridZ));
                occupied[p.gridZ * board.gridCols + p.gridX] = true;
            }
            int converted = 0;
            for (int i = 0; i < before.Length; i++)
            {
                if (before[i] == board.terrain[i]) continue;
                converted++;
                Assert.AreEqual(TerrainType.Lake, before[i]);
                Assert.AreEqual(TerrainType.DeepLake, board.terrain[i]);
                Assert.IsFalse(board.districtSlots[i]);
                Assert.IsFalse(occupied[i]);
            }
            Assert.That(converted, Is.InRange(2, 4));
            Assert.AreEqual(TerrainType.DeepLake, board.terrain[2 * board.gridCols + 2]);
            Assert.AreEqual(TerrainType.DeepLake, board.terrain[4 * board.gridCols + 2]);
            CollectionAssert.AreEqual(ShoalPath.Derive(authored), ShoalPath.Derive(board));
            foreach (bool complete in new[] { false, true })
            {
                var resolved = MatchFactory.ResolveTerrain(board, sample, complete);
                for (int i = 0; i < board.terrain.Length; i++)
                    if (board.terrain[i] == TerrainType.DeepLake) Assert.AreEqual(TerrainType.DeepLake, resolved[i]);
            }
        }

        [Test]
        public void EverySamplePlacementIsLegalOnShippedHourglassInOrder()
        {
            var board = PremadeMaps.Hourglass01();
            var occupied = new bool[board.gridCols * board.gridRows];
            var sample = BoardVisualsBenchDraft.Create();
            Assert.AreEqual(7, sample.Length);
            foreach (var p in sample)
            {
                Assert.IsTrue(PlacementLegality.CanPlace(board, occupied, p.districtType, p.gridX, p.gridZ),
                    $"{p.districtType} at ({p.gridX},{p.gridZ})");
                int cell = p.gridZ * board.gridCols + p.gridX;
                Assert.AreEqual(TerrainType.Lake, board.terrain[cell]);
                Assert.IsTrue(board.districtSlots[cell]);
                occupied[cell] = true;
            }
        }

        [Test]
        public void SampleMirrorsDistrictsAndOwnersWithOneSelfMirroringPier()
        {
            var board = PremadeMaps.Hourglass01();
            var sample = BoardVisualsBenchDraft.Create();
            Assert.AreEqual(1, sample.Count(p => p.districtType == DistrictType.Pier));
            foreach (var p in sample)
            {
                PremadeMaps.Mirror(board.symmetry, board.gridCols, board.gridRows,
                    p.gridX, p.gridZ, out int x, out int z);
                if (x == p.gridX && z == p.gridZ)
                {
                    Assert.AreEqual(DistrictType.Pier, p.districtType);
                    continue;
                }
                Assert.AreEqual(1, sample.Count(q => q.gridX == x && q.gridZ == z
                    && q.districtType == p.districtType && q.playerID == 1 - p.playerID));
            }
        }
    }
}



