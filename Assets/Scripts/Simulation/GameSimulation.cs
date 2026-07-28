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
        private const int CLAIM_THRESHOLD = 10000;
        private const int MAX_CLAIMERS_PER_NODE = 4;
        private const int RESPAWN_TICKS = 50;
        private const int HEAL_INTERVAL_TICKS = 30;
        private const int BREACH_THRESHOLD = 3;

        // ===== MAIN TICK =====

        /// <summary>
        /// Advances the simulation by one tick. Called at fixed rate (10hz).
        /// Tick order:
        /// 1. Commands (handled by TickRunner before this call)
        /// 2. Movement (with combat interruption and breach-on-arrival)
        /// 3. Combat (detect fights, process cooldowns, deal damage, handle deaths)
        /// 4. Claim bars
        /// 5. Healing (every 30 ticks)
        /// 6. Respawn timers
        /// 7. Win condition (breachCount >= 3)
        /// 8. Post-combat resume (fight ended, determine next state)
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

            // Step 5: Healing
            TickHealing(state);

            // Step 6: Respawns
            TickRespawns(state);

            // Step 7: Win condition
            TickWinCondition(state);

            // Step 8: Post-combat resume
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

            if (v.moveProgress >= v.moveSpeedTicks)
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
                        // Defenders present: combat triggers, path preserved
                        v.state = VillagerState.Fighting;
                        v.moveProgress = 0;
                        v.attackCooldownRemaining = v.attackCooldownMax;
                        v.combatTargetID = -1;
                        state.villagers[villagerIndex] = v;
                        return;
                    }
                    else
                    {
                        // No defenders: breach occurs immediately
                        ProcessBreach(state, villagerIndex, v);
                        return;
                    }
                }

                if (enemiesPresent)
                {
                    // Combat interruption: enemies on this node, fight them
                    // Path is preserved (not cleared) so movement can resume after combat
                    v.state = VillagerState.Fighting;
                    v.moveProgress = 0;
                    v.attackCooldownRemaining = v.attackCooldownMax;
                    v.combatTargetID = -1;
                    state.villagers[villagerIndex] = v;
                    return;
                }

                // --- Normal arrival logic (no enemies) ---
                // Reached end of path?
                if (v.movePathIndex >= v.movePath.Length - 1)
                {
                    v.movePath = new int[0];
                    v.movePathIndex = 0;
                    v.moveProgress = 0;
                    v.targetNodeID = -1;
                    v.state = DetermineArrivalState(state, v);
                }
                // else: keep Moving along path
            }

            state.villagers[villagerIndex] = v;
        }

        /// <summary>
        /// Determines what state a villager enters upon arriving at a node (end of path).
        /// Priority: Fighting > Claiming (with max cap) > Idle
        /// </summary>
        private static VillagerState DetermineArrivalState(SimulationState state, VillagerData villager)
        {
            int nodeID = villager.currentNodeID;
            NodeData node = state.nodes[nodeID];

            // Core nodes: always Idle (breach is handled separately in movement)
            if (node.districtType == DistrictType.Core)
            {
                return VillagerState.Idle;
            }

            // Own node -> Idle
            if (node.ownerID == villager.ownerID)
            {
                return VillagerState.Idle;
            }

            // Neutral or enemy-owned: check MAX_CLAIMERS_PER_NODE
            int friendlyClaimers = CountFriendlyClaimersOnNode(state, nodeID, villager.ownerID);
            if (friendlyClaimers < MAX_CLAIMERS_PER_NODE)
            {
                return VillagerState.Claiming;
            }

            // At max claimers: Idle (can't help claim)
            return VillagerState.Idle;
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

        private static void UpdateVillagerClaimStates(SimulationState state)
        {
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];

                if (v.state != VillagerState.Idle && v.state != VillagerState.Claiming) continue;
                if (v.isConsumed) continue;

                NodeData node = state.nodes[v.currentNodeID];

                if (node.districtType == DistrictType.Core)
                {
                    if (v.state != VillagerState.Idle)
                        state.villagers[i].state = VillagerState.Idle;
                    continue;
                }

                if (node.ownerID == v.ownerID)
                {
                    if (v.state != VillagerState.Idle)
                        state.villagers[i].state = VillagerState.Idle;
                }
                else
                {
                    // Check MAX_CLAIMERS_PER_NODE before setting to Claiming
                    if (v.state != VillagerState.Claiming)
                    {
                        int friendlyClaimers = CountFriendlyClaimersOnNode(state, v.currentNodeID, v.ownerID);
                        if (friendlyClaimers < MAX_CLAIMERS_PER_NODE)
                        {
                            state.villagers[i].state = VillagerState.Claiming;
                        }
                        // else stay Idle (at max claimers)
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
                    isConsumed = false
                };
            }

            state.villagers = newArray;
        }

        // ===== STEP 5: HEALING =====

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

        // ===== STEP 7: WIN CONDITION =====

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

        // ===== STEP 8: POST-COMBAT RESUME =====

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
                        // Own node: resume movement
                        state.villagers[i].state = VillagerState.Moving;
                        state.villagers[i].combatTargetID = -1;
                    }
                    else if (currentNode.districtType == DistrictType.Core)
                    {
                        // On a core node (shouldn't be enemy core, handled above): resume
                        state.villagers[i].state = VillagerState.Moving;
                        state.villagers[i].combatTargetID = -1;
                    }
                    else
                    {
                        // Not our node: check if we should help claim or keep moving
                        int friendlyClaimers = CountFriendlyClaimersOnNode(state, v.currentNodeID, v.ownerID);
                        if (friendlyClaimers < MAX_CLAIMERS_PER_NODE)
                        {
                            // Stop and help claim
                            state.villagers[i].state = VillagerState.Claiming;
                            state.villagers[i].combatTargetID = -1;
                        }
                        else
                        {
                            // At max claimers, keep moving
                            state.villagers[i].state = VillagerState.Moving;
                            state.villagers[i].combatTargetID = -1;
                        }
                    }
                }
                else
                {
                    // No remaining path: use standard arrival logic
                    state.villagers[i].state = DetermineArrivalState(state, v);
                    state.villagers[i].combatTargetID = -1;
                    state.villagers[i].movePath = new int[0];
                    state.villagers[i].movePathIndex = 0;
                    state.villagers[i].targetNodeID = -1;
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
    }
}