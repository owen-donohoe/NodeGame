using NodeWar.Lobby;
using NUnit.Framework;

public class SwipeDecisionTests
{
    [TestCase(-60, 0, 1000, 1)] [TestCase(60, 0, 1000, -1)]
    [TestCase(-59, 0, 1000, 0)] [TestCase(-30, 0, 40, 1)]
    [TestCase(20, 0, 30, -1)] [TestCase(19, 0, 10, 0)]
    [TestCase(30, 0, 0, 0)] [TestCase(30, 0, 100, 0)]
    [TestCase(60, 40, 100, 0)] [TestCase(60, 39, 1000, -1)]
    [TestCase(0, 100, 100, 0)]
    public void TravelAxisAndFlickThresholds(float dx, float dy, long ms, int expected)
    {
        Assert.AreEqual(expected, SwipeDecision.Decide(dx, dy, ms, false, false));
    }

    [TestCase(true, false)] [TestCase(false, true)] [TestCase(true, true)]
    public void OverlayAndExcludedElementsBlockSwipes(bool overlay, bool excluded)
    {
        Assert.AreEqual(0, SwipeDecision.Decide(-100, 0, 100, overlay, excluded));
    }

    [Test]
    public void AxisLockCannotChangeAfterVerticalStart()
    {
        Assert.AreEqual(SwipeAxis.Pending, SwipeDecision.Lock(3, 4));
        Assert.AreEqual(SwipeAxis.Vertical, SwipeDecision.Lock(10, 20));
        Assert.AreEqual(SwipeAxis.Horizontal, SwipeDecision.Lock(20, 10));
        Assert.AreEqual(0, SwipeDecision.Decide(-100, 0, 100, false, false, SwipeAxis.Vertical));
        Assert.AreEqual(0, SwipeDecision.Decide(float.NaN, 0, 100, false, false));
    }

    [Test]
    public void PageValuesRemainAppendOnly()
    {
        Assert.AreEqual(0, (int)LobbyPageID.Home);
        Assert.AreEqual(1, (int)LobbyPageID.Workshop);
        Assert.AreEqual(2, (int)LobbyPageID.Shop);
        Assert.AreEqual(3, (int)LobbyPageID.Social);
        Assert.AreEqual(4, (int)LobbyPageID.Guild);
    }
}
