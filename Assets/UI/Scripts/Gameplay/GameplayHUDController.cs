using UnityEngine;
using UnityEngine.UIElements;
using NodeWar.Simulation;
using NodeWar.Debugging;
using NodeWar.Input;

// SafeAreaBinder lives in NodeWar.Lobby because that is where it was first
// needed. It is a general utility with nothing lobby-specific in it, and moving
// it is worth doing once the migration is finished and the namespaces settle -
// not mid-flight, while both stacks are live.
using NodeWar.Lobby;

namespace NodeWar.UI
{
    /// <summary>
    /// The UI Toolkit in-match HUD, after ingame-prototype.html. Counterpart to
    /// HUDManager, and live only when GameManager.useUIToolkitHUD is on - the
    /// same one-checkbox switch the lobby migration used, so the old HUD stays
    /// one tick away for as long as the new one is unproven.
    ///
    /// Namespace NodeWar.UI rather than NodeWar.Lobby, unlike everything else
    /// under Assets/UI/Scripts, because this is gameplay UI and belongs beside
    /// HUDManager in the layer it replaces. The folder says which stack it is
    /// in; the namespace says which layer.
    ///
    /// WHAT IT SHOWS is settled, not preference: both breach walls are always
    /// visible, and only the villager count collapses. Breach is the win
    /// condition, so it is first and it never hides. The resources sit at the
    /// bottom of the screen and, like the rest of that row, can be covered by
    /// the node sheet while it is open.
    ///
    /// YOU ARE ON THE LEFT. The left wall belongs to whichever player is being
    /// controlled - in a networked match that may be player 1 - and each wall
    /// carries its player mark, so position never has to say who is who.
    ///
    /// READ-ONLY, ABSOLUTELY. It reads SimulationState and writes nothing - not
    /// through GameSimulation, not through CommandProcessor. The node sheet's
    /// commands go through InputBuffer like everything else. See
    /// .claude/rules/view-ui.md.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class GameplayHUDController : MonoBehaviour, NodeWar.Core.ICountdownPresenter
    {
        [Tooltip("The HUD layout. Assign GameplayHUD.uxml.")]
        [SerializeField] private VisualTreeAsset hudLayout;

        [Tooltip("NodeSheet.uxml. Without it no node sheet is shown and the old " +
                 "uGUI panel keeps the job.")]
        [SerializeField] private VisualTreeAsset nodeSheetLayout;

        private UIDocument document;
        private SafeAreaBinder safeArea;
        private SafeAreaBinder resSheetInset;
        private NodeSheet nodeSheet;
        private MatchSettingsPanel settingsPanel;
        // Match-local mute and cooldown survive a rebuild of the visual tree.
        private readonly EmotePanel emotePanel = new EmotePanel();
        private NodeWar.View.OpponentRouteSettings routeSettings;
        private VisualElement hudRoot;

        // Kept here as well as in the layer: OnEnable rebuilds the layer, and
        // GameManager binds the director only once.
        private IndicatorLayer indicatorLayer;
        private NodeWar.View.IndicatorDirector indicatorDirector;

        private SimulationState state;
        private GameBalanceData balance;
        private NodeWar.Core.ITickProvider ticks;
        private DebugPlayerSwitch playerSwitch;
        private SelectionSystem selection;
        private NodePanelManager panelSource;
        private int breachThreshold = 3;
        private bool initialized;

        /// <summary>
        /// The node sheet, or null when no layout was assigned. GameManager
        /// asks so it knows whether to suppress the uGUI panel.
        /// </summary>
        public bool HasNodeSheet { get { return nodeSheet != null; } }

        private BreachSide you;
        private BreachSide them;
        private Label clockLabel;
        private VisualElement flash;

        private readonly ResourceReadout[] resources = new ResourceReadout[4];
        private VisualElement metalRoot;
        private VisualElement magicRoot;
        private bool capsApplied;
        private bool reducedMotion;

        private Button villagerToggle;
        private VisualElement villagerCard;
        private Label villagersP0;
        private Label villagersP1;
        private bool villagersOpen;

        private VisualElement selectionDock;
        private Label selectionText;

        // Controls settings: the thumb-placed controls and what shows on them.
        private VisualElement recentreDock;
        private VisualElement selectionBar;
        private Label selectionBarCount;
        private bool showSelectionBar = true;
        private bool showTooltips = true;
        private int cameraButtonTarget;
        private bool handleZoomEnabled = true;
        private float insetLeft = -1f;
        private float insetRight = -1f;
        private float insetPanelWidth = -1f;

        private VisualElement countdownRoot;
        private Label countdownStep;

        // Tempo and sudden-death cards: an event banner and a countdown.
        private VisualElement tempoRoot;
        private VisualElement tempoBanner;
        private Label tempoTitle;
        private Label tempoSub;
        private VisualElement suddenCard;
        private Label suddenTitle;
        private Label suddenSub;
        private IVisualElementScheduledItem bannerHideJob;
        private int lastCountdown = -1;
        private bool suddenDeathScheduled;

        /// <summary>How long a tempo or sudden-death banner stays up.</summary>
        private const long BannerMilliseconds = 3200;

        /// <summary>Matches the card's opacity transition, so --shown outlives the fade.</summary>
        private const long CardFadeMilliseconds = 220;

        private VisualElement endRoot;
        private Label endTitle;
        private Label endSub;
        private readonly EndRow[] endRows = new EndRow[2];
        private VisualElement endRank;
        private Label endRankHeadline;
        private Label endRankDetail;

        private VisualElement holdRoot;
        private Label holdTitle;
        private Label holdLine;
        private Button holdAction;
        private VisualElement connectionBanner;
        private bool surrenderEnabled;

        /// <summary>The hold overlay's one button: claim the win, or leave a private match.</summary>
        public event System.Action HoldActionClicked;

        /// <summary>The player confirmed a surrender in the settings card.</summary>
        public event System.Action SurrenderConfirmed;

        /// <summary>The player pressed Return to Lobby on the end overlay.</summary>
        public event System.Action ReturnToLobby;

        private int lastControlledPID = -1;
        private int lastClockSeconds = -1;
        private int lastUnitsP0 = -1;
        private int lastUnitsP1 = -1;
        private int lastSelected = -1;

        // Reused rather than rebuilt: Refresh runs every frame, and two fresh
        // arrays a frame is litter a phone has to collect.
        private readonly int[] resourceValues = new int[4];

        // The three, in the order the readouts sit in. Spelled out rather than
        // cast from the loop index: the enum and the array agreeing is a fact
        // about this list, not something to leave to their declaration order.
        private static readonly ResourceKind[] ResourceOrder =
            { ResourceKind.Food, ResourceKind.Materials, ResourceKind.Metal, ResourceKind.Magic };

        // Refilled per resource per frame. One buffer, because the three are
        // read one after another and nothing holds on to it.
        private readonly float[] productionBuffer = new float[ResourceProduction.MaxInFlight];

        private void OnEnable()
        {
            document = GetComponent<UIDocument>();

            if (document.panelSettings == null)
            {
                Debug.LogError("[HUD] UIDocument has no PanelSettings; nothing will render. " +
                               "Run Tools > Node War > Set Up UI Toolkit HUD.");
                return;
            }

            VisualElement root = document.rootVisualElement;
            if (root == null) return;

            root.Clear();

            VisualTreeAsset layout = hudLayout != null ? hudLayout : document.visualTreeAsset;
            if (layout == null)
            {
                Debug.LogError("[HUD] No GameplayHUD.uxml assigned, on either this component " +
                               "or the UIDocument.");
                return;
            }

            layout.CloneTree(root);

            // The panel covers the whole screen and the board is underneath it.
            // Without this the HUD eats every tap before the game sees one.
            root.pickingMode = PickingMode.Ignore;

            Bind(root);
        }

        private void OnDisable()
        {
            emotePanel.Detach();

            // The banner writes into a tree OnEnable rebuilds; Initialize
            // subscribes again if the match is still running.
            if (ticks != null) ticks.TickSimulated -= OnTickSimulated;
            if (bannerHideJob != null)
            {
                bannerHideJob.Pause();
                bannerHideJob = null;
            }

            if (panelSource != null)
            {
                panelSource.NodeOpened -= OnNodeOpened;
                panelSource.NodeClosed -= OnNodeClosed;
                panelSource = null;
            }

            // Unsubscribed here rather than in OnDestroy: the elements the
            // handlers write are rebuilt from scratch on the next OnEnable, and
            // a live subscription across that would be writing to a discarded tree.
            if (boardCamera != null)
            {
                boardCamera.ZoomChanged -= OnZoomChanged;
                boardCamera.ZoomGestureActiveChanged -= OnZoomGestureActiveChanged;
            }

            zoomHideJob = null;
            handleDragging = false;
            handleZoomed = false;

            // Same reason as the camera above: the panel writes into a tree
            // that OnEnable rebuilds, so the subscription must not outlive it.
            if (settingsPanel != null)
            {
                settingsPanel.Changed -= ApplyMatchSettings;
                settingsPanel.ForceClose();
                settingsPanel = null;
            }

            routeSettings = null;

            // The director outlives this; only its drawing is rebuilt.
            indicatorLayer = null;

            safeArea = null;
            resSheetInset = null;
            nodeSheet = null;
            initialized = false;
        }

        private void OnNodeOpened(int nodeID)
        {
            if (nodeSheet == null) return;

            nodeSheet.Open(nodeID, CurrentPlayerID());
        }

        private void OnNodeClosed()
        {
            if (nodeSheet != null) nodeSheet.Close();
        }

        private int CurrentPlayerID()
        {
            int pid = playerSwitch != null ? playerSwitch.GetCurrentPlayerID() : 0;

            if (state == null) return 0;
            if (pid < 0 || pid >= state.players.Length) return 0;

            return pid;
        }

        /// <summary>
        /// Called by GameManager once the simulation exists. Mirrors
        /// HUDManager.Initialize, deliberately - the two are interchangeable
        /// and GameManager should not care which it got.
        /// </summary>
        public void Initialize(SimulationState simulationState, DebugPlayerSwitch debugSwitch,
                               int breachThresholdValue, InputBuffer inputBuffer,
                               NodeWar.Core.ITickProvider tickProvider, GameBalanceData balanceData,
                               NodePanelManager nodePanelManager, SelectionSystem selectionSystem)
        {
            state = simulationState;
            balance = balanceData;
            ticks = tickProvider;
            playerSwitch = debugSwitch;
            selection = selectionSystem;
            breachThreshold = breachThresholdValue > 0 ? breachThresholdValue : 1;

            // The sudden-death steps exist only when the simulation will fire
            // them, which is the same test it makes.
            suddenDeathScheduled = balance.BreachBarEnabled() && balance.SuddenDeathValid() &&
                                   balance.suddenDeathTicks != null &&
                                   balance.suddenDeathTicks.Length > 0;

            if (ticks != null)
            {
                ticks.TickSimulated -= OnTickSimulated;
                ticks.TickSimulated += OnTickSimulated;
            }

            if (nodeSheet != null)
            {
                nodeSheet.Bind(state, inputBuffer, tickProvider, balance);

                // The old manager keeps the input path: TapRouter arbitrates the
                // tap, DistrictPanelPolicy decides whether a node deserves a
                // sheet at all, and the camera focus session hangs off the same
                // call. Subscribing rather than raycasting again is what stops
                // two things racing to answer one tap.
                panelSource = nodePanelManager;

                if (panelSource != null)
                {
                    panelSource.NodeOpened += OnNodeOpened;
                    panelSource.NodeClosed += OnNodeClosed;
                }
            }

            initialized = true;

            Refresh();
        }

        /// <summary>Local playtest schedule supplied by GameManager; no state write here.</summary>
        public void SetDebugBalance(GameBalanceData debugBalance)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            balance = debugBalance;
            suddenDeathScheduled = balance.BreachBarEnabled() && balance.SuddenDeathValid() &&
                balance.suddenDeathTicks != null && balance.suddenDeathTicks.Length > 0;
            lastCountdown = -1;
            RefreshSuddenDeathCountdown();
#endif
        }

