using NodeWar.Lobby;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class SegmentedSwitchMathTests
    {
        [TestCase(4, 98, 212, 4, 98)]
        [TestCase(106, 102, 212, 106, 102)]
        [TestCase(144, 66, 214, 144, 66)]
        [TestCase(-2, 90, 200, 0, 90)]
        [TestCase(190, 90, 200, 110, 90)]
        public void TargetUsesMeasuredFacesAndClampsToTrack(float left, float size, float track, float x, float width)
        {
            var target = SegmentedSwitchMath.ThumbTarget(left, size, track);
            Assert.That(target.X, Is.EqualTo(x)); Assert.That(target.Width, Is.EqualTo(width));
        }

        [Test]
        public void CriticalSpringRetargetsWithoutJumpAndSettlesInQuarterSecond()
        {
            var spring = new LobbyMotion.Spring(4) { Target = 104 };
            spring.Step(0.1f, 1f);
            float position = spring.Position, velocity = spring.Velocity;
            spring.Target = 4;
            Assert.That(spring.Position, Is.EqualTo(position)); Assert.That(spring.Velocity, Is.EqualTo(velocity));
            spring.Step(0.25f, 1f);
            Assert.That(spring.Position, Is.EqualTo(4).Within(1));
            Assert.That(float.IsNaN(spring.Position), Is.False);
        }
    }
}
