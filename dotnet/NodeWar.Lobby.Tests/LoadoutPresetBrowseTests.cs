using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class LoadoutPresetBrowseTests
    {
        private static PlayerProfileData Profile()
        {
            var chosen = LoadoutData.CreateEmpty(2);
            chosen.suitIDs[0] = "suit_scout";
            chosen.districtIDs[0] = "node_pier";
            return new PlayerProfileData { loadout = LoadoutRecord.From(chosen) };
        }

        [Test]
        public void MigrationPreservesCurrentInFirstPresetAndIsIdempotent()
        {
            var profile = Profile();
            Assert.IsTrue(LoadoutPresets.Migrate(ref profile));
            Assert.AreEqual(3, profile.loadoutPresets.Length);
            Assert.AreEqual(0, profile.activeLoadoutPreset);
            Assert.AreEqual("node_pier", profile.loadoutPresets[0].nodeIDs[0]);
            Assert.IsEmpty(profile.loadoutPresets[1].nodeIDs[0]);
            Assert.IsFalse(LoadoutPresets.Migrate(ref profile));
            profile.loadout.nodeIDs[0] = "";
            Assert.AreEqual("node_pier", profile.loadoutPresets[0].nodeIDs[0]);
        }

        [Test]
        public void SwitchingStoresEditsAndRestoresChosenSlotsWithoutEquipmentRollback()
        {
            var profile = Profile();
            LoadoutPresets.Switch(ref profile, 1, 2, _ => true, _ => true);
            var edit = profile.loadout.ToLoadout();
            edit.districtIDs[0] = "node_infirmary";
            edit.suitEras[1] = 2;
            LoadoutPresets.Store(ref profile, edit);
            var first = LoadoutPresets.Switch(ref profile, 0, 2, _ => true, _ => true);
            Assert.AreEqual("node_pier", first.districtIDs[0]);
            Assert.AreEqual(2, first.suitEras[1]);
            var second = LoadoutPresets.Switch(ref profile, 1, 2, _ => true, _ => true);
            Assert.AreEqual("node_infirmary", second.districtIDs[0]);
        }

        [TestCase(1, 2, 2)]
        [TestCase(2, 3, 5)]
        public void SwitchingSanitizesCapacityOwnershipAndOffers(int arena, int suits, int districts)
        {
            var profile = Profile();
            LoadoutPresets.Migrate(ref profile);
            var saved = LoadoutData.CreateEmpty(2);
            saved.suitIDs = new[] { "suit_scout", "suit_medic", "suit_guardian" };
            saved.districtIDs = new[] { "node_pier", "node_infirmary", "node_farm", "node_mine", "node_village" };
            profile.loadoutPresets[1] = LoadoutRecord.From(saved);
            var applied = LoadoutPresets.Switch(ref profile, 1, arena,
                id => id != "suit_medic" && LoadoutRules.IsSuitOffered(id, arena),
                id => id != "node_pier" && LoadoutRules.IsDistrictOffered(id, arena));
            Assert.AreEqual(suits, applied.suitIDs.Length);
            Assert.AreEqual(districts, applied.districtIDs.Length);
            Assert.IsEmpty(applied.suitIDs[1]);
            Assert.IsEmpty(applied.districtIDs[0]);
            Assert.AreEqual("node_infirmary", applied.districtIDs[1]);
        }

        [TestCase(false, false, true)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        public void SuitReplacementRechecksGates(bool busy, bool denied, bool expected)
        {
            var chosen = LoadoutData.CreateEmpty(1);
            chosen.suitIDs[0] = "suit_scout";
            chosen.suitIDs[1] = "suit_medic";
            var model = new LoadoutScreenModel(chosen);
            model.Preview("suit_guardian");
            Assert.IsTrue(model.BeginSuitReplace(true, false));
            Assert.IsTrue(model.ReplacingSuit);
            Assert.AreEqual(expected, model.CompleteSuitReplace(1, !denied, busy));
            Assert.IsFalse(model.Replacing);
            Assert.AreEqual(expected ? "suit_guardian" : "suit_medic", model.ToData().suitIDs[1]);
        }

        [Test]
        public void SuitReplacementCancelAndChangedPreviewCannotEquip()
        {
            var chosen = LoadoutData.CreateEmpty(1);
            chosen.suitIDs[0] = "suit_scout";
            chosen.suitIDs[1] = "suit_medic";
            var model = new LoadoutScreenModel(chosen);
            model.Preview("suit_guardian");
            Assert.IsFalse(model.BeginSuitReplace(true, true));
            Assert.IsTrue(model.BeginSuitReplace(true, false));
            model.CancelReplace();
            Assert.IsFalse(model.CompleteSuitReplace(0, true, false));
            Assert.IsTrue(model.BeginSuitReplace(true, false));
            model.Preview("suit_scout");
            Assert.IsFalse(model.CompleteSuitReplace(0, true, false));
            Assert.IsFalse(model.BeginSuitReplace(true, false));
        }

        private static LoadoutTileOrder Tile(string id, string name = "A", string role = "A", bool owned = true, int era = 1)
            => new LoadoutTileOrder(id, name, role, owned, era);

        [Test]
        public void OwnedFirstThenEraThenNameThenId()
        {
            Assert.Less(LoadoutBrowse.Compare(Tile("z", owned: true, era: 9), Tile("a", owned: false), LoadoutSort.OwnedFirst), 0);
            Assert.Less(LoadoutBrowse.Compare(Tile("z", name: "Z"), Tile("a", era: 2), LoadoutSort.OwnedFirst), 0);
            Assert.Less(LoadoutBrowse.Compare(Tile("z"), Tile("a", name: "B"), LoadoutSort.OwnedFirst), 0);
            Assert.Less(LoadoutBrowse.Compare(Tile("a"), Tile("b"), LoadoutSort.OwnedFirst), 0);
        }

        [TestCase(LoadoutSort.Name)]
        [TestCase(LoadoutSort.Role)]
        public void OtherSortsHaveIdTiebreaker(LoadoutSort sort)
        {
            Assert.Less(LoadoutBrowse.Compare(Tile("a"), Tile("b"), sort), 0);
            Assert.Greater(LoadoutBrowse.Compare(Tile("b"), Tile("a"), sort), 0);
            Assert.AreEqual(0, LoadoutBrowse.Compare(Tile("a"), Tile("a"), sort));
        }

        [Test]
        public void NameAndRoleIgnoreOwnership()
        {
            Assert.Less(LoadoutBrowse.Compare(Tile("z", name: "A", owned: false), Tile("a", name: "B"), LoadoutSort.Name), 0);
            Assert.Less(LoadoutBrowse.Compare(Tile("z", role: "A", owned: false), Tile("a", role: "B"), LoadoutSort.Role), 0);
            Assert.Less(LoadoutBrowse.Compare(Tile("z", era: 1), Tile("a", era: 2), LoadoutSort.Role), 0);
        }

        [TestCase(false, false, false)]
        [TestCase(false, true, true)]
        [TestCase(true, false, true)]
        [TestCase(true, true, true)]
        public void OwnedFilter(bool all, bool owned, bool included)
            => Assert.AreEqual(included, LoadoutBrowse.Includes(all, owned));
    }
}
