using NodeWar.Backend;
using NodeWar.Lobby;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    /// <summary>
    /// MatchHistoryRow is the UnityEngine-free formatting MatchHistoryPage
    /// lays into Labels: title per state, RR/arena detail only when settled,
    /// and the relative-time buckets at their boundaries.
    /// </summary>
    public class MatchHistoryRowTests
    {
        private static MatchHistoryEntry Entry(
            MatchRecordState state,
            long createdUnixSeconds = 1_000_000,
            bool? won = null,
            int? rrDelta = null,
            int? arenaAfter = null)
        {
            return new MatchHistoryEntry
            {
                matchId = "m1",
                opponentPlayerId = "opponent",
                state = state,
                createdUnixSeconds = createdUnixSeconds,
                won = won,
                rrDelta = rrDelta,
                arenaAfter = arenaAfter
            };
        }

        // ---- title per state ----

        [Test]
        public void Settled_Won_TitleIsVictory()
        {
            var row = new MatchHistoryRow(Entry(MatchRecordState.Settled, won: true), 1_000_000);
            Assert.AreEqual("Victory", row.Title);
        }

        [Test]
        public void Settled_Lost_TitleIsDefeat()
        {
            var row = new MatchHistoryRow(Entry(MatchRecordState.Settled, won: false), 1_000_000);
            Assert.AreEqual("Defeat", row.Title);
        }

        [Test]
        public void Pending_TitleIsAwaitingResult()
        {
            var row = new MatchHistoryRow(Entry(MatchRecordState.Pending), 1_000_000);
            Assert.AreEqual("Awaiting result", row.Title);
        }

        [Test]
        public void Open_TitleIsAwaitingResult()
        {
            var row = new MatchHistoryRow(Entry(MatchRecordState.Open), 1_000_000);
            Assert.AreEqual("Awaiting result", row.Title);
        }

        [Test]
        public void Void_TitleIsVoid()
        {
            var row = new MatchHistoryRow(Entry(MatchRecordState.Void), 1_000_000);
            Assert.AreEqual("Void", row.Title);
        }

        [Test]
        public void Disputed_TitleIsDisputed()
        {
            var row = new MatchHistoryRow(Entry(MatchRecordState.Disputed), 1_000_000);
            Assert.AreEqual("Disputed", row.Title);
        }

        // ---- detail: sign formatting, arena name, only when settled ----

        [Test]
        public void Settled_PositiveDelta_DetailUsesPlusSign()
        {
            var row = new MatchHistoryRow(
                Entry(MatchRecordState.Settled, won: true, rrDelta: 18, arenaAfter: 1), 1_000_000);
            Assert.AreEqual("+18 RR · Arena 2", row.Detail);
        }

        [Test]
        public void Settled_NegativeDelta_DetailUsesRealMinusSign()
        {
            var row = new MatchHistoryRow(
                Entry(MatchRecordState.Settled, won: false, rrDelta: -12, arenaAfter: 0), 1_000_000);
            Assert.AreEqual("−12 RR · Arena 1", row.Detail);
            StringAssert.DoesNotContain("-12", row.Detail);
        }

        [Test]
        public void Pending_DetailIsEmpty()
        {
            var row = new MatchHistoryRow(Entry(MatchRecordState.Pending), 1_000_000);
            Assert.AreEqual("", row.Detail);
        }

        [Test]
        public void Open_DetailIsEmpty()
        {
            var row = new MatchHistoryRow(Entry(MatchRecordState.Open), 1_000_000);
            Assert.AreEqual("", row.Detail);
        }

        [Test]
        public void Void_DetailIsEmpty()
        {
            var row = new MatchHistoryRow(Entry(MatchRecordState.Void), 1_000_000);
            Assert.AreEqual("", row.Detail);
        }

        [Test]
        public void Disputed_DetailIsEmpty()
        {
            var row = new MatchHistoryRow(Entry(MatchRecordState.Disputed), 1_000_000);
            Assert.AreEqual("", row.Detail);
        }

        // ---- relative time buckets, at their boundaries ----

        [Test]
        public void JustUnder60Seconds_IsJustNow()
        {
            var row = new MatchHistoryRow(Entry(MatchRecordState.Void, createdUnixSeconds: 1000), 1000 + 59);
            Assert.AreEqual("just now", row.RelativeTime);
        }

        [Test]
        public void Exactly60Seconds_IsOneMinuteAgo()
        {
            var row = new MatchHistoryRow(Entry(MatchRecordState.Void, createdUnixSeconds: 1000), 1000 + 60);
            Assert.AreEqual("1 min ago", row.RelativeTime);
        }

        [Test]
        public void FiveMinutes_IsFiveMinAgo()
        {
            var row = new MatchHistoryRow(Entry(MatchRecordState.Void, createdUnixSeconds: 1000), 1000 + 300);
            Assert.AreEqual("5 min ago", row.RelativeTime);
        }

        [Test]
        public void JustUnder1Hour_IsMinutesAgo()
        {
            var row = new MatchHistoryRow(Entry(MatchRecordState.Void, createdUnixSeconds: 1000), 1000 + 3599);
            Assert.AreEqual("59 min ago", row.RelativeTime);
        }

        [Test]
        public void Exactly1Hour_IsOneHourAgo()
        {
            var row = new MatchHistoryRow(Entry(MatchRecordState.Void, createdUnixSeconds: 1000), 1000 + 3600);
            Assert.AreEqual("1 h ago", row.RelativeTime);
        }

        [Test]
        public void ThreeHours_IsThreeHAgo()
        {
            var row = new MatchHistoryRow(Entry(MatchRecordState.Void, createdUnixSeconds: 1000), 1000 + 3 * 3600);
            Assert.AreEqual("3 h ago", row.RelativeTime);
        }

        [Test]
        public void JustUnder1Day_IsHoursAgo()
        {
            var row = new MatchHistoryRow(Entry(MatchRecordState.Void, createdUnixSeconds: 1000), 1000 + 86399);
            Assert.AreEqual("23 h ago", row.RelativeTime);
        }

        [Test]
        public void Exactly1Day_IsOneDayAgo()
        {
            var row = new MatchHistoryRow(Entry(MatchRecordState.Void, createdUnixSeconds: 1000), 1000 + 86400);
            Assert.AreEqual("1 d ago", row.RelativeTime);
        }

        [Test]
        public void TwoDays_IsTwoDAgo()
        {
            var row = new MatchHistoryRow(Entry(MatchRecordState.Void, createdUnixSeconds: 1000), 1000 + 2 * 86400);
            Assert.AreEqual("2 d ago", row.RelativeTime);
        }

        // ---- fixed tag ----

        [Test]
        public void ReplayTag_IsAlwaysReplayComing()
        {
            var row = new MatchHistoryRow(Entry(MatchRecordState.Settled, won: true), 1_000_000);
            Assert.AreEqual("Replay coming", row.ReplayTag);
        }
    }
}
