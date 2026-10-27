using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class WaterFeaturesTests
    {
        // Exact masks guard every cell in the preset table, including disabled features.
        [TestCase(0, 0, 0, 1)]
        [TestCase(1, 17679, 2, 4)]
        [TestCase(2, 19967, 4, 8)]
        [TestCase(3, 28671, 4, 8)]
        public void PresetsMatchFeatureTable(int preset, int features, int waves, int subdivisions)
        {
            Assert.That(WaterFeatures.FeaturesFor(preset), Is.EqualTo(features));
            Assert.That(WaterFeatures.WavesFor(preset), Is.EqualTo(waves));
            Assert.That(WaterFeatures.Subdivisions(waves), Is.EqualTo(subdivisions));
        }
    }
}
