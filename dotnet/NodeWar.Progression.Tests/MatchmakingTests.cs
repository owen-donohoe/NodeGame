using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace NodeWar.Progression.Tests
{
    public class MatchmakingTests
    {
        private readonly MatchmakingConfig cfg = new MatchmakingConfig();

        [TestCase(0, 100)]
        [TestCase(19.99, 100)]
        [TestCase(20, 250)]
        [TestCase(59.99, 250)]
        [TestCase(60, 500)]
        [TestCase(119.99, 500)]
        [TestCase(120, double.PositiveInfinity)]
        [TestCase(1000, double.PositiveInfinity)]
        public void WindowsAreStepsWithInclusiveKnotTimes(double wait, double expected)
        {
            Assert.AreEqual(expected, Matchmaking.Window(wait, cfg));
        }

        [TestCase(1600, 0, 0, true)]
        [TestCase(1600.01, 0, 0, false)]
        [TestCase(1750, 20, 20, true)]
        [TestCase(1750.01, 20, 20, false)]
        [TestCase(2000, 60, 60, true)]
        [TestCase(2000.01, 60, 60, false)]
        [TestCase(1700, 120, 0, false)]
        [TestCase(1700, 0, 120, false)]
        [TestCase(1700, 120, 20, true)]
        [TestCase(100000, 120, 120, true)]
        public void BothPlayersMustAcceptTheRatingGap(double rating, double waitA, double waitB, bool expected)
        {
            var a = Make("a", wait: waitA);
            var b = Make("b", rating: rating, wait: waitB);
            Assert.AreEqual(expected, Matchmaking.CanMatch(a, b, cfg));
            Assert.AreEqual(expected, Matchmaking.CanMatch(b, a, cfg));
        }

        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(3, true)]
        [TestCase(4, false)]
        [TestCase(int.MaxValue, false)]
        public void ArenaCapIsNeverRelaxedEvenWithInfiniteWindows(int arena, bool expected)
        {
            Assert.AreEqual(expected, Matchmaking.CanMatch(Make("a", wait: 120),
                Make("b", rating: 100000, arena: arena, wait: 120), cfg));
        }

        [TestCase(2, 1, "hash")]
        [TestCase(1, 2, "hash")]
        [TestCase(1, 1, "other")]
        [TestCase(1, 1, "HASH")]
        public void VersionOrOrdinalContentMismatchPreventsMatch(int protocol, int sim, string hash)
        {
            Assert.IsFalse(Matchmaking.CanMatch(Make("a", wait: 120),
                Make("b", wait: 120, protocol: protocol, sim: sim, hash: hash), cfg));
        }

        [Test]
        public void OpponentOrderingUsesArenaThenRatingThenWaitThenOrdinalIdRegardlessOfPoolOrder()
        {
            var seeker = Make("self", wait: 120);
            var pool = new List<Ticket>
            {
                Make("neighbor", arena: 1, wait: 1000),
                Make("far", rating: 1590, wait: 1000),
                Make("young", rating: 1510),
                Make("z", rating: 1490, wait: 20),
                Make("a", rating: 1510, wait: 20),
                Make("B", rating: 1490, wait: 20),
                seeker, Make("incompatible", wait: 1000, sim: 2),
                Make("gap", arena: 4, wait: 1000)
            };
            foreach (string expected in new[] { "B", "a", "z", "young", "far", "neighbor" })
            {
                Assert.AreEqual(expected, Matchmaking.BestOpponent(seeker, pool, cfg).Value.TicketId);
                pool.Reverse();
                Assert.AreEqual(expected, Matchmaking.BestOpponent(seeker, pool, cfg).Value.TicketId);
                pool.RemoveAll(ticket => ticket.TicketId == expected);
            }
            Assert.IsNull(Matchmaking.BestOpponent(seeker, pool, cfg));
            Assert.IsNull(Matchmaking.BestOpponent(seeker, Array.Empty<Ticket>(), cfg));
        }

        [Test]
        public void SelfExclusionUsesOrdinalTicketIdInsteadOfFullValueEquality()
        {
            var seeker = Make("self");
            Assert.IsNull(Matchmaking.BestOpponent(seeker, new[] { Make("self", rating: 1510) }, cfg));
            Assert.AreEqual("SELF", Matchmaking.BestOpponent(seeker, new[] { Make("SELF") }, cfg).Value.TicketId);
        }

        [Test]
        public void DuplicatePoolIdsAreRejectedToKeepTheOrderTotal()
        {
            Assert.Throws<ArgumentException>(() => Matchmaking.BestOpponent(Make("self"),
                new[] { Make("same"), Make("same", rating: 1510) }, cfg));
        }

        [TestCase(0, false)]
        [TestCase(89.99, false)]
        [TestCase(90, true)]
        [TestCase(120, true)]
        public void BotOfferStartsAtConfiguredTime(double wait, bool expected)
        {
            Assert.AreEqual(expected, Matchmaking.ShouldOfferBot(wait, cfg));
        }

        [Test]
        public void CustomWindowsArenaCapAndBotTimeAreUsed()
        {
            var custom = new MatchmakingConfig
            {
                WindowKnots = new[] { (0d, 0d), (5d, 50d) }, MaxArenaGap = 0, BotOfferSeconds = 10
            };
            Assert.AreEqual(0, Matchmaking.Window(4.99, custom));
            Assert.AreEqual(50, Matchmaking.Window(5, custom));
            Assert.AreEqual(50, Matchmaking.Window(1000, custom));
            Assert.IsFalse(Matchmaking.CanMatch(Make("a", wait: 5), Make("b", arena: 1, wait: 5), custom));
            Assert.IsTrue(Matchmaking.CanMatch(Make("a", wait: 5), Make("b", rating: 1550, wait: 5), custom));
            Assert.IsFalse(Matchmaking.ShouldOfferBot(9.99, custom));
            Assert.IsTrue(Matchmaking.ShouldOfferBot(10, custom));
        }

        [Test]
        public void RejectsInvalidConfiguration()
        {
            var invalidConfigs = new[]
            {
                new MatchmakingConfig { WindowKnots = null },
                new MatchmakingConfig { WindowKnots = Array.Empty<(double, double)>() },
                new MatchmakingConfig { WindowKnots = new[] { (1d, 100d) } },
                new MatchmakingConfig { WindowKnots = new[] { (0d, 100d), (0d, 200d) } },
                new MatchmakingConfig { WindowKnots = new[] { (0d, 100d), (-1d, 200d) } },
                new MatchmakingConfig { WindowKnots = new[] { (0d, 100d), (double.NaN, 200d) } },
                new MatchmakingConfig { WindowKnots = new[] { (0d, 100d), (double.PositiveInfinity, 200d) } },
                new MatchmakingConfig { WindowKnots = new[] { (0d, -1d) } },
                new MatchmakingConfig { WindowKnots = new[] { (0d, double.NaN) } },
                new MatchmakingConfig { WindowKnots = new[] { (0d, double.NegativeInfinity) } },
                new MatchmakingConfig { WindowKnots = new[] { (0d, 200d), (1d, 100d) } },
                new MatchmakingConfig { MaxArenaGap = -1 },
                new MatchmakingConfig { MaxArenaGap = 2 },
                new MatchmakingConfig { BotOfferSeconds = -1 },
                new MatchmakingConfig { BotOfferSeconds = double.NaN },
                new MatchmakingConfig { BotOfferSeconds = double.PositiveInfinity }
            };
            foreach (var invalid in invalidConfigs)
            {
                Assert.That(() => invalid.Validate(), Throws.InstanceOf<ArgumentException>());
                Assert.That(() => Matchmaking.Window(0, invalid), Throws.InstanceOf<ArgumentException>());
            }
        }

        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidWaitsAreRejected(double wait)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Matchmaking.Window(wait, cfg));
            Assert.Throws<ArgumentOutOfRangeException>(() => Matchmaking.ShouldOfferBot(wait, cfg));
            Assert.Throws<ArgumentOutOfRangeException>(() => Make("a", wait: wait));
        }

        [Test]
        public void InvalidTicketsCannotEnterMatchmaking()
        {
            Assert.Throws<ArgumentException>(() => Make(""));
            Assert.Throws<ArgumentException>(() => Make("a", hash: null));
            Assert.Throws<ArgumentOutOfRangeException>(() => Make("a", rating: double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => Make("a", rating: double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => Make("a", arena: -1));
            Assert.Throws<ArgumentException>(() => Matchmaking.CanMatch(default, Make("a"), cfg));
        }

        private static Ticket Make(string id, double rating = 1500, int arena = 2, double wait = 0,
            int protocol = 1, int sim = 1, string hash = "hash")
        {
            return new Ticket(id, rating, arena, wait, protocol, sim, hash);
        }
    }
}
