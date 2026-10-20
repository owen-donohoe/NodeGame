using System;
using System.Collections.Generic;
using NodeWar.Backend;

namespace NodeWar.Lobby
{
    public static class PlayerPanelPolicy
    {
        public static GuildMember ModerationTarget(GuildView guild, string playerId, string selfId)
        {
            if (guild?.Summary == null || !GuildMembers.IsOfficer(guild.OwnRole) || string.IsNullOrEmpty(playerId) || playerId == selfId) return null;
            return guild.Members?.Find(member => member?.Player != null && member.Player.PlayerId == playerId);
        }
        public static List<MatchSummary> LastTen(List<MatchSummary> summaries)
        {
            var result = summaries == null ? new List<MatchSummary>() : summaries.FindAll(row => row != null);
            result.Sort((a, b) => { int order = b.CreatedUnix.CompareTo(a.CreatedUnix); return order != 0 ? order : StringComparer.Ordinal.Compare(a.MatchId, b.MatchId); });
            if (result.Count > 10) result.RemoveRange(10, result.Count - 10);
            return result;
        }
    }
}
