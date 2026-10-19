using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class OverlayBackHandlerTests
    {
        [Test]
        public void BackHandler_IsOptional_OnlyRunsForTop_AndCanConsume()
        {
            var stack = new LobbyRouteStack();
            int taps = 0;
            stack.Push(new LobbyOverlay("crate", OverlayKind.Fullscreen, true, true, () => { taps++; return true; }));
            stack.Push(new LobbyOverlay("claim", OverlayKind.Panel));
            Assert.IsFalse(stack.TryHandleBack("crate"));
            Assert.IsFalse(stack.TryHandleBack("claim"));
            Assert.AreEqual(0, taps);
            stack.Pop("claim");
            Assert.IsTrue(stack.TryHandleBack("crate"));
            Assert.AreEqual(1, taps);
            Assert.AreEqual("crate", stack.Top.ID);
            stack.Pop("crate");
            stack.Push(new LobbyOverlay("normal", OverlayKind.Panel, backHandler: () => false));
            Assert.IsFalse(stack.TryHandleBack("normal"));
        }

        [Test]
        public void Router_BackIsConsumed_ButForcedCleanupCloses()
        {
            var router = new LobbyOverlayRouter();
            int closed = 0;
            router.Open(new LobbyOverlay("crate", OverlayKind.Fullscreen, backHandler: () => true), () => closed++);
            router.Close("crate");
            Assert.AreEqual(1, router.Stack.Count);
            Assert.AreEqual(0, closed);
            router.CloseAll();
            Assert.AreEqual(0, router.Stack.Count);
            Assert.AreEqual(1, closed);
        }
    }
}
