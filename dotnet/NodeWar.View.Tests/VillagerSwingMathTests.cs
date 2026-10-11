using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class VillagerSwingMathTests
    {
        [TestCase(5, 4, 10, true, false)]   // ordinary decrement
        [TestCase(1, 10, 10, true, true)]   // reset after hit
        [TestCase(10, 10, 10, true, false)] // equal
        [TestCase(1, 10, 10, false, false)] // not in combat
        [TestCase(1, 10, 0, true, false)]   // invalid max
        [TestCase(1, 10, -3, true, false)]
        [TestCase(10, 10, 10, true, false)]
        [TestCase(3, 7, 10, true, false)]   // increase that is not a reset to max
        public void SwingStartedDetectsReset(int prev, int curr, int max, bool combat, bool expected)
            => Assert.AreEqual(expected, VillagerSwingMath.SwingStarted(prev, curr, max, combat));

        [TestCase(0f, 0.25f, 0f)]
        [TestCase(-1f, 0.25f, 0f)]
        [TestCase(0.25f, 0.25f, 0f)]
        [TestCase(9f, 0.25f, 0f)]
        [TestCase(0.0625f, 0.25f, 1f)]
        [TestCase(0.03125f, 0.25f, 0.5f)]
        [TestCase(0.1f, 0f, 0f)]
        [TestCase(0.1f, -1f, 0f)]
        [TestCase(float.NaN, 0.25f, 0f)]
        public void PulseEnvelope(float s, float d, float expected)
            => Assert.AreEqual(expected, VillagerSwingMath.Pulse(s, d), 1e-5f);

        [Test]
        public void PulseStaysWithinBounds()
        {
            for (float s = -0.1f; s < 0.4f; s += 0.001f)
            {
                float p = VillagerSwingMath.Pulse(s, 0.25f);
                Assert.GreaterOrEqual(p, 0f);
                Assert.LessOrEqual(p, 1f);
            }
        }

        [Test]
        public void FirstObservationNeverSwings()
            => Assert.IsFalse(VillagerSwingMath.ShouldSwing(false, true, true, 1, true, true, 10, 10));

        [Test]
        public void RespawnNeverSwings()
            => Assert.IsFalse(VillagerSwingMath.ShouldSwing(true, false, false, 0, true, true, 10, 10));

        [Test]
        public void EnteringCombatNeverSwings()
            => Assert.IsFalse(VillagerSwingMath.ShouldSwing(true, true, false, 3, true, true, 10, 10));

        [Test]
        public void LeavingCombatNeverSwings()
            => Assert.IsFalse(VillagerSwingMath.ShouldSwing(true, true, true, 1, true, false, 10, 10));

        [Test]
        public void DyingNeverSwings()
            => Assert.IsFalse(VillagerSwingMath.ShouldSwing(true, true, true, 1, false, false, 10, 10));

        [Test]
        public void ResetInsideSustainedCombatSwings()
            => Assert.IsTrue(VillagerSwingMath.ShouldSwing(true, true, true, 1, true, true, 10, 10));
    }
}
