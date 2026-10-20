using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class WorkshopLayoutTests
    {
        [TestCase(0)]
        [TestCase(200)]
        [TestCase(480)]
        [TestCase(800)]
        public void CardClipTracksHeaderMiddleWithoutMovingScrolledContent(float scroll)
        {
            var layout = WorkshopLayout.Clip(480, 160, 1600, scroll);
            Assert.That(layout.ClipTop - scroll, Is.EqualTo(layout.HeaderTop - scroll + 80));
            Assert.That(layout.ClipTop + layout.CardOffset + WorkshopLayout.CardGap - scroll, Is.EqualTo(652 - scroll));
            Assert.That(layout.ContentHeight, Is.EqualTo(1612));
            Assert.That(layout.MaskHeight + layout.ClipTop, Is.EqualTo(2252));
            if (scroll >= 480) Assert.That(layout.HeaderTop - scroll, Is.Zero);
        }
    }
}
