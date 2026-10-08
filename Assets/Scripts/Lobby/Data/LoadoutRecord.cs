namespace NodeWar.Lobby
{
    /// <summary>
    /// A loadout as the player's profile file stores it. The field names are
    /// the JSON keys on disk (JsonUtility writes them verbatim) and are frozen:
    /// a save written by any earlier build has to keep reading, and
    /// <see cref="LoadoutData"/> is free to rename its own fields because it
    /// never meets the file. <c>nodeIDs</c> holds the district choices.
    ///
    /// No UnityEngine here, so the key set is testable without the Editor.
    /// </summary>
    [System.Serializable]
    public struct LoadoutRecord
    {
        public string[] suitIDs;
        public string[] nodeIDs;
        public int[] suitEras;
        public int[] districtEras;
        public string[] skinIDs;

        public static LoadoutRecord From(LoadoutData loadout)
        {
            return new LoadoutRecord
            {
                suitIDs = loadout.suitIDs,
                nodeIDs = loadout.districtIDs,
                suitEras = loadout.suitEras,
                districtEras = loadout.districtEras,
                skinIDs = loadout.skinIDs
            };
        }

        private static int[] MigrateDistrictEras(int[] source)
        {
            var result = new int[LoadoutData.DistrictEraSlots];
            if (source == null) return result;
            for (int i = 0; i < source.Length; i++)
            {
                int target = NodeWar.Backend.DistrictMigration.CanonicalType(i);
                if (target == 0) continue;
                int era = source[i];
                if (era >= 0 && era < NodeWar.Backend.CatalogIds.EraCount && era > result[target]) result[target] = era;
            }
            return result;
        }

        public LoadoutData ToLoadout()
        {
            return new LoadoutData
            {
                suitIDs = suitIDs,
                districtIDs = NodeWar.Backend.DistrictMigration.Deck(nodeIDs, LoadoutData.DistrictSlots),
                suitEras = suitEras,
                districtEras = MigrateDistrictEras(districtEras),
                skinIDs = skinIDs
            };
        }
    }
}
