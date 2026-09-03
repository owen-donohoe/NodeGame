namespace NodeWar.Simulation
{
    public static class CommandProcessor
    {
        private static GameBalanceData bal;
        public static void SetBalance(GameBalanceData balance)
        {
            bal = balance;
        }
        public static void ProcessCommand(SimulationState state, GameCommand command)
        {
            switch (command.type)
            {
                case CommandType.Move:
                    ProcessMoveCommand(state, command);
                    break;
                case CommandType.SetAllocation:
                    ProcessSetAllocation(state, command);
                    break;
                case CommandType.Equip:
                    ProcessEquipCommand(state, command);
                    break;
                case CommandType.Respawn:
                    ProcessRespawnCommand(state, command);
                    break;
            }
        }
        /// <summary>
        /// Retargets a villager, honouring the edge it is already on.
        ///
        /// A villager in transit has no position of its own: currentNodeID is the
        /// node it last stood on, and how far it has come is moveProgress against
        /// the leg movePath[movePathIndex] -> movePath[movePathIndex + 1]. Simply
        /// re-pathing from currentNodeID -- what this used to do -- rewound the
        /// villager onto that node and threw the crossing away, which let repeated
        /// orders stall it in place and made turning around free.
        ///
        /// So the leg is kept and only its direction is decided. If the new route
        /// continues through the node being approached, the ground covered still
        /// counts. If it does not, the villager turns around and re-walks exactly
        /// the ground it covered -- expressed as forward travel along the reversed
        /// leg, so the tick loop needs to know nothing about any of this.
        ///
        /// A reversal shows up in state as movePath[movePathIndex] !=
        /// currentNodeID. That is the ONLY case where those two disagree, and it
        /// reads as "between currentNodeID and the node named at movePathIndex,
        /// walking back to currentNodeID".
        /// </summary>
        private static void ProcessMoveCommand(SimulationState state, GameCommand command)
        {
            int vid = command.villagerID;
            if (vid < 0 || vid >= state.villagers.Length) return;

            VillagerData villager = state.villagers[vid];

            if (villager.ownerID != command.playerID) return;
            if (villager.state == VillagerState.Dead) return;
            if (villager.isConsumed) return;

            int destination = command.targetNodeID;
            if (destination < 0 || destination >= state.nodes.Length) return;

            bool onLeg = villager.state == VillagerState.Moving &&
                         villager.movePath != null &&
                         villager.movePathIndex + 1 < villager.movePath.Length;

            if (!onLeg)
            {
                RepathFromNode(state, vid, villager.ownerID, villager.currentNodeID, destination);
                return;
            }

            int legFrom = villager.movePath[villager.movePathIndex];
            int legTo = villager.movePath[villager.movePathIndex + 1];

            int anchor = villager.currentNodeID;
            int otherEnd = (legFrom == anchor) ? legTo : legFrom;

            int legTicks = GetLegTicks(state, legFrom, legTo, villager.moveSpeedTicks);

            // Ticks already spent getting away from the anchor. On a reversal leg
            // progress counts back toward the anchor, so it inverts.
            int covered = (legFrom == anchor) ? villager.moveProgress : legTicks - villager.moveProgress;

            // Still standing on the anchor: nothing crossed, nothing to preserve,
            // and turning around costs nothing.
            if (covered <= 0)
            {
                RepathFromNode(state, vid, villager.ownerID, anchor, destination);
                return;
            }

            // Ordering it back to the node it just left is how a player cancels an
            // order, so it is decided here rather than rejected the way a standing
            // villager order to its own node is.
            if (anchor == destination)
            {
                int cancelTicks = GetLegTicks(state, otherEnd, anchor, villager.moveSpeedTicks);
                ApplyMove(state, vid, new int[] { otherEnd, anchor },
                          cancelTicks - Rescale(covered, legTicks, cancelTicks), destination);
                return;
            }

            int[] path = Pathfinding.FindPath(state, villager.ownerID, anchor, destination);
            if (path.Length < 2) return;

            if (path[1] == otherEnd)
            {
                // The new route runs on through the node already being approached.
                // Keep crossing; only what comes after it changes.
                int aheadTicks = GetLegTicks(state, anchor, otherEnd, villager.moveSpeedTicks);
                ApplyMove(state, vid, path, Rescale(covered, legTicks, aheadTicks), destination);
                return;
            }

            // The new route leaves the anchor in another direction. Walk the
            // crossing back first: prepending the abandoned node makes the return
            // an ordinary forward leg as far as TickMovement is concerned.
            int[] reversed = new int[path.Length + 1];
            reversed[0] = otherEnd;
            for (int i = 0; i < path.Length; i++) reversed[i + 1] = path[i];

            int returnTicks = GetLegTicks(state, otherEnd, anchor, villager.moveSpeedTicks);
            ApplyMove(state, vid, reversed,
                      returnTicks - Rescale(covered, legTicks, returnTicks), destination);
        }

        /// <summary>
        /// The plain case: a villager standing on a node, ordered somewhere else.
        /// </summary>
        private static void RepathFromNode(SimulationState state, int villagerIndex,
                                           int ownerID, int fromNode, int destination)
        {
            if (fromNode == destination) return;

            int[] path = Pathfinding.FindPath(state, ownerID, fromNode, destination);
            if (path.Length < 2) return;

            ApplyMove(state, villagerIndex, path, 0, destination);
        }

        private static void ApplyMove(SimulationState state, int villagerIndex,
                                      int[] path, int progress, int destination)
        {
            state.villagers[villagerIndex].movePath = path;
            state.villagers[villagerIndex].movePathIndex = 0;
            state.villagers[villagerIndex].moveProgress = progress;
            state.villagers[villagerIndex].targetNodeID = destination;
            state.villagers[villagerIndex].state = VillagerState.Moving;
            state.villagers[villagerIndex].combatTargetID = -1;
        }

        /// <summary>
        /// Ticks needed to cross a leg. Never zero -- progress against a zero-tick
        /// leg would be meaningless, and the division in Rescale would fault.
        /// </summary>
        private static int GetLegTicks(SimulationState state, int fromNode, int toNode, int moveSpeedTicks)
        {
            int ticks = GameSimulation.GetEdgeWeight(state, fromNode, toNode) * moveSpeedTicks;
            return ticks < 1 ? 1 : ticks;
        }

        /// <summary>
        /// Carries a tick count from one leg clock onto another. Integer only,
        /// multiplying before dividing so the rounding is the same on both peers.
        ///
        /// Every authored board uses a single uniform edge weight, so the two
        /// clocks match and this returns covered untouched. It exists so that a
        /// board with mixed weights cannot quietly gain or lose ground at a turn.
        /// </summary>
        private static int Rescale(int covered, int fromTicks, int toTicks)
        {
            if (fromTicks == toTicks) return covered;
            if (fromTicks < 1) return 0;

            int scaled = (covered * toTicks) / fromTicks;
            if (scaled < 0) scaled = 0;
            if (scaled > toTicks) scaled = toTicks;
            return scaled;
        }

        private static void ProcessSetAllocation(SimulationState state, GameCommand command)
        {
            int nodeID = command.targetNodeID;
            if (nodeID < 0 || nodeID >= state.nodes.Length) return;
            if (state.nodes[nodeID].ownerID != command.playerID) return;
            if (state.nodes[nodeID].districtType != DistrictType.Forge) return;
            if (command.value < 0) return;

            state.nodes[nodeID].materialAllocation = command.value;
        }

        private static void ProcessEquipCommand(SimulationState state, GameCommand command)
        {
            int vid = command.villagerID;
            if (vid < 0 || vid >= state.villagers.Length) return;
            VillagerData villager = state.villagers[vid];
            if (villager.ownerID != command.playerID) return;
            if (villager.state == VillagerState.Dead) return;
            if (villager.isConsumed) return;
            if (villager.state != VillagerState.Idle) return;
            if (GameBalanceData.IsCombatSuit(villager.suit)) return;
            SuitType requestedSuit = (SuitType)command.value;
            if (!GameBalanceData.IsCombatSuit(requestedSuit)) return;
            int nodeID = villager.currentNodeID;
            if (state.nodes[nodeID].ownerID != command.playerID) return;
            if (!bal.CanEquipSuitAtNode(requestedSuit, state.nodes[nodeID].districtType)) return;
            if (!PlayerHasSuitDrafted(state, command.playerID, requestedSuit)) return;
            SuitStats stats = bal.GetSuitStats(requestedSuit);
            if (state.players[command.playerID].food < stats.foodCost) return;
            if (state.players[command.playerID].materials < stats.materialCost) return;
            // Apply costs
            state.players[command.playerID].food -= stats.foodCost;
            state.players[command.playerID].materials -= stats.materialCost;
            // Apply suit
            state.villagers[vid].suit = requestedSuit;
            state.villagers[vid].attackDamage = stats.attackDamage;
            state.villagers[vid].moveSpeedTicks = stats.moveSpeedTicks;
            state.villagers[vid].attackCooldownMax = stats.attackCooldownMax;
            state.villagers[vid].attackCooldownRemaining = stats.attackCooldownMax;
            state.villagers[vid].fightPriority = stats.fightPriority;
            // Apply HP (baseHP + bonusHP, accounting for Rampart if present)
            int newMaxHP = bal.baseHP + stats.bonusHP;
            if (villager.hasRampartBonus) newMaxHP += bal.rampartMaxHPBonus;
            state.villagers[vid].maxHP = newMaxHP;
            state.villagers[vid].hp = newMaxHP;
        }
        private static void ProcessRespawnCommand(SimulationState state, GameCommand command)
        {
            int vid = command.villagerID;
            if (vid < 0 || vid >= state.villagers.Length) return;
            VillagerData villager = state.villagers[vid];
            if (villager.ownerID != command.playerID) return;
            if (villager.state != VillagerState.Dead) return;
            if (villager.isConsumed) return;
            // Sanctuary cost reduction
            int baseCost = bal.respawnCostFood;
            int sanctuaryWorkers = CountSanctuaryWorkers(state, command.playerID);
            int reductionPercent = bal.sanctuaryRespawnCostReductionPercent * sanctuaryWorkers;
            int reduction = (baseCost * reductionPercent) / 100;
            int finalCost = baseCost - reduction;
            if (finalCost < 1) finalCost = 1;
            if (state.players[command.playerID].food < finalCost) return;
            // Apply
            state.players[command.playerID].food -= finalCost;
            int coreNode = state.players[command.playerID].coreNodeID;
            state.villagers[vid].state = VillagerState.Idle;
            state.villagers[vid].currentNodeID = coreNode;
            state.villagers[vid].previousNodeID = coreNode;
            state.villagers[vid].targetNodeID = -1;
            state.villagers[vid].movePath = new int[0];
            state.villagers[vid].movePathIndex = 0;
            state.villagers[vid].moveProgress = 0;
            state.villagers[vid].hp = bal.baseHP;
            state.villagers[vid].maxHP = bal.baseHP;
            state.villagers[vid].suit = SuitType.None;
            state.villagers[vid].attackDamage = bal.baseAttackDamage;
            state.villagers[vid].moveSpeedTicks = bal.baseMoveSpeedTicks;
            state.villagers[vid].attackCooldownMax = bal.baseAttackCooldownMax;
            state.villagers[vid].attackCooldownRemaining = bal.baseAttackCooldownMax;
            state.villagers[vid].combatTargetID = -1;
            state.villagers[vid].fightPriority = 0;
            state.villagers[vid].respawnTicksRemaining = 0;
            state.villagers[vid].productionTicksRemaining = 0;
            state.villagers[vid].productionTicksMax = 0;
            state.villagers[vid].hasRampartBonus = false;
        }

        private static bool PlayerHasSuitDrafted(SimulationState state, int playerID, SuitType suit)
        {
            int[] drafted = state.players[playerID].draftedSuits;
            if (drafted == null) return false;
            for (int i = 0; i < drafted.Length; i++)
            {
                if (drafted[i] == (int)suit) return true;
            }
            return false;
        }

        private static int CountSanctuaryWorkers(SimulationState state, int playerID)
        {
            int count = 0;
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID != playerID) continue;
                if (v.state != VillagerState.Working) continue;
                if (v.isConsumed) continue;
                if (state.nodes[v.currentNodeID].districtType != DistrictType.Sanctuary) continue;
                if (state.nodes[v.currentNodeID].ownerID != playerID) continue;
                count++;
            }
            return count;
        }
    }
}