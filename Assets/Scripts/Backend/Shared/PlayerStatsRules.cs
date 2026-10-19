using System;
using System.Collections.Generic;

namespace NodeWar.Backend
{
    public static class PlayerStatsRules
    {
        public static void Apply(PlayerStats stats, bool won, int rrAfter)
        {
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            stats.Matches++;
            if (won) { stats.Wins++; stats.Streak++; }
            else { stats.Losses++; stats.Streak = 0; }
            stats.BestRR = Math.Max(stats.BestRR, rrAfter);
        }
    }

    public static class HistorySummaryRules
    {
        public const int Limit = 20;

        // Old history IDs may have no summary. Never invent an outcome for them.
        public static void Prepend(HistoryRecord history, HistorySummary summary)
        {
            if (history.MatchIds == null) history.MatchIds = new List<string>();
            if (history.Summaries == null) history.Summaries = new List<HistorySummary>();
            history.MatchIds.RemoveAll(id => id == summary.MatchId);
            history.MatchIds.Insert(0, summary.MatchId);
            if (history.MatchIds.Count > Limit)
                history.MatchIds.RemoveRange(Limit, history.MatchIds.Count - Limit);
            history.Summaries.RemoveAll(item => item.MatchId == summary.MatchId || !history.MatchIds.Contains(item.MatchId));
            history.Summaries.Insert(0, summary);
            history.Summaries.Sort((a, b) => history.MatchIds.IndexOf(a.MatchId).CompareTo(history.MatchIds.IndexOf(b.MatchId)));
        }
    }
}
