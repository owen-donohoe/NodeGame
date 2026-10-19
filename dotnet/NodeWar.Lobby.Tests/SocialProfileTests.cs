using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class SocialProfileTests
    {
        [TestCase(0, 0, "0%")]
        [TestCase(0, 1, "0%")]
        [TestCase(3, 1, "33%")]
        [TestCase(3, 2, "67%")]
        [TestCase(8, 1, "13%")]
        [TestCase(200, 1, "1%")]
        [TestCase(10, -1, "0%")]
        [TestCase(10, 20, "100%")]
        [TestCase(int.MaxValue, int.MaxValue, "100%")]
        public void WinRate_RoundsHalfUp_AndHandlesInvalidCounts(int matches, int wins, string expected)
            => Assert.AreEqual(expected, ProfileStatsFormat.WinRate(new PlayerStats { Matches = matches, Wins = wins }));

        [Test]
        public void Stats_UnknownIsNotZero_AndGridUsesTheRequestedOrder()
        {
            Assert.AreEqual("—", ProfileStatsFormat.WinRate(null));
            CollectionAssert.AreEqual(new[] { "—", "—", "—", "—", "—", "—" }, ProfileStatsFormat.Values(null));
            CollectionAssert.AreEqual(new[] { "10", "6", "4", "60%", "2", "5" },
                ProfileStatsFormat.Values(new PlayerStats { Matches = 10, Wins = 6, Losses = 4, Streak = 2, Best = 99, BestRR = 5 }));
        }

        [Test]
        public void Friends_SortPresence_ThenHandle_ThenId()
        {
            var cards = new List<PlayerCard>
            {
                Card("offline", "A", "A", PlayerPresence.Offline), Card("online", "Z", "A", PlayerPresence.Online),
                Card("z", "Ash", "TAG", PlayerPresence.InBattle), Card("a", "ash", "tag", PlayerPresence.InBattle),
                Card("tag", "Ash", "AAA", PlayerPresence.InBattle), Card("name", "Aaron", "Z", PlayerPresence.InBattle)
            };
            cards.Sort(FriendPresenceOrder.Instance);
            CollectionAssert.AreEqual(new[] { "name", "tag", "a", "z", "online", "offline" }, cards.ConvertAll(card => card.PlayerId));
            Assert.Greater(FriendPresenceOrder.Instance.Compare(null, cards[0]), 0);
            Assert.AreEqual(0, FriendPresenceOrder.Instance.Compare(cards[0], cards[0]));
        }

        [Test]
        public async Task SocialSamples_AreDeterministic_AndIndependent()
        {
            var social = new LocalSocialService();
            var friends = await social.GetFriendsAsync();
            CollectionAssert.AreEquivalent(new[] { PlayerPresence.InBattle, PlayerPresence.Online, PlayerPresence.Offline },
                friends.ConvertAll(card => card.Presence));
            friends[0].Name = "Changed";
            Assert.AreEqual("Ash", (await social.GetFriendsAsync())[0].Name);
            var live = await social.GetLiveMatchesAsync();
            Assert.AreEqual("sample-live-1", live[0].MatchId);
            Assert.AreEqual(PlayerPresence.InBattle, live[0].A.Presence);
            Assert.AreEqual(PlayerPresence.InBattle, live[0].B.Presence);
        }

        [Test]
        public async Task Self_UsesAcceptedStateAndCurrentSlots_WithUnknownStatsAndRealHistory()
        {
            var history = new LocalMatchHistoryService { Entries = new List<MatchHistoryEntry>
            {
                new MatchHistoryEntry { matchId = "real", opponentPlayerId = "sample-ash", state = MatchRecordState.Settled,
                    won = true, rrDelta = 15, arenaAfter = 2, createdUnixSeconds = 123 },
                new MatchHistoryEntry { matchId = "pending", state = MatchRecordState.Pending }
            } };
            var state = new PlayerState { Profile = new ProfileRecord { Name = "Me", Tag = "SELF" }, Rank = new RankRecord { RR = 500, Arena = 2 } };
            string[] slots = { "suit_guardian" };
            var service = new LocalProfileService(() => "me", () => state, history, () => slots, () => new[] { "district_farm" });
            var profile = await service.GetAsync("me");
            Assert.AreEqual("Me", profile.Card.Name);
            Assert.AreEqual("SELF", profile.Card.Tag);
            Assert.AreEqual(500, profile.Card.RR);
            Assert.AreEqual(2, profile.Card.Arena);
            Assert.IsNull(profile.Stats);
            slots[0] = "changed";
            Assert.AreEqual("suit_guardian", profile.SuitIds[0]);
            Assert.AreEqual(1, profile.Recent.Count);
            Assert.AreEqual("real", profile.Recent[0].MatchId);
            Assert.AreEqual("Ash", profile.Recent[0].Opponent.Name);
            Assert.AreEqual(123, profile.Recent[0].CreatedUnix);
        }

        [Test]
        public async Task PublicSamples_AndUnknownPlayers_DoNotReadSelfHistory()
        {
            var service = new LocalProfileService(() => "me", () => null, new ThrowingHistory());
            Assert.IsNotNull((await service.GetAsync("sample-brook")).Stats);
            var unknown = await service.GetAsync("someone-else");
            Assert.AreEqual("someone-else", unknown.Card.PlayerId);
            Assert.IsNull(unknown.Stats);
            Assert.IsEmpty(unknown.Recent);
        }

        [Test]
        public async Task MissingPlayerId_WhileSignedOut_DoesNotReadSelfHistory()
        {
            var service = new LocalProfileService(() => null, () => null, new ThrowingHistory());
            var unknown = await service.GetAsync(null);
            Assert.IsNull(unknown.Stats);
            Assert.IsEmpty(unknown.Recent);
        }

        [Test]
        public void SelfHistory_FromAReplacedAccount_IsRejected()
        {
            string account = "old";
            var history = new DelayedHistory();
            var service = new LocalProfileService(() => account, () => null, history);
            Task<PublicProfileView> pending = service.GetAsync("old");
            account = "new";
            history.Completion.SetResult(new List<MatchHistoryEntry>());
            Assert.ThrowsAsync<InvalidOperationException>(async () => await pending);
        }

        private static PlayerCard Card(string id, string name, string tag, PlayerPresence presence)
            => new PlayerCard { PlayerId = id, Name = name, Tag = tag, Presence = presence };

        private sealed class ThrowingHistory : IMatchHistoryService
        {
            public Task<List<MatchHistoryEntry>> GetAsync() => throw new Exception("Must not fetch self history");
        }

        private sealed class DelayedHistory : IMatchHistoryService
        {
            public readonly TaskCompletionSource<List<MatchHistoryEntry>> Completion = new TaskCompletionSource<List<MatchHistoryEntry>>();
            public Task<List<MatchHistoryEntry>> GetAsync() => Completion.Task;
        }
    }
}
