using System;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Settings: a push page opened from the gear.
    ///
    /// Every row reads its value from <see cref="PlayerProfile"/> when the page
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

        private GameSettingsData current;
        private int sizeIndex;

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

        public SettingsPage(VisualTreeAsset layout)
            : base("settings-page", layout, "Settings layout missing - assign SettingsPage.uxml")
        {
            masterSlider = Root.Q<Slider>("settings-master");
            musicSlider = Root.Q<Slider>("settings-music");
            effectsSlider = Root.Q<Slider>("settings-effects");
            cameraSlider = Root.Q<Slider>("settings-camera");

            colourblindSwitch = Root.Q<LobbySwitch>("settings-colourblind");
            motionSwitch = Root.Q<LobbySwitch>("settings-motion");
            confirmSwitch = Root.Q<LobbySwitch>("settings-confirm");
            hapticsSwitch = Root.Q<LobbySwitch>("settings-haptics");
            batterySwitch = Root.Q<LobbySwitch>("settings-battery");

            sizeLabel = Root.Q<Label>("settings-size");

            BindSlider(masterSlider);
            BindSlider(musicSlider);
            BindSlider(effectsSlider);
            BindSlider(cameraSlider);

            BindSwitchRow("settings-row-colourblind", colourblindSwitch);
            BindSwitchRow("settings-row-motion", motionSwitch);
            BindSwitchRow("settings-row-confirm", confirmSwitch);
            BindSwitchRow("settings-row-haptics", hapticsSwitch);
            BindSwitchRow("settings-row-battery", batterySwitch);

            Button sizeRow = Root.Q<Button>("settings-row-size");
            if (sizeRow != null) sizeRow.clicked += CycleInterfaceSize;

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
        }

        /// <summary>Flushes whatever the sliders left outstanding.</summary>
        protected override void OnClose()
        {
            Commit();
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
            Button row = Root.Q<Button>(rowName);
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

        private void CycleInterfaceSize()
        {
            sizeIndex = GameSettingsData.NextInterfaceSize(sizeIndex);
            UpdateSizeLabel();
            OnValueChanged(commitNow: true);
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
            GameSettingsData captured = new GameSettingsData
            {
                version = GameSettingsData.CurrentVersion,

                masterVolume = ReadSlider(masterSlider, current.masterVolume),
                musicVolume = ReadSlider(musicSlider, current.musicVolume),
                effectsVolume = ReadSlider(effectsSlider, current.effectsVolume),

                colourblindMarks = ReadSwitch(colourblindSwitch, current.colourblindMarks),
                reducedMotion = ReadSwitch(motionSwitch, current.reducedMotion),
                interfaceSize = sizeIndex,

                cameraSpeed = ReadSlider(cameraSlider, current.cameraSpeed),
                confirmEachCommand = ReadSwitch(confirmSwitch, current.confirmEachCommand),

                haptics = ReadSwitch(hapticsSwitch, current.haptics),
                batterySaver = ReadSwitch(batterySwitch, current.batterySaver)
            };

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
