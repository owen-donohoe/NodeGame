using System;
using UnityEngine.UIElements;
using NodeWar.Lobby;

namespace NodeWar.UI
{
    /// <summary>
    /// In-match settings: a corner card behind the gear in the bottom-right.
    ///
    /// A CARD, NOT A PAGE. Same settlement as the node sheet, for the same
    /// reason - the opponent keeps playing while this is open, so the board
    /// has to stay visible and the panel covers the least area that fits its
    /// rows. There is no dim: the scrim is invisible and exists only to catch
    /// the tap that closes it.
    ///
    /// NOTHING HERE IS DESTRUCTIVE WITHOUT A CONFIRM. Every row is a preference
    /// the player can see the effect of and reverse with a second tap, so a
    /// mis-tap while reaching for the zoom handle costs nothing. Surrender
    /// (ranked only, D18) is the exception, so it sits apart at the foot of the
    /// card and its first tap only asks. It is not a <c>GameCommand</c>: the
    /// server settles it (LeaveMatch), and the opponent hears it from theirs.
    ///
    /// ONE THUMB. The gear is in the bottom-right corner and the card rises
    /// out of it, so opening, adjusting and dismissing all happen inside the
    /// arc a right thumb covers without the hand moving.
    ///
    /// It shares <see cref="GameSettingsData"/> and the profile's save path
    /// with the lobby's Settings page rather than keeping a second copy - the
    /// two screens are the same settings seen from different places.
    /// </summary>
    public class MatchSettingsPanel
    {
        private readonly VisualElement scrim;
        private readonly VisualElement panel;
        private readonly Button gear;

        private readonly Slider musicSlider;
        private readonly Slider effectsSlider;
        private readonly LobbySwitch routesSwitch;
        private readonly LobbySwitch emotesSwitch;
        private readonly LobbyIcon emotesIcon;
        private readonly Label frameCapLabel;
        private readonly LobbySwitch cameraButtonSwitch;
        private readonly Label sideLabel;

        private readonly VisualElement surrenderArea;
        private readonly Label surrenderLine;
        private const string SurrenderQuestion = "Surrender? It counts as a loss.";
        private bool surrenderBusy;

        private GameSettingsData current;

        /// <summary>Suppresses writes while saved values are pushed into controls.</summary>
        private bool loading;

        /// <summary>A slider moved since the last commit.</summary>
        private bool dirty;

        public bool IsOpen { get; private set; }

        /// <summary>
        /// Raised on every change, including mid-drag, so the match can apply
        /// a setting live. Not the save signal - see <see cref="Commit"/>.
        /// </summary>
        public event Action<GameSettingsData> Changed;

        /// <summary>The values as the panel currently shows them.</summary>
        public GameSettingsData Settings { get { return current; } }

        /// <summary>The player confirmed a surrender. The panel waits for <see cref="SurrenderFailed"/> or the match ending.</summary>
        public event Action SurrenderConfirmed;

