using System.Collections.Generic;
using System.IO;
using System.Linq;
using NodeWar.Lobby;
using NodeWar.Network;
using NodeWar.Simulation;
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
        [TestCase("node_camp", 4)]
        [TestCase("district.arsenal.e5", 4)]
        [TestCase("district.shrine", 16)]
        [TestCase("node_fortress", 17)]
        [TestCase("not_node_camp_fake", 0)]
        [TestCase("some_farm_variant", 0)]
        [TestCase("district.camp.e6", 0)]
        [TestCase("node_watchtower", 0)]
        public void ExactIds_RejectSubstringLookalikes(string id, int expected)
        {
            Assert.AreEqual(expected, (int)LoadoutTypes.DistrictForLobbyId(id));
        }
        // The profile file's JSON keys, frozen by every save already written.
        private static readonly string[] PersistedKeys = { "suitIDs", "nodeIDs", "suitEras", "districtEras", "skinIDs" };

        private static LoadoutData Legacy()
        {
            return new LoadoutData
            {
                suitIDs = new[] { "suit_warrior", "suit_guardian", "suit_scout" },
                districtIDs = new[] { "node_farm", "node_storehouse" },
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
            CollectionAssert.AreEqual(legacy.districtEras, normalized.districtEras.Take(legacy.districtEras.Length));
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
            Assert.AreEqual(0, back.districtEras[12], "Retired era moves to the canonical table.");
            Assert.AreEqual(3, back.districtEras[17]);
            Assert.AreEqual(0, back.districtEras[13]);
            Assert.AreEqual(1, back.districtEras[18]);
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

            // The catalog owns the base picks after migration; deck capacity is unchanged.
            BoardConfigData board = PremadeMaps.Hourglass01();
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, board.baseDraftDistrictsP0.Select(d => (int)d));
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, board.baseDraftDistrictsP1.Select(d => (int)d));

            // Keep obsolete serialized-key coverage independent of the migrated live asset.
            const string legacyYaml = "%YAML 1.1\nMonoBehaviour:\n"
                + "  baseDraftNodesP0:\n  - districtType: 1\n  - districtType: 2\n  - districtType: 3\n"
                + "  baseDraftNodesP1:\n  - districtType: 1\n  - districtType: 2\n  - districtType: 3\n";
            string[] lines = legacyYaml.Split('\n');
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, BasePicks(lines, "baseDraftNodesP0", "baseDraftDistrictsP0"));
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, BasePicks(lines, "baseDraftNodesP1", "baseDraftDistrictsP1"));

            string[] liveLines = File.ReadAllLines(Path.Combine(FindRepoRoot(), "Assets/Data/Game/Board/DefaultBoardConfig.asset"));
            CollectionAssert.Contains(liveLines.Select(line => line.Trim()), "mapId: hourglass-01");
        }

        // Every lobby ID the game has shipped, and the numbers they resolve to.
        // The numbers are literals on purpose: they are what logs, wire packets
        // and the catalog's bases were written against.
        [TestCase("node_farm", 1)]
        [TestCase("node_mine", 2)]
        [TestCase("node_village", 3)]
        [TestCase("node_barracks", 4)]
        [TestCase("node_forge", 6)]
        [TestCase("node_camp", 4)]
        [TestCase("node_shrine", 16)]
        [TestCase("node_arsenal", 4)]
        [TestCase("node_sanctuary", 16)]
        [TestCase("node_watchtower", 0)]
        [TestCase("node_rampart", 17)]
        [TestCase("node_market", 18)] [TestCase("node_storehouse", 18)]
        [TestCase("node_crossroads", 0)]
        [TestCase("NODE_RAMPART", 0)]
        [TestCase("some_farm_variant", 0)]
        [TestCase("", 0)]
        [TestCase(null, 0)]
        public void KnownIds_KeepExactMappings(string lobbyId, int district)
        {
            Assert.AreEqual(district, (int)LoadoutTypes.DistrictForLobbyId(lobbyId), lobbyId);
        }

        [TestCase("suit_warrior", 3)]
        [TestCase("suit_guardian", 5)]
        [TestCase("suit_scout", 6)]
        [TestCase("suit_berserker", 7)]
        [TestCase("suit_medic", 8)]
        [TestCase("SUIT_WARRIOR", 0)]
        [TestCase("suit_unknown", 0)]
        [TestCase(null, 0)]
        public void KnownSuitIds_KeepExactMappings(string lobbyId, int suit)
        {
            Assert.AreEqual(suit, (int)LoadoutTypes.SuitForLobbyId(lobbyId), lobbyId);
        }

        // The catalog base each simulation number hangs its variants and skins off.
        [Test]
        public void CatalogBases_KeepExactMappings()
        {
            string[] suits =
            {
                null, "suit.farmer", "suit.miner", "suit.warrior", "suit.smelter", "suit.guardian", "suit.scout",
                "suit.berserker", "suit.medic", "suit.merchant", "suit.acolyte", "suit.watcher"
            };
            string[] districts =
            {
                null, "district.farm", "district.mine", "district.village", "district.barracks", "district.core",
                "district.forge", null, null, null, null,
                null, null, null, "district.pier", "district.town", "district.infirmary", "district.fortress", "district.storehouse"
            };
            for (int i = 0; i < suits.Length; i++) Assert.AreEqual(suits[i], LoadoutTypes.CatalogBaseForSuit(i), "suit " + i);
            for (int i = 0; i < districts.Length; i++) Assert.AreEqual(districts[i], LoadoutTypes.CatalogBaseForDistrict(i), "district " + i);
            Assert.IsNull(LoadoutTypes.CatalogBaseForSuit(suits.Length));
            Assert.IsNull(LoadoutTypes.CatalogBaseForDistrict(districts.Length));
            Assert.IsNull(LoadoutTypes.CatalogBaseForSuit(-1));
            Assert.AreEqual("district.fortress", LoadoutTypes.CatalogBaseForLobbyId("node_rampart"));
            Assert.AreEqual("suit.warrior", LoadoutTypes.CatalogBaseForLobbyId("suit_warrior"));
            Assert.IsNull(LoadoutTypes.CatalogBaseForLobbyId("node_crossroads"));
        }
        // The profile file's top-level keys, frozen by every save on disk.
        // unlockedNodeIDs holds the district unlocks; code reaches it through
        // UnlockedDistrictIDs.
        [Test]
        public void ProfileFileKeys_AreFrozen()
        {
            string[] keys =
            {
                "username", "uuid", "trophies", "loadout", "unlockedSuitIDs", "unlockedNodeIDs",
                "selectedGameModeIndex", "boxesAvailable", "boxProgress", "workshopTabIndex",
                "accountLinkPromptShown", "settings"
            };
            CollectionAssert.AreEquivalent(keys, typeof(PlayerProfileData).GetFields().Select(f => f.Name));

            var data = new PlayerProfileData { UnlockedDistrictIDs = new[] { "node_watchtower", "node_market" } };
            CollectionAssert.AreEqual(new[] { "node_watchtower", "node_market" }, data.unlockedNodeIDs);
            data.unlockedNodeIDs = new[] { "node_farm" };
            CollectionAssert.AreEqual(new[] { "node_farm" }, data.UnlockedDistrictIDs);
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
