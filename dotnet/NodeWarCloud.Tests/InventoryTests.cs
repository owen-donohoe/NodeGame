using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NodeWar.Backend;
using NodeWar.Progression;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class InventoryTests
    {
        private sealed class RecordingStore : IPlayerRecordStore
        {
            private readonly InMemoryPlayerRecordStore inner = new InMemoryPlayerRecordStore();
            public readonly List<PlayerState> Writes = new List<PlayerState>();
            public Task<PlayerState> ReadAsync() => inner.ReadAsync();
            public Task WriteAsync(PlayerState records) { Writes.Add(records); return inner.WriteAsync(records); }
        }

        private static EquippedRecord Variant(string baseId, string id) => new EquippedRecord
        { Variants = new Dictionary<string, string> { { baseId, id } } };

        private static InventoryPlayerStateService Server(IPlayerRecordStore store, IReadOnlyList<CatalogItem> catalog = null) =>
            new InventoryPlayerStateService(store, new InventoryRules(catalog ?? ServerCatalog.Items));

        [TestCase(false)]
        [TestCase(true)]
        public async Task NewPlayer_GrantsAndEquipsEveryStarter_InOneWrite_ThenIsIdempotent(bool local)
        {
            var store = new RecordingStore();
            var fake = new LocalInventoryService(store, CatalogTests.ExpectedBases());
            IPlayerStateService service = local ? new LocalPlayerStateService(store, fake.GrantDefaults) : Server(store);
            var state = await service.GetAsync();
            var bases = CatalogTests.ExpectedBases().ToList();
            Assert.That(state.Inventory.OwnedVariants, Is.EquivalentTo(bases.Select(b => CatalogIds.Variant(b, 0))));
            Assert.That(state.Inventory.OwnedSkins, Is.EquivalentTo(bases.Select(CatalogIds.DefaultSkin)));
            Assert.That(state.Inventory.Equipped.Variants, Has.Count.EqualTo(bases.Count));
            Assert.That(state.Inventory.Equipped.Skins, Has.Count.EqualTo(bases.Count));
            foreach (string baseId in bases)
            {
                Assert.That(state.Inventory.Equipped.Variants[baseId], Is.EqualTo(CatalogIds.Variant(baseId, 0)));
                Assert.That(state.Inventory.Equipped.Skins[baseId], Is.EqualTo(CatalogIds.DefaultSkin(baseId)));
            }
            await service.GetAsync();
            Assert.That(store.Writes, Has.Count.EqualTo(1));
            Assert.That(store.Writes[0].Rating, Is.Not.Null);
            Assert.That(store.Writes[0].Rank, Is.Not.Null);
            Assert.That(store.Writes[0].History, Is.Not.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task HighestArena_GrantsAllReachedEras_KeepsEquippedAndOwnershipOnDemotion(bool local)
        {
            var store = new RecordingStore();
            var fake = new LocalInventoryService(store, CatalogTests.ExpectedBases());
            IPlayerStateService service = local ? new LocalPlayerStateService(store, fake.GrantDefaults) : Server(store);
            var state = await service.GetAsync();
            state.Rank.HighestArena = 3;
            state.Rank.Arena = 1;
            await service.GetAsync();
            Assert.That(state.Inventory.OwnedVariants, Has.Count.EqualTo(23 * 4));
            Assert.That(state.Inventory.OwnedVariants.Distinct().Count(), Is.EqualTo(23 * 4));
            Assert.That(state.Inventory.Equipped.Variants.Values, Has.All.EndsWith(".e0"));
            Assert.That(store.Writes, Has.Count.EqualTo(2));
            AssertInventoryOnly(store.Writes[1]);
            state.Rank.Arena = 0;
            await service.GetAsync();
            Assert.That(state.Inventory.OwnedVariants, Has.Count.EqualTo(23 * 4));
            Assert.That(store.Writes, Has.Count.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task Equip_PartialUpdate_PersistsToSameState_AndNoOpDoesNotWrite(bool local)
        {
            var store = new RecordingStore();
            var fake = new LocalInventoryService(store, CatalogTests.ExpectedBases());
            IPlayerStateService states = local ? new LocalPlayerStateService(store, fake.GrantDefaults) : Server(store);
            IInventoryService inventory = local ? fake : Server(store);
            var state = await states.GetAsync();
            state.Rank.Arena = state.Rank.HighestArena = 2;
            await states.GetAsync();
            var equipped = await inventory.EquipAsync(Variant("suit.warrior", "suit.warrior.e2"));
            Assert.That(equipped.Inventory.Equipped.Variants["suit.warrior"], Is.EqualTo("suit.warrior.e2"));
            Assert.That(equipped.Inventory.Equipped.Variants["suit.miner"], Is.EqualTo("suit.miner.e0"));
            Assert.That(equipped.Inventory.Equipped.Skins, Has.Count.EqualTo(23));
            AssertInventoryOnly(store.Writes.Last());
            int writes = store.Writes.Count;
            await inventory.EquipAsync(Variant("suit.warrior", "suit.warrior.e2"));
            await inventory.EquipAsync(new EquippedRecord());
            var reloaded = await states.GetAsync();
            Assert.That(reloaded.Inventory.Equipped.Variants["suit.warrior"], Is.EqualTo("suit.warrior.e2"));
            Assert.That(store.Writes, Has.Count.EqualTo(writes));
        }

        [Test]
        public async Task OwnedCosmeticSkin_EquipsWithoutArenaRestriction_AndKeepsOtherEntries()
        {
            var catalog = ServerCatalog.Parse(JsonConvert.SerializeObject(ServerCatalog.Items));
            catalog.Add(new CatalogItem { Id = "skin.suit.warrior.gold", BaseId = "suit.warrior", Kind = CatalogItemKind.Skin });
            var store = new RecordingStore();
            var service = Server(store, catalog);
            var state = await service.GetAsync();
            Assert.That(state.Inventory.OwnedSkins, Does.Not.Contain("skin.suit.warrior.gold"));
            state.Inventory.OwnedSkins.Add("skin.suit.warrior.gold");
            var result = await service.EquipAsync(new EquippedRecord
            { Skins = new Dictionary<string, string> { { "suit.warrior", "skin.suit.warrior.gold" } } });
            Assert.That(result.Inventory.Equipped.Skins["suit.warrior"], Is.EqualTo("skin.suit.warrior.gold"));
            Assert.That(result.Inventory.Equipped.Variants["suit.warrior"], Is.EqualTo("suit.warrior.e0"));
            Assert.That(result.Inventory.Equipped.Skins["suit.miner"], Is.EqualTo("skin.suit.miner.default"));
            await service.GetAsync();
            Assert.That(result.Inventory.Equipped.Skins["suit.warrior"], Is.EqualTo("skin.suit.warrior.gold"));
        }

        [TestCase("unowned", false)]
        [TestCase("wrong base", false)]
        [TestCase("locked", false)]
        [TestCase("unknown", false)]
        [TestCase("retired", false)]
        [TestCase("unknown base", false)]
        [TestCase("wrong kind", false)]
        [TestCase("null item", false)]
        [TestCase("null changes", false)]
        [TestCase("skin unowned", false)]
        [TestCase("skin wrong base", false)]
        [TestCase("skin unknown", false)]
        [TestCase("skin retired", false)]
        [TestCase("skin wrong kind", false)]
        [TestCase("unowned", true)]
        [TestCase("wrong base", true)]
        [TestCase("locked", true)]
        [TestCase("unknown", true)]
        [TestCase("unknown base", true)]
        [TestCase("wrong kind", true)]
        [TestCase("null item", true)]
        [TestCase("null changes", true)]
        [TestCase("skin unowned", true)]
        [TestCase("skin wrong base", true)]
        [TestCase("skin unknown", true)]
        [TestCase("skin wrong kind", true)]
        public async Task RefusedEquip_DoesNotMutateOrWriteAnyEntry(string refusal, bool local)
        {
            var catalog = ServerCatalog.Parse(JsonConvert.SerializeObject(ServerCatalog.Items));
            var store = new RecordingStore();
            var fake = new LocalInventoryService(store, CatalogTests.ExpectedBases());
            IPlayerStateService states = local ? new LocalPlayerStateService(store, fake.GrantDefaults) : Server(store, catalog);
            IInventoryService inventory = local ? fake : Server(store, catalog);
            var state = await states.GetAsync();
            state.Rank.Arena = 1;
            state.Rank.HighestArena = 2;
            await states.GetAsync();
            // A valid change is deliberately first: a later invalid item must not apply it.
            var changes = Variant("suit.warrior", "suit.warrior.e1");
            string key = "suit.miner", id = "suit.miner.e1";
            string message = "";
            switch (refusal)
            {
                case "unowned": state.Inventory.OwnedVariants.Remove(id); message = "not owned"; break;
                case "wrong base": id = "suit.scout.e1"; message = "base"; break;
                case "locked": id = "suit.miner.e2"; message = "locked"; break;
                case "unknown": id = "suit.miner.e99"; message = "Unknown"; break;
                case "retired": catalog.Single(i => i.Id == id).Retired = true; message = "retired"; break;
                case "unknown base": key = "suit.unknown"; message = "Unknown base"; break;
                case "wrong kind": id = "skin.suit.miner.default"; break;
                case "null item": id = null; message = "Unknown"; break;
                case "null changes": changes = null; message = "must not be null"; break;
                case "skin unowned": id = "skin.suit.miner.default"; state.Inventory.OwnedSkins.Remove(id); message = "not owned"; break;
                case "skin wrong base": id = "skin.suit.scout.default"; message = "base"; break;
                case "skin unknown": id = "skin.suit.miner.unknown"; message = "Unknown"; break;
                case "skin retired": id = "skin.suit.miner.default"; catalog.Single(i => i.Id == id).Retired = true; message = "retired"; break;
                case "skin wrong kind": id = "suit.miner.e1"; break;
            }
            if (changes != null)
            {
                if (refusal.StartsWith("skin")) changes.Skins = new Dictionary<string, string> { { key, id } };
                else changes.Variants.Add(key, id);
            }
            string before = JsonConvert.SerializeObject(state);
            int writes = store.Writes.Count;
            var exception = Assert.ThrowsAsync<InventoryValidationException>(async () => await inventory.EquipAsync(changes));
            Assert.That(exception.Message, Does.Contain(message));
            Assert.That(JsonConvert.SerializeObject(await store.ReadAsync()), Is.EqualTo(before));
            Assert.That(store.Writes, Has.Count.EqualTo(writes));
        }

        [Test]
        public async Task OldStoredPlayer_WithoutEquipped_LoadsAndMigratesOnce()
        {
            var store = new RecordingStore();
            var old = JsonConvert.DeserializeObject<InventoryRecord>(
                "{\"OwnedVariants\":[\"suit.warrior.e0\"],\"OwnedSkins\":null,\"EquippedSuitIDs\":[\"legacy\"]}");
            await store.WriteAsync(new PlayerState
            {
                Rating = PlayerStateDefaults.Rating(), Rank = PlayerStateDefaults.Rank(),
                Inventory = old, History = PlayerStateDefaults.History()
            });
            var service = Server(store);
            var state = await service.GetAsync();
            Assert.That(state.Inventory.Equipped.Variants, Has.Count.EqualTo(23));
            Assert.That(state.Inventory.Equipped.Skins, Has.Count.EqualTo(23));
            Assert.That(state.Inventory.OwnedVariants, Has.Count.EqualTo(23));
            Assert.That(state.Inventory.OwnedSkins, Has.Count.EqualTo(23));
            Assert.That(JsonConvert.SerializeObject(state.Inventory), Does.Contain("\"EquippedSuitIDs\":[\"legacy\"]"));
            AssertInventoryOnly(store.Writes.Last());
            await service.GetAsync();
            Assert.That(store.Writes, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task SharedDefaults_NormalizeNullListsAndDictionaries_WithoutGrantingItems()
        {
            var store = new RecordingStore();
            await store.WriteAsync(new PlayerState { Inventory = new InventoryRecord { Equipped = new EquippedRecord() } });
            var state = await PlayerStateLogic.GetOrCreateAsync(store);
            Assert.That(state.Inventory.OwnedVariants, Is.Empty);
            Assert.That(state.Inventory.OwnedSkins, Is.Empty);
            Assert.That(state.Inventory.Equipped.Variants, Is.Empty);
            Assert.That(state.Inventory.Equipped.Skins, Is.Empty);
            await PlayerStateLogic.GetOrCreateAsync(store);
            Assert.That(store.Writes, Has.Count.EqualTo(2));
        }

        [Test]
        public async Task InvalidEquip_DoesNotNormalizeLegacyInventory()
        {
            var store = new RecordingStore();
            await store.WriteAsync(new PlayerState { Rank = PlayerStateDefaults.Rank(), Inventory = new InventoryRecord() });
            string before = JsonConvert.SerializeObject(await store.ReadAsync());
            Assert.ThrowsAsync<InventoryValidationException>(async () => await Server(store).EquipAsync(Variant("suit.warrior", "suit.warrior.e0")));
            Assert.That(JsonConvert.SerializeObject(await store.ReadAsync()), Is.EqualTo(before));
            Assert.That(store.Writes, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task EquipBeforePlayerState_RefusesWithoutCreatingRecords()
        {
            var store = new RecordingStore();
            Assert.ThrowsAsync<InventoryValidationException>(async () => await Server(store).EquipAsync(Variant("suit.warrior", "suit.warrior.e0")));
            Assert.That(store.Writes, Is.Empty);
            Assert.That((await store.ReadAsync()).Inventory, Is.Null);
        }

        [Test]
        public async Task RetiredItems_AreNotGrantedOrEquipped()
        {
            var catalog = ServerCatalog.Parse(JsonConvert.SerializeObject(ServerCatalog.Items));
            catalog.Single(i => i.Id == "suit.warrior.e0").Retired = true;
            catalog.Single(i => i.Id == "skin.suit.warrior.default").Retired = true;
            var state = await Server(new RecordingStore(), catalog).GetAsync();
            Assert.That(state.Inventory.OwnedVariants, Does.Not.Contain("suit.warrior.e0"));
            Assert.That(state.Inventory.OwnedSkins, Does.Not.Contain("skin.suit.warrior.default"));
            Assert.That(state.Inventory.Equipped.Variants.ContainsKey("suit.warrior"), Is.False);
            Assert.That(state.Inventory.Equipped.Skins.ContainsKey("suit.warrior"), Is.False);
        }

        private static void AssertInventoryOnly(PlayerState write)
        {
            Assert.That(write.Inventory, Is.Not.Null);
            Assert.That(write.Rating, Is.Null);
            Assert.That(write.Rank, Is.Null);
            Assert.That(write.History, Is.Null);
        }
    }
}