        public MatchSettingsPanel(VisualElement hudRoot)
        {
            scrim = hudRoot.Q<VisualElement>("hud-settings-scrim");
            panel = hudRoot.Q<VisualElement>("hud-settings");
            gear = hudRoot.Q<Button>("hud-settings-gear");

            musicSlider = hudRoot.Q<Slider>("hud-settings-music");
            effectsSlider = hudRoot.Q<Slider>("hud-settings-effects");
            routesSwitch = hudRoot.Q<LobbySwitch>("hud-settings-routes");
            emotesSwitch = hudRoot.Q<LobbySwitch>("hud-settings-emotes");
            emotesIcon = hudRoot.Q<LobbyIcon>("hud-settings-emotes-icon");
            frameCapLabel = hudRoot.Q<Label>("hud-settings-framecap");

            if (gear != null) gear.clicked += Toggle;

            // A tap anywhere off the card closes it. The card itself picks, so
            // its own taps never reach this.
            if (scrim != null)
                scrim.RegisterCallback<PointerDownEvent>(OnScrimDown);

            BindSlider(musicSlider);
            BindSlider(effectsSlider);

            Button routesRow = hudRoot.Q<Button>("hud-settings-row-routes");
            if (routesRow != null && routesSwitch != null)
            {
                routesRow.clicked += routesSwitch.Flip;

                // A switch is a decision, so it saves as it happens. Sliders
                // raise a change per drag frame and flush on close instead.
                routesSwitch.Changed += _ => OnValueChanged(commitNow: true);
            }

            Button emotesRow = hudRoot.Q<Button>("hud-settings-row-emotes");
            if (emotesRow != null && emotesSwitch != null)
            {
                emotesRow.clicked += emotesSwitch.Flip;
                emotesSwitch.Changed += _ => OnValueChanged(commitNow: true);
            }

            Button frameCapRow = hudRoot.Q<Button>("hud-settings-row-framecap");
            if (frameCapRow != null) frameCapRow.clicked += CycleFrameCap;

            // Only what undoes a bad control combination mid-match; the full
            // list is on the lobby's Settings page.
            cameraButtonSwitch = hudRoot.Q<LobbySwitch>("hud-settings-camerabutton");
            sideLabel = hudRoot.Q<Label>("hud-settings-side");
            BindControlsRow(hudRoot, "hud-settings-row-camerabutton", ControlsViewModel.ToggleCameraButton);
            BindControlsRow(hudRoot, "hud-settings-row-side", ControlsViewModel.CycleControlsSide);
            BindControlsRow(hudRoot, "hud-settings-row-controls-reset", ControlsViewModel.ResetControls);

            surrenderArea = hudRoot.Q<VisualElement>("hud-settings-surrender-area");
            surrenderLine = hudRoot.Q<Label>("hud-settings-surrender-line");
            Button ask = hudRoot.Q<Button>("hud-settings-surrender");
            Button no = hudRoot.Q<Button>("hud-settings-surrender-no");
            Button yes = hudRoot.Q<Button>("hud-settings-surrender-yes");
            if (ask != null) ask.clicked += () => SetAsking(true);
            if (no != null) no.clicked += () => SetAsking(false);
            if (yes != null) yes.clicked += ConfirmSurrender;

            Load();
        }

        /// <summary>Shows the surrender row. Only a ranked match has a server to surrender to.</summary>
        public void EnableSurrender(bool enabled)
        {
            if (surrenderArea != null) surrenderArea.EnableInClassList("hud__settings-surrender--on", enabled);
        }

        /// <summary>The server could not take the surrender. The match goes on; say why.</summary>
        public void SurrenderFailed(string message)
        {
            surrenderBusy = false;
            if (surrenderLine != null) surrenderLine.text = message;
        }

        private void SetAsking(bool asking)
        {
            if (surrenderBusy || surrenderArea == null) return;
            if (surrenderLine != null) surrenderLine.text = SurrenderQuestion;
            surrenderArea.EnableInClassList("hud__settings-surrender--asking", asking);
        }

        private void ConfirmSurrender()
        {
            if (surrenderBusy) return;
            surrenderBusy = true;
            if (surrenderLine != null) surrenderLine.text = "Surrendering…";
            SurrenderConfirmed?.Invoke();
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;

            // Re-read rather than trusting the controls: the lobby may have
            // changed these since the match started.
            Load();

            if (scrim != null) scrim.AddToClassList("hud__settings-scrim--on");
            if (panel == null) return;

            panel.AddToClassList("hud__settings--shown");

            // display:none to flex and the opacity change cannot land in one
            // frame - the element has no computed style to animate from yet,
            // so the transition is skipped and the card snaps in. One frame
            // later it has one.
            panel.schedule.Execute(() =>
            {
                if (IsOpen) panel.AddToClassList("hud__settings--on");
            }).ExecuteLater(0);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;

            Commit();

            // A question left open is not an answer: the next open starts at the ask.
            SetAsking(false);

            if (scrim != null) scrim.RemoveFromClassList("hud__settings-scrim--on");
            if (panel == null) return;

            panel.RemoveFromClassList("hud__settings--on");

            // Hold display until the fade finishes, or it vanishes instantly
            // and the transition is decorative only.
            panel.schedule.Execute(() =>
            {
                if (!IsOpen) panel.RemoveFromClassList("hud__settings--shown");
            }).ExecuteLater(160);
        }

