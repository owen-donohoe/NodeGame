using System;
using System.Collections.Generic;
using System.Linq;
using NodeWar.Backend;
using NodeWar.Progression;
using NUnit.Framework;
namespace NodeWar.Cloud.Tests
{
    public class DistrictMigrationTests
    {
        private sealed class Store : NodeWar.Cloud.ILockedPlayerRecordStore
        {
            public PlayerState State;
            public int Writes;
            public System.Threading.Tasks.Task<PlayerState> ReadAsync() =>
                System.Threading.Tasks.Task.FromResult(Newtonsoft.Json.JsonConvert.DeserializeObject<PlayerState>(
                    Newtonsoft.Json.JsonConvert.SerializeObject(State)));
            public System.Threading.Tasks.Task WriteAsync(PlayerState records)
            { Writes++; if (records.Inventory != null) State.Inventory = records.Inventory; return System.Threading.Tasks.Task.CompletedTask; }
            public async System.Threading.Tasks.Task<(PlayerState State, string InventoryWriteLock)> ReadInventoryLockedAsync() =>
                (await ReadAsync(), "1");
            public System.Threading.Tasks.Task WriteDefaultsLockedAsync(PlayerState records, string inventoryLock) => WriteAsync(records);
            public System.Threading.Tasks.Task WriteInventoryLockedAsync(InventoryRecord inventory, string inventoryLock) =>
                WriteAsync(new PlayerState { Inventory = inventory });
        }

        [Test]
        public async System.Threading.Tasks.Task SavedMigration_WritesInventoryOnlyOnce_LocalAndCloud()
        {
            foreach (bool cloud in new[] { false, true })
            {
                var state = Legacy(2);
                PlayerStateLogic.ApplyDefaults(state);
                // Restore a legacy equipped source after default creation so the service owns migration.
                state.Inventory.Equipped.Variants["district.camp"] = "district.camp.e2";
                var store = new Store { State = state };
                Func<System.Threading.Tasks.Task<PlayerState>> get;
                if (cloud)
                {
                    var service = new NodeWar.Cloud.InventoryPlayerStateService(store,
                        new NodeWar.Cloud.InventoryRules(NodeWar.Cloud.ServerCatalog.Items));
                    get = service.GetAsync;
                }
                else
                {
                    var inventory = new LocalInventoryService(store, CatalogTests.ExpectedBases());
                    get = () => PlayerStateLogic.GetOrCreateAsync(store, inventory.GrantDefaults);
                }
                var first = await get();
                Assert.AreEqual("district.barracks.e2", first.Inventory.Equipped.Variants["district.barracks"]);
                Assert.AreEqual(1, store.Writes);
                await get();
                Assert.AreEqual(1, store.Writes);
            }
        }

        [Test]
        public void CanonicalEqualEraTie_KeepsCanonicalSkin()
        {
            var state = Legacy(2);
            state.Inventory.OwnedVariants.Add("district.barracks.e2");
            state.Inventory.OwnedSkins.Add("skin.district.barracks.custom");
            state.Inventory.Equipped.Variants["district.barracks"] = "district.barracks.e2";
            state.Inventory.Equipped.Skins["district.barracks"] = "skin.district.barracks.custom";
            Migrate(state);
            Assert.AreEqual("district.barracks.e2", state.Inventory.Equipped.Variants["district.barracks"]);
            Assert.AreEqual("skin.district.barracks.custom", state.Inventory.Equipped.Skins["district.barracks"]);
        }

