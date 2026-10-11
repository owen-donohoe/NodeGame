using System.Collections.Generic;
using NodeWar.Backend;
using NodeWar.Simulation;

namespace NodeWar.Lobby
{
    /// <summary>One effective draft piece, retaining its source for draft presentation.</summary>
    public struct DraftPoolEntry
    {
        public DistrictType DistrictType;
        public bool IsFromLoadout;
    }

    /// <summary>Resolves each player's canonical loadout without changing the authored board.</summary>
    public static class LoadoutMatchSetup
    {
        public static bool GetsFreeGrants(LoadoutData loadout)
        {
            loadout = Sanitize(loadout);
            return IsLow(loadout);
        }

        public static DraftPoolEntry[] DistrictPool(LoadoutData loadout, DistrictType[] mapBasePool)
        {
            loadout = Sanitize(loadout);
            var pool = new List<DraftPoolEntry>();
            if (IsLow(loadout) && mapBasePool != null)
                for (int i = 0; i < mapBasePool.Length; i++)
                    if (mapBasePool[i] != DistrictType.None)
                        pool.Add(new DraftPoolEntry { DistrictType = mapBasePool[i], IsFromLoadout = false });
            for (int i = 0; i < loadout.districtIDs.Length; i++)
                if (!string.IsNullOrEmpty(loadout.districtIDs[i]))
                    pool.Add(new DraftPoolEntry { DistrictType = LoadoutTypes.DistrictForLobbyId(loadout.districtIDs[i]),
                        IsFromLoadout = true });
            return pool.ToArray();
        }

        public static int[] DraftedSuits(LoadoutData loadout)
        {
            loadout = Sanitize(loadout);
            var suits = new List<int>();
            bool low = IsLow(loadout);
            if (low) suits.Add((int)SuitType.Warrior);
            for (int i = 0; i < loadout.suitIDs.Length; i++)
            {
                if (string.IsNullOrEmpty(loadout.suitIDs[i])) continue;
                SuitType type = LoadoutTypes.SuitForLobbyId(loadout.suitIDs[i]);
                if (!low || type != SuitType.Warrior) suits.Add((int)type);
            }
            return suits.ToArray();
        }

        /// <summary>Chosen types only; effective base pieces are represented by placements.</summary>
        public static int[] DraftedDistricts(LoadoutData loadout)
        {
            loadout = Sanitize(loadout);
            var districts = new List<int>();
            for (int i = 0; i < loadout.districtIDs.Length; i++)
                if (!string.IsNullOrEmpty(loadout.districtIDs[i]))
                    districts.Add((int)LoadoutTypes.DistrictForLobbyId(loadout.districtIDs[i]));
            return districts.ToArray();
        }

        private static bool IsLow(LoadoutData loadout) =>
            LoadoutCapacityPolicy.IsLowShape(loadout.suitIDs.Length, loadout.districtIDs.Length);

        // Normalize a detached copy, then keep only the first valid choice of each type.
        // Slot order and canonical shape determine identical results on both peers.
        private static LoadoutData Sanitize(LoadoutData loadout)
        {
            loadout = LoadoutData.Normalized(loadout);
            int suits = loadout.suitIDs.Length;
            int districts = loadout.districtIDs.Length;

            for (int i = 0; i < suits; i++)
            {
                if (string.IsNullOrEmpty(loadout.suitIDs[i])) continue;
                SuitType type = LoadoutTypes.SuitForLobbyId(loadout.suitIDs[i]);
                if (type == SuitType.None)
                    loadout.suitIDs[i] = "";
                for (int j = 0; j < i; j++)
                    if (type == LoadoutTypes.SuitForLobbyId(loadout.suitIDs[j]))
                        loadout.suitIDs[i] = "";
            }
            for (int i = 0; i < districts; i++)
            {
                if (string.IsNullOrEmpty(loadout.districtIDs[i])) continue;
                DistrictType type = LoadoutTypes.DistrictForLobbyId(loadout.districtIDs[i]);
                DistrictType source = (DistrictType)DistrictMigration.SourceType(loadout.districtIDs[i]);
                if (type == DistrictType.None || type == DistrictType.Core || !DistrictRoster.IsActive(source))
                    loadout.districtIDs[i] = "";
                for (int j = 0; j < i; j++)
                    if (type == LoadoutTypes.DistrictForLobbyId(loadout.districtIDs[j]))
                        loadout.districtIDs[i] = "";
            }
            return loadout;
        }
    }
}
