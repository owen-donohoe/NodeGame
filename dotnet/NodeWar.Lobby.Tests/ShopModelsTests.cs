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

    }
}

