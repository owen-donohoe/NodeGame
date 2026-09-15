using NodeWar.View.Outline;
using NUnit.Framework;

namespace NodeWar.Tests
{
    /// <summary>
    /// thicknessScale was added to StyleEntry after palettes had already been
    /// saved, and Unity deserialises a field that did not exist as zero. Zero
    /// is also the one value that would make a style stop drawing entirely --
    /// which, now that style priority decides who owns a contested pixel, would
    /// look exactly like a higher style eating the line rather than like a
    /// width of nothing. These cases pin the reinterpretation that prevents it.
    /// </summary>
    public class OutlineThicknessScaleTests
    {
        [Test]
        public void Zero_MeansFullWidth_NotNoLine()
        {
            Assert.AreEqual(1f, OutlineSettings.ResolveThicknessScale(0f), 0.0001f);
        }

        [Test]
        public void Negative_AlsoMeansFullWidth()
        {
            Assert.AreEqual(1f, OutlineSettings.ResolveThicknessScale(-3f), 0.0001f);
        }

        [Test]
        public void AnAuthoredFraction_IsKept()
        {
            Assert.AreEqual(0.5f, OutlineSettings.ResolveThicknessScale(0.5f), 0.0001f);
        }

        [Test]
        public void AboveFull_IsClampedToFull()
        {
            // The disc only reaches the tap radius, so a scale above 1 would be
            // a width the taps cannot measure -- it would read as 1 anyway.
            Assert.AreEqual(1f, OutlineSettings.ResolveThicknessScale(4f), 0.0001f);
        }

        [Test]
        public void AVanishinglySmallFraction_IsHeldAtTheFloor()
        {
            Assert.AreEqual(OutlineSettings.MinThicknessScale,
                            OutlineSettings.ResolveThicknessScale(0.0001f), 0.0001f);
        }
    }
}
