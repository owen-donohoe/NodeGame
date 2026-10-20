using System;
using System.Linq;
using NodeWar.Backend;
using NodeWar.Network;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class LoadoutCapacityTests
    {
        [TestCase(0, 3, 1)] [TestCase(1, 2, 0)] [TestCase(2, 0, 0)] [TestCase(5, 0, 0)]
        public void PerEraLocksAndFullShape(int arena, int districts, int suits)
        {
            CollectionAssert.AreEqual(new[] { "node_farm", "node_mine", "node_town" }.Take(districts), LoadoutRules.LockedDistricts(arena));
            CollectionAssert.AreEqual(new[] { "suit_warrior" }.Take(suits), LoadoutRules.LockedSuits(arena));
            Assert.AreEqual(3, LoadoutCapacityPolicy.SuitCapacity(arena));
            Assert.AreEqual(5, LoadoutCapacityPolicy.DistrictCapacity(arena));
            Assert.AreEqual(arena > 0, LoadoutRules.IsSuitOffered("suit_warrior", arena));
            Assert.AreEqual(arena > 0, LoadoutRules.IsDistrictOffered("node_town", arena));
            Assert.AreEqual(arena >= 2, LoadoutRules.IsDistrictOffered("node_farm", arena));
            Assert.AreEqual(arena >= 2, LoadoutRules.IsDistrictOffered("node_mine", arena));
            Assert.IsFalse(LoadoutRules.IsDistrictOffered("node_core", arena));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void FreshDefaultsAndAllPresets(int arena)
        {
            var profile = new PlayerProfileData();
            LoadoutPresets.Migrate(ref profile);
            foreach (var data in profile.loadoutPresets.Select(p => p.ToLoadout()).Concat(new[] { LoadoutData.CreateEmpty(arena), LoadoutData.Normalized(default) }))
            {
                CollectionAssert.AreEqual(new[] { "suit_warrior", "", "" }, data.suitIDs);
                CollectionAssert.AreEqual(new[] { "node_farm", "node_mine", "node_town", "", "" }, data.districtIDs);
                Assert.AreEqual(DistrictType.Town, LoadoutTypes.DistrictForLobbyId(data.districtIDs[2]));
            }
        }

        [Test]
        public void ExistingThreeLegacyPresetsMigrateTogetherOnce()
        {
            var legacy = new LoadoutRecord { suitIDs = new[] { "suit_guardian", "suit_scout" },
                nodeIDs = new[] { "node_barracks", "node_village" } };
            var profile = new PlayerProfileData { activeLoadoutPreset = 2, loadoutPresets = new[] { legacy, legacy, legacy } };
            Assert.IsTrue(LoadoutPresets.Migrate(ref profile));
            Assert.AreEqual(2, profile.activeLoadoutPreset);
            foreach (var preset in profile.loadoutPresets)
            {
                CollectionAssert.AreEqual(new[] { "node_farm", "node_mine", "node_town", "node_barracks", "node_village" }, preset.nodeIDs);
                CollectionAssert.AreEqual(new[] { "suit_warrior", "suit_guardian", "suit_scout" }, preset.suitIDs);
            }
            Assert.IsFalse(LoadoutPresets.Migrate(ref profile));
            Assert.AreEqual(2, legacy.nodeIDs.Length);
        }

        [Test]
        public void ProductionSuitOwnershipCannotBlockQueue()
        {
            var state = Owned(0, "suit_warrior", "node_farm", "node_mine", "node_town", "suit_farmer", "suit_miner");
            Assert.IsTrue(LoadoutRules.IsComplete(LoadoutData.CreateEmpty(0), 0, state));
            Assert.IsFalse(LoadoutRules.IsSuitOffered("suit_farmer", 0));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void LegacyChoicesMigrateOnceAndKeepEquipment(int arena)
        {
            var legacy = new LoadoutData { suitIDs = new[] { "suit_guardian", "suit_scout" },
                districtIDs = new[] { "node_barracks", "node_village" }, suitEras = new[] { 0, 1 }, skinIDs = new[] { "skin.suit.guardian.default" } };
            var migrated = LoadoutData.Normalized(legacy, arena);
            CollectionAssert.AreEqual(new[] { "suit_warrior", "suit_guardian", "suit_scout" }, migrated.suitIDs);
            CollectionAssert.AreEqual(new[] { "node_farm", "node_mine", "node_town", "node_barracks", "node_village" }, migrated.districtIDs);
            for (int i = 0; i < 3; i++)
            {
                var copy = LoadoutRecord.From(migrated).ToLoadout();
                CollectionAssert.AreEqual(migrated.suitIDs, copy.suitIDs);
                CollectionAssert.AreEqual(migrated.districtIDs, copy.districtIDs);
                CollectionAssert.AreEqual(migrated.suitEras, copy.suitEras);
                CollectionAssert.AreEqual(legacy.skinIDs, copy.skinIDs);
                migrated = copy;
            }
            Assert.AreEqual(2, legacy.suitIDs.Length);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void EditorRefusesLockedRemovalReplacementAndClearKeepsOnlyLocks(int arena)
        {
            var source = LoadoutData.CreateEmpty(arena);
            var editor = new LoadoutEditor(source, arena);
            editor.EquipSuit("suit_guardian"); editor.EquipSuit("suit_scout");
            editor.EquipDistrict("node_barracks"); editor.EquipDistrict("node_village");
            foreach (string id in LoadoutRules.LockedDistricts(arena))
            {
                int slot = Array.IndexOf(editor.ToLoadout().districtIDs, id);
                Assert.AreEqual("", editor.ClearDistrictSlot(slot));
                Assert.IsFalse(editor.ReplaceDistrictSlot(slot, "node_forge"));
            }
            foreach (string id in LoadoutRules.LockedSuits(arena))
            {
                int slot = Array.IndexOf(editor.ToLoadout().suitIDs, id);
                Assert.AreEqual("", editor.ClearSuitSlot(slot));
                Assert.IsFalse(editor.ReplaceSuitSlot(slot, "suit_medic"));
            }
            editor.ClearChoices();
            CollectionAssert.AreEquivalent(LoadoutRules.LockedDistricts(arena), editor.ToLoadout().districtIDs.Where(id => id.Length > 0));
            CollectionAssert.AreEquivalent(LoadoutRules.LockedSuits(arena), editor.ToLoadout().suitIDs.Where(id => id.Length > 0));
            Assert.AreEqual(3, editor.SuitSlotCount); Assert.AreEqual(5, editor.DistrictSlotCount);
            CollectionAssert.AreEqual(new[] { "node_farm", "node_mine", "node_town", "", "" }, source.districtIDs);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void DropsNeverRemoveLocksAndClearSurvivesPresetSwitch(int arena)
        {
            var editor = LoadoutRules.LocalEditor(LoadoutData.CreateEmpty(arena), arena);
            editor.DropUnavailable(_ => false, _ => false);
            editor.ClearChoices();
            var profile = new PlayerProfileData();
            LoadoutPresets.Store(ref profile, editor.ToLoadout());
            LoadoutPresets.Switch(ref profile, 1, arena, _ => true, _ => true);
            var restored = LoadoutPresets.Switch(ref profile, 0, arena, _ => true, _ => true);
            CollectionAssert.AreEqual(editor.ToLoadout().districtIDs, restored.districtIDs);
            CollectionAssert.AreEqual(editor.ToLoadout().suitIDs, restored.suitIDs);
        }

        private static PlayerState Owned(int arena, params string[] ids) => new PlayerState
        {
            Rank = new RankRecord { Arena = arena, HighestArena = 5 },
            Inventory = new InventoryRecord { OwnedVariants = ids.Select(id => CatalogIds.Variant(LoadoutTypes.CatalogBaseForLobbyId(id), 0)).ToList() }
        };

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void QueueFullOrExhaustedRequiresBothSides(int arena)
        {
            var data = LoadoutData.CreateEmpty(arena);
            var current = data.suitIDs.Concat(data.districtIDs).Where(id => id.Length > 0).ToArray();
            Assert.IsTrue(LoadoutRules.IsComplete(data, arena, Owned(arena, current)));
            Assert.IsFalse(LoadoutRules.IsComplete(data, arena, Owned(arena, current.Concat(new[] { "suit_guardian" }).ToArray())));
            Assert.IsFalse(LoadoutRules.IsComplete(data, arena, Owned(arena, current.Concat(new[] { "node_barracks" }).ToArray())));
            data.suitIDs = new[] { "suit_warrior", "suit_guardian", "suit_scout" };
            data.districtIDs = new[] { "node_farm", "node_mine", "node_town", "node_barracks", "node_village" };
            Assert.IsTrue(LoadoutRules.IsComplete(data, arena, Owned(arena, current)));
        }

        [Test]
        public void QueueIgnoresOwnedFutureBasesUntilUsableInExplicitEra()
        {
            var data = LoadoutData.CreateEmpty(0);
            var state = Owned(5, "suit_warrior", "node_farm", "node_mine", "node_town", "suit_scout", "node_village");
            Assert.IsTrue(LoadoutRules.IsComplete(data, 0, state));
            Assert.IsFalse(LoadoutRules.IsComplete(data, 1, state));
            var future = Owned(5, "suit_warrior", "node_farm", "node_mine", "node_town", "node_forge");
            Assert.IsTrue(LoadoutRules.IsComplete(data, 1, future));
            Assert.IsFalse(LoadoutRules.IsComplete(data, 2, future));
        }

        [Test]
        public void DemotionRestoresMissingLocksWithoutMutatingFrozenRemote()
        {
            var remote = new LoadoutData { suitIDs = new[] { "suit_scout", "suit_guardian", "suit_medic" },
                districtIDs = new[] { "node_barracks", "node_forge", "node_village", "node_workshop", "node_fortress" } };
            var local = LoadoutRules.LocalEditor(remote, 0).ToLoadout();
            foreach (string id in LoadoutRules.LockedDistricts(0)) CollectionAssert.Contains(local.districtIDs, id);
            CollectionAssert.Contains(local.suitIDs, "suit_warrior");
            CollectionAssert.DoesNotContain(remote.districtIDs, "node_farm");
            Assert.AreEqual("suit_medic", remote.suitIDs[2]);
        }
    }
}