        private void Update()
        {
            if (safeArea != null) safeArea.Update();
            if (resSheetInset != null) resSheetInset.Update();
            UpdateThumbInsets();
            if (nodeSheet != null) nodeSheet.UpdateSafeArea();

            emotePanel.SetNodeSheetOpen(nodeSheet != null && nodeSheet.IsOpen);

            if (!initialized || state == null) return;

            Refresh();

            if (nodeSheet != null) nodeSheet.Update(CurrentPlayerID());
        }

        /// <summary>
        /// Late, so indicators are projected after the camera and every villager
        /// have moved this frame rather than trailing them by one.
        /// </summary>
        private void LateUpdate()
        {
            if (indicatorLayer != null) indicatorLayer.LateUpdate();
        }

        // ===== BINDING =====

        private void Bind(VisualElement root)
        {
            hudRoot = root.Q<VisualElement>("hud-root");

            VisualElement safeAreaElement = root.Q<VisualElement>("hud-safe-area");
            // Not Edges.All. The resource sheet is the last thing in this
            // column and has to reach the true bottom edge, the way the node
            // sheet does; it takes the bottom inset itself, on a spacer of its
            // own inside it, so that the sheet reaches past it.
            if (safeAreaElement != null)
                safeArea = new SafeAreaBinder(safeAreaElement,
                    SafeAreaBinder.Edges.Left | SafeAreaBinder.Edges.Right | SafeAreaBinder.Edges.Top);

            VisualElement resSafeBottom = root.Q<VisualElement>("hud-res-safe-bottom");
            if (resSafeBottom != null)
                resSheetInset = new SafeAreaBinder(resSafeBottom, SafeAreaBinder.Edges.Bottom);

            you = new BreachSide(root, "you");
            them = new BreachSide(root, "them");
            clockLabel = root.Q<Label>("hud-clock");
            flash = root.Q<VisualElement>("hud-flash");

            resources[0] = new ResourceReadout(root.Q<Label>("hud-food"), root.Q<VisualElement>("hud-ring-food"));
            resources[1] = new ResourceReadout(root.Q<Label>("hud-materials"), root.Q<VisualElement>("hud-ring-materials"));
            resources[2] = new ResourceReadout(root.Q<Label>("hud-metal"), root.Q<VisualElement>("hud-ring-metal"), asBar: true, kind: ResourceKind.Metal);
            resources[3] = new ResourceReadout(root.Q<Label>("hud-magic"), root.Q<VisualElement>("hud-ring-magic"), asBar: true, kind: ResourceKind.Magic);
            metalRoot = root.Q<VisualElement>("hud-res-metal");
            magicRoot = root.Q<VisualElement>("hud-res-magic");

            villagerToggle = root.Q<Button>("hud-villager-toggle");
            villagerCard = root.Q<VisualElement>("hud-villagers");
            villagersP0 = root.Q<Label>("hud-villagers-p0");
            villagersP1 = root.Q<Label>("hud-villagers-p1");

            if (villagerToggle != null)
                villagerToggle.clicked += ToggleVillagers;

            selectionDock = root.Q<VisualElement>("hud-selection");
            selectionText = root.Q<Label>("hud-selection-text");

            selectionBar = root.Q<VisualElement>("hud-selbar");
            selectionBarCount = root.Q<Label>("hud-selbar-count");
            BindSelectionCounter();
            recentreDock = root.Q<VisualElement>("hud-recentre-dock");

            zoomRoot = root.Q<VisualElement>("hud-zoom");
            zoomValue = root.Q<Label>("hud-zoom-value");
            zoomFill = root.Q<VisualElement>("hud-zoom-fill");

            recentreButton = root.Q<VisualElement>("hud-recentre");
            RegisterZoomHandle();

            countdownRoot = root.Q<VisualElement>("hud-countdown");
            countdownStep = root.Q<Label>("hud-countdown-step");

            tempoRoot = root.Q<VisualElement>("hud-tempo");
            tempoBanner = root.Q<VisualElement>("hud-tempo-banner");
            tempoTitle = root.Q<Label>("hud-tempo-title");
            tempoSub = root.Q<Label>("hud-tempo-sub");
            suddenCard = root.Q<VisualElement>("hud-sd-countdown");
            suddenTitle = root.Q<Label>("hud-sd-title");
            suddenSub = root.Q<Label>("hud-sd-sub");
            lastCountdown = -1;

            endRoot = root.Q<VisualElement>("hud-end");
            endTitle = root.Q<Label>("hud-end-title");
            endSub = root.Q<Label>("hud-end-sub");
            endRows[0] = new EndRow(root, "a");
            endRows[1] = new EndRow(root, "b");
            endRank = root.Q<VisualElement>("hud-end-rank");
            endRankHeadline = root.Q<Label>("hud-end-rank-headline");
            endRankDetail = root.Q<Label>("hud-end-rank-detail");

            Button endReturn = root.Q<Button>("hud-end-return");
            if (endReturn != null)
                endReturn.clicked += () => { if (ReturnToLobby != null) ReturnToLobby(); };

            holdRoot = root.Q<VisualElement>("hud-hold");
            holdTitle = root.Q<Label>("hud-hold-title");
            holdLine = root.Q<Label>("hud-hold-line");
            holdAction = root.Q<Button>("hud-hold-action");
            connectionBanner = root.Q<VisualElement>("hud-connection");
            if (holdAction != null)
                holdAction.clicked += () => { if (HoldActionClicked != null) HoldActionClicked(); };

            BuildNodeSheet(root);
            emotePanel.Attach(hudRoot);
            emotePanel.Opening -= CloseSettingsForEmotes;
            emotePanel.Opening += CloseSettingsForEmotes;
            BuildSettingsPanel();
            BuildIndicatorLayer(root);
        }

