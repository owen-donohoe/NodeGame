using System;
using NodeWar.Lobby;
using NodeWar.Network;
using NodeWar.Simulation;
using NUnit.Framework;
namespace NodeWar.Lobby.Tests
{
    public class DistrictMigrationTests
    {
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void WorkshopDraftLoadoutMigrationAndEraRoundTrip(int era)
        {
            var type = (DistrictType)19; string id = "district.workshop.e" + era;
            Assert.IsTrue(PlacementLegality.IsDraftable(type)); Assert.IsTrue(NodeWar.Backend.DistrictMigration.IsActive(19));
            Assert.AreEqual(type, LoadoutTypes.DistrictForLobbyId(id));
            CollectionAssert.AreEqual(new[] { "node_workshop", "" }, NodeWar.Backend.DistrictMigration.Deck(new[] { id, "node_workshop" }, 2));
            var equipped = new NodeWar.Backend.EquippedRecord { Variants = new System.Collections.Generic.Dictionary<string, string> { ["district.workshop"] = id } };
            LoadoutTypes.ErasFromEquipped(equipped, out _, out var eras); Assert.AreEqual(era, eras[19]);
            var loadout = LoadoutData.CreateEmpty(); loadout.districtIDs[0] = "node_workshop"; loadout.districtEras[19] = era;
            var bytes = DraftSerializer.SerializeDraftLoadout(0, loadout); DraftSerializer.DeserializeDraftLoadout(bytes, out _, out var read);
            Assert.AreEqual("node_workshop", read.districtIDs[0]); Assert.AreEqual(era, read.districtEras[19]);
        }
        [TestCase("node_camp", "node_arsenal", "node_barracks", "")]
        [TestCase("node_watchtower", "node_farm", "", "node_farm")]
        [TestCase("district.shrine.e2", "not_node_camp_fake", "node_infirmary", "")]
        public void DeckMergeKeepsFirstAndClearsRetired(string a, string b, string x, string y)
        {
            var record = new LoadoutRecord { nodeIDs = new[] { a, b } };
            CollectionAssert.AreEqual(new[] { x, y }, record.ToLoadout().districtIDs);
            CollectionAssert.AreEqual(new[] { a, b }, record.nodeIDs, "Detached conversion must not edit source.");
        }
        [TestCase(7)]
        [TestCase(11)]
        public void CurrentPacketsRejectRetiredTypes(int retired)
        {
            byte[] packet = DraftSerializer.SerializeDraftPlacement(0, retired, 1, 1, false);
            Assert.Throws<FormatException>(() => DraftSerializer.DeserializeDraftPlacement(packet,
                out _, out _, out _, out _, out _));
            Assert.IsFalse(PlacementLegality.IsDraftable((DistrictType)retired));
            Assert.AreEqual(4, (int)LoadoutTypes.DistrictForLobbyId(
                new LoadoutRecord { nodeIDs = new[] { "node_camp", "" } }.ToLoadout().districtIDs[0]));
        }
    }
}
