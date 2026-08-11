using NodeWar.Simulation;
using System.Collections.Generic;

namespace NodeWar.Input
{
    /// <summary>
    /// Heuristic bot that reads SimulationState each tick and issues commands
    /// via InputBuffer. Runs only in local mode via TickRunner.
    /// Evaluates priorities top-to-bottom, marking villagers as claimed
    /// so lower priorities don't double-assign.
    /// </summary>
    public class BotPlayer
    {
        private SimulationState state;
        private InputBuffer inputBuffer;
        private int playerID;
        private int enemyID;

        // Per-tick scratch state (reset each Evaluate call)
        private bool[] claimedThisTick;

        public BotPlayer(SimulationState state, InputBuffer buffer, int playerID)
        {
            this.state = state;
            this.inputBuffer = buffer;
            this.playerID = playerID;
            this.enemyID = 1 - playerID;
        }

        public void Evaluate()
        {
            if (state.gameOver) return;

            // Resize claimed array if villager count grew
            if (claimedThisTick == null || claimedThisTick.Length < state.villagers.Length)
                claimedThisTick = new bool[state.villagers.Length];

            for (int i = 0; i < claimedThisTick.Length; i++)
                claimedThisTick[i] = false;

            CoreDefense();
            RespawnDead();
            ResourceEmergency();
            NodeDefense();
            Expansion();
            Military();
            EconomyFill();
        }

        // ===== PRIORITY 0: CORE DEFENSE =====

        private void CoreDefense()
        {
            int myCoreNode = state.players[playerID].coreNodeID;

            // Count enemies on core or heading to core
            int threatCount = 0;
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID != enemyID) continue;
                if (v.state == VillagerState.Dead || v.isConsumed) continue;

                if (v.currentNodeID == myCoreNode || v.targetNodeID == myCoreNode)
                    threatCount++;
            }

            if (threatCount == 0) return;

            // Send threatCount + 1 closest villagers to core (any non-dead unclaimed)
            int toSend = threatCount + 1;
            List<int> candidates = GetAvailableVillagers(true, true); // include moving + working

