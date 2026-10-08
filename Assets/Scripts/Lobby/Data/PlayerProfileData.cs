namespace NodeWar.Lobby
{
    /// <summary>
    /// What the player's profile file stores. The field names are its JSON keys (JsonUtility
    /// writes them verbatim) and are frozen; new members only add keys. No UnityEngine, so the
    /// key set is testable without the Editor.
    /// </summary>
    [System.Serializable]
    public struct PlayerProfileData
    {
        public string username;
        public string uuid;
        public int trophies;
        public LoadoutRecord loadout;
        public string[] unlockedSuitIDs;
        // The key is frozen: every save on disk spells the district unlocks this way.
        // Code reads and writes them through UnlockedDistrictIDs.
        public string[] unlockedNodeIDs;
        public int selectedGameModeIndex; // cast to GameMode
        public int boxesAvailable;
        public float boxProgress;

        // Which Workshop tab was open last. 0 is Districts, which is also
        // what an older save without this field deserialises to - so the
        // requested default costs no migration.
        public int workshopTabIndex;

        // Older saves omit this field and start false: the reminder has
        // not been shown on this device yet.
        public bool accountLinkPromptShown;

        // The Settings page's values. Unlike workshopTabIndex this one
        // cannot lean on zero being the wanted default - every slider at 0
        // and every switch off is a state a player can legitimately choose.
        // GameSettingsData.version carries that distinction; Load() runs
        // the block through Normalized, which turns an absent one into the
        // defaults and rewrites the file.
        public GameSettingsData settings;

        public string[] UnlockedDistrictIDs
        {
            get { return unlockedNodeIDs; }
            set { unlockedNodeIDs = value; }
        }
    }
}