        /// <summary>
        /// The indicators draw under everything else here. Edge icons keep clear
        /// of the right-hand column of controls, and of the node sheet while it
        /// is up.
        /// </summary>
        private void BuildIndicatorLayer(VisualElement root)
        {
            if (hudRoot == null) return;

            indicatorLayer = new IndicatorLayer(hudRoot);
            // Either edge, depending on the Controls side setting.
            indicatorLayer.AvoidEdge(root.Q<VisualElement>("hud-recentre-dock"));
            indicatorLayer.AvoidEdge(root.Q<VisualElement>("hud-selbar"));
            indicatorLayer.AvoidLeft(root.Q<VisualElement>("hud-emote-dock"));

            VisualElement sheetPanel = nodeSheet != null ? nodeSheet.Root.Q<VisualElement>("node-sheet") : null;
            indicatorLayer.SetSheet(() => nodeSheet != null && nodeSheet.IsOpen ? sheetPanel : null);

            if (settingsPanel != null) indicatorLayer.SetCalm(settingsPanel.Settings.reducedMotion);
            if (indicatorDirector != null) indicatorLayer.Bind(indicatorDirector);
            if (boardCamera != null) indicatorLayer.SetCameraController(boardCamera);
        }

        /// <summary>
        /// Hands over the director that decides which indicators exist. Called
        /// by GameManager once the board and its views are built.
        /// </summary>
        public void BindIndicators(NodeWar.View.IndicatorDirector director)
        {
            indicatorDirector = director;
            if (indicatorLayer != null) indicatorLayer.Bind(director);
        }

        public void BindEmotes(NodeWar.Core.IEmoteChannel channel, System.Func<int> localPlayer)
        {
            emotePanel.Bind(channel, localPlayer);
        }

        private void CloseSettingsForEmotes()
        {
            if (settingsPanel != null) settingsPanel.ForceClose();
        }

        /// <summary>
        /// The sheet is added to the document root rather than inside the safe
        /// area, and after everything else so it draws over the readouts. It is
        /// the one element on this surface allowed to reach the bottom edge -
        /// its own padding keeps its buttons clear of the home indicator.
        /// </summary>
        private void BuildNodeSheet(VisualElement root)
        {
            if (nodeSheetLayout == null)
            {
                Debug.LogWarning("[HUD] No NodeSheet.uxml assigned; the uGUI node panel " +
                                 "keeps the job. Run Tools > Node War > Set Up UI Toolkit HUD.");
                return;
            }

            nodeSheet = new NodeSheet(nodeSheetLayout);
            nodeSheet.SetReducedMotion(reducedMotion);
            nodeSheet.Closed += OnSheetClosedByPlayer;

            VisualElement host = hudRoot != null ? hudRoot : root;
            host.Add(nodeSheet.Root);
        }

        /// <summary>
        /// The in-match settings card. Its elements are authored in
        /// GameplayHUD.uxml rather than built here, because the scrim's picking
        /// behaviour is the load-bearing part of this surface and belongs where
        /// the rest of the picking rules are stated.
        /// </summary>
        private void BuildSettingsPanel()
        {
            if (hudRoot == null) return;

            settingsPanel = new MatchSettingsPanel(hudRoot);
            settingsPanel.Changed += ApplyMatchSettings;
            settingsPanel.SurrenderConfirmed += () => { if (SurrenderConfirmed != null) SurrenderConfirmed(); };
            settingsPanel.EnableSurrender(surrenderEnabled);

            ApplyMatchSettings(settingsPanel.Settings);
        }

        /// <summary>Ranked matches only: there is a server to surrender to.</summary>
        public void EnableSurrender(bool enabled)
        {
            surrenderEnabled = enabled;
            if (settingsPanel != null) settingsPanel.EnableSurrender(enabled);
        }

        public void SurrenderFailed(string message)
        {
            if (settingsPanel != null) settingsPanel.SurrenderFailed(message);
        }

        // ===== DISCONNECT HOLD =====

        /// <summary>
        /// Shows or updates the hold overlay (8.2c). It covers the board, so
        /// the node sheet and settings card are put away under it.
        /// </summary>
        public void ShowHold(NodeWar.Backend.HoldStatus status)
        {
            if (holdRoot == null || status == null) return;

            holdTitle.text = status.Title;
            holdLine.text = status.Line;
            bool hasAction = !string.IsNullOrEmpty(status.Action);
            holdAction.text = hasAction ? status.Action : "";
            holdAction.style.display = hasAction ? DisplayStyle.Flex : DisplayStyle.None;

            if (!holdRoot.ClassListContains("hud__end--on"))
            {
                if (settingsPanel != null) settingsPanel.ForceClose();
                if (indicatorLayer != null) indicatorLayer.Suppress();
                holdRoot.AddToClassList("hud__end--on");
            }
        }

        /// <summary>
        /// A light banner while the match plays on past a missing opponent input
        /// (8.2e). It does not cover the board: the player keeps playing.
        /// </summary>
        public void ShowConnectionBanner(bool on)
        {
            if (connectionBanner != null) connectionBanner.EnableInClassList("hud__connection--on", on);
        }

        /// <summary>
        /// Ranked only: the server could not be reached, so the dropped link is
        /// ours and the pill must not blame the opponent. The uxml's text is the
        /// opponent wording, which a private match keeps.
        /// </summary>
        public void SetConnectionBannerSelfOffline(bool selfOffline)
        {
            var label = connectionBanner?.Q<Label>(className: "hud__connection-label");
            if (label != null) label.text = selfOffline ? "Reconnecting…" : "Opponent's connection is unstable…";
        }

        public void HideHold()
        {
            if (holdRoot == null) return;
            holdRoot.RemoveFromClassList("hud__end--on");
            if (indicatorLayer != null) indicatorLayer.Resume();
        }

