using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class WaterFeaturesTests
    {
        // Exact masks guard every cell in the preset table, including disabled features.
        [TestCase(0, 2048, 0, 1)]
        [TestCase(1, 18763, 1, 3)]
        [TestCase(2, 18943, 4, 6)]
        [TestCase(3, 27135, 4, 8)]
        public void PresetsMatchFeatureTable(int preset, int features, int waves, int subdivisions)
        {
            Assert.That(WaterFeatures.FeaturesFor(preset), Is.EqualTo(features));
            Assert.That(WaterFeatures.WavesFor(preset), Is.EqualTo(waves));
            Assert.That(WaterMeshBuilder.SubdivisionsForTier(preset), Is.EqualTo(subdivisions));
        }

        // G2 runs waves A and B; B (6.876) is its shortest. At least 4 vertices per shortest wavelength.
        [TestCase(2, 6.876f)]
        [TestCase(4, 4.248f)]
        public void GerstnerSubdivisionResolvesShortestWave(int waves, float shortest)
        {
            foreach (float scale in new[] { 0.25f, 0.6f, 1f, 4f })
            {
                int subdivisions = WaterFeatures.Subdivisions(waves, scale, 6f);
                Assert.That(shortest * scale / (6f / subdivisions), Is.GreaterThanOrEqualTo(4f), $"scale {scale}");
            }
        }
    }
}
