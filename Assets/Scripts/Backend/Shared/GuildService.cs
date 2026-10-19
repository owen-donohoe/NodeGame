using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NodeWar.Backend
{
    public sealed class GuildSummary
    {
        public string GuildId;
        public string Name;
        public string IconId;
        public int Members;
        public int Capacity = 25;
        public bool Joinable;
        public int MinRR;
    }

    public sealed class GuildFilter
    {
        public bool JoinableOnly;
        public int MinRR;
        public int MinMembers;
    }

    public enum GuildPostKind { Announcement, Achievement, Invite, InBattle }

    public sealed class GuildPost
    {
        public string PostId;
        public GuildPostKind Kind;
        public string Text;
        public PlayerCard Player;
        public long CreatedUnix;
    }

    public sealed class GuildView
    {
        public GuildSummary Summary;
        public int WeeklyProgress;
        public int WeeklyGoal;
        public List<GuildPost> Pinned = new List<GuildPost>();
        public List<GuildPost> Feed = new List<GuildPost>();
        public int OnlineCount;
    }

    public sealed class ChatMessage
    {
        public string MessageId;
        public PlayerCard Sender;
        public string Text;
        public long CreatedUnix;
    }

    public interface IGuildService
    {
        Task<GuildView> GetMineAsync();
        Task<List<GuildSummary>> SearchAsync(string query, GuildFilter filter);
        Task<List<GuildSummary>> BrowseAsync();
        Task<List<ChatMessage>> GetChatAsync();
        Task SendChatAsync(string text);
    }

    public sealed class LocalGuildService : IGuildService
    {
        private readonly Func<PlayerCard> sender;
        private readonly List<ChatMessage> chat = new List<ChatMessage>
        {
            new ChatMessage { MessageId = "sample-chat-1", Sender = LocalSocialService.Sample("sample-brook"), Text = "Welcome to the Grove! 🌿", CreatedUnix = 1792281600 },
            new ChatMessage { MessageId = "sample-chat-2", Sender = LocalSocialService.Sample("sample-ash"), Text = "Ready for a match?", CreatedUnix = 1792281601 }
        };
        public bool InGuild { get; set; }

        public LocalGuildService(Func<PlayerCard> sender = null) { this.sender = sender; }

        public Task<GuildView> GetMineAsync()
        {
            if (!InGuild) return Task.FromResult<GuildView>(null);
            return Task.FromResult(new GuildView
            {
                Summary = Samples()[0], WeeklyProgress = 7, WeeklyGoal = 20, OnlineCount = 3,
                Pinned = new List<GuildPost> { Post("pin", GuildPostKind.Announcement, "Welcome! Weekly goal: play together.", null) },
                Feed = new List<GuildPost>
                {
                    Post("achievement", GuildPostKind.Achievement, "Brook reached a new era.", LocalSocialService.Sample("sample-brook")),
                    Post("invite", GuildPostKind.Invite, "Join a friendly match.", LocalSocialService.Sample("sample-brook")),
                    Post("battle", GuildPostKind.InBattle, "Ash is in battle.", LocalSocialService.Sample("sample-ash"))
                }
            });
        }

        public Task<List<GuildSummary>> SearchAsync(string query, GuildFilter filter)
        {
            var results = new List<GuildSummary>();
            string text = (query ?? "").Trim();
            foreach (var guild in Samples())
                if (guild.Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 && GuildFilters.Matches(guild, filter)) results.Add(guild);
            return Task.FromResult(results);
        }

        public Task<List<GuildSummary>> BrowseAsync() => Task.FromResult(Samples());

        public Task<List<ChatMessage>> GetChatAsync()
        {
            RequireMembership();
            var result = new List<ChatMessage>();
            foreach (var message in chat)
                result.Add(new ChatMessage { MessageId = message.MessageId, Sender = message.Sender,
                    Text = message.Text, CreatedUnix = message.CreatedUnix });
            return Task.FromResult(result);
        }

        public Task SendChatAsync(string text)
        {
            RequireMembership();
            if (!GuildChatText.TryNormalize(text, out string normalized)) throw new ArgumentException("Enter a message.", nameof(text));
            chat.Add(new ChatMessage { MessageId = "local-chat-" + (chat.Count + 1), Sender = sender?.Invoke() ?? LocalSocialService.Sample("sample-brook"),
                Text = normalized, CreatedUnix = 1792281600 + chat.Count });
            return Task.CompletedTask;
        }

        private void RequireMembership() { if (!InGuild) throw new InvalidOperationException("You're not in a guild."); }
        private static GuildPost Post(string id, GuildPostKind kind, string text, PlayerCard player) =>
            new GuildPost { PostId = id, Kind = kind, Text = text, Player = player, CreatedUnix = 1792281600 };

        private static List<GuildSummary> Samples() => new List<GuildSummary>
        {
            new GuildSummary { GuildId = "sample-grove", Name = "The Grove", IconId = "grove", Members = 18, Joinable = true },
            new GuildSummary { GuildId = "sample-spire", Name = "Spire", IconId = "spire", Members = 25, MinRR = 700, Joinable = false },
            new GuildSummary { GuildId = "sample-tide", Name = "Tide", IconId = "tide", Members = 8, MinRR = 300, Joinable = true }
        };
    }
}
