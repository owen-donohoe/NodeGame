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
                nodeIDs = loadout.nodeIDs,
                suitEras = loadout.suitEras,
                districtEras = loadout.districtEras,
                skinIDs = loadout.skinIDs
            };
        }

        public LoadoutData ToLoadout()
        {
            return new LoadoutData
            {
                suitIDs = suitIDs,
                nodeIDs = nodeIDs,
                suitEras = suitEras,
                districtEras = districtEras,
                skinIDs = skinIDs
            };
        }
    }
}
