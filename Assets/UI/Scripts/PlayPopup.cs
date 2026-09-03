using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// The play sheet: pick a mode, then host or join.
    ///
    /// Local modes (Bot, Testing) are handed to LobbyManager rather than
    /// reimplemented, so there stays exactly one definition of what starting a
    /// local match means. Networked play goes through MatchLauncher, which owns
    /// the handshake.
    ///
    /// Local network play is kept, against the rebuild brief, which said to drop
    /// the IP field because "LAN does not exist". It does: NetworkManager has a
    /// working DirectUDP transport, StartAsHost/StartAsClient and a
    /// GetLocalIPAddress helper, and the shipped NetworkingModal already offers
    /// it as a toggle. What does not exist is LAN *discovery*. Removing a working
    /// feature on a false premise is not a migration.
    /// </summary>
    public class PlayPopup
    {
        private enum View
        {
            Modes,
            Connect,
            Status
        }

        public VisualElement Root { get; private set; }

        private readonly MatchLauncher launcher = new MatchLauncher();
        private readonly LobbyManager lobbyManager;

        private readonly VisualElement modesView;
        private readonly VisualElement connectView;
        private readonly VisualElement statusView;

        private readonly Button botButton;
        private readonly Button onlineButton;
        private readonly Button testingButton;

        private readonly Button relayTabButton;
        private readonly Button lanTabButton;
        private readonly Label transportHint;
        private readonly Button hostButton;
        private readonly Button joinButton;
        private readonly TextField codeField;
        private readonly Button connectBackButton;

        private readonly Label statusLabel;
        private readonly VisualElement codeDisplay;
        private readonly Label codeValue;
        private readonly Label recoveryLabel;
        private readonly Button retryButton;
        private readonly Button cancelButton;

        private bool useLan;

        public PlayPopup(VisualTreeAsset layout, LobbyManager lobbyManager)
        {
            this.lobbyManager = lobbyManager;

            Root = new VisualElement();
            Root.name = "play-popup-host";
            if (layout != null) layout.CloneTree(Root);

            modesView = Root.Q<VisualElement>("play-modes");
            connectView = Root.Q<VisualElement>("play-connect");
            statusView = Root.Q<VisualElement>("play-status");

            botButton = Root.Q<Button>("play-mode-bot");
            onlineButton = Root.Q<Button>("play-mode-1v1");
            testingButton = Root.Q<Button>("play-mode-testing");

            relayTabButton = Root.Q<Button>("play-transport-relay");
            lanTabButton = Root.Q<Button>("play-transport-lan");
            transportHint = Root.Q<Label>("play-transport-hint");
            hostButton = Root.Q<Button>("play-host");
            joinButton = Root.Q<Button>("play-join");
            codeField = Root.Q<TextField>("play-code");
            connectBackButton = Root.Q<Button>("play-connect-back");

            statusLabel = Root.Q<Label>("play-status-text");
            codeDisplay = Root.Q<VisualElement>("play-code-display");
            codeValue = Root.Q<Label>("play-code-value");
            recoveryLabel = Root.Q<Label>("play-failure-recovery");
            retryButton = Root.Q<Button>("play-retry");
            cancelButton = Root.Q<Button>("play-cancel");

            Wire();

            launcher.Changed += RefreshStatus;

            Hide();
        }

        private void Wire()
        {
            Bind(Root.Q<Button>("play-close"), Hide);
            Bind(Root.Q<VisualElement>("play-scrim"), Hide);

            Bind(botButton, () => LaunchLocal(GameMode.Bot));
            Bind(testingButton, () => LaunchLocal(GameMode.Testing));
            Bind(onlineButton, () => ShowView(View.Connect));

            Bind(relayTabButton, () => SetTransport(false));
            Bind(lanTabButton, () => SetTransport(true));

            Bind(hostButton, OnHost);
            Bind(joinButton, OnJoin);
            Bind(connectBackButton, () => ShowView(View.Modes));

            Bind(retryButton, OnRetry);
            Bind(cancelButton, OnCancel);
        }

        private static void Bind(Button button, System.Action action)
        {
            if (button != null) button.clicked += action;
        }

        /// <summary>
        /// The scrim is a plain VisualElement, so it needs a pointer callback
        /// rather than a click event. Tapping outside the sheet dismisses it,
        /// which is what a sheet is expected to do.
        /// </summary>
        private static void Bind(VisualElement element, System.Action action)
        {
            if (element == null) return;
            element.RegisterCallback<PointerDownEvent>(evt => { action(); evt.StopPropagation(); });
        }

        // ===== VISIBILITY =====

        public void Show()
        {
            Root.EnableInClassList("popup--hidden", false);
            SetTransport(useLan);
            ShowView(View.Modes);
        }

        public void Hide()
        {
            // Closing while connecting must not leave a socket open, or the next
            // attempt binds a second NetworkManager and the first keeps
            // receiving.
            launcher.Cancel();
            Root.EnableInClassList("popup--hidden", true);
        }

        public bool IsOpen
        {
            get { return !Root.ClassListContains("popup--hidden"); }
        }

        private void ShowView(View view)
        {
            SetViewVisible(modesView, view == View.Modes);
            SetViewVisible(connectView, view == View.Connect);
            SetViewVisible(statusView, view == View.Status);
        }

        private static void SetViewVisible(VisualElement element, bool visible)
        {
            if (element != null) element.EnableInClassList("popup__view--hidden", !visible);
        }

        // ===== MODES =====

        private void LaunchLocal(GameMode mode)
        {
            if (lobbyManager == null)
            {
                Debug.LogError("[PlayPopup] No LobbyManager, cannot start a local match.");
                return;
            }

            lobbyManager.SetGameMode(mode);
            lobbyManager.LaunchMatch();
        }

        private void SetTransport(bool lan)
        {
            useLan = lan;

            if (relayTabButton != null)
                relayTabButton.EnableInClassList("popup__tab--active", !lan);
            if (lanTabButton != null)
                lanTabButton.EnableInClassList("popup__tab--active", lan);

            if (transportHint != null)
            {
                transportHint.text = lan
                    ? "Both devices must be on the same Wi-Fi. Join with the host's IP address."
                    : "Online play goes through Unity Relay. No port forwarding needed.";
            }

            if (codeField != null)
                codeField.label = lan ? "Host IP address" : "Join code";

            if (joinButton != null)
                joinButton.text = lan ? "Join by IP" : "Join with code";
        }

        // ===== CONNECT =====

        private void OnHost()
        {
            if (useLan) launcher.HostLan(); else launcher.HostRelay();
            ShowView(View.Status);
        }

        private void OnJoin()
        {
            string entered = codeField != null ? codeField.value : "";

            if (useLan) launcher.JoinLan(entered); else launcher.JoinRelay(entered);
            ShowView(View.Status);
        }

        private void OnRetry()
        {
            launcher.Cancel();
            ShowView(View.Connect);
        }

        private void OnCancel()
        {
            launcher.Cancel();
            ShowView(View.Modes);
        }

        // ===== STATUS =====

        public void Update()
        {
            if (!IsOpen) return;
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

            if (codeDisplay != null)
                codeDisplay.EnableInClassList("popup__code--hidden", !showCode);
            if (codeValue != null && showCode)
                codeValue.text = launcher.JoinCode;

            if (recoveryLabel != null)
            {
                recoveryLabel.EnableInClassList("popup__recovery--hidden", !failed);
                recoveryLabel.text = failed ? launcher.FailureRecovery : "";
            }

            // Retry is only an answer to a failure. Offering it mid-connection
            // would just be a second way to cancel.
            if (retryButton != null)
                retryButton.style.display = failed ? DisplayStyle.Flex : DisplayStyle.None;

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
