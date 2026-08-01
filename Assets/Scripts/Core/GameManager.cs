using UnityEngine;
using NodeWar.Simulation;
using NodeWar.Input;
using NodeWar.Debugging;

namespace NodeWar.Core
{
    public class GameManager : MonoBehaviour
    {
        [Header("Prefabs")]
        public GameObject nodePrefab;
        public GameObject villagerPrefab;

        [Header("Runtime")]
        public SimulationState state;

        private Transform nodeParent;
        private Transform villagerParent;

        private InputBuffer inputBuffer;
        private TickRunner tickRunner;
        private SelectionSystem selectionSystem;
        private CommandSystem commandSystem;

        // Track villager count for dynamic view spawning
        private int trackedVillagerCount;

        private void Awake()
        {
            state = new SimulationState();
            inputBuffer = new InputBuffer();

            InitializeNodes();
            InitializePlayers();
            InitializeVillagers();

            InitializeSystems();

            SpawnNodeViews();
            SpawnVillagerViews();

            trackedVillagerCount = state.villagers.Length;
        }

        private void Update()
        {
            // Detect bonus villagers spawned by simulation and create their views
            if (state.villagers.Length > trackedVillagerCount)
            {
                SpawnNewVillagerViews(trackedVillagerCount, state.villagers.Length);
                trackedVillagerCount = state.villagers.Length;
            }
        }

        private void InitializeSystems()
        {
            tickRunner = gameObject.AddComponent<TickRunner>();
            tickRunner.Initialize(state, inputBuffer);

            selectionSystem = gameObject.AddComponent<SelectionSystem>();
            selectionSystem.Initialize(state, 0);

            commandSystem = gameObject.AddComponent<CommandSystem>();
            commandSystem.Initialize(state, inputBuffer, selectionSystem, 0);

            // Phase 6.1: Debug player switch
            NodeWar.Debugging.DebugPlayerSwitch debugSwitch = gameObject.AddComponent<NodeWar.Debugging.DebugPlayerSwitch>();
            debugSwitch.Initialize(selectionSystem, commandSystem);

            // Selection lasso
            CreateSelectionLasso();
        }

        private void CreateSelectionLasso()
        {
            GameObject lassoGO = new GameObject("SelectionLasso");
            NodeWar.UI.SelectionLasso lasso = lassoGO.AddComponent<NodeWar.UI.SelectionLasso>();
            lasso.Initialize(selectionSystem);
        }

        // ===== NODE INITIALIZATION =====

