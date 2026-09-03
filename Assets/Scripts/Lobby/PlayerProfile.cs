using UnityEngine;
using System.IO;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Persistent player data singleton. Survives scene transitions.
    /// Local JSON persistence now, server-swappable later.
    /// </summary>
    public class PlayerProfile : MonoBehaviour
    {
        private static PlayerProfile instance;
        public static PlayerProfile Instance => instance;

        [System.Serializable]
        public struct PlayerProfileData
        {
            public string username;
            public string uuid;
            public int trophies;
            public LoadoutData loadout;
            public string[] unlockedSuitIDs;
            public string[] unlockedNodeIDs;
            public int selectedGameModeIndex; // cast to GameMode
            public int boxesAvailable;
            public float boxProgress;
        }

        public PlayerProfileData data;

        private string SavePath => Path.Combine(Application.persistentDataPath, "player_profile.json");

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Debug.Log("[PlayerProfile] Save path: " + SavePath);

            instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        // ===== PUBLIC API =====

        public string Username => data.username;
        public string UUID => data.uuid;
        public int Trophies => data.trophies;
        public GameMode SelectedGameMode
        {
            get => (GameMode)data.selectedGameModeIndex;
            set { data.selectedGameModeIndex = (int)value; Save(); }
        }
        public LoadoutData Loadout => data.loadout;

        public void SetUsername(string newName)
        {
            if (!ValidateUsername(newName)) return;
            data.username = newName;
            Save();
        }

        public void AddTrophies(int amount)
        {
            data.trophies += amount;
            if (data.trophies < 0) data.trophies = 0;
            Save();
        }

        public void SetLoadout(LoadoutData loadout)
        {
            data.loadout = LoadoutData.Normalized(loadout);
            Save();
        }
        public bool IsSuitUnlocked(string suitID) { return true; }
        public bool IsNodeUnlocked(string nodeID) { return true; }
        //public bool IsSuitUnlocked(string suitID)
        //{
        //    if (data.unlockedSuitIDs == null) return false;
        //    for (int i = 0; i < data.unlockedSuitIDs.Length; i++)
        //    {
        //        if (data.unlockedSuitIDs[i] == suitID) return true;
        //    }
        //    return false;
        //}

        //public bool IsNodeUnlocked(string nodeID)
        //{
        //    if (data.unlockedNodeIDs == null) return false;
        //    for (int i = 0; i < data.unlockedNodeIDs.Length; i++)
        //    {
        //        if (data.unlockedNodeIDs[i] == nodeID) return true;
        //    }
        //    return false;
        //}

        // ===== VALIDATION =====

        public static bool ValidateUsername(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.Length > 16) return false;

            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (!char.IsLetterOrDigit(c) && c != ' ' && c != '_')
                    return false;
            }
            return true;
        }

        // ===== PERSISTENCE =====

        public void Save()
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
        }

        public void Load()
        {
            if (File.Exists(SavePath))
            {
                string json = File.ReadAllText(SavePath);
                data = JsonUtility.FromJson<PlayerProfileData>(json);

                bool migrated = TryMigrateLegacyLoadout(json, ref data.loadout);
                data.loadout = LoadoutData.Normalized(data.loadout);

                if (migrated)
                {
                    Debug.Log("[PlayerProfile] Migrated flat loadout fields to arrays.");
                    Save();
                }
            }
            else
            {
                CreateDefaults();
                Save();
            }
        }

        // ===== LEGACY SAVE MIGRATION =====

        [System.Serializable]
        private struct LegacyLoadout
        {
            public string suitID0;
            public string suitID1;
            public string suitID2;
            public string nodeID0;
            public string nodeID1;
        }

        [System.Serializable]
        private struct LegacyProfile
        {
            public LegacyLoadout loadout;
        }

        /// <summary>
        /// Saves written before LoadoutData became array-backed carry
        /// loadout.suitID0..nodeID1. JsonUtility does not error on those — it
        /// simply leaves suitIDs/nodeIDs null, which would silently wipe the
        /// player's selection on first launch after the change. So read the old
        /// field names back and convert.
        ///
        /// The legacy shape was always 3 suits and 2 nodes; that is hardcoded
        /// here on purpose, because it describes a file format that is now
        /// fixed forever. Normalized() reconciles it with the current counts.
        ///
        /// Returns true when a migration actually happened, so the caller can
        /// rewrite the file and make it a one-time cost.
        /// </summary>
        private static bool TryMigrateLegacyLoadout(string json, ref LoadoutData loadout)
        {
            bool alreadyMigrated =
                (loadout.suitIDs != null && loadout.suitIDs.Length > 0) ||
                (loadout.nodeIDs != null && loadout.nodeIDs.Length > 0);

            if (alreadyMigrated) return false;
            if (string.IsNullOrEmpty(json)) return false;
            if (json.IndexOf("suitID0", System.StringComparison.Ordinal) < 0) return false;

            LegacyProfile legacy = JsonUtility.FromJson<LegacyProfile>(json);

            loadout = new LoadoutData
            {
                suitIDs = new string[]
                {
                    legacy.loadout.suitID0,
                    legacy.loadout.suitID1,
                    legacy.loadout.suitID2
                },
                nodeIDs = new string[]
                {
                    legacy.loadout.nodeID0,
                    legacy.loadout.nodeID1
                }
            };

            return true;
        }

        private void CreateDefaults()
        {
            string uuid = GenerateUUID();

            data = new PlayerProfileData
            {
                username = "player_" + uuid,
                uuid = uuid,
                trophies = 0,
                loadout = LoadoutData.CreateEmpty(),
                unlockedSuitIDs = new string[] { "suit_warrior", "suit_guardian" },
                unlockedNodeIDs = new string[] { "node_watchtower", "node_market" },
                selectedGameModeIndex = (int)GameMode.Bot,
                boxesAvailable = 0,
                boxProgress = 0f
            };
        }

        private string GenerateUUID()
        {
            const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";
            char[] result = new char[8];
            for (int i = 0; i < 8; i++)
            {
                result[i] = chars[Random.Range(0, chars.Length)];
            }
            return new string(result);
        }
    }
}