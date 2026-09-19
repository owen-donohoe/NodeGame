using UnityEngine;
using UnityEngine.SceneManagement;
using NodeWar.Simulation;
using NodeWar.Config;
using NodeWar.Input;
using NodeWar.Debugging;
using NodeWar.UI;
using NodeWar.Network;
using NodeWar.View.Outline;
using System.Collections.Generic;
using DG.Tweening;

namespace NodeWar.Core
{
    public class GameManager : MonoBehaviour
    {
        [Header("Balance")]
        [SerializeField] private GameBalance balance;
        [SerializeField] private BoardConfig boardConfig;

        [Header("Node Prefabs")]
        [SerializeField] private GameObject nodePrefabDefault;
        [SerializeField] private GameObject nodePrefabCore;
        [SerializeField] private GameObject nodePrefabFarm;
        [SerializeField] private GameObject nodePrefabMine;
        [SerializeField] private GameObject nodePrefabVillage;
        [SerializeField] private GameObject nodePrefabBarracks;
        [SerializeField] private GameObject nodePrefabForge;
        [SerializeField] private GameObject nodePrefabCamp;
        [SerializeField] private GameObject nodePrefabShrine;
        [SerializeField] private GameObject nodePrefabArsenal;
        [SerializeField] private GameObject nodePrefabSanctuary;
        [SerializeField] private GameObject nodePrefabWatchtower;
        [SerializeField] private GameObject nodePrefabRampart;
        [SerializeField] private GameObject nodePrefabMarket;

        [Header("Villager Prefab")]
        [SerializeField] private GameObject villagerPrefab;

        [Header("Movement Routes")]
        [Tooltip("Shape of a drawn movement route. One instance, handed to both " +
                 "MovementPathRenderer and every VillagerView, so the curve the " +
                 "sprite walks and the curve drawn on the board cannot disagree.")]
        [SerializeField] private NodeWar.View.PathCurveSettings pathCurveSettings =
            new NodeWar.View.PathCurveSettings();

        [Tooltip("How much of an opponent route the player is allowed to see. " +
                 "The reveal is a real truncation, not a fade, so the cut is a " +
                 "rule rather than a look.")]
        [SerializeField] private NodeWar.View.OpponentRouteSettings opponentRouteSettings =
            new NodeWar.View.OpponentRouteSettings();

        [Header("UI")]
        [SerializeField] private GameObject uiManagerPrefab;
        private NodePanelManager nodePanelManager;
        private GameOverPanel gameOverPanel;

        [Header("UI Toolkit HUD (migration)")]
        [Tooltip("Swap the uGUI HUD band for the UI Toolkit one. The node panels, " +
                 "draft UI and game-over panel are unaffected either way.")]
        [SerializeField] private bool useUIToolkitHUD;

        [Tooltip("Scene object carrying the UIDocument and GameplayHUDController. " +
                 "Created by Tools > Node War > Set Up UI Toolkit HUD.")]
        [SerializeField] private GameObject uiToolkitHudRoot;

        private GameplayHUDController uiToolkitHud;

        [Header("Transitions")]
        [SerializeField] private MatchTransitionController transitionController;

        [Header("Draft")]
        [SerializeField] private GameObject draftUIPrefab;

        [Tooltip("Swap the uGUI draft screen for the UI Toolkit one. Independent " +
                 "of the HUD toggle: the two phases never overlap, so there is no " +
                 "reason a half-finished migration has to move them together.")]
        [SerializeField] private bool useUIToolkitDraft;

        [Tooltip("Scene object carrying the UIDocument and DraftScreenController. " +
                 "Created by Tools > Node War > Set Up UI Toolkit Draft.")]
        [SerializeField] private GameObject uiToolkitDraftRoot;

        [SerializeField] private GameObject placementPreviewPrefab;
        [SerializeField] private GameObject gridCellMarkerPrefab;

        [Header("Runtime")]
        public SimulationState state;

        private DebugPlayerSwitch debugPlayerSwitch;
        private HUDManager hudManager;
        private CameraController cameraController;

        private Transform nodeParent;
        private Transform villagerParent;

        private InputBuffer inputBuffer;
        private ITickProvider tickProvider;
        private SelectionSystem selectionSystem;
        private CommandSystem commandSystem;

        // View references
        private NodeWar.View.NodeSlotManager[] nodeSlotManagers;
        private int trackedVillagerCount;
        private Transform[] villagerTransforms;

        // Outline groups, indexed in step with the view arrays beside them.
        private OutlineGroup[] villagerOutlines;
        private OutlineGroup[] nodeOutlines;
        private NodeWar.View.OutlineDriver outlineDriver;
        private NodeWar.View.NodePresentation[] nodePresentations;
        private NodeWar.View.NodeView[] nodeViews;
        private NodeWar.View.MovementPathRenderer pathRenderer;

        // Network
        private LockstepRunner lockstepRunner;
        private NodeWar.Input.BotPlayer botPlayer;

        // Game over
        private bool gameOverHandled = false;

        // Draft
        private DraftManager draftManager;
        private DraftResult? pendingDraftResult;

        private enum MatchPhase { PreDraft, Drafting, PostDraft, Countdown, Playing }
        private MatchPhase matchPhase = MatchPhase.PreDraft;

        // ===== INITIALIZATION =====

        private void Awake()
        {
            Application.runInBackground = true;

            state = new SimulationState();

            if (balance == null) { Debug.LogError("[GameManager] GameBalance not assigned!"); return; }
            if (boardConfig == null) { Debug.LogError("[GameManager] BoardConfig not assigned!"); return; }

            GameSimulation.SetBalance(balance.Data);
            CommandProcessor.SetBalance(balance.Data);
            state.defaultEdgeWeight = boardConfig.Data.defaultEdgeWeight;

            Pathfinding.OwnedMultiplier = boardConfig.Data.ownedMultiplier;
            Pathfinding.PartiallyOwnedMultiplier = boardConfig.Data.partiallyOwnedMultiplier;
            Pathfinding.UnownedMultiplier = boardConfig.Data.unownedMultiplier;
            Pathfinding.EnemyPartiallyOwnedMultiplier = boardConfig.Data.enemyPartiallyOwnedMultiplier;
            Pathfinding.EnemyOwnedMultiplier = boardConfig.Data.enemyOwnedMultiplier;

            inputBuffer = new InputBuffer();

            cameraController = FindAnyObjectByType<CameraController>();
            if (cameraController != null)
                cameraController.InitializeSides(boardConfig);

            // Wire transition controller events
            if (transitionController != null)
            {
                transitionController.OnRequestPlayerSideSwitch += OnPlayerSideChanged;
                transitionController.OnStartupTransitionComplete += OnTransitionComplete;
            }

            MatchConnection match = MatchConnection.Instance;

            if (match != null && (match.isBotMatch || match.isNetworked))
                StartDraftPhase(match);
            else
                SkipDraftAndInitialize();
        }

