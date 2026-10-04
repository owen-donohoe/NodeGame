using System;
using NUnit.Framework;

namespace NodeWar.Progression.Tests
{
    public class MatchSettlementTests
    {
        private const long Week = 7 * 24 * 3600;
        private readonly SettlementConfig cfg = new SettlementConfig();

        [TestCase(1000)]
        [TestCase(1)]
        [TestCase(0)]
        public void WinnerGainsAndLoserLosesWithAZeroFloor(int loserRR)
        {
            var input = Input(Player(default, 1000), Player(default, loserRR));
            var result = MatchSettlement.Settle(input, cfg);

            Assert.AreEqual(2, result.Players.Length);
            Assert.Greater(result.Players[0].Rating.R, 1500);
            Assert.Greater(result.Players[0].RRDelta, 0);
            Assert.Greater(result.Players[0].Rank.RR, 1000);
            Assert.Less(result.Players[1].Rating.R, 1500);
            Assert.Less(result.Players[1].RRDelta, 0);
            if (loserRR > 1) Assert.Less(result.Players[1].Rank.RR, loserRR);
            else Assert.AreEqual(0, result.Players[1].Rank.RR);
        }

        [Test]
        public void SwappingPlayersAndWinnerExactlySwapsEveryOutcomeField()
        {
            var first = Player(new Rating(1700, 65, 0.04), 1195, 2, 2, 1);
            var second = Player(new Rating(1400, 180, 0.08), 710, 2, 4, Week + 1);
            var original = MatchSettlement.Settle(Input(first, second), cfg);
            var swappedInput = Input(second, first);
            swappedInput.Winner = 1;
            var swapped = MatchSettlement.Settle(swappedInput, cfg);

            AssertSettlement(original.Players[0], swapped.Players[1]);
            AssertSettlement(original.Players[1], swapped.Players[0]);
        }

        [Test]
        public void EachUpdateUsesBothDecayedPreMatchRatingsAndPostMatchRatingForRR()
        {
            var first = Player(new Rating(1600, 80, 0.05), 1000, last: 1);
            var second = Player(new Rating(1450, 140, 0.07), 1000, last: Week + 1);
            var input = Input(first, second);
            var beforeFirst = Glicko2.DecayForInactivity(first.Rating, 3, cfg.Glicko);
            var beforeSecond = Glicko2.DecayForInactivity(second.Rating, 2, cfg.Glicko);
            var expectedFirst = Glicko2.UpdateSingleMatch(beforeFirst, beforeSecond, 1, cfg.Glicko);
            var expectedSecond = Glicko2.UpdateSingleMatch(beforeSecond, beforeFirst, 0, cfg.Glicko);

            var result = MatchSettlement.Settle(input, cfg);

            AssertRating(expectedFirst, result.Players[0].Rating);
            AssertRating(expectedSecond, result.Players[1].Rating);
            Assert.AreEqual(RankPoints.RRDelta(1000, expectedFirst, true, cfg.Rank), result.Players[0].RRDelta);
            Assert.AreEqual(RankPoints.RRDelta(1000, expectedSecond, false, cfg.Rank), result.Players[1].RRDelta);
        }

        [TestCase(0, 0)]
        [TestCase(1, 3)]
        [TestCase(2, 2)]
        [TestCase(1814401, 0)]
        [TestCase(1814402, 0)]
        [TestCase(long.MaxValue, 0)]
        public void DecayHonorsWholePeriodsNewPlayersAndFutureTimestamps(long last, int periods)
        {
            var first = Player(new Rating(1530, 60), 1000, last: last);
            var second = Player(new Rating(1490, 90), 1000);
            var before = Glicko2.DecayForInactivity(first.Rating, periods, cfg.Glicko);
            var result = MatchSettlement.Settle(Input(first, second), cfg);

            AssertRating(Glicko2.UpdateSingleMatch(before, second.Rating, 1, cfg.Glicko), result.Players[0].Rating);
            AssertRating(Glicko2.UpdateSingleMatch(second.Rating, before, 0, cfg.Glicko), result.Players[1].Rating);
        }

        [Test]
        public void CustomPeriodControlsDecay()
        {
            var custom = new SettlementConfig { InactivityPeriodSeconds = 10 };
            var first = Player(new Rating(1500, 50), 1000, last: 5);
            var second = Player(new Rating(1500, 100), 1000);
            var input = Input(first, second);
            input.NowUnixSeconds = 39;
            var before = Glicko2.DecayForInactivity(first.Rating, 3, custom.Glicko);

            AssertRating(Glicko2.UpdateSingleMatch(before, second.Rating, 1, custom.Glicko),
                MatchSettlement.Settle(input, custom).Players[0].Rating);
        }

