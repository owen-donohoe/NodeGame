using System;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class PaperMathTests
    {
        [Test]
        public void SeedsAndPathsAreStable()
        {
            Assert.AreEqual(2166136261u, PaperMath.Seed(null));
            Assert.AreEqual(PaperMath.Seed("card"), PaperMath.Seed("card"));
            Assert.AreNotEqual(PaperMath.Seed("card"), PaperMath.Seed("other"));
            var a = PaperMath.Outline(240, 100, 16, 0.8f, PaperMath.Seed("card"));
            var b = PaperMath.Outline(240, 100, 16, 0.8f, PaperMath.Seed("card"));
            CollectionAssert.AreEqual(a, b);
        }
        [TestCase(0u)] [TestCase(1u)] [TestCase(4294967295u)]
        public void NoiseAndRotationAreBounded(uint seed)
        {
            for (int i = -500; i <= 500; i++)
                Assert.LessOrEqual(Math.Abs(PaperMath.Noise(seed, i * 0.05f)), 1f);
            Assert.LessOrEqual(Math.Abs(PaperMath.Rotation(seed)), 1f);
        }
        [TestCase(20f, 16f)] [TestCase(400f, 200f)]
        public void OutlineIsClosedAndDisplacementIsBounded(float width, float height)
        {
            var clean = PaperMath.Outline(width, height, 12, 0, 19);
            var rough = PaperMath.Outline(width, height, 12, 0.8f, 19);
            Assert.AreEqual(rough[0], rough[rough.Length - 1]);
            for (int i = 0; i < rough.Length; i++)
            {
                double dx = rough[i].X - clean[i].X, dy = rough[i].Y - clean[i].Y;
                Assert.LessOrEqual(Math.Sqrt(dx * dx + dy * dy), 0.801);
            }
            double area = 0;
            for (int i = 1; i < rough.Length; i++) area += rough[i - 1].X * rough[i].Y - rough[i].X * rough[i - 1].Y;
            Assert.Greater(area, 0);
        }
        [Test]
        public void InkPointsHaveStableBoundedDrift()
        {
            var a = PaperMath.InkPoint(12, 5, 7);
            Assert.AreEqual(a, PaperMath.InkPoint(12, 5, 7));
            Assert.LessOrEqual(Math.Abs(a.X - 5), 0.221f);
            Assert.LessOrEqual(Math.Abs(a.Y - 7), 0.221f);
        }
    }
}
