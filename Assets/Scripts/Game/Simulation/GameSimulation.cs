using System.Collections.Generic;

namespace NodeWar.Simulation
{
    public static class GameSimulation
    {
        private static GameBalanceData bal;

        public static void SetBalance(GameBalanceData balance)
        {
            bal = balance;
        }

        // ===== MAIN TICK =====

        /// <summary>
        /// Advances the simulation by one tick. Called at fixed rate (10hz).
        /// Tick order:
        /// 1. Commands (handled by TickRunner before this call)
        /// 2. Movement (with combat interruption and breach-on-arrival)
        /// 3. Combat (detect fights, process cooldowns, deal damage, handle deaths)
        /// 4. Claim bars
        /// 5. Production
        /// 6. Healing (every 30 ticks)
        /// 7. Respawn timers
        /// 8. Win condition (breachCount >= 3)
        /// 9. Post-combat resume (fight ended, determine next state)
        /// </summary>
        public static void SimulateTick(SimulationState state)
        {
            state.tickCount++;

            // Step 2: Movement
            TickAllMovement(state);

            TickRampartBonuses(state);   // NEW

            // Step 3: Combat
            TickCombat(state);

            // Step 4: Claiming
            TickClaiming(state);

            // Step 5: Production
            TickProduction(state);

            // Step 6: Healing
            TickHealing(state);

            // Step 7: Respawns
            TickRespawns(state);

            // Step 8: Win condition
            TickWinCondition(state);

            // Step 9: Post-combat resume
            // Separated from step 3 to avoid state thrashing within a single tick.
            // Combat resolves, deaths happen, THEN survivors figure out what to do next.
            // This prevents a villager from killing an enemy and immediately starting to
            // claim in the same tick, which could cause edge cases with the claim
            // evaluation also running in step 4.
            TickPostCombatResume(state);
        }

        // ===== STEP 2: MOVEMENT =====

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

            // Invariant: a Moving villager always has a further node ahead on its path.
            // If that is violated the villager is in a corrupt movement state -- recover
            // deterministically rather than indexing off the end of movePath below.
            if (v.movePathIndex + 1 >= v.movePath.Length)
            {
                v.movePath = new int[0];
                v.movePathIndex = 0;
                v.moveProgress = 0;
                v.targetNodeID = -1;
                state.villagers[villagerIndex] = v;
                ApplyArrivalState(state, villagerIndex);
                return;
            }

            v.moveProgress++;

            // Calculate ticks needed for current edge
            int edgeWeight = GetEdgeWeight(state, v.movePath[v.movePathIndex], v.movePath[v.movePathIndex + 1]);
            int ticksForEdge = edgeWeight * v.moveSpeedTicks;

            if (v.moveProgress >= ticksForEdge)
            {
                // Advance to next node in path
                v.previousNodeID = v.movePath[v.movePathIndex];
                v.movePathIndex++;
                v.moveProgress = 0;

                v.currentNodeID = v.movePath[v.movePathIndex];

                // --- Check for combat interruption or breach on EVERY node arrival ---
                int enemyCoreID = state.players[1 - v.ownerID].coreNodeID;

                bool enemiesPresent = HasLivingEnemiesOnNode(state, v.currentNodeID, v.ownerID);

                if (v.currentNodeID == enemyCoreID)
                {
                    if (enemiesPresent)
                    {
                        v.state = VillagerState.Fighting;
                        v.moveProgress = 0;
                        v.attackCooldownRemaining = v.attackCooldownMax;
                        v.combatTargetID = -1;
                        state.villagers[villagerIndex] = v;
                        return;
                    }
                    else
                    {
                        ProcessBreach(state, villagerIndex, v);
                        return;
                    }
                }

                if (enemiesPresent)
                {
                    v.state = VillagerState.Fighting;
                    v.moveProgress = 0;
                    v.attackCooldownRemaining = v.attackCooldownMax;
                    v.combatTargetID = -1;
                    state.villagers[villagerIndex] = v;
                    return;
                }

                // --- Normal arrival logic (no enemies) ---
                if (v.movePathIndex >= v.movePath.Length - 1)
                {
                    v.movePath = new int[0];
                    v.movePathIndex = 0;
                    v.moveProgress = 0;
                    v.targetNodeID = -1;
                    state.villagers[villagerIndex] = v;
                    ApplyArrivalState(state, villagerIndex);
                    return;
                }
            }

            state.villagers[villagerIndex] = v;
        }

        private static void TickRampartBonuses(SimulationState state)
        {
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.state == VillagerState.Dead || v.isConsumed) continue;

                bool shouldHaveBonus =
                    state.nodes[v.currentNodeID].districtType == DistrictType.Rampart &&
                    state.nodes[v.currentNodeID].ownerID == v.ownerID;

