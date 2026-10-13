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
        /// <summary>Only complete supported IDs resolve; unknown strings are empty.</summary>
        public static SuitType SuitForLobbyId(string suitID)
        {
            for (int type = 1; type < CatalogKeys.SuitTableLength; type++)
            {
                if (suitID == CatalogKeys.SuitLobbyId(type) || suitID == CatalogKeys.SuitBase(type)) return (SuitType)type;
                for (int era = 0; era < CatalogIds.EraCount; era++)
                    if (suitID == CatalogIds.Variant(CatalogKeys.SuitBase(type), era)) return (SuitType)type;
            }
            return SuitType.None;
        }
        public static DistrictType DistrictForLobbyId(string districtID)
        {
            return (DistrictType)DistrictMigration.CanonicalType(DistrictMigration.SourceType(districtID));
        }
        /// <summary>
        /// The catalog base a lobby item's variants and skins hang off, or null
        /// for an item with no simulation type (node_crossroads).
        /// </summary>
        public static string CatalogBaseForLobbyId(string lobbyID)
        {
            SuitType suit = SuitForLobbyId(lobbyID);
            if (suit != SuitType.None) return CatalogBaseForSuit((int)suit);
            DistrictType district = DistrictForLobbyId(lobbyID);
            if (district != DistrictType.None) return CatalogBaseForDistrict((int)district);
            return null;
        }

        public const int SuitTypeCount = (int)SuitType.Minion + 1;
        public const int DistrictTypeCount = (int)DistrictType.Workshop + 1;

        public static string CatalogBaseForSuit(int type) => CatalogKeys.SuitBase(type);

        public static string CatalogBaseForDistrict(int type) => DistrictRoster.IsActive((DistrictType)type) ? CatalogKeys.DistrictBase(type) : null;

        /// <summary>The lobby ID an item is written with ("suit_warrior"); empty for a type with none.</summary>
        public static string LobbyIdForSuit(SuitType type) => CatalogKeys.SuitLobbyId((int)type) ?? "";

        /// <summary>The lobby ID a district is written with ("node_rampart", frozen); empty for a type with none.</summary>
        public static string LobbyIdForDistrict(DistrictType type) => CatalogKeys.DistrictLobbyId((int)type) ?? "";

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
                suitEras[s] = EraOf(equipped.Variants, CatalogBaseForSuit(s));
            for (int d = 1; d < DistrictTypeCount; d++)
                districtEras[d] = EraOf(equipped.Variants, CatalogBaseForDistrict(d));
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
            if (baseId == null || !variants.TryGetValue(baseId, out string variantId)) return 0;
            if (!CatalogIds.TryParseVariant(variantId, out string parsedBase, out int era)) return 0;
            if (!string.Equals(parsedBase, baseId, StringComparison.Ordinal)) return 0;
            return era >= 0 && era < GameBalanceData.EraCount ? era : 0;
        }
    }
}
