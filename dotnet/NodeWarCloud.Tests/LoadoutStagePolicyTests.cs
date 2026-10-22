using System;
using System.Linq;
using System.Threading.Tasks;
using NodeWar.Backend;
using NodeWar.Lobby;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class LoadoutStagePolicyTests
    {
        [TestCase(0, false, "node_farm", true)] [TestCase(1, false, "node_farm", true)] [TestCase(2, false, "node_farm", false)]
        [TestCase(0, false, "node_town", true)] [TestCase(1, false, "node_town", false)]
        [TestCase(0, true, "suit_warrior", true)] [TestCase(1, true, "suit_warrior", false)]
        public void SnapshotLocksCannotBeOmitted(int arena, bool suit, string id, bool required)
        {
            var (record, log) = MatchEligibilityTests.Current(1);
            record.players[0].Rank.Arena = arena;
            log.loadouts[0].suits = new[] { (int)SuitType.Warrior, (int)SuitType.Guardian };
            log.loadouts[0].districts = new[] { (int)DistrictType.Farm, (int)DistrictType.Mine, (int)DistrictType.Town, (int)DistrictType.Barracks };
            log.draft = Array.Empty<DraftPlacement>();
            Assert.IsNull(MatchEligibility.Check(record, log));
            if (suit) log.loadouts[0].suits = log.loadouts[0].suits.Where(t => t != (int)LoadoutTypes.SuitForLobbyId(id)).ToArray();
            else log.loadouts[0].districts = log.loadouts[0].districts.Where(t => t != (int)LoadoutTypes.DistrictForLobbyId(id)).ToArray();
            string error = MatchEligibility.Check(record, log);
            if (required) StringAssert.Contains("locked", error);
            else Assert.IsNull(error);
        }

        [TestCase(0, 3, 1)] [TestCase(1, 2, 0)] [TestCase(2, 0, 0)]
        public void ServerUsesSameCapacitiesAndLocks(int arena, int districts, int suits)
        {
            Assert.AreEqual(5, LoadoutCapacityPolicy.DistrictCapacity(arena));
            Assert.AreEqual(3, LoadoutCapacityPolicy.SuitCapacity(arena));
            Assert.AreEqual(districts, LoadoutCapacityPolicy.LockedDistricts(arena).Length);
            Assert.AreEqual(suits, LoadoutCapacityPolicy.LockedSuits(arena).Length);
        }

        [TestCase("district.forge", 1, false)] [TestCase("district.forge", 2, true)]
        [TestCase("suit.scout", 0, false)] [TestCase("suit.scout", 1, true)]
        [TestCase("district.town", 0, true)] [TestCase("suit.warrior", 0, true)]
        public async Task OwnedRootEquipAgreesWithFakeAndClamp(string baseId, int arena, bool allowed)
        {
            var store = new InMemoryPlayerRecordStore();
            var state = MatchRecordTests.Player();
            state.Rank.Arena = arena;
            string root = CatalogIds.Variant(baseId, 0);
            state.Inventory.OwnedVariants.Add(root);
            state.Inventory.Equipped.Variants[baseId] = CatalogIds.Variant(baseId, 4);
            InventoryClamp.ClampToArena(state.Inventory, arena);
            Assert.AreEqual(root, state.Inventory.Equipped.Variants[baseId]);
            Assert.AreEqual(allowed, LoadoutCapacityPolicy.IsVariantUsable(baseId, root, arena));
            Assert.IsFalse(InventoryClamp.ClampToArena(state.Inventory, arena));
            var patch = new EquippedRecord { Variants = new System.Collections.Generic.Dictionary<string, string> { [baseId] = root } };
            var rules = new InventoryRules(ServerCatalog.Items);
            await store.WriteAsync(state);
            var fake = new LocalInventoryService(store, new[] { baseId });
            if (allowed)
            {
                Assert.DoesNotThrow(() => rules.Equip(state, patch));
                Assert.DoesNotThrowAsync(async () => await fake.EquipAsync(patch));
            }
            else
            {
                Assert.Throws<InventoryValidationException>(() => rules.Equip(state, patch));
                Assert.ThrowsAsync<InventoryValidationException>(async () => await fake.EquipAsync(patch));
            }
        }
    }
}
