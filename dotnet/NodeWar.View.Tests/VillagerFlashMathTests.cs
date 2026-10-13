using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class VillagerFlashMathTests
    {
        [TestCase(0f, 0.12f, 1f)]
        [TestCase(-1f, 0.12f, 1f)]
        [TestCase(0.06f, 0.12f, 0.5f)]
        [TestCase(0.12f, 0.12f, 0f)]
        [TestCase(5f, 0.12f, 0f)]
        [TestCase(0f, 0f, 0f)]
        [TestCase(0.1f, -1f, 0f)]
        [TestCase(float.NaN, 0.12f, 0f)]
        public void AmountIsClampedLinearDecay(float elapsed, float duration, float expected)
            => Assert.AreEqual(expected, VillagerFlashMath.Amount(elapsed, duration), 1e-5f);

        [Test]
        public void RepeatedHitRestartsEnvelope()
        {
            // A second hit restarts elapsed at zero: full white again, whichever source fired.
            float before = VillagerFlashMath.Amount(0.09f, 0.12f);
            float after = VillagerFlashMath.Amount(0f, 0.12f);
            Assert.Less(before, after);
            Assert.AreEqual(1f, after);
        }

        [TestCase(10, 8, true, true)]
        [TestCase(10, 10, true, false)]
        [TestCase(8, 10, true, false)]
        [TestCase(10, 8, false, false)]
        public void TookDamageOnlyForDecreaseInSameLife(int prev, int curr, bool same, bool expected)
            => Assert.AreEqual(expected, VillagerFlashMath.TookDamage(prev, curr, same));

        [TestCase(false, true, true, false)]  // first observation
        [TestCase(true, true, true, true)]
        [TestCase(true, true, false, false)]  // died or consumed
        [TestCase(true, false, true, false)]  // respawned
        [TestCase(true, false, false, false)]
        public void SameLifeRequiresBothObservationsAlive(bool had, bool prevAlive, bool currAlive, bool expected)
            => Assert.AreEqual(expected, VillagerFlashMath.SameLife(had, prevAlive, currAlive));

        [Test]
        public void HealThenHitStillFlashesOnlyTheHit()
        {
            Assert.IsFalse(VillagerFlashMath.TookDamage(5, 9, true));
            Assert.IsTrue(VillagerFlashMath.TookDamage(9, 7, true));
        }
    }
}
