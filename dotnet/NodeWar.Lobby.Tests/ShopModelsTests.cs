using System;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class ShopModelsTests
    {
        [TestCase(StallKind.Daily)]
        [TestCase(StallKind.Crates)]
        [TestCase(StallKind.Coins)]
        [TestCase(StallKind.Laurels)]
        public void DailyResets_AtNextLocalMidnight(StallKind kind)
        {
            var now = new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Local);
            var next = ShopResets.Next(now, kind);
            Assert.AreEqual(new DateTime(2027, 1, 1), next);
            Assert.AreEqual(DateTimeKind.Local, next.Kind);
            Assert.AreEqual(now.Date.AddDays(1), ShopResets.Next(now.Date, kind));
        }

        [TestCase(18, 19)] // Sunday.
        [TestCase(19, 26)] // Monday midnight is a new period.
        [TestCase(20, 26)]
        public void WeeklyResets_AtNextMonday(int day, int expected)
            => Assert.AreEqual(new DateTime(2026, 10, expected), ShopResets.Next(new DateTime(2026, 10, day), StallKind.Weekly));

        [Test]
        public void Bundle_UsesSuppliedStart_NotRefreshTime()
        {
            var start = new DateTime(2026, 10, 18, 14, 30, 0);
            Assert.AreEqual(start.AddDays(2), ShopResets.Next(start.AddHours(8), StallKind.Bundle, start));
            Assert.AreEqual(start.AddDays(2), ShopResets.Next(start, StallKind.Bundle));
        }

        [TestCase(-1, "0s")]
        [TestCase(0, "0s")]
        [TestCase(45, "45s")]
        [TestCase(60, "1m 0s")]
        [TestCase(3599, "59m 59s")]
        [TestCase(3600, "1h 0m")]
        [TestCase(18720, "5h 12m")]
        [TestCase(183600, "2d 3h")]
        public void Countdown_FormatsPassedTime(int seconds, string expected)
            => Assert.AreEqual(expected, ShopCountdown.Format(TimeSpan.FromSeconds(seconds)));

        [TestCase(120, 50, null)]
        [TestCase(120, 40, null)]
        [TestCase(120, -40, null)]
        [TestCase(120, 10, 200f)]
        [TestCase(120, -10, 100f)]
        [TestCase(120, 0, 100f)]
        [TestCase(100, 10, 100f)]
        [TestCase(-20, -10, 0f)]
        [TestCase(300, 10, 200f)]
        public void Snap_WaitsForSlowVelocity_ThenFollowsDirection(float y, float velocity, float? expected)
            => Assert.AreEqual(expected, ShopSnap.Target(y, velocity, new[] { 0f, 100f, 200f }));

        [Test]
        public void Snap_EmptyOrInvalidInput_HasNoTarget()
        {
            Assert.IsNull(ShopSnap.Target(0, 0, null));
            Assert.IsNull(ShopSnap.Target(0, 0, Array.Empty<float>()));
            Assert.IsNull(ShopSnap.Target(float.NaN, 0, new[] { 0f }));
            Assert.IsNull(ShopSnap.Target(0, float.NaN, new[] { 0f }));
        }

        [TestCase(100, 1f)]
        [TestCase(150, 1f)]
        [TestCase(200, 1f)]
        [TestCase(250, 0.8f)]
        [TestCase(300, 0.6f)]
        [TestCase(50, 0.8f)]
        [TestCase(-100, 0.6f)]
        public void Shrink_StaysFullOnStall_AndEasesAway(float y, float expected)
            => Assert.AreEqual(expected, ShopSnap.Shrink(y, 100, 100), 0.0001f);

        [Test]
        public void Shrink_ZeroHeight_IsSafe() => Assert.AreEqual(0.6f, ShopSnap.Shrink(0, 0, 0));
    }
}
