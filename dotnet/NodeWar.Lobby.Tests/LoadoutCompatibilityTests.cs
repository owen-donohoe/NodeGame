using System.Collections.Generic;
using System.IO;
using System.Linq;
using NodeWar.Lobby;
using NodeWar.Network;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    /// <summary>
    /// What the district/deck vocabulary rename must leave alone: the loadout's
    /// contents and order on the wire and in the profile file, the key names
    /// that file is read with, and how many choices a player gets.
    /// </summary>
    public class LoadoutCompatibilityTests
    {
        // The profile file's JSON keys, frozen by every save already written.
        private static readonly string[] PersistedKeys = { "suitIDs", "nodeIDs", "suitEras", "districtEras", "skinIDs" };

        private static LoadoutData Legacy()
        {
            return new LoadoutData
            {
                suitIDs = new[] { "suit_warrior", "suit_guardian", "suit_scout" },
                districtIDs = new[] { "node_crossroads", "node_market" },
                suitEras = new[] { 0, 1, 0, 2, 0, 0, 0, 0 },
                districtEras = new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 1 },
                skinIDs = new[] { "skin.suit.warrior.default", "skin.district.farm.gilded" }
            };
        }

        [Test]
        public void LegacyNodeIDs_RoundTripAsDistrictChoices()
        {
            LoadoutData legacy = Legacy();

            LoadoutData normalized = LoadoutData.Normalized(legacy);
            CollectionAssert.AreEqual(legacy.suitIDs, normalized.suitIDs);
            CollectionAssert.AreEqual(legacy.districtIDs, normalized.districtIDs, "No truncated or reordered district choice.");
            CollectionAssert.AreEqual(legacy.districtEras, normalized.districtEras);
            CollectionAssert.AreEqual(legacy.skinIDs, normalized.skinIDs);

            // The wire.
            byte[] packet = DraftSerializer.SerializeDraftLoadout(1, normalized);
            DraftSerializer.DeserializeDraftLoadout(packet, out int player, out LoadoutData decoded);
            Assert.AreEqual(1, player);
            CollectionAssert.AreEqual(normalized.suitIDs, decoded.suitIDs);
            CollectionAssert.AreEqual(normalized.districtIDs, decoded.districtIDs);
            CollectionAssert.AreEqual(normalized.suitEras, decoded.suitEras);
            CollectionAssert.AreEqual(normalized.districtEras, decoded.districtEras);
            CollectionAssert.AreEqual(normalized.skinIDs, decoded.skinIDs);

            // The profile file: the record carries the same contents under the same keys.
            LoadoutRecord record = LoadoutRecord.From(normalized);
            CollectionAssert.AreEquivalent(PersistedKeys, typeof(LoadoutRecord).GetFields().Select(f => f.Name));
            LoadoutData back = record.ToLoadout();
            CollectionAssert.AreEqual(normalized.suitIDs, back.suitIDs);
            CollectionAssert.AreEqual(normalized.districtIDs, back.districtIDs);
            CollectionAssert.AreEqual(normalized.suitEras, back.suitEras);
            CollectionAssert.AreEqual(normalized.districtEras, back.districtEras);
            CollectionAssert.AreEqual(normalized.skinIDs, back.skinIDs);
        }

        [Test]
        public void DeckCounts_Unchanged()
        {
            Assert.AreEqual(2, LoadoutData.DistrictSlots);
            Assert.AreEqual(3, LoadoutData.SuitSlots);
            LoadoutData empty = LoadoutData.CreateEmpty();
            Assert.AreEqual(2, empty.districtIDs.Length);
            Assert.AreEqual(3, empty.suitIDs.Length);

            // The board's base picks, read from the asset as Unity wrote it
            // (the key is renamed with the asset's next Editor save, not before).
            string[] lines = File.ReadAllLines(Path.Combine(FindRepoRoot(), "Assets/Data/Game/Board/DefaultBoardConfig.asset"));
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, BasePicks(lines, "baseDraftNodesP0", "baseDraftDistrictsP0"));
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, BasePicks(lines, "baseDraftNodesP1", "baseDraftDistrictsP1"));
        }

        private static List<int> BasePicks(string[] lines, params string[] keys)
        {
            var picks = new List<int>();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (!keys.Any(k => line == k + ":")) continue;
                for (int j = i + 1; j < lines.Length && lines[j].Trim().StartsWith("- districtType:"); j++)
                    picks.Add(int.Parse(lines[j].Trim().Substring("- districtType:".Length).Trim()));
                return picks;
            }
            Assert.Fail("No base draft list found for " + string.Join("/", keys));
            return picks;
        }

        private static string FindRepoRoot()
        {
            for (var dir = new DirectoryInfo(System.AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "Assets/Data/Game/Board/DefaultBoardConfig.asset")))
                    return dir.FullName;
            throw new FileNotFoundException("Repository root not found.");
        }
    }
}