        // ===== DRAFT PHASE =====

        private void StartDraftPhase(MatchConnection match)
        {
            matchPhase = MatchPhase.Drafting;

            GameObject draftGO = new GameObject("DraftManager");
            draftManager = draftGO.AddComponent<DraftManager>();

            NodeWar.Lobby.LoadoutData loadout = (match != null) ? match.loadout : new NodeWar.Lobby.LoadoutData();

            draftManager.Initialize(
                boardConfig,
                match.isNetworked ? match.networkManager : null,
                match.isNetworked ? match.localPlayerID : 0,
                match.isNetworked,
                match.isBotMatch,
                cameraController,
                gridCellMarkerPrefab,
                loadout
            );

            draftManager.OnDraftComplete += OnDraftComplete;
            draftManager.OnDraftDisconnect += OnDraftDisconnect;

            draftPresenter = CreateDraftPresenter(match);

            if (draftPresenter != null)
            {
                draftPresenter.Initialize(draftManager, match.isNetworked ? match.localPlayerID : 0);
                draftManager.SetDraftUI(draftPresenter);
            }
        }

        /// <summary>
        /// Which draft surface draws this match, and the guarantee that only one
        /// of them does.
        ///
        /// The uGUI draft is a prefab instantiated per match; the UI Toolkit one
        /// is a scene object that is switched on. So the two are turned off in
        /// different ways, and the check that only one is live is that this
        /// method returns exactly one presenter and instantiates nothing it did
        /// not choose.
        ///
        /// A toggle with nothing wired to it keeps the old draft rather than
        /// leaving the phase with no UI at all - a draft you cannot see is a
        /// match you cannot start.
        /// </summary>
        private IDraftPresenter CreateDraftPresenter(MatchConnection match)
        {
            if (useUIToolkitDraft)
            {
                if (uiToolkitDraftRoot == null)
                {
                    Debug.LogWarning("[GameManager] useUIToolkitDraft is on but no " +
                                     "uiToolkitDraftRoot is assigned. Keeping the uGUI draft. " +
                                     "Run Tools > Node War > Set Up UI Toolkit Draft.");
                }
                else
                {
                    uiToolkitDraftRoot.SetActive(true);

                    IDraftPresenter presenter =
                        uiToolkitDraftRoot.GetComponent<NodeWar.UI.DraftScreenController>();

                    if (presenter != null) return presenter;

                    Debug.LogError("[GameManager] uiToolkitDraftRoot has no " +
                                   "DraftScreenController. Keeping the uGUI draft.");
                    uiToolkitDraftRoot.SetActive(false);
                }
            }
            else if (uiToolkitDraftRoot != null)
            {
                uiToolkitDraftRoot.SetActive(false);
            }

            if (draftUIPrefab == null) return null;

            GameObject uiGO = Instantiate(draftUIPrefab);
            return uiGO.GetComponent<NodeWar.UI.DraftUI>();
        }

        // Whichever draft surface drew this match. Held rather than searched for
        // later: the uGUI one is a prefab instance and the UI Toolkit one is a
        // scene object, so there is no one FindAnyObjectByType that finds both.
        private IDraftPresenter draftPresenter;

        private NodeWar.Lobby.LoadoutData cachedLocalLoadout;
        private NodeWar.Lobby.LoadoutData cachedRemoteLoadout;

        private void OnDraftComplete(DraftResult result)
        {
            pendingDraftResult = result;
            matchPhase = MatchPhase.PostDraft;

            // Capture loadouts before destroying DraftManager
            if (draftManager != null)
            {
                cachedLocalLoadout = draftManager.GetLocalLoadout();
                cachedRemoteLoadout = draftManager.GetRemoteLoadout();
            }

            if (draftManager != null)
            {
                Destroy(draftManager.gameObject);
                draftManager = null;
            }

            InitializeFromDraftResult(result);
        }

        private void OnDraftDisconnect()
        {
            Debug.LogError("[GameManager] Draft disconnected.");
            if (draftManager != null)
            {
                Destroy(draftManager.gameObject);
                draftManager = null;
            }
            ShowDisconnect();
        }

        // ===== POST-DRAFT INITIALIZATION =====

        private void InitializeFromDraftResult(DraftResult result)
        {
            InitializeNodesFromDraft(result);
            InitializePlayers();
            InitializeVillagers();
            InitializeInputSystems();

            MatchConnection match = MatchConnection.Instance;

            if (match != null && match.isNetworked)
            {
                StartNetworkPlay(match);
            }
            else
            {
                StartLocalPlay();

                if (match != null && match.isBotMatch)
                {
                    botPlayer = new NodeWar.Input.BotPlayer(state, inputBuffer, 1, boardConfig.Data.defaultEdgeWeight);
                    debugPlayerSwitch.LockToPlayer(0);

                    TickRunner runner = GetComponent<TickRunner>();
                    if (runner != null)
                        runner.SetBot(botPlayer);
                }
            }

            SpawnNodeViews();
            SpawnVillagerViews();
            trackedVillagerCount = state.villagers.Length;

            debugPlayerSwitch.OnPlayerSwitched += OnPlayerSideChanged;

            InitializeUI();

            // Begin transition sequence
            BeginPostDraftTransition(match);
        }

        private void BeginPostDraftTransition(MatchConnection match)
        {
            matchPhase = MatchPhase.Countdown;

            if (cameraController != null)
                cameraController.SetDraftMode(false);

            // Gather placeholders from whichever draft surface drew them, before
            // it becomes irrelevant. They outlive it: the transition dissolves
            // them as the real nodes arrive.
            List<GameObject> placeholders =
                draftPresenter != null ? draftPresenter.GetPersistentPlacements() : null;

            int localPID = (match != null && match.isNetworked) ? match.localPlayerID : 0;

            if (transitionController != null)
            {
                transitionController.PlayPostDraftTransition(
                    localPID, placeholders, nodePresentations, state, boardConfig);
            }
            else
            {
                // Fallback if no controller assigned — just start immediately
                OnPlayerSideChanged(localPID);
                OnTransitionComplete();
            }
        }

