using System;
using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class FoamNoiseTests
    {
        [TestCase(7)] [TestCase(29099)]
        public void NoiseIsPeriodicAndInRange(int seed)
        {
            for (int i = 0; i < 129; i++)
            {
                double t = i / 128.0;
                foreach (int period in new[] { 4, 6, 16 })
                {
                    double a = FoamNoise.Warped(0, t, seed, period);
                    Assert.That(a, Is.InRange(0.0, 1.0));
                    Assert.That(FoamNoise.Warped(1, t, seed, period), Is.EqualTo(a).Within(1e-12));
                    Assert.That(FoamNoise.Warped(t, 1, seed, period),
                        Is.EqualTo(FoamNoise.Warped(t, 0, seed, period)).Within(1e-12));
                }
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void BakesAreDeterministicAndEdgesContinuous(bool normals)
        {
            int size = 256;
            byte[] Bake(int seed) => normals ? FoamNoise.BakeSwell(size, seed) : FoamNoise.BakeVoronoi(size, seed, 6, 0.65);
            var a = Bake(71);
            CollectionAssert.AreEqual(a, Bake(71));
            CollectionAssert.AreNotEqual(a, Bake(72));
            for (int i = 0; i < size; i++)
                for (int c = 0; c < 4; c++)
                {
                    Assert.That(Math.Abs(a[i * 4 + c] - a[((size - 1) * size + i) * 4 + c]), Is.LessThanOrEqualTo(20));
                    Assert.That(Math.Abs(a[i * size * 4 + c] - a[(i * size + size - 1) * 4 + c]), Is.LessThanOrEqualTo(20));
                }
            for (int i = 0; i < a.Length; i += 4) Assert.AreEqual(255, a[i + 3]);
        }

        [TestCase(7)] [TestCase(0x71ab)] [TestCase(0x36c1)]
        public void VoronoiIsExactlyPeriodicAndInRange(int seed)
        {
            for (int i = 0; i < 129; i++)
            {
                double t = i / 128.0;
                foreach (int cells in new[] { 6, 10 })
                {
                    double a = FoamNoise.Voronoi(0, t, seed, cells, 0.65);
                    Assert.That(a, Is.InRange(0.0, 1.0));
                    Assert.That(FoamNoise.Voronoi(1, t, seed, cells, 0.65), Is.EqualTo(a).Within(1e-12));
                    Assert.That(FoamNoise.Voronoi(t, 1, seed, cells, 0.65), Is.EqualTo(FoamNoise.Voronoi(t, 0, seed, cells, 0.65)).Within(1e-12));
                }
            }
        }

        [Test]
        public void SwellDerivativesMatchWrappedHeightDifferences()
        {
            const int size = 256;
            var a = FoamNoise.BakeSwell(size, 71);
            double H(int x, int y) => a[(((y + size) % size) * size + (x + size) % size) * 4] / 255.0;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int at = (y * size + x) * 4;
                    double dx = (H(x + 1, y) - H(x - 1, y)) * size / 2;
                    double dy = (H(x, y + 1) - H(x, y - 1)) * size / 2;
                    Assert.That((a[at + 1] / 255.0 - 0.5) * 32, Is.EqualTo(dx).Within(16.0 / 255 + 1e-9));
                    Assert.That((a[at + 2] / 255.0 - 0.5) * 32, Is.EqualTo(dy).Within(16.0 / 255 + 1e-9));
                }
        }

        [Test]
        public void InvalidSizeIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FoamNoise.BakeVoronoi(0, 1, 6, 0.65));
            Assert.Throws<ArgumentOutOfRangeException>(() => FoamNoise.BakeSwell(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => FoamNoise.BakeVoronoi(16, 1, 0, 0.65));
            Assert.Throws<ArgumentOutOfRangeException>(() => FoamNoise.BakeVoronoi(16, 1, 6, double.NaN));
        }
    }
}
