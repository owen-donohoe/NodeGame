using System;
using System.Collections.Generic;
using NodeWar.Backend;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Settings: a push page opened from the gear.
    ///
    /// Account identity and actions come from BackendServices. Preference
    /// rows read their values from <see cref="PlayerProfile"/> when the page
    /// opens and writes it back when it changes, through the profile's existing
    /// save path rather than a second one of its own.
    ///
    /// Two commit points, not one. A switch or the size row is a decision, so it
    /// is saved as it happens. A slider raises a change per drag frame, and
    /// writing the profile JSON on each of those would put a file write in a
    /// gesture loop - those mark the page dirty and are flushed when it closes.
    /// <see cref="PlayerProfile.SetSettings"/> drops a write whose values did
    /// not actually move, so a visit that changes nothing touches no disk.
    ///
    /// What the values then *do* is a separate matter, and mostly not here yet.
    /// Reduced motion is applied by LobbyUIController, which owns the root the
    /// class goes on. The rest are stored and not yet consumed: the audio rows
    /// have no system to drive - the project has no AudioMixer, AudioSource or
    /// AudioListener anywhere - and haptics, colourblind marks, camera speed,
    /// command confirmation and battery saver each need the feature they name
    /// to exist first. The footnote in SettingsPage.uxml says so, and narrows
    /// as those land.
    /// </summary>
    public class SettingsPage : LobbyPushPage
    {
        private static readonly string[] InterfaceSizes = { "Small", "Default", "Large" };

        private readonly Slider masterSlider;
        private readonly Slider musicSlider;
        private readonly Slider effectsSlider;
        private readonly Slider cameraSlider;

        private readonly LobbySwitch colourblindSwitch;
        private readonly LobbySwitch motionSwitch;
        private readonly LobbySwitch confirmSwitch;
        private readonly LobbySwitch hapticsSwitch;
        private readonly LobbySwitch batterySwitch;

        private readonly Label sizeLabel;
        private readonly Label frameCapLabel;

        // Controls live in their own overlay, opened by one button. The panel owns
        // its rows; this page owns the value and the save.
        private readonly ControlsPanel controlsPanel;

        private readonly AccountFlow accountFlow;
        private readonly Label accountStatus;
        private readonly Label accountMessage;
        private readonly Button accountLink;
        private readonly Button accountSignIn;
        private readonly Button accountSignOut;
        private readonly Button accountGuest;
        private IAccountService account;
        private int accountVisit;

        private GameSettingsData current;
        private int sizeIndex;
        private int frameCapIndex;

        /// <summary>
        /// True while saved values are being pushed into the controls. The
        /// controls raise their change events either way, and without this the
        /// act of loading would be indistinguishable from the player editing.
        /// </summary>
        private bool loading;

        /// <summary>A slider moved since the last commit.</summary>
        private bool dirty;

        /// <summary>
        /// Raised whenever a value changes, including while dragging, so the
        /// lobby can apply a setting live. This is not the save signal - see
        /// the note on commit points above.
        /// </summary>
        public event Action<GameSettingsData> Changed;

        /// <summary>
        /// The values as the page currently shows them. Lets the lobby apply
        /// the saved settings once on build, before anything has changed.
        /// </summary>
        public GameSettingsData Settings => current;

        public SettingsPage(VisualTreeAsset layout, AccountFlow accountFlow)
            : base("settings-page", layout, "Settings layout missing - assign SettingsPage.uxml")
        {
            this.accountFlow = accountFlow;
            accountStatus = NodeWar.UI.UiRequired.Q<Label>(Root, NodeWar.UI.UiRequiredNames.Settings.SettingsAccountStatus, nameof(SettingsPage));
            accountMessage = NodeWar.UI.UiRequired.Q<Label>(Root, NodeWar.UI.UiRequiredNames.Settings.SettingsAccountMessage, nameof(SettingsPage));
            accountLink = NodeWar.UI.UiRequired.Q<Button>(Root, NodeWar.UI.UiRequiredNames.Settings.SettingsAccountLink, nameof(SettingsPage));
            accountSignIn = NodeWar.UI.UiRequired.Q<Button>(Root, NodeWar.UI.UiRequiredNames.Settings.SettingsAccountSignIn, nameof(SettingsPage));
            accountSignOut = NodeWar.UI.UiRequired.Q<Button>(Root, NodeWar.UI.UiRequiredNames.Settings.SettingsAccountSignOut, nameof(SettingsPage));
            accountGuest = NodeWar.UI.UiRequired.Q<Button>(Root, NodeWar.UI.UiRequiredNames.Settings.SettingsAccountGuest, nameof(SettingsPage));
            if (accountLink != null) accountLink.clicked += async () =>
            {
                int visit = accountVisit;
                await accountFlow.LinkAsync(() => IsOpen && accountVisit == visit);
            };
            if (accountSignIn != null) accountSignIn.clicked += async () =>
            {
                int visit = accountVisit;
                await accountFlow.SignInAsync(() => IsOpen && accountVisit == visit);
            };
            if (accountSignOut != null) accountSignOut.clicked += async () => await accountFlow.SignOutAsync();
            if (accountGuest != null) accountGuest.clicked += async () => await accountFlow.EnsureAsync();

            masterSlider = NodeWar.UI.UiRequired.Q<Slider>(Root, NodeWar.UI.UiRequiredNames.Settings.SettingsMaster, nameof(SettingsPage));
            musicSlider = NodeWar.UI.UiRequired.Q<Slider>(Root, NodeWar.UI.UiRequiredNames.Settings.SettingsMusic, nameof(SettingsPage));
            effectsSlider = NodeWar.UI.UiRequired.Q<Slider>(Root, NodeWar.UI.UiRequiredNames.Settings.SettingsEffects, nameof(SettingsPage));
            cameraSlider = NodeWar.UI.UiRequired.Q<Slider>(Root, NodeWar.UI.UiRequiredNames.Settings.SettingsCamera, nameof(SettingsPage));

            colourblindSwitch = NodeWar.UI.UiRequired.Q<LobbySwitch>(Root, NodeWar.UI.UiRequiredNames.Settings.SettingsColourblind, nameof(SettingsPage));
            motionSwitch = NodeWar.UI.UiRequired.Q<LobbySwitch>(Root, NodeWar.UI.UiRequiredNames.Settings.SettingsMotion, nameof(SettingsPage));
            confirmSwitch = NodeWar.UI.UiRequired.Q<LobbySwitch>(Root, NodeWar.UI.UiRequiredNames.Settings.SettingsConfirm, nameof(SettingsPage));
            hapticsSwitch = NodeWar.UI.UiRequired.Q<LobbySwitch>(Root, NodeWar.UI.UiRequiredNames.Settings.SettingsHaptics, nameof(SettingsPage));
            batterySwitch = NodeWar.UI.UiRequired.Q<LobbySwitch>(Root, NodeWar.UI.UiRequiredNames.Settings.SettingsBattery, nameof(SettingsPage));

            sizeLabel = NodeWar.UI.UiRequired.Q<Label>(Root, NodeWar.UI.UiRequiredNames.Settings.SettingsSize, nameof(SettingsPage));
            frameCapLabel = NodeWar.UI.UiRequired.Q<Label>(Root, NodeWar.UI.UiRequiredNames.Settings.SettingsFramecap, nameof(SettingsPage));

            BindSlider(masterSlider);
            BindSlider(musicSlider);
            BindSlider(effectsSlider);
            BindSlider(cameraSlider);

            BindSwitchRow(NodeWar.UI.UiRequiredNames.Settings.SettingsRowColourblind, colourblindSwitch);
            BindSwitchRow(NodeWar.UI.UiRequiredNames.Settings.SettingsRowMotion, motionSwitch);
            BindSwitchRow(NodeWar.UI.UiRequiredNames.Settings.SettingsRowConfirm, confirmSwitch);
            BindSwitchRow(NodeWar.UI.UiRequiredNames.Settings.SettingsRowHaptics, hapticsSwitch);
            BindSwitchRow(NodeWar.UI.UiRequiredNames.Settings.SettingsRowBattery, batterySwitch);

            Button sizeRow = NodeWar.UI.UiRequired.Q<Button>(Root, NodeWar.UI.UiRequiredNames.Settings.SettingsRowSize, nameof(SettingsPage));
            if (sizeRow != null) sizeRow.clicked += CycleInterfaceSize;

            Button frameCapRow = NodeWar.UI.UiRequired.Q<Button>(Root, NodeWar.UI.UiRequiredNames.Settings.SettingsRowFramecap, nameof(SettingsPage));
            if (frameCapRow != null) frameCapRow.clicked += CycleFrameCap;

            controlsPanel = new ControlsPanel(Root, () => current, EditControls, EditHoldTime);
            Button controlsOpen = NodeWar.UI.UiRequired.Q<Button>(Root, NodeWar.UI.UiRequiredNames.Settings.SettingsRowControls, nameof(SettingsPage));
            if (controlsOpen != null)
            {
                if (controlsPanel.IsWired) controlsOpen.clicked += controlsPanel.Open;
                else controlsOpen.style.display = DisplayStyle.None;
            }


            Load();
        }

        /// <summary>
        /// Re-read on every open. The profile is the source of truth and the
        /// page outlives any one visit, so a value changed elsewhere - or a
        /// profile that finished loading after this page was built - shows up
        /// rather than being overwritten by a stale control.
        /// </summary>
        protected override void OnOpen()
        {
            Load();
            UnsubscribeAccount();
            account = BackendServices.Account;
            account.Changed += OnAccountChanged;
            accountFlow.Changed += RenderAccount;
            RenderAccount();
            _ = accountFlow.EnsureAsync();
        }

        /// <summary>Flushes whatever the sliders left outstanding.</summary>
        protected override void OnClose()
        {
            UnsubscribeAccount();
            Commit();
        }

        /// <summary>
        /// Writes out anything still outstanding without the page having to be
        /// closed first.
        ///
        /// The lobby calls this as it is torn down. Closing is the ordinary
        /// commit point, but it only happens if the player presses Back - quit
        /// from the settings page, unload the scene, or switch the UI Toolkit
        /// lobby off, and a slider moved since the last commit would otherwise
        /// be back where it started on the next launch. OnDisable is the last
        /// moment there is still somewhere to write it.
        /// </summary>
        public void Flush()
        {
            UnsubscribeAccount();
            Commit();
        }

        private void UnsubscribeAccount()
        {
            accountVisit++;
            if (account != null) accountFlow.CloseDialog();
            if (account != null) account.Changed -= OnAccountChanged;
            accountFlow.Changed -= RenderAccount;
            account = null;
        }

        private void OnAccountChanged(AccountInfo ignored)
        {
            RenderAccount();
        }

        private void RenderAccount()
        {
            AccountInfo info = BackendServices.Account.Current;
            bool guest = info.Status == AccountStatus.Guest;
            bool linked = info.Status == AccountStatus.Linked;
            if (accountStatus != null)
                accountStatus.text = guest ? "Guest — progress is saved on this device only"
                    : linked ? (string.IsNullOrEmpty(info.DisplayName) ? "Signed in" : "Signed in as " + info.DisplayName)
                    : "Signed out";
            if (accountMessage != null)
            {
                accountMessage.text = accountFlow.Busy ? accountFlow.BusyText : accountFlow.Message;
                accountMessage.style.display = string.IsNullOrEmpty(accountMessage.text)
                    ? DisplayStyle.None : DisplayStyle.Flex;
            }
            SetAccountButton(accountLink, guest);
            SetAccountButton(accountSignIn, !linked);
            SetAccountButton(accountSignOut, linked);
            SetAccountButton(accountGuest, !guest && !linked);
            if (accountSignIn != null)
                accountSignIn.text = guest ? "Sign in to another account" : "Sign in";
        }

        private void SetAccountButton(Button button, bool visible)
        {
            if (button == null) return;
            button.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            button.SetEnabled(!accountFlow.Busy);
        }

        private void Load()
        {
            PlayerProfile profile = PlayerProfile.Instance;
            current = profile != null
                ? profile.Settings
                : GameSettingsData.CreateDefault();

            // A profile that has never been saved carries version 0; normalise
            // so the controls never show a zeroed struct as if it were chosen.
            current = GameSettingsData.Normalized(current);
            sizeIndex = current.interfaceSize;
            frameCapIndex = current.frameCap;

            loading = true;

            SetSlider(masterSlider, current.masterVolume);
            SetSlider(musicSlider, current.musicVolume);
            SetSlider(effectsSlider, current.effectsVolume);
            SetSlider(cameraSlider, current.cameraSpeed);

            SetSwitch(colourblindSwitch, current.colourblindMarks);
            SetSwitch(motionSwitch, current.reducedMotion);
            SetSwitch(confirmSwitch, current.confirmEachCommand);
            SetSwitch(hapticsSwitch, current.haptics);
            SetSwitch(batterySwitch, current.batterySaver);

            UpdateSizeLabel();
            UpdateFrameCapLabel();
            controlsPanel.Close();
            controlsPanel.Render();

            loading = false;
            dirty = false;

            RaiseChanged();
        }

        /// <summary>
        /// The whole settings row is the tap target for its switch, as in the
        /// prototype - the switch itself ignores picking.
        /// </summary>
        private void BindSwitchRow(string rowName, LobbySwitch toggle)
        {
            Button row = NodeWar.UI.UiRequired.Q<Button>(Root, rowName, nameof(SettingsPage));
            if (row == null || toggle == null) return;

            row.clicked += toggle.Flip;

            // A switch is a decision rather than a gesture, so it commits now.
            toggle.Changed += _ => OnValueChanged(commitNow: true);
        }

        private void BindSlider(Slider slider)
        {
            if (slider == null) return;

            // Sliders raise this per drag frame, so they only mark dirty; the
            // write happens when the page closes.
            slider.RegisterValueChangedCallback(_ => OnValueChanged(commitNow: false));
        }

        /// <summary>
        /// A controls edit is a decision, so it commits now. The edit returns a
        /// new value with its own copy of inputBindings; see ControlsViewModel.
        /// </summary>
        private void EditControls(Func<GameSettingsData, GameSettingsData> edit)
        {
            if (loading) return;

            current = edit(current);
            controlsPanel.Render();
            OnValueChanged(commitNow: true);
        }

        /// <summary>
        /// The hold-time slider raises a change per drag frame, so like the other
        /// sliders it marks dirty and the write waits for the page to close.
        /// </summary>
        private void EditHoldTime(float seconds)
        {
            if (loading) return;

            current = ControlsViewModel.SetHoldTime(current, seconds);
            OnValueChanged(commitNow: false);
        }

        private void CycleInterfaceSize()
        {
            sizeIndex = GameSettingsData.NextInterfaceSize(sizeIndex);
            UpdateSizeLabel();
            OnValueChanged(commitNow: true);
        }

        private void CycleFrameCap()
        {
            frameCapIndex = GameSettingsData.NextFrameCap(frameCapIndex);
            UpdateFrameCapLabel();
            OnValueChanged(commitNow: true);
        }

        private void UpdateFrameCapLabel()
        {
            if (frameCapLabel != null) frameCapLabel.text = GameSettingsData.FrameCapLabel(frameCapIndex);
        }

        private void UpdateSizeLabel()
        {
            if (sizeLabel == null) return;

            int index = sizeIndex;
            if (index < 0 || index >= InterfaceSizes.Length)
                index = GameSettingsData.DefaultInterfaceSize;

            sizeLabel.text = InterfaceSizes[index];
        }

        private void OnValueChanged(bool commitNow)
        {
            if (loading) return;

            current = Capture();
            dirty = true;

            RaiseChanged();

            if (commitNow) Commit();
        }

        /// <summary>Reads the controls back into a value, clamped on the way.</summary>
        private GameSettingsData Capture()
        {
            // Carry settings edited elsewhere, including future fields.
            GameSettingsData captured = current;
            // Clone inputBindings before editing entries: this copy aliases current, hiding changes from Differ.
            captured.inputBindings = current.inputBindings != null
                ? (InputBinding[])current.inputBindings.Clone()
                : InputBindings.CreateDefault();
            captured.version = GameSettingsData.CurrentVersion;

            captured.masterVolume = ReadSlider(masterSlider, current.masterVolume);
            captured.musicVolume = ReadSlider(musicSlider, current.musicVolume);
            captured.effectsVolume = ReadSlider(effectsSlider, current.effectsVolume);

            captured.colourblindMarks = ReadSwitch(colourblindSwitch, current.colourblindMarks);
            captured.reducedMotion = ReadSwitch(motionSwitch, current.reducedMotion);
            captured.interfaceSize = sizeIndex;
            captured.frameCap = frameCapIndex;

            captured.cameraSpeed = ReadSlider(cameraSlider, current.cameraSpeed);
            captured.confirmEachCommand = ReadSwitch(confirmSwitch, current.confirmEachCommand);

            captured.haptics = ReadSwitch(hapticsSwitch, current.haptics);
            captured.batterySaver = ReadSwitch(batterySwitch, current.batterySaver);


            return GameSettingsData.Normalized(captured);
        }

        private void Commit()
        {
            if (!dirty) return;
            dirty = false;

            PlayerProfile profile = PlayerProfile.Instance;
            if (profile == null) return;

            profile.SetSettings(current);
        }

        private void RaiseChanged()
        {
            if (Changed != null) Changed(current);
        }

        private static void SetSlider(Slider slider, float value)
        {
            if (slider != null) slider.value = value;
        }

        private static void SetSwitch(LobbySwitch toggle, bool value)
        {
            if (toggle != null) toggle.Value = value;
        }

        private static float ReadSlider(Slider slider, float fallback)
        {
            return slider != null ? slider.value : fallback;
        }

        private static bool ReadSwitch(LobbySwitch toggle, bool fallback)
        {
            return toggle != null ? toggle.Value : fallback;
        }
    }
}