        private void InitializeNodes()
        {
            state.nodes = new NodeData[20];

            Vector3[] positions = new Vector3[20];
            positions[0] = new Vector3(-8f, 0f, 0f);
            positions[1] = new Vector3(-5.5f, 0f, 2.5f);
            positions[2] = new Vector3(-5.5f, 0f, -2.5f);
            positions[3] = new Vector3(-3f, 0f, 4f);
            positions[4] = new Vector3(-3f, 0f, -4f);
            positions[5] = new Vector3(-1f, 0f, 2f);
            positions[6] = new Vector3(-1f, 0f, -2f);
            positions[7] = new Vector3(0f, 0f, 4.5f);
            positions[8] = new Vector3(0f, 0f, 0f);
            positions[9] = new Vector3(0f, 0f, -4.5f);
            positions[10] = new Vector3(1f, 0f, 2f);
            positions[11] = new Vector3(1f, 0f, -2f);
            positions[12] = new Vector3(3f, 0f, 4f);
            positions[13] = new Vector3(3f, 0f, -4f);
            positions[14] = new Vector3(5.5f, 0f, 2.5f);
            positions[15] = new Vector3(5.5f, 0f, -2.5f);
            positions[16] = new Vector3(8f, 0f, 0f);
            positions[17] = new Vector3(-3f, 0f, 0f);
            positions[18] = new Vector3(3f, 0f, 0f);
            positions[19] = new Vector3(0f, 0f, 2f);

            DistrictType[] types = new DistrictType[20];
            types[0] = DistrictType.Core;
            types[1] = DistrictType.Farm;
            types[2] = DistrictType.Mine;
            types[3] = DistrictType.Village;
            types[4] = DistrictType.Farm;
            types[5] = DistrictType.Farm;
            types[6] = DistrictType.Mine;
            types[7] = DistrictType.Barracks;
            types[8] = DistrictType.Village;
            types[9] = DistrictType.Mine;
            types[10] = DistrictType.Farm;
            types[11] = DistrictType.Barracks;
            types[12] = DistrictType.Farm;
            types[13] = DistrictType.Village;
            types[14] = DistrictType.Mine;
            types[15] = DistrictType.Farm;
            types[16] = DistrictType.Core;
            types[17] = DistrictType.Forge;
            types[18] = DistrictType.Forge;
            types[19] = DistrictType.None;

            int[] bonus = new int[20];
            bonus[0] = 0; bonus[16] = 0;
            bonus[3] = 2; bonus[8] = 3; bonus[13] = 2;
            bonus[17] = 1;bonus[18] = 1; 
            bonus[19] = 0;
            for (int i = 0; i < 20; i++)
            {
                if (bonus[i] == 0 && types[i] != DistrictType.Core && types[i] != DistrictType.None)
                    bonus[i] = 1;
            }

            int[][] connections = new int[20][];
            connections[0] = new int[] { 1, 2 };
            connections[1] = new int[] { 0, 2, 3, 17 };
            connections[2] = new int[] { 0, 1, 4, 17 };
            connections[3] = new int[] { 1, 5, 7 };
            connections[4] = new int[] { 2, 6, 9 };
            connections[5] = new int[] { 3, 7, 8, 17, 19 };
            connections[6] = new int[] { 4, 8, 9, 17, 19 };
            connections[7] = new int[] { 3, 5, 10, 12, 19 };
            connections[8] = new int[] { 5, 6, 10, 11, 18, 17 };
            connections[9] = new int[] { 4, 6, 11, 13 };
            connections[10] = new int[] { 7, 8, 12, 18, 19 };
            connections[11] = new int[] { 8, 9, 13, 18, 19 };
            connections[12] = new int[] { 7, 10, 14, 18 };
            connections[13] = new int[] { 9, 11, 15, 18 };
            connections[14] = new int[] { 12, 15, 16, 18 };
            connections[15] = new int[] { 13, 14, 16, 18 };
            connections[16] = new int[] { 14, 15 };
            connections[17] = new int[] { 1, 2, 5, 6, 8 };
            connections[18] = new int[] { 8, 10, 11, 12, 13, 14, 15 };
            connections[19] = new int[] { 5, 6, 7, 10, 11 };

            for (int i = 0; i < 20; i++)
            {
                state.nodes[i] = new NodeData
                {
                    nodeID = i,
                    worldPosition = positions[i],
                    connectedNodes = connections[i],
                    districtType = types[i],
                    claimBar = 0,
                    ownerID = -1,
                    bonusVillagersOnClaim = bonus[i]
                };
            }

            state.nodes[0].claimBar = 10000;
            state.nodes[0].ownerID = 0;
            state.nodes[16].claimBar = -10000;
            state.nodes[16].ownerID = 1;
        }

        private void InitializePlayers()
        {
            state.players = new PlayerData[2];

            state.players[0] = new PlayerData
            {
                playerID = 0,
                coreNodeID = 0,
                food = 5,
                materials = 3,
                metal = 0,
                breachCount = 0
            };

            state.players[1] = new PlayerData
            {
                playerID = 1,
                coreNodeID = 16,
                food = 5,
                materials = 3,
                metal = 0,
                breachCount = 0
            };
        }

        private void InitializeVillagers()
        {
            int totalVillagers = 20;
            state.villagers = new VillagerData[totalVillagers];

            for (int i = 0; i < totalVillagers; i++)
            {
                int owner = (i < 10) ? 0 : 1;
                int coreNode = (owner == 0) ? 0 : 16;

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
                    // Phase 5 combat fields
                    attackCooldownRemaining = 20,
                    attackCooldownMax = 20,
                    combatTargetID = -1,
                    fightPriority = 0,
                    // Phase 6 breach fields
                    isConsumed = false,
                    // Phase 7
                    productionTicksRemaining = 0,
                    productionTicksMax = 0
                };
            }
        }

        private void SpawnNodeViews()
        {
            nodeParent = new GameObject("NodeViews").transform;

            for (int i = 0; i < state.nodes.Length; i++)
            {
                GameObject nodeGO = Instantiate(nodePrefab, nodeParent);
                nodeGO.name = "NodeView_" + i + "_" + state.nodes[i].districtType.ToString();

                NodeWar.View.NodeView view = nodeGO.GetComponent<NodeWar.View.NodeView>();
                if (view != null)
                {
                    view.Initialize(state, i);
                }

                // Initialize claim bar (lives as child of the prefab)
                NodeWar.UI.NodeClaimBar claimBar = nodeGO.GetComponentInChildren<NodeWar.UI.NodeClaimBar>();
                if (claimBar != null)
                {
                    claimBar.Initialize(state, i);
                }
            }
        }

        private void SpawnVillagerViews()
        {
            villagerParent = new GameObject("VillagerViews").transform;

            for (int i = 0; i < state.villagers.Length; i++)
            {
                SpawnSingleVillagerView(i);
            }
        }

        private void SpawnNewVillagerViews(int fromIndex, int toIndex)
        {
            for (int i = fromIndex; i < toIndex; i++)
            {
                SpawnSingleVillagerView(i);
            }
        }

