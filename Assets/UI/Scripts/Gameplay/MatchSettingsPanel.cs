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
    /// NOTHING HERE IS DESTRUCTIVE. Every row is a preference the player can
    /// see the effect of and reverse with a second tap, so a mis-tap while
    /// reaching for the zoom handle costs nothing. Surrender is not here on
    /// purpose: it is a <c>GameCommand</c> rather than a setting, and putting
    /// it one row from a volume slider is how a match gets thrown away by
    /// accident.
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

        public MatchSettingsPanel(VisualElement hudRoot)
        {
            scrim = hudRoot.Q<VisualElement>("hud-settings-scrim");
            panel = hudRoot.Q<VisualElement>("hud-settings");
            gear = hudRoot.Q<Button>("hud-settings-gear");

            musicSlider = hudRoot.Q<Slider>("hud-settings-music");
            effectsSlider = hudRoot.Q<Slider>("hud-settings-effects");
            routesSwitch = hudRoot.Q<LobbySwitch>("hud-settings-routes");
            emotesSwitch = hudRoot.Q<LobbySwitch>("hud-settings-emotes");

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

            Load();
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

            loading = false;
            dirty = false;

            RaiseChanged();
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
            if (Changed != null) Changed(current);
        }
    }
}
