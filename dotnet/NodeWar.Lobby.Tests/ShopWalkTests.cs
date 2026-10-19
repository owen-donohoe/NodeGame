using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class ShopWalkTests
    {
        [Test]
        public void Panels_AreOrderedFarToNear_AndOnlySignalChanges()
        {
            var distances = new[] { 20f, 10f, 5f, 10f, 30f };
            var order = new[] { 0, 1, 2, 3, 4 };
            Assert.IsTrue(ShopProjection.OrderByDistance(distances, order));
            CollectionAssert.AreEqual(new[] { 4, 0, 1, 3, 2 }, order);
            Assert.IsFalse(ShopProjection.OrderByDistance(distances, order));
            distances[0] = 1;
            Assert.IsTrue(ShopProjection.OrderByDistance(distances, order));
            Assert.AreEqual(0, order[4]);
        }

        [TestCase(0, 2, false)] // Daily must be absent at Weekly.
        [TestCase(1, 2, true)]  // Only the immediately previous stall remains.
        [TestCase(2, 2, true)]
        [TestCase(3, 2, false)]
        [TestCase(0, 1.001f, false)]
        public void Panels_HideBeyondOneStallBehind(float stall, float position, bool visible)
            => Assert.AreEqual(visible, ShopProjection.IsVisible(stall, position, 10));

        [TestCase(390f / 844f)]
        [TestCase(360f / 900f)]
        [TestCase(9f / 16f)]
        public void MarketFrame_FitsPaperSceneryLeftAndOffersRight(float aspect)
        {
            ShopMarketFrame frame = ShopMarketFraming.ForAspect(aspect);
            float norm = (float)System.Math.Sqrt(frame.CameraBack * frame.CameraBack +
                ShopMarketFraming.CameraRise * ShopMarketFraming.CameraRise);
            // Project the widest opaque canopy edges, including its highest point.
            for (int corner = 0; corner < 4; corner++)
            {
                float x = ShopMarketFraming.StallX + ((corner & 1) == 0 ? -0.66f : 0.66f);
                float y = (corner & 2) == 0 ? 1.49f : 2f;
                float depth = (frame.CameraBack * frame.CameraBack +
                    (1f + ShopMarketFraming.CameraRise - y) * ShopMarketFraming.CameraRise) / norm;
                float halfWidth = depth * (float)System.Math.Tan(ShopMarketFraming.FieldOfView * System.Math.PI / 360) * aspect;
                float screenX = 0.5f + (x - frame.CameraX) / (2f * halfWidth);
                Assert.That(screenX, Is.InRange(0.01f, 0.46f));
            }
            float anchorHalfWidth = norm * (float)System.Math.Tan(ShopMarketFraming.FieldOfView * System.Math.PI / 360) * aspect;
            Assert.AreEqual(0.54f, 0.5f + (frame.AnchorX - frame.CameraX) / (2f * anchorHalfWidth), 0.0001f);
        }

        [TestCase("daily", 0)]
        [TestCase("crates", 1)]
        [TestCase("weekly", 2)]
        [TestCase("bundle", 3)]
        [TestCase("coins", 4)]
        [TestCase("laurels", 4)]
        [TestCase("BANK", 4)]
        [TestCase(null, -1)]
        [TestCase("missing", -1)]
        public void DeepLink_ResolvesStallsAndBankAliases(string name, int expected)
            => Assert.AreEqual(expected, ShopSnap.StallIndex(name));

        [TestCase(1.2f, 1, 2)]
        [TestCase(1.8f, -1, 1)]
        [TestCase(1.8f, 0, 2)]
        [TestCase(0, -1, 0)]
        [TestCase(4, 1, 4)]
        public void Snap_SelectsNearestInTravelDirection(float position, float direction, float expected)
            => Assert.AreEqual(expected, ShopSnap.Target(position, direction));

        [Test]
        public void Anchors_DailyAtTop_OthersAtMiddle()
        {
            Assert.AreEqual(0, ShopSnap.ScreenAnchor(0));
            for (int i = 1; i < 5; i++) Assert.AreEqual(0.5f, ShopSnap.ScreenAnchor(i));
        }

        [Test]
        public void Drag_ClampsBothEnds_ReleaseRetainsVelocity()
        {
            var model = new ShopSnap();
            model.BeginDrag();
            model.Drag(-1, 0.1f);
            Assert.AreEqual(0, model.Position);
            model.Drag(0.3f, 0.1f);
            model.Release();
            Assert.AreEqual(3, model.Velocity, 0.001f);
            model.Step(0.1f);
            Assert.Greater(model.Position, 0.3f);
            Assert.Less(model.Velocity, 3);
            model.BeginDrag();
            model.Drag(100, 0.1f);
            model.Release();
            model.Step(1);
            Assert.AreEqual(4, model.Position);
        }

        [Test]
        public void Inertia_IsExponentialAcrossTimesteps()
        {
            var a = Released(); var b = Released();
            a.Step(0.2f);
            b.Step(0.1f); b.Step(0.1f);
            Assert.AreEqual(a.Position, b.Position, 0.00001f);
            Assert.AreEqual(a.Velocity, b.Velocity, 0.00001f);
        }

        private static ShopSnap Released()
        {
            var model = new ShopSnap();
            model.BeginDrag(); model.Drag(0.2f, 0.1f); model.Release();
            return model;
        }

        [Test]
        public void DeepLink_AnimatesAndStopsExactly_DragInterrupts()
        {
            var model = new ShopSnap();
            model.ScrollTo(4);
            model.Step(0.016f);
            Assert.That(model.Position, Is.InRange(0.01f, 3.99f));
            for (int i = 0; i < 120; i++) model.Step(0.016f);
            Assert.AreEqual(4, model.Position);
            Assert.IsFalse(model.Moving);
            model.ScrollTo(0); model.BeginDrag(); model.Step(1);
            Assert.AreEqual(4, model.Position);
        }

        [TestCase(0, 1, true)]
        [TestCase(-1, 1, true)]
        [TestCase(-1.01f, 1, false)]
        [TestCase(0.1f, 1, false)]
        [TestCase(0, -1, false)]
        public void Projection_CullsPassedStallsDistantStallsAndBehindCamera(float stall, float depth, bool visible)
            => Assert.AreEqual(visible, ShopProjection.Project(100, 200, depth, 5, 5, stall, 0, 100, 0.5f).Visible);

        [Test]
        public void Projection_PerspectiveRecessionAndVerticalAnchor()
        {
            var daily = ShopProjection.Project(100, 200, 1, 5, 5, 0, 0, 100, 0);
            var next = ShopProjection.Project(100, 200, 1, 10, 5, -1, 0, 100, 0.5f);
            Assert.AreEqual(1, daily.Scale);
            Assert.AreEqual(200, daily.Y);
            Assert.AreEqual(0.3f, next.Scale, 0.0001f);
            Assert.AreEqual(185, next.Y, 0.0001f);
            var offset = ShopProjection.Project(100, 200, 1, 5, 5, 0, 0, 100, 0.5f, 200, 400);
            Assert.AreEqual(350, offset.Y);
            Assert.IsFalse(ShopProjection.Changed(1, 1.0001f, 0.001f));
            Assert.IsTrue(ShopProjection.Changed(float.NaN, 1, 0.001f));
        }
    }
}
