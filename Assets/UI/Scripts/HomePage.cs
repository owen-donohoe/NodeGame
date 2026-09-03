using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// The lobby's landing page: who you are, how many trophies you have, and
    /// the Play button.
    ///
    /// Reads PlayerProfile directly. It does not cache anything, because
    /// OnShow is the only moment the values can have changed from this page's
    /// point of view - a match result is written while the Gameplay scene is
    /// loaded, and coming back means a fresh Lobby scene anyway.
    /// </summary>
    public class HomePage : LobbyPage
    {
        private readonly Label usernameLabel;
        private readonly Label trophyLabel;
        private readonly Label modeLabel;
        private readonly Button playButton;
        private readonly Button renameButton;

        /// <summary>Raised when Play is pressed. The shell opens the popup.</summary>
        public event System.Action PlayRequested;

        /// <summary>
        /// Raised when Rename is pressed. Nothing handles this yet - the rename
        /// flow is still the uGUI RenameModal, which this stack cannot reach.
        /// Wired now so the button is not a lie, and so S3 has somewhere to
        /// attach when Profile lands.
        /// </summary>
        public event System.Action RenameRequested;

        public HomePage(VisualTreeAsset layout)
            : base(LobbyPageID.Home, Build(layout))
        {
            usernameLabel = Root.Q<Label>("home-username");
            trophyLabel = Root.Q<Label>("home-trophy-count");
            modeLabel = Root.Q<Label>("home-mode-label");
            playButton = Root.Q<Button>("home-play");
            renameButton = Root.Q<Button>("home-rename");

            if (playButton != null)
                playButton.clicked += () => { if (PlayRequested != null) PlayRequested(); };

            if (renameButton != null)
                renameButton.clicked += () => { if (RenameRequested != null) RenameRequested(); };
        }

        private static VisualElement Build(VisualTreeAsset layout)
        {
            VisualElement root = new VisualElement();
            root.name = "page-home";

            if (layout != null) layout.CloneTree(root);

            return root;
        }

        public override void OnShow()
        {
            Refresh();
        }

        public void Refresh()
        {
            PlayerProfile profile = PlayerProfile.Instance;

            if (usernameLabel != null)
                usernameLabel.text = profile != null ? profile.Username : "player";

            if (trophyLabel != null)
                trophyLabel.text = profile != null ? profile.Trophies.ToString() : "0";

            if (modeLabel != null)
                modeLabel.text = "Mode: " + DescribeMode(profile);
        }

        private static string DescribeMode(PlayerProfile profile)
        {
            if (profile == null) return "Bot";

            switch (profile.SelectedGameMode)
            {
                case GameMode.Bot: return "Bot";
                case GameMode.OneVsOne: return "1v1 Online";
                case GameMode.Testing: return "Testing board";

                // Locked short-circuits in LobbyManager and nobody has recorded
                // whether it is a placeholder or dead code, so it is described
                // rather than acted on.
                case GameMode.Locked: return "Locked";
                default: return profile.SelectedGameMode.ToString();
            }
        }
    }
}
