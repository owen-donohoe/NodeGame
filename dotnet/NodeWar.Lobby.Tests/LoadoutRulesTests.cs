using NodeWar.Backend;
using NodeWar.Lobby;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class LoadoutRulesTests
    {
        [TestCase(false)] [TestCase(true)]
        public void DropUnavailableIsSkippedWithoutCompleteOwnership(bool partial)
        {
            var data = LoadoutData.CreateEmpty(2);
            data.suitIDs[1] = "suit_medic";
            data.districtIDs[3] = "node_fortress";
            var state = partial ? new PlayerState { Rank = new RankRecord { Arena = 2 }, Inventory = new InventoryRecord() } : null;
            var editor = LoadoutRules.OwnedEditor(data, 2, state);
            CollectionAssert.AreEqual(data.suitIDs, editor.ToLoadout().suitIDs);
            CollectionAssert.AreEqual(data.districtIDs, editor.ToLoadout().districtIDs);
        }

        [Test]
        public void ClearWithoutStateReappliesAllStarterLocks()
        {
            PlayerState state = null;
            var model = new LoadoutScreenModel(new LoadoutData { suitIDs = new string[3], districtIDs = new string[5] }, state?.Rank?.Arena ?? 0);
            model.ClearChoices();
            CollectionAssert.AreEqual(new[] { "suit_warrior", "", "" }, model.ToData().suitIDs);
            CollectionAssert.AreEqual(new[] { "node_farm", "node_mine", "node_town", "", "" }, model.ToData().districtIDs);
        }

        private static PlayerState Owns(params string[] ids)
        {
            var variants = new System.Collections.Generic.List<string>();
            foreach (string id in ids) variants.Add(CatalogIds.Variant(LoadoutTypes.CatalogBaseForLobbyId(id), 0));
            return new PlayerState { Inventory = new InventoryRecord { OwnedVariants = variants } };
        }

        [TestCase(1, 0, "Missing 1 district")]
        [TestCase(2, 0, "Missing 2 districts")]
        [TestCase(0, 1, "Missing 1 suit")]
        [TestCase(0, 2, "Missing 2 suits")]
        [TestCase(1, 1, "Fill your loadout")]
        [TestCase(0, 0, "")]
        public void MissingCountsOnlyFillableEmptySlotsAndPluralizes(int districts, int suits, string message)
        {
            var data = LoadoutData.CreateEmpty(2);
            var state = Owns("node_farm", "node_mine", "node_town", "suit_warrior");
            if (districts > 0) state.Inventory.OwnedVariants.Add("district.barracks.e0");
            if (districts > 1) state.Inventory.OwnedVariants.Add("district.village.e0");
            if (suits > 0) state.Inventory.OwnedVariants.Add("suit.guardian.e0");
            if (suits > 1) state.Inventory.OwnedVariants.Add("suit.scout.e0");
            var missing = LoadoutRules.MissingCounts(data, 2, state);
            Assert.AreEqual(districts, missing.Districts);
            Assert.AreEqual(suits, missing.Suits);
            Assert.AreEqual(message, LoadoutRules.MissingMessage(data, 2, state));
            Assert.AreEqual(districts + suits == 0, LoadoutRules.IsComplete(data, 2, state));
        }

        [Test]
        public void MissingCountsClampToEmptySlotsAndExcludeOwnedItemsOutsideEra()
        {
            var data = LoadoutData.CreateEmpty(2);
            data.districtIDs[3] = "node_barracks";
            data.suitIDs[1] = "suit_guardian";
            var state = Owns("node_barracks", "node_village", "node_forge", "node_fortress",
                "suit_guardian", "suit_scout", "suit_medic", "suit_berserker");
            var missing = LoadoutRules.MissingCounts(data, 2, state);
            Assert.AreEqual(1, missing.Districts);
            Assert.AreEqual(1, missing.Suits);
            var unavailable = Owns("node_fortress");
            Assert.AreEqual(0, LoadoutRules.MissingCounts(LoadoutData.CreateEmpty(0), 0, unavailable).Districts);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void AllEligibleOwnedEquipped_AllowsShortLoadout(int arena)
        {
            var data = LoadoutData.CreateEmpty(arena);
            data.suitIDs[0] = "suit_guardian";
            if (arena >= 2)
            {
                data.districtIDs[0] = "node_farm";
                data.districtIDs[1] = "node_mine";
                data.districtIDs[2] = "node_town";
            }
            var state = Owns("suit_guardian", "suit_warrior", "node_farm", "node_mine", "node_town");
            if (arena >= 1) data.suitIDs[1] = "suit_warrior";
            Assert.IsTrue(LoadoutRules.IsComplete(data, arena, state));
            Assert.IsTrue(new LoadoutScreenModel(data, arena).IsComplete(arena, state));
        }

        [TestCase(true)] [TestCase(false)]
        public void UnequippedOwnedItemOnEitherSide_BlocksShortLoadout(bool suit)
        {
            var data = LoadoutData.CreateEmpty(1);
            var state = Owns(suit ? "suit_guardian" : "node_barracks");
            Assert.IsFalse(LoadoutRules.IsComplete(data, 1, state));
            (suit ? data.suitIDs : data.districtIDs)[1] = suit ? "suit_guardian" : "node_barracks";
            Assert.IsTrue(LoadoutRules.IsComplete(data, 1, state));
        }

        [Test]
        public void GrantsAreAlreadyEquipped_AndNeverSpendChosenCapacity()
        {
            var data = LoadoutData.CreateEmpty(1);
            var state = Owns("suit_warrior", "node_farm", "node_mine", "node_town");
            Assert.IsTrue(LoadoutRules.IsComplete(data, 1, state));
            Assert.IsTrue(LoadoutRules.IsComplete(LoadoutData.CreateEmpty(2), 2, state));
            CollectionAssert.AreEqual(new[] { "suit_warrior", "", "" }, data.suitIDs);
            CollectionAssert.AreEqual(new[] { "node_farm", "node_mine", "node_town", "", "" }, data.districtIDs);
        }

        [Test]
        public void UnownedOrUnofferedItems_DoNotBlockShortLoadout()
        {
            var state = Owns();
            state.Inventory.OwnedVariants.Add("district.core.e0");
            state.Inventory.OwnedVariants.Add("district.camp.e0");
            state.Inventory.OwnedVariants.Add("suit.scout.e1"); // No owned base/root.
            Assert.IsTrue(LoadoutRules.IsComplete(LoadoutData.CreateEmpty(1), 1, state));
            Assert.IsFalse(LoadoutRules.IsComplete(LoadoutData.CreateEmpty(1), 1)); // Offline starter ownership.
        }

        [TestCase(1)] [TestCase(2)]
        public void FullLoadout_DoesNotRequireEquippingOtherOwnedItems(int arena)
        {
            var data = LoadoutData.CreateEmpty(arena);
            string[] suits = { "suit_guardian", "suit_scout", "suit_medic" };
            string[] districts = { "node_barracks", "node_village", "node_farm", "node_mine", "node_town" };
            System.Array.Copy(suits, data.suitIDs, data.suitIDs.Length);
            System.Array.Copy(districts, data.districtIDs, data.districtIDs.Length);
            Assert.IsTrue(LoadoutRules.IsComplete(data, arena, Owns("suit_berserker", "node_forge")));
        }

        [TestCase(0, 3, 1)] [TestCase(1, 2, 0)] [TestCase(2, 0, 0)]
        public void ArenaLocksAreIncludedInTotalCapacity(int arena, int districts, int suits)
        {
            Assert.AreEqual(districts, LoadoutRules.LockedDistricts(arena).Length);
            Assert.AreEqual(suits, LoadoutRules.LockedSuits(arena).Length);
            Assert.AreEqual(5, LoadoutRules.ChosenDistrictSlots(arena));
            Assert.AreEqual(3, LoadoutRules.ChosenSuitSlots(arena));
        }

        [Test]
        public void MissingRank_FallsBackToOne()
        {
            Assert.AreEqual(1, LoadoutRules.ArenaOrFallback(null));
            Assert.AreEqual(0, LoadoutRules.ArenaOrFallback(new RankRecord { Arena = 0 }));
            Assert.AreEqual(7, LoadoutRules.ArenaOrFallback(new RankRecord { Arena = 7 }));
        }

        [Test]
        public void LocksStayInsideArraysAndPolicyArraysAreDetached()
        {
            var editor = new LoadoutEditor(LoadoutData.CreateEmpty(0), 0);
            editor.ClearChoices();
            editor.DropUnavailable(_ => false, _ => false);
            CollectionAssert.AreEqual(new[] { "suit_warrior", "", "" }, editor.ToLoadout().suitIDs);
            CollectionAssert.AreEqual(new[] { "node_farm", "node_mine", "node_town", "", "" }, editor.ToLoadout().districtIDs);
            LoadoutRules.LockedSuits(0)[0] = "changed";
            LoadoutRules.LockedDistricts(0)[0] = "changed";
            Assert.AreEqual("suit_warrior", LoadoutRules.LockedSuits(0)[0]);
            Assert.AreEqual("node_farm", LoadoutRules.LockedDistricts(0)[0]);
        }
    }
}
