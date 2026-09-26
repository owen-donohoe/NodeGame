namespace NodeWar.Simulation
{
    /// <summary>
    /// What one player brings to a match: the suit and district types their
    /// loadout resolved to. Resolving lobby loadouts (string IDs) into these is
    /// the caller's job, because lobby data is not simulation data.
    /// </summary>
    public struct PlayerSetup
    {
        public int[] suits; // (int)SuitType values
        public int[] nodes; // (int)DistrictType values

        /// <summary>
        /// The era this player fields for each suit and district, indexed by
        /// (int)SuitType / (int)DistrictType. Null or short means era 0.
        /// </summary>
        public int[] suitEras;
        public int[] districtEras;

        public int DistrictEra(DistrictType district)
        {
            int i = (int)district;
            return districtEras != null && i >= 0 && i < districtEras.Length ? districtEras[i] : 0;
        }
    }

    /// <summary>
    /// Builds a match's starting <see cref="SimulationState"/> without Unity.
    ///
    /// The live game, the match-log referee and headless play (bot training,
    /// balance runs) all start a match here, so there is one copy of the rules
    /// for what a board looks like at tick 0. A second copy would be free to
    /// drift, and a referee whose starting board differed from the players'
    /// would reject every honest log.
    ///
    /// The simulation still reads its balance and path costs from statics, so
    /// <see cref="Configure"/> must run before <see cref="Build"/> and before
    /// any tick, and two matches cannot run at once in one process.
    /// </summary>
    public static class MatchFactory
    {
        /// <summary>Sets every static the simulation reads for a match.</summary>
        public static void Configure(GameBalanceData balance, BoardConfigData board)
        {
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);

            Pathfinding.OwnedMultiplier = board.ownedMultiplier;
            Pathfinding.PartiallyOwnedMultiplier = board.partiallyOwnedMultiplier;
            Pathfinding.UnownedMultiplier = board.unownedMultiplier;
            Pathfinding.EnemyPartiallyOwnedMultiplier = board.enemyPartiallyOwnedMultiplier;
            Pathfinding.EnemyOwnedMultiplier = board.enemyOwnedMultiplier;
        }

        /// <summary>
        /// A drafted match at tick 0: the board's fixed placements, then the
        /// draft's, then both players and their starting villagers.
        /// </summary>
        public static SimulationState Build(GameBalanceData balance, BoardConfigData board,
            DraftPlacement[] draft, PlayerSetup[] players)
        {
            SimulationState state = new SimulationState();
            Fill(state, balance, board, draft, players);
            return state;
        }

        /// <summary>
        /// <see cref="Build"/> into a state the caller already holds. The live
        /// game creates its state before the draft, and hands the same object
        /// to everything that reads it.
        /// </summary>
        public static void Fill(SimulationState state, GameBalanceData balance, BoardConfigData board,
            DraftPlacement[] draft, PlayerSetup[] players)
        {
            state.defaultEdgeWeight = board.defaultEdgeWeight;
            BuildNodes(state, balance, board, draft, players);
            InitializePlayers(state, balance, board, players);
            InitializeVillagers(state, balance, board);
        }

        /// <summary>
        /// The grid, with every node connected to its four neighbours at the
        /// board's default weight, then the board's fixed placements, then the
        /// draft's. Draft placements start unowned, at the era their placer
        /// fields for that district; the board's own placements are era 0.
        /// </summary>
        public static void BuildNodes(SimulationState state, GameBalanceData balance,
            BoardConfigData board, DraftPlacement[] draft, PlayerSetup[] players)
        {
            int cols = board.gridCols;
            int rows = board.gridRows;
            state.nodes = new NodeData[cols * rows];

            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int nodeID = z * cols + x;
                    state.nodes[nodeID] = new NodeData
                    {
                        nodeID = nodeID,
                        gridX = x,
                        gridZ = z,
                        edges = GridEdges(x, z, cols, rows, board.defaultEdgeWeight),
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

            if (board.initialPlacements != null)
            {
                for (int i = 0; i < board.initialPlacements.Length; i++)
                {
                    BoardConfigData.InitialNodePlacement ip = board.initialPlacements[i];
                    int nodeID = ip.gridZ * cols + ip.gridX;
                    state.nodes[nodeID].districtType = ip.districtType;
                    state.nodes[nodeID].baseDistrictType = ip.districtType;
                    state.nodes[nodeID].ownerID = ip.ownerID;
                    state.nodes[nodeID].claimBar = ip.claimBar;
                }
            }

            if (draft != null)
            {
                for (int i = 0; i < draft.Length; i++)
                {
                    DraftPlacement dp = draft[i];
                    int nodeID = dp.gridZ * cols + dp.gridX;
                    state.nodes[nodeID].districtType = dp.districtType;
                    state.nodes[nodeID].baseDistrictType = dp.districtType;
                    state.nodes[nodeID].ownerID = -1;
                    state.nodes[nodeID].slotType = NodeSlotType.Fixed;

                    int era = dp.playerID >= 0 && players != null && dp.playerID < players.Length
                        ? players[dp.playerID].DistrictEra(dp.districtType) : 0;
                    state.nodes[nodeID].districtEra = era;
                    state.nodes[nodeID].bonusVillagersOnClaim =
                        balance.GetDistrictStats(dp.districtType, era).bonusVillagersOnClaim;
                }
            }
        }

        /// <summary>
        /// Left, right, down, up: the order the live game has always used, and
        /// edge order is part of the simulation (pathfinding visits edges in it).
        /// </summary>
        public static Edge[] GridEdges(int x, int z, int cols, int rows, int weight)
        {
            int count = (x > 0 ? 1 : 0) + (x < cols - 1 ? 1 : 0) + (z > 0 ? 1 : 0) + (z < rows - 1 ? 1 : 0);
            Edge[] edges = new Edge[count];
            int e = 0;
            if (x > 0) edges[e++] = new Edge { toNode = z * cols + (x - 1), travelWeight = weight };
            if (x < cols - 1) edges[e++] = new Edge { toNode = z * cols + (x + 1), travelWeight = weight };
            if (z > 0) edges[e++] = new Edge { toNode = (z - 1) * cols + x, travelWeight = weight };
            if (z < rows - 1) edges[e++] = new Edge { toNode = (z + 1) * cols + x, travelWeight = weight };
            return edges;
        }

        /// <summary>
        /// Both players on a built board. Cores are found by position, not by
        /// the board's ownerID values, and then forced to belong to their
        /// player, so a misconfigured board cannot hand a player the wrong core.
        /// </summary>
        public static void InitializePlayers(SimulationState state, GameBalanceData balance,
            BoardConfigData board, PlayerSetup[] players)
        {
            state.players = new PlayerData[2];
            for (int p = 0; p < 2; p++)
            {
                PlayerSetup setup = players != null && p < players.Length ? players[p] : default;
                state.players[p] = new PlayerData
                {
                    playerID = p,
                    coreNodeID = FindCoreNodeID(state, p),
                    food = board.startingFood,
                    materials = board.startingMaterials,
                    metal = board.startingMetal,
                    breachCount = 0,
                    draftedSuits = setup.suits ?? new int[0],
                    draftedNodes = setup.nodes ?? new int[0],
                    suitEras = setup.suitEras,
                    districtEras = setup.districtEras
                };
            }

            int p0Core = state.players[0].coreNodeID;
            int p1Core = state.players[1].coreNodeID;
            state.nodes[p0Core].ownerID = 0;
            state.nodes[p0Core].claimBar = balance.claimThreshold;
            state.nodes[p1Core].ownerID = 1;
            state.nodes[p1Core].claimBar = -balance.claimThreshold;
        }

        /// <summary>
        /// P0 owns the highest-Z core and P1 the lowest; the first core found
        /// wins a tie on Z. The fallbacks are the default 4x7 board's cores.
        /// </summary>
        public static int FindCoreNodeID(SimulationState state, int playerID)
        {
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
            return lowestZNode >= 0 ? lowestZNode : 2;
        }

        /// <summary>
        /// Each player's starting villagers, idle on their core: P0's first,
        /// then P1's, so villager IDs are the same on every machine.
        /// </summary>
        public static void InitializeVillagers(SimulationState state, GameBalanceData balance,
            BoardConfigData board)
        {
            int perPlayer = board.startingVillagersPerPlayer;
            int total = perPlayer * 2;
            state.villagers = new VillagerData[total];

            for (int i = 0; i < total; i++)
            {
                int owner = i < perPlayer ? 0 : 1;
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
    }
}
