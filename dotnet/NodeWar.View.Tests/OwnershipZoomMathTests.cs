using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class OwnershipZoomMathTests
    {
        [TestCase(0f, 0.1f)]
        [TestCase(0.15f, 0.1f)]
        [TestCase(0.45f, 0.45f)]
        [TestCase(0.75f, 0.8f)]
        [TestCase(1f, 0.8f)]
        [TestCase(-1f, 0.1f)]
        [TestCase(2f, 0.8f)]
        public void AlphaUsesCloseFarEndpointsAndSmoothstep(float zoom, float expected)
            => Assert.AreEqual(expected, OwnershipZoomMath.Alpha(zoom, 0.15f, 0.75f, 0.1f, 0.8f), 1e-5f);

        [TestCase(0.25f, 0.15625f)]
        [TestCase(0.75f, 0.84375f)]
        public void AlphaUsesSmoothstepRatherThanLinearInterpolation(float zoom, float expected)
            => Assert.AreEqual(expected, OwnershipZoomMath.Alpha(zoom, 0f, 1f, 0f, 1f), 1e-5f);

        [Test]
        public void AlphaIsBoundedAndMonotonicAcrossSampledZooms()
        {
            float previous = 0.1f;
            for (int i = -100; i <= 200; i++)
            {
                float alpha = OwnershipZoomMath.Alpha(i / 100f, 0.15f, 0.75f, 0.1f, 0.8f);
                Assert.GreaterOrEqual(alpha, previous);
                Assert.GreaterOrEqual(alpha, 0.1f);
                Assert.LessOrEqual(alpha, 0.8f);
                previous = alpha;
            }
        }

        [TestCase(0f, -1f, 2f, -1f, 2f, 0f)]
        [TestCase(0.5f, -1f, 2f, -1f, 2f, 0.5f)]
        [TestCase(1f, -1f, 2f, -1f, 2f, 1f)]
        [TestCase(0.5f, 0f, 1f, 0.8f, 0.2f, 0.8f)]
        [TestCase(1f, 0f, 1f, 2f, -1f, 1f)]
        public void AlphaClampsEndpointsAndKeepsFarAtLeastNear(float zoom, float start, float end,
            float near, float far, float expected)
            => Assert.AreEqual(expected, OwnershipZoomMath.Alpha(zoom, start, end, near, far), 1e-5f);

        [TestCase(0.49f, 0.5f, 0.5f, 0.1f)]
        [TestCase(0.5f, 0.5f, 0.5f, 0.8f)]
        [TestCase(0.51f, 0.5f, 0.5f, 0.8f)]
        [TestCase(0.49f, 0.5f, 0.2f, 0.1f)]
        [TestCase(0.5f, 0.5f, 0.2f, 0.8f)]
        [TestCase(0f, -1f, -2f, 0.8f)]
        [TestCase(0.99f, 2f, 1f, 0.1f)]
        [TestCase(1f, 2f, 1f, 0.8f)]
        public void AlphaUsesStepAtClampedStartForZeroOrReversedFadeRange(float zoom, float start,
            float end, float expected)
            => Assert.AreEqual(expected, OwnershipZoomMath.Alpha(zoom, start, end, 0.1f, 0.8f), 1e-5f);

        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        public void EqualAlphaEndpointsStayConstant(float zoom)
            => Assert.AreEqual(0.3f, OwnershipZoomMath.Alpha(zoom, 0.15f, 0.75f, 0.3f, 0.3f));

        [TestCase(float.NaN, 0f)]
        [TestCase(float.NegativeInfinity, 0f)]
        [TestCase(float.PositiveInfinity, 1f)]
        public void AlphaNonfiniteInputsUseBoundedEndpointFallbacks(float invalid, float fallback)
        {
            float[] inputs = { 0.5f, 0.15f, 0.75f, 0.1f, 0.8f };
            for (int slot = 0; slot < inputs.Length; slot++)
            {
                float saved = inputs[slot];
                inputs[slot] = fallback;
                float expected = OwnershipZoomMath.Alpha(inputs[0], inputs[1], inputs[2], inputs[3], inputs[4]);
                inputs[slot] = invalid;
                float actual = OwnershipZoomMath.Alpha(inputs[0], inputs[1], inputs[2], inputs[3], inputs[4]);
                Assert.AreEqual(expected, actual);
                Assert.GreaterOrEqual(actual, 0f);
                Assert.LessOrEqual(actual, 1f);
                inputs[slot] = saved;
            }
        }

        [TestCase(5f, 0f)]
        [TestCase(30f, 1f)]
        [TestCase(17.5f, 0.5f)]
        [TestCase(-10f, 0f)]
        [TestCase(50f, 1f)] // draft distance beyond gameplay maximum
        public void NormalizeUsesGameplayRangeAndClamps(float distance, float expected)
            => Assert.AreEqual(expected, OwnershipZoomMath.Normalize(distance, 5f, 30f), 1e-5f);

        [TestCase(10f, 5f, 5f)]
        [TestCase(10f, 30f, 5f)]
        public void NormalizeInvalidDistanceRangeReturnsClose(float distance, float min, float max)
            => Assert.AreEqual(0f, OwnershipZoomMath.Normalize(distance, min, max));

        [TestCase(float.NaN)]
        [TestCase(float.NegativeInfinity)]
        [TestCase(float.PositiveInfinity)]
        public void NormalizeNonfiniteInputsReturnClose(float invalid)
        {
            Assert.AreEqual(0f, OwnershipZoomMath.Normalize(invalid, 5f, 30f));
            Assert.AreEqual(0f, OwnershipZoomMath.Normalize(10f, invalid, 30f));
            Assert.AreEqual(0f, OwnershipZoomMath.Normalize(10f, 5f, invalid));
        }

        [Test]
        public void NormalizeHandlesExtremeFiniteDistanceRange()
            => Assert.AreEqual(0.5f, OwnershipZoomMath.Normalize(0f, -float.MaxValue, float.MaxValue));
    }
}
