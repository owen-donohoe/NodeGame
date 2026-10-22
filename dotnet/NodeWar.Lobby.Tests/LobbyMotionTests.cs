using System;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class LobbyMotionTests
    {
        [TestCase(float.NaN, 48)]
        [TestCase(float.PositiveInfinity, 48)]
        [TestCase(float.NegativeInfinity, 48)]
        [TestCase(0, 48)]
        [TestCase(-1, 48)]
        [TestCase(600, 600)]
        public void SheetMotionHasFiniteTravelBeforeAndAfterLayout(float height, float expected) =>
            Assert.AreEqual(expected, LobbyMotion.SheetTravel(height));

        [Test]
        public void SpringIsFrameRateIndependentAndRetargetPreservesMomentum()
        {
            var a = new LobbyMotion.Spring(0) { Target = 1, Velocity = 2 };
            var b = a;
            a.Step(0.12f);
            for (int i = 0; i < 12; i++) b.Step(0.01f);
            Assert.That(a.Position, Is.EqualTo(b.Position).Within(0.00001f));
            Assert.That(a.Velocity, Is.EqualTo(b.Velocity).Within(0.0001f));
            float position = a.Position, velocity = a.Velocity;
            a.Target = -1;
            Assert.That(a.Position, Is.EqualTo(position));
            Assert.That(a.Velocity, Is.EqualTo(velocity));
            a.Step(0.001f);
            Assert.That(a.Position, Is.GreaterThan(position));
            a.Step(1f);
            Assert.That(a.Position, Is.EqualTo(-1).Within(0.001f));
        }

        [Test]
        public void PageSpringSettlesAround350msAndPopOvershoots()
        {
            var page = new LobbyMotion.Spring(0) { Target = 1 };
            page.Step(0.350f);
            Assert.That(page.Settled, Is.True);
            var pop = new LobbyMotion.Spring(0.92f) { Target = 1 };
            pop.Step(0.150f, LobbyMotion.PopDamping);
            Assert.That(pop.Position, Is.GreaterThan(1));
            pop.Step(0.150f, LobbyMotion.PopDamping);
            Assert.That(pop.Position, Is.EqualTo(1).Within(0.001f));
        }

        [Test]
        public void NonAdjacentJumpTravelsOneWidthWithOnlyTwoPagesAndSharedProgress()
        {
            var strip = new LobbyMotion.PageStrip(); strip.Reset(0); strip.Go(4);
            Assert.That(strip.Motion.Target, Is.EqualTo(1));
            strip.Step(0.05f);
            Assert.That(strip.Visible(0) && strip.Visible(4), Is.True);
            for (int i = 1; i < 4; i++) Assert.That(strip.Visible(i), Is.False);
            Assert.That(strip.Slots[4] - strip.Slots[0], Is.EqualTo(1));
            strip.Pair(out int from, out int to, out float t);
            Assert.That(from, Is.EqualTo(0)); Assert.That(to, Is.EqualTo(4));
            Assert.That(t, Is.EqualTo(strip.Motion.Position));
            Assert.That(from + (to - from) * t, Is.GreaterThan(t)); // pills/camera traverse the full logical path
            strip.Step(1);
            Assert.That(strip.Visible(4), Is.True); Assert.That(strip.Visible(0), Is.False);
        }

        [Test]
        public void DragTracksFingerRubberBandsEdgesAndRetainsReleaseVelocity()
        {
            var strip = new LobbyMotion.PageStrip(); strip.Reset(2); strip.BeginDrag();
            strip.Drag(0.4f, 2f);
            Assert.That(strip.Motion.Position, Is.EqualTo(0.4f));
            Assert.That(strip.Visible(3), Is.True); Assert.That(strip.Visible(1), Is.False);
            strip.Release(1); Assert.That(strip.Motion.Velocity, Is.EqualTo(2));
            strip.Step(1); Assert.That(strip.Current, Is.EqualTo(3));
            strip.Reset(0); strip.BeginDrag(); strip.Drag(-0.5f, -2);
            Assert.That(strip.Motion.Position, Is.EqualTo(LobbyMotion.RubberBand(-0.5f, 0, 1, 1)).Within(0.0001f));
            strip.Release(-1); strip.Step(1); Assert.That(strip.Current, Is.Zero);
            Assert.That(strip.Motion.Position, Is.Zero);
        }

        [Test]
        public void EdgeReleaseReturnsBeforeSnappingToAnotherPageAndRegrabDoesNotJump()
        {
            var strip = new LobbyMotion.PageStrip(); strip.Reset(0); strip.BeginDrag(); strip.Drag(-1f, -4);
            float position = strip.Motion.Position;
            strip.BeginDrag(); strip.Drag(0, 0);
            Assert.That(strip.Motion.Position, Is.EqualTo(position).Within(0.0001));
            strip.Release(1);
            Assert.That(strip.Motion.Target, Is.Zero);
            strip.Step(0.05f);
            Assert.That(strip.Motion.Position, Is.LessThan(0));
            for (int i = 0; i < 100; i++) strip.Step(0.016f);
            Assert.That(strip.Motion.Position, Is.EqualTo(strip.Slots[1]));
        }

        [Test]
        public void InterruptedNavigationNeverMovesExistingPagesOrResetsVelocity()
        {
            var strip = new LobbyMotion.PageStrip(); strip.Reset(2); strip.Go(4); strip.Step(0.03f);
            float x = strip.Motion.Position, v = strip.Motion.Velocity, slot = strip.Slots[4];
            strip.Go(0);
            Assert.That(strip.Motion.Position, Is.EqualTo(x));
            Assert.That(strip.Motion.Velocity, Is.EqualTo(v));
            Assert.That(strip.Slots[4], Is.EqualTo(slot));
            strip.Go(4); Assert.That(strip.Motion.Velocity, Is.EqualTo(v));
            strip.BeginDrag(); strip.Drag(0, v);
            Assert.That(strip.Motion.Position, Is.EqualTo(x));
            strip.Release(0); strip.Go(0); strip.Go(3);
            x = strip.Motion.Position; strip.BeginDrag(); strip.Drag(0, v);
            Assert.That(strip.Motion.Position, Is.EqualTo(x));
        }

        [Test]
        public void EdgeCameraAndPillProgressUseTheRubberBandCoordinate()
        {
            var strip = new LobbyMotion.PageStrip(); strip.Reset(0); strip.BeginDrag(); strip.Drag(-0.5f, -1);
            strip.Pair(out int from, out int to, out float progress);
            Assert.That(from, Is.Zero); Assert.That(to, Is.EqualTo(1));
            Assert.That(progress, Is.EqualTo(strip.Motion.Position));
        }

        [Test]
        public void SwipeInterruptingLongJumpStillReachesItsLogicalNeighbour()
        {
            var strip = new LobbyMotion.PageStrip(); strip.Reset(0); strip.Go(4); strip.Step(0.03f);
            strip.BeginDrag(); strip.Drag(-0.1f, -1f);
            float x = strip.Motion.Position, v = strip.Motion.Velocity;
            strip.Release(-1);
            Assert.That(strip.Current, Is.EqualTo(3));
            Assert.That(strip.Motion.Position, Is.EqualTo(x));
            Assert.That(strip.Motion.Velocity, Is.EqualTo(v));
            strip.Step(1f); Assert.That(strip.Visible(3), Is.True);
        }

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

        [Test]
        public void ReducedMotion_SnapsSpringsAndPageStrip_AndShortensPans()
        {
            try
            {
                int raised = 0;
                LobbyMotion.ReducedChanged += () => raised++;
                LobbyMotion.SetReduced(true);
                LobbyMotion.SetReduced(true);
                Assert.That(raised, Is.EqualTo(1));
                var spring = new LobbyMotion.Spring(0) { Target = 1, Velocity = 5 };
                spring.Advance(0.001f);
                Assert.That(spring.Position, Is.EqualTo(1));
                Assert.That(spring.Settled, Is.True);
                var strip = new LobbyMotion.PageStrip();
                strip.Reset(2); strip.Go(3);
                Assert.That(strip.Moving, Is.True);
                strip.Step(0.001f);
                Assert.That(strip.Moving, Is.False);
                Assert.That(strip.Motion.Position, Is.EqualTo(strip.Slots[3]));
                Assert.That(LobbyMotion.PanSeconds, Is.EqualTo(LobbyMotion.ReducedPanSeconds));
                Assert.That(LobbyMotion.ReducedFadeSeconds, Is.EqualTo(0.08f));
                LobbyMotion.SetReduced(false);
                var normal = new LobbyMotion.Spring(0) { Target = 1 };
                normal.Advance(0.01f);
                Assert.That(normal.Position, Is.LessThan(1));
                Assert.That(LobbyMotion.PanSeconds, Is.EqualTo(LobbyMotion.PageSeconds));
            }
            finally { LobbyMotion.SetReduced(false); }
        }
    }
}
