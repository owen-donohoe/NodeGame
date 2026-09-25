using System;
using NUnit.Framework;

namespace NodeWar.Progression.Tests
{
    public class RankPointsTests
    {
        private readonly RankConfig cfg = new RankConfig();

        [TestCase(1400, 16, -24)]
        [TestCase(1500, 20, -20)]
        [TestCase(1600, 24, -16)]
        public void DivergencePullsVisibleRankTowardHiddenRating(double hidden, int win, int loss)
        {
            Assert.AreEqual(win, RankPoints.RRDelta(1000, new Rating(hidden), true, cfg));
            Assert.AreEqual(loss, RankPoints.RRDelta(1000, new Rating(hidden), false, cfg));
        }

        [TestCase(0, true, 8)]
        [TestCase(0, false, -40)]
        [TestCase(3000, true, 40)]
        [TestCase(3000, false, -8)]
        public void AwardsClampWithoutChangingTheResultSign(double hidden, bool won, int expected)
        {
            Assert.AreEqual(expected, RankPoints.RRDelta(1000, new Rating(hidden), won, cfg));
        }

        [TestCase(true, 21)]
        [TestCase(false, -21)]
        public void HalfPointAwardsRoundAwayFromZero(bool won, int expected)
        {
            var hidden = new Rating(won ? 1512.5 : 1487.5);
            Assert.AreEqual(expected, RankPoints.RRDelta(1000, hidden, won, cfg));
        }

        [Test]
        public void UsesConfiguredImpliedRatingMapAndGains()
        {
            var custom = new RankConfig
            {
                ImpliedRatingAtZero = 1200, RatingPerRR = 1,
                BaseGain = 30, Divergence = 0.1, MinGain = 5, MaxGain = 60
            };
            Assert.AreEqual(40, RankPoints.RRDelta(200, new Rating(1500), true, custom));
            Assert.AreEqual(-20, RankPoints.RRDelta(200, new Rating(1500), false, custom));
            Assert.AreEqual(60, RankPoints.RRDelta(200, new Rating(2000), true, custom));
            Assert.AreEqual(-5, RankPoints.RRDelta(200, new Rating(2000), false, custom));
        }

        [Test]
        public void LossAtZeroStillReturnsASignedAward()
        {
            // The balance owner floors the applied total. Clipping this award
            // would contradict both the minimum loss and the signed-delta API.
            Assert.AreEqual(-20, RankPoints.RRDelta(0, new Rating(1000), false, cfg));
            Assert.AreEqual(-20, RankPoints.RRDelta(-100, new Rating(1000), false, cfg));
        }

        [TestCase(-1, 0)]
        [TestCase(0, 0)]
        [TestCase(299, 0)]
        [TestCase(300, 1)]
        [TestCase(699, 1)]
        [TestCase(700, 2)]
        [TestCase(1199, 2)]
        [TestCase(1200, 3)]
        [TestCase(1799, 3)]
        [TestCase(1800, 4)]
        [TestCase(2499, 4)]
        [TestCase(2500, 5)]
        [TestCase(int.MaxValue, 5)]
        public void ArenaBoundariesAreInclusive(int rr, int expected)
        {
            Assert.AreEqual(expected, RankPoints.ArenaFor(rr, cfg));
        }

        [Test]
        public void CustomArenasAllowImmediateDemotion()
        {
            var custom = new RankConfig { ArenaThresholds = new[] { 0, 10, 50 } };
            Assert.AreEqual(1, RankPoints.ArenaFor(10, custom));
            Assert.AreEqual(0, RankPoints.ArenaFor(9, custom));
            Assert.AreEqual(2, RankPoints.ArenaFor(10000, custom));
            Assert.AreEqual(0, RankPoints.ArenaFor(10000,
                new RankConfig { ArenaThresholds = new[] { 0 } }));
        }

        [Test]
        public void RejectsAmbiguousArenaThresholds()
        {
            foreach (var thresholds in new[] { Array.Empty<int>(), new[] { 10 }, new[] { 0, 10, 10 }, new[] { 0, 50, 10 } })
                Assert.Throws<ArgumentException>(() => RankPoints.ArenaFor(100,
                    new RankConfig { ArenaThresholds = thresholds }));
        }
    }
}