        /// <summary>
        /// Route visibility is shared by reference with MovementPathRenderer,
        /// which reads <c>show</c> every frame, so writing it here takes effect
        /// on the next draw with nothing to notify.
        ///
        /// This writes view configuration, never SimulationState - the setting
        /// changes what this player is shown and cannot change what either
        /// simulation computes. A setting that did would desync the match the
        /// moment the two players chose differently.
        /// </summary>
        private void ApplyMatchSettings(NodeWar.Lobby.GameSettingsData settings)
        {
            NodeWar.Core.FrameRateCap.Apply(settings.frameCap);
            emotePanel.ApplySettings(settings);
            if (routeSettings != null)
                routeSettings.show = settings.opponentRoutes;

            if (indicatorLayer != null)
                indicatorLayer.SetCalm(settings.reducedMotion);

            if (tempoRoot != null)
                tempoRoot.EnableInClassList("hud__tempo--calm", settings.reducedMotion);

            // Same flags for the breacher highlight and the core bar: view
            // configuration, shared by reference like routeSettings.
            if (breachCues != null)
            {
                breachCues.colourblindMarks = settings.colourblindMarks;
                breachCues.reducedMotion = settings.reducedMotion;
            }

            if (boardCamera != null) boardCamera.ShakeEnabled = !settings.reducedMotion;
            if (boardCamera != null) boardCamera.ApplyInputSettings(settings);

            ApplyControls(settings);
        }

        /// <summary>
        /// The Controls settings that live on the HUD. View only: nothing here
        /// reaches the simulation.
        ///
        /// Camera button off hides the handle with display:none, which removes
        /// its hit area as well as its pixels, so it stops swallowing presses.
        /// Drag to zoom off leaves the same element as a plain return-to-core
        /// click (see OnHandleMove). The side is one class on the root; the
        /// rest is USS.
        /// </summary>
        private void ApplyControls(NodeWar.Lobby.GameSettingsData settings)
        {
            if (hudRoot != null)
                hudRoot.EnableInClassList("hud--left", settings.controlsSide == 1);

            if (recentreDock != null)
                recentreDock.style.display = settings.showCameraButton ? DisplayStyle.Flex : DisplayStyle.None;

            handleZoomEnabled = settings.cameraButtonZoom;
            showSelectionBar = settings.showSelectionBar;
            showTooltips = settings.tooltips;

            // Reduced motion: a full resource is a steady brighter tint, with no pulse or wave.
            reducedMotion = settings.reducedMotion;
            if (nodeSheet != null) nodeSheet.SetReducedMotion(reducedMotion);
            for (int i = 0; i < resources.Length; i++)
                if (resources[i] != null) resources[i].SetReducedMotion(reducedMotion);
            cameraButtonTarget = settings.cameraButtonTarget;

            // Redraw the bar now rather than on the next selection change.
            lastSelected = -1;
            RefreshSelection();
        }

        /// <summary>
        /// Keeps the thumb-placed controls clear of a notch or rounded corner
        /// on either edge. Margins rather than SafeAreaBinder's padding: these
        /// are absolutely positioned, and padding on them would not move them.
        /// </summary>
        private void UpdateThumbInsets()
        {
            if (hudRoot == null || Screen.width <= 0) return;

            float panelWidth = hudRoot.resolvedStyle.width;
            if (float.IsNaN(panelWidth) || panelWidth <= 0f) return;

            Rect safe = Screen.safeArea;
            float left = Mathf.Max(0f, safe.xMin) / Screen.width * panelWidth;
            float right = Mathf.Max(0f, Screen.width - safe.xMax) / Screen.width * panelWidth;
            if (Mathf.Approximately(left, insetLeft) && Mathf.Approximately(right, insetRight) &&
                Mathf.Approximately(panelWidth, insetPanelWidth)) return;

            insetLeft = left;
            insetRight = right;
            insetPanelWidth = panelWidth;

            ApplyInset(recentreDock, left, right);
            ApplyInset(selectionBar, left, right);
        }

        private static void ApplyInset(VisualElement element, float left, float right)
        {
            if (element == null) return;
            element.style.marginLeft = left;
            element.style.marginRight = right;
        }

        /// <summary>
        /// Handed the same OpponentRouteSettings instance GameManager gave the
        /// path renderer. Without it the routes toggle stores a preference that
        /// draws nothing.
        /// </summary>
        public void BindRouteSettings(NodeWar.View.OpponentRouteSettings settings)
        {
            routeSettings = settings;

            if (settingsPanel != null)
                ApplyMatchSettings(settingsPanel.Settings);
        }

        /// <summary>
        /// Handed the BreachCueSettings GameManager gave the villager views and
        /// the core bars, so the accessibility flags in the settings card reach
        /// them. Applied at once: the card's saved values are already loaded.
        /// </summary>
        public void BindBreachCues(NodeWar.View.BreachCueSettings cues)
        {
            breachCues = cues;

            if (settingsPanel != null)
                ApplyMatchSettings(settingsPanel.Settings);
        }

        private NodeWar.View.BreachCueSettings breachCues;

        /// <summary>
        /// The player closed the sheet from its own button or handle. The old
        /// manager still owns the open/closed truth - it drives the camera focus
        /// session and decides what a later tap means - so it is told rather
        /// than bypassed.
        /// </summary>
        private void OnSheetClosedByPlayer()
        {
            if (panelSource != null) panelSource.ClosePanel();
        }

        private void ToggleVillagers()
        {
            villagersOpen = !villagersOpen;

            if (villagerCard != null)
                villagerCard.EnableInClassList("hud__units-card--open", villagersOpen);

            if (villagerToggle != null)
                villagerToggle.EnableInClassList("hud__units--open", villagersOpen);
        }

        // ===== REFRESH =====

        private void Refresh()
        {
            int pid = CurrentPlayerID();

            // A viewer switch changes whose numbers these are, not the numbers.
            // Everything redraws at once, with no breach punch and no
            // resource-ring pop, because nothing happened in the match.
            bool switched = pid != lastControlledPID;
            lastControlledPID = pid;

            if (switched) Snap();

            RefreshBreaches(pid, switched);
            RefreshClock();
            RefreshSuddenDeathCountdown();
            RefreshResources(pid, switched);
            RefreshUnits(pid);
            RefreshSelection();
        }

        private void Snap()
        {
            if (hudRoot == null) return;

            hudRoot.AddToClassList("hud--snap");
            hudRoot.schedule.Execute(() => hudRoot.RemoveFromClassList("hud--snap")).StartingIn(50);
        }

        private void RefreshBreaches(int pid, bool switched)
        {
            int other = pid == 0 ? 1 : 0;

            int threshold = CurrentBreachThreshold();
            bool hitYou = you.Set(pid, state.players[pid].breachCount, threshold, breachThreshold, switched,
                state.gameOver && state.winnerID != pid);
            bool hitThem = them.Set(other, state.players[other].breachCount, threshold, breachThreshold, switched,
                state.gameOver && state.winnerID != other);

            if ((hitYou || hitThem) && flash != null)
            {
                flash.AddToClassList("hud__flash--on");
                flash.schedule.Execute(() => flash.RemoveFromClassList("hud__flash--on")).StartingIn(40);
            }
        }

        /// <summary>
        /// Breaches needed to win right now. Sudden death lowers it mid-match,
        /// so the walls and the tally read it from the tick count rather than
        /// from the opening value Initialize was given.
        /// </summary>
        private int CurrentBreachThreshold()
        {
            int threshold = balance.BreachThresholdAt(state.tickCount);
            return threshold > 0 ? threshold : 1;
        }

        // ===== TEMPO AND SUDDEN DEATH =====

        /// <summary>
        /// The moments come from the tick's event log, as they do for the
        /// indicators and the screen shake; the countdown is read off the tick
        /// count in Refresh. Wall-clock timing here is presentation only.
        /// </summary>
        private void OnTickSimulated(TickEventLog log)
        {
            if (log == null || tempoBanner == null) return;

            for (int i = 0; i < log.Count; i++)
            {
                TickEvent e = log[i];

                if (e.type == TickEventType.TempoStage)
                    ShowBanner(NodeWar.View.BreachTempoMath.TempoStageTitle(e.value),
                               NodeWar.View.BreachTempoMath.TempoStageSub(e.value));
                else if (e.type == TickEventType.SuddenDeath)
                    ShowBanner(NodeWar.View.BreachTempoMath.SuddenDeathTitle(),
                               NodeWar.View.BreachTempoMath.ThresholdLine(e.value));
            }
        }

