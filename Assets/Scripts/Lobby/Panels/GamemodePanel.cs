using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NodeWar.Lobby
{
    /// <summary>
    /// 2x2 grid of mode selection buttons.
    /// Clicking a valid mode selects it, highlights it, and returns to Homepage.
    /// Locked mode is non-interactable.
    /// </summary>
    public class GameModePanel : LobbyPanel
    {
        [Header("Mode Buttons")]
        [SerializeField] private Button oneVsOneButton;
        [SerializeField] private Button botButton;
        [SerializeField] private Button testingButton;
        [SerializeField] private Button lockedButton;

        [Header("Mode Button Images (for highlight)")]
        [SerializeField] private Image oneVsOneImage;
        [SerializeField] private Image botImage;
        [SerializeField] private Image testingImage;
        [SerializeField] private Image lockedImage;

        [Header("Navigation")]
        [SerializeField] private Button backButton;

        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.16f, 0.16f, 0.23f, 1f);
        [SerializeField] private Color selectedColor = new Color(0.25f, 0.35f, 0.55f, 1f);
        [SerializeField] private Color lockedColor = new Color(0.12f, 0.12f, 0.15f, 1f);

        private void Awake()
        {
            oneVsOneButton.onClick.AddListener(() => SelectMode(GameMode.OneVsOne));
            botButton.onClick.AddListener(() => SelectMode(GameMode.Bot));
            testingButton.onClick.AddListener(() => SelectMode(GameMode.Testing));

            // Locked button does nothing
            lockedButton.interactable = false;
            lockedImage.color = lockedColor;

            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);
        }

        public override void OnShow()
        {
            RefreshHighlight();
        }

        private void SelectMode(GameMode mode)
        {
            lobbyManager.SetGameMode(mode);
            RefreshHighlight();

            // Auto-return to homepage after selection
            lobbyManager.ShowPanel(PanelType.Homepage);
        }

        private void RefreshHighlight()
        {
            GameMode current = lobbyManager.SelectedMode;

            oneVsOneImage.color = (current == GameMode.OneVsOne) ? selectedColor : normalColor;
            botImage.color = (current == GameMode.Bot) ? selectedColor : normalColor;
            testingImage.color = (current == GameMode.Testing) ? selectedColor : normalColor;
            // locked stays locked color always
        }

        private void OnBackClicked()
        {
            lobbyManager.ShowPanel(PanelType.Homepage);
        }

        private void OnDestroy()
        {
            oneVsOneButton.onClick.RemoveAllListeners();
            botButton.onClick.RemoveAllListeners();
            testingButton.onClick.RemoveAllListeners();
            if (backButton != null) backButton.onClick.RemoveAllListeners();
        }
    }
}