using NodeWar.Simulation;
using System.Collections.Generic;

namespace NodeWar.Input
{
    /// <summary>
    /// Heuristic bot. Reads SimulationState each tick, issues commands via InputBuffer.
    /// Priority-based evaluation with per-villager command cooldowns to prevent oscillation.
    /// 
    /// Priority order:
    /// 0. Core emergency (enemies ON core — everyone responds)
    /// 1. Core intercept (enemies heading toward core — soldiers intercept)
    /// 2. Respawn dead villagers
    /// 3. Node defense (enemies ON owned farm/mine/barracks)
    /// 4. Expansion (village ? farm ? mine ? barracks ? second village)
    /// 5. Military (equip at barracks, attack in wolfpacks of 3)
    /// 6. Economy management (fill/reduce workers based on resource levels)
    /// </summary>
    public class BotPlayer
    {
        private SimulationState state;
        private InputBuffer inputBuffer;
        private int playerID;
        private int enemyID;

        // Per-villager command cooldown (tick when they can next be commanded)
        private int[] commandCooldownUntil;

        // Per-tick scratch
        private bool[] claimedThisTick;

        // Balance references (read once, used for cooldown calculation)
        private int defaultEdgeWeight;

        public BotPlayer(SimulationState state, InputBuffer buffer, int playerID, int defaultEdgeWeight)
        {
            this.state = state;
            this.inputBuffer = buffer;
            this.playerID = playerID;
            this.enemyID = 1 - playerID;
            this.defaultEdgeWeight = defaultEdgeWeight;
        }

        public void Evaluate()
        {
            if (state.gameOver) return;

            // Resize arrays if villager count grew
            if (claimedThisTick == null || claimedThisTick.Length < state.villagers.Length)
                claimedThisTick = new bool[state.villagers.Length];
            if (commandCooldownUntil == null || commandCooldownUntil.Length < state.villagers.Length)
            {
                int[] newCooldowns = new int[state.villagers.Length];
                if (commandCooldownUntil != null)
                {
                    for (int i = 0; i < commandCooldownUntil.Length; i++)
                        newCooldowns[i] = commandCooldownUntil[i];
                }
                commandCooldownUntil = newCooldowns;
            }

            for (int i = 0; i < claimedThisTick.Length; i++)
                claimedThisTick[i] = false;

            CoreEmergency();
            CoreIntercept();
            RespawnDead();
            NodeDefense();
            Expansion();
            Military();
            EconomyManagement();
        }

        // ===== PRIORITY 0: CORE EMERGENCY (enemies ON core) =====

        private void CoreEmergency()
        {
            int myCoreNode = state.players[playerID].coreNodeID;

            int enemiesOnCore = 0;
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID != enemyID) continue;
                if (v.state == VillagerState.Dead || v.isConsumed) continue;
                if (v.currentNodeID == myCoreNode)
                    enemiesOnCore++;
            }

            if (enemiesOnCore == 0) return;

            // Emergency: override ALL cooldowns, send everyone to core
            List<int> candidates = GetAllLivingVillagers();