        private void ShowBanner(string title, string sub)
        {
            tempoTitle.text = title;
            tempoSub.text = sub;

            // A new banner takes over from one still showing.
            if (bannerHideJob != null) bannerHideJob.Pause();

            SetCard(tempoBanner, true);

            VisualElement card = tempoBanner;
            bannerHideJob = card.schedule.Execute(() =>
            {
                bannerHideJob = null;
                SetCard(card, false);
            }).StartingIn(BannerMilliseconds);
        }

        /// <summary>
        /// Shows a card by adding --shown, then --in a frame later so the
        /// transition runs; hiding reverses it and drops --shown once the fade
        /// is done. Reduced motion has no transition, so the same calls simply
        /// appear and go.
        /// </summary>
        private static void SetCard(VisualElement card, bool on)
        {
            if (card == null) return;

            if (on)
            {
                card.AddToClassList("hud__tempo-card--shown");
                card.schedule.Execute(() => card.AddToClassList("hud__tempo-card--in")).StartingIn(16);
            }
            else
            {
                card.RemoveFromClassList("hud__tempo-card--in");
                card.schedule.Execute(() =>
                {
                    if (!card.ClassListContains("hud__tempo-card--in"))
                        card.RemoveFromClassList("hud__tempo-card--shown");
                }).StartingIn(CardFadeMilliseconds);
            }
        }

        /// <summary>
        /// "Sudden death in 5…" for the five seconds before each sudden-death
        /// tick. Computed from the tick count against the balance arrays, so a
        /// rollback or a paused match shows the right number; only a change of
        /// the whole second touches the labels.
        /// </summary>
        private void RefreshSuddenDeathCountdown()
        {
            if (suddenCard == null) return;

            int seconds = 0;
            int next = 0;
            if (suddenDeathScheduled && !state.gameOver)
            {
                int ticksPerSecond = balance.ticksPerSecond > 0 ? balance.ticksPerSecond : 10;
                seconds = NodeWar.View.BreachTempoMath.SuddenDeathCountdown(
                    balance.suddenDeathTicks, balance.suddenDeathThresholds,
                    state.tickCount, ticksPerSecond, out next);
            }

            if (seconds == lastCountdown) return;
            bool wasShowing = lastCountdown > 0;
            lastCountdown = seconds;

            if (seconds > 0)
            {
                suddenTitle.text = NodeWar.View.BreachTempoMath.CountdownTitle(seconds);
                suddenSub.text = NodeWar.View.BreachTempoMath.ThresholdLine(next);
                if (!wasShowing) SetCard(suddenCard, true);
            }
            else if (wasShowing)
            {
                SetCard(suddenCard, false);
            }
        }

        /// <summary>
        /// Elapsed match time from the tick count. The match has no time limit -
        /// breach is the only way it ends - so this counts up, never down.
        /// </summary>
        private void RefreshClock()
        {
            int ticksPerSecond = balance.ticksPerSecond > 0 ? balance.ticksPerSecond : 10;
            int seconds = state.tickCount / ticksPerSecond;

            if (seconds == lastClockSeconds) return;
            lastClockSeconds = seconds;

            if (clockLabel != null)
                clockLabel.text = (seconds / 60) + ":" + (seconds % 60).ToString("00");
        }

        /// <summary>
        /// Your three resources, as segmented rings. A viewer switch resets
        /// each readout's baseline so the redraw carries no pop; otherwise
        /// Render decides for itself whether the value moved and which way.
        /// </summary>
        private void RefreshResources(int pid, bool switched)
        {
            PlayerData player = state.players[pid];

            if (!capsApplied) ApplyResourceCaps();

            // Metal is hidden until the player's arena reaches ResourceHudMath.MetalArena or
            // they hold some; the node sheet's chip reads the same rule.
            if (metalRoot != null)
                metalRoot.EnableInClassList("hud__res-metal--on", ResourceVisibility.MetalVisible(player.metal));
            if (magicRoot != null)
                magicRoot.EnableInClassList("hud__res-metal--on", ResourceVisibility.MagicVisible());

            resourceValues[0] = player.food;
            resourceValues[1] = player.materials;
            resourceValues[2] = player.metal;
            resourceValues[3] = ResourceHudMath.DisplayOnlyMagicAmount();

            // Sub-tick, so the production fill moves at render rate rather than
            // stepping ten times a second. Same alpha ProductionContent reads.
            float alpha = ticks != null ? ticks.TickAlpha : 0f;

            for (int i = 0; i < resources.Length; i++)
            {
                if (resources[i] == null) continue;

                if (switched) resources[i].Reset();

                resources[i].Render(resourceValues[i]);

                int jobs = ResourceProduction.InFlight(state, balance, pid, ResourceOrder[i],
                    alpha, productionBuffer);
                resources[i].SetProduction(productionBuffer, jobs);
            }
        }

        /// <summary>
        /// The typed balance caps, with finite 30/30/10 display defaults for uncapped resources.
        /// </summary>
        private void ApplyResourceCaps()
        {
            capsApplied = true;
            if (resources[0] != null) resources[0].SetCap(ResourceCaps.Food(balance));
            if (resources[1] != null) resources[1].SetCap(ResourceCaps.Materials(balance));
            if (resources[2] != null) resources[2].SetCap(ResourceCaps.Metal(balance));
            if (resources[3] != null) resources[3].SetCap(ResourceHudMath.DefaultMagicCap);
            for (int i = 0; i < resources.Length; i++)
                if (resources[i] != null) resources[i].SetReducedMotion(reducedMotion);
        }

        /// <summary>
        /// Units per player: every villager not consumed, the dead included,
        /// because the dead come back. Recounted every frame - a claimed Village
        /// grows the villager array mid-match, so a count taken at the start
        /// would drift - and shown against the balance cap, not a literal.
        /// </summary>
        private void RefreshUnits(int pid)
        {
            int p0 = 0;
            int p1 = 0;

            for (int i = 0; i < state.villagers.Length; i++)
            {
                if (state.villagers[i].isConsumed) continue;

                if (state.villagers[i].ownerID == 0) p0++;
                else if (state.villagers[i].ownerID == 1) p1++;
            }

            if (p0 == lastUnitsP0 && p1 == lastUnitsP1 && villagerToggle != null &&
                villagerToggle.userData is int shownFor && shownFor == pid)
                return;

            lastUnitsP0 = p0;
            lastUnitsP1 = p1;

            int cap = balance.maxVillagersPerPlayer;

            if (villagersP0 != null) villagersP0.text = cap > 0 ? p0 + " / " + cap : p0.ToString();
            if (villagersP1 != null) villagersP1.text = cap > 0 ? p1 + " / " + cap : p1.ToString();

            if (villagerToggle != null)
            {
                int mine = pid == 1 ? p1 : p0;
                villagerToggle.text = mine + (mine == 1 ? " unit" : " units");
                villagerToggle.userData = pid;
            }
        }

        // ===== SELECTION COUNTER =====
        //
        // A press captures its pointer, so the whole press-drag-release belongs to
        // this element: the board never sees it. (PointerGestureSource also reads a
        // press over UI as Blocked and latches it to release.) Only a release over
        // the circle clears; leaving it un-presses the visual and releasing
        // elsewhere does nothing. Hover is for pointer devices; a touch has none,
        // so its press goes straight to the inverted state.

        private int counterPointer = -1;

