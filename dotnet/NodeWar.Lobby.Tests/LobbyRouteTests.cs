using NodeWar.Lobby;
using NUnit.Framework;

public class LobbyRouteTests
{
    [Test]
    public void BackClosesTopThenGoesHomeThenConfirmsExit()
    {
        var stack = new LobbyRouteStack();
        Assert.IsNull(stack.Top);
        Assert.AreEqual(BackActionKind.ConfirmExit, stack.Back(LobbyPageID.Home).Kind);
        Assert.AreEqual(BackActionKind.GoHome, stack.Back(LobbyPageID.Shop).Kind);
        stack.Push(new LobbyOverlay("sheet", OverlayKind.Sheet));
        stack.Push(new LobbyOverlay("panel", OverlayKind.Fullscreen, true, true));
        Assert.AreEqual(2, stack.Count);
        Assert.IsTrue(stack.IsSheetOpen);
        Assert.IsTrue(stack.AnyFullscreen);
        Assert.IsTrue(stack.Top.HidesPills && stack.Top.HidesHeader);
        Assert.AreEqual(BackActionKind.CloseTop, stack.Back(LobbyPageID.Home).Kind);
        Assert.AreEqual("panel", stack.Back(LobbyPageID.Shop).ID);
        Assert.IsTrue(stack.Pop("sheet"));
        Assert.AreEqual("panel", stack.Top.ID);
        Assert.IsFalse(stack.IsSheetOpen);
        Assert.IsFalse(stack.Pop("absent"));
        stack.Pop("panel");
        Assert.AreEqual(0, stack.Count);
        Assert.IsFalse(stack.AnyFullscreen);
    }

    [Test]
    public void ReopeningAnIdMovesItToTopWithoutDuplicating()
    {
        var stack = new LobbyRouteStack();
        int changes = 0;
        stack.Changed += () => changes++;
        stack.Push(new LobbyOverlay("a", OverlayKind.Panel));
        stack.Push(new LobbyOverlay("b", OverlayKind.Menu));
        stack.Push(new LobbyOverlay("a", OverlayKind.Panel));
        Assert.AreEqual(2, stack.Count);
        Assert.AreEqual("a", stack.Top.ID);
        stack.Pop("missing");
        Assert.AreEqual(3, changes);
    }

    [TestCase("nodewar://page/shop", LobbyRouteKind.Page, "shop")]
    [TestCase("nodewar://page/loadout", LobbyRouteKind.Page, "loadout")]
    [TestCase("nodewar://page/home", LobbyRouteKind.Page, "home")]
    [TestCase("nodewar://page/social", LobbyRouteKind.Page, "social")]
    [TestCase("nodewar://page/guild", LobbyRouteKind.Page, "guild")]
    [TestCase("NODEWAR://PAGE/SHOP", LobbyRouteKind.Page, "shop")]
    [TestCase("nodewar://shop/daily", LobbyRouteKind.ShopStall, "daily")]
    [TestCase("nodewar://shop/crates", LobbyRouteKind.ShopStall, "crates")]
    [TestCase("nodewar://shop/weekly", LobbyRouteKind.ShopStall, "weekly")]
    [TestCase("nodewar://shop/bundle", LobbyRouteKind.ShopStall, "bundle")]
    [TestCase("nodewar://shop/coins", LobbyRouteKind.ShopStall, "coins")]
    [TestCase("nodewar://shop/laurels", LobbyRouteKind.ShopStall, "laurels")]
    [TestCase("nodewar://profile/PlayerID", LobbyRouteKind.Profile, "PlayerID")]
    [TestCase("nodewar://guild/chat", LobbyRouteKind.GuildChat, null)]
    [TestCase("nodewar://crate/open", LobbyRouteKind.CrateOpen, null)]
    [TestCase("nodewar://bell", LobbyRouteKind.Bell, null)]
    public void ParsesKnownRoutes(string url, LobbyRouteKind kind, string target)
    {
        Assert.IsTrue(LobbyDeepLink.TryParse(url, out var route));
        Assert.AreEqual(kind, route.Kind);
        Assert.AreEqual(target, route.Target);
    }

    [TestCase(null)] [TestCase("")] [TestCase("garbage")] [TestCase("https://page/home")]
    [TestCase("nodewar://")] [TestCase("nodewar://page")] [TestCase("nodewar://page/")]
    [TestCase("nodewar://page/unknown")] [TestCase("nodewar://shop/nope")]
    [TestCase("nodewar://bell/")] [TestCase("nodewar://profile/a/b")]
    [TestCase("nodewar://profile/a?b")] [TestCase("nodewar://profile/%ZZ")]
    [TestCase("nodewar://profile/ ")] [TestCase(" nodewar://bell")]
    public void RejectsMalformedRoutesWithoutThrowing(string url)
    {
        Assert.IsFalse(LobbyDeepLink.TryParse(url, out _));
    }
}
