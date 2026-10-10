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
        [TestCase(0, 2, 2, true)]
        [TestCase(1, 2, 2, true)]
        [TestCase(2, 3, 5, false)]
        [TestCase(100, 3, 5, false)]
        public void Arena1And2_ExactShapesAndLocks(int arena, int suits, int districts, bool grants)
        {
            Assert.AreEqual(suits, LoadoutCapacityPolicy.SuitCapacity(arena));
            Assert.AreEqual(districts, LoadoutCapacityPolicy.DistrictCapacity(arena));
            Assert.AreEqual(grants, LoadoutCapacityPolicy.GetsFreeGrants(arena));
            Assert.AreEqual(suits, LoadoutRules.ChosenSuitSlots(arena));
            Assert.AreEqual(districts, LoadoutRules.ChosenDistrictSlots(arena));
            Assert.AreEqual(grants ? 1 : 0, LoadoutRules.LockedSuits(arena).Length);
            Assert.AreEqual(grants ? 3 : 0, LoadoutRules.LockedDistricts(arena).Length);
            Assert.AreEqual(grants, LoadoutCapacityPolicy.IsLowShape(suits, districts));
            Assert.AreEqual(!grants, LoadoutCapacityPolicy.IsHighShape(suits, districts));
        }

        [Test]
        public void DefaultOffline_AreLow()
        {
            foreach (var deck in new[] { LoadoutData.CreateEmpty(), LoadoutData.Normalized(default),
                LoadoutTypes.WithEquipment(LoadoutData.CreateEmpty(2), null) })
            {
                Assert.AreEqual(2, deck.suitIDs.Length);
                Assert.AreEqual(2, deck.districtIDs.Length);
            }
            Assert.AreEqual(3, LoadoutData.SuitSlots);
            Assert.AreEqual(5, LoadoutData.DistrictSlots);
        }

        [Test]
        public void PromotionPads_DemotionTrimsWithoutEquipmentLoss()
        {
            var low = LoadoutData.CreateEmpty(1);
            low.suitIDs = new[] { "suit_scout", "suit_medic" };
            low.districtIDs = new[] { "node_pier", "node_infirmary" };
            low.suitEras[(int)SuitType.Medic] = 2;
            low.districtEras[(int)DistrictType.Pier] = 3;
            low.skinIDs = new[] { "skin.suit.medic.default" };
            var high = LoadoutData.Normalized(low, 2);
            CollectionAssert.AreEqual(new[] { "suit_scout", "suit_medic", "" }, high.suitIDs);
            CollectionAssert.AreEqual(new[] { "node_pier", "node_infirmary", "", "", "" }, high.districtIDs);
            high.suitIDs[2] = "suit_guardian";
            high.districtIDs[4] = "node_workshop";
            var demoted = LoadoutData.Normalized(high, 1);
            CollectionAssert.AreEqual(low.suitIDs, demoted.suitIDs);
            CollectionAssert.AreEqual(low.districtIDs, demoted.districtIDs);
            CollectionAssert.AreEqual(low.suitEras, demoted.suitEras);
            CollectionAssert.AreEqual(low.districtEras, demoted.districtEras);
            CollectionAssert.AreEqual(low.skinIDs, demoted.skinIDs);
            Assert.AreEqual("", LoadoutData.Normalized(demoted, 2).suitIDs[2]);
            var equipped = LoadoutTypes.WithEquipment(low, new PlayerState { Rank = new RankRecord { Arena = 2 } });
            Assert.AreEqual(3, equipped.suitIDs.Length);
            Assert.AreEqual(5, equipped.districtIDs.Length);
        }

        [Test]
        public void LegacyThreeByTwo_IsNotHigh()
        {
            var legacy = new LoadoutData { suitIDs = new[] { "a", "b", "dropped" }, districtIDs = new[] { "", "" } };
            Assert.IsFalse(LoadoutCapacityPolicy.IsHighShape(3, 2));
            CollectionAssert.AreEqual(new[] { "a", "b" }, LoadoutData.Normalized(legacy).suitIDs);
            CollectionAssert.AreEqual(new[] { "a", "b", "" }, LoadoutData.Normalized(legacy, 2).suitIDs);
            var record = new LoadoutRecord { suitIDs = legacy.suitIDs, nodeIDs = legacy.districtIDs };
            CollectionAssert.AreEqual(new[] { "a", "b" }, record.ToLoadout().suitIDs);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void Normalize_CopiesAndPreservesShape(int arena)
        {
            var source = LoadoutData.CreateEmpty(arena);
            source.suitIDs[0] = "suit_scout";
            source.districtIDs[0] = "node_pier";
            source.suitEras[(int)SuitType.Medic] = 2;
            source.districtEras[(int)DistrictType.Pier] = 3;
            source.skinIDs = new[] { "skin.suit.scout.default" };
            var copy = LoadoutData.Normalized(source);
            CollectionAssert.AreEqual(source.suitIDs, copy.suitIDs);
            CollectionAssert.AreEqual(source.districtIDs, copy.districtIDs);
            Assert.AreNotSame(source.suitIDs, copy.suitIDs);
            Assert.AreNotSame(source.districtIDs, copy.districtIDs);
            Assert.AreNotSame(source.suitEras, copy.suitEras);
            Assert.AreNotSame(source.districtEras, copy.districtEras);
            Assert.AreNotSame(source.skinIDs, copy.skinIDs);
            for (int i = 0; i < 3; i++) copy = LoadoutRecord.From(copy).ToLoadout();
            CollectionAssert.AreEqual(source.suitIDs, copy.suitIDs);
            CollectionAssert.AreEqual(source.districtIDs, copy.districtIDs);
            CollectionAssert.AreEqual(source.skinIDs, copy.skinIDs);
            CollectionAssert.AreEqual(source.suitEras, copy.suitEras);
            CollectionAssert.AreEqual(source.districtEras, copy.districtEras);
            CollectionAssert.AreEquivalent(new[] { "suitIDs", "nodeIDs", "suitEras", "districtEras", "skinIDs" },
                typeof(LoadoutRecord).GetFields().Select(f => f.Name));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void Editor_FillClearAndDropAtEffectiveCapacity(int arena)
        {
            var editor = new LoadoutEditor(default, arena);
            for (int i = 0; i < editor.SuitSlotCount; i++) Assert.AreEqual(i, editor.EquipSuit("s" + i));
            for (int i = 0; i < editor.DistrictSlotCount; i++) Assert.AreEqual(i, editor.EquipDistrict("d" + i));
            Assert.IsTrue(editor.SuitSlotsFull);
            Assert.IsTrue(editor.DistrictSlotsFull);
            Assert.AreEqual(-1, editor.EquipSuit("extra"));
            Assert.AreEqual(-1, editor.EquipDistrict("extra"));
            int last = editor.DistrictSlotCount - 1;
            Assert.AreEqual("d" + last, editor.ClearDistrictSlot(last));
            Assert.AreEqual(last, editor.EquipDistrict("replacement"));
            var snapshot = editor.ToLoadout();
            var reloaded = new LoadoutEditor(snapshot);
            Assert.AreEqual(editor.SuitSlotCount, reloaded.SuitSlotCount);
            Assert.AreEqual(editor.DistrictSlotCount, reloaded.DistrictSlotCount);
            Assert.AreEqual(editor.SuitSlotCount + editor.DistrictSlotCount, editor.DropUnavailable(s => false, d => false));
            Assert.AreEqual(0, editor.FilledSuitCount);
            Assert.AreEqual(0, editor.FilledDistrictCount);
            Assert.IsTrue(reloaded.DistrictSlotsFull);
        }

        [TestCase(1, 0)]
        [TestCase(1, 1)]
        [TestCase(2, 0)]
        [TestCase(2, 1)]
        public void BothModes_RoundTripIncludingBlanks_RemoteShapeIsIndependentOfLocalArena(int arena, int seat)
        {
            var source = LoadoutData.CreateEmpty(arena);
            source.suitIDs[0] = "suit_scout";
            source.districtIDs[source.districtIDs.Length - 1] = "node_pier";
            source.suitEras[(int)SuitType.Medic] = 2;
            source.districtEras[(int)DistrictType.Pier] = 3;
            source.skinIDs = new[] { "skin.suit.medic.default" };
            var local = LoadoutData.CreateEmpty(arena == 1 ? 2 : 1);
            byte[] packet = DraftSerializer.SerializeDraftLoadout(seat, source);
            Assert.AreEqual(source.suitIDs.Length, packet[5]);
            Assert.IsTrue(DraftSerializer.TryDeserializeDraftLoadout(packet, out int player, out var remote));
            Assert.AreEqual(seat, player);
            CollectionAssert.AreEqual(source.suitIDs, remote.suitIDs);
            CollectionAssert.AreEqual(source.districtIDs, remote.districtIDs);
            CollectionAssert.AreEqual(source.suitEras, remote.suitEras);
            CollectionAssert.AreEqual(source.districtEras, remote.districtEras);
            CollectionAssert.AreEqual(source.skinIDs, remote.skinIDs);
            Assert.AreNotEqual(local.suitIDs.Length, LoadoutData.Normalized(remote).suitIDs.Length);
        }

        [TestCase(3, 2)]
        [TestCase(2, 5)]
        [TestCase(4, 5)]
        [TestCase(3, 6)]
        [TestCase(0, 0)]
        [TestCase(255, 255)]
        public void NoncanonicalPairs_AreRefused(int suits, int districts)
        {
            // Raw counts, with empty strings, ending at the optional equipment boundary.
            var packet = new byte[7 + suits + districts];
            packet[0] = (byte)PacketType.DraftLoadout;
            packet[5] = (byte)suits;
            packet[6 + suits] = (byte)districts;
            Assert.IsFalse(DraftSerializer.TryDeserializeDraftLoadout(packet, out int player, out _));
            Assert.AreEqual(-1, player);
            Assert.Throws<FormatException>(() => DraftSerializer.DeserializeDraftLoadout(packet, out _, out _));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(100)]
        public void IsComplete_RequiresEveryEffectiveChosenSlot(int arena)
        {
            Assert.IsFalse(LoadoutCapacityPolicy.IsComplete(default, arena));
            var deck = LoadoutData.CreateEmpty(arena);
            for (int i = 0; i < deck.suitIDs.Length; i++) deck.suitIDs[i] = "s" + i;
            for (int i = 0; i < deck.districtIDs.Length; i++) deck.districtIDs[i] = "d" + i;
            Assert.IsTrue(LoadoutCapacityPolicy.IsComplete(deck, arena));
            for (int i = 0; i < deck.suitIDs.Length; i++)
            {
                string saved = deck.suitIDs[i];
                deck.suitIDs[i] = null;
                Assert.IsFalse(LoadoutCapacityPolicy.IsComplete(deck, arena));
                deck.suitIDs[i] = saved;
            }
            for (int i = 0; i < deck.districtIDs.Length; i++)
            {
                string saved = deck.districtIDs[i];
                deck.districtIDs[i] = "";
                Assert.IsFalse(LoadoutCapacityPolicy.IsComplete(deck, arena));
                deck.districtIDs[i] = saved;
            }
            if (arena < 2) Assert.IsFalse(LoadoutCapacityPolicy.IsComplete(deck, 2));
        }
    }
}
