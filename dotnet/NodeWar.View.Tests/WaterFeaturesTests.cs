using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class WaterFeaturesTests
    {
        // Exact masks guard every cell in the preset table, including disabled features.
        [TestCase(0, 0, 0, 1)]
        [TestCase(1, 16655, 1, 3)]
        [TestCase(2, 18943, 4, 6)]
        [TestCase(3, 27135, 4, 8)]
        public void PresetsMatchFeatureTable(int preset, int features, int waves, int subdivisions)
        {
            Assert.That(WaterFeatures.FeaturesFor(preset), Is.EqualTo(features));
            Assert.That(WaterFeatures.WavesFor(preset), Is.EqualTo(waves));
            Assert.That(WaterMeshBuilder.SubdivisionsForTier(preset), Is.EqualTo(subdivisions));
        }
    }
}
