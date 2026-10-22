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
        public string Description;
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

    public enum GuildPostKind { Announcement, Achievement, Invite, InBattle, ChatAnnouncement, DistrictUnlocked, SuitUnlocked, TournamentWon, EraReached }
    public enum GuildRole { Member, CoLeader, Leader }

    public sealed class GuildMember
    {
        public PlayerCard Player;
        public GuildRole Role;
        public long JoinedUnix;
        public bool Online;
        public int RR;
    }

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
        public string WeeklyReward;
        public GuildRole OwnRole;
        public List<GuildMember> Members = new List<GuildMember>();
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
        Task<GuildView> GetAsync(string guildId);
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
            new ChatMessage { MessageId = "sample-chat-1", Sender = LocalSocialService.Sample("sample-brook"), Text = "Welcome to the Grove! \U0001F33F", CreatedUnix = 1792281600 },
            new ChatMessage { MessageId = "sample-chat-2", Sender = LocalSocialService.Sample("sample-ash"), Text = "Ready for a match?", CreatedUnix = 1792281601 }
        };
        public bool InGuild { get; set; }

        public LocalGuildService(Func<PlayerCard> sender = null) { this.sender = sender; }

        public Task<GuildView> GetMineAsync()
        {
            if (!InGuild) return Task.FromResult<GuildView>(null);
            return GetAsync("sample-grove");
        }

        public Task<GuildView> GetAsync(string guildId)
        {
            var summary = Samples().Find(guild => guild.GuildId == guildId);
            if (summary == null) return Task.FromResult<GuildView>(null);
            var members = new List<GuildMember>();
            for (int i = 0; i < summary.Members; i++)
            {
                string[] ids = { "sample-brook", "sample-ash", "sample-cedar", "sample-dune" };
                var player = LocalSocialService.Sample(ids[i % ids.Length]);
                if (i >= ids.Length) player = new PlayerCard { PlayerId = guildId + "-member-" + i, Name = "Sailor " + (i + 1), Tag = "SEA", RR = 300 - i * 7 };
                members.Add(new GuildMember { Player = player, Role = i == 0 ? GuildRole.Leader : i == 1 ? GuildRole.CoLeader : GuildRole.Member,
                    JoinedUnix = 1792200000 + i * 3600, Online = i < 3, RR = player.RR });
            }
            return Task.FromResult(new GuildView
            {
                Summary = summary, WeeklyProgress = 7, WeeklyGoal = 20, WeeklyReward = "a guild reward crate",
                OwnRole = GuildRole.CoLeader, Members = members, OnlineCount = 3,
                Pinned = new List<GuildPost> { Post("pin", GuildPostKind.Announcement, "Welcome! Play together to fill our weekly crate.", null) },
                Feed = new List<GuildPost>
                {
                    Post("chat", GuildPostKind.ChatAnnouncement, "Brook: Tournament practice tonight!", LocalSocialService.Sample("sample-brook")),
                    Post("district", GuildPostKind.DistrictUnlocked, "Ash unlocked a new district.", LocalSocialService.Sample("sample-ash")),
                    Post("suit", GuildPostKind.SuitUnlocked, "Cedar unlocked a new suit.", LocalSocialService.Sample("sample-cedar")),
                    Post("tournament", GuildPostKind.TournamentWon, "Brook won a tournament!", LocalSocialService.Sample("sample-brook")),
                    Post("era", GuildPostKind.EraReached, "Ash reached a new era.", LocalSocialService.Sample("sample-ash"))
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
            new GuildSummary { GuildId = "sample-grove", Name = "The Grove", IconId = "grove", Description = "Grow together. Friendly matches, weekly rewards and a place for every captain.", Members = 18, Joinable = true },
            new GuildSummary { GuildId = "sample-spire", Name = "Spire", IconId = "spire", Description = "Climb higher with us. A competitive crew for the next era.", Members = 25, MinRR = 700, Joinable = false },
            new GuildSummary { GuildId = "sample-tide", Name = "Tide", IconId = "tide", Description = "Follow the tide. New captains welcome; sail and learn together.", Members = 8, MinRR = 300, Joinable = true }
        };
    }
}
