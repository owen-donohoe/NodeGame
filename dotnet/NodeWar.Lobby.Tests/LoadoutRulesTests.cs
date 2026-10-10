using NodeWar.Backend;
using NodeWar.Lobby;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class LoadoutRulesTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(100)]
        public void ArenaZeroAndOne_ThreeDistrictLocksAndWarrior(int arena)
        {
            CollectionAssert.AreEqual(new[] { "node_farm", "node_mine", "node_village" }, LoadoutRules.LockedDistricts(arena));
            CollectionAssert.AreEqual(new[] { "suit_warrior" }, LoadoutRules.LockedSuits(arena));
            Assert.AreEqual(5, LoadoutRules.DisplayDistrictSlots);
            Assert.AreEqual(3, LoadoutRules.DisplaySuitSlots);
            Assert.AreEqual(5, LoadoutData.DistrictSlots + LoadoutRules.LockedDistricts(arena).Length);
            Assert.AreEqual(3, LoadoutData.SuitSlots + LoadoutRules.LockedSuits(arena).Length);
        }

        [Test]
        public void MissingRank_FallsBackToOne()
        {
            Assert.AreEqual(1, LoadoutRules.ArenaOrFallback(null));
            Assert.AreEqual(0, LoadoutRules.ArenaOrFallback(new RankRecord { Arena = 0 }));
            Assert.AreEqual(7, LoadoutRules.ArenaOrFallback(new RankRecord { Arena = 7 }));
        }

        [Test]
        public void LocksNeverEnterChosenArrays()
        {
            var editor = new LoadoutEditor(LoadoutData.CreateEmpty());
            editor.EquipSuit("suit_scout");
            editor.EquipSuit("suit_medic");
            editor.EquipDistrict("node_pier");
            editor.EquipDistrict("node_infirmary");
            foreach (string id in LoadoutRules.LockedSuits(1))
                CollectionAssert.DoesNotContain(editor.ToLoadout().suitIDs, id);
            foreach (string id in LoadoutRules.LockedDistricts(1))
                CollectionAssert.DoesNotContain(editor.ToLoadout().districtIDs, id);
            editor.ClearSuitSlot(0);
            editor.ClearDistrictSlot(0);
            editor.DropUnavailable(suit => false, district => false);
            CollectionAssert.AreEqual(new[] { "", "" }, editor.ToLoadout().suitIDs);
            CollectionAssert.AreEqual(new[] { "", "" }, editor.ToLoadout().districtIDs);
            // Callers cannot change subsequent display rules through an array alias.
            LoadoutRules.LockedSuits(1)[0] = "changed";
            LoadoutRules.LockedDistricts(1)[0] = "changed";
            Assert.AreEqual("suit_warrior", LoadoutRules.LockedSuits(1)[0]);
            Assert.AreEqual("node_farm", LoadoutRules.LockedDistricts(1)[0]);
        }
    }
}
