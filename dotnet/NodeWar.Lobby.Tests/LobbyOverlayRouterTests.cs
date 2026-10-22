using NodeWar.Lobby;
using NUnit.Framework;

public class LobbyOverlayRouterTests
{
    [Test]
    public void SearchingOnlyAdmitsTheInviteSheet()
    {
        var router = new LobbyOverlayRouter(); bool rejected = false;
        router.Open(new LobbyOverlay("searching", OverlayKind.Fullscreen, true), () => { });
        router.Open(new LobbyOverlay("profile", OverlayKind.Sheet), () => rejected = true);
        Assert.That(rejected, Is.True); Assert.That(router.Stack.Count, Is.EqualTo(1));
        router.Open(new LobbyOverlay("teammate-invite", OverlayKind.Sheet), () => { });
        Assert.That(router.Stack.Count, Is.EqualTo(2));
        Assert.That(router.Allows("burger"), Is.False);
    }
    [Test]
    public void DismissalUsesCloseCallbacksAndToleratesAlreadyUnregisteredEntries()
    {
        var router = new LobbyOverlayRouter();
        string closed = "";
        router.Open(new LobbyOverlay("sheet", OverlayKind.Sheet), () => { closed += "s"; router.Closed("sheet"); });
        router.Open(new LobbyOverlay("menu", OverlayKind.Menu), () => closed += "m");
        router.CloseAll();
        Assert.AreEqual("ms", closed);
        Assert.AreEqual(0, router.Stack.Count);
        router.Close("unknown");
        router.Close(null);
    }
}
