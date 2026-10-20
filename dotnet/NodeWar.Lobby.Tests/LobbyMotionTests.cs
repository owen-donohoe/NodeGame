using System;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class LobbyMotionTests
    {
        [Test]
        public void EveryTabPairFollowsPillOrderIncludingNonAdjacentJumps()
        {
            var order = new[] { LobbyPageID.Shop, LobbyPageID.Workshop, LobbyPageID.Home, LobbyPageID.Social, LobbyPageID.Guild };
            for (int from = 0; from < order.Length; from++)
                for (int to = 0; to < order.Length; to++)
                    Assert.That(LobbyMotion.ContentDirection(order[from], order[to]), Is.EqualTo(Math.Sign(from - to)));
        }

        [TestCase(-1f, 0f, 0f)]
        [TestCase(0f, 0f, 0f)]
        [TestCase(0.5f, 0.875f, 0.125f)]
        [TestCase(1f, 1f, 1f)]
        [TestCase(2f, 1f, 1f)]
        public void CubicsHaveExpectedEndpointsMidpointsAndClamping(float t, float outValue, float inValue)
        {
            Assert.That(LobbyMotion.OutCubic(t), Is.EqualTo(outValue).Within(0.00001f));
            Assert.That(LobbyMotion.InCubic(t), Is.EqualTo(inValue).Within(0.00001f));
        }

        [TestCase(OverlayKind.Panel)]
        [TestCase(OverlayKind.Sheet)]
        [TestCase(OverlayKind.Menu)]
        public void OrdinaryOverlaysCannotHidePillsEvenWithLegacyFlag(OverlayKind kind)
        {
            var routes = new LobbyRouteStack();
            routes.Push(new LobbyOverlay("bell-or-sheet", kind, true));
            Assert.That(routes.HidesPills, Is.False);
        }

        [Test]
        public void PanelOverSearchingKeepsPillsHiddenUntilTheFlowCloses()
        {
            var routes = new LobbyRouteStack();
            routes.Push(new LobbyOverlay("searching", OverlayKind.Fullscreen, true));
            routes.Push(new LobbyOverlay("invite", OverlayKind.Sheet, true));
            Assert.That(routes.HidesPills, Is.True);
            routes.Pop("searching");
            Assert.That(routes.HidesPills, Is.False);
        }
    }
}
