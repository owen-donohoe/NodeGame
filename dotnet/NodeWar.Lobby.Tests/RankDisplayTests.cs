using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class RankDisplayTests
    {
        [TestCase(0, 0, 0, 300)]
        [TestCase(-1, 0, 0, 300)]
        [TestCase(int.MinValue, 0, 0, 300)]
        [TestCase(299, 0, 299, 300)]
        [TestCase(300, 1, 0, 400)]
        [TestCase(699, 1, 399, 400)]
        [TestCase(700, 2, 0, 500)]
        [TestCase(1199, 2, 499, 500)]
        [TestCase(1200, 3, 0, 600)]
        [TestCase(1799, 3, 599, 600)]
        [TestCase(1800, 4, 0, 700)]
        [TestCase(2499, 4, 699, 700)]
        [TestCase(2500, 5, 0, null)]
        [TestCase(2700, 5, 200, null)]
        [TestCase(int.MaxValue, 5, int.MaxValue - 2500, null)]
        public void Progress_MatchesArenaBoundaries(int rr, int arena, int into, int? span)
        {
            var display = new RankDisplay(rr);
            Assert.That(display.RR, Is.EqualTo(rr < 0 ? 0 : rr));
            Assert.That(display.Arena, Is.EqualTo(arena));
            Assert.That(display.Name, Is.EqualTo("Arena " + (arena + 1)));
            Assert.That(display.RRIntoArena, Is.EqualTo(into));
            Assert.That(display.Span, Is.EqualTo(span));
            Assert.That(display.Fill, Is.InRange(0f, 1f));
            Assert.That(display.Fill, Is.EqualTo(span.HasValue ? (float)into / span.Value : 1f).Within(0.000001f));
        }
    }
}
