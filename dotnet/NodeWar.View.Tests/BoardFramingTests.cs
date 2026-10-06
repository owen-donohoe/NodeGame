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
