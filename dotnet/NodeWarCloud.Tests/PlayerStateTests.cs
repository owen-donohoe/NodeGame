using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodeWar.Backend;
using NodeWar.Progression;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class PlayerStateTests
    {
        [Test]
        public async Task FirstCall_CreatesEveryRecord_InOneWrite()
        {
            var store = new InMemoryPlayerRecordStore();

            PlayerState state = await PlayerStateLogic.GetOrCreateAsync(store);

            Assert.That(store.WriteCount, Is.EqualTo(1));
            Assert.That(state.Rating.R, Is.EqualTo(1500));
            Assert.That(state.Rank.RR, Is.EqualTo(0));
            Assert.That(state.Inventory.OwnedVariants, Is.Empty);
            Assert.That(state.History.MatchIds, Is.Empty);
        }

        [Test]
        public async Task LaterCalls_WriteNothing()
        {
            var store = new InMemoryPlayerRecordStore();
            await PlayerStateLogic.GetOrCreateAsync(store);

            await PlayerStateLogic.GetOrCreateAsync(store);

            Assert.That(store.WriteCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ExistingRecords_AreKept_AndOnlyMissingOnesWritten()
        {
            var store = new InMemoryPlayerRecordStore();
            await store.WriteAsync(new PlayerState { Rank = new RankRecord { RR = 250, Arena = 1, HighestArena = 1 } });

            PlayerState state = await PlayerStateLogic.GetOrCreateAsync(store);
            PlayerState stored = await store.ReadAsync();

            Assert.That(state.Rank.RR, Is.EqualTo(250));
            Assert.That(stored.Rank.RR, Is.EqualTo(250));
            Assert.That(stored.Rating, Is.Not.Null);
            Assert.That(stored.History, Is.Not.Null);
        }

        [Test]
        public async Task LocalFake_KeepsStateBetweenCalls()
        {
            var service = new LocalPlayerStateService();

            PlayerState first = await service.GetAsync();
            PlayerState second = await service.GetAsync();

            Assert.That(second.Rating, Is.SameAs(first.Rating));
        }

        [Test]
        public void DefaultRating_MatchesGlicko2Defaults()
        {
            var glicko = new Rating();
            RatingRecord record = PlayerStateDefaults.Rating();

            Assert.That(record.R, Is.EqualTo(glicko.R));
            Assert.That(record.Rd, Is.EqualTo(glicko.RD));
            Assert.That(record.Sigma, Is.EqualTo(glicko.Sigma));
        }

        // The server serializes its return value and the client deserializes it
        // with Newtonsoft on both ends. Field names are the stored data's names.
        [Test]
        public async Task State_RoundTripsThroughJson_WithStableFieldNames()
        {
            PlayerState state = await PlayerStateLogic.GetOrCreateAsync(new InMemoryPlayerRecordStore());
            state.Rank.RR = 42;
            state.History.MatchIds.Add("m1");

            string json = JsonConvert.SerializeObject(state);
            PlayerState back = JsonConvert.DeserializeObject<PlayerState>(json);

            Assert.That(back.Rank.RR, Is.EqualTo(42));
            Assert.That(back.History.MatchIds, Is.EqualTo(new List<string> { "m1" }));
            Assert.That(back.Rating.Sigma, Is.EqualTo(0.06));

            JObject tree = JObject.Parse(json);
            Assert.That(tree["Rating"]?["R"], Is.Not.Null);
            Assert.That(tree["Rating"]?["Rd"], Is.Not.Null);
            Assert.That(tree["Rating"]?["LastMatchUnixSeconds"], Is.Not.Null);
            Assert.That(tree["Rank"]?["HighestArena"], Is.Not.Null);
            Assert.That(tree["Inventory"]?["EquippedSuitIDs"], Is.Not.Null);
        }

        [Test]
        public async Task DisciplineDefaultsPersistAndStayOutsideSettlementKeys()
        {
            var store = new InMemoryPlayerRecordStore();
            var state = await PlayerStateLogic.GetOrCreateAsync(store);
            Assert.That(state.Discipline.Level, Is.Zero);
            Assert.That(state.Discipline.NonReports, Is.Empty);
            Assert.That(state.Discipline.StruckMatchIds, Is.Empty);
            Assert.That((await store.ReadAsync()).Discipline, Is.SameAs(state.Discipline));
            Assert.That(PlayerStateKeys.Discipline, Is.EqualTo("discipline"));
            Assert.That(PlayerStateKeys.All, Does.Not.Contain(PlayerStateKeys.Discipline));
        }

        [Test]
        public async Task ExistingDisciplineSurvivesDefaultsAndJsonRoundTrip()
        {
            var store = new InMemoryPlayerRecordStore();
            await store.WriteAsync(new PlayerState { Discipline = new DisciplineRecord
            {
                Level = 4, LastStrikeUnixSeconds = 100, LastDecayUnixSeconds = 90,
                BlockedUntilUnixSeconds = 220, NonReports = new List<long> { 80 },
                StruckMatchIds = new List<string> { "m" }
            } });
            var state = await PlayerStateLogic.GetOrCreateAsync(store);
            var back = JsonConvert.DeserializeObject<PlayerState>(JsonConvert.SerializeObject(state)).Discipline;
            Assert.That(back.Level, Is.EqualTo(4));
            Assert.That(back.LastStrikeUnixSeconds, Is.EqualTo(100));
            Assert.That(back.LastDecayUnixSeconds, Is.EqualTo(90));
            Assert.That(back.BlockedUntilUnixSeconds, Is.EqualTo(220));
            Assert.That(back.NonReports, Is.EqualTo(new long[] { 80 }));
            Assert.That(back.StruckMatchIds, Is.EqualTo(new[] { "m" }));
        }

        [Test]
        public void Keys_AreStable()
        {
            Assert.That(PlayerStateKeys.All, Is.EqualTo(new[] { "rating", "rank", "inventory", "history" }));
        }
    }
}
