using System.Collections.Generic;
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
    /// WHAT IT SHOWS is settled, not preference: both breach walls and three
    /// resources are always visible, and only the villager count collapses.
    /// Breach is the win condition, so it is first and it never hides.
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

        // The prototype's locked resource readout: five past samples, a full
        // bar at 15, and colour zones at 2 and 6. Presentation only - nothing
        // in the simulation knows about these numbers.
        private const int HistorySamples = 5;
        private const int ReadoutCap = 15;
        private const float ReadoutHeight = 40f;
        private const int LowZoneMax = 2;
        private const int MidZoneMax = 6;

        private UIDocument document;
        private SafeAreaBinder safeArea;
        private NodeSheet nodeSheet;
        private MatchSettingsPanel settingsPanel;
        private NodeWar.View.OpponentRouteSettings routeSettings;
        private VisualElement hudRoot;

        // Kept here as well as in the layer: OnEnable rebuilds the layer, and
        // GameManager binds the director only once.
        private IndicatorLayer indicatorLayer;
        private NodeWar.View.IndicatorDirector indicatorDirector;

        private SimulationState state;
        private GameBalanceData balance;
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

        private readonly ResourceReadout[] resources = new ResourceReadout[3];

        private Button villagerToggle;
        private VisualElement villagerCard;
        private Label villagersP0;
        private Label villagersP1;
        private bool villagersOpen;

        private VisualElement selectionDock;
        private Label selectionText;

        private VisualElement countdownRoot;
        private Label countdownStep;

        private VisualElement endRoot;
        private Label endTitle;
        private Label endSub;
        private readonly EndRow[] endRows = new EndRow[2];

        /// <summary>The player pressed Return to Lobby on the end overlay.</summary>
        public event System.Action ReturnToLobby;

        private int lastControlledPID = -1;
        private int lastClockSeconds = -1;
        private int lastSampleSecond = -1;
        private int lastUnitsP0 = -1;
        private int lastUnitsP1 = -1;
        private int lastSelected = -1;

        // Reused rather than rebuilt: Refresh runs every frame, and two fresh
        // arrays a frame is litter a phone has to collect.
        private readonly int[] resourceValues = new int[3];

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
            playerSwitch = debugSwitch;
            selection = selectionSystem;
            breachThreshold = breachThresholdValue > 0 ? breachThresholdValue : 1;

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

        private void Update()
        {
            if (safeArea != null) safeArea.Update();
            if (nodeSheet != null) nodeSheet.UpdateSafeArea();

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
            if (safeAreaElement != null) safeArea = new SafeAreaBinder(safeAreaElement);

            you = new BreachSide(root, "you");
            them = new BreachSide(root, "them");
            clockLabel = root.Q<Label>("hud-clock");
            flash = root.Q<VisualElement>("hud-flash");

            resources[0] = new ResourceReadout(root.Q<Label>("hud-food"), root.Q<VisualElement>("hud-hist-food"));
            resources[1] = new ResourceReadout(root.Q<Label>("hud-materials"), root.Q<VisualElement>("hud-hist-materials"));
            resources[2] = new ResourceReadout(root.Q<Label>("hud-metal"), root.Q<VisualElement>("hud-hist-metal"));

            villagerToggle = root.Q<Button>("hud-villager-toggle");
            villagerCard = root.Q<VisualElement>("hud-villagers");
            villagersP0 = root.Q<Label>("hud-villagers-p0");
            villagersP1 = root.Q<Label>("hud-villagers-p1");

            if (villagerToggle != null)
                villagerToggle.clicked += ToggleVillagers;

            selectionDock = root.Q<VisualElement>("hud-selection");
            selectionText = root.Q<Label>("hud-selection-text");

            zoomRoot = root.Q<VisualElement>("hud-zoom");
            zoomValue = root.Q<Label>("hud-zoom-value");
            zoomFill = root.Q<VisualElement>("hud-zoom-fill");

            recentreButton = root.Q<VisualElement>("hud-recentre");
            RegisterZoomHandle();

            countdownRoot = root.Q<VisualElement>("hud-countdown");
            countdownStep = root.Q<Label>("hud-countdown-step");

            endRoot = root.Q<VisualElement>("hud-end");
            endTitle = root.Q<Label>("hud-end-title");
            endSub = root.Q<Label>("hud-end-sub");
            endRows[0] = new EndRow(root, "a");
            endRows[1] = new EndRow(root, "b");

            Button endReturn = root.Q<Button>("hud-end-return");
            if (endReturn != null)
                endReturn.clicked += () => { if (ReturnToLobby != null) ReturnToLobby(); };

            BuildNodeSheet(root);
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
            indicatorLayer.AvoidRight(root.Q<VisualElement>("hud-recentre-dock"));
            indicatorLayer.AvoidRight(root.Q<VisualElement>("hud-settings-dock"));

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

            ApplyMatchSettings(settingsPanel.Settings);
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
            if (routeSettings != null)
                routeSettings.show = settings.opponentRoutes;

            if (indicatorLayer != null)
                indicatorLayer.SetCalm(settings.reducedMotion);
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
            // Everything redraws at once, with no breach punch and no bars
            // sliding, because nothing happened in the match.
            bool switched = pid != lastControlledPID;
            lastControlledPID = pid;

            if (switched) Snap();

            RefreshBreaches(pid, switched);
            RefreshClock();
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

            bool hitYou = you.Set(pid, state.players[pid].breachCount, breachThreshold, switched);
            bool hitThem = them.Set(other, state.players[other].breachCount, breachThreshold, switched);

            if ((hitYou || hitThem) && flash != null)
            {
                flash.AddToClassList("hud__flash--on");
                flash.schedule.Execute(() => flash.RemoveFromClassList("hud__flash--on")).StartingIn(40);
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
        /// Your three resources. History is sampled once per simulated second,
        /// keyed on the tick count so a paused tick loop records nothing, and it
        /// starts again from the current values when the viewer switches.
        /// </summary>
        private void RefreshResources(int pid, bool switched)
        {
            PlayerData player = state.players[pid];

            resourceValues[0] = player.food;
            resourceValues[1] = player.materials;
            resourceValues[2] = player.metal;

            int ticksPerSecond = balance.ticksPerSecond > 0 ? balance.ticksPerSecond : 10;
            int second = state.tickCount / ticksPerSecond;
            bool sample = second != lastSampleSecond;
            lastSampleSecond = second;

            for (int i = 0; i < resources.Length; i++)
            {
                if (resources[i] == null) continue;

                if (switched) resources[i].Reset(resourceValues[i]);
                else if (sample) resources[i].Sample(resourceValues[i]);

                resources[i].Render(resourceValues[i]);
            }
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

        private void RefreshSelection()
        {
            int count = selection != null ? selection.SelectedVillagerIDs.Count : 0;
            if (count == lastSelected) return;
            lastSelected = count;

            if (selectionDock != null)
                selectionDock.EnableInClassList("hud__selection-dock--on", count > 0);

            if (selectionText != null && count > 0)
                selectionText.text = "Tap a node to move · " + count;
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
            if (!handleDragging || boardCamera == null) return;

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

        private void ShowEnd(int viewerPID)
        {
            int other = viewerPID == 0 ? 1 : 0;

            endRows[0].Set(viewerPID, "You", state.players[viewerPID].breachCount, breachThreshold);
            endRows[1].Set(other, "Opponent", state.players[other].breachCount, breachThreshold);

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
            public bool Set(int playerID, int breaches, int threshold, bool snap)
            {
                if (playerID == shownPlayer && breaches == shownCount) return false;

                bool landed = !snap && playerID == shownPlayer && breaches > shownCount && shownCount >= 0;

                shownPlayer = playerID;
                shownCount = breaches;

                if (mark != null)
                {
                    mark.EnableInClassList("ui-mark--p0", playerID == 0);
                    mark.EnableInClassList("ui-mark--p1", playerID == 1);
                }

                if (markLabel != null) markLabel.text = (playerID + 1).ToString();

                if (count != null) count.text = breaches + "/" + threshold;

                // Remaining wall, not damage taken. Clamped because a count past
                // the threshold is a won match still being drawn for a frame.
                int remaining = threshold - breaches;
                if (remaining < 0) remaining = 0;
                Length width = Length.Percent(remaining * 100f / threshold);

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
        /// One resource's readout: the number, five fading past bars and the
        /// current bar.
        ///
        /// The staircase stays hidden until this resource has been held at all.
        /// A row of flat minimum-height bars sitting under a zero says nothing
        /// except that nothing has happened yet, and three of them across the
        /// top of the board is noise over the only thing worth looking at. Once
        /// the resource has been earned the history means something, so it
        /// appears and then stays -- dropping back to zero is exactly the case
        /// the staircase is for.
        /// </summary>
        private class ResourceReadout
        {
            private readonly Label value;
            private readonly VisualElement barHost;
            private readonly VisualElement[] bars = new VisualElement[HistorySamples + 1];
            private readonly List<int> history = new List<int>(HistorySamples + 1);

            private int shownValue = int.MinValue;
            private bool everHeld;
            private bool barsShown = true;

            public ResourceReadout(Label valueLabel, VisualElement host)
            {
                value = valueLabel;
                barHost = host;
                if (host == null) return;

                for (int i = 0; i < bars.Length; i++)
                {
                    VisualElement bar = new VisualElement();
                    bar.AddToClassList("hud__bar");
                    bar.AddToClassList(i < HistorySamples ? "hud__bar--age-" + i : "hud__bar--current");
                    bar.pickingMode = PickingMode.Ignore;
                    host.Add(bar);
                    bars[i] = bar;
                }

                SetBarsShown(false);
            }

            /// <summary>
            /// Kept in the layout rather than collapsed, so the readouts do not
            /// jump upward the first time a resource is earned mid-match.
            /// </summary>
            private void SetBarsShown(bool shown)
            {
                if (barHost == null || shown == barsShown) return;
                barsShown = shown;

                barHost.style.visibility = shown ? Visibility.Visible : Visibility.Hidden;
            }

            public void Reset(int current)
            {
                history.Clear();
                for (int i = 0; i < HistorySamples; i++) history.Add(current);
                shownValue = int.MinValue;

                // Latched per viewer, not per match: the debug switch resets the
                // history, and the new viewer's own holdings decide afresh.
                everHeld = current > 0;
                SetBarsShown(everHeld);
            }

            public void Sample(int current)
            {
                if (history.Count == 0) { Reset(current); return; }

                history.Add(current);
                while (history.Count > HistorySamples) history.RemoveAt(0);
                shownValue = int.MinValue;
            }

            public void Render(int current)
            {
                if (history.Count == 0) Reset(current);

                if (!everHeld && current > 0)
                {
                    everHeld = true;
                    SetBarsShown(true);
                }

                if (current != shownValue)
                {
                    shownValue = current;

                    if (value != null) value.text = current.ToString();

                    for (int i = 0; i < HistorySamples && bars[i] != null; i++)
                        bars[i].style.height = HeightFor(history[i]);

                    VisualElement now = bars[HistorySamples];
                    if (now != null)
                    {
                        now.style.height = HeightFor(current);
                        now.EnableInClassList("hud__bar--low", current <= LowZoneMax);
                        now.EnableInClassList("hud__bar--mid", current > LowZoneMax && current <= MidZoneMax);
                        now.EnableInClassList("hud__bar--high", current > MidZoneMax);
                    }
                }
            }

            private static float HeightFor(int amount)
            {
                return Mathf.Max(2f, Mathf.Min(1f, amount / (float)ReadoutCap) * ReadoutHeight);
            }
        }
    }
}
