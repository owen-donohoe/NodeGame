using NodeWar.Core;
using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class BoardFramingTests
    {
        [TestCase(5, 5, 2f, 4f, 4f)]
        [TestCase(7, 5, 3f, 9f, 6f)]
        [TestCase(1, 1, 4f, 0f, 0f)]
        public void CentreIsTheMiddleOfTheNodeGrid(int cols, int rows, float scale, float expectedX, float expectedZ)
        {
            BoardFraming.Centre(cols, rows, scale, out float x, out float z);
            Assert.AreEqual(expectedX, x, 1e-5f);
            Assert.AreEqual(expectedZ, z, 1e-5f);
        }

        [Test]
        public void WidthFitDistance_PutsEveryColumnInFrame()
        {
            // 90-degree vertical FOV at aspect 1: half-width equals distance.
            Assert.AreEqual(21f, BoardFraming.WidthFitDistance(7, 6f, 90f, 1f, 1f), 1e-3f);
            // A narrow portrait screen needs to stand further back than a square one.
            float portrait = BoardFraming.WidthFitDistance(7, 6f, 60f, 0.45f, 1f);
            float square = BoardFraming.WidthFitDistance(7, 6f, 60f, 1f, 1f);
            Assert.Greater(portrait, square);
            // The width seen at that distance is exactly the board's seven cells.
            double seen = 2.0 * portrait * System.Math.Tan(30.0 * System.Math.PI / 180.0) * 0.45;
            Assert.AreEqual(42.0, seen, 1e-3);
            // Margin widens it proportionally.
            Assert.AreEqual(square * 1.1f, BoardFraming.WidthFitDistance(7, 6f, 60f, 1f, 1.1f), 1e-3f);
        }

        [Test]
        public void ConfiguredBoundsNeverClipTheNodeGrid()
        {
            // The asset's camera bounds were authored for the old 4x7 board. The shipped
            // hourglass is 7x7 at the same spacing: its far edge sits at 36, past the old 30.
            BoardFraming.ExpandBounds(-10f, 30f, 7, 6f, out float minX, out float maxX);
            Assert.GreaterOrEqual(maxX, 36f + 6f, "a cell of margin beyond the last column");
            Assert.LessOrEqual(minX, -6f, "and before the first");
            Assert.LessOrEqual(minX, -10f, "bounds the asset already allows are kept");

            BoardFraming.ExpandBounds(-15f, 45f, 7, 6f, out float minZ, out float maxZ);
            Assert.AreEqual(-15f, minZ, 1e-5f);
            Assert.AreEqual(45f, maxZ, 1e-5f, "already roomy enough: unchanged");

            BoardFraming.ExpandBounds(-12f, 12f, 3, 4f, out float a, out float b);
            Assert.AreEqual(-12f, a, 1e-5f);
            Assert.AreEqual(12f, b, 1e-5f);
        }

        [Test]
        public void FitIsReachedAtOrNearTheFarthestZoom()
        {
            Assert.IsTrue(BoardFraming.IsAtFit(30f, 30f));
            Assert.IsTrue(BoardFraming.IsAtFit(29.5f, 30f));
            Assert.IsFalse(BoardFraming.IsAtFit(20f, 30f));
        }

        [Test]
        public void ToggleGoesToFitThenBackToDefault()
        {
            float zoom = BoardFraming.ToggleZoom(18f, 18f, 30f);
            Assert.AreEqual(30f, zoom);
            zoom = BoardFraming.ToggleZoom(zoom, 18f, 30f);
            Assert.AreEqual(18f, zoom);
        }
    }
}
