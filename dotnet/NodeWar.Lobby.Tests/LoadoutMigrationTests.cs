using System.Linq;
using NodeWar.Lobby;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class LoadoutMigrationTests
    {
        [TestCase(1)]
        [TestCase(2)]
        public void RecordAliasMigration_PreservesStoredCapacityAndBlankPositions(int arena)
        {
            var deck = LoadoutData.CreateEmpty(arena);
            deck.suitIDs[0] = "suit_scout";
            deck.districtIDs[deck.districtIDs.Length - 1] = "node_rampart";
            var record = LoadoutRecord.From(deck);
            // Startup reads stored mode without a speculative local rank fallback.
            for (int i = 0; i < 3; i++)
                record = LoadoutRecord.From(record.ToLoadout());
            Assert.AreEqual(deck.suitIDs.Length, record.suitIDs.Length);
            Assert.AreEqual(deck.districtIDs.Length, record.nodeIDs.Length);
            Assert.AreEqual("", record.nodeIDs[0]);
            Assert.AreEqual("node_fortress", record.nodeIDs[record.nodeIDs.Length - 1]);
        }

        private static LoadoutData OldChoices()
        {
            var source = LoadoutData.CreateEmpty();
            source.suitIDs = new[] { "suit_guardian", "suit_scout", "suit_medic" };
            source.districtIDs = new[] { "node_pier", "node_infirmary" };
            source.suitEras[(int)SuitType.Medic] = 2;
            source.suitEras[(int)SuitType.Warrior] = 1;
            source.districtEras[(int)DistrictType.Pier] = 3;
            source.skinIDs = new[] { "skin.suit.medic.default" };
            return source;
        }

        [Test]
        public void ThreeSuits_KeepsFirstTwoAndPersistedKeys()
        {
            var record = LoadoutRecord.From(LoadoutData.Normalized(OldChoices()));
            CollectionAssert.AreEquivalent(new[] { "suitIDs", "nodeIDs", "suitEras", "districtEras", "skinIDs" },
                typeof(LoadoutRecord).GetFields().Select(f => f.Name));
            CollectionAssert.AreEqual(new[] { "suit_guardian", "suit_scout" }, record.ToLoadout().suitIDs);
            CollectionAssert.AreEqual(OldChoices().districtIDs, record.nodeIDs);
        }

        [Test]
        public void TrimmingChoices_RetainsTypeIndexedEquipment()
        {
            var source = OldChoices();
            var result = LoadoutData.Normalized(source);
            Assert.AreEqual(2, result.suitIDs.Length);
            CollectionAssert.AreEqual(source.suitEras, result.suitEras);
            CollectionAssert.AreEqual(source.districtEras, result.districtEras);
            CollectionAssert.AreEqual(source.districtIDs, result.districtIDs);
            CollectionAssert.AreEqual(source.skinIDs, result.skinIDs);
            CollectionAssert.AreEqual(new[] { "suit_guardian", "suit_scout", "suit_medic" }, source.suitIDs);
        }

        [Test]
        public void Normalized_IsIdempotentAndCopiesArrays()
        {
            var source = OldChoices();
            var once = LoadoutData.Normalized(source);
            var twice = LoadoutData.Normalized(once);
            CollectionAssert.AreEqual(once.suitIDs, twice.suitIDs);
            CollectionAssert.AreEqual(once.districtIDs, twice.districtIDs);
            CollectionAssert.AreEqual(once.suitEras, twice.suitEras);
            CollectionAssert.AreEqual(once.districtEras, twice.districtEras);
            CollectionAssert.AreEqual(once.skinIDs, twice.skinIDs);
            Assert.AreNotSame(once.suitIDs, twice.suitIDs);
            Assert.AreNotSame(once.districtIDs, twice.districtIDs);
            Assert.AreNotSame(once.suitEras, twice.suitEras);
            Assert.AreNotSame(once.districtEras, twice.districtEras);
            Assert.AreNotSame(once.skinIDs, twice.skinIDs);
            twice.suitIDs[0] = "changed";
            twice.districtIDs[0] = "changed";
            twice.suitEras[(int)SuitType.Medic] = 0;
            twice.districtEras[(int)DistrictType.Pier] = 0;
            twice.skinIDs[0] = "changed";
            Assert.AreEqual("suit_guardian", source.suitIDs[0]);
            Assert.AreEqual("node_pier", source.districtIDs[0]);
            Assert.AreEqual(2, source.suitEras[(int)SuitType.Medic]);
            Assert.AreEqual(3, source.districtEras[(int)DistrictType.Pier]);
            Assert.AreEqual("skin.suit.medic.default", source.skinIDs[0]);
        }
    }
}
