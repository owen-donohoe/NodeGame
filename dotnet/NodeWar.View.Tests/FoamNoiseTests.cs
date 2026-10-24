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
            const int size = 256;
            byte[] Bake(int seed) => normals ? FoamNoise.BakeNormals(size, seed) : FoamNoise.Bake(size, seed);
            var a = Bake(71);
            CollectionAssert.AreEqual(a, Bake(71));
            CollectionAssert.AreNotEqual(a, Bake(72));
            for (int i = 0; i < size; i++)
                for (int c = 0; c < 4; c++)
                {
                    Assert.That(Math.Abs(a[i * 4 + c] - a[((size - 1) * size + i) * 4 + c]), Is.LessThanOrEqualTo(20));
                    Assert.That(Math.Abs(a[i * size * 4 + c] - a[(i * size + size - 1) * 4 + c]), Is.LessThanOrEqualTo(20));
                }
            if (!normals) return;
            for (int i = 0; i < a.Length; i += 4)
            {
                double x = a[i] / 127.5 - 1, y = a[i + 1] / 127.5 - 1, z = a[i + 2] / 127.5 - 1;
                Assert.That(Math.Sqrt(x*x + y*y + z*z), Is.EqualTo(1).Within(0.007));
                Assert.That(a[i + 3], Is.EqualTo(255));
            }
        }

        [Test]
        public void InvalidSizeIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FoamNoise.Bake(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => FoamNoise.BakeNormals(0, 1));
        }
    }
}
