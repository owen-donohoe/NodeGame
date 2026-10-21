using NodeWar.Backend;

namespace NodeWar.Lobby
{
    /// <summary>Fixed items live inside the loadout; only other slots are editable.</summary>
    public static class LoadoutRules
    {
        public const int DisplaySuitSlots = 3;
        public const int DisplayDistrictSlots = 5;

        public static int ChosenSuitSlots(int arena) => LoadoutCapacityPolicy.SuitCapacity(arena);
        public static int ChosenDistrictSlots(int arena) => LoadoutCapacityPolicy.DistrictCapacity(arena);

        /// <summary>Each side is full, or has equipped every owned, offered base on that side.</summary>
        public static bool IsComplete(LoadoutData loadout, int arena, PlayerState state = null)
        {
            var missing = MissingCounts(loadout, arena, state);
            return missing.Districts == 0 && missing.Suits == 0;
        }

        /// <summary>Only empty slots with an owned, era-usable, unequipped candidate are missing.</summary>
        public static (int Districts, int Suits) MissingCounts(LoadoutData loadout, int arena, PlayerState state = null)
        {
            loadout = LocalEditor(loadout, arena).ToLoadout();
            return (SideMissing(loadout.districtIDs, false, arena, state),
                SideMissing(loadout.suitIDs, true, arena, state));
        }

        public static string MissingMessage(LoadoutData loadout, int arena, PlayerState state = null)
        {
            var missing = MissingCounts(loadout, arena, state);
            if (missing.Districts > 0 && missing.Suits > 0) return "Fill your loadout";
            if (missing.Districts > 0) return "Missing " + missing.Districts + (missing.Districts == 1 ? " district" : " districts");
            if (missing.Suits > 0) return "Missing " + missing.Suits + (missing.Suits == 1 ? " suit" : " suits");
            return "";
        }

        private static int SideMissing(string[] chosen, bool suits,
            int arena, PlayerState state)
        {
            int empty = System.Array.FindAll(chosen, string.IsNullOrEmpty).Length;
            if (empty == 0) return 0;
            int available = 0;
            int length = suits ? CatalogKeys.SuitTableLength : CatalogKeys.DistrictTableLength;
            for (int type = 1; type < length; type++)
            {
                string id = suits ? CatalogKeys.SuitLobbyId(type) : CatalogKeys.DistrictLobbyId(type);
                if (!(suits ? IsSuitOffered(id, arena) : IsDistrictOffered(id, arena)) ||
                    !UnlockStates.ForBase(id, state, arena).CanUse) continue;
                if (System.Array.IndexOf(chosen, id) < 0) available++;
            }
            return System.Math.Min(empty, available);
        }

        public static string[] LockedDistricts(int arena)
        {
            return LoadoutCapacityPolicy.LockedDistricts(arena);
        }

        public static string[] LockedSuits(int arena)
        {
            return LoadoutCapacityPolicy.LockedSuits(arena);
        }

        public static bool IsSuitOffered(string id, int arena)
        {
            foreach (int type in new[] { 3, 5, 6, 7, 8 })
                if (id == CatalogKeys.SuitLobbyId(type))
                    return System.Array.IndexOf(LockedSuits(arena), id) < 0;
            return false;
        }

        public static bool IsDistrictOffered(string id, int arena)
        {
            foreach (int type in CatalogKeys.CatalogDistrictTypes)
                if (id == CatalogKeys.DistrictLobbyId(type))
                    return System.Array.IndexOf(LockedDistricts(arena), id) < 0;
            return false;
        }

        /// <summary>Detached local editor; remote loadouts never pass through this seam.</summary>
        public static LoadoutEditor LocalEditor(LoadoutData source, int arena)
        {
            var editor = new LoadoutEditor(source, arena);
            editor.DropUnavailable(id => IsSuitOffered(id, arena), id => IsDistrictOffered(id, arena));
            return editor;
        }

        /// <summary>Ownership filtering requires a complete authoritative snapshot.</summary>
        public static LoadoutEditor OwnedEditor(LoadoutData source, int arena, PlayerState state)
        {
            var editor = new LoadoutEditor(source, arena);
            if (RememberGuard.HasInventoryState(state))
                editor.DropUnavailable(id => IsSuitOffered(id, arena) && UnlockStates.ForBase(id, state, arena).CanUse,
                    id => IsDistrictOffered(id, arena) && UnlockStates.ForBase(id, state, arena).CanUse);
            return editor;
        }
        public static int ArenaOrFallback(RankRecord rank)
        {
            return rank == null ? 1 : rank.Arena;
        }
    }
}
