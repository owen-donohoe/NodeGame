using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class GuildTests
    {
        [TestCase(false, 0, 0, true)]
        [TestCase(true, 300, 8, true)]
        [TestCase(true, 301, 0, false)]
        [TestCase(false, 0, 9, false)]
        [TestCase(false, -1, -1, true)]
        public void Filters_ApplyMinimumsAndJoinability(bool joinable, int rr, int members, bool expected)
            => Assert.AreEqual(expected, GuildFilters.Matches(new GuildSummary { Joinable = true, Members = 8, MinRR = 300 },
                new GuildFilter { JoinableOnly = joinable, MinRR = rr, MinMembers = members }));

        [Test]
        public void FullClosedAndMissingGuilds_AreNotJoinable()
        {
            var filter = new GuildFilter { JoinableOnly = true };
            Assert.IsFalse(GuildFilters.Matches(null, filter));
            Assert.IsFalse(GuildFilters.Matches(new GuildSummary { Joinable = false }, filter));
            Assert.IsFalse(GuildFilters.Matches(new GuildSummary { Joinable = true, Members = 25 }, filter));
            Assert.IsTrue(GuildFilters.Matches(new GuildSummary(), null));
        }

        [TestCase(null, false, "")]
        [TestCase("   ", false, "")]
        [TestCase("  hello 🌿  ", true, "hello 🌿")]
        [TestCase("<b>plain</b>", true, "<b>plain</b>")]
        public void Chat_IsTrimmedPlainText_AndEmptyIsRefused(string text, bool accepted, string expected)
        {
            Assert.AreEqual(accepted, GuildChatText.TryNormalize(text, out string result));
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void Chat_ClampsWithoutSplittingEmoji()
        {
            Assert.IsTrue(GuildChatText.TryNormalize(new string('a', 210), out string text));
            Assert.AreEqual(200, text.Length);
            GuildChatText.TryNormalize(new string('a', 199) + "🌿", out text);
            Assert.AreEqual(199, text.Length);
            GuildChatText.TryNormalize(new string('a', 198) + "🌿", out text);
            Assert.AreEqual(200, text.Length);
        }

        [Test]
        public async Task Browse_LoadsOnceIncludingEmpty_AndRefreshesOnlyOnDemand()
        {
            var service = new StubGuild();
            var cache = new GuildBrowseCache();
            await cache.GetAsync(service); await cache.GetAsync(service);
            Assert.IsTrue(cache.HasValue);
            Assert.AreEqual(1, service.Calls);
            await cache.GetAsync(service, true);
            Assert.AreEqual(2, service.Calls);
            await new GuildBrowseCache().GetAsync(service); // New app session.
            Assert.AreEqual(3, service.Calls);
        }

        [Test]
        public async Task Browse_CoalescesRequests_AndRetainsLastSuccessOnFailure()
        {
            var service = new StubGuild { Pending = new TaskCompletionSource<List<GuildSummary>>() };
            var cache = new GuildBrowseCache();
            var first = cache.GetAsync(service);
            var second = cache.GetAsync(service);
            Assert.AreEqual(1, service.Calls);
            service.Pending.SetResult(new List<GuildSummary> { new GuildSummary { Name = "Saved" } });
            await first; await second;
            service.Fail = true; service.Pending = null;
            Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync(service, true));
            Assert.AreEqual("Saved", (await cache.GetAsync(service))[0].Name);
        }

        [Test]
        public async Task Fake_MembershipSearchPostsAndSending()
        {
            var service = new LocalGuildService();
            Assert.IsNull(await service.GetMineAsync());
            Assert.ThrowsAsync<InvalidOperationException>(() => service.SendChatAsync("hi"));
            Assert.AreEqual(1, (await service.SearchAsync(" tIdE ", new GuildFilter { JoinableOnly = true })).Count);
            service.InGuild = true;
            var mine = await service.GetMineAsync();
            Assert.AreEqual(25, mine.Summary.Capacity);
            Assert.AreEqual(GuildPostKind.Announcement, mine.Pinned[0].Kind);
            int before = (await service.GetChatAsync()).Count;
            await service.SendChatAsync("  hi 🌿  ");
            var chat = await service.GetChatAsync();
            Assert.AreEqual(before + 1, chat.Count);
            Assert.AreEqual("hi 🌿", chat[chat.Count - 1].Text);
            Assert.ThrowsAsync<ArgumentException>(() => service.SendChatAsync(" "));
        }

        [TestCase(GuildMemberOrder.OnlineByRR, "c,a,b")]
        [TestCase(GuildMemberOrder.Seniority, "a,b,c")]
        [TestCase(GuildMemberOrder.AllByRR, "b,c,a")]
        public void Members_SortCopyWithStableIdTies(GuildMemberOrder order, string expected)
        {
            var members = new List<GuildMember>
            {
                new GuildMember { Player = new PlayerCard { PlayerId = "c" }, Online = true, RR = 200, JoinedUnix = 3 },
                new GuildMember { Player = new PlayerCard { PlayerId = "b" }, Online = false, RR = 200, JoinedUnix = 1 },
                new GuildMember { Player = new PlayerCard { PlayerId = "a" }, Online = true, RR = 100, JoinedUnix = 1 }
            };
            Assert.AreEqual(expected, string.Join(",", GuildMembers.Sorted(members, order).ConvertAll(m => m.Player.PlayerId)));
            Assert.AreEqual("c", members[0].Player.PlayerId);
            members.Reverse();
            Assert.AreEqual(expected, string.Join(",", GuildMembers.Sorted(members, order).ConvertAll(m => m.Player.PlayerId)));
        }

        [Test]
        public async Task Details_IncludeRosterRolesAndAnnouncementKinds()
        {
            var service = new LocalGuildService { InGuild = true };
            var guild = await service.GetMineAsync();
            Assert.IsNotEmpty(guild.Summary.Description);
            Assert.IsNotEmpty(guild.WeeklyReward);
            Assert.AreEqual(guild.Summary.Members, guild.Members.Count);
            Assert.AreEqual(guild.OnlineCount, guild.Members.FindAll(m => m.Online).Count);
            Assert.IsTrue(GuildMembers.IsOfficer(guild.OwnRole));
            Assert.IsFalse(GuildMembers.IsOfficer(GuildRole.Member));
            Assert.AreEqual(GuildRole.Leader, guild.Members[0].Role);
            Assert.AreEqual(GuildRole.CoLeader, guild.Members[1].Role);
            Assert.AreEqual(GuildRole.Member, guild.Members[2].Role);
            Assert.AreEqual(5, guild.Feed.Count);
            Assert.IsNull(await service.GetAsync("missing"));
            Assert.AreEqual("Tide", (await service.GetAsync("sample-tide")).Summary.Name);
            Assert.IsEmpty(GuildMembers.Sorted(null, GuildMemberOrder.OnlineByRR));
        }
        private sealed class StubGuild : IGuildService
        {
            public int Calls;
            public bool Fail;
            public TaskCompletionSource<List<GuildSummary>> Pending;
            public Task<List<GuildSummary>> BrowseAsync()
            {
                Calls++;
                if (Fail) throw new InvalidOperationException();
                return Pending?.Task ?? Task.FromResult(new List<GuildSummary>());
            }
            public Task<GuildView> GetAsync(string guildId) => Task.FromResult<GuildView>(null);
            public Task<GuildView> GetMineAsync() => Task.FromResult<GuildView>(null);
            public Task<List<GuildSummary>> SearchAsync(string query, GuildFilter filter) => BrowseAsync();
            public Task<List<ChatMessage>> GetChatAsync() => Task.FromResult(new List<ChatMessage>());
            public Task SendChatAsync(string text) => Task.CompletedTask;
        }
    }
}
