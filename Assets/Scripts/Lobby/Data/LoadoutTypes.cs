using System;
using System.Collections.Generic;
using NodeWar.Backend;
using NodeWar.Simulation;

namespace NodeWar.Lobby
{
    /// <summary>
    /// The one translation between the lobby's item IDs ("suit_warrior",
    /// "node_rampart"), the simulation's types, and the catalog's base IDs
    /// ("suit.warrior", "district.rampart").
    ///
    /// Lobby IDs name items in loadouts and the Workshop. The catalog names
    /// what a player owns and has equipped, one base per simulation type.
    /// Everything that crosses between the two goes through here.
    /// </summary>
    public static class LoadoutTypes
    {
        /// <summary>Matched by substring, as the lobby IDs always have been.</summary>
        public static SuitType SuitForLobbyId(string suitID)
        {
            if (suitID == null) return SuitType.None;
            string lower = suitID.ToLowerInvariant();

            if (lower.Contains("warrior")) return SuitType.Warrior;
            if (lower.Contains("guardian")) return SuitType.Guardian;
            if (lower.Contains("scout")) return SuitType.Scout;
            if (lower.Contains("berserker")) return SuitType.Berserker;
            if (lower.Contains("medic")) return SuitType.Medic;

            return SuitType.None;
        }

        public static DistrictType DistrictForLobbyId(string nodeID)
        {
            if (nodeID == null) return DistrictType.None;
            string lower = nodeID.ToLowerInvariant();

            if (lower.Contains("farm")) return DistrictType.Farm;
            if (lower.Contains("mine")) return DistrictType.Mine;
            if (lower.Contains("village")) return DistrictType.Village;
            if (lower.Contains("barracks")) return DistrictType.Barracks;
            if (lower.Contains("forge")) return DistrictType.Forge;
            if (lower.Contains("camp")) return DistrictType.Camp;
            if (lower.Contains("shrine")) return DistrictType.Shrine;
            if (lower.Contains("arsenal")) return DistrictType.Arsenal;
            if (lower.Contains("sanctuary")) return DistrictType.Sanctuary;
            if (lower.Contains("watchtower")) return DistrictType.Watchtower;
            if (lower.Contains("rampart")) return DistrictType.Rampart;
            if (lower.Contains("market")) return DistrictType.Market;

            return DistrictType.None;
        }

        /// <summary>
        /// The catalog base a lobby item's variants and skins hang off, or null
        /// for an item with no simulation type (node_crossroads).
        /// </summary>
        public static string CatalogBaseForLobbyId(string lobbyID)
        {
            SuitType suit = SuitForLobbyId(lobbyID);
            if (suit != SuitType.None) return CatalogIds.SuitBase(suit.ToString());
            DistrictType district = DistrictForLobbyId(lobbyID);
            if (district != DistrictType.None) return CatalogIds.DistrictBase(district.ToString());
            return null;
        }

        public const int SuitTypeCount = (int)SuitType.Watcher + 1;
        public const int DistrictTypeCount = (int)DistrictType.Market + 1;

        /// <summary>
        /// The era of every suit and district a player has equipped, as the
        /// simulation's tables (indexed by enum value). Anything not equipped,
        /// or equipped with an ID that does not parse, plays era 0.
        /// </summary>
        public static void ErasFromEquipped(EquippedRecord equipped, out int[] suitEras, out int[] districtEras)
        {
            suitEras = new int[SuitTypeCount];
            districtEras = new int[DistrictTypeCount];
            if (equipped?.Variants == null) return;

            for (int s = 1; s < SuitTypeCount; s++)
                suitEras[s] = EraOf(equipped.Variants, CatalogIds.SuitBase(((SuitType)s).ToString()));
            for (int d = 1; d < DistrictTypeCount; d++)
                districtEras[d] = EraOf(equipped.Variants, CatalogIds.DistrictBase(((DistrictType)d).ToString()));
        }

        /// <summary>
        /// The loadout a match launches with: the player's own slot choices,
        /// plus the eras and skins the server says they have equipped. Without
        /// a known server state everything plays era 0 with no skins, the same
        /// game a brand-new player gets.
        /// </summary>
        public static LoadoutData WithEquipment(LoadoutData loadout, PlayerState state)
        {
            loadout = LoadoutData.Normalized(loadout);
            EquippedRecord equipped = state?.Inventory?.Equipped;
            ErasFromEquipped(equipped, out loadout.suitEras, out loadout.districtEras);

            var skins = new List<string>();
            if (equipped?.Skins != null)
                foreach (KeyValuePair<string, string> pair in equipped.Skins)
                    if (!string.IsNullOrEmpty(pair.Value)) skins.Add(pair.Value);
            // Dictionary order is not guaranteed; the wire and the log should
            // not depend on it.
            skins.Sort(StringComparer.Ordinal);
            loadout.skinIDs = skins.ToArray();
            return LoadoutData.Normalized(loadout);
        }

        private static int EraOf(Dictionary<string, string> variants, string baseId)
        {
            if (!variants.TryGetValue(baseId, out string variantId)) return 0;
            if (!CatalogIds.TryParseVariant(variantId, out string parsedBase, out int era)) return 0;
            if (!string.Equals(parsedBase, baseId, StringComparison.Ordinal)) return 0;
            return era >= 0 && era < GameBalanceData.EraCount ? era : 0;
        }
    }
}
