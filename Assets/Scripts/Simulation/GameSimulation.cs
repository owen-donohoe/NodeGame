using System.Collections.Generic;
using System.Diagnostics;
using NodeWar.Simulation;
using UnityEngine;

namespace NodeWar.Simulation
{
    public static class GameSimulation
    {
        // ===== CONSTANTS =====
        private const int BASE_CLAIM_PER_TICK = 17;
        private const int DECREMENT_MULTIPLIER = 4;
        private const int CLAIM_THRESHOLD = 10000;//this is public for pathfinding currently. this might need to change.
        private const int MAX_CLAIMERS_PER_NODE = 4;
        private const int RESPAWN_TICKS = 50;
        private const int HEAL_INTERVAL_TICKS = 30;
        private const int BREACH_THRESHOLD = 3;

        private const int FOOD_PRODUCTION_TICKS = 30;
        private const int MATERIAL_PRODUCTION_TICKS = 40;
        private const int METAL_PRODUCTION_TICKS = 50;
        private const int MAX_WORKERS_PER_NODE = 2;
        private const int MAX_VILLAGERS_PER_PLAYER = 25;

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

            v.moveProgress++;

            // Calculate ticks needed for current edge
            int edgeWeight = 3; // default
            if (v.movePathIndex + 1 < v.movePath.Length)
            {
                edgeWeight = GetEdgeWeight(state, v.movePath[v.movePathIndex], v.movePath[v.movePathIndex + 1]);
            }
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
                if (v.suit != SuitType.Soldier)
                {
                    state.villagers[villagerIndex].suit = SuitType.None;
                    state.villagers[villagerIndex].attackDamage = 1;
                    state.villagers[villagerIndex].moveSpeedTicks = 4;
                    state.villagers[villagerIndex].attackCooldownMax = 20;
                }
                state.villagers[villagerIndex].state = VillagerState.Idle;
                return;
            }

            // Own node
            if (node.ownerID == v.ownerID)
            {
                // Soldiers never auto-assign suit, just go Idle
                if (v.suit == SuitType.Soldier)
                {
                    state.villagers[villagerIndex].state = VillagerState.Idle;
                    return;
                }

                if (node.districtType == DistrictType.Farm)
                {
                    state.villagers[villagerIndex].suit = SuitType.Farmer;
                    int workers = CountFriendlyWorkersOnNode(state, nodeID, v.ownerID);
                    if (workers < MAX_WORKERS_PER_NODE)
                    {
                        state.villagers[villagerIndex].productionTicksMax = FOOD_PRODUCTION_TICKS;
                        state.villagers[villagerIndex].productionTicksRemaining = FOOD_PRODUCTION_TICKS;
                        state.villagers[villagerIndex].state = VillagerState.Working;
                    }
                    else
                    {
                        state.villagers[villagerIndex].state = VillagerState.Idle;
                    }
                    return;
                }

                if (node.districtType == DistrictType.Mine)
                {
                    state.villagers[villagerIndex].suit = SuitType.Miner;
                    int workers = CountFriendlyWorkersOnNode(state, nodeID, v.ownerID);
                    if (workers < MAX_WORKERS_PER_NODE)
                    {
                        state.villagers[villagerIndex].productionTicksMax = MATERIAL_PRODUCTION_TICKS;
                        state.villagers[villagerIndex].productionTicksRemaining = MATERIAL_PRODUCTION_TICKS;
                        state.villagers[villagerIndex].state = VillagerState.Working;
                    }
                    else
                    {
                        state.villagers[villagerIndex].state = VillagerState.Idle;
                    }
                    return;
                }

                if (node.districtType == DistrictType.Forge)
                {
                    state.villagers[villagerIndex].suit = SuitType.Smelter;
                    int workers = CountFriendlyWorkersOnNode(state, nodeID, v.ownerID);
                    if (workers < MAX_WORKERS_PER_NODE)
                    {
                        state.villagers[villagerIndex].productionTicksMax = METAL_PRODUCTION_TICKS;
                        state.villagers[villagerIndex].productionTicksRemaining = METAL_PRODUCTION_TICKS;
                        state.villagers[villagerIndex].state = VillagerState.Working;
                    }
                    else
                    {
                        state.villagers[villagerIndex].state = VillagerState.Idle;
                    }
                    return;
                }

                // Barracks, Village, None: just Idle, no suit change
                state.villagers[villagerIndex].state = VillagerState.Idle;
                return;
            }