        private static bool Migrate(PlayerState state)
        {
            return DistrictMigration.Apply(state);
        }
        private static PlayerState Legacy(int arena = 5)
        {
            var state = new PlayerState { Rank = new RankRecord { Arena = arena, HighestArena = 5 },
                Inventory = PlayerStateDefaults.Inventory() };
            foreach (string id in new[] { "district.camp.e2", "district.arsenal.e3",
                "district.shrine.e1", "district.sanctuary.e2", "district.rampart.e1", "district.watchtower.e4",
                "district.market.e2" })
            {
                state.Inventory.OwnedVariants.Add(id);
                CatalogIds.TryParseVariant(id, out var baseId, out _);
                state.Inventory.Equipped.Variants[baseId] = id;
            }
            state.Inventory.EquippedNodeIDs = new[] { "node_camp", "node_arsenal" };
            return state;
        }
        [Test]
        public void LegacyRoster_MigratesIdempotently()
        {
            var state = Legacy();
            var old = state.Inventory.OwnedVariants.ToArray();
            Assert.IsTrue(Migrate(state));
            foreach (string id in old) CollectionAssert.Contains(state.Inventory.OwnedVariants, id);
            foreach (string id in new[] { "district.barracks.e2", "district.barracks.e3",
                "district.infirmary.e1", "district.infirmary.e2", "district.fortress.e1" })
                CollectionAssert.Contains(state.Inventory.OwnedVariants, id);
            CollectionAssert.AreEqual(new[] { "node_barracks", "" }, state.Inventory.EquippedNodeIDs);
            Assert.AreEqual("district.market.e2", state.Inventory.Equipped.Variants["district.market"]);
            Assert.IsFalse(Migrate(state), "Second run must require zero writes.");
        }
        [Test]
        public void CollisionAndDemotion_PickHighestUsableEra()
        {
            var state = Legacy(2);
            state.Inventory.OwnedVariants.Add("district.barracks.e1");
            state.Inventory.Equipped.Variants["district.barracks"] = "district.barracks.e1";
            Migrate(state);
            Assert.AreEqual("district.barracks.e2", state.Inventory.Equipped.Variants["district.barracks"]);
            state.Inventory.Equipped.Variants["district.camp"] = "district.camp.e2";
            Assert.IsTrue(Migrate(state));
            Assert.AreEqual("district.barracks.e2", state.Inventory.Equipped.Variants["district.barracks"]);
            var rules = new NodeWar.Cloud.InventoryRules(NodeWar.Cloud.ServerCatalog.Items);
            foreach (string baseId in new[] { "district.camp", "district.watchtower", "district.unknown" })
                Assert.Throws<InventoryValidationException>(() => rules.Equip(state,
                    new EquippedRecord { Variants = new Dictionary<string, string> { [baseId] = baseId + ".e0" } }));
        }
        [Test]
        public void LegacySkinRemainsOwnedButNewBaseUsesDefault()
        {
            var state = Legacy();
            state.Inventory.OwnedSkins.Add("skin.district.rampart.custom");
            state.Inventory.Equipped.Skins["district.rampart"] = "skin.district.rampart.custom";
            Migrate(state);
            CollectionAssert.Contains(state.Inventory.OwnedSkins, "skin.district.rampart.custom");
            CollectionAssert.Contains(state.Inventory.OwnedSkins, "skin.district.fortress.default");
            Assert.AreEqual("skin.district.fortress.default", state.Inventory.Equipped.Skins["district.fortress"]);
            Assert.IsFalse(state.Inventory.OwnedSkins.Contains("skin.district.fortress.custom"));
        }
        [Test]
        public void CatalogRetainsOldIdsAsRetired()
        {
            var catalog = NodeWar.Cloud.ServerCatalog.Items;
            foreach (string key in new[] { "camp", "shrine", "arsenal", "sanctuary", "watchtower", "rampart" })
                Assert.That(catalog.Where(i => i.BaseId == "district." + key).Select(i => i.Retired),
                    Is.Not.Empty.And.All.True);
            foreach (string key in new[] { "farm", "mine", "village", "barracks", "forge", "market",
                "pier", "town", "infirmary", "fortress" })
            {
                var items = catalog.Where(i => i.BaseId == "district." + key && !i.Retired).ToArray();
                CollectionAssert.AreEquivalent(Enumerable.Range(0, 6),
                    items.Where(i => i.Kind == CatalogItemKind.Variant).Select(i => i.Era));
                Assert.That(items.Select(i => i.Id), Does.Contain("skin.district." + key + ".default"));
            }
            Assert.IsEmpty(CatalogValidation.ValidateAgainstPrevious(CatalogTests.CommittedCatalog(), catalog));
        }
    }
}
