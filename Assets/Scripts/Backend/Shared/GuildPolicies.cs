using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NodeWar.Backend
{
    public enum GuildMemberOrder { OnlineByRR, Seniority, AllByRR }

    public static class GuildMembers
    {
        public static bool IsOfficer(GuildRole role) => role == GuildRole.Leader || role == GuildRole.CoLeader;

        /// <summary>Sort a copy; ties use the stable player ID, never response order.</summary>
        public static List<GuildMember> Sorted(List<GuildMember> members, GuildMemberOrder order)
        {
            var result = members == null ? new List<GuildMember>() : members.FindAll(member => member?.Player != null);
            result.Sort((a, b) =>
            {
                int comparison = 0;
                if (order == GuildMemberOrder.OnlineByRR) comparison = b.Online.CompareTo(a.Online);
                if (comparison == 0) comparison = order == GuildMemberOrder.Seniority
                    ? a.JoinedUnix.CompareTo(b.JoinedUnix) : b.RR.CompareTo(a.RR);
                return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(a.Player.PlayerId, b.Player.PlayerId);
            });
            return result;
        }
    }

    public static class GuildFilters
    {
        public static bool Matches(GuildSummary guild, GuildFilter filter) => guild != null && (filter == null ||
            ((!filter.JoinableOnly || guild.Joinable && guild.Members < guild.Capacity) &&
             guild.MinRR >= Math.Max(0, filter.MinRR) && guild.Members >= Math.Max(0, filter.MinMembers)));
    }

    /// <summary>Public results live for the app session. Empty results count as loaded; failures do not.</summary>
    public sealed class GuildBrowseCache
    {
        public bool HasValue { get; private set; }
        public List<GuildSummary> Value { get; private set; }
        private Task<List<GuildSummary>> pending;

        public async Task<List<GuildSummary>> GetAsync(IGuildService service, bool refresh = false)
        {
            if (pending != null) return await pending;
            if (HasValue && !refresh) return Value;
            pending = service.BrowseAsync();
            try
            {
                Value = await pending ?? new List<GuildSummary>();
                HasValue = true;
                return Value;
            }
            finally { pending = null; }
        }
    }

    public static class GuildChatText
    {
        public const int MaxLength = 200;
        public static bool TryNormalize(string text, out string normalized)
        {
            normalized = (text ?? "").Trim();
            if (normalized.Length > MaxLength)
            {
                int length = char.IsHighSurrogate(normalized[MaxLength - 1]) && char.IsLowSurrogate(normalized[MaxLength]) ? MaxLength - 1 : MaxLength;
                normalized = normalized.Substring(0, length).TrimEnd();
            }
            return normalized.Length > 0;
        }
    }
}
