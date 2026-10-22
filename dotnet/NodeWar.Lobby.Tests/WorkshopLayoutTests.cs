using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class WorkshopLayoutTests
    {
        [TestCase(208)]
        [TestCase(276)]
        [TestCase(338)]
        [TestCase(540)]
        [TestCase(800)]
        public void TwoSquareTilesFillWidthWithExactlyOneGap(float available)
        {
            float width = WorkshopLayout.TileWidth(available);
            float used = 2 * width + WorkshopLayout.TileGap;
            Assert.That(used, Is.LessThanOrEqualTo(available));
            Assert.That(used, Is.EqualTo(available).Within(0.001));
            Assert.That(width, Is.GreaterThanOrEqualTo(44));
        }

        [TestCase(0)]
        [TestCase(200)]
        [TestCase(480)]
        [TestCase(800)]
        public void CardClipTracksHeaderMiddleWithoutMovingScrolledContent(float scroll)
        {
            var layout = WorkshopLayout.Clip(480, 160, 1600, scroll, 80);
            Assert.That(layout.ClipTop - scroll, Is.EqualTo(layout.HeaderTop - scroll + 80));
            Assert.That(layout.ClipTop + layout.CardOffset + WorkshopLayout.CardGap - scroll, Is.EqualTo(652 - scroll));
            Assert.That(layout.ContentHeight, Is.EqualTo(1612));
            Assert.That(layout.MaskHeight + layout.ClipTop, Is.EqualTo(2252));
            if (scroll >= 480) Assert.That(layout.HeaderTop - scroll, Is.Zero);
        }

        [Test]
        public void DefaultClipEdgeIsTheMiddleOfTheSelector()
        {
            var layout = WorkshopLayout.Clip(300, WorkshopLayout.HeaderHeight, 900, 0);
            Assert.That(layout.ClipTop - layout.HeaderTop, Is.EqualTo(59 + 8 + 26));
        }
    }
}
