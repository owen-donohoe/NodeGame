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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            instance = null;
        }

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

        public bool AccountLinkPromptShown => data.accountLinkPromptShown;

        public void MarkAccountLinkPromptShown()
        {
            if (data.accountLinkPromptShown) return;
            data.accountLinkPromptShown = true;
            Save();
        }

        /// <summary>
        /// Which Workshop tab to open on. Saved so it survives a scene reload
        /// and a relaunch - the Lobby scene is rebuilt every time you come back
        /// from a match, so anything remembered only in the page would reset
        /// constantly and never actually be remembered.
        /// </summary>
        public int WorkshopTabIndex
        {
            get => data.workshopTabIndex;
            set
            {
                if (data.workshopTabIndex == value) return;
                data.workshopTabIndex = value;
                Save();
            }
        }

        /// <summary>
        /// Boxes the player has waiting. Nothing writes this yet - the box
        /// system is unplanned and blocked on unlocks existing at all - so the
        /// Shop displays it as the unwired figure it is rather than dressing it
        /// up as a balance.
        /// </summary>
        public int BoxesAvailable => data.boxesAvailable;

        /// <summary>Progress toward the next box, 0 to 1. Also written by nothing yet.</summary>
        public float BoxProgress => data.boxProgress;

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

        /// <summary>
        /// The Settings page's values, always normalised - sliders inside 0..1
        /// and the interface size inside its range, whatever the file held.
        /// </summary>
        public GameSettingsData Settings => data.settings;

        /// <summary>
        /// Writes the settings back, skipping the disk entirely when nothing
        /// actually moved. The Settings page commits on close rather than on
        /// every slider frame, so this runs once per visit, not once per pixel.
        /// </summary>
        public void SetSettings(GameSettingsData settings)
        {
            GameSettingsData normalized = GameSettingsData.Normalized(settings);
            if (!GameSettingsData.Differ(normalized, data.settings)) return;

            data.settings = normalized;
            Save();
        }

        // Unlock gating has not shipped: every caller is deliberately told "yes".
        // Before setting this to false, ensure profile creation seeds the starter
        // set in unlockedSuitIDs/unlockedNodeIDs (CreateDefaults already seeds a
        // small set), and migrate existing saves to preserve intended access.
        // static readonly rather than const on purpose: a const true folds the
        // lookup away at compile time, and every call site then compiles with an
        // unreachable-expression warning. This way the gate reads the same and
        // flipping it is still a one-line change.
        private static readonly bool AllContentUnlocked = true;

        public bool IsSuitUnlocked(string suitID) =>
            AllContentUnlocked || ContainsUnlockedID(data.unlockedSuitIDs, suitID);

        public bool IsNodeUnlocked(string nodeID) =>
            AllContentUnlocked || ContainsUnlockedID(data.unlockedNodeIDs, nodeID);

        private static bool ContainsUnlockedID(string[] unlockedIDs, string contentID)
        {
            if (unlockedIDs == null) return false;
            for (int i = 0; i < unlockedIDs.Length; i++)
            {
                if (unlockedIDs[i] == contentID) return true;
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

                bool migrated = TryMigrateLegacyLoadout(json, ref data.loadout);
                data.loadout = LoadoutData.Normalized(data.loadout);

                // A save written before settings existed deserialises to an
                // all-zero block, which Normalized turns into the defaults.
                // Rewriting the file here makes that a one-time cost rather
                // than something re-derived on every launch.
                bool settingsAbsent = data.settings.version < GameSettingsData.CurrentVersion;
                data.settings = GameSettingsData.Normalized(data.settings);

                if (migrated)
                    Debug.Log("[PlayerProfile] Migrated flat loadout fields to arrays.");

                if (settingsAbsent)
                    Debug.Log("[PlayerProfile] Added default settings to an older save.");

                if (migrated || settingsAbsent) Save();
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
                boxProgress = 0f,
                settings = GameSettingsData.CreateDefault()
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
