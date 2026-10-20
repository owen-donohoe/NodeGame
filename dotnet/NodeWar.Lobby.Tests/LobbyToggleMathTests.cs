using NodeWar.Lobby;
using NUnit.Framework;

public class LobbyToggleMathTests
{
    [Test]
    public void KnobSlidesFullyBetweenTheInnerEdges()
    {
        Assert.AreEqual(2f, LobbyToggleMath.KnobX(false));
        Assert.AreEqual(22f, LobbyToggleMath.KnobX(true));
    }

    [Test]
    public void KnobKeepsTheSameInsetOnBothSides()
    {
        float left = LobbyToggleMath.KnobX(false);
        float right = LobbyToggleMath.InnerWidth - (LobbyToggleMath.KnobX(true) + LobbyToggleMath.KnobSize);
        Assert.AreEqual(left, right);
        Assert.AreEqual(44f, LobbyToggleMath.InnerWidth);
    }
}
