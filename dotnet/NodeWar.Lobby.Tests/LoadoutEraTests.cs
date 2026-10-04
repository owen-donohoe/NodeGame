using System;
using System.Collections.Generic;
using NodeWar.Backend;
using NodeWar.Network;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    /// <summary>
    /// Eras and skins on the way into a match: from the server's equipped
    /// variants into the loadout, across the draft wire to the opponent, and
    /// reconciled so no peer can field an era this build has no numbers for.
    /// </summary>
    [TestFixture]
    public class LoadoutEraTests
    {
        private static LoadoutData RoundTrip(LoadoutData source)
        {
            byte[] packet = DraftSerializer.SerializeDraftLoadout(1, source);
            DraftSerializer.DeserializeDraftLoadout(packet, out int _, out LoadoutData decoded);
            return decoded;
        }

        [Test]
        public void EraSlots_CoverEveryEnumValue()
        {
            Assert.AreEqual(Enum.GetValues(typeof(SuitType)).Length, LoadoutData.SuitEraSlots);
            Assert.AreEqual(Enum.GetValues(typeof(DistrictType)).Length, LoadoutData.DistrictEraSlots);
            Assert.AreEqual(LoadoutData.SuitEraSlots, LoadoutTypes.SuitTypeCount);
            Assert.AreEqual(LoadoutData.DistrictEraSlots, LoadoutTypes.DistrictTypeCount);
        }

        [Test]
        public void Normalized_PadsErasAndClampsUnknownOnesToZero()
        {
            LoadoutData result = LoadoutData.Normalized(new LoadoutData
            {
                suitEras = new[] { 0, 0, 0, 5, 6, -1 },
                skinIDs = new[] { "skin.suit.warrior.default", null, "" }
            });

            Assert.AreEqual(LoadoutData.SuitEraSlots, result.suitEras.Length);
            Assert.AreEqual(LoadoutData.DistrictEraSlots, result.districtEras.Length);
            Assert.AreEqual(5, result.suitEras[3]);
            Assert.AreEqual(0, result.suitEras[4], "era 6 does not exist");
            Assert.AreEqual(0, result.suitEras[5], "negative era");
            Assert.AreEqual(new[] { "skin.suit.warrior.default" }, result.skinIDs);
        }

        [Test]
        public void Wire_CarriesErasAndSkins()
        {
            var source = LoadoutData.CreateEmpty();
            source.suitIDs[0] = "suit_scout";
            source.suitEras[(int)SuitType.Scout] = 2;
            source.districtEras[(int)DistrictType.Rampart] = 4;
            source.skinIDs = new[] { "skin.suit.scout.default", "skin.district.rampart.default" };

            LoadoutData decoded = RoundTrip(source);

            Assert.AreEqual("suit_scout", decoded.suitIDs[0]);
            Assert.AreEqual(source.suitEras, decoded.suitEras);
            Assert.AreEqual(source.districtEras, decoded.districtEras);
            Assert.AreEqual(source.skinIDs, decoded.skinIDs);
        }

        [Test]
        public void Wire_APacketEndingBeforeTheEras_ReadsAsEraZero()
        {
            var source = LoadoutData.CreateEmpty();
            source.suitEras[(int)SuitType.Scout] = 2;
            byte[] packet = DraftSerializer.SerializeDraftLoadout(1, source);

            // Cut after the node IDs: what a protocol-1 peer sends.
            int cut = 1 + 4 + 1 + LoadoutData.SuitSlots + 1 + LoadoutData.NodeSlots;
            byte[] old = new byte[cut];
            Array.Copy(packet, old, cut);
            DraftSerializer.DeserializeDraftLoadout(old, out int _, out LoadoutData decoded);

            Assert.AreEqual(new int[LoadoutData.SuitEraSlots], decoded.suitEras);
            Assert.AreEqual(new string[0], decoded.skinIDs);
        }

        [Test]
        public void WithEquipment_ReadsTheServersEquippedVariantsAndSkins()
        {
            string scout = CatalogIds.SuitBase("Scout");
            string rampart = CatalogIds.DistrictBase("Rampart");
            var state = new PlayerState
            {
                Inventory = new InventoryRecord
                {
                    Equipped = new EquippedRecord
                    {
                        Variants = new Dictionary<string, string>
                        {
                            [scout] = CatalogIds.Variant(scout, 3),
                            [rampart] = CatalogIds.Variant(rampart, 1),
                            // Wrong base for the key: ignored, era 0.
                            [CatalogIds.SuitBase("Medic")] = CatalogIds.Variant(scout, 5)
                        },
                        Skins = new Dictionary<string, string>
                        {
                            [scout] = CatalogIds.DefaultSkin(scout),
                            [rampart] = CatalogIds.DefaultSkin(rampart)
                        }
                    }
                }
            };

            LoadoutData result = LoadoutTypes.WithEquipment(LoadoutData.CreateEmpty(), state);

            Assert.AreEqual(3, result.suitEras[(int)SuitType.Scout]);
            Assert.AreEqual(1, result.districtEras[(int)DistrictType.Rampart]);
            Assert.AreEqual(0, result.suitEras[(int)SuitType.Medic]);
            Assert.AreEqual(new[] { CatalogIds.DefaultSkin(rampart), CatalogIds.DefaultSkin(scout) }, result.skinIDs,
                "skins are sorted, so the wire never depends on dictionary order");
        }

        [Test]
        public void WithEquipment_WithoutAServerState_IsEraZero()
        {
            var profile = LoadoutData.CreateEmpty();
            profile.suitIDs[0] = "suit_guardian";

            LoadoutData result = LoadoutTypes.WithEquipment(profile, null);

            Assert.AreEqual("suit_guardian", result.suitIDs[0]);
            Assert.AreEqual(new int[LoadoutData.SuitEraSlots], result.suitEras);
            Assert.AreEqual(new string[0], result.skinIDs);
        }

        [Test]
        public void CatalogBase_MapsLobbyIdsToSimulationTypes()
        {
            Assert.AreEqual("suit.warrior", LoadoutTypes.CatalogBaseForLobbyId("suit_warrior"));
            Assert.AreEqual("district.rampart", LoadoutTypes.CatalogBaseForLobbyId("node_rampart"));
            Assert.IsNull(LoadoutTypes.CatalogBaseForLobbyId("node_crossroads"));
        }
    }
}
