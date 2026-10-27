using System.Linq;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class BoardVisualsBenchDraftTests
    {
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