            // Not own node: Claiming logic
            int friendlyClaimers = CountFriendlyClaimersOnNode(state, nodeID, v.ownerID);
            if (friendlyClaimers < MAX_CLAIMERS_PER_NODE)
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
                    // Deal damage to target
                    int targetID = state.villagers[v].combatTargetID;
                    if (targetID >= 0 && targetID < state.villagers.Length)
                    {
                        if (state.villagers[targetID].state != VillagerState.Dead &&
                            !state.villagers[targetID].isConsumed)
                        {
                            state.villagers[targetID].hp -= state.villagers[v].attackDamage;
                        }
                    }

                    // Reset cooldown
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
                    state.villagers[v].respawnTicksRemaining = RESPAWN_TICKS;
                    state.villagers[v].movePath = new int[0];
                    state.villagers[v].movePathIndex = 0;
                    state.villagers[v].moveProgress = 0;
                    state.villagers[v].targetNodeID = -1;
                    state.villagers[v].combatTargetID = -1;
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
                    int rate;
                    if (node.claimBar < 0)
                    {
                        rate = DECREMENT_MULTIPLIER * BASE_CLAIM_PER_TICK * p0Claimers;
                    }
                    else
                    {
                        rate = BASE_CLAIM_PER_TICK * p0Claimers;
                    }

                    node.claimBar += rate;

                    if (node.claimBar >= CLAIM_THRESHOLD)
                    {
                        node.claimBar = CLAIM_THRESHOLD;
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
                    int rate;
                    if (node.claimBar > 0)
                    {
                        rate = DECREMENT_MULTIPLIER * BASE_CLAIM_PER_TICK * p1Claimers;
                    }
                    else
                    {
                        rate = BASE_CLAIM_PER_TICK * p1Claimers;
                    }

                    node.claimBar -= rate;

                    if (node.claimBar <= -CLAIM_THRESHOLD)
                    {
                        node.claimBar = -CLAIM_THRESHOLD;
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
                    if (v.suit == SuitType.Soldier)
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
                            if (workers < MAX_WORKERS_PER_NODE)
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
                    if (friendlyClaimers < MAX_CLAIMERS_PER_NODE)
                    {
                        state.villagers[idx].state = VillagerState.Claiming;
                    }
                }
            }
        }

        private static void CompleteClaimForPlayer(SimulationState state, int nodeIndex, int playerID)
        {
            state.nodes[nodeIndex].ownerID = playerID;

            int bonus = state.nodes[nodeIndex].bonusVillagersOnClaim;
            if (bonus > 0)
            {
                SpawnBonusVillagers(state, nodeIndex, playerID, bonus);
            }
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
            int maxAllowed = MAX_VILLAGERS_PER_PLAYER - playerVillagerCount;
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

            state.villagers = newArray;
        }

        // ===== STEP 6 =: HEALING =====

        /// <summary>
        /// Every 30 ticks (3 seconds), heal all damaged non-fighting, non-dead villagers by 1 HP.
        /// Simple approach: no per-villager heal timer needed.
        /// </summary>
        private static void TickHealing(SimulationState state)
        {
            if (state.tickCount % HEAL_INTERVAL_TICKS != 0) return;

            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.isConsumed) continue;
                if (v.state == VillagerState.Dead) continue;
                if (v.state == VillagerState.Fighting) continue;
                if (v.hp >= v.maxHP) continue;

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

                state.villagers[i].respawnTicksRemaining--;

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
                    state.villagers[i].attackDamage = 1;
                    state.villagers[i].moveSpeedTicks = 4;
                    state.villagers[i].attackCooldownMax = 20;
                    state.villagers[i].attackCooldownRemaining = 20;
                    state.villagers[i].combatTargetID = -1;
                    state.villagers[i].respawnTicksRemaining = 0;
                }
            }
        }

        // ===== STEP 8: WIN CONDITION =====

        private static void TickWinCondition(SimulationState state)
        {
            for (int p = 0; p < state.players.Length; p++)
            {
                if (state.players[p].breachCount >= BREACH_THRESHOLD)
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
                        if (v.suit != SuitType.Soldier && v.suit == GetExpectedSuit(currentNode.districtType))
                        {
                            int workers = CountFriendlyWorkersOnNode(state, v.currentNodeID, v.ownerID);
                            if (workers < MAX_WORKERS_PER_NODE)
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
                        if (friendlyClaimers < MAX_CLAIMERS_PER_NODE)
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
                case DistrictType.Farm: return FOOD_PRODUCTION_TICKS;
                case DistrictType.Mine: return MATERIAL_PRODUCTION_TICKS;
                case DistrictType.Forge: return METAL_PRODUCTION_TICKS;
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
                default: return SuitType.None;
            }
        }

        /// <summary>
        /// Gets the edge weight between two connected nodes.
        /// Returns default weight of 3 if not found.
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
            return 3; // fallback, no direct edge found
        }
    }
}