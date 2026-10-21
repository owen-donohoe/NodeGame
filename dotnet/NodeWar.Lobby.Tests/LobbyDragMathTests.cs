using NodeWar.Lobby;
using NUnit.Framework;

public class LobbyDragMathTests
{
    [TestCase(800, 200, 400, 0)]
    [TestCase(800, 600, 400, 200)]
    [TestCase(-20, 600, 400, 0)]
    [TestCase(120, 600, 400, 120)]
    [TestCase(800, 400, 400, 0)]
    public void ContentResizeClampsOffset(float offset, float content, float viewport, float expected) =>
        Assert.AreEqual(expected, LobbyDragMath.ClampOffset(offset, content, viewport));

    [TestCase(7.99f, 0, false)] [TestCase(0, -7.99f, false)]
    [TestCase(8, 0, true)] [TestCase(0, -8, true)]
    public void EightPixelThresholdPreservesTaps(float x, float y, bool passed) =>
        Assert.AreEqual(passed, LobbyDragMath.PassedThreshold(x, y));

    [Test]
    public void ElasticTravelIsContinuousSymmetricAndBounded()
    {
        Assert.AreEqual(50, LobbyDragMath.Elastic(50, 100, 200));
        float top = LobbyDragMath.Elastic(-10000, 100, 200);
        float bottom = LobbyDragMath.Elastic(10100, 100, 200);
        Assert.That(top, Is.InRange(-60f, 0f));
        Assert.That(bottom, Is.InRange(100f, 160f));
        Assert.That(bottom - 100, Is.EqualTo(-top).Within(0.001));
        Assert.That(LobbyDragMath.Elastic(-0.01f, 100, 200), Is.EqualTo(-0.0055f).Within(0.001));
    }

    [Test]
    public void ResistanceIncreasesAndRegrabKeepsTheDisplayedOffset()
    {
        float a = LobbyDragMath.Elastic(-20, 100, 200), b = LobbyDragMath.Elastic(-40, 100, 200);
        float c = LobbyDragMath.Elastic(-60, 100, 200);
        Assert.Less(b - c, a - b);
        float raw = LobbyMotion.UnrubberBand(b, 0, 100, 200);
        Assert.That(LobbyDragMath.Elastic(raw, 100, 200), Is.EqualTo(b).Within(0.0001));
        Assert.That(LobbyMotion.EdgeVelocity(-1000, b, 0, 100), Is.EqualTo(-150));
    }

    [TestCase(-60f, 5000f, 0f)]
    [TestCase(160f, -5000f, 100f)]
    public void FastInwardReleaseCannotCrossEdgeAndReturnsInAbout300ms(float position, float velocity, float edge)
    {
        float initial = position;
        for (int i = 0; i < 20; i++)
        {
            LobbyDragMath.Step(ref position, ref velocity, 100, 0.016f);
            Assert.That(position, Is.InRange(System.Math.Min(initial, edge), System.Math.Max(initial, edge)));
        }
        Assert.That(position, Is.EqualTo(edge).Within(0.1));
    }

    [Test]
    public void EdgeCrossingIsIndependentOfFrameRateAndDissipatesFlick()
    {
        float a = 90, av = 1000, b = a, bv = av;
        LobbyDragMath.Step(ref a, ref av, 100, 0.1f);
        for (int i = 0; i < 10; i++) LobbyDragMath.Step(ref b, ref bv, 100, 0.01f);
        Assert.That(b, Is.EqualTo(a).Within(0.001));
        Assert.That(bv, Is.EqualTo(av).Within(0.01));
        Assert.Less(a, 103);
    }

    [Test]
    public void ReducedMotionClampsDragAndRelease()
    {
        try
        {
            LobbyMotion.SetReduced(true);
            Assert.AreEqual(0, LobbyDragMath.Elastic(-200, 100, 200));
            Assert.AreEqual(100, LobbyDragMath.Elastic(300, 100, 200));
            float p = -20, v = -1000;
            LobbyDragMath.Step(ref p, ref v, 100, 0.016f);
            Assert.AreEqual(0, p); Assert.AreEqual(0, v);
        }
        finally { LobbyMotion.SetReduced(false); }
    }

    [Test]
    public void ReleaseDropsStaleVelocityAndBoundsSamples()
    {
        Assert.AreEqual(1000, LobbyDragMath.Velocity(16, 16));
        Assert.AreEqual(0, LobbyDragMath.Velocity(16, 0));
        Assert.AreEqual(-5000, LobbyDragMath.Velocity(-1000, 1));
        Assert.AreEqual(250, LobbyDragMath.ReleaseVelocity(250, 100));
        Assert.AreEqual(0, LobbyDragMath.ReleaseVelocity(250, 101));
    }

    [Test]
    public void MomentumMovesAfterReleaseAndIsIndependentOfFrameRate()
    {
        float a = 100, av = 500, b = a, bv = av;
        LobbyDragMath.Step(ref a, ref av, 1000, 0.1f);
        for (int i = 0; i < 10; i++) LobbyDragMath.Step(ref b, ref bv, 1000, 0.01f);
        Assert.Greater(a, 100); Assert.That(av, Is.InRange(0f, 499f));
        Assert.That(b, Is.EqualTo(a).Within(0.001));
        Assert.That(bv, Is.EqualTo(av).Within(0.001));
    }

    [TestCase(-50, -100, 0)] [TestCase(150, 100, 100)]
    [TestCase(50, -2000, 0)] [TestCase(50, 2000, 100)]
    public void MomentumAndOverscrollSettleAtEitherEdge(float position, float velocity, float expected)
    {
        for (int i = 0; i < 300; i++) LobbyDragMath.Step(ref position, ref velocity, 100, 1f / 60);
        Assert.That(position, Is.EqualTo(expected).Within(0.1));
        Assert.AreEqual(0, velocity);
    }
}
