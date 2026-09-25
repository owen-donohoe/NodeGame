using System;
using NUnit.Framework;

namespace NodeWar.Progression.Tests
{
    public class Glicko2Tests
    {
        private readonly Glicko2Config cfg = new Glicko2Config();

        [Test]
        public void DefaultValuesRepresentANewPlayer()
        {
            foreach (Rating rating in new[] { default(Rating), new Rating(), new Rating(1500) })
            {
                Assert.AreEqual(1500, rating.R);
                Assert.AreEqual(350, rating.RD);
                Assert.AreEqual(0.06, rating.Sigma);
            }
        }

        [Test]
        public void MatchesGlickmansWorkedExample()
        {
            var updated = Glicko2.Update(new Rating(1500, 200, 0.06), new[]
            {
                (new Rating(1400, 30), 1.0),
                (new Rating(1550, 100), 0.0),
                (new Rating(1700, 300), 0.0)
            }, cfg);

            Assert.AreEqual(1464.06, updated.R, 0.01);
            Assert.AreEqual(151.52, updated.RD, 0.01);
            // A hundredth would accept a broken volatility update at this scale.
            Assert.AreEqual(0.05999, updated.Sigma, 0.00001);
        }

        [Test]
        public void EqualPlayersHaveEqualAndOppositeRatingChanges()
        {
            var player = new Rating();
            var winner = Glicko2.UpdateSingleMatch(player, player, 1, cfg);
            var loser = Glicko2.UpdateSingleMatch(player, player, 0, cfg);
            Assert.Greater(winner.R, player.R);
            Assert.AreEqual(winner.R - player.R, player.R - loser.R, 1e-10);
            Assert.AreEqual(winner.RD, loser.RD, 1e-10);
            Assert.AreEqual(winner.Sigma, loser.Sigma, 1e-10);
        }

        [Test]
        public void UpsetMovesRatingFurtherThanExpectedWin()
        {
            var player = new Rating(1500, 100);
            var upset = Glicko2.UpdateSingleMatch(player, new Rating(1800, 100), 1, cfg);
            var expected = Glicko2.UpdateSingleMatch(player, new Rating(1200, 100), 1, cfg);
            Assert.Greater(upset.R, expected.R);
            Assert.Greater(upset.Sigma, player.Sigma);
        }

        [Test]
        public void UncertainPlayersMoveFurtherForTheSameResult()
        {
            var opponent = new Rating();
            var high = Glicko2.UpdateSingleMatch(new Rating(1500, 350), opponent, 1, cfg);
            var low = Glicko2.UpdateSingleMatch(new Rating(1500, 30), opponent, 1, cfg);
            Assert.Greater(high.R, low.R);
        }

        [Test]
        public void WinningStreakKeepsRaisingRating()
        {
            var player = new Rating();
            for (int i = 0; i < 100; i++)
            {
                var next = Glicko2.UpdateSingleMatch(player, new Rating(player.R, 100), 1, cfg);
                Assert.Greater(next.R, player.R);
                player = next;
            }
            Assert.Greater(player.R, 2000);
        }

        [Test]
        public void InactivityAddsVarianceWithoutChangingRatingOrVolatility()
        {
            var player = new Rating(1700, 100);
            var decayed = Glicko2.DecayForInactivity(player, 10, cfg);
            Assert.AreEqual(Math.Sqrt(100 * 100 + 10 * Math.Pow(173.7178 * 0.06, 2)), decayed.RD, 1e-10);
            Assert.Greater(decayed.RD, player.RD);
            Assert.AreEqual(player.R, decayed.R);
            Assert.AreEqual(player.Sigma, decayed.Sigma);
            Assert.AreEqual(cfg.MaxRD, Glicko2.DecayForInactivity(player, int.MaxValue, cfg).RD);
            Assert.AreEqual(player, Glicko2.DecayForInactivity(player, 0, cfg));
        }

        [Test]
        public void EmptyPeriodMatchesOneInactivityPeriod()
        {
            var player = new Rating(1700, 100);
            Assert.AreEqual(Glicko2.DecayForInactivity(player, 1, cfg),
                Glicko2.Update(player, Array.Empty<(Rating, double)>(), cfg));
        }

        [Test]
        public void MinRDFloorSurvivesManyMatches()
        {
            var player = new Rating(1500, 100, 0.001);
            for (int i = 0; i < 2000; i++)
            {
                player = Glicko2.UpdateSingleMatch(player, new Rating(1500, 30), 0.5, cfg);
                Assert.GreaterOrEqual(player.RD, cfg.MinRD);
            }
            Assert.AreEqual(cfg.MinRD, player.RD);
            Assert.AreEqual(1500, player.R);
        }

        [Test]
        public void ExpectedScoreUsesOpponentUncertainty()
        {
            Assert.AreEqual(0.5, Glicko2.ExpectedScore(new Rating(), new Rating()));
            var player = new Rating(1500, 200);
            Assert.AreEqual(0.639, Glicko2.ExpectedScore(player, new Rating(1400, 30)), 0.001);
            Assert.Greater(Glicko2.ExpectedScore(player, new Rating(1400, 30)),
                Glicko2.ExpectedScore(player, new Rating(1400, 300)));
        }

        [TestCase(-1)]
        [TestCase(0.25)]
        [TestCase(2)]
        [TestCase(double.NaN)]
        public void RejectsInvalidScores(double score)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Glicko2.UpdateSingleMatch(new Rating(), new Rating(), score, cfg));
        }

        [Test]
        public void RejectsInvalidInactivityAndConvergenceSettings()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Glicko2.DecayForInactivity(new Rating(), -1, cfg));
            Assert.Throws<ArgumentOutOfRangeException>(() => Glicko2.UpdateSingleMatch(new Rating(), new Rating(), 1,
                new Glicko2Config { Tau = 0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => Glicko2.UpdateSingleMatch(new Rating(), new Rating(), 1,
                new Glicko2Config { Epsilon = double.NaN }));
        }
    }
}