        private void SpawnSingleVillagerView(int index)
        {
            GameObject villagerGO = Instantiate(villagerPrefab, villagerParent);
            villagerGO.name = "V_" + index + "_P" + state.villagers[index].ownerID + "_" + state.villagers[index].suit + "_" + state.villagers[index].state;

            NodeWar.View.VillagerView view = villagerGO.GetComponent<NodeWar.View.VillagerView>();
            if (view != null)
            {
                view.Initialize(state, index);
                view.SetTickRunner(tickRunner);
                view.SetSelectionSystem(selectionSystem);
            }

            // Initialize health ring (lives as child of the prefab)
            NodeWar.UI.VillagerHealthRing healthRing = villagerGO.GetComponentInChildren<NodeWar.UI.VillagerHealthRing>();
            if (healthRing != null)
            {
                healthRing.Initialize(state, index);
            }
        }

        private void OnGUI()
        {
            if (state == null) return;

            // === RESOURCE DISPLAY (always visible) ===
            int controlledPlayer = 0;
            NodeWar.Debugging.DebugPlayerSwitch debugSwitch = GetComponent<NodeWar.Debugging.DebugPlayerSwitch>();
            if (debugSwitch != null)
                controlledPlayer = debugSwitch.GetCurrentPlayerID();

            PlayerData player = state.players[controlledPlayer];

            GUIStyle resourceStyle = new GUIStyle(GUI.skin.label);
            resourceStyle.fontSize = 18;
            resourceStyle.normal.textColor = Color.white;

            GUIStyle headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize = 14;
            headerStyle.normal.textColor = (controlledPlayer == 0)
                ? new Color(0.4f, 0.6f, 1f)
                : new Color(1f, 0.4f, 0.4f);

            float x = Screen.width - 200;
            float y = 10;

            GUI.Label(new Rect(x, y, 190, 25), "Player " + controlledPlayer + " Resources", headerStyle);
            y += 22;
            GUI.Label(new Rect(x, y, 190, 25), "Food: " + player.food, resourceStyle);
            y += 22;
            GUI.Label(new Rect(x, y, 190, 25), "Materials: " + player.materials, resourceStyle);
            y += 22;
            GUI.Label(new Rect(x, y, 190, 25), "Metal: " + player.metal, resourceStyle);
            y += 30;

            // Breach display
            GUIStyle breachStyle = new GUIStyle(GUI.skin.label);
            breachStyle.fontSize = 16;
            breachStyle.normal.textColor = new Color(0.4f, 0.6f, 1f);
            GUI.Label(new Rect(x, y, 190, 25), "P0 Breaches: " + state.players[0].breachCount + " / 3", breachStyle);
            y += 20;
            breachStyle.normal.textColor = new Color(1f, 0.4f, 0.4f);
            GUI.Label(new Rect(x, y, 190, 25), "P1 Breaches: " + state.players[1].breachCount + " / 3", breachStyle);
            y += 20;

            // Count per-player villagers (non-consumed)
            GUIStyle countStyle = new GUIStyle(GUI.skin.label);
            countStyle.fontSize = 12;
            countStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            int p0Count = 0;
            int p1Count = 0;
            for (int i = 0; i < state.villagers.Length; i++)
            {
                if (state.villagers[i].isConsumed) continue;
                if (state.villagers[i].ownerID == 0) p0Count++;
                else p1Count++;
            }
            GUI.Label(new Rect(x, y, 190, 20), "P0: " + p0Count + "/25  P1: " + p1Count + "/25", countStyle);

            // === GAME OVER OVERLAY ===
            if (!state.gameOver) return;

            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 48;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.alignment = TextAnchor.MiddleCenter;

            if (state.winnerID == 0)
                titleStyle.normal.textColor = new Color(0.3f, 0.5f, 1f);
            else
                titleStyle.normal.textColor = new Color(1f, 0.3f, 0.3f);

            string winnerText = "PLAYER " + state.winnerID + " WINS!";
            GUI.Label(new Rect(0, Screen.height / 2 - 60, Screen.width, 60), winnerText, titleStyle);

            GUIStyle infoStyle = new GUIStyle(GUI.skin.label);
            infoStyle.fontSize = 24;
            infoStyle.alignment = TextAnchor.MiddleCenter;
            infoStyle.normal.textColor = Color.white;

            string infoText = "Breaches — P0: " + state.players[0].breachCount + "  P1: " + state.players[1].breachCount;
            GUI.Label(new Rect(0, Screen.height / 2 + 10, Screen.width, 40), infoText, infoStyle);

            string tickText = "Game ended at tick " + state.tickCount;
            GUI.Label(new Rect(0, Screen.height / 2 + 50, Screen.width, 30), tickText, infoStyle);
        }
    }
}