        private void OnTransitionComplete()
        {
            matchPhase = MatchPhase.Playing;

            TickRunner tickRunner = GetComponent<TickRunner>();
            if (tickRunner != null) tickRunner.Unpause();

            LockstepRunner lockstep = GetComponent<LockstepRunner>();
            if (lockstep != null) lockstep.Unpause();
        }

        // ===== TESTING MODE (skip draft, legacy board) =====

        private void SkipDraftAndInitialize()
        {
            matchPhase = MatchPhase.Playing;

            InitializeNodes();
            InitializePlayers();
            InitializeVillagers();
            InitializeInputSystems();

            MatchConnection match = MatchConnection.Instance;
            if (match != null && match.isNetworked)
                StartNetworkPlay(match);
            else
                StartLocalPlay();

            SpawnNodeViews();
            SpawnVillagerViews();
            trackedVillagerCount = state.villagers.Length;

            debugPlayerSwitch.OnPlayerSwitched += OnPlayerSideChanged;
            OnPlayerSideChanged(debugPlayerSwitch.GetCurrentPlayerID());

            InitializeUI();

            // Just play the startup animation, no countdown sequence
            if (transitionController != null)
                transitionController.PlayNodeStartupWave(nodePresentations, state, boardConfig);

            // PlayNodeStartupWave is animation only -- it deliberately raises no
            // events, so OnStartupTransitionComplete never fires on this path and
            // nothing else would ever unpause the runner. Without this the tick
            // loop stays paused forever: commands enqueue and are never drained,
            // and the whole simulation is frozen. The draft path gets here via
            // PostDraftSequence instead.
            //
            // TODO: no test covers this. Both entry paths need to end with an
            // unpaused runner, and only the draft one is exercised today -- which
            // is why this went unnoticed. A test that drives each path and asserts
            // tickCount advances would have caught it. Needs a seam first: Unpause()
            // is reached through a MonoBehaviour and a DOTween animation, neither of
            // which the EditMode suite can drive.
            OnTransitionComplete();
        }

        // ===== UPDATE =====

        private void Update()
        {
            if (matchPhase != MatchPhase.Playing) return;

            // Spawn views for bonus villagers created mid-game
            if (state.villagers.Length > trackedVillagerCount)
            {
                SpawnNewVillagerViews(trackedVillagerCount, state.villagers.Length);
                trackedVillagerCount = state.villagers.Length;
            }

            if (state.gameOver && !gameOverHandled)
            {
                gameOverHandled = true;
                ShowGameOver();
            }
        }

        // ===== INPUT SYSTEMS =====

        private NodeWar.Input.PointerGestureSource gestureSource;
        private NodeWar.Input.TapRouter tapRouter;
        private NodeWar.Input.HitFlashRouter hitFlashRouter;

        private void InitializeInputSystems()
        {
            selectionSystem = gameObject.AddComponent<SelectionSystem>();
            selectionSystem.Initialize(state, 0);

            commandSystem = gameObject.AddComponent<CommandSystem>();
            commandSystem.Initialize(state, inputBuffer, selectionSystem, 0);

            debugPlayerSwitch = gameObject.AddComponent<DebugPlayerSwitch>();
            debugPlayerSwitch.Initialize(selectionSystem, commandSystem);

            // The only thing that sets outline intents. Built here so it exists
            // before the views that register with it, and fed the same
            // SelectionSystem the tap path uses -- hover and selection then
            // agree about ownership by construction rather than by two copies
            // of the same rule.
            outlineDriver = gameObject.AddComponent<NodeWar.View.OutlineDriver>();
            outlineDriver.Initialize(state, selectionSystem, Camera.main);
            debugPlayerSwitch.OnPlayerSwitched += outlineDriver.OnPlayerSideChanged;

            // Selection and move orders share one pointer reader.
            // The router is wired in InitializeUI, once the panel exists.
            gestureSource = gameObject.AddComponent<NodeWar.Input.PointerGestureSource>();
            gestureSource.Initialize(Camera.main);

            tapRouter = gameObject.AddComponent<NodeWar.Input.TapRouter>();

            // Built before villagers spawn so the flash components they get on
            // creation have a router to reach them through.
            hitFlashRouter = gameObject.AddComponent<NodeWar.Input.HitFlashRouter>();
            hitFlashRouter.Initialize(gestureSource);

            // Lasso completion goes straight to the selection owner. Only
            // *taps* need arbitration -- a lasso has one meaning, so routing it
            // through TapRouter would add indirection without removing any.
            selectionSystem.SetGestureSource(gestureSource);

            // Opponent villagers are not tap targets; presses fall through them
            // to the node beneath.
            gestureSource.SetVillagerFilter(selectionSystem.IsSelectable);

            // One-finger drag pans the board. Middle-mouse still works for
            // desktop habit, but this is the path that exists on a phone.
            if (cameraController != null)
                cameraController.SetGestureSource(gestureSource);

            CreateSelectionLasso();
            CreateMovementPathRenderer();
        }

        /// <summary>
        /// The dotted routes for the local player movers.
        ///
        /// Node slot managers and the tick provider arrive through setters rather
        /// than the constructor call, because either may not exist yet depending
        /// on whether this runs before SpawnNodeViews and StartLocalPlay. Both
        /// call sites push their value in when they have it.
        /// </summary>
        private void CreateMovementPathRenderer()
        {
            GameObject routesGO = new GameObject("MovementRoutes");
            pathRenderer = routesGO.AddComponent<NodeWar.View.MovementPathRenderer>();

            // Read straight from the cross-scene MatchConnection rather than
            // waiting on OnPlayerSideChanged: DebugPlayerSwitch.LockToPlayer
            // fires that event before GameManager subscribes to it, so a
            // networked player 1 would otherwise be left watching player 0
            // routes. The event still corrects the Tab-key debug switch.
            MatchConnection match = MatchConnection.Instance;
            int localPID = (match != null && match.isNetworked) ? match.localPlayerID : 0;
            pathRenderer.Initialize(state, localPID, pathCurveSettings,
                                    opponentRouteSettings, tickProvider, Camera.main);
            pathRenderer.SetNodeSlotManagers(nodeSlotManagers);
        }

        private void OnPlayerSideChanged(int playerID)
        {
            if (cameraController != null)
                cameraController.SetPlayerSide(playerID);

            if (pathRenderer != null)
                pathRenderer.SetPlayerID(playerID);

            float rotation = playerID == 0 ? 180f : 0f;

            if (nodePresentations != null)
            {
                for (int i = 0; i < nodePresentations.Length; i++)
                {
                    if (nodePresentations[i] != null)
                        nodePresentations[i].RotateNode(rotation);
                }
            }
        }

