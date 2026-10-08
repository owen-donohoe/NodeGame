using System;
using NodeWar.Lobby;
using NodeWar.Network;
using NodeWar.Simulation;
using NUnit.Framework;
namespace NodeWar.Lobby.Tests
{
    public class DistrictMigrationTests
    {
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
