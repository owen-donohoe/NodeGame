using NodeWar.Core;
using NUnit.Framework;

namespace NodeWar.View.Tests
{
    public class EmoteRateLimiterTests
    {
        [Test]
        public void OneSecondWindow_AllowsFifthAndRefusesSixth()
        {
            var limiter = new EmoteRateLimiter();
            for (int i = 0; i < 5; i++) Assert.IsTrue(limiter.TryAccept(0));
            Assert.IsFalse(limiter.TryAccept(0.999));
            Assert.IsTrue(limiter.TryAccept(1));
        }

        [Test]
        public void FiveSecondWindow_AllowsTenthAndRefusesEleventh()
        {
            var limiter = new EmoteRateLimiter();
            for (int i = 0; i < 10; i++) Assert.IsTrue(limiter.TryAccept(i * 0.4));
            Assert.IsFalse(limiter.TryAccept(4.9));
            Assert.IsTrue(limiter.TryAccept(5));
        }

        [Test]
        public void ExpiredWindows_RestoreFullBurst()
        {
            var limiter = new EmoteRateLimiter();
            for (int i = 0; i < 5; i++) limiter.TryAccept(0);
            for (int i = 0; i < 5; i++) limiter.TryAccept(1);
            Assert.IsFalse(limiter.TryAccept(2));
            for (int i = 0; i < 5; i++) Assert.IsTrue(limiter.TryAccept(6));
            Assert.IsFalse(limiter.TryAccept(6));
        }

        [Test]
        public void NextAllowedTime_UsesLaterOfBothWindows()
        {
            var limiter = new EmoteRateLimiter();
            Assert.AreEqual(0, limiter.NextAllowedTime(0));
            for (int i = 0; i < 5; i++) limiter.TryAccept(0);
            Assert.AreEqual(1, limiter.NextAllowedTime(0.5));
            for (int i = 0; i < 5; i++) limiter.TryAccept(1);
            Assert.AreEqual(5, limiter.NextAllowedTime(1));
            Assert.IsTrue(limiter.TryAccept(5));
            Assert.AreEqual(5, limiter.NextAllowedTime(5));
        }

        [Test]
        public void Refusals_DoNotExtendCooldown()
        {
            var limiter = new EmoteRateLimiter();
            for (int i = 0; i < 5; i++) limiter.TryAccept(10);
            Assert.IsFalse(limiter.TryAccept(10.5));
            Assert.IsFalse(limiter.TryAccept(10.9));
            Assert.AreEqual(11, limiter.NextAllowedTime(10.9));
            Assert.IsTrue(limiter.TryAccept(11));
        }
    }
}
