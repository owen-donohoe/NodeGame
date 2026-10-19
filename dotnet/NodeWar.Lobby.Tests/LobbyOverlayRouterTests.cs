using NodeWar.Lobby;
using NUnit.Framework;

public class LobbyOverlayRouterTests
{
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
