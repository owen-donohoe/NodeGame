using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class RememberGuardTests
    {
        [Test]
        public void SameAccountThroughoutIsRemembered()
        {
            Assert.That(RememberGuard.ShouldRemember("player-1", "player-1"), Is.True);
        }

        [Test]
        public void AccountChangedMidFlightIsDiscarded()
        {
            Assert.That(RememberGuard.ShouldRemember("player-1", "player-2"), Is.False);
        }

        [Test]
        public void NullRequestedForIsDiscarded()
        {
            Assert.That(RememberGuard.ShouldRemember(null, "player-1"), Is.False);
        }

        [Test]
        public void EmptyRequestedForIsDiscarded()
        {
            Assert.That(RememberGuard.ShouldRemember("", "player-1"), Is.False);
        }

        [Test]
        public void NullCurrentPlayerIsDiscarded()
        {
            Assert.That(RememberGuard.ShouldRemember("player-1", null), Is.False);
        }

        [Test]
        public void EmptyCurrentPlayerIsDiscarded()
        {
            Assert.That(RememberGuard.ShouldRemember("player-1", ""), Is.False);
        }

        [Test]
        public void BothNullIsDiscarded()
        {
            Assert.That(RememberGuard.ShouldRemember(null, null), Is.False);
        }
    }
}