            for (int i = 0; i < candidates.Count; i++)
            {
                int vid = candidates[i];
                VillagerData v = state.villagers[vid];

                // Already on core
                if (v.currentNodeID == myCoreNode && v.state != VillagerState.Moving) continue;
                // Already heading there
                if (v.targetNodeID == myCoreNode) continue;

                IssueMoveForce(vid, myCoreNode); // Force = ignores cooldown
            }
        }

        // ===== PRIORITY 1: CORE INTERCEPT (enemies heading toward core) =====

        private void CoreIntercept()
        {
            int myCoreNode = state.players[playerID].coreNodeID;

            int incomingThreats = 0;
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID != enemyID) continue;
                if (v.state == VillagerState.Dead || v.isConsumed) continue;
                if (v.currentNodeID == myCoreNode) continue; // handled by Emergency

                if (v.targetNodeID == myCoreNode)
                {
                    incomingThreats++;
                    continue;
                }

                // Check if within 3 edges of core
                int dist = PathLength(v.currentNodeID, myCoreNode);
                if (dist >= 0 && dist <= 3)
                    incomingThreats++;
            }

            if (incomingThreats == 0) return;

            // Send soldiers to core to intercept (prefer soldiers)
            int toSend = incomingThreats + 1;
            List<int> soldiers = GetAvailableSoldiers();
            List<int> others = GetAvailableNonSoldiers(false);

            // Sort both by distance to core
            soldiers.Sort((a, b) => PathCost(state.villagers[a].currentNodeID, myCoreNode)
                .CompareTo(PathCost(state.villagers[b].currentNodeID, myCoreNode)));
            others.Sort((a, b) => PathCost(state.villagers[a].currentNodeID, myCoreNode)
                .CompareTo(PathCost(state.villagers[b].currentNodeID, myCoreNode)));

            // Send soldiers first
            for (int i = 0; i < soldiers.Count && toSend > 0; i++)
            {
                int vid = soldiers[i];
                VillagerData v = state.villagers[vid];
                if (v.currentNodeID == myCoreNode && v.state != VillagerState.Moving) continue;
                if (v.targetNodeID == myCoreNode) continue;

                IssueMove(vid, myCoreNode);
                toSend--;
            }

            // Then non-soldiers if still needed
            for (int i = 0; i < others.Count && toSend > 0; i++)
            {
                int vid = others[i];
                VillagerData v = state.villagers[vid];
                if (v.currentNodeID == myCoreNode && v.state != VillagerState.Moving) continue;
                if (v.targetNodeID == myCoreNode) continue;

                IssueMove(vid, myCoreNode);
                toSend--;
            }
        }

        // ===== PRIORITY 2: RESPAWN =====

        private void RespawnDead()
        {
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID != playerID) continue;
                if (v.state != VillagerState.Dead) continue;
                if (v.isConsumed) continue;

                if (state.players[playerID].food < 1) return;

                GameCommand cmd = new GameCommand
                {
                    type = CommandType.Respawn,
                    playerID = playerID,
                    villagerID = i,
                    issuedOnTick = state.tickCount
                };
                inputBuffer.EnqueueCommand(cmd);
            }
        }

        // ===== PRIORITY 3: NODE DEFENSE (enemies ON owned farm/mine/barracks) =====

        private void NodeDefense()
        {
            for (int n = 0; n < state.nodes.Length; n++)
            {
                NodeData node = state.nodes[n];
                if (node.ownerID != playerID) continue;
                if (node.districtType == DistrictType.Core) continue; // handled above

                // Only defend production and military nodes
                if (node.districtType != DistrictType.Farm &&
                    node.districtType != DistrictType.Mine &&
                    node.districtType != DistrictType.Barracks)
                    continue;

                int enemyCount = CountEnemiesOnNode(n);
                if (enemyCount == 0) continue;

                int toSend = enemyCount + 1;

                // Prefer soldiers
                List<int> soldiers = GetAvailableSoldiers();
                soldiers.Sort((a, b) => PathCost(state.villagers[a].currentNodeID, n)
                    .CompareTo(PathCost(state.villagers[b].currentNodeID, n)));

                for (int i = 0; i < soldiers.Count && toSend > 0; i++)
                {
                    int vid = soldiers[i];
                    if (state.villagers[vid].currentNodeID == n) continue;
                    if (state.villagers[vid].targetNodeID == n) continue;
                    IssueMove(vid, n);
                    toSend--;
                }

                // Then non-soldiers
                if (toSend > 0)
                {
                    List<int> others = GetAvailableNonSoldiers(false);
                    others.Sort((a, b) => PathCost(state.villagers[a].currentNodeID, n)
                        .CompareTo(PathCost(state.villagers[b].currentNodeID, n)));

                    for (int i = 0; i < others.Count && toSend > 0; i++)
                    {
                        int vid = others[i];
                        if (state.villagers[vid].currentNodeID == n) continue;
                        if (state.villagers[vid].targetNodeID == n) continue;
                        IssueMove(vid, n);
                        toSend--;
                    }
                }
            }
        }

        // ===== PRIORITY 4: EXPANSION =====

        private void Expansion()
        {
            int maxClaimers = 4; // matches simulation MAX_CLAIMERS_PER_NODE

            // Order: Village ? Farm ? Mine ? Barracks ? second Village
            // For each type: if we don't own enough, send max claimers to the closest unowned

            int ownedVillages = CountOwnedOfType(DistrictType.Village);
            int ownedFarms = CountOwnedOfType(DistrictType.Farm);
            int ownedMines = CountOwnedOfType(DistrictType.Mine);
            int ownedBarracks = CountOwnedOfType(DistrictType.Barracks);

            if (ownedVillages < 1)
            {
                SendClaimersToClosestUnowned(DistrictType.Village, maxClaimers);
                return;
            }

            if (ownedFarms < 1)
            {
                SendClaimersToClosestUnowned(DistrictType.Farm, maxClaimers);
                return;
            }

            if (ownedMines < 1)
            {
                SendClaimersToClosestUnowned(DistrictType.Mine, maxClaimers);
                return;
            }

            if (ownedBarracks < 1)
            {
                SendClaimersToClosestUnowned(DistrictType.Barracks, maxClaimers);
                return;
            }

            // Second village
            if (ownedVillages < 2)
            {
                SendClaimersToClosestUnowned(DistrictType.Village, maxClaimers);
                return;
            }

            // Second farm
            if (ownedFarms < 2)
            {
                SendClaimersToClosestUnowned(DistrictType.Farm, maxClaimers);
                return;
            }

            // Second mine
            if (ownedMines < 2)
            {
                SendClaimersToClosestUnowned(DistrictType.Mine, maxClaimers);
                return;
            }
        }

        // ===== PRIORITY 5: MILITARY =====

        private void Military()
        {
            if (!OwnsNodeOfType(DistrictType.Barracks)) return;

            // Equip idle villagers on owned barracks
            EquipAtBarracks();

            // Wolfpack: attack enemy core when we have 3+ soldiers with no pending commands
            AttackWithWolfpack(3);
        }

        private void EquipAtBarracks()
        {
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID != playerID) continue;
                if (v.state != VillagerState.Idle) continue;
                if (v.suit == SuitType.Soldier) continue;
                if (v.isConsumed) continue;
                if (claimedThisTick[i]) continue;

                int nodeID = v.currentNodeID;
                if (state.nodes[nodeID].districtType != DistrictType.Barracks) continue;
                if (state.nodes[nodeID].ownerID != playerID) continue;

                if (state.players[playerID].food < 2 || state.players[playerID].materials < 1)
                    break;

                GameCommand cmd = new GameCommand
                {
                    type = CommandType.Equip,
                    playerID = playerID,
                    villagerID = i,
                    issuedOnTick = state.tickCount
                };
                inputBuffer.EnqueueCommand(cmd);
                claimedThisTick[i] = true;
            }
        }

        private void AttackWithWolfpack(int packSize)
        {
            int enemyCore = state.players[enemyID].coreNodeID;

            // Count available soldiers (idle or on cooldown but not heading somewhere critical)
            List<int> readySoldiers = new List<int>();
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID != playerID) continue;
                if (v.suit != SuitType.Soldier) continue;
                if (v.state == VillagerState.Dead || v.isConsumed) continue;
                if (v.state == VillagerState.Fighting) continue;
                if (claimedThisTick[i]) continue;

                // Already heading to enemy core counts toward the pack
                if (v.targetNodeID == enemyCore)
                {
                    readySoldiers.Add(i);
                    continue;
                }

                // Idle or available soldiers
                if (v.state == VillagerState.Idle || IsOffCooldown(i))
                    readySoldiers.Add(i);
            }

            if (readySoldiers.Count < packSize) return;

            // Send all ready soldiers to enemy core
            for (int i = 0; i < readySoldiers.Count; i++)
            {
                int vid = readySoldiers[i];
                if (state.villagers[vid].targetNodeID == enemyCore) continue;
                if (state.villagers[vid].currentNodeID == enemyCore) continue;
                IssueMove(vid, enemyCore);
            }
        }

        // ===== PRIORITY 6: ECONOMY MANAGEMENT =====

        private void EconomyManagement()
        {
            ManageProduction(DistrictType.Farm, state.players[playerID].food, true);
            ManageProduction(DistrictType.Mine, state.players[playerID].materials, false);
        }

        /// <summary>
        /// Manages worker count on all owned nodes of a type based on resource level.
        /// alwaysKeepOne: if true, never pull the last worker (farms).
        /// </summary>
        private void ManageProduction(DistrictType type, int currentResource, bool alwaysKeepOne)
        {
            for (int n = 0; n < state.nodes.Length; n++)
            {
                NodeData node = state.nodes[n];
                if (node.districtType != type) continue;
                if (node.ownerID != playerID) continue;

                int workers = CountFriendlyWorkersOnNode(n);

                // Determine desired worker count
                int desiredWorkers;
                if (currentResource >= 20)
                    desiredWorkers = alwaysKeepOne ? 1 : 0;
                else if (currentResource >= 10)
                    desiredWorkers = 1;
                else
                    desiredWorkers = 2;

                if (alwaysKeepOne && desiredWorkers < 1)
                    desiredWorkers = 1;

                if (workers > desiredWorkers)
                {
                    // Pull excess workers — send to barracks if owned, else core
                    int toRemove = workers - desiredWorkers;
                    for (int i = 0; i < state.villagers.Length && toRemove > 0; i++)
                    {
                        VillagerData v = state.villagers[i];
                        if (v.ownerID != playerID) continue;
                        if (v.currentNodeID != n) continue;
                        if (v.state != VillagerState.Working) continue;
                        if (v.isConsumed) continue;
                        if (claimedThisTick[i]) continue;

                        int destination = FindClosestOwnedNodeOfType(DistrictType.Barracks, n);
                        if (destination < 0)
                            destination = state.players[playerID].coreNodeID;

                        IssueMove(i, destination);
                        toRemove--;
                    }
                }
                else if (workers < desiredWorkers)
                {
                    int needed = desiredWorkers - workers;
                    int inbound = CountVillagersHeadingTo(n);
                    needed -= inbound;

                    for (int s = 0; s < needed; s++)
                    {
                        int villager = GetClosestFreeNonSoldierTo(n);
                        if (villager < 0) break;
                        IssueMove(villager, n);
                    }
                }
            }
        }

        // ===== COMMAND ISSUANCE =====

        /// <summary>
        /// Issues a move command respecting cooldown. Does nothing if on cooldown.
        /// </summary>
        private void IssueMove(int villagerID, int targetNode)
        {
            if (claimedThisTick[villagerID]) return;
            if (!IsOffCooldown(villagerID)) return;

            VillagerData v = state.villagers[villagerID];
            if (v.currentNodeID == targetNode && v.state != VillagerState.Moving) return;
            if (v.targetNodeID == targetNode) return;

            GameCommand cmd = new GameCommand
            {
                type = CommandType.Move,
                playerID = playerID,
                villagerID = villagerID,
                targetNodeID = targetNode,
                issuedOnTick = state.tickCount
            };
            inputBuffer.EnqueueCommand(cmd);
            claimedThisTick[villagerID] = true;

            // Set cooldown: 1 tick more than crossing one edge
            int cooldownTicks = defaultEdgeWeight * v.moveSpeedTicks + 1;
            commandCooldownUntil[villagerID] = state.tickCount + cooldownTicks;
        }

        /// <summary>
        /// Issues a move command IGNORING cooldown. Used for core emergency only.
        /// </summary>
        private void IssueMoveForce(int villagerID, int targetNode)
        {
            if (claimedThisTick[villagerID]) return;

            VillagerData v = state.villagers[villagerID];
            if (v.currentNodeID == targetNode && v.state != VillagerState.Moving) return;
            if (v.targetNodeID == targetNode) return;

            GameCommand cmd = new GameCommand
            {
                type = CommandType.Move,
                playerID = playerID,
                villagerID = villagerID,
                targetNodeID = targetNode,
                issuedOnTick = state.tickCount
            };
            inputBuffer.EnqueueCommand(cmd);
            claimedThisTick[villagerID] = true;

            int cooldownTicks = defaultEdgeWeight * v.moveSpeedTicks + 1;
            commandCooldownUntil[villagerID] = state.tickCount + cooldownTicks;
        }

        private bool IsOffCooldown(int villagerID)
        {
            if (villagerID >= commandCooldownUntil.Length) return true;
            return state.tickCount >= commandCooldownUntil[villagerID];
        }

        // ===== EXPANSION HELPERS =====

        private void SendClaimersToClosestUnowned(DistrictType type, int maxToSend)
        {
            int coreNode = state.players[playerID].coreNodeID;
            int targetNode = FindClosestUnownedNodeOfType(type, coreNode);
            if (targetNode < 0) return;

            // Don't send villagers already heading there
            int alreadyHeading = CountVillagersHeadingTo(targetNode);
            int toSend = maxToSend - alreadyHeading;
            if (toSend <= 0) return;

            for (int s = 0; s < toSend; s++)
            {
                int villager = GetClosestFreeNonSoldierTo(targetNode);
                if (villager < 0) break;
                IssueMove(villager, targetNode);
            }
        }

        // ===== QUERY HELPERS =====

        private List<int> GetAllLivingVillagers()
        {
            List<int> result = new List<int>();
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID != playerID) continue;
                if (v.state == VillagerState.Dead || v.isConsumed) continue;
                result.Add(i);
            }
            return result;
        }

        private List<int> GetAvailableSoldiers()
        {
            List<int> result = new List<int>();
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID != playerID) continue;
                if (v.suit != SuitType.Soldier) continue;
                if (v.state == VillagerState.Dead || v.isConsumed) continue;
                if (v.state == VillagerState.Fighting) continue;
                if (claimedThisTick[i]) continue;
                result.Add(i);
            }
            return result;
        }

        private List<int> GetAvailableNonSoldiers(bool includeWorking)
        {
            List<int> result = new List<int>();
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID != playerID) continue;
                if (v.suit == SuitType.Soldier) continue;
                if (v.state == VillagerState.Dead || v.isConsumed) continue;
                if (v.state == VillagerState.Fighting) continue;
                if (!includeWorking && v.state == VillagerState.Working) continue;
                if (claimedThisTick[i]) continue;
                if (!IsOffCooldown(i)) continue;
                result.Add(i);
            }
            return result;
        }

        /// <summary>
        /// Closest idle non-soldier villager to a target. -1 if none.
        /// </summary>
        private int GetClosestFreeNonSoldierTo(int targetNode)
        {
            int bestID = -1;
            int bestCost = int.MaxValue;

            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID != playerID) continue;
                if (v.suit == SuitType.Soldier) continue;
                if (v.state != VillagerState.Idle) continue;
                if (v.isConsumed) continue;
                if (claimedThisTick[i]) continue;
                if (!IsOffCooldown(i)) continue;

                int cost = PathCost(v.currentNodeID, targetNode);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestID = i;
                }
            }
            return bestID;
        }

        private bool OwnsNodeOfType(DistrictType type)
        {
            for (int i = 0; i < state.nodes.Length; i++)
            {
                if (state.nodes[i].districtType == type && state.nodes[i].ownerID == playerID)
                    return true;
            }
            return false;
        }

        private int CountOwnedOfType(DistrictType type)
        {
            int count = 0;
            for (int i = 0; i < state.nodes.Length; i++)
            {
                if (state.nodes[i].districtType == type && state.nodes[i].ownerID == playerID)
                    count++;
            }
            return count;
        }

        private int CountEnemiesOnNode(int nodeID)
        {
            int count = 0;
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID == playerID) continue;
                if (v.currentNodeID != nodeID) continue;
                if (v.state == VillagerState.Dead || v.isConsumed) continue;
                count++;
            }
            return count;
        }

        private int CountFriendlyWorkersOnNode(int nodeID)
        {
            int count = 0;
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.currentNodeID != nodeID) continue;
                if (v.ownerID != playerID) continue;
                if (v.state != VillagerState.Working) continue;
                if (v.isConsumed) continue;
                count++;
            }
            return count;
        }

        private int CountVillagersHeadingTo(int nodeID)
        {
            int count = 0;
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID != playerID) continue;
                if (v.state != VillagerState.Moving) continue;
                if (v.isConsumed) continue;
                if (v.targetNodeID == nodeID) count++;
            }
            return count;
        }

        private int FindClosestUnownedNodeOfType(DistrictType type, int fromNode)
        {
            int bestNode = -1;
            int bestCost = int.MaxValue;

            for (int i = 0; i < state.nodes.Length; i++)
            {
                if (state.nodes[i].districtType != type) continue;
                if (state.nodes[i].ownerID == playerID) continue;

                int cost = PathCost(fromNode, i);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestNode = i;
                }
            }
            return bestNode;
        }

        private int FindClosestOwnedNodeOfType(DistrictType type, int fromNode)
        {
            int bestNode = -1;
            int bestCost = int.MaxValue;

            for (int i = 0; i < state.nodes.Length; i++)
            {
                if (state.nodes[i].districtType != type) continue;
                if (state.nodes[i].ownerID != playerID) continue;

                int cost = PathCost(fromNode, i);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestNode = i;
                }
            }
            return bestNode;
        }

        private int PathCost(int fromNode, int toNode)
        {
            if (fromNode == toNode) return 0;
            int[] path = Pathfinding.FindPath(state, playerID, fromNode, toNode);
            if (path.Length < 2) return int.MaxValue;

            int cost = 0;
            for (int i = 0; i < path.Length - 1; i++)
                cost += GameSimulation.GetEdgeWeight(state, path[i], path[i + 1]);
            return cost;
        }

        private int PathLength(int fromNode, int toNode)
        {
            if (fromNode == toNode) return 0;
            int[] path = Pathfinding.FindPath(state, playerID, fromNode, toNode);
            if (path.Length < 2) return -1;
            return path.Length - 1;
        }
    }
}