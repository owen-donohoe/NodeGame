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
            data.loadout = loadout;
            Save();
        }

        public bool IsSuitUnlocked(string suitID)
        {
            if (data.unlockedSuitIDs == null) return false;
            for (int i = 0; i < data.unlockedSuitIDs.Length; i++)
            {
                if (data.unlockedSuitIDs[i] == suitID) return true;
            }
            return false;
        }

        public bool IsNodeUnlocked(string nodeID)
        {
            if (data.unlockedNodeIDs == null) return false;
            for (int i = 0; i < data.unlockedNodeIDs.Length; i++)
            {
                if (data.unlockedNodeIDs[i] == nodeID) return true;
            }
            return false;
        }

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
            }
            else
            {
                CreateDefaults();
                Save();
            }
        }

        private void CreateDefaults()
        {
            string uuid = GenerateUUID();

            data = new PlayerProfileData
            {
                username = "player_" + uuid,
                uuid = uuid,
                trophies = 0,
                loadout = new LoadoutData(),
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