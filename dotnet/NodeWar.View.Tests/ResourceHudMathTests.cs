using NodeWar.UI;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class ResourceHudMathTests
    {
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

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(int.MinValue)]
        public void NonpositiveCapsUseFiniteDisplayDefaults(int cap)
        {
            var balance = new GameBalanceData { foodCap = cap, materialsCap = cap, metalCap = cap };
            Assert.AreEqual(30, ResourceCaps.Food(balance));
            Assert.AreEqual(30, ResourceCaps.Materials(balance));
            Assert.AreEqual(10, ResourceCaps.Metal(balance));
        }

        [Test]
        public void PositiveCapsReadEachTypedFieldDirectlyWithoutCaching()
        {
            var balance = new GameBalanceData { foodCap = 40, materialsCap = 50, metalCap = 12 };
            Assert.AreEqual(40, ResourceCaps.Food(balance));
            Assert.AreEqual(50, ResourceCaps.Materials(balance));
            Assert.AreEqual(12, ResourceCaps.Metal(balance));
            balance.foodCap = 20;
            Assert.AreEqual(20, ResourceCaps.Food(balance));
            Assert.AreEqual(50, ResourceCaps.Materials(balance));
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

        [TestCase(0, false, false)]
        [TestCase(2, false, false)]
        [TestCase(3, false, true)]
        [TestCase(5, false, true)]
        [TestCase(0, true, true)]
        public void MagicVisibilityUsesArenaOrDebug(int arena, bool debug, bool visible)
        {
            Assert.AreEqual(3, ResourceHudMath.MagicArena);
            Assert.AreEqual(visible, ResourceHudMath.MagicVisible(arena, debug));
        }

        [Test]
        public void DisplayOnlyMagicHasOneZeroSourceAndTenCapacity()
        {
            Assert.AreEqual(0, ResourceHudMath.DisplayOnlyMagicAmount());
            Assert.AreEqual(10, ResourceHudMath.DefaultMagicCap);
            Assert.IsFalse(ResourceHudMath.IsFull(ResourceHudMath.DisplayOnlyMagicAmount(), 10));
            Assert.IsTrue(ResourceHudMath.MetalVisible(0, 0, true));
            Assert.IsTrue(ResourceHudMath.MetalVisible(0, 1, false));
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
            Assert.AreEqual(1f, ResourceHudMath.PulseAmount(0f), 1e-6f);
            Assert.AreEqual(0f, ResourceHudMath.PulseAmount(ResourceHudMath.PulsePeriodSeconds * 0.5f), 1e-5f);
            Assert.AreEqual(1f, ResourceHudMath.PulseAmount(ResourceHudMath.PulsePeriodSeconds), 1e-5f);

            float low = ResourceHudMath.FullWhiteMix(ResourceHudMath.PulsePeriodSeconds * 0.5f, false);
            float high = ResourceHudMath.FullWhiteMix(0f, false);
            Assert.Greater(high, low);
            Assert.Greater(low, 0f, "even at its dimmest a full resource is a little whiter");

            Assert.AreEqual(ResourceHudMath.StaticWhiteMix, ResourceHudMath.FullWhiteMix(0f, true));
            Assert.AreEqual(ResourceHudMath.StaticWhiteMix, ResourceHudMath.FullWhiteMix(0.37f, true), "constant");
            Assert.Greater(ResourceHudMath.StaticWhiteMix, 0f);
            Assert.AreEqual(-1f, ResourceHudMath.EffectiveWaveProgress(0.2f, true), "no wave");
            Assert.AreEqual(ResourceHudMath.WaveProgress(0.2f), ResourceHudMath.EffectiveWaveProgress(0.2f, false));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(10)]
        [TestCase(100000)]
        public void EveryCyclePeaksWhenWaveEnters(int cycle)
        {
            Assert.AreEqual(ResourceHudMath.WavePeriodSeconds, ResourceHudMath.PulsePeriodSeconds);
            double time = (double)ResourceHudMath.FullPeriodSeconds * cycle;
            ResourceHudMath.FullEffectAt(time, false, out float white, out float wave);
            Assert.AreEqual(0f, wave, 1e-5f);
            Assert.AreEqual(0.28f, white, 1e-5f);
            ResourceHudMath.FullEffectAt(time + ResourceHudMath.FullPeriodSeconds * 0.5, false, out white, out wave);
            Assert.AreEqual(0.08f, white, 1e-5f, "pulse trough halfway through the same cycle");
            Assert.AreEqual(ResourceHudMath.FullPeriodSeconds * 0.5f / ResourceHudMath.WaveTravelSeconds, wave, 1e-5f);
        }

        [TestCase(0f)]
        [TestCase(0.25f)]
        [TestCase(0.5f)]
        [TestCase(0.75f)]
        public void GlobalPhaseDrivesBothEffectsAndRepeatsAcrossFrames(float fraction)
        {
            double time = ResourceHudMath.FullPeriodSeconds * (1000.0 + fraction);
            Assert.AreEqual(fraction, ResourceHudMath.FullPhase(time), 1e-5f);
            ResourceHudMath.FullEffectAt(time, false, out float white, out float wave);
            Assert.AreEqual(0.18f + 0.1f * System.Math.Cos(fraction * 2 * System.Math.PI), white, 1e-5);
            float expectedWave = fraction * ResourceHudMath.FullPeriodSeconds < ResourceHudMath.WaveTravelSeconds
                ? fraction * ResourceHudMath.FullPeriodSeconds / ResourceHudMath.WaveTravelSeconds : -1f;
            Assert.AreEqual(expectedWave, wave, 1e-5f);
            // Full resources can join at any time; phase depends only on global time.
            ResourceHudMath.FullEffectAt(time + 19 * (double)ResourceHudMath.FullPeriodSeconds, false,
                out float repeatedWhite, out float repeatedWave);
            Assert.AreEqual(white, repeatedWhite, 1e-5f);
            Assert.AreEqual(wave, repeatedWave, 1e-5f);
            ResourceHudMath.FullEffectAt(time, true, out white, out wave);
            Assert.AreEqual(ResourceHudMath.StaticWhiteMix, white);
            Assert.AreEqual(-1f, wave);
        }
    }
}
