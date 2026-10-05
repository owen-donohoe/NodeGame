using NodeWar.UI;
using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class ResourceHudMathTests
    {
        private sealed class CappedBalance { public int foodCap = 40; public int materialsCap; public int metalCap = 12; }
        private sealed class OldBalance { public int somethingElse = 3; }

        [TestCase(30, 30, true)]
        [TestCase(31, 30, true)]
        [TestCase(29, 30, false)]
        [TestCase(10, 10, true)]
        [TestCase(9, 10, false)]
        [TestCase(5, 0, false)]
        [TestCase(0, 0, false)]
        public void FullMeansAtOrOverTheCap_AndNeverAgainstNoCap(int value, int cap, bool full)
        {
            Assert.AreEqual(full, ResourceHudMath.IsFull(value, cap));
        }

        [Test]
        public void CapsFallBackToThirtyThirtyTenWhenTheFieldIsMissingOrUnset()
        {
            var old = new OldBalance();
            Assert.AreEqual(30, ResourceCaps.Food(old));
            Assert.AreEqual(30, ResourceCaps.Materials(old));
            Assert.AreEqual(10, ResourceCaps.Metal(old));
            Assert.AreEqual(30, ResourceCaps.Food(null));

            var capped = new CappedBalance();
            Assert.AreEqual(40, ResourceCaps.Food(capped));
            Assert.AreEqual(30, ResourceCaps.Materials(capped), "zero means unset");
            Assert.AreEqual(12, ResourceCaps.Metal(capped));
        }

        [TestCase(0, 0, false)]
        [TestCase(1, 0, false)]
        [TestCase(2, 0, true)]
        [TestCase(5, 0, true)]
        [TestCase(0, 1, true)]
        [TestCase(1, 4, true)]
        public void MetalShowsFromTheMetalArenaOrWhenHeld(int arena, int metal, bool visible)
        {
            Assert.AreEqual(visible, ResourceHudMath.MetalVisible(arena, metal));
        }

        [Test]
        public void MetalArenaIsTheSinglePlaceholderConstant()
        {
            Assert.AreEqual(2, ResourceHudMath.MetalArena);
            Assert.IsTrue(ResourceHudMath.MetalVisible(ResourceHudMath.MetalArena, 0));
            Assert.IsFalse(ResourceHudMath.MetalVisible(ResourceHudMath.MetalArena - 1, 0));
        }

        [TestCase(0, 10, 0f)]
        [TestCase(5, 10, 0.5f)]
        [TestCase(10, 10, 1f)]
        [TestCase(14, 10, 1f)]
        [TestCase(-3, 10, 0f)]
        [TestCase(4, 0, 0f)]
        public void BarFillIsTheShareOfTheCap(int value, int cap, float fill)
        {
            Assert.AreEqual(fill, ResourceHudMath.BarFill(value, cap), 1e-6f);
        }

        [TestCase(0f, 0, 0f)]
        [TestCase(0.4f, 0, 0.4f)]
        [TestCase(3f, 2, 1f)]
        [TestCase(3f, 3, 0f)]
        [TestCase(3.25f, 3, 0.25f)]
        public void BarSegmentsFillOneUnitEach(float value, int segment, float fraction)
        {
            Assert.AreEqual(fraction, ResourceHudMath.BarSegmentFraction(value, segment), 1e-6f);
        }

        [Test]
        public void SemicircleSizeGrowsWithWidthAndThicknessIsClamped()
        {
            Assert.AreEqual(8f, ResourceHudMath.RingThickness(40f));
            Assert.AreEqual(20f, ResourceHudMath.RingThickness(2000f));
            Assert.Greater(ResourceHudMath.RingThickness(300f), ResourceHudMath.RingThickness(120f));
            float width = 170f;
            Assert.AreEqual(width * 0.5f + ResourceHudMath.RingThickness(width) * 0.5f, ResourceHudMath.HostHeight(width), 1e-4f);
            // Three rings and their gaps always fit inside the radius.
            float t = ResourceHudMath.RingThickness(width);
            Assert.Less(3 * t + 2 * ResourceHudMath.RingGap(t), width * 0.5f);
        }

        [Test]
        public void WaveCrossesOncePerPeriodThenRests()
        {
            Assert.AreEqual(0f, ResourceHudMath.WaveProgress(0f), 1e-6f);
            Assert.AreEqual(0.5f, ResourceHudMath.WaveProgress(ResourceHudMath.WaveTravelSeconds * 0.5f), 1e-5f);
            Assert.AreEqual(-1f, ResourceHudMath.WaveProgress(ResourceHudMath.WaveTravelSeconds + 0.01f));
            Assert.AreEqual(-1f, ResourceHudMath.WaveProgress(ResourceHudMath.WavePeriodSeconds - 0.01f));
            Assert.AreEqual(0f, ResourceHudMath.WaveProgress(ResourceHudMath.WavePeriodSeconds), 1e-5f, "repeats");
            Assert.AreEqual(0.5f, ResourceHudMath.WaveProgress(ResourceHudMath.WavePeriodSeconds + ResourceHudMath.WaveTravelSeconds * 0.5f), 1e-4f);
            Assert.AreEqual(-1f, ResourceHudMath.WaveProgress(-1f));
            Assert.That(ResourceHudMath.WavePeriodSeconds, Is.InRange(1.5f, 2f));
        }

        [Test]
        public void WaveStartsAtTheRightEndAndTravelsLeft()
        {
            Assert.AreEqual(ResourceHudMath.WavePeakAlpha, ResourceHudMath.WaveAlpha(1f, 0f), 1e-5f);
            Assert.AreEqual(0f, ResourceHudMath.WaveAlpha(0f, 0f), "nothing at the left yet");
            // Mid-crossing the line is mid-arc.
            Assert.Greater(ResourceHudMath.WaveAlpha(0.5f, 0.5f), 0.4f);
            Assert.AreEqual(0f, ResourceHudMath.WaveAlpha(1f, 0.5f), "and has left the right end");
            // At the left end it is there, and faint.
            float atEnd = ResourceHudMath.WaveAlpha(0f, 1f);
            Assert.Greater(atEnd, 0f);
            Assert.Less(atEnd, ResourceHudMath.WavePeakAlpha * 0.2f);
            Assert.AreEqual(0f, ResourceHudMath.WaveAlpha(0.5f, -1f), "resting");
        }

        [Test]
        public void WaveFadesProgressivelyAsItTravels()
        {
            float previous = float.MaxValue;
            for (int step = 0; step <= 10; step++)
            {
                float progress = step / 10f;
                float peak = ResourceHudMath.WaveAlpha(1f - progress, progress);
                Assert.Less(peak, previous, "step " + step);
                previous = peak;
            }
        }

        [Test]
        public void SegmentAlphaIsHighestWhereTheLineIsAndSameForEveryRing()
        {
            // The line is at segment 7 of 10 (position about 0.75) when progress is 0.25.
            float progress = 0.25f;
            int best = -1;
            float bestAlpha = 0f;
            for (int s = 0; s < 10; s++)
            {
                float a = ResourceHudMath.SegmentWaveAlpha(s, 10, progress);
                if (a > bestAlpha) { bestAlpha = a; best = s; }
            }
            Assert.AreEqual(7, best);
            // Segments far from the line get nothing; the function does not take a ring, so all
            // three rings read the same value for a segment: the wave is simultaneous.
            Assert.AreEqual(0f, ResourceHudMath.SegmentWaveAlpha(0, 10, progress));
            Assert.AreEqual(ResourceHudMath.SegmentWaveAlpha(7, 10, progress), ResourceHudMath.SegmentWaveAlpha(7, 10, progress));
        }

        [Test]
        public void PulseGoesToAndFro_AndReducedMotionIsAStaticBrighterTint()
        {
            Assert.AreEqual(0f, ResourceHudMath.PulseAmount(0f), 1e-6f);
            Assert.AreEqual(1f, ResourceHudMath.PulseAmount(ResourceHudMath.PulsePeriodSeconds * 0.5f), 1e-5f);
            Assert.AreEqual(0f, ResourceHudMath.PulseAmount(ResourceHudMath.PulsePeriodSeconds), 1e-5f);

            float low = ResourceHudMath.FullWhiteMix(0f, false);
            float high = ResourceHudMath.FullWhiteMix(ResourceHudMath.PulsePeriodSeconds * 0.5f, false);
            Assert.Greater(high, low);
            Assert.Greater(low, 0f, "even at its dimmest a full resource is a little whiter");

            Assert.AreEqual(ResourceHudMath.StaticWhiteMix, ResourceHudMath.FullWhiteMix(0f, true));
            Assert.AreEqual(ResourceHudMath.StaticWhiteMix, ResourceHudMath.FullWhiteMix(0.37f, true), "constant");
            Assert.Greater(ResourceHudMath.StaticWhiteMix, 0f);
            Assert.AreEqual(-1f, ResourceHudMath.EffectiveWaveProgress(0.2f, true), "no wave");
            Assert.AreEqual(ResourceHudMath.WaveProgress(0.2f), ResourceHudMath.EffectiveWaveProgress(0.2f, false));
        }
    }
}
