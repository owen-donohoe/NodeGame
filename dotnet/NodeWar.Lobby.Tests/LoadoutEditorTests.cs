using NUnit.Framework;
using NodeWar.Lobby;

namespace NodeWar.Lobby.Tests
{
    /// <summary>
    /// The Workshop's slot rules.
    ///
    /// These exist because GroupSelectionPanel expresses the same rules across
    /// five [SerializeField] slot displays, a visibility pass and a coroutine,
    /// where they cannot be tested at all. Pulling them into LoadoutEditor was
    /// what made them testable; this is the payment for that.
    ///
    /// Nothing here touches UnityEngine, which is the only reason it can run
    /// under `dotnet test`.
    /// </summary>
    [TestFixture]
    public class LoadoutEditorTests
    {
        private static LoadoutEditor Empty()
        {
            return new LoadoutEditor(LoadoutData.CreateEmpty());
        }

        // ===== SHAPE =====

        [Test]
        public void SlotCounts_ComeFromLoadoutData()
        {
            LoadoutEditor editor = Empty();

            Assert.AreEqual(LoadoutData.SuitSlots, editor.SuitSlotCount);
            Assert.AreEqual(LoadoutData.NodeSlots, editor.NodeSlotCount);
        }

        [Test]
        public void DefaultStruct_NormalisesRatherThanThrowing()
        {
            // `new LoadoutData()` leaves both arrays null. Anything reading them
            // without Normalized first would NullReference here.
            LoadoutEditor editor = new LoadoutEditor(new LoadoutData());

            Assert.AreEqual(LoadoutData.SuitSlots, editor.SuitSlotCount);
            Assert.AreEqual("", editor.SuitAt(0));
            Assert.IsFalse(editor.SuitSlotsFull);
        }

        [Test]
        public void ShortSavedLoadout_IsPaddedToCurrentSlotCount()
        {
            // A save written when the counts were smaller.
            LoadoutData legacy = new LoadoutData
            {
                suitIDs = new[] { "suit_scout" },
                nodeIDs = new string[0]
            };

            LoadoutEditor editor = new LoadoutEditor(legacy);

            Assert.AreEqual("suit_scout", editor.SuitAt(0));
            Assert.AreEqual("", editor.SuitAt(LoadoutData.SuitSlots - 1));
            Assert.AreEqual(LoadoutData.NodeSlots, editor.NodeSlotCount);
        }

        [Test]
        public void SlotAccess_OutOfRangeReturnsEmptyRatherThanThrowing()
        {
            LoadoutEditor editor = Empty();

            Assert.AreEqual("", editor.SuitAt(-1));
            Assert.AreEqual("", editor.SuitAt(99));
            Assert.AreEqual("", editor.NodeAt(99));
        }

        // ===== EQUIP =====

        [Test]
        public void EquipSuit_FillsTheFirstEmptySlot()
        {
            LoadoutEditor editor = Empty();

            Assert.AreEqual(0, editor.EquipSuit("suit_scout"));
            Assert.AreEqual(1, editor.EquipSuit("suit_medic"));

            Assert.AreEqual("suit_scout", editor.SuitAt(0));
            Assert.AreEqual("suit_medic", editor.SuitAt(1));
        }

        [Test]
        public void EquipSuit_ReusesAGapLeftByUnequipping()
        {
            LoadoutEditor editor = Empty();

            editor.EquipSuit("suit_scout");
            editor.EquipSuit("suit_medic");
            editor.ClearSuitSlot(0);

            Assert.AreEqual(0, editor.EquipSuit("suit_guardian"));
            Assert.AreEqual("suit_guardian", editor.SuitAt(0));
            Assert.AreEqual("suit_medic", editor.SuitAt(1));
        }

        [Test]
        public void EquipSuit_RefusesADuplicate()
        {
            LoadoutEditor editor = Empty();

            editor.EquipSuit("suit_scout");

            // Two slots holding one suit is one slot thrown away: the draft
            // cannot tell the difference.
            Assert.AreEqual(LoadoutEditor.NoSlot, editor.EquipSuit("suit_scout"));
            Assert.AreEqual("", editor.SuitAt(1));
        }

        [Test]
        public void EquipSuit_RefusesWhenFull()
        {
            LoadoutEditor editor = Empty();

            for (int i = 0; i < LoadoutData.SuitSlots; i++)
                editor.EquipSuit("suit_" + i);

            Assert.IsTrue(editor.SuitSlotsFull);
            Assert.AreEqual(LoadoutEditor.NoSlot, editor.EquipSuit("suit_extra"));
        }

