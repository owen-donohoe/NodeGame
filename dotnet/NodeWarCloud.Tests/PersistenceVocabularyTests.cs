using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodeWar.Backend;
using NodeWar.Progression;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    /// <summary>
    /// What stored data and catalog IDs must keep through a domain rename of the
    /// simulation's types: the keys already on disk and in Cloud Save, and every
    /// ID the committed catalog has ever carried.
    /// </summary>
    public class PersistenceVocabularyTests
    {
        // Every base the catalog has, spelled out. Neither the enum member names
        // nor anything derived from them is allowed to change what is stored.
        internal static readonly string[] Bases =
        {
            "suit.farmer", "suit.miner", "suit.warrior", "suit.smelter", "suit.guardian", "suit.scout",
            "suit.berserker", "suit.medic", "suit.merchant", "suit.acolyte", "suit.watcher",
            "district.farm", "district.mine", "district.village", "district.barracks", "district.forge",
            "district.pier", "district.town", "district.infirmary", "district.fortress", "district.storehouse"
        };

        private const string LegacyInventory = @"{
            ""OwnedVariants"": [""suit.warrior.e0"", ""suit.warrior.e1"", ""suit.warrior.e2"", ""district.rampart.e0"", ""district.rampart.e1""],
            ""OwnedSkins"": [""skin.suit.warrior.default"", ""skin.suit.warrior.gilded"", ""skin.district.rampart.default""],
            ""EquippedSuitIDs"": [""suit_warrior"", ""suit_guardian""],
            ""EquippedNodeIDs"": [""node_rampart"", ""node_market""],
            ""Equipped"": {
                ""Variants"": { ""suit.warrior"": ""suit.warrior.e1"", ""district.rampart"": ""district.rampart.e1"" },
                ""Skins"": { ""suit.warrior"": ""skin.suit.warrior.gilded"", ""district.rampart"": ""skin.district.rampart.default"" }
            }
        }";

        private sealed class RecordingStore : IPlayerRecordStore
        {
            private readonly InMemoryPlayerRecordStore inner = new InMemoryPlayerRecordStore();
            public readonly List<PlayerState> Writes = new List<PlayerState>();
            public Task<PlayerState> ReadAsync() => inner.ReadAsync();
            public Task WriteAsync(PlayerState written) { Writes.Add(written); return inner.WriteAsync(written); }
        }

        [Test]
        public async Task OldInventory_RoundTripPreservesEveryKey()
        {
            // The stored shape survives a read and a write with every key and value.
            InventoryRecord inventory = JsonConvert.DeserializeObject<InventoryRecord>(LegacyInventory);
            Assert.That(JObject.DeepEquals(JObject.Parse(LegacyInventory), JObject.FromObject(inventory)), Is.True,
                JsonConvert.SerializeObject(inventory));

            // An equip through the local service rewrites only the inventory, and
            // leaves the obsolete EquippedNodeIDs and the other equipped items as stored.
            var store = new RecordingStore();
            await store.WriteAsync(new PlayerState
            {
                Rating = new RatingRecord { R = 1500, Rd = 200, Sigma = 0.06 },
                Rank = new RankRecord { RR = 40, Arena = 2, HighestArena = 2 },
                History = new HistoryRecord { MatchIds = new List<string> { "m1" } },
                Inventory = inventory
            });
            store.Writes.Clear();

            var service = new LocalInventoryService(store, Bases);
            PlayerState result = await service.EquipAsync(new EquippedRecord
            {
                Variants = new Dictionary<string, string> { ["suit.warrior"] = "suit.warrior.e2" }
            });

            Assert.That(store.Writes, Has.Count.EqualTo(1));
            PlayerState written = store.Writes[0];
            Assert.That(written.Inventory, Is.Not.Null);
            Assert.That(written.Rating, Is.Null);
            Assert.That(written.Rank, Is.Null);
            Assert.That(written.History, Is.Null);
            Assert.That(written.Discipline, Is.Null);

            JObject expected = JObject.Parse(LegacyInventory);
            expected["Equipped"]["Variants"]["suit.warrior"] = "suit.warrior.e2";
            var expectedState = new PlayerState { Inventory = expected.ToObject<InventoryRecord>(), Rank = result.Rank };
            DistrictMigration.Apply(expectedState);
            Assert.That(JObject.DeepEquals(JObject.FromObject(expectedState.Inventory), JObject.FromObject(written.Inventory)), Is.True,
                JsonConvert.SerializeObject(written.Inventory));
            Assert.That(result.Inventory.EquippedNodeIDs, Is.EqualTo(new[] { "node_fortress", "node_storehouse" }));
        }

        [Test]
        public void CatalogBases_DoNotChangeOnDomainRename()
        {
            // The explicit list is what the enum names give today...
            CollectionAssert.AreEqual(Bases, CatalogTests.ExpectedBases().ToArray());

            // ...and exactly what the committed catalog has: no base, variant or skin gained or lost.
            var expected = Bases.SelectMany(b => Enumerable.Range(0, CatalogIds.EraCount)
                .Select(e => CatalogIds.Variant(b, e)).Append(CatalogIds.DefaultSkin(b))).ToList();
            string json = File.ReadAllText(Path.Combine(RepositoryRoot(), CatalogTests.CatalogPath));
            var committed = ServerCatalog.Parse(json);
            Assert.That(committed.Where(i => !i.Retired).Select(i => i.Id), Is.EquivalentTo(expected));
            Assert.That(committed.Where(i => !i.Retired).Select(i => i.BaseId).Distinct(), Is.EquivalentTo(Bases));
            Assert.That(CatalogValidation.ValidateAgainstPrevious(committed, ServerCatalog.Items), Is.Empty);
            Assert.That(CatalogValidation.ValidateAgainstPrevious(ServerCatalog.Items, committed), Is.Empty);
        }

        [Test]
        public void KeyTables_AreExplicitAndMatchTheCatalog()
        {
            // The tables answer by number. Core (5) has a lobby key but no catalog base.
            Assert.That(Enumerable.Range(1, CatalogKeys.SuitTableLength - 1).Select(CatalogKeys.SuitBase),
                Is.EqualTo(Bases.Where(b => b.StartsWith("suit.")).ToArray()));
            Assert.That(CatalogKeys.CatalogDistrictTypes.Select(CatalogKeys.DistrictBase),
                Is.EqualTo(Bases.Where(b => b.StartsWith("district.")).ToArray()));
            Assert.That(CatalogKeys.DistrictBase(5), Is.EqualTo("district.core"));
            Assert.That(CatalogKeys.CatalogDistrictTypes, Does.Not.Contain(5));
            Assert.That(CatalogKeys.SuitBase(0), Is.Null);
            Assert.That(CatalogKeys.DistrictBase(0), Is.Null);
            Assert.That(CatalogKeys.SuitBase(CatalogKeys.SuitTableLength), Is.Null);
            Assert.That(CatalogKeys.DistrictBase(-1), Is.Null);
            Assert.That(CatalogKeys.SuitLobbyId(3), Is.EqualTo("suit_warrior"));
            Assert.That(CatalogKeys.DistrictLobbyId(12), Is.EqualTo("node_rampart"));
            Assert.That(CatalogKeys.DistrictLobbyId(14), Is.EqualTo("node_pier"));
        }

        private static string RepositoryRoot()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, CatalogTests.CatalogPath))) return dir.FullName;
            throw new FileNotFoundException("Repository root not found.");
        }
    }
}
