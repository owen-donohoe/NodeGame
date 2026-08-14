using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Profile panel layout:
    ///   Top-left: Username + rename button
    ///   Below username (left): 2 columns x 3 rows of stat boxes
    ///   Right side: Vertical scrollable trophy bar (200px height)
    ///   Bottom-left: Back button
    /// </summary>
    public class ProfilePanel : LobbyPanel
    {
        [Header("Username (top-left)")]
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private Button renameButton;

        [Header("Stat Boxes (2 columns x 3 rows)")]
        [SerializeField] private TextMeshProUGUI stat0Label;
        [SerializeField] private TextMeshProUGUI stat0Value;
        [SerializeField] private TextMeshProUGUI stat1Label;
        [SerializeField] private TextMeshProUGUI stat1Value;
        [SerializeField] private TextMeshProUGUI stat2Label;
        [SerializeField] private TextMeshProUGUI stat2Value;
        [SerializeField] private TextMeshProUGUI stat3Label;
        [SerializeField] private TextMeshProUGUI stat3Value;
        [SerializeField] private TextMeshProUGUI stat4Label;
        [SerializeField] private TextMeshProUGUI stat4Value;
        [SerializeField] private TextMeshProUGUI stat5Label;
        [SerializeField] private TextMeshProUGUI stat5Value;

        [Header("Trophy Bar (right side, vertical)")]
        [SerializeField] private TrophyBarDisplay trophyBarDisplay;

        [Header("Rename Modal")]
        [SerializeField] private RenameModal renameModal;

        [Header("Navigation")]
        [SerializeField] private Button backButton;

        private void Awake()
        {
            if (renameButton != null)
                renameButton.onClick.AddListener(OnRenameClicked);
            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);

            // Set stat labels (static)
            SetStatLabel(stat0Label, "Matches:");
            SetStatLabel(stat1Label, "Wins:");
            SetStatLabel(stat2Label, "Losses:");
            SetStatLabel(stat3Label, "Win Rate:");
            SetStatLabel(stat4Label, "Streak:");
            SetStatLabel(stat5Label, "Best:");
        }

        public override void OnShow()
        {
            RefreshAll();
        }

        private void RefreshAll()
        {
            PlayerProfile profile = PlayerProfile.Instance;
            if (profile == null) return;

            // Username
            usernameText.text = profile.Username;

            // Trophy bar
            trophyBarDisplay.Setup(profile.Trophies);

            // Stats (placeholder values for now)
            SetStatValue(stat0Value, "--");
            SetStatValue(stat1Value, "--");
            SetStatValue(stat2Value, "--");
            SetStatValue(stat3Value, "--");
            SetStatValue(stat4Value, "--");
            SetStatValue(stat5Value, "--");
        }

        private void SetStatLabel(TextMeshProUGUI text, string label)
        {
            if (text != null) text.text = label;
        }

        private void SetStatValue(TextMeshProUGUI text, string value)
        {
            if (text != null) text.text = value;
        }

        private void OnRenameClicked()
        {
            if (renameModal != null)
                renameModal.Open(OnRenameComplete);
        }

        private void OnRenameComplete(string newName)
        {
            PlayerProfile.Instance.SetUsername(newName);
            RefreshAll();
        }

        private void OnBackClicked()
        {
            lobbyManager.ShowPanel(PanelType.Homepage);
        }

        private void OnDestroy()
        {
            if (renameButton != null) renameButton.onClick.RemoveAllListeners();
            if (backButton != null) backButton.onClick.RemoveAllListeners();
        }
    }
}