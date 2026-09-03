using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Profile: who you are, how many trophies you have, and an honest account
    /// of everything the project does not track yet. Replaces ProfilePanel.
    ///
    /// Also takes over rename, which in the uGUI stack is RenameModal - a
    /// MonoBehaviour on a uGUI object that this stack cannot reach. Rather than
    /// leave HomePage.RenameRequested wired to nothing, the editor lives here
    /// and Home navigates to it. That closes the one loose end S2 left open.
    ///
    /// Validation goes through PlayerProfile.ValidateUsername rather than a
    /// second copy of the rules, so the field and the setter can never disagree
    /// about what a legal name is.
    /// </summary>
    public class ProfilePage : LobbyPage
    {
        /// <summary>
        /// The six figures ProfilePanel shows as "--". Nothing in the project
        /// writes a match result, so all six are placeholders; they are listed
        /// here rather than in the layout only because six near-identical
        /// blocks of UXML would be worse to read than one array.
        /// </summary>
        private static readonly string[] StatNames =
        {
            "Matches", "Wins", "Losses", "Win rate", "Streak", "Best"
        };

        private readonly Label usernameLabel;
        private readonly Button renameButton;

        private readonly VisualElement renameEditor;
        private readonly TextField nameField;
        private readonly Label nameError;
        private readonly Button nameSaveButton;
        private readonly Button nameCancelButton;

        private readonly Label trophyCountLabel;
        private readonly VisualElement trophyFill;
        private readonly Label trophyMinLabel;
        private readonly Label trophyMaxLabel;

        public ProfilePage(VisualTreeAsset layout)
            : base(LobbyPageID.Profile, Build(layout))
        {
            usernameLabel = Root.Q<Label>("profile-username");
            renameButton = Root.Q<Button>("profile-rename");

            renameEditor = Root.Q<VisualElement>("profile-rename-editor");
            nameField = Root.Q<TextField>("profile-name-field");
            nameError = Root.Q<Label>("profile-name-error");
            nameSaveButton = Root.Q<Button>("profile-name-save");
            nameCancelButton = Root.Q<Button>("profile-name-cancel");

            trophyCountLabel = Root.Q<Label>("profile-trophy-count");
            trophyFill = Root.Q<VisualElement>("profile-trophy-fill");
            trophyMinLabel = Root.Q<Label>("profile-trophy-min");
            trophyMaxLabel = Root.Q<Label>("profile-trophy-max");

            if (renameButton != null) renameButton.clicked += BeginRename;
            if (nameSaveButton != null) nameSaveButton.clicked += CommitRename;
            if (nameCancelButton != null) nameCancelButton.clicked += CancelRename;

            // Enter commits, which is what a one-field form should do on a
            // phone keyboard.
            if (nameField != null)
                nameField.RegisterCallback<KeyDownEvent>(OnNameFieldKeyDown);

            BuildStatGrid();
        }

        private static VisualElement Build(VisualTreeAsset layout)
        {
            VisualElement root = new VisualElement();
            root.name = "page-profile";

            if (layout != null)
            {
                layout.CloneTree(root);
            }
            else
            {
                VisualElement box = new VisualElement();
                box.AddToClassList("placeholder");
                box.style.flexGrow = 1;

                Label note = new Label("Profile layout missing - assign ProfilePage.uxml");
                note.AddToClassList("placeholder__label");

                box.Add(note);
                root.Add(box);
            }

            return root;
        }

        public override void OnShow()
        {
            Refresh();
        }

        public override void OnHide()
        {
            // Leaving the page abandons a half-typed name rather than saving it.
            CancelRename();
        }

        /// <summary>
        /// Opens the rename editor. Public because Home's Rename button routes
        /// here - the shell navigates to this page and calls this, so the button
        /// on Home lands somewhere real instead of raising an event nobody
        /// handles.
        /// </summary>
        public void BeginRename()
        {
            if (renameEditor == null) return;

            PlayerProfile profile = PlayerProfile.Instance;

            if (nameField != null)
                nameField.SetValueWithoutNotify(profile != null ? profile.Username : "");

            ShowError(null);
            renameEditor.AddToClassList("profile__rename-editor--open");

            // The field was display:none a moment ago, so it cannot take focus
            // until layout has run again.
            if (nameField != null)
                nameField.schedule.Execute(() => nameField.Focus());
        }

        private void CancelRename()
        {
            if (renameEditor == null) return;

            renameEditor.RemoveFromClassList("profile__rename-editor--open");
            ShowError(null);
        }

        private void CommitRename()
        {
            PlayerProfile profile = PlayerProfile.Instance;

            if (profile == null)
            {
                ShowError("No player profile loaded.");
                return;
            }

            // Trimmed before validating: ValidateUsername accepts spaces, so a
            // name of nothing but spaces would otherwise pass and then be
            // impossible to tell from an empty one on screen.
            string candidate = (nameField != null ? nameField.value : "").Trim();

            if (!PlayerProfile.ValidateUsername(candidate))
            {
                ShowError("1 to 16 characters: letters, numbers, spaces or underscores.");
                return;
            }

            profile.SetUsername(candidate);

            CancelRename();
            Refresh();
        }

        private void OnNameFieldKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;

            CommitRename();
            evt.StopPropagation();
        }

        private void ShowError(string message)
        {
            if (nameError == null) return;

            bool visible = !string.IsNullOrEmpty(message);

            nameError.text = visible ? message : "";
            nameError.EnableInClassList("profile__error--visible", visible);
        }

        private void Refresh()
        {
            PlayerProfile profile = PlayerProfile.Instance;

            if (usernameLabel != null)
                usernameLabel.text = profile != null ? profile.Username : "player";

            int trophies = profile != null ? profile.Trophies : 0;

            if (trophyCountLabel != null)
                trophyCountLabel.text = trophies.ToString();

            RefreshTrophyBar(trophies);
        }

        /// <summary>
        /// The same sliding 100-trophy window HomepagePanel uses, so both
        /// screens describe progress the same way.
        ///
        /// The bar is accompanied by its range in text at both ends. A bar whose
        /// only output is a length is unreadable to anyone who needs the number,
        /// and this one is the page's only real progress indicator.
        /// </summary>
        private void RefreshTrophyBar(int trophies)
        {
            TrophyBarLogic logic = new TrophyBarLogic(trophies, 100);
            float fill = logic.UpdateAndGetFill(trophies);

            if (trophyFill != null)
                trophyFill.style.width = Length.Percent(fill * 100f);

            if (trophyMinLabel != null)
                trophyMinLabel.text = logic.RangeMin.ToString();

            if (trophyMaxLabel != null)
                trophyMaxLabel.text = logic.RangeMax.ToString();
        }

        /// <summary>
        /// Six placeholder boxes, one per untracked figure. Each wears
        /// .placeholder rather than showing a plausible-looking "0", because a
        /// zero would read as a real value that happens to be zero.
        /// </summary>
        private void BuildStatGrid()
        {
            VisualElement grid = Root.Q<VisualElement>("profile-stat-grid");
            if (grid == null) return;

            for (int i = 0; i < StatNames.Length; i++)
            {
                VisualElement cell = new VisualElement();
                cell.AddToClassList("placeholder");
                cell.AddToClassList("profile__stat");

                Label value = new Label("--");
                value.AddToClassList("placeholder__label");
                value.AddToClassList("profile__stat-value");

                Label name = new Label(StatNames[i]);
                name.AddToClassList("placeholder__label");

                cell.Add(value);
                cell.Add(name);
                grid.Add(cell);
            }
        }
    }
}