        [Test]
        public void EquipSuit_RefusesABlankID()
        {
            LoadoutEditor editor = Empty();

            Assert.AreEqual(LoadoutEditor.NoSlot, editor.EquipSuit(null));
            Assert.AreEqual(LoadoutEditor.NoSlot, editor.EquipSuit(""));
            Assert.IsFalse(editor.SuitSlotsFull);
        }

        [Test]
        public void EquipNode_ObeysTheSameRules()
        {
            LoadoutEditor editor = Empty();

            Assert.AreEqual(0, editor.EquipNode("node_market"));
            Assert.AreEqual(LoadoutEditor.NoSlot, editor.EquipNode("node_market"));

            for (int i = 1; i < LoadoutData.NodeSlots; i++)
                editor.EquipNode("node_" + i);

            Assert.IsTrue(editor.NodeSlotsFull);
            Assert.AreEqual(LoadoutEditor.NoSlot, editor.EquipNode("node_shrine"));
        }

        // ===== UNEQUIP =====

        [Test]
        public void ClearSuitSlot_ReturnsWhatItRemoved()
        {
            LoadoutEditor editor = Empty();
            editor.EquipSuit("suit_scout");

            Assert.AreEqual("suit_scout", editor.ClearSuitSlot(0));
            Assert.AreEqual("", editor.SuitAt(0));
            Assert.IsFalse(editor.IsSuitEquipped("suit_scout"));
        }

        [Test]
        public void ClearSuitSlot_OnAnEmptyOrInvalidSlotReturnsEmpty()
        {
            LoadoutEditor editor = Empty();

            // The Workshop distinguishes these two by the return value: an
            // empty slot navigates to the list instead of unequipping.
            Assert.AreEqual("", editor.ClearSuitSlot(0));
            Assert.AreEqual("", editor.ClearSuitSlot(99));
        }

        // ===== DROPPING WHAT THE DRAFT CANNOT USE =====

        [Test]
        public void DropUnavailable_ClearsEntriesTheListNoLongerOffers()
        {
            LoadoutData saved = new LoadoutData
            {
                suitIDs = new[] { "suit_warrior", "suit_scout", "" },
                nodeIDs = new[] { "node_crossroads", "node_market" }
            };

            LoadoutEditor editor = new LoadoutEditor(saved);

            // Warrior is granted to everyone and Crossroads maps to nothing, so
            // neither is offered by the picker.
            int cleared = editor.DropUnavailable(
                suit => suit == "suit_scout",
                node => node == "node_market");

            Assert.AreEqual(2, cleared);
            Assert.AreEqual("", editor.SuitAt(0));
            Assert.AreEqual("suit_scout", editor.SuitAt(1));
            Assert.AreEqual("", editor.NodeAt(0));
            Assert.AreEqual("node_market", editor.NodeAt(1));
        }

        [Test]
        public void DropUnavailable_ReportsZeroWhenEverythingIsStillOffered()
        {
            LoadoutEditor editor = Empty();
            editor.EquipSuit("suit_scout");

            Assert.AreEqual(0, editor.DropUnavailable(suit => true, node => true));
            Assert.AreEqual("suit_scout", editor.SuitAt(0));
        }

        [Test]
        public void DropUnavailable_LeavesBlankSlotsAlone()
        {
            LoadoutEditor editor = Empty();

            // A blank slot is not "an entry that is not offered" - counting it
            // would make the caller rewrite the profile on every visit.
            Assert.AreEqual(0, editor.DropUnavailable(suit => false, node => false));
        }

        // ===== HANDING THE RESULT BACK =====

        [Test]
        public void ToLoadout_RoundTripsThroughAnotherEditor()
        {
            LoadoutEditor editor = Empty();
            editor.EquipSuit("suit_scout");
            editor.EquipNode("node_market");

            LoadoutEditor reloaded = new LoadoutEditor(editor.ToLoadout());

            Assert.AreEqual("suit_scout", reloaded.SuitAt(0));
            Assert.AreEqual("node_market", reloaded.NodeAt(0));
        }

        [Test]
        public void ToLoadout_IsACopy_NotAWindowIntoTheEditor()
        {
            LoadoutEditor editor = Empty();
            editor.EquipSuit("suit_scout");

            LoadoutData snapshot = editor.ToLoadout();
            editor.ClearSuitSlot(0);

            // PlayerProfile.SetLoadout stores what it is handed. If this were
            // the editor's own array, every later edit would rewrite the saved
            // profile behind its back.
            Assert.AreEqual("suit_scout", snapshot.suitIDs[0]);
            Assert.AreEqual("", editor.SuitAt(0));
        }
    }
}
