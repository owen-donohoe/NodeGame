using System;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class PaperRasterizerTests
    {
        [TestCase(PaperKind.House)]
        [TestCase(PaperKind.Tower)]
        [TestCase(PaperKind.Workshop)]
        [TestCase(PaperKind.Villager)]
        [TestCase(PaperKind.Marketman)]
        [TestCase(PaperKind.Stall)]
        [TestCase(PaperKind.Canopy)]
        public void Art_HasTransparentPaddingFillAndInk_AndIsRepeatable(PaperKind kind)
        {
            const int size = 128;
            byte[] pixels = PaperRasterizer.Rasterize(kind, size, size);
            CollectionAssert.AreEqual(pixels, PaperRasterizer.Rasterize(kind, size, size));
            int fill = 0, ink = 0;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    byte pixel = pixels[y * size + x];
                    if (x == 0 || y == 0 || x == size - 1 || y == size - 1) Assert.AreEqual(0, pixel);
                    if (pixel == 1) fill++;
                    if (pixel == 2) ink++;
                }
            Assert.Greater(fill, 100);
            Assert.Greater(ink, 100);
        }

        [Test]
        public void Stroke_IsInwardAndCircular_AndDoesNotWrapAcrossRows()
        {
            var mask = new byte[81];
            for (int y = 1; y < 8; y++)
                for (int x = 1; x < 8; x++) mask[y * 9 + x] = 1;
            var pixels = PaperRasterizer.Outline(mask, 9, 9, 2);
            Assert.AreEqual(0, pixels[0]);
            Assert.AreEqual(2, pixels[1 * 9 + 4]);
            Assert.AreEqual(2, pixels[2 * 9 + 4]);
            Assert.AreEqual(1, pixels[3 * 9 + 4]);
            Assert.AreEqual(1, pixels[4 * 9 + 4]);
            var edge = PaperRasterizer.Outline(new byte[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 }, 3, 3, 1);
            Assert.AreEqual(2, edge[0]);
            Assert.AreEqual(1, edge[4]);
        }

        [Test]
        public void EmptyMaskAndZeroStroke_AreWellDefined()
        {
            CollectionAssert.AreEqual(new byte[9], PaperRasterizer.Outline(new byte[9], 3, 3, 2));
            var mask = new byte[] { 1, 0, 1, 0 };
            CollectionAssert.AreEqual(mask, PaperRasterizer.Outline(mask, 2, 2, 0));
            Assert.Throws<ArgumentException>(() => PaperRasterizer.Outline(mask, 3, 3, 1));
            Assert.Throws<ArgumentException>(() => PaperRasterizer.Outline(mask, 2, 2, -1));
        }

        [Test]
        public void Grain_IsStableAndSubtle_WithoutTouchingRandomState()
        {
            float min = 1, max = 1;
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float value = PaperRasterizer.Grain(x, y);
                    Assert.AreEqual(value, PaperRasterizer.Grain(x, y));
                    min = Math.Min(min, value); max = Math.Max(max, value);
                }
            Assert.That(min, Is.InRange(0.984f, 0.99f));
            Assert.That(max, Is.InRange(1.01f, 1.016f));
        }

        [Test]
        public void Shapes_HaveReadableBasesAndDistinctSilhouettes()
        {
            Assert.IsTrue(PaperRasterizer.Contains(PaperKind.House, 50, 10));
            Assert.IsTrue(PaperRasterizer.Contains(PaperKind.Tower, 28, 90));
            Assert.IsFalse(PaperRasterizer.Contains(PaperKind.House, 28, 90));
            Assert.IsTrue(PaperRasterizer.Contains(PaperKind.Marketman, 22, 85));
            Assert.IsFalse(PaperRasterizer.Contains(PaperKind.Villager, 22, 85));
            Assert.IsFalse(PaperRasterizer.Contains(PaperKind.Villager, 50, 10));
        }
    }
}
