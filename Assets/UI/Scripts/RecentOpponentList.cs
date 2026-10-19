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
    }
}
