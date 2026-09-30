using System.Collections.Generic;
using NUnit.Framework;

namespace NodeWar.Progression.Tests
{
    public class DisconnectPenaltyTests
    {
        [TestCase(1, 0)]
        [TestCase(2, 0)]
        [TestCase(3, 120)]
        [TestCase(4, 120)]
        [TestCase(5, 3600)]
        [TestCase(6, 86400)]
        [TestCase(7, 172800)]
        [TestCase(8, 172800)]
        public void StrikeUsesNewLevel(int level, long duration)
        {
            var state = DisconnectPenalty.Strike(new DisconnectPenaltyState { Level = level - 1 }, 100);
            Assert.That(state.Level, Is.EqualTo(level));
            Assert.That(state.LastStrikeUnixSeconds, Is.EqualTo(100));
            Assert.That(state.BlockedUntilUnixSeconds, Is.EqualTo(100 + duration));
        }

        [TestCase(0, 0, 5)]
        [TestCase(57599, 0, 5)]
        [TestCase(57600, 1, 4)]
        [TestCase(115217, 2, 3)]
        [TestCase(576000, 10, 0)]
        public void DecayConsumesOnlyWholePeriodsAndFloorsAtZero(long elapsed, long periods, int level)
        {
            var state = DisconnectPenalty.Decay(new DisconnectPenaltyState
                { Level = 5, LastStrikeUnixSeconds = 100, BlockedUntilUnixSeconds = 999 }, 100 + elapsed);
            Assert.That(state.Level, Is.EqualTo(level));
            Assert.That(state.LastDecayUnixSeconds, Is.EqualTo(periods == 0 ? 0 : 100 + periods * DisconnectPenalty.DecayPeriodSeconds));
            Assert.That(state.BlockedUntilUnixSeconds, Is.EqualTo(999));
        }

        [Test]
        public void RepeatedDecayPreservesRemainderAndDoesNotConsumePeriodsTwice()
        {
            long period = DisconnectPenalty.DecayPeriodSeconds;
            var state = new DisconnectPenaltyState { Level = 5, LastStrikeUnixSeconds = 100 };
            state = DisconnectPenalty.Decay(state, 100 + period + 10);
            state = DisconnectPenalty.Decay(state, 100 + period + 20);
            Assert.That(state.Level, Is.EqualTo(4));
            state = DisconnectPenalty.Decay(state, 100 + 2 * period);
            Assert.That(state.Level, Is.EqualTo(3));
            Assert.That(state.LastDecayUnixSeconds, Is.EqualTo(100 + 2 * period));
        }

        [Test]
        public void StrikeDecaysFirstAndResetsAnchorToLaterStrike()
        {
            long period = DisconnectPenalty.DecayPeriodSeconds;
            var state = DisconnectPenalty.Strike(new DisconnectPenaltyState
                { Level = 5, LastStrikeUnixSeconds = 100, LastDecayUnixSeconds = 50 }, 100 + 2 * period + 10);
            Assert.That(state.Level, Is.EqualTo(4));
            Assert.That(state.LastDecayUnixSeconds, Is.EqualTo(100 + 2 * period));
            Assert.That(state.BlockedUntilUnixSeconds, Is.EqualTo(100 + 2 * period + 130));
            state = DisconnectPenalty.Decay(state, 100 + 3 * period);
            Assert.That(state.Level, Is.EqualTo(4));
        }

        [Test]
        public void EarlierTimeDoesNotDecay()
        {
            var state = DisconnectPenalty.Decay(new DisconnectPenaltyState
                { Level = 2, LastStrikeUnixSeconds = 100, LastDecayUnixSeconds = 200 }, 50);
            Assert.That(state.Level, Is.EqualTo(2));
            Assert.That(state.LastDecayUnixSeconds, Is.EqualTo(200));
        }

        [Test]
        public void SecondAndLaterNonReportsStrikeWithoutMutatingInputList()
        {
            var original = new List<long>();
            var state = DisconnectPenalty.NonReport(new DisconnectPenaltyState { NonReports = original }, 100);
            Assert.That(state.Level, Is.Zero);
            Assert.That(original, Is.Empty);
            state = DisconnectPenalty.NonReport(state, 101);
            Assert.That(state.Level, Is.EqualTo(1));
            state = DisconnectPenalty.NonReport(state, 102);
            Assert.That(state.Level, Is.EqualTo(2));
            Assert.That(state.NonReports, Is.EqualTo(new long[] { 100, 101, 102 }));
        }

        [TestCase(0, 1)]
        [TestCase(1, 0)]
        public void PrunesOnlyNonReportsOlderThanSevenDays(long extraAge, int expectedLevel)
        {
            long now = 100 + DisconnectPenalty.NonReportWindowSeconds + extraAge;
            var state = DisconnectPenalty.NonReport(new DisconnectPenaltyState { NonReports = new List<long> { 100 } }, now);
            Assert.That(state.Level, Is.EqualTo(expectedLevel));
            Assert.That(state.NonReports.Count, Is.EqualTo(expectedLevel == 0 ? 1 : 2));
        }
    }
}
