using NodeWar.Backend;

namespace NodeWar.Lobby
{
    /// <summary>Grants and choices form the effective loadout; only chosen slots are editable.</summary>
    public static class LoadoutRules
    {
        public const int DisplaySuitSlots = 3;
        public const int DisplayDistrictSlots = 5;

        public static int ChosenSuitSlots(int arena) => LoadoutCapacityPolicy.SuitCapacity(arena);
        public static int ChosenDistrictSlots(int arena) => LoadoutCapacityPolicy.DistrictCapacity(arena);

        /// <summary>Each side is full, or has equipped every owned, offered base on that side.</summary>
        public static bool IsComplete(LoadoutData loadout, int arena, PlayerState state = null)
        {
            loadout = LocalEditor(loadout, arena).ToLoadout();
            return SideComplete(loadout.suitIDs, LockedSuits(arena), DisplaySuitSlots, true, arena, state) &&
                SideComplete(loadout.districtIDs, LockedDistricts(arena), DisplayDistrictSlots, false, arena, state);
        }

        private static bool SideComplete(string[] chosen, string[] grants, int capacity, bool suits,
            int arena, PlayerState state)
        {
            var equipped = new System.Collections.Generic.List<string>(grants);
            foreach (string id in chosen)
                if (!string.IsNullOrEmpty(id) && !equipped.Contains(id)) equipped.Add(id);
            int filled = equipped.Count;
            if (filled >= capacity) return true;
            int length = suits ? CatalogKeys.SuitTableLength : CatalogKeys.DistrictTableLength;
            for (int type = 1; type < length; type++)
            {
                string id = suits ? CatalogKeys.SuitLobbyId(type) : CatalogKeys.DistrictLobbyId(type);
                if (!(suits ? IsSuitOffered(id, arena) : IsDistrictOffered(id, arena)) ||
                    !UnlockStates.ForBase(id, state).CanUse) continue;
                if (System.Array.IndexOf(chosen, id) < 0) return false;
            }
            return true;
        }

        public static string[] LockedDistricts(int arena)
        {
            return LoadoutCapacityPolicy.GetsFreeGrants(arena)
                ? new[] { "node_farm", "node_mine", "node_village" } : new string[0];
        }

        public static string[] LockedSuits(int arena)
        {
            return LoadoutCapacityPolicy.GetsFreeGrants(arena) ? new[] { "suit_warrior" } : new string[0];
        }

        public static bool IsSuitOffered(string id, int arena)
        {
            for (int type = 1; type < CatalogKeys.SuitTableLength; type++)
                if (id == CatalogKeys.SuitLobbyId(type))
                    return id != "suit_warrior" || !LoadoutCapacityPolicy.GetsFreeGrants(arena);
            return false;
        }

        public static bool IsDistrictOffered(string id, int arena)
        {
            foreach (int type in CatalogKeys.CatalogDistrictTypes)
                if (id == CatalogKeys.DistrictLobbyId(type))
                    return !LoadoutCapacityPolicy.GetsFreeGrants(arena) ||
                        (id != "node_farm" && id != "node_mine" && id != "node_village");
            return false;
        }

        /// <summary>Detached local editor; remote loadouts never pass through this seam.</summary>
        public static LoadoutEditor LocalEditor(LoadoutData source, int arena)
        {
            var editor = new LoadoutEditor(source, arena);
            editor.DropUnavailable(id => IsSuitOffered(id, arena), id => IsDistrictOffered(id, arena));
            return editor;
        }
        public static int ArenaOrFallback(RankRecord rank)
        {
            return rank == null ? 1 : rank.Arena;
        }
    }
}