                if (shouldHaveBonus && !v.hasRampartBonus)
                {
                    state.villagers[i].maxHP += bal.rampartMaxHPBonus;
                    state.villagers[i].hp += bal.rampartMaxHPBonus;
                    state.villagers[i].hasRampartBonus = true;
                }
                else if (!shouldHaveBonus && v.hasRampartBonus)
                {
                    state.villagers[i].maxHP -= bal.rampartMaxHPBonus;
                    if (state.villagers[i].hp > state.villagers[i].maxHP)
                        state.villagers[i].hp = state.villagers[i].maxHP;
                    state.villagers[i].hasRampartBonus = false;
                }
            }
        }


        /// <summary>
        /// Applies suit assignment, production timer, and state for a villager
        /// that has arrived at a node (end of path or post-combat with no path).
        /// Directly modifies state.villagers[villagerIndex].
        /// </summary>
        private static void ApplyArrivalState(SimulationState state, int villagerIndex)
        {
            VillagerData v = state.villagers[villagerIndex];
            int nodeID = v.currentNodeID;
            NodeData node = state.nodes[nodeID];

            // Core nodes: always Idle.
            // Non-combat suits (Farmer, Miner, Smelter) are free and re-assigned on arrival
            // at production nodes, so reverting them here is harmless and keeps things clean.
            // Soldier suit is PERMANENT until death — do not strip it.
            if (node.districtType == DistrictType.Core)
            {
                if (!GameBalanceData.IsCombatSuit(v.suit))
                {
                    state.villagers[villagerIndex].suit = SuitType.None;
                    state.villagers[villagerIndex].attackDamage = bal.baseAttackDamage;
                    state.villagers[villagerIndex].moveSpeedTicks = bal.baseMoveSpeedTicks;
                    state.villagers[villagerIndex].attackCooldownMax = bal.baseAttackCooldownMax;
                }
                state.villagers[villagerIndex].state = VillagerState.Idle;
                return;
            }

            // Own node
            if (node.ownerID == v.ownerID)
            {
                if (GameBalanceData.IsCombatSuit(v.suit))
                {
                    state.villagers[villagerIndex].state = VillagerState.Idle;
                    return;
                }

                if (node.districtType == DistrictType.Farm)
                {
                    state.villagers[villagerIndex].suit = SuitType.Farmer;
                    int workers = CountFriendlyWorkersOnNode(state, nodeID, v.ownerID);
                    if (workers < bal.maxWorkersPerNode)
                    {
                        state.villagers[villagerIndex].productionTicksMax = bal.foodProductionTicks;
                        state.villagers[villagerIndex].productionTicksRemaining = bal.foodProductionTicks;
                        state.villagers[villagerIndex].state = VillagerState.Working;
                    }
                    else state.villagers[villagerIndex].state = VillagerState.Idle;
                    return;
                }

                if (node.districtType == DistrictType.Mine)
                {
                    state.villagers[villagerIndex].suit = SuitType.Miner;
                    int workers = CountFriendlyWorkersOnNode(state, nodeID, v.ownerID);
                    if (workers < bal.maxWorkersPerNode)
                    {
                        state.villagers[villagerIndex].productionTicksMax = bal.materialProductionTicks;
                        state.villagers[villagerIndex].productionTicksRemaining = bal.materialProductionTicks;
                        state.villagers[villagerIndex].state = VillagerState.Working;
                    }
                    else state.villagers[villagerIndex].state = VillagerState.Idle;
                    return;
                }

                if (node.districtType == DistrictType.Forge)
                {
                    state.villagers[villagerIndex].suit = SuitType.Smelter;
                    int workers = CountFriendlyWorkersOnNode(state, nodeID, v.ownerID);
                    if (workers < bal.maxWorkersPerNode)
                    {
                        state.villagers[villagerIndex].productionTicksMax = bal.metalProductionTicks;
                        state.villagers[villagerIndex].productionTicksRemaining = bal.metalProductionTicks;
                        state.villagers[villagerIndex].state = VillagerState.Working;
                    }
                    else state.villagers[villagerIndex].state = VillagerState.Idle;
                    return;
                }

                if (node.districtType == DistrictType.Market)
                {
                    state.villagers[villagerIndex].suit = SuitType.Merchant;
                    int workers = CountFriendlyWorkersOnNode(state, nodeID, v.ownerID);
                    if (workers < bal.maxWorkersPerNode)
                    {
                        state.villagers[villagerIndex].productionTicksMax = bal.marketFoodProductionTicks;
                        state.villagers[villagerIndex].productionTicksRemaining = bal.marketFoodProductionTicks;
                        state.villagers[villagerIndex].state = VillagerState.Working;
                    }
                    else state.villagers[villagerIndex].state = VillagerState.Idle;
                    return;
                }

                if (node.districtType == DistrictType.Sanctuary)
                {
                    state.villagers[villagerIndex].suit = SuitType.Acolyte;
                    int workers = CountFriendlyWorkersOnNode(state, nodeID, v.ownerID);
                    if (workers < bal.maxWorkersPerNode)
                    {
                        state.villagers[villagerIndex].productionTicksMax = 0;
                        state.villagers[villagerIndex].productionTicksRemaining = 0;
                        state.villagers[villagerIndex].state = VillagerState.Working;
                    }
                    else state.villagers[villagerIndex].state = VillagerState.Idle;
                    return;
                }

                if (node.districtType == DistrictType.Watchtower)
                {
                    state.villagers[villagerIndex].suit = SuitType.Watcher;
                    int workers = CountFriendlyWorkersOnNode(state, nodeID, v.ownerID);
                    if (workers < bal.maxWorkersPerNode)
                    {
                        state.villagers[villagerIndex].productionTicksMax = 0;
                        state.villagers[villagerIndex].productionTicksRemaining = 0;
                        state.villagers[villagerIndex].state = VillagerState.Working;
                    }
                    else state.villagers[villagerIndex].state = VillagerState.Idle;
                    return;
                }

                // Camp, Barracks, Arsenal, Rampart, Shrine, Village, None — strip non-combat suit, go Idle
                if (!GameBalanceData.IsCombatSuit(v.suit))
                {
                    state.villagers[villagerIndex].suit = SuitType.None;
                    state.villagers[villagerIndex].attackDamage = bal.baseAttackDamage;
                    state.villagers[villagerIndex].moveSpeedTicks = bal.baseMoveSpeedTicks;
                    state.villagers[villagerIndex].attackCooldownMax = bal.baseAttackCooldownMax;
                }
                state.villagers[villagerIndex].state = VillagerState.Idle;
                return;
            }

            // Not own node: Claiming logic
            int friendlyClaimers = CountFriendlyClaimersOnNode(state, nodeID, v.ownerID);
            if (friendlyClaimers < bal.maxClaimersPerNode)
                state.villagers[villagerIndex].state = VillagerState.Claiming;
            else
                state.villagers[villagerIndex].state = VillagerState.Idle;
        }

        // ===== STEP 3: COMBAT =====

        private static void TickCombat(SimulationState state)
        {
            // Phase A: For each node, detect if both players have living villagers present.
            //          Set all living villagers on contested nodes to Fighting state.
            for (int nodeIndex = 0; nodeIndex < state.nodes.Length; nodeIndex++)
            {
                bool hasP0 = false;
                bool hasP1 = false;

                for (int v = 0; v < state.villagers.Length; v++)
                {
                    VillagerData vil = state.villagers[v];
                    if (vil.currentNodeID != nodeIndex) continue;
                    if (vil.state == VillagerState.Dead) continue;
                    if (vil.isConsumed) continue;

                    if (vil.ownerID == 0) hasP0 = true;
                    else hasP1 = true;
                }

                if (!hasP0 || !hasP1) continue;

                // Both players present on this node: set everyone to Fighting
                for (int v = 0; v < state.villagers.Length; v++)
                {
                    VillagerData vil = state.villagers[v];
                    if (vil.currentNodeID != nodeIndex) continue;
                    if (vil.state == VillagerState.Dead) continue;
                    if (vil.isConsumed) continue;

                    if (vil.state != VillagerState.Fighting)
                    {
                        state.villagers[v].state = VillagerState.Fighting;
                        state.villagers[v].attackCooldownRemaining = state.villagers[v].attackCooldownMax;
                        state.villagers[v].combatTargetID = -1;
                        state.villagers[v].moveProgress = 0;
                    }
                }
            }

            // Phase B: Assign round-robin targets for all contested nodes
            AssignAllCombatTargets(state);

            // Phase C: Process attack cooldowns and deal damage
            for (int v = 0; v < state.villagers.Length; v++)
            {
                if (state.villagers[v].state != VillagerState.Fighting) continue;
                if (state.villagers[v].isConsumed) continue;

                state.villagers[v].attackCooldownRemaining--;

                if (state.villagers[v].attackCooldownRemaining <= 0)
                {
                    if (state.villagers[v].suit == SuitType.Medic)
                    {
                        int healTarget = FindMostDamagedFriendly(state,
                            state.villagers[v].currentNodeID, state.villagers[v].ownerID, v);
                        if (healTarget >= 0)
                        {
                            state.villagers[healTarget].hp++;
                            if (state.villagers[healTarget].hp > state.villagers[healTarget].maxHP)
                                state.villagers[healTarget].hp = state.villagers[healTarget].maxHP;
                        }
                    }
                    else
                    {
                        int targetID = state.villagers[v].combatTargetID;
                        if (targetID >= 0 && targetID < state.villagers.Length)
                        {
                            if (state.villagers[targetID].state != VillagerState.Dead &&
                                !state.villagers[targetID].isConsumed)
                            {
                                int damage = state.villagers[v].attackDamage;
                                if (state.villagers[targetID].hasRampartBonus)
                                {
                                    damage -= bal.rampartDamageReduction;
                                    if (damage < 1) damage = 1;
                                }
                                state.villagers[targetID].hp -= damage;
                            }
                        }
                    }
                    state.villagers[v].attackCooldownRemaining = state.villagers[v].attackCooldownMax;
                }
            }

            // Phase D: Handle deaths
            for (int v = 0; v < state.villagers.Length; v++)
            {
                if (state.villagers[v].state == VillagerState.Dead) continue;
                if (state.villagers[v].isConsumed) continue;

                if (state.villagers[v].hp <= 0)
                {
                    state.villagers[v].state = VillagerState.Dead;
                    state.villagers[v].hp = 0;
                    state.villagers[v].respawnTicksRemaining = bal.respawnTicks;
                    state.villagers[v].movePath = new int[0];
                    state.villagers[v].movePathIndex = 0;
                    state.villagers[v].moveProgress = 0;
                    state.villagers[v].targetNodeID = -1;
                    state.villagers[v].combatTargetID = -1;
                    state.villagers[v].hasRampartBonus = false;
                }
            }
        }

        /// <summary>
        /// Assigns round-robin combat targets for all nodes with active combat.
        /// Called after setting Fighting states and after deaths.
        /// </summary>
        private static void AssignAllCombatTargets(SimulationState state)
        {
            // Track which nodes have combat
            for (int nodeIndex = 0; nodeIndex < state.nodes.Length; nodeIndex++)
            {
                // Gather fighters per side on this node
                List<int> p0Fighters = new List<int>();
                List<int> p1Fighters = new List<int>();

                for (int v = 0; v < state.villagers.Length; v++)
                {
                    VillagerData vil = state.villagers[v];
                    if (vil.currentNodeID != nodeIndex) continue;
                    if (vil.state != VillagerState.Fighting) continue;
                    if (vil.isConsumed) continue;

                    if (vil.ownerID == 0) p0Fighters.Add(v);
                    else p1Fighters.Add(v);
                }

                // Need both sides for combat assignments
                if (p0Fighters.Count == 0 || p1Fighters.Count == 0) continue;

                // Sort targets by fightPriority descending, then villagerID ascending
                // P0 attackers target P1 fighters
                List<int> p1Targets = new List<int>(p1Fighters);
                p1Targets.Sort((a, b) =>
                {
                    int priA = state.villagers[a].fightPriority;
                    int priB = state.villagers[b].fightPriority;
                    if (priB != priA) return priB.CompareTo(priA); // descending priority
                    return a.CompareTo(b); // ascending ID
                });

                // P1 attackers target P0 fighters
                List<int> p0Targets = new List<int>(p0Fighters);
                p0Targets.Sort((a, b) =>
                {
                    int priA = state.villagers[a].fightPriority;
                    int priB = state.villagers[b].fightPriority;
                    if (priB != priA) return priB.CompareTo(priA);
                    return a.CompareTo(b);
                });

                // Assign round-robin: P0 attackers -> P1 targets
                for (int i = 0; i < p0Fighters.Count; i++)
                {
                    int targetIndex = i % p1Targets.Count;
                    state.villagers[p0Fighters[i]].combatTargetID = p1Targets[targetIndex];
                }

                // Assign round-robin: P1 attackers -> P0 targets
                for (int i = 0; i < p1Fighters.Count; i++)
                {
                    int targetIndex = i % p0Targets.Count;
                    state.villagers[p1Fighters[i]].combatTargetID = p0Targets[targetIndex];
                }
            }
        }

        // ===== STEP 4: CLAIMING =====

        private static void TickClaiming(SimulationState state)
        {
            // Re-evaluate Idle/Claiming states based on current ownership
            UpdateVillagerClaimStates(state);

            // Process claim bars per node
            for (int nodeIndex = 0; nodeIndex < state.nodes.Length; nodeIndex++)
            {
                NodeData node = state.nodes[nodeIndex];

                if (node.districtType == DistrictType.Core) continue;

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

                // Contested: both present -> frozen (combat handles this via Fighting state,
                // but kept as safety check)
                if (p0Claimers > 0 && p1Claimers > 0) continue;

                if (p0Claimers == 0 && p1Claimers == 0) continue;

                // --- Player 0 claiming ---
                if (p0Claimers > 0 && node.ownerID != 0)
                {
                    int activeDecrement = bal.decrementMultiplier;
                    if (node.districtType == DistrictType.Rampart)
                        activeDecrement = bal.rampartDecrementMultiplier;

                    int rate;
                    if (node.claimBar < 0)
                    {
                        rate = activeDecrement * bal.baseClaimPerTick * p0Claimers;
                    }
                    else
                    {
                        rate = bal.baseClaimPerTick * p0Claimers;
                    }

                    if (HasAdjacentFriendlyWatchtowerWorkers(state, nodeIndex, 0)) 
                        rate = rate * bal.watchtowerClaimNumerator / bal.watchtowerClaimDenominator;

                    node.claimBar += rate;

                    if (node.claimBar >= bal.claimThreshold)
                    {
                        node.claimBar = bal.claimThreshold;
                        CompleteClaimForPlayer(state, nodeIndex, 0);
                        node = state.nodes[nodeIndex];
                    }
                    else if (node.ownerID == 1 && node.claimBar >= 0)
                    {
                        node.claimBar = 0;
                        node.ownerID = -1;
                    }
                }

                // --- Player 1 claiming ---
                if (p1Claimers > 0 && node.ownerID != 1)
                {
                    int activeDecrement = bal.decrementMultiplier;
                    if (node.districtType == DistrictType.Rampart)
                        activeDecrement = bal.rampartDecrementMultiplier;

                    int rate;
                    if (node.claimBar > 0)
                    {
                        rate = activeDecrement * bal.baseClaimPerTick * p1Claimers;
                    }
                    else
                    {
                        rate = bal.baseClaimPerTick * p1Claimers;
                    }

                    if (HasAdjacentFriendlyWatchtowerWorkers(state, nodeIndex, 1)) 
                        rate = rate * bal.watchtowerClaimNumerator / bal.watchtowerClaimDenominator;

                    node.claimBar -= rate;

                    if (node.claimBar <= -bal.claimThreshold)
                    {
                        node.claimBar = -bal.claimThreshold;
                        CompleteClaimForPlayer(state, nodeIndex, 1);
                        node = state.nodes[nodeIndex];
                    }
                    else if (node.ownerID == 0 && node.claimBar <= 0)
                    {
                        node.claimBar = 0;
                        node.ownerID = -1;
                    }
                }

                state.nodes[nodeIndex] = node;
            }

            UpdateVillagerClaimStates(state);
        }

        // ===== STEP 5: PRODUCTION =====

        /// <summary>
        /// Every tick, decrements production timers for Working villagers.
        /// When a timer reaches 0: awards the appropriate resource to the owner
        /// and resets the timer for the next cycle.
        /// Farm -> +1 Food, Mine -> +1 Material, Forge -> +1 Metal (costs 1 Material, requires allocation > 0).
        /// </summary>
        private static void TickProduction(SimulationState state)
        {
            for (int idx = 0; idx < state.villagers.Length; idx++)
            {
                if (state.villagers[idx].state != VillagerState.Working) continue;
                if (state.villagers[idx].isConsumed) continue;
                if (state.villagers[idx].productionTicksMax <= 0) continue;

                state.villagers[idx].productionTicksRemaining--;

                if (state.villagers[idx].productionTicksRemaining <= 0)
                {
                    int ownerID = state.villagers[idx].ownerID;
                    int nodeID = state.villagers[idx].currentNodeID;
                    DistrictType district = state.nodes[nodeID].districtType;

                    switch (district)
                    {
                        case DistrictType.Farm:
                            state.players[ownerID].food++;
                            break;

                        case DistrictType.Mine:
                            state.players[ownerID].materials++;
                            break;

                        case DistrictType.Forge:
                            // Only produce if allocation is enabled AND player has materials
                            if (state.nodes[nodeID].materialAllocation > 0 &&
                                state.players[ownerID].materials >= 1)
                            {
                                state.players[ownerID].materials--;
                                state.players[ownerID].metal++;
                            }
                            // If allocation is 0 or no materials: timer resets, nothing produced
                            break;
                        case DistrictType.Market:
                            if (state.villagers[idx].productionTicksMax == bal.marketFoodProductionTicks)
                            {
                                state.players[ownerID].food++;
                                state.villagers[idx].productionTicksMax = bal.marketMaterialProductionTicks;
                                state.villagers[idx].productionTicksRemaining = bal.marketMaterialProductionTicks;
                            }
                            else
                            {
                                state.players[ownerID].materials++;
                                state.villagers[idx].productionTicksMax = bal.marketFoodProductionTicks;
                                state.villagers[idx].productionTicksRemaining = bal.marketFoodProductionTicks;
                            }
                            break;
                    }

                    // Reset timer for next production cycle
                    state.villagers[idx].productionTicksRemaining = state.villagers[idx].productionTicksMax;
                }
            }
        }

        private static void UpdateVillagerClaimStates(SimulationState state)
        {
            for (int idx = 0; idx < state.villagers.Length; idx++)
            {
                VillagerData v = state.villagers[idx];

                // Only re-evaluate Idle, Claiming, and Working villagers
                if (v.state != VillagerState.Idle && v.state != VillagerState.Claiming && v.state != VillagerState.Working) continue;
                if (v.isConsumed) continue;

                NodeData node = state.nodes[v.currentNodeID];

                // Core nodes: always Idle
                if (node.districtType == DistrictType.Core)
                {
                    if (v.state != VillagerState.Idle)
                        state.villagers[idx].state = VillagerState.Idle;
                    continue;
                }

                // === OWN NODE ===
                if (node.ownerID == v.ownerID)
                {
                    // Soldiers never work, just Idle
                    if (GameBalanceData.IsCombatSuit(v.suit))
                    {
                        if (v.state != VillagerState.Idle)
                            state.villagers[idx].state = VillagerState.Idle;
                        continue;
                    }

                    // Is this a production node?
                    SuitType expectedSuit = GetExpectedSuit(node.districtType);

                    if (expectedSuit != SuitType.None)
                    {
                        // Production node: assign suit if needed
                        if (v.suit != expectedSuit)
                            state.villagers[idx].suit = expectedSuit;

                        // Try Working if not already
                        if (v.state != VillagerState.Working)
                        {
                            int workers = CountFriendlyWorkersOnNode(state, v.currentNodeID, v.ownerID);
                            if (workers < bal.maxWorkersPerNode)
                            {
                                state.villagers[idx].state = VillagerState.Working;
                                state.villagers[idx].productionTicksMax = GetProductionTicks(node.districtType);
                                state.villagers[idx].productionTicksRemaining = GetProductionTicks(node.districtType);
                            }
                            else
                            {
                                state.villagers[idx].state = VillagerState.Idle;
                            }
                        }
                        // If already Working, stay Working
                    }
                    else
                    {
                        // Non-production node (Barracks, Village, None): Idle
                        if (v.state != VillagerState.Idle)
                            state.villagers[idx].state = VillagerState.Idle;
                    }

                    continue;
                }

                // === NOT OWN NODE ===
                if (v.state != VillagerState.Claiming)
                {
                    int friendlyClaimers = CountFriendlyClaimersOnNode(state, v.currentNodeID, v.ownerID);
                    if (friendlyClaimers < bal.maxClaimersPerNode)
                    {
                        state.villagers[idx].state = VillagerState.Claiming;
                    }
                }
            }
        }

        private static void CompleteClaimForPlayer(SimulationState state, int nodeIndex, int playerID)
        {
            state.nodes[nodeIndex].ownerID = playerID;

            if (state.nodes[nodeIndex].slotType != NodeSlotType.Fixed)
            {
                DistrictType upgrade = GetPlayerUpgradeForSlot(state, playerID, state.nodes[nodeIndex].slotType);
                state.nodes[nodeIndex].districtType = upgrade != DistrictType.None
                    ? upgrade
                    : state.nodes[nodeIndex].baseDistrictType;

                // Reset non-combat workers — node type just changed
                for (int i = 0; i < state.villagers.Length; i++)
                {
                    if (state.villagers[i].currentNodeID != nodeIndex) continue;
                    if (state.villagers[i].state == VillagerState.Dead || state.villagers[i].isConsumed) continue;
                    if (GameBalanceData.IsCombatSuit(state.villagers[i].suit)) continue;
                    state.villagers[i].state = VillagerState.Idle;
                    state.villagers[i].suit = SuitType.None;
                    state.villagers[i].productionTicksRemaining = 0;
                    state.villagers[i].productionTicksMax = 0;
                }
            }

            int bonus = state.nodes[nodeIndex].bonusVillagersOnClaim;
            if (bonus > 0)
                SpawnBonusVillagers(state, nodeIndex, playerID, bonus);
        }

        private static DistrictType GetPlayerUpgradeForSlot(SimulationState state, int playerID, NodeSlotType slotType)
        {
            int[] draftedNodes = state.players[playerID].draftedNodes;
            if (draftedNodes == null) return DistrictType.None;
            for (int i = 0; i < draftedNodes.Length; i++)
            {
                DistrictType drafted = (DistrictType)draftedNodes[i];
                if (GameBalanceData.GetSlotTypeForDistrict(drafted) == slotType)
                    return drafted;
            }
            return DistrictType.None;
        }

        private static void SpawnBonusVillagers(SimulationState state, int nodeID, int playerID, int count)
        {
            // Count how many villagers this player currently has (including dead, excluding consumed)
            int playerVillagerCount = 0;
            for (int i = 0; i < state.villagers.Length; i++)
            {
                if (state.villagers[i].ownerID == playerID && !state.villagers[i].isConsumed)
                    playerVillagerCount++;
            }

            // Enforce per-player cap
            int maxAllowed = bal.maxVillagersPerPlayer - playerVillagerCount;
            if (maxAllowed <= 0) return;
            if (count > maxAllowed) count = maxAllowed;

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
                    hp = bal.baseHP,
                    maxHP = bal.baseHP,
                    attackDamage = bal.baseAttackDamage,
                    moveSpeedTicks = bal.baseMoveSpeedTicks,
                    respawnTicksRemaining = 0,
                    attackCooldownRemaining = bal.baseAttackCooldownMax,
                    attackCooldownMax = bal.baseAttackCooldownMax,
                    combatTargetID = -1,
                    fightPriority = 0,
                    isConsumed = false,
                    productionTicksRemaining = 0,
                    productionTicksMax = 0,
                    hasRampartBonus = false
                };
            }

            state.villagers = newArray;
        }

        // ===== STEP 6 =: HEALING =====

        /// <summary>
        /// Every 30 ticks (3 seconds), heal all damaged non-fighting, non-dead villagers by 1 HP.
        /// Simple approach: no per-villager heal timer needed.
        /// </summary>
        private static void TickHealing(SimulationState state)
        {
            if (state.tickCount % bal.healIntervalTicks != 0) return;

            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.isConsumed) continue;
                if (v.state == VillagerState.Dead) continue;
                if (v.state == VillagerState.Fighting) continue;
                if (v.hp >= v.maxHP) continue;

                bool onOwnedShrine = state.nodes[v.currentNodeID].districtType == DistrictType.Shrine &&
                                     state.nodes[v.currentNodeID].ownerID == v.ownerID;
                int interval = onOwnedShrine ? bal.shrineHealIntervalTicks : bal.healIntervalTicks;
                if (state.tickCount % interval == 0)
                    state.villagers[i].hp++;
            }
        }

        // ===== STEP 6: RESPAWNS =====

        private static void TickRespawns(SimulationState state)
        {
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.state != VillagerState.Dead) continue;
                if (v.isConsumed) continue;

                int sanctuaryWorkers = CountSanctuaryWorkersForPlayer(state, v.ownerID);
                int decrement = 1 + (sanctuaryWorkers * bal.sanctuaryRespawnBoostPerWorker);
                state.villagers[i].respawnTicksRemaining -= decrement;

                if (state.villagers[i].respawnTicksRemaining <= 0)
                {
                    int coreNode = state.players[v.ownerID].coreNodeID;

                    state.villagers[i].state = VillagerState.Idle;
                    state.villagers[i].currentNodeID = coreNode;
                    state.villagers[i].previousNodeID = coreNode;
                    state.villagers[i].targetNodeID = -1;
                    state.villagers[i].movePath = new int[0];
                    state.villagers[i].movePathIndex = 0;
                    state.villagers[i].moveProgress = 0;
                    state.villagers[i].hp = state.villagers[i].maxHP;
                    state.villagers[i].suit = SuitType.None;
                    state.villagers[i].attackDamage = bal.baseAttackDamage;
                    state.villagers[i].moveSpeedTicks = bal.baseMoveSpeedTicks;
                    state.villagers[i].attackCooldownMax = bal.baseAttackCooldownMax;
                    state.villagers[i].attackCooldownRemaining = bal.baseAttackCooldownMax;
                    state.villagers[i].combatTargetID = -1;
                    state.villagers[i].respawnTicksRemaining = 0;
                    state.villagers[i].hasRampartBonus = false;
                }
            }
        }

        // ===== STEP 8: WIN CONDITION =====

        private static void TickWinCondition(SimulationState state)
        {
            for (int p = 0; p < state.players.Length; p++)
            {
                if (state.players[p].breachCount >= bal.breachThreshold)
                {
                    state.gameOver = true;
                    // The winner is the OTHER player (the one who breached this player's core)
                    state.winnerID = 1 - p;
                    return;
                }
            }
        }

        // ===== STEP 9: POST-COMBAT RESUME =====

        /// <summary>
        /// For villagers in Fighting state whose fight just ended (no enemies remain on node):
        /// Determine next state - resume path, claim, idle, or breach if on enemy core.
        /// 
        /// This is separated from TickCombat (step 3) to avoid state thrashing.
        /// Combat resolves and deaths happen first, THEN survivors figure out what to do.
        /// Without this separation, a villager could kill an enemy and immediately begin
        /// claiming in the same tick that TickClaiming also evaluates, potentially causing
        /// double-counting or ordering-dependent bugs.
        /// </summary>
        private static void TickPostCombatResume(SimulationState state)
        {
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.state != VillagerState.Fighting) continue;
                if (v.isConsumed) continue;

                // Check if enemies remain on this node
                if (HasLivingEnemiesOnNode(state, v.currentNodeID, v.ownerID)) continue;

                // Fight is over. Determine next state.

                // Check: are we on the enemy core? If so, breach.
                int enemyCoreID = state.players[1 - v.ownerID].coreNodeID;
                if (v.currentNodeID == enemyCoreID)
                {
                    ProcessBreach(state, i, v);
                    continue;
                }

                // Check: do we have a remaining path to resume?
                if (v.movePath.Length > 0 && v.movePathIndex < v.movePath.Length - 1)
                {
                    NodeData currentNode = state.nodes[v.currentNodeID];

                    if (currentNode.ownerID == v.ownerID)
                    {
                        // Own node: check if suit matches for Working
                        if (!GameBalanceData.IsCombatSuit(v.suit) && v.suit == GetExpectedSuit(currentNode.districtType))
                        {
                            int workers = CountFriendlyWorkersOnNode(state, v.currentNodeID, v.ownerID);
                            if (workers < bal.maxWorkersPerNode)
                            {
                                // Stop and work here
                                state.villagers[i].state = VillagerState.Working;
                                state.villagers[i].productionTicksMax = GetProductionTicks(currentNode.districtType);
                                state.villagers[i].productionTicksRemaining = GetProductionTicks(currentNode.districtType);
                                state.villagers[i].combatTargetID = -1;
                                continue;
                            }
                        }
                        // No match or at cap: resume movement
                        state.villagers[i].state = VillagerState.Moving;
                        state.villagers[i].combatTargetID = -1;
                    }
                    else if (currentNode.districtType == DistrictType.Core)
                    {
                        // On a core node (not enemy, handled above): resume
                        state.villagers[i].state = VillagerState.Moving;
                        state.villagers[i].combatTargetID = -1;
                    }
                    else
                    {
                        // Not our node: check if we should help claim or keep moving
                        int friendlyClaimers = CountFriendlyClaimersOnNode(state, v.currentNodeID, v.ownerID);
                        if (friendlyClaimers < bal.maxClaimersPerNode)
                        {
                            state.villagers[i].state = VillagerState.Claiming;
                            state.villagers[i].combatTargetID = -1;
                        }
                        else
                        {
                            state.villagers[i].state = VillagerState.Moving;
                            state.villagers[i].combatTargetID = -1;
                        }
                    }
                }
                else
                {
                    // No remaining path: full arrival logic
                    state.villagers[i].combatTargetID = -1;
                    state.villagers[i].movePath = new int[0];
                    state.villagers[i].movePathIndex = 0;
                    state.villagers[i].targetNodeID = -1;
                    ApplyArrivalState(state, i);
                }
            }
        }

        // ===== BREACH PROCESSING =====

        private static void ProcessBreach(SimulationState state, int villagerIndex, VillagerData v)
        {
            // Increment breach count for the defending player
            int defendingPlayer = 1 - v.ownerID;
            state.players[defendingPlayer].breachCount++;

            // Consume the breaching villager permanently
            state.villagers[villagerIndex].state = VillagerState.Dead;
            state.villagers[villagerIndex].isConsumed = true;
            state.villagers[villagerIndex].hp = 0;
            state.villagers[villagerIndex].movePath = new int[0];
            state.villagers[villagerIndex].movePathIndex = 0;
            state.villagers[villagerIndex].moveProgress = 0;
            state.villagers[villagerIndex].targetNodeID = -1;
            state.villagers[villagerIndex].combatTargetID = -1;
            state.villagers[villagerIndex].hasRampartBonus = false;
        }

        // ===== HELPER FUNCTIONS =====

        /// <summary>
        /// Returns true if there are any living enemy villagers on the specified node.
        /// Living means: not Dead, not isConsumed.
        /// </summary>
        private static bool HasLivingEnemiesOnNode(SimulationState state, int nodeID, int myOwnerID)
        {
            for (int v = 0; v < state.villagers.Length; v++)
            {
                VillagerData vil = state.villagers[v];
                if (vil.currentNodeID != nodeID) continue;
                if (vil.ownerID == myOwnerID) continue;
                if (vil.state == VillagerState.Dead) continue;
                if (vil.isConsumed) continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Counts friendly villagers in Claiming state on a given node.
        /// Used for MAX_CLAIMERS_PER_NODE enforcement.
        /// </summary>
        private static int CountFriendlyClaimersOnNode(SimulationState state, int nodeID, int ownerID)
        {
            int count = 0;
            for (int v = 0; v < state.villagers.Length; v++)
            {
                VillagerData vil = state.villagers[v];
                if (vil.currentNodeID != nodeID) continue;
                if (vil.ownerID != ownerID) continue;
                if (vil.state != VillagerState.Claiming) continue;
                count++;
            }
            return count;
        }

        /// <summary>
        /// Returns the production tick duration for a given district type.
        /// Returns 0 for non-production districts.
        /// </summary>
        private static int GetProductionTicks(DistrictType district)
        {
            switch (district)
            {
                case DistrictType.Farm: return bal.foodProductionTicks;
                case DistrictType.Mine: return bal.materialProductionTicks;
                case DistrictType.Forge: return bal.metalProductionTicks;
                case DistrictType.Market: return bal.marketFoodProductionTicks;
                case DistrictType.Sanctuary: return 0;
                case DistrictType.Watchtower: return 0;
                default: return 0;
            }
        }

        /// <summary>
        /// Counts friendly villagers in Working state on a given node.
        /// Used for MAX_WORKERS_PER_NODE enforcement.
        /// </summary>
        private static int CountFriendlyWorkersOnNode(SimulationState state, int nodeID, int ownerID)
        {
            int count = 0;
            for (int v = 0; v < state.villagers.Length; v++)
            {
                VillagerData vil = state.villagers[v];
                if (vil.currentNodeID != nodeID) continue;
                if (vil.ownerID != ownerID) continue;
                if (vil.state != VillagerState.Working) continue;
                count++;
            }
            return count;
        }

        /// <summary>
        /// Returns the expected suit for a given production district.
        /// Returns SuitType.None for non-production districts.
        /// </summary>
        private static SuitType GetExpectedSuit(DistrictType district)
        {
            switch (district)
            {
                case DistrictType.Farm: return SuitType.Farmer;
                case DistrictType.Mine: return SuitType.Miner;
                case DistrictType.Forge: return SuitType.Smelter;
                case DistrictType.Market: return SuitType.Merchant;
                case DistrictType.Sanctuary: return SuitType.Acolyte;
                case DistrictType.Watchtower: return SuitType.Watcher;
                default: return SuitType.None;
            }
        }

        /// <summary>
        /// Gets the edge weight between two connected nodes.
        /// Returns state.defaultEdgeWeight if no direct edge is found.
        /// Public for View layer access (interpolation).
        /// </summary>
        public static int GetEdgeWeight(SimulationState state, int fromNode, int toNode)
        {
            Edge[] edges = state.nodes[fromNode].edges;
            for (int i = 0; i < edges.Length; i++)
            {
                if (edges[i].toNode == toNode)
                    return edges[i].travelWeight;
            }
            return state.defaultEdgeWeight; // fallback, no direct edge found
        }

        private static int FindMostDamagedFriendly(SimulationState state, int nodeID, int ownerID, int excludeID)
        {
            int bestTarget = -1;
            int mostDamage = 0;
            for (int i = 0; i < state.villagers.Length; i++)
            {
                if (i == excludeID) continue;
                VillagerData v = state.villagers[i];
                if (v.currentNodeID != nodeID) continue;
                if (v.ownerID != ownerID) continue;
                if (v.state == VillagerState.Dead || v.isConsumed) continue;
                if (v.hp >= v.maxHP) continue;
                int damage = v.maxHP - v.hp;
                if (damage > mostDamage) { mostDamage = damage; bestTarget = i; }
            }
            return bestTarget;
        }

        private static bool HasAdjacentFriendlyWatchtowerWorkers(SimulationState state, int nodeIndex, int playerID)
        {
            Edge[] edges = state.nodes[nodeIndex].edges;
            for (int e = 0; e < edges.Length; e++)
            {
                int adjNode = edges[e].toNode;
                if (state.nodes[adjNode].districtType != DistrictType.Watchtower) continue;
                if (state.nodes[adjNode].ownerID != playerID) continue;
                for (int v = 0; v < state.villagers.Length; v++)
                {
                    VillagerData vil = state.villagers[v];
                    if (vil.currentNodeID != adjNode) continue;
                    if (vil.ownerID != playerID) continue;
                    if (vil.state != VillagerState.Working || vil.isConsumed) continue;
                    return true;
                }
            }
            return false;
        }
        private static int CountSanctuaryWorkersForPlayer(SimulationState state, int playerID)
        {
            int count = 0;
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID != playerID) continue;
                if (v.state != VillagerState.Working || v.isConsumed) continue;
                if (state.nodes[v.currentNodeID].districtType != DistrictType.Sanctuary) continue;
                if (state.nodes[v.currentNodeID].ownerID != playerID) continue;
                count++;
            }
            return count;
        }
    }
}