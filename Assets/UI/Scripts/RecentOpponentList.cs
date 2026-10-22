using System;
using System.Collections.Generic;
using NodeWar.Backend;

namespace NodeWar.Lobby
{
    public static class RecentOpponentList
    {
        public static string[] From(IEnumerable<MatchHistoryEntry> history)
        {
            var opponents = new List<string>();
            if (history == null) return opponents.ToArray();
            foreach (var entry in history)
            {
                string id = entry?.opponentPlayerId;
                if (string.IsNullOrWhiteSpace(id) || opponents.Exists(item => string.Equals(item, id, StringComparison.Ordinal))) continue;
                opponents.Add(id);
                if (opponents.Count == 20) break;
            }
            return opponents.ToArray();
        }

        public static string[] HandlesFrom(IEnumerable<MatchHistoryEntry> history)
        {
            var entries = history == null ? new List<MatchHistoryEntry>() : new List<MatchHistoryEntry>(history);
            var result = new List<string>();
            foreach (string id in From(entries))
            {
                var entry = entries.Find(item => item?.opponentPlayerId == id);
                result.Add(HandleDisplay.Display(entry.OpponentName, entry.OpponentTag, id));
            }
            return result.ToArray();
        }
    }
}
