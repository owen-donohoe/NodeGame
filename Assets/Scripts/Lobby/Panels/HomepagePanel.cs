using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NodeWar.Lobby
{
    public class HomepagePanel : LobbyPanel
    {
        [Header("Play Section")]
        [SerializeField] private Button playButton;

        [Header("Game Mode Button")]
        [SerializeField] private Button gameModeButton;
        [SerializeField] private TextMeshProUGUI gameModeText;
        [SerializeField] private Image gameModeIcon;

        [Header("Trophy/Profile Button (top-left)")]
        [SerializeField] private Button trophyProfileButton;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Image trophyBarBackground;
        [SerializeField] private Image trophyBarMaxFill;
        [SerializeField] private Image trophyBarCurrentFill;

        [Header("Navigation Buttons")]
        [SerializeField] private Button loadoutButton;
        [SerializeField] private Button shopButton;

        [Header("Display")]
        [SerializeField] private TextMeshProUGUI titleText;

        private TrophyBarLogic trophyLogic;

        private void Awake()
        {
            playButton.onClick.AddListener(OnPlayClicked);

            if (gameModeButton != null)
                gameModeButton.onClick.AddListener(OnGameModeClicked);
            if (trophyProfileButton != null)
                trophyProfileButton.onClick.AddListener(OnProfileClicked);
            if (loadoutButton != null)
                loadoutButton.onClick.AddListener(OnLoadoutClicked);
            if (shopButton != null)
                shopButton.onClick.AddListener(OnShopClicked);
        }

        public override void OnShow()
        {
            RefreshModeDisplay();
            RefreshTrophyDisplay();
        }

        private void RefreshModeDisplay()
        {
            if (gameModeText == null || lobbyManager == null) return;

            // Read from PlayerProfile if available, else from LobbyManager
            GameMode mode = lobbyManager.SelectedMode;

            switch (mode)
            {
                case GameMode.Bot:
                    gameModeText.text = "Bot Match";
                    break;
                case GameMode.Testing:
                    gameModeText.text = "Testing";
                    break;
                case GameMode.OneVsOne:
                    gameModeText.text = "1 vs 1";
                    break;
                default:
                    gameModeText.text = "---";
                    break;
            }
        }

        private void RefreshTrophyDisplay()
        {
            PlayerProfile profile = PlayerProfile.Instance;

            if (profile != null)
            {
                if (nameText != null)
                    nameText.text = profile.Username;

                int trophies = profile.Trophies;

                if (trophyLogic == null)
                    trophyLogic = new TrophyBarLogic(trophies, 100);

                float fill = trophyLogic.UpdateAndGetFill(trophies);

                if (trophyBarCurrentFill != null)
                    trophyBarCurrentFill.fillAmount = fill;

                // Max fill: next milestone relative to range
                if (trophyBarMaxFill != null)
                {
                    if (trophies > 0)
                    {
                        int nextMilestone = ((trophies / 100) + 1) * 100;
                        float maxFill = (float)(nextMilestone - trophyLogic.RangeMin) /
                            (trophyLogic.RangeMax - trophyLogic.RangeMin);
                        trophyBarMaxFill.fillAmount = Mathf.Clamp01(maxFill);
                    }
                    else
                    {
                        trophyBarMaxFill.fillAmount = 0f;
                    }
                }
            }
            else
            {
                if (nameText != null)
                    nameText.text = "player_00000000";
                if (trophyBarCurrentFill != null)
                    trophyBarCurrentFill.fillAmount = 0f;
                if (trophyBarMaxFill != null)
                    trophyBarMaxFill.fillAmount = 0f;
            }
        }

        private void OnPlayClicked()
        {
            lobbyManager.LaunchMatch();
        }

        private void OnGameModeClicked()
        {
            lobbyManager.ShowPanel(PanelType.GameMode);
        }

        private void OnProfileClicked()
        {
            lobbyManager.ShowPanel(PanelType.Profile);
        }

        private void OnLoadoutClicked()
        {
            lobbyManager.ShowPanel(PanelType.GroupSelection);
        }

        private void OnShopClicked()
        {
            lobbyManager.ShowPanel(PanelType.Shop);
        }

        private void OnDestroy()
        {
            playButton.onClick.RemoveAllListeners();
            if (gameModeButton != null) gameModeButton.onClick.RemoveAllListeners();
            if (trophyProfileButton != null) trophyProfileButton.onClick.RemoveAllListeners();
            if (loadoutButton != null) loadoutButton.onClick.RemoveAllListeners();
            if (shopButton != null) shopButton.onClick.RemoveAllListeners();
        }
    }
}