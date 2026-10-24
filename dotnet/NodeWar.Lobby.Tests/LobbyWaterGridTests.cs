using System;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class LobbyWaterGridTests
    {
        [Test]
        public void GridUsesBoundsAndCellCentresInStageLocalSpace()
        {
            var grid = LobbyWaterGrid.Build(new[] { new LobbyLandBounds(-2, 3, 0, 4) }, 0.5f);
            Assert.AreEqual(4, grid.cols); Assert.AreEqual(2, grid.rows);
            Assert.AreEqual(-1.75f, grid.originX); Assert.AreEqual(3.25f, grid.originZ);
            Assert.AreEqual(0.5f, grid.cellSize);
            CollectionAssert.AreEqual(new[] { TerrainType.Land, TerrainType.Land, TerrainType.Land, TerrainType.Land,
                TerrainType.Land, TerrainType.Land, TerrainType.Land, TerrainType.Land }, grid.terrain);
        }

        [Test]
        public void SeparatedFootprintsLeaveOceanBetweenThem()
        {
            var grid = LobbyWaterGrid.Build(new[] { new LobbyLandBounds(0, 0, 1, 1), new LobbyLandBounds(2, 0, 3, 1) });
            Assert.AreEqual(3, grid.cols); Assert.AreEqual(1, grid.rows);
            CollectionAssert.AreEqual(new[] { TerrainType.Land, TerrainType.Ocean, TerrainType.Land }, grid.terrain);
        }

        [Test]
        public void BoundaryCellsNeverOmitWaterBeyondActualLand()
        {
            var land = new[] { new LobbyLandBounds(-1.2f, -0.2f, 2.2f, 1.2f) };
            var grid = LobbyWaterGrid.Build(land);
            Assert.AreEqual(5, grid.cols); Assert.AreEqual(3, grid.rows);
            for (int z = 0; z < grid.rows; z++)
                for (int x = 0; x < grid.cols; x++)
                {
                    TerrainType expected = z == 1 && x >= 1 && x <= 3 ? TerrainType.Land : TerrainType.Ocean;
                    Assert.AreEqual(expected, grid.terrain[z * grid.cols + x]);
                }
            var repeat = LobbyWaterGrid.Build(land);
            CollectionAssert.AreEqual(grid.terrain, repeat.terrain);
        }

        [Test]
        public void LazyMarketExpandsTheSameGridWithoutFillingTheGap()
        {
            var home = new LobbyLandBounds(-4, -3, 4, 3);
            var grid = LobbyWaterGrid.Build(new[] { home, new LobbyLandBounds(-15, -18, -11, 12) });
            Assert.AreEqual(19, grid.cols); Assert.AreEqual(30, grid.rows);
            Assert.AreEqual(TerrainType.Land, grid.terrain[18 * grid.cols]);
            Assert.AreEqual(TerrainType.Ocean, grid.terrain[18 * grid.cols + 5]);
            Assert.AreEqual(TerrainType.Land, grid.terrain[18 * grid.cols + 12]);
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidCellSizeIsRejected(float cellSize)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => LobbyWaterGrid.Build(new[] { new LobbyLandBounds(0, 0, 1, 1) }, cellSize));
        }
    }
}