        private void BindSelectionCounter()
        {
            if (selectionBar == null) return;

            selectionBar.RegisterCallback<PointerEnterEvent>(e =>
            {
                if (e.pointerType != UnityEngine.UIElements.PointerType.touch)
                    selectionBar.AddToClassList("hud__selbar--hover");
            });
            selectionBar.RegisterCallback<PointerLeaveEvent>(e => selectionBar.RemoveFromClassList("hud__selbar--hover"));
            selectionBar.RegisterCallback<PointerDownEvent>(e =>
            {
                if (counterPointer != -1) return;
                counterPointer = e.pointerId;
                selectionBar.CapturePointer(e.pointerId);
                selectionBar.AddToClassList("hud__selbar--pressed");
                e.StopPropagation();
            });
            selectionBar.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (e.pointerId != counterPointer) return;
                selectionBar.EnableInClassList("hud__selbar--pressed", selectionBar.ContainsPoint(e.localPosition));
            });
            selectionBar.RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.pointerId != counterPointer) return;
                bool inside = selectionBar.ContainsPoint(e.localPosition);
                EndCounterPress();
                selectionBar.ReleasePointer(e.pointerId);
                e.StopPropagation();
                if (inside && selection != null) selection.ClearSelection();
            });
            selectionBar.RegisterCallback<PointerCancelEvent>(e => EndCounterPress());
            selectionBar.RegisterCallback<PointerCaptureOutEvent>(e => EndCounterPress());
        }

        private void EndCounterPress()
        {
            counterPointer = -1;
            selectionBar.RemoveFromClassList("hud__selbar--pressed");
        }

        private void RefreshSelection()
        {
            int count = selection != null ? selection.SelectedVillagerIDs.Count : 0;
            if (count == lastSelected) return;
            lastSelected = count;

            if (selectionDock != null)
                selectionDock.EnableInClassList("hud__selection-dock--on", showTooltips && count > 0);

            if (selectionBar != null)
            {
                bool shown = showSelectionBar && count > 0;
                selectionBar.EnableInClassList("hud__selbar--on", shown);
                // A hidden element gets no leave event; do not leave it looking hovered.
                if (!shown) { selectionBar.RemoveFromClassList("hud__selbar--hover"); selectionBar.RemoveFromClassList("hud__selbar--pressed"); }
            }

            if (selectionBarCount != null && count > 0)
                selectionBarCount.text = count.ToString();

        }

        // ===== CAMERA AFFORDANCES =====
        //
        // Two things, neither of them a camera control. The readout is
        // feedback that answers "am I in zoom mode" and then leaves; the
        // recentre button is the undo for having gone somewhere. The camera
        // itself is driven entirely by gestures on the board.

        private VisualElement zoomRoot;
        private Label zoomValue;
        private VisualElement zoomFill;
        private VisualElement recentreButton;

        [Header("Zoom handle")]
        [Tooltip("Responsiveness of dragging UP to zoom in. Lower than the out " +
                 "gain on purpose: going in is a precision move toward something " +
                 "you have already picked out, going out is a panic move.")]
        [SerializeField] private float zoomInGain = 10f;

        [Tooltip("Responsiveness of dragging DOWN to zoom out. What matters is " +
                 "the ratio against the in gain, not either number alone.")]
        [SerializeField] private float zoomOutGain = 17f;

        [Tooltip("Drag distance, in panel units, that crosses the whole zoom " +
                 "range at a gain of 10. Raise it to make the handle longer-throw.")]
        [SerializeField] private float zoomDragRange = 260f;

        [Tooltip("How far the press may travel and still count as a click that " +
                 "recentres rather than a drag that zooms.")]
        [SerializeField] private float zoomHandleClickSlop = 6f;

        private NodeWar.Core.CameraController boardCamera;
        private IVisualElementScheduledItem zoomHideJob;

        // Zoom-handle drag. The handle is one control with two meanings, so it
        // has to know which one is happening: a press that never travels past
        // the slop is a click and recentres on release, and one that does is a
        // zoom and must NOT also recentre when the finger lifts.
        private bool handleDragging;
        private bool handleZoomed;
        private float handleStartY;
        private float handleStartNormalized;

        /// <summary>
        /// Subscribes the readout to the camera. Event-driven rather than
        /// polled: a camera at rest publishes nothing, so a HUD that is mostly
        /// idle does no per-frame work for a control that is mostly hidden.
        /// </summary>
        public void BindCamera(NodeWar.Core.CameraController cameraController)
        {
            if (boardCamera != null)
            {
                boardCamera.ZoomChanged -= OnZoomChanged;
                boardCamera.ZoomGestureActiveChanged -= OnZoomGestureActiveChanged;
            }

            boardCamera = cameraController;

            if (boardCamera != null)
            {
                boardCamera.ZoomChanged += OnZoomChanged;
                boardCamera.ZoomGestureActiveChanged += OnZoomGestureActiveChanged;

                if (settingsPanel != null) boardCamera.ShakeEnabled = !settingsPanel.Settings.reducedMotion;
                if (settingsPanel != null) boardCamera.ApplyInputSettings(settingsPanel.Settings);
            }

            // A tapped edge indicator moves this camera to its subject.
            if (indicatorLayer != null) indicatorLayer.SetCameraController(boardCamera);
        }

        private void OnZoomChanged(float normalized)
        {
            // Filled means zoomed IN, so the bar grows the same way the number
            // beside it does. normalized is 0 at the closest distance, hence
            // the inversion.
            if (zoomFill != null)
                zoomFill.style.width = Length.Percent((1f - Mathf.Clamp01(normalized)) * 100f);

            if (zoomValue != null && boardCamera != null)
            {
                // The TARGET distance, not the current one. This event fires
                // when the target moves, and at that instant ApplyZoom has
                // barely begun easing toward it -- so reading the current
                // distance here reports where the camera still is, and because
                // no further event follows a released gesture, that stale value
                // is the one left on screen. It made a fast drag look like it
                // was hunting around the value it started from.
                //
                // Shown as a magnification the player can reason about, not as
                // the dolly distance in world units, which means nothing to
                // anyone looking at a board.
                float distance = boardCamera.GetTargetZoomDistance();
                float magnification = distance > 0.01f ? boardCamera.DefaultZoomDistance / distance : 1f;
                zoomValue.text = magnification.ToString("0.0") + "x";
            }
        }

        private void OnZoomGestureActiveChanged(bool active)
        {
            if (zoomRoot == null) return;

            if (zoomHideJob != null)
            {
                zoomHideJob.Pause();
                zoomHideJob = null;
            }

            if (active)
            {
                zoomRoot.AddToClassList("hud__zoom--on");
                return;
            }

            // Held briefly past the end of the gesture so the final value is
            // readable, rather than vanishing with the fingers that set it.
            zoomHideJob = zoomRoot.schedule
                .Execute(() => zoomRoot.RemoveFromClassList("hud__zoom--on"))
                .StartingIn(520);
        }

        /// <summary>
        /// The handle is permanent and carries both meanings: a click returns
        /// the camera to your core, and a vertical drag zooms. One circle, two
        /// inputs, no board area spent on either.
        ///
        /// Driven by raw pointer events rather than Button.clicked, because a
        /// Button fires its click on release regardless of how far the finger
        /// travelled -- so every zoom drag would end by also recentring, undoing
        /// the zoom the player just set.
        /// </summary>
        private void RegisterZoomHandle()
        {
            if (recentreButton == null)
            {
                Debug.LogWarning("[HUD] hud-recentre not found in GameplayHUD.uxml; " +
                                 "the zoom handle will not respond.");
                return;
            }

            // Picking is set here rather than in UXML so it cannot be lost to an
            // edit of the markup: this element is the one thing on the HUD that
            // must take a touch, and it is surrounded by Ignore.
            recentreButton.pickingMode = PickingMode.Position;

            recentreButton.RegisterCallback<PointerDownEvent>(OnHandleDown);
            recentreButton.RegisterCallback<PointerMoveEvent>(OnHandleMove);
            recentreButton.RegisterCallback<PointerUpEvent>(OnHandleUp);
            recentreButton.RegisterCallback<PointerCaptureOutEvent>(OnHandleCaptureLost);
        }

        private void OnHandleDown(PointerDownEvent evt)
        {
            // Deliberately not gated on the camera. A press that silently does
            // nothing is indistinguishable from a dead control, so the handle
            // always takes the pointer and says why if it cannot act.
            handleDragging = true;
            handleZoomed = false;
            handleStartY = evt.position.y;
            handleStartNormalized = boardCamera != null
                ? boardCamera.GetTargetZoomNormalized()
                : 0f;

            recentreButton.AddToClassList("hud__recentre--held");

            // Captured so the drag survives the finger leaving the 46px circle,
            // which it does within a few pixels of a real throw.
            recentreButton.CapturePointer(evt.pointerId);
            evt.StopPropagation();

            if (boardCamera == null)
                Debug.LogWarning("[HUD] Zoom handle pressed but no CameraController is bound. " +
                                 "GameManager.InitializeUI should have called BindCamera.");
        }

        private void OnHandleMove(PointerMoveEvent evt)
        {
            // Drag to zoom off: the handle is a plain return-to-core click.
            if (!handleDragging || boardCamera == null || !handleZoomEnabled) return;

            // Panel Y grows downward, so a finger moving up is a NEGATIVE delta.
            // Flipped here once, so everything below reads in player terms.
            float dy = handleStartY - evt.position.y;

            if (!handleZoomed)
            {
                if (Mathf.Abs(dy) < zoomHandleClickSlop) return;

                handleZoomed = true;
                boardCamera.BeginZoomGesture();
            }

            float gain = dy > 0f ? zoomInGain : zoomOutGain;
            float range = Mathf.Max(1f, zoomDragRange);

            // Normalized is 0 at the closest distance, so zooming IN subtracts.
            float delta = dy * (gain / 10f) / range;
            boardCamera.SetZoomNormalized(handleStartNormalized - delta);

            evt.StopPropagation();
        }

        private void OnHandleUp(PointerUpEvent evt)
        {
            if (!handleDragging) return;

            recentreButton.ReleasePointer(evt.pointerId);
            FinishHandleDrag();
            evt.StopPropagation();
        }

        /// <summary>
        /// A capture can be lost without a PointerUp -- the panel rebuilding, or
        /// the OS taking the touch. Without this the handle would stay armed and
        /// the zoom readout would never be told the gesture ended.
        /// </summary>
        private void OnHandleCaptureLost(PointerCaptureOutEvent evt)
        {
            FinishHandleDrag();
        }

        private void FinishHandleDrag()
        {
            if (!handleDragging) return;
            handleDragging = false;

            if (recentreButton != null)
                recentreButton.RemoveFromClassList("hud__recentre--held");

            if (boardCamera == null) { handleZoomed = false; return; }

            // Which half of the control fired is decided by whether the press
            // ever travelled: a click that stayed put recentres, and a drag that
            // zoomed must NOT also recentre, or it would throw away the zoom the
            // player just set on the way to lifting their finger.
            if (handleZoomed) boardCamera.EndZoomGesture();
            else if (cameraButtonTarget == 1) boardCamera.RecentreOnBoard();
            else boardCamera.RecentreOnHome();

            handleZoomed = false;
        }

        // ===== COUNTDOWN =====

        /// <summary>
        /// "3 - 2 - 1 - GO" before the tick loop starts, in the same beats the
        /// uGUI overlay used: 0.7s a number, 0.8s on GO, then a 0.3s fade. The
        /// callback must always arrive - the transition waits on it before
        /// unpausing the match - so it is scheduled, not tied to an animation.
        /// </summary>
        public void PlayCountdown(System.Action onComplete)
        {
            if (countdownRoot == null || countdownStep == null)
            {
                if (onComplete != null) onComplete();
                return;
            }

            countdownRoot.RemoveFromClassList("hud__countdown--out");
            countdownRoot.AddToClassList("hud__countdown--on");

            string[] steps = { "3", "2", "1", "GO" };
            long at = 0;

            for (int i = 0; i < steps.Length; i++)
            {
                string text = steps[i];
                countdownRoot.schedule.Execute(() => ShowCountdownStep(text)).StartingIn(at);
                at += i < steps.Length - 1 ? 700 : 800;
            }

            countdownRoot.schedule.Execute(() => countdownRoot.AddToClassList("hud__countdown--out")).StartingIn(at);
            countdownRoot.schedule.Execute(() =>
            {
                countdownRoot.RemoveFromClassList("hud__countdown--on");
                if (onComplete != null) onComplete();
            }).StartingIn(at + 300);
        }

        private void ShowCountdownStep(string text)
        {
            countdownStep.text = text;

            // Off, then on a frame later, so the transition runs each time
            // rather than only on the first step.
            countdownStep.RemoveFromClassList("hud__countdown-step--in");
            countdownStep.schedule.Execute(() => countdownStep.AddToClassList("hud__countdown-step--in")).StartingIn(16);
        }

        // ===== END OF MATCH =====

        /// <summary>
        /// The result from the viewer's side. Never "PLAYER 0 WINS": that is
        /// zero-indexed, clashes with the 1/2 marks everywhere else, and says
        /// nothing about you.
        /// </summary>
        public void ShowMatchEnd(int viewerPID)
        {
            if (state == null || endRoot == null) return;

            bool won = state.winnerID == viewerPID;

            endTitle.text = won ? "Victory" : "Defeat";
            endTitle.EnableInClassList("hud__end-title--won", won);
            endSub.text = (won ? "You held the wall." : "Your wall fell.") + " " + MatchLength();

            ShowEnd(viewerPID);
        }

        /// <summary>
        /// An ending the tally alone does not explain: a hold resolved, a
        /// surrender. The caller words it; the tally still stands.
        /// </summary>
        public void ShowMatchEndWith(int viewerPID, string title, bool won, string sub)
        {
            if (state == null || endRoot == null) return;

            HideHold();
            if (settingsPanel != null) settingsPanel.ForceClose();
            endTitle.text = title;
            endTitle.EnableInClassList("hud__end-title--won", won);
            endSub.text = sub + " " + MatchLength();

            ShowEnd(viewerPID);
        }

        /// <summary>
        /// The opponent left. Can happen mid-match, so the tally still stands.
        /// </summary>
        public void ShowDisconnected(int viewerPID)
        {
            if (state == null || endRoot == null) return;

            endTitle.text = "Disconnected";
            endTitle.EnableInClassList("hud__end-title--won", false);
            endSub.text = "Your opponent has disconnected. " + MatchLength();

            ShowEnd(viewerPID);
        }

        /// <summary>
        /// The ranked block under the tally. Called on every change of the
        /// match's server result, so it starts at "Confirming" and fills in; a
        /// match that is not ranked never calls it and the block stays hidden.
        /// </summary>
        public void ShowRankedResult(NodeWar.Backend.RankedResultStatus status)
        {
            if (endRank == null || status == null) return;

            endRankHeadline.text = status.Headline;
            endRankDetail.text = status.Detail;
            endRankDetail.style.display = string.IsNullOrEmpty(status.Detail) ? DisplayStyle.None : DisplayStyle.Flex;

            bool gain = status.Phase == NodeWar.Backend.RankedResultPhase.Settled &&
                        (status.Result?.rrDelta ?? 0) > 0;
            endRankHeadline.EnableInClassList("hud__end-rank-headline--gain", gain);
            endRank.AddToClassList("hud__end-rank--on");
        }

        private void ShowEnd(int viewerPID)
        {
            int other = viewerPID == 0 ? 1 : 0;

            int threshold = CurrentBreachThreshold();
            endRows[0].Set(viewerPID, "You", state.players[viewerPID].breachCount, threshold);
            endRows[1].Set(other, "Opponent", state.players[other].breachCount, threshold);

            if (indicatorLayer != null) indicatorLayer.Suppress();

            endRoot.AddToClassList("hud__end--on");
        }

        /// <summary>
        /// How long the match ran, as time rather than ticks. The old panel
        /// printed "Game ended at tick N", which is a number for a log.
        /// </summary>
        private string MatchLength()
        {
            int ticksPerSecond = balance.ticksPerSecond > 0 ? balance.ticksPerSecond : 10;
            int seconds = state.tickCount / ticksPerSecond;

            return (seconds / 60) + ":" + (seconds % 60).ToString("00");
        }

        /// <summary>One player's line on the end card: mark, who, breaches.</summary>
        private class EndRow
        {
            private readonly VisualElement mark;
            private readonly Label markLabel;
            private readonly Label name;
            private readonly Label count;

            public EndRow(VisualElement root, string which)
            {
                mark = root.Q<VisualElement>("hud-end-mark-" + which);
                markLabel = root.Q<Label>("hud-end-mark-" + which + "-label");
                name = root.Q<Label>("hud-end-name-" + which);
                count = root.Q<Label>("hud-end-count-" + which);
            }

            public void Set(int playerID, string who, int breaches, int threshold)
            {
                if (mark != null)
                {
                    mark.EnableInClassList("ui-mark--p0", playerID == 0);
                    mark.EnableInClassList("ui-mark--p1", playerID == 1);
                }

                if (markLabel != null) markLabel.text = (playerID + 1).ToString();
                if (name != null) name.text = who;
                if (count != null) count.text = breaches + "/" + threshold;
            }
        }

        /// <summary>
        /// One breach wall: a player's mark, their count as text, and the wall
        /// itself with the ghost of where it stood.
        /// </summary>
        private class BreachSide
        {
            private readonly VisualElement side;
            private readonly VisualElement mark;
            private readonly Label markLabel;
            private readonly Label count;
            private readonly VisualElement fill;
            private readonly VisualElement ghost;

            private int shownPlayer = -1;
            private int shownCount = -1;
            private int shownThreshold = -1;
            private bool shownDefeated;

            public BreachSide(VisualElement root, string which)
            {
                side = root.Q<VisualElement>("hud-side-" + which);
                mark = root.Q<VisualElement>("hud-mark-" + which);
                markLabel = root.Q<Label>("hud-mark-" + which + "-label");
                count = root.Q<Label>("hud-breach-count-" + which);
                fill = root.Q<VisualElement>("hud-fill-" + which);
                ghost = root.Q<VisualElement>("hud-ghost-" + which);
            }

            /// <summary>Returns true when this call showed a new breach landing.</summary>
            public bool Set(int playerID, int breaches, int threshold, int originalMax, bool snap, bool defeated)
            {
                if (playerID == shownPlayer && breaches == shownCount && threshold == shownThreshold && defeated == shownDefeated) return false;

                bool landed = !snap && playerID == shownPlayer && breaches > shownCount && shownCount >= 0;

                shownPlayer = playerID;
                shownCount = breaches;
                shownThreshold = threshold;
                shownDefeated = defeated;

                if (mark != null)
                {
                    mark.EnableInClassList("ui-mark--p0", playerID == 0);
                    mark.EnableInClassList("ui-mark--p1", playerID == 1);
                }

                if (markLabel != null) markLabel.text = (playerID + 1).ToString();

                if (count != null) count.text = NodeWar.View.BreachTempoMath.WallLabel(breaches, threshold, defeated);

                // R1 needs a new breach to defeat an active core, even over threshold.
                Length width = Length.Percent(NodeWar.View.BreachTempoMath.WallFill(breaches, threshold, defeated, originalMax) * 100f);

                if (fill != null)
                {
                    fill.EnableInClassList("hud__wall-fill--p0", playerID == 0);
                    fill.EnableInClassList("hud__wall-fill--p1", playerID == 1);
                    fill.style.width = width;
                }

                if (ghost != null) ghost.style.width = width;

                if (landed && side != null)
                {
                    side.AddToClassList("hud__side--hit");
                    side.schedule.Execute(() => side.RemoveFromClassList("hud__side--hit")).StartingIn(180);
                }

                return landed;
            }
        }

        /// <summary>
        /// <summary>
        /// One resource's readout: the number, centred inside a ResourceRing
        /// hosted on "hud-ring-*". The ring draws only what the player has -
        /// there is no unlit track behind it - so at zero the resource is the
        /// icon and the number alone.
        ///
        /// POP ON CHANGE, and only the pop. An increase scales the ring host
        /// (rings and number together) up and back; a decrease scales it down
        /// and back, so a spend lands with a bounce while the number changes
        /// instantly. The class is added here and removed a moment later by
        /// schedule.Execute(...).StartingIn(...) - the same idiom BreachSide
        /// uses for hud__side--hit. Neither fires on the sentinel left by
        /// Reset, so a viewer switch and the very first render redraw
        /// silently, and the ring is told to snap on those too.
        ///
        /// A decrease used to also tint the resource card's border red for
        /// the same beat. The cards lost their borders when the three moved
        /// onto one sheet, and the ring's own white spend ghost says it
        /// better anyway - see ResourceRing.
        /// </summary>
        private class ResourceReadout
        {
            private const long PopMilliseconds = 150;

            private readonly Label value;
            private readonly VisualElement ringHost;
            private readonly ResourceRing ring;
            private readonly ResourceBar bar;

            private int shownValue = int.MinValue;
            private int cap;
            private IVisualElementScheduledItem popJob;

            /// <param name="asBar">Metal: a segmented bar read as "x/cap" instead of a semicircle.</param>
            public ResourceReadout(Label valueLabel, VisualElement host, bool asBar = false, ResourceKind kind = ResourceKind.Food)
            {
                value = valueLabel;
                ringHost = host;
                if (host == null) return;

                if (asBar)
                {
                    bar = new ResourceBar();
                    bar.SetResourceKind(kind);
                    host.Insert(0, bar);
                    return;
                }

                ring = new ResourceRing();

                // Inserted first so the value label - already in the UXML
                // host - draws on top of it.
                host.Insert(0, ring);

                // A semicircle is half as tall as it is wide, plus half a stroke, so its
                // host takes its height from whatever width the layout gave it.
                host.RegisterCallback<GeometryChangedEvent>(evt =>
                {
                    float height = ResourceHudMath.HostHeight(evt.newRect.width);
                    if (evt.newRect.width > 0f && !Mathf.Approximately(host.resolvedStyle.height, height))
                        host.style.height = height;
                });
            }

            public void SetCap(int amount)
            {
                cap = amount;
                if (ring != null) ring.SetCap(amount);
                if (bar != null) bar.SetCap(amount);
                if (bar != null && value != null && shownValue != int.MinValue)
                    value.text = shownValue + "/" + cap;
            }

            public void SetReducedMotion(bool reduced)
            {
                if (ring != null) ring.SetReducedMotion(reduced);
                if (bar != null) bar.SetReducedMotion(reduced);
            }

            /// <summary>
            /// Drops the baseline so the next Render redraws with no pop:
            /// used on a viewer switch, where the numbers change but nothing
            /// happened in the match.
            /// </summary>
            public void Reset()
            {
                CancelPop();
                shownValue = int.MinValue;
            }

            public void Render(int current)
            {
                if (current == shownValue) return;

                bool isFirst = shownValue == int.MinValue;
                bool increased = !isFirst && current > shownValue;
                bool decreased = !isFirst && current < shownValue;

                shownValue = current;

                if (value != null) value.text = bar != null ? current + "/" + cap : current.ToString();
                if (ring != null) ring.SetValue(current, isFirst);
                if (bar != null) bar.SetValue(current);

                if (increased) Pop("hud__res-ring-host--up");
                else if (decreased) Pop("hud__res-ring-host--down");
            }

            /// <summary>Passes the resource's in-flight production to the ring.</summary>
            public void SetProduction(float[] fractions, int count)
            {
                if (ring != null) ring.SetProduction(fractions, count);
                if (bar != null) bar.SetProduction(fractions, count);
            }

            private void Pop(string ringHostClass)
            {
                CancelPop();
                if (ringHost == null) return;

                ringHost.RemoveFromClassList("hud__res-ring-host--up");
                ringHost.RemoveFromClassList("hud__res-ring-host--down");
                ringHost.AddToClassList(ringHostClass);

                popJob = ringHost.schedule.Execute(() =>
                {
                    ringHost.RemoveFromClassList(ringHostClass);
                    popJob = null;
                }).StartingIn(PopMilliseconds);
            }

            private void CancelPop()
            {
                if (popJob == null) return;
                popJob.Pause();
                popJob = null;

                if (ringHost == null) return;
                ringHost.RemoveFromClassList("hud__res-ring-host--up");
                ringHost.RemoveFromClassList("hud__res-ring-host--down");
            }
        }
    }
}