        /// <summary>
        /// Closes without saving prompts or animation, for a match ending or
        /// the HUD being torn down underneath it.
        /// </summary>
        public void ForceClose()
        {
            // Commit before the early-out, not after it. The panel being shut
            // does not prove there is nothing outstanding, and this is the last
            // moment the HUD has somewhere to write.
            Commit();

            if (!IsOpen)
            {
                if (panel != null) panel.RemoveFromClassList("hud__settings--shown");
                return;
            }

            Close();

            if (panel != null) panel.RemoveFromClassList("hud__settings--shown");
        }

        private void OnScrimDown(PointerDownEvent evt)
        {
            Close();

            // The tap closed the panel and means nothing else. Without this it
            // also reaches the board underneath and moves villagers.
            evt.StopPropagation();
        }

        private void BindSlider(Slider slider)
        {
            if (slider == null) return;
            slider.RegisterValueChangedCallback(_ => OnValueChanged(commitNow: false));
        }

        private void Load()
        {
            PlayerProfile profile = PlayerProfile.Instance;
            current = GameSettingsData.Normalized(profile != null
                ? profile.Settings
                : GameSettingsData.CreateDefault());

            loading = true;

            if (musicSlider != null) musicSlider.value = current.musicVolume;
            if (effectsSlider != null) effectsSlider.value = current.effectsVolume;
            if (routesSwitch != null) routesSwitch.Value = current.opponentRoutes;
            if (emotesSwitch != null) emotesSwitch.Value = current.opponentEmotes;
            UpdateFrameCapLabel();
            UpdateControls();

            loading = false;
            dirty = false;

            RaiseChanged();
        }

        private void BindControlsRow(VisualElement hudRoot, string rowName,
            Func<GameSettingsData, GameSettingsData> edit)
        {
            Button row = hudRoot.Q<Button>(rowName);
            if (row == null) return;

            // A controls edit is a decision, so it saves as it happens. The edit
            // returns a value with its own inputBindings array.
            row.clicked += () =>
            {
                current = edit(current);
                UpdateControls();
                OnValueChanged(commitNow: true);
            };
        }

        private void UpdateControls()
        {
            if (cameraButtonSwitch != null) cameraButtonSwitch.Value = current.showCameraButton;
            if (sideLabel != null) sideLabel.text = ControlsViewModel.SideLabel(current.controlsSide);
        }

        private void CycleFrameCap()
        {
            current.frameCap = GameSettingsData.NextFrameCap(current.frameCap);
            UpdateFrameCapLabel();
            OnValueChanged(commitNow: true);
        }

        private void UpdateFrameCapLabel()
        {
            if (frameCapLabel != null) frameCapLabel.text = GameSettingsData.FrameCapLabel(current.frameCap);
        }

        private void OnValueChanged(bool commitNow)
        {
            if (loading) return;

            current = Capture();
            dirty = true;

            RaiseChanged();

            if (commitNow) Commit();
        }

        /// <summary>
        /// Reads the controls back over the stored value. Only the rows
        /// this panel shows are taken from controls; everything else the lobby
        /// owns is carried through untouched, so saving here never reverts a
        /// setting this screen does not display.
        /// </summary>
        private GameSettingsData Capture()
        {
            GameSettingsData captured = current;
            // Own copy of the array: this struct copy aliases current, and an alias hides edits from Differ.
            if (current.inputBindings != null)
                captured.inputBindings = (InputBinding[])current.inputBindings.Clone();
            captured.version = GameSettingsData.CurrentVersion;

            if (musicSlider != null) captured.musicVolume = musicSlider.value;
            if (effectsSlider != null) captured.effectsVolume = effectsSlider.value;
            if (routesSwitch != null) captured.opponentRoutes = routesSwitch.Value;
            if (emotesSwitch != null) captured.opponentEmotes = emotesSwitch.Value;

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
            emotesIcon?.EnableInClassList("hud__speaker--muted", !current.opponentEmotes);
            if (Changed != null) Changed(current);
        }
    }
}
