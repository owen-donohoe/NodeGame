using System;
using System.Linq;
using NodeWar.Lobby;
using NUnit.Framework;

public class LobbyIconContextCompatibilityTests
{
    [Test]
    public void SurvivingContexts_KeepSerializedValues()
    {
        Assert.AreEqual(0, (int)LobbyIconContext.Anywhere);
        Assert.AreEqual(1, (int)LobbyIconContext.TopBar);
        Assert.AreEqual(2, (int)LobbyIconContext.PageHeader);
        Assert.AreEqual(3, (int)LobbyIconContext.NavBar);
        Assert.AreEqual(4, (int)LobbyIconContext.HeadsUpDisplay);
        Assert.AreEqual(5, (int)LobbyIconContext.NodeSheet);
        Assert.AreEqual(6, (int)LobbyIconContext.InlineText);
        Assert.AreEqual(7, (int)LobbyIconContext.EmotePicker);
        Assert.AreEqual(8, (int)LobbyIconContext.EmoteBubble);
        Assert.AreEqual(9, (int)LobbyIconContext.OffScreenIndicator);
        Assert.AreEqual(10, (int)LobbyIconContext.ShopCard);
        Assert.AreEqual(12, (int)LobbyIconContext.Workshop);
        Assert.AreEqual(13, (int)LobbyIconContext.Profile);
        Assert.AreEqual(14, (int)LobbyIconContext.Home);
    }

    [Test]
    public void RetiredSuitTreeContext_IsReservedAndNotAdvertised()
    {
        Assert.IsFalse(Enum.IsDefined(typeof(LobbyIconContext), 11), "value 11 stays reserved, never reused");
        Assert.IsFalse(Enum.GetNames(typeof(LobbyIconContext)).Contains("SuitTree"));
        foreach (var icon in LobbyIconUsage.All)
            foreach (var location in icon.Locations)
                Assert.AreNotEqual(11, (int)location.Context, icon.Path);
    }
}
