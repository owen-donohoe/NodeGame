using System;
using NUnit.Framework;

namespace NodeWar.Progression.Tests
{
    public class ArenasTests
    {
        private readonly ArenaConfig cfg = new ArenaConfig();

        [TestCase(int.MinValue, 0)]
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
        public void ThresholdsAreInclusive(int rr, int arena)
        {
            Assert.AreEqual(arena, Arenas.ArenaForRR(rr, cfg));
        }

        [TestCase(300, -1, 299, 0, 1)]
        [TestCase(299, 1, 300, 1, 1)]
        [TestCase(300, -1000, 0, 0, 1)]
        [TestCase(300, int.MinValue, 0, 0, 1)]
        [TestCase(300, 2200, 2500, 5, 5)]
        public void AppliesDeltaWithImmediateDemotionAndPersistentHighestArena(
            int rr, int delta, int expectedRR, int arena, int highest)
        {
            var old = new RankState(rr, 1, 1);
            var next = Arenas.ApplyRRDelta(old, delta, cfg);
            Assert.AreEqual(expectedRR, next.RR);
            Assert.AreEqual(arena, next.Arena);
            Assert.AreEqual(highest, next.HighestArena);
            Assert.AreEqual(rr, old.RR);
        }

        [Test]
        public void RecomputesArenaAndPreservesHistoricalPeak()
        {
            var next = Arenas.ApplyRRDelta(new RankState(300, 0, 5), 0, cfg);
            Assert.AreEqual(1, next.Arena);
            Assert.AreEqual(5, next.HighestArena);
            Assert.AreEqual(0, Arenas.ApplyRRDelta(default, -20, cfg).RR);
        }

        [Test]
        public void OverflowIsRejectedInsteadOfWrappingOrResettingRank()
        {
            Assert.Throws<OverflowException>(() =>
                Arenas.ApplyRRDelta(new RankState(int.MaxValue, 5, 5), 1, cfg));
            Assert.AreEqual(0, Arenas.ApplyRRDelta(new RankState(int.MinValue, 0, 0), -1, cfg).RR);
        }

        [Test]
        public void CustomThresholdsWorkThroughBothArenaApis()
        {
            var thresholds = new[] { 0, 10, 50 };
            var custom = new ArenaConfig { Thresholds = thresholds };
            Assert.AreEqual(2, Arenas.ArenaForRR(50, custom));
            Assert.AreEqual(2, RankPoints.ArenaFor(50, new RankConfig { ArenaThresholds = thresholds }));
            Assert.AreEqual(0, Arenas.ArenaForRR(int.MaxValue, new ArenaConfig { Thresholds = new[] { 0 } }));
        }

        [Test]
        public void InvalidThresholdsAreRejectedByValidationAndRules()
        {
            foreach (var thresholds in new[] { null, Array.Empty<int>(), new[] { 1 },
                new[] { 0, 1, 1 }, new[] { 0, 2, 1 }, new[] { 0, -1 } })
            {
                var invalid = new ArenaConfig { Thresholds = thresholds };
                Assert.Throws<ArgumentException>(() => invalid.Validate());
                Assert.Throws<ArgumentException>(() => Arenas.ArenaForRR(100, invalid));
                Assert.Throws<ArgumentException>(() => Arenas.ApplyRRDelta(default, 0, invalid));
            }
        }
    }
}
