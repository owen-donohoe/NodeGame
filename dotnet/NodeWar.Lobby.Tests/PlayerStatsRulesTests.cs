using System.Collections.Generic;
using System.Linq;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class PlayerStatsRulesTests
    {
        [Test]
        public void WinsLossesStreakAndBestRR()
        {
            var stats = new PlayerStats();
            PlayerStatsRules.Apply(stats, true, 20);
            PlayerStatsRules.Apply(stats, true, 40);
            Assert.That(stats.Streak, Is.EqualTo(2));
            PlayerStatsRules.Apply(stats, false, 30);
            Assert.That(new[] { stats.Matches, stats.Wins, stats.Losses, stats.Streak, stats.BestRR },
                Is.EqualTo(new[] { 3, 2, 1, 0, 40 }));
            PlayerStatsRules.Apply(stats, true, 35);
            Assert.That(stats.Streak, Is.EqualTo(1));
            Assert.That(stats.BestRR, Is.EqualTo(40));
        }

        [Test]
        public void SummariesFollowRetainedIdsNewestFirst()
        {
            var history = new HistoryRecord { MatchIds = new List<string> { "old" } };
            for (int i = 0; i < 25; i++)
                HistorySummaryRules.Prepend(history, new HistorySummary { MatchId = i.ToString() });
            Assert.That(history.MatchIds, Is.EqualTo(Enumerable.Range(5, 20).Reverse().Select(i => i.ToString())));
            Assert.That(history.Summaries.Select(s => s.MatchId), Is.EqualTo(history.MatchIds));
        }
    }
}
