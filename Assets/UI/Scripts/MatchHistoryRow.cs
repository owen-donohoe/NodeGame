using NodeWar.Backend;

namespace NodeWar.Lobby
{
    /// <summary>
    /// One row's display text for the match history page, derived from a
    /// server entry and "now". UnityEngine-free so it is testable without the
    /// page: MatchHistoryPage only lays these strings into Labels.
    /// </summary>
    public readonly struct MatchHistoryRow
    {
        public string Title { get; }
        public string Detail { get; }
        public string RelativeTime { get; }
        public string ReplayTag => "Replay coming";

        public MatchHistoryRow(MatchHistoryEntry entry, long nowUnixSeconds)
        {
            Title = TitleFor(entry);
            Detail = DetailFor(entry);
            RelativeTime = RelativeTimeFor(entry.createdUnixSeconds, nowUnixSeconds);
        }

        private static string TitleFor(MatchHistoryEntry entry)
        {
            switch (entry.state)
            {
                case MatchRecordState.Settled:
                    return entry.won == true ? "Victory" : "Defeat";
                case MatchRecordState.Void:
                    return "Void";
                case MatchRecordState.Disputed:
                    return "Disputed";
                case MatchRecordState.Open:
                case MatchRecordState.Pending:
                default:
                    return "Awaiting result";
            }
        }

        private static string DetailFor(MatchHistoryEntry entry)
        {
            if (entry.state != MatchRecordState.Settled) return "";
            if (!entry.rrDelta.HasValue || !entry.arenaAfter.HasValue) return "";

            int delta = entry.rrDelta.Value;
            string sign = delta < 0 ? "−" : "+";
            int magnitude = delta < 0 ? -delta : delta;
            string arenaName = RankTable.Names[entry.arenaAfter.Value];
            return $"{sign}{magnitude} RR · {arenaName}";
        }

        private static string RelativeTimeFor(long thenUnixSeconds, long nowUnixSeconds)
        {
            long diff = nowUnixSeconds - thenUnixSeconds;
            if (diff < 0) diff = 0;

            if (diff < 60) return "just now";
            if (diff < 3600) return $"{diff / 60} min ago";
            if (diff < 86400) return $"{diff / 3600} h ago";
            return $"{diff / 86400} d ago";
        }
    }
}