            // Remove villagers already on or heading to core
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                VillagerData v = state.villagers[candidates[i]];
                if (v.currentNodeID == myCoreNode && v.state != VillagerState.Moving)
                    candidates.RemoveAt(i);
                else if (v.targetNodeID == myCoreNode)
                    candidates.RemoveAt(i);
            }

            // Sort by path distance to core
            candidates.Sort((a, b) => PathCost(state.villagers[a].currentNodeID, myCoreNode)
                .CompareTo(PathCost(state.villagers[b].currentNodeID, myCoreNode)));

            for (int i = 0; i < candidates.Count && toSend > 0; i++)
            {
                IssueMove(candidates[i], myCoreNode);
                toSend--;
            }
        }

        // ===== PRIORITY 1: RESPAWN =====

        private void RespawnDead()
        {
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID != playerID) continue;
                if (v.state != VillagerState.Dead) continue;
                if (v.isConsumed) continue;

                if (state.players[playerID].food < 1) return; // can't afford any more

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

        // ===== PRIORITY 2: RESOURCE EMERGENCY =====

        private void ResourceEmergency()
        {
            // Only activate after owning at least one farm and one mine
            if (!OwnsNodeOfType(DistrictType.Farm) || !OwnsNodeOfType(DistrictType.Mine))
                return;

            // Food emergency
            if (state.players[playerID].food < 10 && !HasWorkerOnOwnedType(DistrictType.Farm))
            {
                int farmNode = FindClosestOwnedNodeOfType(DistrictType.Farm, state.players[playerID].coreNodeID);
                if (farmNode >= 0)
                {
                    int villager = GetClosestFreeVillagerTo(farmNode);
                    if (villager >= 0)
                        IssueMove(villager, farmNode);
                }
            }

            // Material emergency
            if (state.players[playerID].materials < 10 && !HasWorkerOnOwnedType(DistrictType.Mine))
            {
                int mineNode = FindClosestOwnedNodeOfType(DistrictType.Mine, state.players[playerID].coreNodeID);
                if (mineNode >= 0)
                {
                    int villager = GetClosestFreeVillagerTo(mineNode);
                    if (villager >= 0)
                        IssueMove(villager, mineNode);
                }
            }
        }

        // ===== PRIORITY 3: NODE DEFENSE =====

        private void NodeDefense()
        {
            // Find owned nodes with enemies present
            for (int n = 0; n < state.nodes.Length; n++)
            {
                NodeData node = state.nodes[n];
                if (node.ownerID != playerID) continue;
                if (node.districtType == DistrictType.Core) continue; // core handled by Priority 0

                if (!HasEnemiesOnNode(n)) continue;

                // Send up to 2 available villagers within 3 edges
                int sent = 0;
                List<int> candidates = GetAvailableVillagers(false, false);

                // Sort by distance to threatened node
                candidates.Sort((a, b) => PathCost(state.villagers[a].currentNodeID, n)
                    .CompareTo(PathCost(state.villagers[b].currentNodeID, n)));

                for (int i = 0; i < candidates.Count && sent < 2; i++)
                {
                    int vid = candidates[i];
                    int pathLen = PathLength(state.villagers[vid].currentNodeID, n);
                    if (pathLen < 0 || pathLen > 3) continue;

                    // Already there or heading there
                    if (state.villagers[vid].currentNodeID == n) continue;
                    if (state.villagers[vid].targetNodeID == n) continue;

                    // Prefer soldiers
                    IssueMove(vid, n);
                    sent++;
                }
            }
        }

        // ===== PRIORITY 4: EXPANSION =====

        private void Expansion()
        {
            int coreNode = state.players[playerID].coreNodeID;

            // Claim farm if we don't own one
            if (!OwnsNodeOfType(DistrictType.Farm))
            {
                SendFreesToClosestUnownedType(DistrictType.Farm, 2);
                return;
            }

            // Claim mine if we don't own one
            if (!OwnsNodeOfType(DistrictType.Mine))
            {
                SendFreesToClosestUnownedType(DistrictType.Mine, 2);
                return;
            }

            // Claim village if we don't own one
            if (!OwnsNodeOfType(DistrictType.Village))
            {
                SendFreesToClosestUnownedType(DistrictType.Village, 2);
                return;
            }

            // Claim barracks if we don't own one
            if (!OwnsNodeOfType(DistrictType.Barracks))
            {
                SendFreesToClosestUnownedType(DistrictType.Barracks, 2);
                return;
            }
        }

        // ===== PRIORITY 5: MILITARY =====

        private void Military()
        {
            if (!OwnsNodeOfType(DistrictType.Barracks)) return;

            // Equip idle villagers on owned barracks
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

            // Send soldiers to enemy core
            int enemyCore = state.players[enemyID].coreNodeID;
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID != playerID) continue;
                if (v.suit != SuitType.Soldier) continue;
                if (v.state == VillagerState.Dead || v.isConsumed) continue;
                if (claimedThisTick[i]) continue;

                // Skip if already heading to enemy core
                if (v.targetNodeID == enemyCore) continue;
                // Skip if currently fighting (let combat resolve)
                if (v.state == VillagerState.Fighting) continue;

                IssueMove(i, enemyCore);
            }
        }

        // ===== PRIORITY 6: ECONOMY FILL =====

        private void EconomyFill()
        {
            // Fill owned farms to 2 workers
            FillProductionNodes(DistrictType.Farm);
            // Fill owned mines to 2 workers
            FillProductionNodes(DistrictType.Mine);
        }

        private void FillProductionNodes(DistrictType type)
        {
            for (int n = 0; n < state.nodes.Length; n++)
            {
                NodeData node = state.nodes[n];
                if (node.districtType != type) continue;
                if (node.ownerID != playerID) continue;

                int workers = CountFriendlyWorkersOnNode(n);
                int needed = 2 - workers;
                if (needed <= 0) continue;

                // Count villagers already heading there
                int inbound = CountVillagersHeadingTo(n);
                needed -= inbound;
                if (needed <= 0) continue;

                for (int s = 0; s < needed; s++)
                {
                    int villager = GetClosestFreeVillagerTo(n);
                    if (villager < 0) break;
                    IssueMove(villager, n);
                }
            }
        }

        // ===== HELPER METHODS =====

        private void IssueMove(int villagerID, int targetNode)
        {
            if (claimedThisTick[villagerID]) return;

            VillagerData v = state.villagers[villagerID];

            // Don't issue if already there
            if (v.currentNodeID == targetNode && v.state != VillagerState.Moving) return;
            // Don't re-issue if already heading there
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
        }

        /// <summary>
        /// Returns villager IDs that are alive, owned by bot, unclaimed this tick.
        /// includeMoving: include villagers currently in Moving state.
        /// includeWorking: include villagers currently in Working state.
        /// </summary>
        private List<int> GetAvailableVillagers(bool includeMoving, bool includeWorking)
        {
            List<int> result = new List<int>();
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID != playerID) continue;
                if (v.state == VillagerState.Dead || v.isConsumed) continue;
                if (claimedThisTick[i]) continue;

                if (v.state == VillagerState.Fighting) continue; // never pull from fights
                if (v.state == VillagerState.Moving && !includeMoving) continue;
                if (v.state == VillagerState.Working && !includeWorking) continue;

                result.Add(i);
            }
            return result;
        }

        /// <summary>
        /// Returns the closest free (idle, unclaimed) villager to a target node. -1 if none.
        /// </summary>
        private int GetClosestFreeVillagerTo(int targetNode)
        {
            int bestID = -1;
            int bestCost = int.MaxValue;

            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID != playerID) continue;
                if (v.state != VillagerState.Idle) continue;
                if (v.isConsumed) continue;
                if (claimedThisTick[i]) continue;

                int cost = PathCost(v.currentNodeID, targetNode);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestID = i;
                }
            }

            return bestID;
        }

        private void SendFreesToClosestUnownedType(DistrictType type, int count)
        {
            int coreNode = state.players[playerID].coreNodeID;
            int targetNode = FindClosestUnownedNodeOfType(type, coreNode);
            if (targetNode < 0) return;

            for (int s = 0; s < count; s++)
            {
                int villager = GetClosestFreeVillagerTo(targetNode);
                if (villager < 0) break;
                IssueMove(villager, targetNode);
            }
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

        private bool HasWorkerOnOwnedType(DistrictType type)
        {
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID != playerID) continue;
                if (v.state != VillagerState.Working) continue;
                if (v.isConsumed) continue;

                int nodeID = v.currentNodeID;
                if (state.nodes[nodeID].districtType == type && state.nodes[nodeID].ownerID == playerID)
                    return true;
            }
            return false;
        }

        private bool HasEnemiesOnNode(int nodeID)
        {
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID == playerID) continue;
                if (v.currentNodeID != nodeID) continue;
                if (v.state == VillagerState.Dead || v.isConsumed) continue;
                return true;
            }
            return false;
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

        /// <summary>
        /// Returns total weighted path cost from Pathfinding. Returns int.MaxValue if unreachable.
        /// </summary>
        private int PathCost(int fromNode, int toNode)
        {
            if (fromNode == toNode) return 0;
            int[] path = Pathfinding.FindPath(state, playerID, fromNode, toNode);
            if (path.Length < 2) return int.MaxValue;

            int cost = 0;
            for (int i = 0; i < path.Length - 1; i++)
            {
                cost += GameSimulation.GetEdgeWeight(state, path[i], path[i + 1]);
            }
            return cost;
        }

        /// <summary>
        /// Returns number of edges in the path (path.Length - 1). -1 if unreachable.
        /// </summary>
        private int PathLength(int fromNode, int toNode)
        {
            if (fromNode == toNode) return 0;
            int[] path = Pathfinding.FindPath(state, playerID, fromNode, toNode);
            if (path.Length < 2) return -1;
            return path.Length - 1;
        }

        /// <summary>
        /// Finds the closest node of a given type NOT owned by this player.
        /// Returns -1 if none found.
        /// </summary>
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

        /// <summary>
        /// Finds the closest node of a given type owned by this player.
        /// Returns -1 if none found.
        /// </summary>
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
    }
}