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
            DebugPlayerSwitch debugSwitch = gameObject.AddComponent<DebugPlayerSwitch>();
            debugSwitch.Initialize(selectionSystem, commandSystem);
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
            types[17] = DistrictType.None;
            types[18] = DistrictType.None;
            types[19] = DistrictType.None;

            int[] bonus = new int[20];
            bonus[0] = 0; bonus[16] = 0;
            bonus[3] = 2; bonus[8] = 3; bonus[13] = 2;
            bonus[17] = 0; bonus[18] = 0; bonus[19] = 0;
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
                    isConsumed = false
                };
            }
        }

        private void SpawnNodeViews()
        {
            nodeParent = new GameObject("NodeViews").transform;

            for (int i = 0; i < state.nodes.Length; i++)
            {
                GameObject nodeGO = Instantiate(nodePrefab, nodeParent);
                nodeGO.name = "NodeView_" + i;

                NodeWar.View.NodeView view = nodeGO.GetComponent<NodeWar.View.NodeView>();
                if (view != null)
                {
                    view.Initialize(state, i);
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
            villagerGO.name = "VillagerView_" + index;

            NodeWar.View.VillagerView view = villagerGO.GetComponent<NodeWar.View.VillagerView>();
            if (view != null)
            {
                view.Initialize(state, index);
                view.SetTickRunner(tickRunner);
                view.SetSelectionSystem(selectionSystem);
            }
        }

        private void OnGUI()
        {
            if (state == null) return;
            if (!state.gameOver) return;

            // Dark overlay
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Winner text
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

            // Breach info
            GUIStyle infoStyle = new GUIStyle(GUI.skin.label);
            infoStyle.fontSize = 24;
            infoStyle.alignment = TextAnchor.MiddleCenter;
            infoStyle.normal.textColor = Color.white;

            string infoText = "Breaches — P0: " + state.players[0].breachCount + "  P1: " + state.players[1].breachCount;
            GUI.Label(new Rect(0, Screen.height / 2 + 10, Screen.width, 40), infoText, infoStyle);

            // Tick count
            string tickText = "Game ended at tick " + state.tickCount;
            GUI.Label(new Rect(0, Screen.height / 2 + 50, Screen.width, 30), tickText, infoStyle);
        }
    }
}