        [Test]
        public void ArenaChangesUseRankThresholdsAndPreserveHighestArenaOnDemotion()
        {
            var custom = new SettlementConfig
            {
                Rank = new RankConfig { ArenaThresholds = new[] { 0, 100, 200 }, Divergence = 0 }
            };
            var result = MatchSettlement.Settle(Input(
                Player(default, 90), Player(default, 110, 1, 2)), custom);

            Assert.AreEqual(110, result.Players[0].Rank.RR);
            Assert.AreEqual(1, result.Players[0].Rank.Arena);
            Assert.AreEqual(1, result.Players[0].Rank.HighestArena);
            Assert.IsTrue(result.Players[0].Promoted);
            Assert.IsFalse(result.Players[0].Demoted);
            Assert.AreEqual(90, result.Players[1].Rank.RR);
            Assert.AreEqual(0, result.Players[1].Rank.Arena);
            Assert.AreEqual(2, result.Players[1].Rank.HighestArena);
            Assert.IsFalse(result.Players[1].Promoted);
            Assert.IsTrue(result.Players[1].Demoted);
        }

        [Test]
        public void RecordsServerTimeWithoutMutatingInputsAndLeavesFlagsFalseWithinAnArena()
        {
            var first = Player(new Rating(1500, 80), 1000, 2, 3, 1);
            var second = Player(new Rating(1500, 80), 1000, 2, 3, 1);
            var input = Input(first, second);
            var result = MatchSettlement.Settle(input, cfg);

            foreach (var player in result.Players)
            {
                Assert.AreEqual(input.NowUnixSeconds, player.LastMatchUnixSeconds);
                Assert.IsFalse(player.Promoted);
                Assert.IsFalse(player.Demoted);
            }
            foreach (var player in input.Players)
            {
                AssertRating(new Rating(1500, 80), player.Rating);
                Assert.AreEqual(1, player.LastMatchUnixSeconds);
                Assert.AreEqual(1000, player.Rank.RR);
                Assert.AreEqual(2, player.Rank.Arena);
                Assert.AreEqual(3, player.Rank.HighestArena);
            }
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void RejectsInvalidWinner(int winner)
        {
            var input = Input(Player(default, 0), Player(default, 0));
            input.Winner = winner;
            Assert.Throws<ArgumentException>(() => MatchSettlement.Settle(input, cfg));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(3)]
        public void RequiresExactlyTwoPlayers(int count)
        {
            var input = new SettlementInput { Players = new SettlementPlayer[count] };
            Assert.Throws<ArgumentException>(() => MatchSettlement.Settle(input, cfg));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void RejectsNonPositivePeriod(long period)
        {
            var input = Input(Player(default, 0), Player(default, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => MatchSettlement.Settle(input,
                new SettlementConfig { InactivityPeriodSeconds = period }));
        }

        [Test]
        public void RejectsNullInputConfigPlayersAndNestedConfigs()
        {
            var input = Input(Player(default, 0), Player(default, 0));
            Assert.Throws<ArgumentNullException>(() => MatchSettlement.Settle(null, cfg));
            Assert.Throws<ArgumentNullException>(() => MatchSettlement.Settle(input, null));
            Assert.Throws<ArgumentNullException>(() => MatchSettlement.Settle(new SettlementInput(), cfg));
            Assert.Throws<ArgumentNullException>(() => MatchSettlement.Settle(input, new SettlementConfig { Glicko = null }));
            Assert.Throws<ArgumentNullException>(() => MatchSettlement.Settle(input, new SettlementConfig { Rank = null }));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void RejectsNullPlayer(int index)
        {
            var input = Input(Player(default, 0), Player(default, 0));
            input.Players[index] = null;
            Assert.Throws<ArgumentException>(() => MatchSettlement.Settle(input, cfg));
        }

        [Test]
        public void RejectsIdlePeriodCountsOutsideGlickoRangeWithoutWrapping()
        {
            var input = Input(Player(default, 0, last: 1), Player(default, 0));
            input.NowUnixSeconds = (long)int.MaxValue + 2;
            Assert.Throws<ArgumentOutOfRangeException>(() => MatchSettlement.Settle(input,
                new SettlementConfig { InactivityPeriodSeconds = 1 }));
        }

        private static SettlementPlayer Player(Rating rating, int rr, int arena = 0,
            int highestArena = 0, long last = 0)
        {
            return new SettlementPlayer
            {
                Rating = rating, Rank = new RankState(rr, arena, highestArena), LastMatchUnixSeconds = last
            };
        }

        private static SettlementInput Input(SettlementPlayer first, SettlementPlayer second)
        {
            return new SettlementInput { Players = new[] { first, second }, Winner = 0, NowUnixSeconds = 3 * Week + 1 };
        }

        private static void AssertRating(Rating expected, Rating actual)
        {
            Assert.AreEqual(expected.R, actual.R);
            Assert.AreEqual(expected.RD, actual.RD);
            Assert.AreEqual(expected.Sigma, actual.Sigma);
        }

        private static void AssertSettlement(PlayerSettlement expected, PlayerSettlement actual)
        {
            AssertRating(expected.Rating, actual.Rating);
            Assert.AreEqual(expected.LastMatchUnixSeconds, actual.LastMatchUnixSeconds);
            Assert.AreEqual(expected.RRDelta, actual.RRDelta);
            Assert.AreEqual(expected.Rank.RR, actual.Rank.RR);
            Assert.AreEqual(expected.Rank.Arena, actual.Rank.Arena);
            Assert.AreEqual(expected.Rank.HighestArena, actual.Rank.HighestArena);
            Assert.AreEqual(expected.Promoted, actual.Promoted);
            Assert.AreEqual(expected.Demoted, actual.Demoted);
        }
    }
}