        private void StartLocalPlay()
        {
            TickRunner tickRunner = gameObject.AddComponent<TickRunner>();
            tickRunner.Initialize(state, inputBuffer);
            tickProvider = tickRunner;
        }

        private void StartNetworkPlay(MatchConnection match)
        {
            int localPlayerID = match.localPlayerID;
            NetworkManager netManager = match.networkManager;

            debugPlayerSwitch.LockToPlayer(localPlayerID);

            lockstepRunner = gameObject.AddComponent<LockstepRunner>();
            lockstepRunner.Initialize(state, inputBuffer, netManager, localPlayerID);
            lockstepRunner.OnDisconnect += OnNetworkDisconnect;
            lockstepRunner.OnDesync += OnDesyncDetected;
            tickProvider = lockstepRunner;

            Debug.Log("[GameManager] Network match started. Local player: " + localPlayerID);
        }

        private void OnNetworkDisconnect()
        {
            Debug.LogError("[GameManager] Opponent disconnected.");
            if (gameOverHandled) return;
            gameOverHandled = true;
            ShowDisconnect();
        }

        private void OnDesyncDetected(int tick)
        {
            Debug.LogError("[GameManager] DESYNC at tick " + tick + "! Determinism bug exists.");
        }

        private void CreateSelectionLasso()
        {
            GameObject lassoGO = new GameObject("SelectionLasso");
            lassoGO.AddComponent<LineRenderer>();
            SelectionLasso lasso = lassoGO.AddComponent<SelectionLasso>();
            lasso.Initialize(gestureSource, Camera.main);

            // Separate object: the cue sits on the ground plane under the
            // finger, while the lasso line is projected near the camera.
            GameObject cueGO = new GameObject("LassoArmedCue");
            LassoArmedCue cue = cueGO.AddComponent<LassoArmedCue>();
            cue.Initialize(gestureSource, Camera.main, cameraController);
        }

        // ===== GAME OVER =====

        /// <summary>
        /// Which player is watching: the local one in a networked match, and
        /// otherwise whoever the debug switch is controlling. The result screen
        /// is written from this side, so it is asked rather than assumed to be
        /// player 0.
        /// </summary>
        private int ViewerPlayerID()
        {
            MatchConnection match = MatchConnection.Instance;
            if (match != null && match.isNetworked) return match.localPlayerID;

            return debugPlayerSwitch != null ? debugPlayerSwitch.GetCurrentPlayerID() : 0;
        }

        /// <summary>
        /// Ends the match on whichever HUD stack is live. Neither one is handed
        /// a string to print: both are told the state and who is looking, and
        /// word the result themselves.
        /// </summary>
        private void ShowGameOver()
        {
            if (transitionController != null)
                transitionController.PlayNodeBreakdownWave(nodePresentations, state);

            int viewer = ViewerPlayerID();

            if (uiToolkitHud != null)
            {
                uiToolkitHud.ShowMatchEnd(viewer);
                return;
            }

            if (gameOverPanel == null)
            {
                Debug.LogWarning("[GameManager] GameOverPanel not found.");
                return;
            }

            gameOverPanel.ShowResult(state, viewer, balance.Data.breachThreshold);
        }

        private void ShowDisconnect()
        {
            int viewer = ViewerPlayerID();

            if (uiToolkitHud != null && state != null)
            {
                uiToolkitHud.ShowDisconnected(viewer);
                return;
            }

            if (gameOverPanel == null)
            {
                Debug.LogWarning("[GameManager] GameOverPanel not found.");
                return;
            }

            if (state != null)
            {
                gameOverPanel.ShowDisconnected(state, viewer, balance.Data.breachThreshold);
                return;
            }

            // A disconnect during the draft, before any simulation exists.
            gameOverPanel.Show("DISCONNECTED", new Color(1f, 0.8f, 0.2f),
                               "Opponent has disconnected.");
        }

        private void ReturnToLobby()
        {
            if (MatchConnection.Instance != null)
                MatchConnection.Instance.Shutdown();
            SceneManager.LoadScene("Lobby");
        }

        // ===== UI =====

        private void InitializeUI()
        {
            if (uiManagerPrefab == null)
            {
                Debug.LogError("[GameManager] uiManagerPrefab not assigned!");
                return;
            }

            GameObject uiGO = Instantiate(uiManagerPrefab);
            uiGO.name = "UIManager";

            hudManager = uiGO.GetComponent<HUDManager>();
            if (hudManager != null)
                hudManager.Initialize(state, debugPlayerSwitch, balance.Data.breachThreshold);

            nodePanelManager = uiGO.GetComponentInChildren<NodePanelManager>();
            if (outlineDriver != null) outlineDriver.BindPanel(nodePanelManager);
            if (nodePanelManager != null)
                nodePanelManager.Initialize(state, inputBuffer, selectionSystem, debugPlayerSwitch,
                                            tickProvider, balance.Data);

            // After the node panel, not before: the new HUD subscribes to it,
            // and suppressing a manager that has not been initialised would hand
            // the sheet an event source with no simulation behind it.
            ApplyHUDStackChoice(uiGO);

            gameOverPanel = uiGO.GetComponentInChildren<GameOverPanel>(true);
            if (gameOverPanel != null)
                gameOverPanel.OnReturnToLobby += ReturnToLobby;
            else
                Debug.LogWarning("[GameManager] GameOverPanel not found in UIManager prefab.");

            WireGestureRouting();
        }

        /// <summary>
        /// Turns on exactly one of the two HUD bands.
        ///
        /// Only the band - the resource readouts, the breach bars and the
        /// villager count. The node panels, the draft UI and the game-over
        /// panel all live in the same prefab and are untouched by this, because
        /// they are not what S6 replaced. That is why this hides HUD_Canvas
        /// rather than the prefab: HUDManager sits on the prefab root alongside
        /// NodePanelManager, so disabling its GameObject would take the panels
        /// with it.
        ///
        /// HUD_Canvas is found by name. That is a weak link and it is
        /// deliberate: the alternative is a serialized field, and adding one
        /// means editing UI_Manager.prefab, which is exactly the kind of change
        /// this migration has avoided while both stacks are live. It fails
        /// loudly rather than silently drawing two HUDs on top of each other.
        /// </summary>
        private void ApplyHUDStackChoice(GameObject uiGO)
        {
            if (!useUIToolkitHUD)
            {
                if (uiToolkitHudRoot != null) uiToolkitHudRoot.SetActive(false);
                return;
            }

            if (uiToolkitHudRoot == null)
            {
                Debug.LogWarning("[GameManager] useUIToolkitHUD is on but no uiToolkitHudRoot " +
                                 "is assigned. Keeping the uGUI HUD. Run " +
                                 "Tools > Node War > Set Up UI Toolkit HUD.");
                return;
            }

            Canvas[] canvases = uiGO.GetComponentsInChildren<Canvas>(true);
            bool hidden = false;

            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].gameObject.name != "HUD_Canvas") continue;

