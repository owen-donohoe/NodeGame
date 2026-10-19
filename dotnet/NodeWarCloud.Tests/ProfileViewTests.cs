using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class ProfileViewTests
    {
        private sealed class Store : IProfileViewStore
        {
            public PlayerState State;
            public int Reads;
            public Task<PlayerState> ReadProfileViewAsync() { Reads++; return Task.FromResult(State); }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void RejectsUnauthenticatedBeforeReading(string caller)
        {
            Assert.ThrowsAsync<UnauthorizedAccessException>(() => ProfileViews.GetAsync(caller, "target",
                _ => throw new Exception("Must not read")));
        }

        [TestCase("", "caller")]
        [TestCase(null, "caller")]
        [TestCase("other", "other")]
        public async Task AuthenticatedCallerCanReadSelfOrOtherPublicFieldsOnly(string target, string expected)
        {
            var state = PlayerStateLogic.ApplyDefaults(new PlayerState());
            state.Profile = new ProfileRecord { Name = "Opponent", Tag = "TAG", NameNorm = "private" };
            state.Rank = new RankRecord { RR = 800, Arena = 2 };
            state.Inventory.Equipped = new EquippedRecord { Variants = new Dictionary<string, string>
                { ["suit.guardian"] = "suit.guardian.e0", ["district.farm"] = "district.farm.e0" } };
            state.History.Stats = new PlayerStats { Matches = 1, Wins = 1, Streak = 1, BestRR = 800 };
            HistorySummaryRules.Prepend(state.History, new HistorySummary { MatchId = "match", OpponentId = "rival",
                OpponentName = "Rival", OpponentTag = "R", Won = true, RRDelta = 20, Arena = 2, CreatedUnix = 123 });
            var store = new Store { State = state };
            string readTarget = null;
            var view = await ProfileViews.GetAsync("caller", target, id => { readTarget = id; return store; });
            Assert.That(readTarget, Is.EqualTo(expected));
            Assert.That(store.Reads, Is.EqualTo(1));
            Assert.That(view.Card.PlayerId, Is.EqualTo(expected));
            Assert.That(view.Card.Name, Is.EqualTo("Opponent"));
            Assert.That(view.Card.Tag, Is.EqualTo("TAG"));
            Assert.That(view.Card.RR, Is.EqualTo(800));
            Assert.That(view.Card.Arena, Is.EqualTo(2));
            Assert.That(view.Card.Presence, Is.EqualTo(PlayerPresence.Offline));
            Assert.That(view.Stats.BestRR, Is.EqualTo(800));
            Assert.That(view.SuitIds, Is.EqualTo(new[] { "suit_guardian" }));
            Assert.That(view.DistrictIds, Is.EqualTo(new[] { "node_farm" }));
            Assert.That(view.Recent[0].Opponent.Name, Is.EqualTo("Rival"));
            var json = JObject.Parse(JsonConvert.SerializeObject(view));
            foreach (var property in json.Descendants())
                if (property is JProperty field)
                    Assert.That(field.Name.ToLowerInvariant(), Is.Not.EqualTo("rating").And.Not.EqualTo("r")
                        .And.Not.EqualTo("rd").And.Not.EqualTo("sigma").And.Not.EqualTo("wallet")
                        .And.Not.EqualTo("discipline").And.Not.EqualTo("activematch").And.Not.EqualTo("namenorm"));
        }

        [Test]
        public void OldHistoryDeserializesWithZeroStatsAndEmptySummaries()
        {
            var record = JsonConvert.DeserializeObject<HistoryRecord>("{\"MatchIds\":[\"old\"]}");
            Assert.That(record.MatchIds, Is.EqualTo(new[] { "old" }));
            Assert.That(new[] { record.Stats.Matches, record.Stats.Wins, record.Stats.Losses,
                record.Stats.Streak, record.Stats.BestRR }, Is.All.Zero);
            Assert.That(record.Summaries, Is.Empty);
        }

        [Test]
        public async Task MissingPublicRecordsReturnEmptyProfile()
        {
            var view = await ProfileViews.GetAsync("caller", "missing", _ => new Store { State = new PlayerState() });
            Assert.That(view.Card.Name, Is.Empty);
            Assert.That(view.Stats.Matches, Is.Zero);
            Assert.That(view.Recent, Is.Empty);
            Assert.That(view.SuitIds, Is.Empty);
        }
    }
}
