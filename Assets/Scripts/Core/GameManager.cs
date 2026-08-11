using UnityEngine;
using UnityEngine.SceneManagement;
using NodeWar.Simulation;
using NodeWar.Input;
using NodeWar.Debugging;
using NodeWar.UI;
using NodeWar.Network;
using System.Collections.Generic;

namespace NodeWar.Core
{
    public class GameManager : MonoBehaviour
    {
        [Header("Node Prefabs (assign per district type)")]
        [SerializeField] private GameObject nodePrefabDefault;
        [SerializeField] private GameObject nodePrefabCore;
        [SerializeField] private GameObject nodePrefabFarm;
        [SerializeField] private GameObject nodePrefabMine;
        [SerializeField] private GameObject nodePrefabVillage;
        [SerializeField] private GameObject nodePrefabBarracks;
        [SerializeField] private GameObject nodePrefabForge;

        [Header("Villager Prefab")]
        [SerializeField] private GameObject villagerPrefab;

        [Header("Board Settings")]
        [SerializeField] private float nodeScale = 5f;
        [SerializeField] private int defaultEdgeWeight = 3;

        [Header("Starting Values")]
        [SerializeField] private int startingVillagersPerPlayer = 3;
        [SerializeField] private int startingFood = 5;
        [SerializeField] private int startingMaterials = 3;
        [SerializeField] private int startingMetal = 0;

        [Header("Pathfinding Preference (integer percentages)")]
        [SerializeField] private int ownedMultiplier = 50;
        [SerializeField] private int partiallyOwnedMultiplier = 75;
        [SerializeField] private int unownedMultiplier = 100;
        [SerializeField] private int enemyPartiallyOwnedMultiplier = 150;
        [SerializeField] private int enemyOwnedMultiplier = 200;

        [Header("UI")]
        [SerializeField] private GameObject uiManagerPrefab;
        private NodePanelManager nodePanelManager;
        private GameOverPanel gameOverPanel;

        [Header("Runtime")]
        public SimulationState state;

        private DebugPlayerSwitch debugPlayerSwitch;
        private HUDManager hudManager;

        private const int GRID_COLS = 4;
        private const int GRID_ROWS = 7;

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

        // Network references
        private LockstepRunner lockstepRunner;
        private NodeWar.Input.BotPlayer botPlayer;

        // Game over tracking
        private bool gameOverHandled = false;

        private void Awake()
        {
            Application.runInBackground = true;

            state = new SimulationState();
            inputBuffer = new InputBuffer();

            Pathfinding.OwnedMultiplier = ownedMultiplier;
            Pathfinding.PartiallyOwnedMultiplier = partiallyOwnedMultiplier;
            Pathfinding.UnownedMultiplier = unownedMultiplier;
            Pathfinding.EnemyPartiallyOwnedMultiplier = enemyPartiallyOwnedMultiplier;
            Pathfinding.EnemyOwnedMultiplier = enemyOwnedMultiplier;

            // 1. Initialize simulation state
            InitializeNodes();
            InitializePlayers();
            InitializeVillagers();

            // 2. Initialize input systems
            InitializeInputSystems();

            // 3. Create tick provider (BEFORE views so they receive a valid reference)
            MatchConnection match = MatchConnection.Instance;

            if (match != null && match.isNetworked)
            {
                StartNetworkPlay(match);
            }
            else
            {
                StartLocalPlay();

                // Create bot if this is a bot match
                if (match != null && match.isBotMatch)
                {
                    botPlayer = new NodeWar.Input.BotPlayer(state, inputBuffer, 1);
                    debugPlayerSwitch.LockToPlayer(0);

                    TickRunner runner = GetComponent<TickRunner>();
                    if (runner != null)
                        runner.SetBot(botPlayer);
                }
            }

            // 4. Spawn views (tickProvider is now valid)
            SpawnNodeViews();
            SpawnVillagerViews();

            trackedVillagerCount = state.villagers.Length;

            // 5. Initialize UI (tickProvider is valid, views exist)
            InitializeUI();
        }

        private void Update()
        {
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
            int nodeCount = GRID_COLS * GRID_ROWS;
            state.nodes = new NodeData[nodeCount];

            DistrictType[,] layout = new DistrictType[GRID_ROWS, GRID_COLS];
            layout[0, 0] = DistrictType.None; layout[0, 1] = DistrictType.None; layout[0, 2] = DistrictType.Core; layout[0, 3] = DistrictType.None;
            layout[1, 0] = DistrictType.None; layout[1, 1] = DistrictType.Mine; layout[1, 2] = DistrictType.Farm; layout[1, 3] = DistrictType.None;
            layout[2, 0] = DistrictType.Mine; layout[2, 1] = DistrictType.Barracks; layout[2, 2] = DistrictType.Village; layout[2, 3] = DistrictType.Farm;
            layout[3, 0] = DistrictType.Forge; layout[3, 1] = DistrictType.None; layout[3, 2] = DistrictType.None; layout[3, 3] = DistrictType.Forge;
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
                    {
                        edges[i] = new Edge { toNode = neighborIDs[i], travelWeight = defaultEdgeWeight };
                    }

                    int bonus = 0;
                    if (layout[z, x] == DistrictType.Village) bonus = 2;

                    int ownerID = -1;
                    int claimBar = 0;
                    if (z == 6 && x == 1) { ownerID = 0; claimBar = 10000; }
                    if (z == 0 && x == 2) { ownerID = 1; claimBar = -10000; }

                    state.nodes[nodeID] = new NodeData
                    {
                        nodeID = nodeID,
                        worldPosition = new Vector3(x * nodeScale, 0f, z * nodeScale),
                        gridX = x,
                        gridZ = z,
                        edges = edges,
                        districtType = layout[z, x],
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

            state.players[0] = new PlayerData
            {
                playerID = 0,
                coreNodeID = 25,
                food = startingFood,
                materials = startingMaterials,
                metal = startingMetal,
                breachCount = 0
            };

            state.players[1] = new PlayerData
            {
                playerID = 1,
                coreNodeID = 2,
                food = startingFood,
                materials = startingMaterials,
                metal = startingMetal,
                breachCount = 0
            };
        }

        private void InitializeVillagers()
        {
            int totalVillagers = startingVillagersPerPlayer * 2;
            state.villagers = new VillagerData[totalVillagers];

            for (int i = 0; i < totalVillagers; i++)
            {
                int owner = (i < startingVillagersPerPlayer) ? 0 : 1;
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
                    hp = 5,
                    maxHP = 5,
                    attackDamage = 1,
                    moveSpeedTicks = 4,
                    respawnTicksRemaining = 0,
                    attackCooldownRemaining = 20,
                    attackCooldownMax = 20,
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
                default: prefab = nodePrefabDefault; break;
            }
            if (prefab == null) prefab = nodePrefabDefault;
            return prefab;
        }

        private void SpawnNodeViews()
        {
            nodeParent = new GameObject("NodeViews").transform;
            nodeSlotManagers = new NodeWar.View.NodeSlotManager[state.nodes.Length];

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
                slotManager.Initialize(i, nodeScale);
                nodeSlotManagers[i] = slotManager;

                NodeClaimBar claimBar = nodeGO.GetComponentInChildren<NodeClaimBar>();
                if (claimBar != null)
                    claimBar.Initialize(state, i);
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
    }
}