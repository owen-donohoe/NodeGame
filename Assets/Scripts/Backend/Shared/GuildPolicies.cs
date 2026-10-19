using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NodeWar.Backend
{
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
