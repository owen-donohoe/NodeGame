using NodeWar.View;
using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class PlinthTests
    {
        [TestCase(-1f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(0.5f, 0.875f)]
        [TestCase(1f, 1f)]
        [TestCase(2f, 1f)]
        public void CubicEaseClampsAndHasExpectedEndpoints(float time, float expected)
        {
            Assert.AreEqual(expected, PlinthMotion.EaseOutCubic(time), 0.00001f);
        }

        [TestCase(-1f, -0.97f)]
        [TestCase(0f, -0.97f)]
        [TestCase(1f, -0.12f)]
        [TestCase(2f, -0.12f)]
        public void RiseUsesTopHeightsAndClamps(float time, float expected)
        {
            Assert.AreEqual(expected, PlinthMotion.RiseY(time, -0.97f, -0.12f), 0.00001f);
        }

        [Test]
        public void RiseIsMonotonicAndSlowsTowardsTheTop()
        {
            float previous = -0.97f, previousStep = float.MaxValue;
            for (int i = 1; i <= 100; i++)
            {
                float y = PlinthMotion.RiseY(i / 100f, -0.97f, -0.12f);
                Assert.GreaterOrEqual(y, previous);
                Assert.LessOrEqual(y - previous, previousStep + 0.00001f);
                previousStep = y - previous;
                previous = y;
            }
        }

        [TestCase(0, 0, 6f, -0.12f, 0f, 0f)]
        [TestCase(3, 5, 6f, -0.12f, 18f, 30f)]
        [TestCase(2, 1, 2.5f, -0.4f, 5f, 2.5f)]
        public void LayoutKeepsTheOriginAtCellTop(int x, int z, float scale, float top, float worldX, float worldZ)
        {
            var centre = PlinthLayout.WorldCentre(x, z, scale, top);
            Assert.AreEqual(worldX, centre.x);
            Assert.AreEqual(worldZ, centre.z);
            Assert.AreEqual(top, centre.y, "Height is already in world units; do not scale it again.");
            Assert.AreEqual(0.6f, PlinthLayout.Height);
        }
    }
}
