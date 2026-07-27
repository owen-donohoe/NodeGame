namespace NodeWar.Simulation
{
    public static class GameSimulation
    {
        // ===== CONSTANTS =====
        private const int BASE_CLAIM_PER_TICK = 17;
        private const int DECREMENT_MULTIPLIER = 4;
        private const int CLAIM_THRESHOLD = 10000;

        // ===== MAIN TICK =====

        /// <summary>
        /// Advances the simulation by one tick. Called at fixed rate (10hz).
        /// Tick order per design doc:
        /// 1. Commands (handled by TickRunner before this call)
        /// 2. Movement
        /// 3. Combat (Phase 5)
        /// 4. Claim bars
        /// 5. Production (Phase 7)
        /// 6. Respawn timers (Phase 5)
        /// 7. Win condition (Phase 6)
        /// </summary>
        public static void SimulateTick(SimulationState state)
        {
            state.tickCount++;

            // Step 2: Movement
            TickAllMovement(state);

            // Step 3: Combat — Phase 5 placeholder
            // TickCombat(state);

            // Step 4: Claiming
            TickClaiming(state);

            // Step 5: Production — Phase 7 placeholder
            // TickProduction(state);

            // Step 6: Respawns — Phase 5 placeholder
            // TickRespawns(state);

            // Step 7: Win condition — Phase 6 placeholder
            // TickWinCondition(state);
        }

        // ===== MOVEMENT =====

        private static void TickAllMovement(SimulationState state)
        {
            for (int i = 0; i < state.villagers.Length; i++)
            {
                if (state.villagers[i].state == VillagerState.Moving)
                {
                    TickMovement(state, i);
                }
            }
        }

        private static void TickMovement(SimulationState state, int villagerIndex)
        {
            VillagerData v = state.villagers[villagerIndex];

            v.moveProgress++;

            if (v.moveProgress >= v.moveSpeedTicks)
            {
                // Advance to next node in path
                v.previousNodeID = v.movePath[v.movePathIndex];
                v.movePathIndex++;
                v.moveProgress = 0;

                v.currentNodeID = v.movePath[v.movePathIndex];

                // Reached end of path?
                if (v.movePathIndex >= v.movePath.Length - 1)
                {
                    v.movePath = new int[0];
                    v.movePathIndex = 0;
                    v.moveProgress = 0;
                    v.targetNodeID = -1;

                    // Determine state from context
                    v.state = DetermineArrivalState(state, v);
                }
            }

            state.villagers[villagerIndex] = v;
        }

        /// <summary>
        /// Determines what state a villager enters upon arriving at a node.
        /// Priority order (future phases fill in higher-priority slots):
        ///   Breach (Phase 6) > Fighting (Phase 5) > Working (Phase 7) > Claiming > Idle
        /// </summary>
        private static VillagerState DetermineArrivalState(SimulationState state, VillagerData villager)
        {
            int nodeID = villager.currentNodeID;
            NodeData node = state.nodes[nodeID];

            // Phase 6 slot: if arrived at enemy core -> process breach
            // int enemyCoreID = state.players[1 - villager.ownerID].coreNodeID;
            // if (nodeID == enemyCoreID) { /* breach logic */ }

            // Phase 5 slot: if enemies present on this node -> Fighting
            // if (HasEnemiesOnNode(state, nodeID, villager.ownerID)) return VillagerState.Fighting;

            // Core nodes are not claimable through normal means
            if (node.districtType == DistrictType.Core)
            {
                return VillagerState.Idle;
            }

            // Own node -> Idle (Phase 7 will add Working for suited villagers on matching districts)
            if (node.ownerID == villager.ownerID)
            {
                return VillagerState.Idle;
            }

            // Neutral or enemy-owned -> Claiming
            return VillagerState.Claiming;
        }

        // ===== CLAIMING =====

        private static void TickClaiming(SimulationState state)
        {
            // First: re-evaluate Idle/Claiming states based on current ownership
            // (handles case where ownership changed under a stationary villager)
            UpdateVillagerClaimStates(state);

            // Then: process claim bars per node
            for (int nodeIndex = 0; nodeIndex < state.nodes.Length; nodeIndex++)
            {
                NodeData node = state.nodes[nodeIndex];

                // Core nodes cannot be claimed
                if (node.districtType == DistrictType.Core) continue;

                // Count claimers per player on this node
                int p0Claimers = 0;
                int p1Claimers = 0;

                for (int v = 0; v < state.villagers.Length; v++)
                {
                    VillagerData vil = state.villagers[v];
                    if (vil.currentNodeID != nodeIndex) continue;
                    if (vil.state != VillagerState.Claiming) continue;

                    if (vil.ownerID == 0) p0Claimers++;
                    else p1Claimers++;
                }

                // Contested: both players present -> claim bar frozen (combat in Phase 5)
                if (p0Claimers > 0 && p1Claimers > 0) continue;

                // Nobody claiming
                if (p0Claimers == 0 && p1Claimers == 0) continue;

                // --- Player 0 claiming ---
                if (p0Claimers > 0 && node.ownerID != 0)
                {
                    int rate;
                    if (node.claimBar < 0)
                    {
                        // Erasing enemy progress: 4x rate
                        rate = DECREMENT_MULTIPLIER * BASE_CLAIM_PER_TICK * p0Claimers;
                    }
                    else
                    {
                        // Building own claim: normal rate
                        rate = BASE_CLAIM_PER_TICK * p0Claimers;
                    }

                    node.claimBar += rate;

                    // Check completion
                    if (node.claimBar >= CLAIM_THRESHOLD)
                    {
                        node.claimBar = CLAIM_THRESHOLD;
                        CompleteClaimForPlayer(state, nodeIndex, 0);
                        node = state.nodes[nodeIndex]; // Re-read (struct was copied)
                    }
                    // Check ownership loss (P1 had it, bar crossed zero)
                    else if (node.ownerID == 1 && node.claimBar >= 0)
                    {
                        node.claimBar = 0; // Clamp at neutral boundary
                        node.ownerID = -1;
                    }
                }

                // --- Player 1 claiming ---
                if (p1Claimers > 0 && node.ownerID != 1)
                {
                    int rate;
                    if (node.claimBar > 0)
                    {
                        // Erasing enemy progress: 4x rate
                        rate = DECREMENT_MULTIPLIER * BASE_CLAIM_PER_TICK * p1Claimers;
                    }
                    else
                    {
                        // Building own claim: normal rate
                        rate = BASE_CLAIM_PER_TICK * p1Claimers;
                    }

                    node.claimBar -= rate;

                    // Check completion
                    if (node.claimBar <= -CLAIM_THRESHOLD)
                    {
                        node.claimBar = -CLAIM_THRESHOLD;
                        CompleteClaimForPlayer(state, nodeIndex, 1);
                        node = state.nodes[nodeIndex];
                    }
                    // Check ownership loss (P0 had it, bar crossed zero)
                    else if (node.ownerID == 0 && node.claimBar <= 0)
                    {
                        node.claimBar = 0;
                        node.ownerID = -1;
                    }
                }

                state.nodes[nodeIndex] = node;
            }

            // Post-claim: update villagers whose context just changed
            UpdateVillagerClaimStates(state);
        }

        /// <summary>
        /// Re-evaluates Idle/Claiming states based on current node ownership.
        /// Only touches Idle and Claiming villagers — Moving, Fighting, Dead are untouched.
        /// </summary>
        private static void UpdateVillagerClaimStates(SimulationState state)
        {
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];

                // Only re-evaluate these two states
                if (v.state != VillagerState.Idle && v.state != VillagerState.Claiming) continue;

                NodeData node = state.nodes[v.currentNodeID];

                // Cores: always Idle
                if (node.districtType == DistrictType.Core)
                {
                    if (v.state != VillagerState.Idle)
                        state.villagers[i].state = VillagerState.Idle;
                    continue;
                }

                // On own node -> Idle
                if (node.ownerID == v.ownerID)
                {
                    if (v.state != VillagerState.Idle)
                        state.villagers[i].state = VillagerState.Idle;
                }
                // Not own node -> Claiming
                else
                {
                    if (v.state != VillagerState.Claiming)
                        state.villagers[i].state = VillagerState.Claiming;
                }
            }
        }

        // ===== CLAIM COMPLETION =====
        //seemingly applies for both, check this.

        private static void CompleteClaimForPlayer(SimulationState state, int nodeIndex, int playerID)
        {
            state.nodes[nodeIndex].ownerID = playerID;

            int bonus = state.nodes[nodeIndex].bonusVillagersOnClaim;
            if (bonus > 0)
            {
                SpawnBonusVillagers(state, nodeIndex, playerID, bonus);
            }
        }

        //villager view objects are made in GameManager, this is exclusively updating the gamestate
        private static void SpawnBonusVillagers(SimulationState state, int nodeID, int playerID, int count)
        {
            int oldLength = state.villagers.Length;
            int newLength = oldLength + count;
            VillagerData[] newArray = new VillagerData[newLength];

            for (int i = 0; i < oldLength; i++)
            {
                newArray[i] = state.villagers[i];
            }

            for (int i = 0; i < count; i++)
            {
                int newID = oldLength + i;
                newArray[newID] = new VillagerData
                {
                    villagerID = newID,
                    ownerID = playerID,
                    currentNodeID = nodeID,
                    targetNodeID = -1,
                    movePath = new int[0],
                    movePathIndex = 0,
                    moveProgress = 0,
                    previousNodeID = nodeID,
                    state = VillagerState.Idle,
                    suit = SuitType.None,
                    hp = 5,
                    maxHP = 5,
                    attackDamage = 1,
                    moveSpeedTicks = 4,
                    respawnTicksRemaining = 0
                };
            }

            state.villagers = newArray;
        }
    }
}