                canvases[i].gameObject.SetActive(false);
                hidden = true;
                break;
            }

            if (!hidden)
            {
                Debug.LogError("[GameManager] Could not find HUD_Canvas in the UI prefab, so the " +
                               "uGUI HUD cannot be hidden. Leaving the UI Toolkit HUD off rather " +
                               "than drawing both.");
                return;
            }

            uiToolkitHudRoot.SetActive(true);

            uiToolkitHud = uiToolkitHudRoot.GetComponent<GameplayHUDController>();

            if (uiToolkitHud == null)
            {
                Debug.LogWarning("[GameManager] uiToolkitHudRoot has no GameplayHUDController.");
                return;
            }

            uiToolkitHud.Initialize(state, debugPlayerSwitch, balance.Data.breachThreshold,
                                    inputBuffer, tickProvider, balance.Data, nodePanelManager,
                                    selectionSystem);

            uiToolkitHud.ReturnToLobby += ReturnToLobby;

            // The zoom readout and the zoom handle. Without this the handle
            // still takes the press but has nothing to drive, and says so.
            uiToolkitHud.BindCamera(cameraController);

            // The countdown belongs to whichever stack is live, or two would
            // run at once. The uGUI prefab is used when this is not set.
            if (transitionController != null)
                transitionController.SetCountdownPresenter(uiToolkitHud);

            // Only hand the node panel over if the new sheet actually exists.
            // Without a layout assigned the uGUI panel keeps the job, which is
            // better than a tap that opens nothing at all.
            if (nodePanelManager != null)
                nodePanelManager.SetSuppressed(uiToolkitHud.HasNodeSheet);
        }

        /// <summary>
        /// Hands tap arbitration to the router and right-clicks to commands.
        /// Deferred to here because the router needs the panel,
        /// which only exists once the UI prefab is instantiated.
        ///
        /// Selection and panel legacy reads are gated off. CommandSystem only
        /// consumes resolved pointer intent; its keyboard shortcuts stay live.
        /// </summary>
        private void WireGestureRouting()
        {
            if (gestureSource == null || tapRouter == null) return;

            tapRouter.Initialize(gestureSource, selectionSystem, commandSystem, nodePanelManager);

            if (selectionSystem != null) selectionSystem.SetGestureRouted(true);

            if (commandSystem != null)
            {
                commandSystem.SetGestureSource(gestureSource);
                commandSystem.SetGestureRouted(true);
            }

            if (nodePanelManager != null)
            {
                nodePanelManager.SetGestureRouted(true);
                nodePanelManager.SetGestureSource(gestureSource);
                nodePanelManager.SetCameraController(cameraController);
                nodePanelManager.SetNodeViews(nodeViews);
            }
        }

        // ===== NODE INITIALIZATION (from draft) =====

        private void InitializeNodesFromDraft(DraftResult result)
        {
            int GRID_COLS = boardConfig.Data.gridCols;
            int GRID_ROWS = boardConfig.Data.gridRows;
            state.nodes = new NodeData[GRID_COLS * GRID_ROWS];

            // Step 1: Create all nodes with grid topology, no district types
            for (int z = 0; z < GRID_ROWS; z++)
            {
                for (int x = 0; x < GRID_COLS; x++)
                {
                    int nodeID = z * GRID_COLS + x;
                    List<int> neighborIDs = new List<int>();
                    if (x > 0) neighborIDs.Add(z * GRID_COLS + (x - 1));
                    if (x < GRID_COLS - 1) neighborIDs.Add(z * GRID_COLS + (x + 1));
                    if (z > 0) neighborIDs.Add((z - 1) * GRID_COLS + x);
                    if (z < GRID_ROWS - 1) neighborIDs.Add((z + 1) * GRID_COLS + x);

                    Edge[] edges = new Edge[neighborIDs.Count];
                    for (int i = 0; i < neighborIDs.Count; i++)
                        edges[i] = new Edge { toNode = neighborIDs[i], travelWeight = boardConfig.Data.defaultEdgeWeight };

                    state.nodes[nodeID] = new NodeData
                    {
                        nodeID = nodeID,
                        gridX = x,
                        gridZ = z,
                        edges = edges,
                        districtType = DistrictType.None,
                        baseDistrictType = DistrictType.None,
                        slotType = NodeSlotType.Fixed,
                        claimBar = 0,
                        ownerID = -1,
                        bonusVillagersOnClaim = 0,
                        materialAllocation = 0
                    };
                }
            }

            // Step 2: Apply BoardConfig initial placements (cores, fixed nodes)
            if (boardConfig.Data.initialPlacements != null)
            {
                for (int i = 0; i < boardConfig.Data.initialPlacements.Length; i++)
                {
                    var ip = boardConfig.Data.initialPlacements[i];
                    int nodeID = ip.gridZ * GRID_COLS + ip.gridX;
                    state.nodes[nodeID].districtType = ip.districtType;
                    state.nodes[nodeID].baseDistrictType = ip.districtType;
                    state.nodes[nodeID].ownerID = ip.ownerID;
                    state.nodes[nodeID].claimBar = ip.claimBar;
                }
            }

            // Step 3: Apply draft placements (unowned, player-chosen positions)
            if (result.placements != null)
            {
                for (int i = 0; i < result.placements.Length; i++)
                {
                    var dp = result.placements[i];
                    int nodeID = dp.gridZ * GRID_COLS + dp.gridX;
                    state.nodes[nodeID].districtType = dp.districtType;
                    state.nodes[nodeID].baseDistrictType = dp.districtType;
                    state.nodes[nodeID].ownerID = -1;
                    state.nodes[nodeID].slotType = NodeSlotType.Fixed;

                    if (dp.districtType == DistrictType.Village)
                        state.nodes[nodeID].bonusVillagersOnClaim = balance.Data.bonusVillagersOnVillageClaim;
                }
            }
        }

        // ===== NODE INITIALIZATION (legacy testing mode) =====

        private void InitializeNodes()
        {
            int GRID_COLS = boardConfig.Data.gridCols;
            int GRID_ROWS = boardConfig.Data.gridRows;
            state.nodes = new NodeData[GRID_COLS * GRID_ROWS];

            DistrictType[,] layout = new DistrictType[GRID_ROWS, GRID_COLS];
            layout[0, 0] = DistrictType.None; layout[0, 1] = DistrictType.None; layout[0, 2] = DistrictType.Core; layout[0, 3] = DistrictType.None;
            layout[1, 0] = DistrictType.None; layout[1, 1] = DistrictType.Mine; layout[1, 2] = DistrictType.Farm; layout[1, 3] = DistrictType.None;
            layout[2, 0] = DistrictType.Mine; layout[2, 1] = DistrictType.Barracks; layout[2, 2] = DistrictType.Village; layout[2, 3] = DistrictType.Farm;
            layout[3, 0] = DistrictType.Forge; layout[3, 1] = DistrictType.Market; layout[3, 2] = DistrictType.Market; layout[3, 3] = DistrictType.Forge;
            layout[4, 0] = DistrictType.Farm; layout[4, 1] = DistrictType.Village; layout[4, 2] = DistrictType.Barracks; layout[4, 3] = DistrictType.Mine;
            layout[5, 0] = DistrictType.None; layout[5, 1] = DistrictType.Farm; layout[5, 2] = DistrictType.Mine; layout[5, 3] = DistrictType.None;
            layout[6, 0] = DistrictType.None; layout[6, 1] = DistrictType.Core; layout[6, 2] = DistrictType.None; layout[6, 3] = DistrictType.None;

            for (int z = 0; z < GRID_ROWS; z++)
            {
                for (int x = 0; x < GRID_COLS; x++)
                {
                    int nodeID = z * GRID_COLS + x;
                    List<int> neighborIDs = new List<int>();
                    if (x > 0) neighborIDs.Add(z * GRID_COLS + (x - 1));
                    if (x < GRID_COLS - 1) neighborIDs.Add(z * GRID_COLS + (x + 1));
                    if (z > 0) neighborIDs.Add((z - 1) * GRID_COLS + x);
                    if (z < GRID_ROWS - 1) neighborIDs.Add((z + 1) * GRID_COLS + x);

                    Edge[] edges = new Edge[neighborIDs.Count];
                    for (int i = 0; i < neighborIDs.Count; i++)
                        edges[i] = new Edge { toNode = neighborIDs[i], travelWeight = boardConfig.Data.defaultEdgeWeight };

                    int bonus = layout[z, x] == DistrictType.Village ? balance.Data.bonusVillagersOnVillageClaim : 0;
                    int ownerID = -1;
                    int claimBar = 0;
                    if (z == 6 && x == 1) { ownerID = 0; claimBar = balance.Data.claimThreshold; }
                    if (z == 0 && x == 2) { ownerID = 1; claimBar = -balance.Data.claimThreshold; }

                    state.nodes[nodeID] = new NodeData
                    {
                        nodeID = nodeID,
                        gridX = x,
                        gridZ = z,
                        edges = edges,
                        districtType = layout[z, x],
                        baseDistrictType = layout[z, x],
                        slotType = NodeSlotType.Fixed,
                        claimBar = claimBar,
                        ownerID = ownerID,
                        bonusVillagersOnClaim = bonus,
                        materialAllocation = 0
                    };
                }
            }
        }

        private void InitializePlayers()
        {
            state.players = new PlayerData[2];

            MatchConnection match = MatchConnection.Instance;
            NodeWar.Lobby.LoadoutData loadout = (match != null)
                ? match.loadout
                : new NodeWar.Lobby.LoadoutData();

            state.players[0] = new PlayerData
            {
                playerID = 0,
                coreNodeID = FindCoreNodeID(0),
                food = boardConfig.Data.startingFood,
                materials = boardConfig.Data.startingMaterials,
                metal = boardConfig.Data.startingMetal,
                breachCount = 0,
                draftedSuits = BuildDraftedSuits(0, loadout),
                draftedNodes = BuildDraftedNodes(0, loadout)
            };
            state.players[1] = new PlayerData
            {
                playerID = 1,
                coreNodeID = FindCoreNodeID(1),
                food = boardConfig.Data.startingFood,
                materials = boardConfig.Data.startingMaterials,
                metal = boardConfig.Data.startingMetal,
                breachCount = 0,
                draftedSuits = BuildDraftedSuits(1, loadout),
                draftedNodes = BuildDraftedNodes(1, loadout)
            };

            // Correct core node data to match resolved player assignments.
            // Guards against misconfigured BoardConfig asset ownerID values.
            int p0CoreID = state.players[0].coreNodeID;
            int p1CoreID = state.players[1].coreNodeID;

            state.nodes[p0CoreID].ownerID = 0;
            state.nodes[p0CoreID].claimBar = balance.Data.claimThreshold;
            state.nodes[p1CoreID].ownerID = 1;
            state.nodes[p1CoreID].claimBar = -balance.Data.claimThreshold;

            // Where "home" actually is. InitializeSides runs in Awake, before
            // the board exists, so it can only guess from grid dimensions -- and
            // its guess is the middle of your back row, which is the board's
            // centre line, not your core. The cores sit wherever the layout puts
            // them, so the real positions have to come back here once known.
            if (cameraController != null)
            {
                cameraController.SetHomeAnchor(0, CoreWorldPosition(p0CoreID));
                cameraController.SetHomeAnchor(1, CoreWorldPosition(p1CoreID));
            }
        }

        /// <summary>
        /// Node world positions are laid out by SpawnNodeViews as grid index
        /// times nodeScale. Duplicated as one line here rather than read off a
        /// NodeView, because the cameras need it before the views exist.
        /// </summary>
        private Vector3 CoreWorldPosition(int nodeID)
        {
            return new Vector3(
                state.nodes[nodeID].gridX * boardConfig.nodeScale,
                0f,
                state.nodes[nodeID].gridZ * boardConfig.nodeScale);
        }

        private int FindCoreNodeID(int playerID)
        {
            // Position-based: P0 owns the highest-Z core, P1 owns the lowest-Z core.
            // This is robust regardless of ownerID values in the asset.
            int lowestZNode = -1;
            int highestZNode = -1;
            int lowestZ = int.MaxValue;
            int highestZ = int.MinValue;

            for (int i = 0; i < state.nodes.Length; i++)
            {
                if (state.nodes[i].districtType != DistrictType.Core) continue;

                int z = state.nodes[i].gridZ;
                if (z < lowestZ) { lowestZ = z; lowestZNode = i; }
                if (z > highestZ) { highestZ = z; highestZNode = i; }
            }

            if (playerID == 0)
                return highestZNode >= 0 ? highestZNode : 25;
            else
                return lowestZNode >= 0 ? lowestZNode : 2;
        }

        private int[] BuildDraftedSuits(int playerID, NodeWar.Lobby.LoadoutData localLoadout)
        {
            List<int> suits = new List<int>();

            // Global suits (always available)
            suits.Add((int)SuitType.Warrior);

            // Determine which loadout belongs to this player
            MatchConnection match = MatchConnection.Instance;
            NodeWar.Lobby.LoadoutData playerLoadout;

            if (match == null)
            {
                playerLoadout = localLoadout;
            }
            else if (playerID == match.localPlayerID)
            {
                playerLoadout = cachedLocalLoadout;
            }
            else
            {
                playerLoadout = cachedRemoteLoadout;
            }

            playerLoadout = NodeWar.Lobby.LoadoutData.Normalized(playerLoadout);
            for (int i = 0; i < playerLoadout.suitIDs.Length; i++)
                AddSuitFromID(suits, playerLoadout.suitIDs[i]);

            return suits.ToArray();
        }

        private int[] BuildDraftedNodes(int playerID, NodeWar.Lobby.LoadoutData localLoadout)
        {
            List<int> nodes = new List<int>();

            MatchConnection match = MatchConnection.Instance;
            NodeWar.Lobby.LoadoutData playerLoadout;

            if (match == null)
            {
                playerLoadout = localLoadout;
            }
            else if (playerID == match.localPlayerID)
            {
                playerLoadout = cachedLocalLoadout;
            }
            else
            {
                playerLoadout = cachedRemoteLoadout;
            }

            playerLoadout = NodeWar.Lobby.LoadoutData.Normalized(playerLoadout);
            for (int i = 0; i < playerLoadout.nodeIDs.Length; i++)
                AddNodeFromID(nodes, playerLoadout.nodeIDs[i]);

            return nodes.ToArray();
        }

        private void AddSuitFromID(List<int> suits, string suitID)
        {
            if (string.IsNullOrEmpty(suitID)) return;
            SuitType type = MapSuitIDToType(suitID);
            if (type == SuitType.None) return;
            int intType = (int)type;
            // Prevent duplicates
            for (int i = 0; i < suits.Count; i++)
            {
                if (suits[i] == intType) return;
            }
            suits.Add(intType);
        }

        private void AddNodeFromID(List<int> nodes, string nodeID)
        {
            if (string.IsNullOrEmpty(nodeID)) return;
            DistrictType type = DraftManager.MapNodeIDToDistrict(nodeID);
            if (type == DistrictType.None) return;
            int intType = (int)type;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] == intType) return;
            }
            nodes.Add(intType);
        }

        private SuitType MapSuitIDToType(string suitID)
        {
            if (suitID == null) return SuitType.None;
            string lower = suitID.ToLower();

            if (lower.Contains("warrior")) return SuitType.Warrior;
            if (lower.Contains("guardian")) return SuitType.Guardian;
            if (lower.Contains("scout")) return SuitType.Scout;
            if (lower.Contains("berserker")) return SuitType.Berserker;
            if (lower.Contains("medic")) return SuitType.Medic;

            return SuitType.None;
        }

        private void InitializeVillagers()
        {
            int totalVillagers = boardConfig.Data.startingVillagersPerPlayer * 2;
            state.villagers = new VillagerData[totalVillagers];

            for (int i = 0; i < totalVillagers; i++)
            {
                int owner = (i < boardConfig.Data.startingVillagersPerPlayer) ? 0 : 1;
                int coreNode = state.players[owner].coreNodeID;

                state.villagers[i] = new VillagerData
                {
                    villagerID = i,
                    ownerID = owner,
                    currentNodeID = coreNode,
                    targetNodeID = -1,
                    movePath = new int[0],
                    movePathIndex = 0,
                    moveProgress = 0,
                    previousNodeID = coreNode,
                    state = VillagerState.Idle,
                    suit = SuitType.None,
                    hp = balance.Data.baseHP,
                    maxHP = balance.Data.baseHP,
                    attackDamage = balance.Data.baseAttackDamage,
                    moveSpeedTicks = balance.Data.baseMoveSpeedTicks,
                    respawnTicksRemaining = 0,
                    attackCooldownRemaining = balance.Data.baseAttackCooldownMax,
                    attackCooldownMax = balance.Data.baseAttackCooldownMax,
                    combatTargetID = -1,
                    fightPriority = 0,
                    isConsumed = false,
                    productionTicksRemaining = 0,
                    productionTicksMax = 0
                };
            }
        }

        // ===== VIEW SPAWNING =====

        private GameObject GetPrefabForDistrict(DistrictType type)
        {
            GameObject prefab = null;
            switch (type)
            {
                case DistrictType.Core: prefab = nodePrefabCore; break;
                case DistrictType.Farm: prefab = nodePrefabFarm; break;
                case DistrictType.Mine: prefab = nodePrefabMine; break;
                case DistrictType.Village: prefab = nodePrefabVillage; break;
                case DistrictType.Barracks: prefab = nodePrefabBarracks; break;
                case DistrictType.Forge: prefab = nodePrefabForge; break;
                case DistrictType.Camp: prefab = nodePrefabCamp; break;
                case DistrictType.Shrine: prefab = nodePrefabShrine; break;
                case DistrictType.Arsenal: prefab = nodePrefabArsenal; break;
                case DistrictType.Sanctuary: prefab = nodePrefabSanctuary; break;
                case DistrictType.Watchtower: prefab = nodePrefabWatchtower; break;
                case DistrictType.Rampart: prefab = nodePrefabRampart; break;
                case DistrictType.Market: prefab = nodePrefabMarket; break;
                default: prefab = nodePrefabDefault; break;
            }
            if (prefab == null) prefab = nodePrefabDefault;
            return prefab;
        }

        private void SpawnNodeViews()
        {
            nodeParent = new GameObject("NodeViews").transform;
            nodeSlotManagers = new NodeWar.View.NodeSlotManager[state.nodes.Length];
            nodePresentations = new NodeWar.View.NodePresentation[state.nodes.Length];
            nodeViews = new NodeWar.View.NodeView[state.nodes.Length];
            nodeOutlines = new OutlineGroup[state.nodes.Length];

            for (int i = 0; i < state.nodes.Length; i++)
            {
                GameObject prefab = GetPrefabForDistrict(state.nodes[i].districtType);
                GameObject nodeGO = Instantiate(prefab, nodeParent);
                nodeGO.name = "NodeView_" + i + "_" + state.nodes[i].districtType.ToString();
                nodeGO.transform.position = new Vector3(
                    state.nodes[i].gridX * boardConfig.nodeScale,
                    0f,
                    state.nodes[i].gridZ * boardConfig.nodeScale);

                NodeWar.View.NodeView view = nodeGO.GetComponent<NodeWar.View.NodeView>();
                if (view != null)
                    view.Initialize(state, i, balance.Data.claimThreshold);
                nodeViews[i] = view;

                NodeWar.View.NodeSlotManager slotManager = nodeGO.GetComponent<NodeWar.View.NodeSlotManager>();
                if (slotManager == null)
                    slotManager = nodeGO.AddComponent<NodeWar.View.NodeSlotManager>();
                slotManager.Initialize(i, boardConfig.nodeScale);
                nodeSlotManagers[i] = slotManager;

                NodeWar.View.NodePresentation presentation = nodeGO.GetComponent<NodeWar.View.NodePresentation>();
                if (presentation == null)
                    presentation = nodeGO.AddComponent<NodeWar.View.NodePresentation>();
                nodePresentations[i] = presentation;

                // One group for the whole node: the ground quad and every
                // building sprite share an ID, so the seams between them grow
                // no line and the node reads as a single silhouette. The
                // move-order pulse ring is a LineRenderer and the claim bar a
                // CanvasRenderer, so neither is collected.
                nodeOutlines[i] = nodeGO.AddComponent<OutlineGroup>();

                NodeClaimBar claimBar = nodeGO.GetComponentInChildren<NodeClaimBar>();
                if (claimBar != null)
                    claimBar.Initialize(state, i, balance.Data.claimThreshold);
            }

            if (outlineDriver != null) outlineDriver.SetNodeGroups(nodeOutlines);

            // Pre-hide all nodes. Transition controller reveals them during startup wave.
            for (int i = 0; i < nodePresentations.Length; i++)
            {
                if (nodePresentations[i] != null)
                    nodePresentations[i].SetHidden();
            }

            selectionSystem.SetNodeSlotManagers(nodeSlotManagers);
            if (pathRenderer != null)
                pathRenderer.SetNodeSlotManagers(nodeSlotManagers);

            // Lets a move issued by node ID still fire the destination
            // highlight, which the raycast path got from the hit directly.
            if (commandSystem != null)
                commandSystem.SetNodeViews(nodeViews);
        }

        private void SpawnVillagerViews()
        {
            villagerParent = new GameObject("VillagerViews").transform;
            villagerTransforms = new Transform[state.villagers.Length];
            villagerOutlines = new OutlineGroup[state.villagers.Length];

            for (int i = 0; i < state.villagers.Length; i++)
                SpawnSingleVillagerView(i);

            selectionSystem.SetVillagerTransforms(villagerTransforms);
            if (outlineDriver != null) outlineDriver.SetVillagerGroups(villagerOutlines);
            if (pathRenderer != null)
                pathRenderer.SetTickProvider(tickProvider);
            if (hitFlashRouter != null)
                hitFlashRouter.SetVillagerTransforms(villagerTransforms);
        }

        private void SpawnNewVillagerViews(int fromIndex, int toIndex)
        {
            Transform[] newArray = new Transform[toIndex];
            for (int i = 0; i < villagerTransforms.Length; i++)
                newArray[i] = villagerTransforms[i];
            villagerTransforms = newArray;

            // Grown in step. SpawnSingleVillagerView writes into this by index,
            // so a stale length here would silently stop outlining every
            // villager produced by a bonus spawn.
            OutlineGroup[] newOutlines = new OutlineGroup[toIndex];
            for (int i = 0; i < villagerOutlines.Length; i++)
                newOutlines[i] = villagerOutlines[i];
            villagerOutlines = newOutlines;

            for (int i = fromIndex; i < toIndex; i++)
                SpawnSingleVillagerView(i);

            selectionSystem.SetVillagerTransforms(villagerTransforms);
            if (outlineDriver != null) outlineDriver.SetVillagerGroups(villagerOutlines);
            if (pathRenderer != null)
                pathRenderer.SetTickProvider(tickProvider);
            if (hitFlashRouter != null)
                hitFlashRouter.SetVillagerTransforms(villagerTransforms);
        }

        private void SpawnSingleVillagerView(int index)
        {
            GameObject villagerGO = Instantiate(villagerPrefab, villagerParent);
            villagerGO.name = "V_" + index + "_P" + state.villagers[index].ownerID +
                "_" + state.villagers[index].suit + "_" + state.villagers[index].state;

            if (index < villagerTransforms.Length)
                villagerTransforms[index] = villagerGO.transform;

            NodeWar.View.VillagerView view = villagerGO.GetComponent<NodeWar.View.VillagerView>();
            if (view != null)
            {
                view.Initialize(state, index);
                view.SetTickProvider(tickProvider);
                view.SetSelectionSystem(selectionSystem);
                view.SetNodeSlotManagers(nodeSlotManagers);
                view.SetPathCurveSettings(pathCurveSettings);

                NodeWar.View.VillagerFlash flash = villagerGO.AddComponent<NodeWar.View.VillagerFlash>();
                flash.Initialize(view, gestureSource != null
                    ? gestureSource.Thresholds.flashDuration
                    : 0.12f);
            }

            // Added at runtime, like the touch target below, so the villager
            // prefab needs no edit. The component collects its own silhouette
            // from the children on enable -- MeshRenderer and SpriteRenderer
            // only, so the health ring's CanvasRenderer stays out of it.
            if (index < villagerOutlines.Length)
                villagerOutlines[index] = villagerGO.AddComponent<OutlineGroup>();

            // Constant-size tap target, so a villager stays hittable at the far
            // end of the dolly range where its sprite is only a few pixels.
            NodeWar.View.VillagerTouchTarget touchTarget =
                villagerGO.AddComponent<NodeWar.View.VillagerTouchTarget>();
            touchTarget.Initialize(Camera.main, gestureSource != null
                ? gestureSource.Thresholds
                : null);

            VillagerHealthRing healthRing = villagerGO.GetComponentInChildren<VillagerHealthRing>();
            if (healthRing != null)
                healthRing.Initialize(state, index);
        }
    }
}
