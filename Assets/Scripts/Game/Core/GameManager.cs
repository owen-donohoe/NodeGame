using UnityEngine;
using UnityEngine.SceneManagement;
using NodeWar.Simulation;
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

        [Header("UI")]
        [SerializeField] private GameObject uiManagerPrefab;
        private NodePanelManager nodePanelManager;
        private GameOverPanel gameOverPanel;

        [Header("Runtime")]
        public SimulationState state;

        private DebugPlayerSwitch debugPlayerSwitch;
        private HUDManager hudManager;

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


        // Network references
        private LockstepRunner lockstepRunner;
        private NodeWar.Input.BotPlayer botPlayer;

        // Game over tracking
        private bool gameOverHandled = false;

        private CameraController cameraController;

        [Header("Draft")]
        [SerializeField] private GameObject draftUIPrefab;
        [SerializeField] private GameObject countdownUIPrefab;
        [SerializeField] private GameObject placementPreviewPrefab;
        [SerializeField] private GameObject gridCellMarkerPrefab;

        private enum MatchPhase
        {
            PreDraft,
            Drafting,
            PostDraft,
            Countdown,
            Playing
        }

        private MatchPhase matchPhase = MatchPhase.PreDraft;
        private DraftManager draftManager;
        private DraftResult? pendingDraftResult;

        private void Awake()
        {
            Application.runInBackground = true;

            state = new SimulationState();

            if (balance == null)
            {
                Debug.LogError("[GameManager] GameBalance asset not assigned!");
                return;
            }
            if (boardConfig == null)
            {
                Debug.LogError("[GameManager] BoardConfig asset not assigned!");
                return;
            }

            // Phase 1: Pre-draft setup (no board/state dependency)
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);

            Pathfinding.OwnedMultiplier = boardConfig.ownedMultiplier;
            Pathfinding.PartiallyOwnedMultiplier = boardConfig.partiallyOwnedMultiplier;
            Pathfinding.UnownedMultiplier = boardConfig.unownedMultiplier;
            Pathfinding.EnemyPartiallyOwnedMultiplier = boardConfig.enemyPartiallyOwnedMultiplier;
            Pathfinding.EnemyOwnedMultiplier = boardConfig.enemyOwnedMultiplier;

            inputBuffer = new InputBuffer();

            cameraController = FindAnyObjectByType<CameraController>();
            if (cameraController != null)
                cameraController.InitializeSides(boardConfig);

            // Determine match type and start appropriate flow
            MatchConnection match = MatchConnection.Instance;

            if (match != null && match.isBotMatch)
            {
                StartDraftPhase(match);
            }
            else if (match != null && match.isNetworked)
            {
                StartDraftPhase(match);
            }
            else
            {
                // Testing mode: skip draft, use legacy hardcoded board
                SkipDraftAndInitialize();
            }
        }

        // ===== NEW: Draft phase startup =====

        private void StartDraftPhase(MatchConnection match)
        {
            matchPhase = MatchPhase.Drafting;

            // Create DraftManager
            GameObject draftGO = new GameObject("DraftManager");
            draftManager = draftGO.AddComponent<DraftManager>();

            draftManager.Initialize(
                boardConfig,
                match.isNetworked ? match.networkManager : null,
                match.isNetworked ? match.localPlayerID : 0,
                match.isNetworked,
                match.isBotMatch,
                cameraController,
                gridCellMarkerPrefab
            );

            draftManager.OnDraftComplete += OnDraftComplete;
            draftManager.OnDraftDisconnect += OnDraftDisconnect;

            // Create Draft UI
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

        private void OnDraftComplete(DraftResult result)
        {
            pendingDraftResult = result;
            matchPhase = MatchPhase.PostDraft;

            // Clean up draft manager
            if (draftManager != null)
            {
                Destroy(draftManager.gameObject);
                draftManager = null;
            }

            // Proceed to post-draft initialization
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

            // Show disconnect and return to lobby
            ShowDisconnect();
        }

        // ===== NEW: Post-draft initialization =====

        private void InitializeFromDraftResult(DraftResult result)
        {
            // Build the board from initial placements + draft results
            InitializeNodesFromDraft(result);
            InitializePlayers();
            InitializeVillagers();
            InitializeInputSystems();

            // Create tick provider
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
                    botPlayer = new NodeWar.Input.BotPlayer(state, inputBuffer, 1, boardConfig.defaultEdgeWeight);
                    debugPlayerSwitch.LockToPlayer(0);

                    TickRunner runner = GetComponent<TickRunner>();
                    if (runner != null)
                        runner.SetBot(botPlayer);
                }
            }

            // Spawn views
            SpawnNodeViews();
            SpawnVillagerViews();
            trackedVillagerCount = state.villagers.Length;

            // Wire camera side
            debugPlayerSwitch.OnPlayerSwitched += OnPlayerSideChanged;

            // Initialize UI
            InitializeUI();

            // Start countdown
            StartCountdown();
        }

        // ===== NEW: Build nodes from draft =====

        private void InitializeNodesFromDraft(DraftResult result)
        {
            int GRID_COLS = boardConfig.gridCols;
            int GRID_ROWS = boardConfig.gridRows;
            state.nodes = new NodeData[GRID_COLS * GRID_ROWS];

            // Step 1: Create all nodes as None with grid topology
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
                        edges[i] = new Edge { toNode = neighborIDs[i], travelWeight = boardConfig.defaultEdgeWeight };

                    state.nodes[nodeID] = new NodeData
                    {
                        nodeID = nodeID,
                        worldPosition = new Vector3(x * boardConfig.nodeScale, 0f, z * boardConfig.nodeScale),
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

            // Step 2: Apply initial placements from BoardConfig
            if (boardConfig.initialPlacements != null)
            {
                for (int i = 0; i < boardConfig.initialPlacements.Length; i++)
                {
                    var ip = boardConfig.initialPlacements[i];
                    int nodeID = ip.gridZ * GRID_COLS + ip.gridX;
                    state.nodes[nodeID].districtType = ip.districtType;
                    state.nodes[nodeID].baseDistrictType = ip.districtType;
                    state.nodes[nodeID].ownerID = ip.ownerID;
                    state.nodes[nodeID].claimBar = ip.claimBar;
                }
            }

            // Step 3: Apply draft placements
            if (result.placements != null)
            {
                for (int i = 0; i < result.placements.Length; i++)
                {
                    var dp = result.placements[i];
                    int nodeID = dp.gridZ * GRID_COLS + dp.gridX;
                    state.nodes[nodeID].districtType = dp.districtType;
                    state.nodes[nodeID].baseDistrictType = dp.districtType;
                    state.nodes[nodeID].ownerID = -1; // drafted nodes start unowned
                    state.nodes[nodeID].slotType = NodeSlotType.Fixed;

                    // Village bonus
                    if (dp.districtType == DistrictType.Village)
                        state.nodes[nodeID].bonusVillagersOnClaim = balance.bonusVillagersOnVillageClaim;
                }
            }
        }

        // ===== NEW: Skip draft for testing mode (preserves existing behavior) =====

        private void SkipDraftAndInitialize()
        {
            matchPhase = MatchPhase.Playing;

            // Use legacy initialization (hardcoded board)
            InitializeNodes(); // existing method, unchanged
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
            }

            SpawnNodeViews();
            SpawnVillagerViews();
            trackedVillagerCount = state.villagers.Length;

            debugPlayerSwitch.OnPlayerSwitched += OnPlayerSideChanged;
            OnPlayerSideChanged(debugPlayerSwitch.GetCurrentPlayerID());

            InitializeUI();
            PlayAllNodeStartup();
        }

        private void StartCountdown()
        {
            matchPhase = MatchPhase.Countdown;

            if (cameraController != null)
                cameraController.SetDraftMode(false);

            MatchConnection match = MatchConnection.Instance;
            int pid = (match != null && match.isNetworked) ? match.localPlayerID : 0;
            OnPlayerSideChanged(pid);

            AnimatePlaceholdersAway();

            // Delay the startup and countdown to let removal breathe
            float removalDuration = 1.5f; // approximate time for all placeholders to fly away
            StartCoroutine(DelayedStartup(removalDuration));
        }

        private System.Collections.IEnumerator DelayedStartup(float delay)
        {
            yield return new WaitForSeconds(delay);

            PlayAllNodeStartup();

            // Additional pause to let nodes spring up before countdown text appears
            yield return new WaitForSeconds(1.0f);

            if (countdownUIPrefab != null)
            {
                GameObject countdownGO = Instantiate(countdownUIPrefab);
                CountdownUI countdown = countdownGO.GetComponent<CountdownUI>();
                if (countdown != null)
                {
                    countdown.OnCountdownComplete += OnCountdownComplete;
                    countdown.StartCountdown();
                }
                else
                {
                    OnCountdownComplete();
                }
            }
            else
            {
                OnCountdownComplete();
            }
        }

        private void AnimatePlaceholdersAway()
        {
            DraftUI draftUI = FindAnyObjectByType<DraftUI>();
            List<GameObject> placeholders = null;

            if (draftUI != null)
                placeholders = draftUI.GetPersistentPlacements();

            if (placeholders == null || placeholders.Count == 0) return;

            for (int i = 0; i < placeholders.Count; i++)
            {
                if (placeholders[i] == null) continue;

                GameObject obj = placeholders[i];
                float delay = i * 0.12f; // was 0.05f — slower stagger between each

                obj.transform.DOMove(obj.transform.position + Vector3.up * 10f, 0.8f) // was 0.5f
                    .SetDelay(delay)
                    .SetEase(Ease.InBack)
                    .OnComplete(() => Destroy(obj));
            }
        }


        private void OnCountdownComplete()
        {
            matchPhase = MatchPhase.Playing;

            TickRunner tickRunner = GetComponent<TickRunner>();
            if (tickRunner != null) tickRunner.Unpause();

            LockstepRunner lockstep = GetComponent<LockstepRunner>();
            if (lockstep != null) lockstep.Unpause();
        }

        private void Update()
        {
            if (matchPhase != MatchPhase.Playing) return;

            // Spawn views for bonus villagers
            if (state.villagers.Length > trackedVillagerCount)
            {
                SpawnNewVillagerViews(trackedVillagerCount, state.villagers.Length);
                trackedVillagerCount = state.villagers.Length;
            }

            // Detect game over
            if (state.gameOver && !gameOverHandled)
            {
                gameOverHandled = true;
                ShowGameOver();
            }
        }

        // ===== SYSTEM INITIALIZATION =====

        private void InitializeInputSystems()
        {
            selectionSystem = gameObject.AddComponent<SelectionSystem>();
            selectionSystem.Initialize(state, 0);

            commandSystem = gameObject.AddComponent<CommandSystem>();
            commandSystem.Initialize(state, inputBuffer, selectionSystem, 0);

            debugPlayerSwitch = gameObject.AddComponent<DebugPlayerSwitch>();
            debugPlayerSwitch.Initialize(selectionSystem, commandSystem);

            CreateSelectionLasso();
        }


        private void OnPlayerSideChanged(int playerID)
        {
            if (cameraController != null)
                cameraController.SetPlayerSide(playerID);

            Vector3 spriteRot = cameraController != null
                ? cameraController.GetSpriteRotation()
                : new Vector3(50f, playerID == 0 ? 180f : 0f, 0f);

            if (nodePresentations != null)
            {
                for (int i = 0; i < nodePresentations.Length; i++)
                {
                    if (nodePresentations[i] != null)
                        nodePresentations[i].SetBaseRotation(spriteRot);
                }
            }
        }

        /// <summary>
        /// Local play: create TickRunner. Sets tickProvider.
        /// </summary>
        private void StartLocalPlay()
        {
            TickRunner tickRunner = gameObject.AddComponent<TickRunner>();
            tickRunner.Initialize(state, inputBuffer);
            tickProvider = tickRunner;

            // Wire bot if this is a bot match (botPlayer created after this call in Awake)
            // Defer wire-up to after bot creation — handled below
        }

        /// <summary>
        /// Network play: create LockstepRunner from MatchConnection data. Sets tickProvider.
        /// </summary>
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
            SelectionLasso lasso = lassoGO.AddComponent<SelectionLasso>();
            lasso.Initialize(selectionSystem);
        }

        // ===== GAME OVER / DISCONNECT =====

        private void ShowGameOver()
        {
            PlayAllNodeBreakdown();

            if (gameOverPanel == null)
            {
                Debug.LogWarning("[GameManager] GameOverPanel not found. Cannot show game over UI.");
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
                Debug.LogWarning("[GameManager] GameOverPanel not found. Cannot show disconnect UI.");
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
            // Shutdown MatchConnection (closes socket, destroys persistent object)
            if (MatchConnection.Instance != null)
            {
                MatchConnection.Instance.Shutdown();
            }

            SceneManager.LoadScene("Lobby");
        }

        // ===== UI INITIALIZATION =====

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
                hudManager.Initialize(state, debugPlayerSwitch);

            nodePanelManager = uiGO.GetComponentInChildren<NodePanelManager>();
            if (nodePanelManager != null)
                nodePanelManager.Initialize(state, inputBuffer, selectionSystem, debugPlayerSwitch, tickProvider);

            gameOverPanel = uiGO.GetComponentInChildren<GameOverPanel>(true); // true = include inactive
            if (gameOverPanel != null)
            {
                gameOverPanel.OnReturnToLobby += ReturnToLobby;
            }
            else
            {
                Debug.LogWarning("[GameManager] GameOverPanel not found in UIManager prefab.");
            }
        }

        // ===== NODE INITIALIZATION (4x7 GRID) =====
        private void InitializeNodes()
        {
            int GRID_COLS = boardConfig.gridCols;
            int GRID_ROWS = boardConfig.gridRows;
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
                        edges[i] = new Edge { toNode = neighborIDs[i], travelWeight = boardConfig.defaultEdgeWeight };

                    int bonus = layout[z, x] == DistrictType.Village ? balance.bonusVillagersOnVillageClaim : 0;
                    int ownerID = -1;
                    int claimBar = 0;
                    if (z == 6 && x == 1) { ownerID = 0; claimBar = 10000; }
                    if (z == 0 && x == 2) { ownerID = 1; claimBar = -10000; }

                    state.nodes[nodeID] = new NodeData
                    {
                        nodeID = nodeID,
                        worldPosition = new Vector3(x * boardConfig.nodeScale, 0f, z * boardConfig.nodeScale),
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

            int[] defaultSuits = new int[] { (int)SuitType.Warrior };
            int[] defaultNodes = new int[0];

            state.players[0] = new PlayerData
            {
                playerID = 0,
                coreNodeID = 25,
                food = boardConfig.startingFood,
                materials = boardConfig.startingMaterials,
                metal = boardConfig.startingMetal,
                breachCount = 0,
                draftedSuits = defaultSuits,
                draftedNodes = defaultNodes
            };
            state.players[1] = new PlayerData
            {
                playerID = 1,
                coreNodeID = 2,
                food = boardConfig.startingFood,
                materials = boardConfig.startingMaterials,
                metal = boardConfig.startingMetal,
                breachCount = 0,
                draftedSuits = defaultSuits,
                draftedNodes = defaultNodes
            };
        }

        private void InitializeVillagers()
        {
            int totalVillagers = boardConfig.startingVillagersPerPlayer * 2;
            state.villagers = new VillagerData[totalVillagers];

            for (int i = 0; i < totalVillagers; i++)
            {
                int owner = (i < boardConfig.startingVillagersPerPlayer) ? 0 : 1;
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
                    hp = balance.baseHP,
                    maxHP = balance.baseHP,
                    attackDamage = balance.baseAttackDamage,
                    moveSpeedTicks = balance.baseMoveSpeedTicks,
                    respawnTicksRemaining = 0,
                    attackCooldownRemaining = balance.baseAttackCooldownMax,
                    attackCooldownMax = balance.baseAttackCooldownMax,
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

            for (int i = 0; i < state.nodes.Length; i++)
            {
                GameObject prefab = GetPrefabForDistrict(state.nodes[i].districtType);
                GameObject nodeGO = Instantiate(prefab, nodeParent);
                nodeGO.name = "NodeView_" + i + "_" + state.nodes[i].districtType.ToString();
                nodeGO.transform.position = state.nodes[i].worldPosition;

                NodeWar.View.NodeView view = nodeGO.GetComponent<NodeWar.View.NodeView>();
                if (view != null)
                    view.Initialize(state, i);

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
                    claimBar.Initialize(state, i);
            }

            // Pre-hide all nodes. PlayAllNodeStartup reveals them after placeholders leave.
            for (int i = 0; i < nodePresentations.Length; i++)
            {
                if (nodePresentations[i] != null)
                    nodePresentations[i].SetHidden();
            }
        }

        private void SpawnVillagerViews()
        {
            villagerParent = new GameObject("VillagerViews").transform;
            villagerTransforms = new Transform[state.villagers.Length];

            for (int i = 0; i < state.villagers.Length; i++)
            {
                SpawnSingleVillagerView(i);
            }

            selectionSystem.SetVillagerTransforms(villagerTransforms);
        }

        private void SpawnNewVillagerViews(int fromIndex, int toIndex)
        {
            Transform[] newArray = new Transform[toIndex];
            for (int i = 0; i < villagerTransforms.Length; i++)
                newArray[i] = villagerTransforms[i];
            villagerTransforms = newArray;

            for (int i = fromIndex; i < toIndex; i++)
            {
                SpawnSingleVillagerView(i);
            }

            selectionSystem.SetVillagerTransforms(villagerTransforms);
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
            }

            VillagerHealthRing healthRing = villagerGO.GetComponentInChildren<VillagerHealthRing>();
            if (healthRing != null)
                healthRing.Initialize(state, index);
        }

        // ===== PRESENTATION TRIGGERS =====

        /// <summary>
        /// Animate all nodes appearing. Call at match start or after draft reveal.
        /// Staggers by grid distance from center for a wave effect.
        /// </summary>
        public void PlayAllNodeStartup()
        {
            if (nodePresentations == null) return;

            float centerX = (boardConfig.gridCols - 1) * 0.5f;
            float centerZ = (boardConfig.gridRows - 1) * 0.5f;

            for (int i = 0; i < nodePresentations.Length; i++)
            {
                if (nodePresentations[i] == null) continue;

                NodeData node = state.nodes[i];
                float dist = Mathf.Abs(node.gridX - centerX) + Mathf.Abs(node.gridZ - centerZ);
                float delay = dist * 0.14f; // was 0.08f — more time between each ring

                nodePresentations[i].SetHidden();
                nodePresentations[i].PlayStartup(delay);
            }
        }

        /// <summary>
        /// Animate all nodes collapsing. Call on game over.
        /// Staggers outward from the breached core for narrative effect.
        /// </summary>
        public void PlayAllNodeBreakdown()
        {
            if (nodePresentations == null) return;

            // Wave originates from the losing player's core
            int loserID = state.winnerID == 0 ? 1 : 0;
            int originNode = state.players[loserID].coreNodeID;
            Vector3 origin = state.nodes[originNode].worldPosition;

            for (int i = 0; i < nodePresentations.Length; i++)
            {
                if (nodePresentations[i] == null) continue;

                float dist = Vector3.Distance(state.nodes[i].worldPosition, origin);
                float delay = dist * 0.04f; // 40ms per unit distance from core

                nodePresentations[i].PlayBreakdown(delay);
            }
        }
    }
}