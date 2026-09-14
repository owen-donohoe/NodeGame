using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// The battle sheet: pick a mode, then start, host or join.
    ///
    /// Looks like the prototype's "Find a match" sheet. Behaviour is the same as
    /// before the reskin: local modes (Bot, Testing) are handed to LobbyManager
    /// so there is one definition of starting a local match, and networked play
    /// goes through MatchLauncher, which owns the handshake.
    ///
    /// Local network play is kept, against the prototype, which is relay-only.
    /// NetworkManager has a working DirectUDP transport and the shipped
    /// NetworkingModal already offers it; what does not exist is LAN
    /// *discovery*. Removing a working feature is not a reskin.
    /// </summary>
    public class PlayPopup
    {
        private enum View
        {
            Find,
            Join,
            Status
        }

        // Drag distance on the handle that changes detent, from the prototype.
        private const float HandleDragThreshold = 44f;

        public VisualElement Root { get; private set; }

        private readonly MatchLauncher launcher = new MatchLauncher();
        private readonly LobbyManager lobbyManager;
        private readonly LobbyToast toast;

        private readonly VisualElement scrim;
        private readonly VisualElement sheet;
        private readonly VisualElement findView;
        private readonly VisualElement joinView;
        private readonly VisualElement statusView;

        private readonly Button modeOnline;
        private readonly Button modeBot;
        private readonly Button modeTesting;
        private readonly Button modeLocked;
        private readonly VisualElement transportRow;
        private readonly Button relayTabButton;
        private readonly Button lanTabButton;
        private readonly Label note;
        private readonly VisualElement hostWrap;
        private readonly VisualElement joinOpenWrap;
        private readonly VisualElement startWrap;
        private readonly Button startButton;

        private readonly Label joinTitle;
        private readonly Label joinHint;
        private readonly TextField codeField;

        private readonly Label statusLabel;
        private readonly VisualElement codeDisplay;
        private readonly Label codeValue;
        private readonly Label recoveryLabel;
        private readonly VisualElement copyWrap;
        private readonly VisualElement retryWrap;
        private readonly Button cancelButton;

        private GameMode mode = GameMode.OneVsOne;
        private bool useLan;
        private bool open;
        private float dragStartY;
        private bool dragging;

        public PlayPopup(VisualTreeAsset layout, LobbyManager lobbyManager, LobbyToast toast)
        {
            this.lobbyManager = lobbyManager;
            this.toast = toast;

            Root = new VisualElement();
            Root.name = "play-popup-host";
            Root.pickingMode = PickingMode.Ignore;
            Root.style.position = Position.Absolute;
            Root.style.left = 0;
            Root.style.right = 0;
            Root.style.top = 0;
            Root.style.bottom = 0;
            if (layout != null) layout.CloneTree(Root);

            scrim = Root.Q<VisualElement>("play-scrim");
            sheet = Root.Q<VisualElement>("play-sheet");
            findView = Root.Q<VisualElement>("play-find");
            joinView = Root.Q<VisualElement>("play-join");
            statusView = Root.Q<VisualElement>("play-status");

            modeOnline = Root.Q<Button>("play-mode-1v1");
            modeBot = Root.Q<Button>("play-mode-bot");
            modeTesting = Root.Q<Button>("play-mode-testing");
            modeLocked = Root.Q<Button>("play-mode-locked");
            transportRow = Root.Q<VisualElement>("play-transport");
            relayTabButton = Root.Q<Button>("play-transport-relay");
            lanTabButton = Root.Q<Button>("play-transport-lan");
            note = Root.Q<Label>("play-note");
            hostWrap = Root.Q<VisualElement>("play-host-wrap");
            joinOpenWrap = Root.Q<VisualElement>("play-join-wrap");
            startWrap = Root.Q<VisualElement>("play-start-wrap");
            startButton = Root.Q<Button>("play-start");

            joinTitle = Root.Q<Label>("play-join-title");
            joinHint = Root.Q<Label>("play-join-hint");
            codeField = Root.Q<TextField>("play-code");

            statusLabel = Root.Q<Label>("play-status-text");
            codeDisplay = Root.Q<VisualElement>("play-code-display");
            codeValue = Root.Q<Label>("play-code-value");
            recoveryLabel = Root.Q<Label>("play-failure-recovery");
            copyWrap = Root.Q<VisualElement>("play-copy-wrap");
            retryWrap = Root.Q<VisualElement>("play-retry-wrap");
            cancelButton = Root.Q<Button>("play-cancel");

            Wire();

            launcher.Changed += RefreshStatus;

            SetOpen(false);
        }

        private void Wire()
        {
            if (scrim != null)
            {
                scrim.RegisterCallback<PointerDownEvent>(evt =>
                {
                    Hide();
                    evt.StopPropagation();
                });
            }

            WireHandle(Root.Q<VisualElement>("play-handle"));

            Bind(modeOnline, () => SetMode(GameMode.OneVsOne));
            Bind(modeBot, () => SetMode(GameMode.Bot));
            Bind(modeTesting, () => SetMode(GameMode.Testing));

            // Locked short-circuits in LobbyManager and nobody has recorded what
            // it is for, so it looks disabled but still answers a tap.
            Bind(modeLocked, () => Say("This mode is not available yet"));

            Bind(relayTabButton, () => SetTransport(false));
            Bind(lanTabButton, () => SetTransport(true));

            Bind(Root.Q<Button>("play-host"), OnHost);
            Bind(Root.Q<Button>("play-join-open"), () => ShowView(View.Join));
            Bind(startButton, () => LaunchLocal(mode));

            Bind(Root.Q<Button>("play-join-go"), OnJoin);
            Bind(Root.Q<Button>("play-join-back"), () => ShowView(View.Find));

            Bind(Root.Q<Button>("play-copy"), OnCopy);
            Bind(Root.Q<Button>("play-retry"), OnRetry);
            Bind(cancelButton, OnCancel);
        }

        private static void Bind(Button button, System.Action action)
        {
            if (button != null) button.clicked += action;
        }

        /// <summary>
        /// Drag the handle up to expand, down to shrink or close - the
        /// prototype's two detents.
        /// </summary>
        private void WireHandle(VisualElement handle)
        {
            if (handle == null) return;

            handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                dragging = true;
                dragStartY = evt.position.y;
                handle.CapturePointer(evt.pointerId);
            });

            handle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!dragging || sheet == null) return;

                float dy = evt.position.y - dragStartY;

                if (dy < -HandleDragThreshold)
                {
                    sheet.AddToClassList("lb-sheet--large");
                    dragging = false;
                }
                else if (dy > HandleDragThreshold)
                {
                    if (sheet.ClassListContains("lb-sheet--large"))
                        sheet.RemoveFromClassList("lb-sheet--large");
                    else
                        Hide();
                    dragging = false;
                }
            });

            handle.RegisterCallback<PointerUpEvent>(evt =>
            {
                dragging = false;
                handle.ReleasePointer(evt.pointerId);
            });
        }

        private void Say(string message)
        {
            if (toast != null) toast.Show(message);
        }

        // ===== VISIBILITY =====

        public void Show()
        {
            PlayerProfile profile = PlayerProfile.Instance;
            GameMode saved = profile != null ? profile.SelectedGameMode : GameMode.OneVsOne;
            if (saved == GameMode.Bot || saved == GameMode.Testing || saved == GameMode.OneVsOne)
                mode = saved;

            if (sheet != null) sheet.RemoveFromClassList("lb-sheet--large");

            SetTransport(useLan);
            SetMode(mode);
            ShowView(View.Find);
            SetOpen(true);
        }

        public void Hide()
        {
            // Closing while connecting must not leave a socket open, or the next
            // attempt binds a second NetworkManager and the first keeps receiving.
            launcher.Cancel();
            SetOpen(false);
        }

        public bool IsOpen
        {
            get { return open; }
        }

        /// <summary>
        /// The sheet slides rather than appearing, so it is never display:none -
        /// a hidden sheet is translated below the screen and the scrim stops
        /// taking taps, which is what lets the page under it work.
        /// </summary>
        private void SetOpen(bool value)
        {
            open = value;

            if (sheet != null) sheet.EnableInClassList("lb-sheet--open", value);
            if (scrim != null)
            {
                scrim.EnableInClassList("lb-scrim--on", value);
                scrim.pickingMode = value ? PickingMode.Position : PickingMode.Ignore;
            }
        }

        private void ShowView(View view)
        {
            SetVisible(findView, view == View.Find);
            SetVisible(joinView, view == View.Join);
            SetVisible(statusView, view == View.Status);
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            if (element != null) element.EnableInClassList("lb-view--hidden", !visible);
        }

        // ===== MODES =====

        private void SetMode(GameMode value)
        {
            mode = value;

            SetOn(modeOnline, mode == GameMode.OneVsOne);
            SetOn(modeBot, mode == GameMode.Bot);
            SetOn(modeTesting, mode == GameMode.Testing);

            bool online = mode == GameMode.OneVsOne;
            SetVisible(transportRow, online);
            SetVisible(hostWrap, online);
            SetVisible(joinOpenWrap, online);
            SetVisible(startWrap, !online);

            if (startButton != null)
                startButton.text = mode == GameMode.Testing ? "Start testing board" : "Play a Bot";

            RefreshNote();
        }

        private static void SetOn(Button button, bool on)
        {
            if (button != null) button.EnableInClassList("lb-seg__btn--on", on);
        }

        private void RefreshNote()
        {
            if (note == null) return;

            if (mode == GameMode.Testing)
            {
                note.text = "Development shortcut: hardcoded board, no draft.";
                return;
            }

            string loadout = DescribeLoadout();

            if (mode == GameMode.OneVsOne)
            {
                note.text = loadout + (useLan
                    ? "\nSame Wi-Fi. Join with the host's IP address."
                    : "");
            }
            else
            {
                note.text = loadout;
            }
        }

        private static string DescribeLoadout()
        {
            PlayerProfile profile = PlayerProfile.Instance;
            LoadoutData loadout = profile != null
                ? LoadoutData.Normalized(profile.Loadout)
                : LoadoutData.CreateEmpty();

            return "Loadout: " + Filled(loadout.nodeIDs) + "/" + LoadoutData.NodeSlots + " districts · " +
                   Filled(loadout.suitIDs) + "/" + LoadoutData.SuitSlots + " suits";
        }

        private static int Filled(string[] ids)
        {
            int count = 0;
            if (ids == null) return 0;
            for (int i = 0; i < ids.Length; i++)
                if (!string.IsNullOrEmpty(ids[i])) count++;
            return count;
        }

        private void LaunchLocal(GameMode value)
        {
            if (lobbyManager == null)
            {
                Debug.LogError("[PlayPopup] No LobbyManager, cannot start a local match.");
                Say("Cannot start a match: no LobbyManager in the scene");
                return;
            }

            lobbyManager.SetGameMode(value);
            lobbyManager.LaunchMatch();
        }

        private void SetTransport(bool lan)
        {
            useLan = lan;

            SetOn(relayTabButton, !lan);
            SetOn(lanTabButton, lan);

            if (joinTitle != null) joinTitle.text = lan ? "Enter the address" : "Enter the code";
            if (joinHint != null)
                joinHint.text = lan ? "The host's local IP address" : "The join code from your opponent";

            Button joinOpen = Root.Q<Button>("play-join-open");
            if (joinOpen != null) joinOpen.text = lan ? "Join by IP" : "Join with a code";

            RefreshNote();
        }

        // ===== CONNECT =====

        private void OnHost()
        {
            if (useLan) launcher.HostLan(); else launcher.HostRelay();
            ShowView(View.Status);
            RefreshStatus();
        }

        private void OnJoin()
        {
            string entered = codeField != null ? codeField.value : "";

            if (useLan) launcher.JoinLan(entered); else launcher.JoinRelay(entered);
            ShowView(View.Status);
            RefreshStatus();
        }

        private void OnCopy()
        {
            if (string.IsNullOrEmpty(launcher.JoinCode)) return;

            GUIUtility.systemCopyBuffer = launcher.JoinCode;
            Say("Code copied");
        }

        private void OnRetry()
        {
            launcher.Cancel();
            ShowView(View.Find);
        }

        private void OnCancel()
        {
            launcher.Cancel();
            ShowView(View.Find);
        }

        // ===== STATUS =====

        public void Update()
        {
            if (!open) return;
            launcher.Update();
        }

        private void RefreshStatus()
        {
            bool failed = launcher.CurrentPhase == MatchLauncher.Phase.Failed;

            if (statusLabel != null)
                statusLabel.text = failed ? launcher.FailureMessage : DescribePhase();

            // The code panel only appears once there is a real code to read out.
            bool showCode =
                !failed &&
                launcher.CurrentPhase == MatchLauncher.Phase.WaitingForOpponent &&
                !string.IsNullOrEmpty(launcher.JoinCode);

            SetVisible(codeDisplay, showCode);
            SetVisible(copyWrap, showCode);
            if (codeValue != null && showCode) codeValue.text = launcher.JoinCode;

            if (recoveryLabel != null)
            {
                SetVisible(recoveryLabel, failed);
                recoveryLabel.text = failed ? launcher.FailureRecovery : "";
            }

            // Retry is only an answer to a failure. Offering it mid-connection
            // would just be a second way to cancel.
            SetVisible(retryWrap, failed);

            if (cancelButton != null)
                cancelButton.text = failed ? "Back" : "Cancel";
        }

        private string DescribePhase()
        {
            switch (launcher.CurrentPhase)
            {
                case MatchLauncher.Phase.CreatingRoom:
                    return launcher.JoinCode == null ? "Creating room..." : "Joining room...";
                case MatchLauncher.Phase.WaitingForOpponent:
                    return "Waiting for an opponent";
                case MatchLauncher.Phase.Connecting:
                    return "Connecting...";
                case MatchLauncher.Phase.Connected:
                    return "Connected. Starting match...";
                default:
                    return "";
            }
        }

        public void Dispose()
        {
            launcher.Changed -= RefreshStatus;
            launcher.Dispose();
        }
    }
}
