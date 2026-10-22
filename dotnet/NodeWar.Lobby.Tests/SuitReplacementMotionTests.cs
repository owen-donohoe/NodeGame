using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class SuitReplacementMotionTests
    {
        [Test]
        public void LiftAndShakeAreContinuousBoundedRepeatableAndPhaseOffset()
        {
            for (int slot = 0; slot < 3; slot++)
            {
                Assert.That(SuitReplacementMotion.Lift(0, slot, 3), Is.Zero);
                Assert.That(SuitReplacementMotion.Shake(0, slot, 3), Is.Zero);
                for (int sample = 0; sample < 15000; sample++)
                {
                    float t = sample * 0.001f;
                    float lift = SuitReplacementMotion.Lift(t, slot, 3);
                    float shake = SuitReplacementMotion.Shake(t, slot, 3);
                    Assert.That(lift, Is.InRange(0, 0.41001f));
                    Assert.That(shake, Is.InRange(-3.00001f, 3.00001f));
                    Assert.That(SuitReplacementMotion.Lift(t + 0.0001f, slot, 3), Is.EqualTo(lift).Within(0.001));
                    Assert.That(SuitReplacementMotion.Shake(t + 0.0001f, slot, 3), Is.EqualTo(shake).Within(0.005));
                    Assert.That(SuitReplacementMotion.Lift(t, slot, 3), Is.EqualTo(lift));
                }
            }
            Assert.That(SuitReplacementMotion.Lift(1, 0, 3), Is.Not.EqualTo(SuitReplacementMotion.Lift(1, 1, 3)));
            Assert.That(SuitReplacementMotion.Lift(1, 0, 3), Is.EqualTo(SuitReplacementMotion.Lift(2.4f, 0, 3)).Within(0.00001));
            Assert.That(SuitReplacementMotion.Shake(1, 1, 3), Is.EqualTo(SuitReplacementMotion.Shake(1.4f, 1, 3)).Within(0.00001));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)]
        public void WanderingLanesKeepEveryBodySeparateDuringPop(int count)
        {
            for (int i = 0; i + 1 < count; i++)
                Assert.That(SuitReplacementMotion.WanderX(i + 1, count, -1) - SuitReplacementMotion.WanderX(i, count, 1),
                    Is.GreaterThan(0.7f / 0.78f));
        }

        [Test]
        public void LowerAndPopHaveContinuousEndpoints()
        {
            Assert.That(SuitReplacementMotion.Lower(0), Is.EqualTo(1));
            Assert.That(SuitReplacementMotion.Lower(0.2f), Is.Zero);
            Assert.That(SuitReplacementMotion.Pop(0), Is.EqualTo(1));
            Assert.That(SuitReplacementMotion.Pop(0.28f), Is.EqualTo(1).Within(0.00001));
            for (int i = 0; i < 1000; i++)
            {
                float t = i * 0.001f;
                Assert.That(SuitReplacementMotion.Pop(t), Is.InRange(0.78f, 1.22f));
                Assert.That(SuitReplacementMotion.Lower(t + 0.0001f), Is.EqualTo(SuitReplacementMotion.Lower(t)).Within(0.002));
            }
        }

        [Test]
        public void Reduced_RemovesShakeAndBobButKeepsTheLift()
        {
            for (int slot = 0; slot < 3; slot++)
                for (int sample = 0; sample < 3000; sample++)
                {
                    float t = sample * 0.001f;
                    Assert.That(SuitReplacementMotion.Shake(t, slot, 3, true), Is.Zero);
                    float lift = SuitReplacementMotion.Lift(t, slot, 3, true);
                    Assert.That(lift, Is.InRange(0, 0.35001f));
                    if (t >= SuitReplacementMotion.RiseSeconds) Assert.That(lift, Is.EqualTo(0.35f).Within(0.0001f));
                }
        }
    }
}
