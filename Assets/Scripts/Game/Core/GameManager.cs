using UnityEngine;
using UnityEngine.SceneManagement;
using NodeWar.Simulation;
using NodeWar.Config;
using NodeWar.Input;
using NodeWar.Debugging;
using NodeWar.UI;
using NodeWar.Network;
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

            if (draftUIPrefab != null)
            {
                GameObject uiGO = Instantiate(draftUIPrefab);
                NodeWar.UI.DraftUI draftUI = uiGO.GetComponent<NodeWar.UI.DraftUI>();
                if (draftUI != null)
                {
                    draftUI.Initialize(draftManager, match.isNetworked ? match.localPlayerID : 0);
                    draftManager.SetDraftUI(draftUI);
                }
            }
        }

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

            // Gather placeholders from DraftUI before it becomes irrelevant
            DraftUI draftUI = FindAnyObjectByType<DraftUI>();
            List<GameObject> placeholders = draftUI != null ? draftUI.GetPersistentPlacements() : null;

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

            // The gesture source must be the only device reader, so it is built
            // before anything that could otherwise be tempted to read one.
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

            //Vector3 spriteRot = cameraController != null
            //    ? cameraController.GetSpriteRotation()
            //    : new Vector3(50f, playerID == 0 ? 180f : 0f, 0f);
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

        private void ShowGameOver()
        {
            if (transitionController != null)
                transitionController.PlayNodeBreakdownWave(nodePresentations, state);

            if (gameOverPanel == null)
            {
                Debug.LogWarning("[GameManager] GameOverPanel not found.");
                return;
            }

            Color winnerColor = (state.winnerID == 0)
                ? new Color(0.3f, 0.5f, 1f)
                : new Color(1f, 0.3f, 0.3f);

            string title = "PLAYER " + state.winnerID + " WINS!";
            string info = "Breaches - P0: " + state.players[0].breachCount +
                          "  P1: " + state.players[1].breachCount +
                          "\nGame ended at tick " + state.tickCount;

            gameOverPanel.Show(title, winnerColor, info);
        }

        private void ShowDisconnect()
        {
            if (gameOverPanel == null)
            {
                Debug.LogWarning("[GameManager] GameOverPanel not found.");
                return;
            }

            gameOverPanel.Show(
                "DISCONNECTED",
                new Color(1f, 0.8f, 0.2f),
                "Opponent has disconnected."
            );
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

            ApplyHUDStackChoice(uiGO);

            nodePanelManager = uiGO.GetComponentInChildren<NodePanelManager>();
            if (nodePanelManager != null)
                nodePanelManager.Initialize(state, inputBuffer, selectionSystem, debugPlayerSwitch,
                                            tickProvider, balance.Data);

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

            if (uiToolkitHud != null)
                uiToolkitHud.Initialize(state, debugPlayerSwitch, balance.Data.breachThreshold);
            else
                Debug.LogWarning("[GameManager] uiToolkitHudRoot has no GameplayHUDController.");
        }

        /// <summary>
        /// Hands tap arbitration to the router and silences the three legacy
        /// input paths. Deferred to here because the router needs the panel,
        /// which only exists once the UI prefab is instantiated.
        ///
        /// The legacy paths are gated rather than deleted: flipping both
        /// SetGestureRouted calls to false restores the old behaviour intact,
        /// which is the comparison the thresholds still need.
        /// </summary>
        private void WireGestureRouting()
        {
            if (gestureSource == null || tapRouter == null) return;

            tapRouter.Initialize(gestureSource, selectionSystem, commandSystem, nodePanelManager);

            if (selectionSystem != null) selectionSystem.SetGestureRouted(true);

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
            DistrictType type = MapNodeIDToDistrict(nodeID);
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

        private DistrictType MapNodeIDToDistrict(string nodeID)
        {
            if (nodeID == null) return DistrictType.None;
            string lower = nodeID.ToLower();

            if (lower.Contains("farm")) return DistrictType.Farm;
            if (lower.Contains("mine")) return DistrictType.Mine;
            if (lower.Contains("village")) return DistrictType.Village;
            if (lower.Contains("barracks")) return DistrictType.Barracks;
            if (lower.Contains("forge")) return DistrictType.Forge;
            if (lower.Contains("camp")) return DistrictType.Camp;
            if (lower.Contains("shrine")) return DistrictType.Shrine;
            if (lower.Contains("arsenal")) return DistrictType.Arsenal;
            if (lower.Contains("sanctuary")) return DistrictType.Sanctuary;
            if (lower.Contains("watchtower")) return DistrictType.Watchtower;
            if (lower.Contains("rampart")) return DistrictType.Rampart;
            if (lower.Contains("market")) return DistrictType.Market;
            //if (lower.Contains("crossroads")) return DistrictType.Crossroads;

            return DistrictType.None;
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

                NodeClaimBar claimBar = nodeGO.GetComponentInChildren<NodeClaimBar>();
                if (claimBar != null)
                    claimBar.Initialize(state, i, balance.Data.claimThreshold);
            }

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

            for (int i = 0; i < state.villagers.Length; i++)
                SpawnSingleVillagerView(i);

            selectionSystem.SetVillagerTransforms(villagerTransforms);
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

            for (int i = fromIndex; i < toIndex; i++)
                SpawnSingleVillagerView(i);

            selectionSystem.SetVillagerTransforms(villagerTransforms);
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