using UnityEngine;
using UnityEngine.SceneManagement;
using NodeWar.Core;

namespace NodeWar.Lobby
{
    public enum PanelType
    {
        Homepage,
        GameMode,
        Profile,
        Shop,
        GroupSelection
    }

    public class LobbyManager : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private HomepagePanel homepagePanel;
        [SerializeField] private LobbyPanel gameModePanel;
        [SerializeField] private LobbyPanel profilePanel;
        [SerializeField] private LobbyPanel shopPanel;
        [SerializeField] private LobbyPanel groupSelectionPanel;

        [Header("PlayerProfile Prefab (spawns if none exists)")]
        [SerializeField] private GameObject playerProfilePrefab;

        [Header("Modals")]
        [SerializeField] private NetworkingModal networkingModal;

        [Header("UI Toolkit migration")]
        [Tooltip("Show the UI Toolkit lobby instead of these uGUI panels. " +
                 "Off keeps the shipped lobby exactly as it was; this is the " +
                 "switch to flip back if the new one misbehaves. Removed in S5 " +
                 "once the uGUI panels are retired.")]
        [SerializeField] private bool useUIToolkitLobby;

        [Tooltip("Root object carrying the UIDocument and LobbyUIController. " +
                 "Created by Tools > Node War > Set Up UI Toolkit Lobby.")]
        [SerializeField] private GameObject uiToolkitLobbyRoot;

        private LobbyPanel currentPanel;

        public GameMode SelectedMode
        {
            get
            {
                if (PlayerProfile.Instance != null)
                    return PlayerProfile.Instance.SelectedGameMode;
                return GameMode.Bot;
            }
        }

        private void Awake()
        {
            Application.runInBackground = true;

            if (MatchConnection.Instance != null)
                MatchConnection.Instance.Shutdown();

            EnsurePlayerProfile();
        }

        private void Start()
        {
            RegisterPanel(homepagePanel);
            RegisterPanel(gameModePanel);
            RegisterPanel(profilePanel);
            RegisterPanel(shopPanel);
            RegisterPanel(groupSelectionPanel);

            ApplyUIStackChoice();

            // The uGUI panels stay unregistered-but-present when the new lobby
            // is live, so flipping the toggle back needs no other change.
            if (!useUIToolkitLobby)
                ShowPanel(PanelType.Homepage);
        }

        /// <summary>
        /// Turns on exactly one of the two lobby UIs.
        ///
        /// Both exist in the scene during the migration. This is the only place
        /// that decides which one the player sees, and it is deliberately a
        /// serialized bool rather than a #define or a build flag: the point of
        /// the toggle is that it can be flipped in the inspector mid-session
        /// when the new lobby does something wrong.
        /// </summary>
        private void ApplyUIStackChoice()
        {
            if (uiToolkitLobbyRoot != null)
            {
                uiToolkitLobbyRoot.SetActive(useUIToolkitLobby);
            }
            else if (useUIToolkitLobby)
            {
                Debug.LogWarning("[LobbyManager] useUIToolkitLobby is on but no " +
                                 "uiToolkitLobbyRoot is assigned. Falling back to the " +
                                 "uGUI lobby. Run Tools > Node War > Set Up UI Toolkit Lobby.");
                useUIToolkitLobby = false;
            }

            if (!useUIToolkitLobby) return;

            // Hide the uGUI panels without unregistering them.
            if (currentPanel != null)
            {
                currentPanel.OnHide();
                currentPanel.gameObject.SetActive(false);
                currentPanel = null;
            }
        }

        private void EnsurePlayerProfile()
        {
            if (PlayerProfile.Instance != null) return;

            if (playerProfilePrefab != null)
            {
                Instantiate(playerProfilePrefab);
            }
            else
            {
                GameObject go = new GameObject("PlayerProfile");
                go.AddComponent<PlayerProfile>();
            }
        }

        private void RegisterPanel(LobbyPanel panel)
        {
            if (panel == null) return;
            panel.SetManager(this);
            panel.gameObject.SetActive(false);
        }

        public void ShowPanel(PanelType type)
        {
            LobbyPanel target = GetPanel(type);
            if (target == null)
            {
                Debug.LogWarning("[LobbyManager] Panel not assigned: " + type);
                return;
            }

            if (currentPanel != null)
            {
                currentPanel.OnHide();
                currentPanel.gameObject.SetActive(false);
            }

            currentPanel = target;
            currentPanel.gameObject.SetActive(true);
            currentPanel.OnShow();
        }

        public void SetGameMode(GameMode mode)
        {
            if (mode == GameMode.Locked) return;

            if (PlayerProfile.Instance != null)
                PlayerProfile.Instance.SelectedGameMode = mode;
        }

        public void LaunchMatch()
        {
            GameMode mode = SelectedMode;

            switch (mode)
            {
                case GameMode.Bot:
                    LaunchBotMatch();
                    break;
                case GameMode.Testing:
                    LaunchTestingMatch();
                    break;
                case GameMode.OneVsOne:
                    LaunchOnlineMatch();
                    break;
            }
        }

        private void LaunchBotMatch()
        {
            GameObject mcGO = new GameObject("MatchConnection");
            MatchConnection mc = mcGO.AddComponent<MatchConnection>();
            mc.isNetworked = false;
            mc.isBotMatch = true;
            mc.localPlayerID = 0;
            mc.networkManager = null;

            if (PlayerProfile.Instance != null)
                mc.loadout = PlayerProfile.Instance.Loadout;

            SceneManager.LoadScene("Gameplay");
        }

        private void LaunchTestingMatch()
        {
            GameObject mcGO = new GameObject("MatchConnection");
            MatchConnection mc = mcGO.AddComponent<MatchConnection>();
            mc.isNetworked = false;
            mc.isBotMatch = false;
            mc.localPlayerID = 0;
            mc.networkManager = null;

            if (PlayerProfile.Instance != null)
                mc.loadout = PlayerProfile.Instance.Loadout;

            SceneManager.LoadScene("Gameplay");
        }

        private void LaunchOnlineMatch()
        {
            if (networkingModal != null)
                networkingModal.Open();
            else
                Debug.LogWarning("[LobbyManager] NetworkingModal not assigned.");
        }

        private LobbyPanel GetPanel(PanelType type)
        {
            switch (type)
            {
                case PanelType.Homepage: return homepagePanel;
                case PanelType.GameMode: return gameModePanel;
                case PanelType.Profile: return profilePanel;
                case PanelType.Shop: return shopPanel;
                case PanelType.GroupSelection: return groupSelectionPanel;
                default: return null;
            }
        }
    }
}