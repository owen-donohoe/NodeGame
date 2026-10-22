using System;
using System.Threading.Tasks;
using NodeWar.Backend;
using NodeWar.Lobby;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class HistoryOpponentNamesTests
    {
        private sealed class Profiles : IProfileService
        {
            public int Calls;
            public readonly TaskCompletionSource<PublicProfileView> Result = new TaskCompletionSource<PublicProfileView>();
            public Task<PublicProfileView> GetAsync(string id) { Calls++; return Result.Task; }
        }

        [Test]
        public async Task MissingHistoryNamesResolveThroughOneCachedLookup()
        {
            var profiles = new Profiles(); var names = new HistoryOpponentNames(profiles);
            var entry = new MatchHistoryEntry { opponentPlayerId = "account-id" };
            var first = names.ResolveAsync(entry); var second = names.ResolveAsync(entry);
            Assert.That(profiles.Calls, Is.EqualTo(1));
            profiles.Result.SetResult(new PublicProfileView { Card = new PlayerCard { Name = "Ash", Tag = "ASH" } });
            Assert.That(await first, Is.EqualTo("Ash#ASH")); Assert.That(await second, Is.EqualTo("Ash#ASH"));
            Assert.That(await names.ResolveAsync(entry), Is.EqualTo("Ash#ASH")); Assert.That(profiles.Calls, Is.EqualTo(1));
        }

        [Test]
        public async Task FailedLookupNeverExposesAccountId()
        {
            var profiles = new Profiles(); var names = new HistoryOpponentNames(profiles);
            profiles.Result.SetException(new InvalidOperationException("offline"));
            Assert.That(await names.ResolveAsync(new MatchHistoryEntry { opponentPlayerId = "secret-id" }), Is.EqualTo("Unknown"));
        }

        [Test]
        public async Task LocalProfileIdFallbackIsNotAHandle()
        {
            var profiles = new Profiles(); var names = new HistoryOpponentNames(profiles);
            profiles.Result.SetResult(new PublicProfileView { Card = new PlayerCard { Name = "account-id", Tag = "" } });
            Assert.That(await names.ResolveAsync(new MatchHistoryEntry { opponentPlayerId = "account-id" }), Is.EqualTo("Unknown"));
        }

        [Test]
        public async Task StoredHandleDoesNotNeedLookup()
        {
            var profiles = new Profiles(); var names = new HistoryOpponentNames(profiles);
            Assert.That(await names.ResolveAsync(new MatchHistoryEntry { opponentPlayerId = "id", OpponentName = "Dune", OpponentTag = "DNE" }), Is.EqualTo("Dune#DNE"));
            Assert.That(profiles.Calls, Is.Zero);
        }
    }
}
