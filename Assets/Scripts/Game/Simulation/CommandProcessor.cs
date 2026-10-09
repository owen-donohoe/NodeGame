namespace NodeWar.Simulation
{
    public static class CommandProcessor
    {
        private static GameBalanceData bal;
        public static void SetBalance(GameBalanceData balance)
        {
            bal = balance;
        }
        /// <summary>
        /// Validates and applies one command. log, when given, is the same
        /// TickEventLog the tick about to run will append to; only a paid
        /// respawn writes to it from here.
        /// </summary>
        public static void ProcessCommand(SimulationState state, GameCommand command, TickEventLog log = null)
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
                    ProcessRespawnCommand(state, command, log);
                    break;
                case CommandType.Recruit:
                    ProcessRecruit(state, command);
                    break;
                case CommandType.InstallMinion:
                    ProcessInstallMinion(state, command);
                    break;
                case CommandType.Collect:
                    ProcessCollect(state, command);
                    break;
                case CommandType.UpgradeFortress:
                    ProcessUpgradeFortress(state, command);
                    break;
                case CommandType.SetAutoRecruit:
                    ProcessSetAutoRecruit(state, command);
                    break;
            }
        }
        private static void ProcessCollect(SimulationState state, GameCommand command)
        {
            if (!BankRules.CanCollect(state, command)) return;
            int nodeID = command.targetNodeID;
            state.nodes[nodeID].collectRequested = command.value == 1;
            if (command.value == 0 && !BankRules.HasStationaryCollector(state, state.nodes[nodeID]))
                state.nodes[nodeID].collectProgress = 0;
        }

        private static void ProcessInstallMinion(SimulationState state, GameCommand command)
        {
            if (!BankRules.CanInstallMinion(state, bal, command)) return;
            int nodeID = command.targetNodeID;
            state.players[command.playerID].metal -= bal.minionMetalCost;
            state.nodes[nodeID] = BankRules.CreateMinion(state.nodes[nodeID], bal);
            BankRules.DemoteSurplusWorkers(state, nodeID, bal);
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
            if (command.playerID < 0 || command.playerID >= state.players.Length) return;
            int vid = command.villagerID;
            if (vid < 0 || vid >= state.villagers.Length) return;

            VillagerData villager = state.villagers[vid];

            if (villager.ownerID != command.playerID) return;
            if (villager.state == VillagerState.Dead) return;
            if (villager.isConsumed) return;

            int destination = command.targetNodeID;
            if (destination < 0 || destination >= state.nodes.Length) return;

            // Orders change intent during combat, never the attack clock or fight state.
            if (villager.state == VillagerState.Fighting)
            {
                state.villagers[vid].targetNodeID = destination == villager.currentNodeID ? -1 : destination;
                return;
            }

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

            int legTicks = GameSimulation.GetMoveLegDurationTicks(state, villager);

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
                int cancelTicks = villager.moveLegDurationTicks > 0 ? legTicks : GetLegTicks(state, villager.ownerID, otherEnd, anchor, villager.moveSpeedTicks);
                ApplyMove(state, vid, new int[] { otherEnd, anchor },
                          cancelTicks - (villager.moveLegDurationTicks > 0 ? covered : Rescale(covered, legTicks, cancelTicks)), destination);
                return;
            }

            int[] path = Pathfinding.FindPath(state, villager.ownerID, anchor, destination, villager.moveSpeedTicks);
            if (path.Length < 2)
            {
                // Pay the return crossing before waiting for an unreachable destination.
                int backTicks = villager.moveLegDurationTicks > 0 ? legTicks : GetLegTicks(state, villager.ownerID, otherEnd, anchor, villager.moveSpeedTicks);
                ApplyMove(state, vid, new int[] { otherEnd, anchor },
                          backTicks - (villager.moveLegDurationTicks > 0 ? covered : Rescale(covered, legTicks, backTicks)), destination);
                return;
            }

            if (path[1] == otherEnd)
            {
                // The new route runs on through the node already being approached.
                // Keep crossing; only what comes after it changes.
                int aheadTicks = villager.moveLegDurationTicks > 0 ? legTicks : GetLegTicks(state, villager.ownerID, anchor, otherEnd, villager.moveSpeedTicks);
                ApplyMove(state, vid, path, (villager.moveLegDurationTicks > 0 ? covered : Rescale(covered, legTicks, aheadTicks)), destination);
                return;
            }

            // The new route leaves the anchor in another direction. Walk the
            // crossing back first: prepending the abandoned node makes the return
            // an ordinary forward leg as far as TickMovement is concerned.
            int[] reversed = new int[path.Length + 1];
            reversed[0] = otherEnd;
            for (int i = 0; i < path.Length; i++) reversed[i + 1] = path[i];

            int returnTicks = villager.moveLegDurationTicks > 0 ? legTicks : GetLegTicks(state, villager.ownerID, otherEnd, anchor, villager.moveSpeedTicks);
            ApplyMove(state, vid, reversed,
                      returnTicks - (villager.moveLegDurationTicks > 0 ? covered : Rescale(covered, legTicks, returnTicks)), destination);
        }

        /// <summary>
        /// The plain case: a villager standing on a node, ordered somewhere else.
        /// </summary>
        private static void RepathFromNode(SimulationState state, int villagerIndex,
                                           int ownerID, int fromNode, int destination)
        {
            state.villagers[villagerIndex].targetNodeID = fromNode == destination ? -1 : destination;
            state.villagers[villagerIndex].movePath = new int[0];
            state.villagers[villagerIndex].movePathIndex = 0;
            GameSimulation.ClearLeg(ref state.villagers[villagerIndex]);
            state.villagers[villagerIndex].combatTargetID = -1;
            state.villagers[villagerIndex].state = VillagerState.Idle;
            if (fromNode == destination)
            {
                GameSimulation.ApplyArrivalState(state, villagerIndex);
                return;
            }

            int[] path = Pathfinding.FindPath(state, ownerID, fromNode, destination, state.villagers[villagerIndex].moveSpeedTicks);
            if (path.Length < 2) return;

            ApplyMove(state, villagerIndex, path, 0, destination);
        }

        private static void ApplyMove(SimulationState state, int villagerIndex,
                                      int[] path, int progress, int destination)
        {
            state.villagers[villagerIndex].targetNodeID = destination;
            if (PierGate.BlocksDeparture(state, state.villagers[villagerIndex], path[1])) return;
            state.villagers[villagerIndex].movePath = path;
            state.villagers[villagerIndex].movePathIndex = 0;
            state.villagers[villagerIndex].moveProgress = progress;
            state.villagers[villagerIndex].targetNodeID = destination;
            if (state.villagers[villagerIndex].moveLegDurationTicks == 0)
                GameSimulation.BeginLeg(state, ref state.villagers[villagerIndex]);
            state.villagers[villagerIndex].state = VillagerState.Moving;
            state.villagers[villagerIndex].combatTargetID = -1;
        }

        /// <summary>
        /// Ticks needed to cross a leg. Never zero -- progress against a zero-tick
        /// leg would be meaningless, and the division in Rescale would fault.
        /// </summary>
        private static int GetLegTicks(SimulationState state, int ownerID, int fromNode, int toNode, int moveSpeedTicks)
        {
            return GameSimulation.CalculateLegTicks(state, ownerID, fromNode, toNode, moveSpeedTicks);
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

        private static void ProcessUpgradeFortress(SimulationState state, GameCommand command)
        {
            if (!NodeActionRules.CanUpgradeFortress(state, bal, command.playerID, command.targetNodeID,
                command.value, command.villagerID)) return;
            NodeData node = state.nodes[command.targetNodeID];
            DistrictStats stats = bal.GetDistrictStats(DistrictType.Fortress, node.districtEra);
            int next = node.fortressLevel + 1;
            if (command.value == 0) state.players[command.playerID].materials -= stats.fortressMaterialsCosts[next];
            else state.players[command.playerID].metal -= stats.fortressMetalCosts[next];
            state.nodes[command.targetNodeID].fortressLevel = next;
            // Historical all-zero tuning has no structure mechanism: no HP-less Fortification.
            if (next == 1 && bal.StructureTuningValid())
            {
                state.nodes[command.targetNodeID].structureKind = StructureKind.Fortification;
                state.nodes[command.targetNodeID].structureHP = bal.GetDistrictStats(DistrictType.Fortress, state.nodes[command.targetNodeID].districtEra).fortificationHP;
            }
        }

        private static void ProcessRecruit(SimulationState state, GameCommand command)
        {
            if (command.villagerID != -1 || command.value != 0) return;
            TryRecruit(state, command.playerID, command.targetNodeID);
        }

        private static void ProcessSetAutoRecruit(SimulationState state, GameCommand command)
        {
            if (command.villagerID != -1 ||
                !NodeActionRules.CanSetAutoRecruit(state, command.playerID, command.targetNodeID, command.value)) return;
            state.nodes[command.targetNodeID].autoRecruit = command.value == 1;
        }

        internal static bool TryRecruit(SimulationState state, int playerID, int nodeID)
        {
            if (!NodeActionRules.CanRecruit(state, bal, playerID, nodeID, out _)) return false;
            if (!bal.TryRecruitCostAndCooldown(state.players[playerID].recruitCount, state.tickCount,
                out int cost, out int readyTick)) return false;
            state.players[playerID].food -= cost;
            GameSimulation.SpawnBonusVillagers(state, nodeID, playerID, 1);
            state.players[playerID].recruitCount++;
            state.nodes[nodeID].recruitReadyTick = readyTick;
            return true;
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
            int suitEra = state.players[command.playerID].SuitEra(requestedSuit);
            if (!bal.TryGetSuitStats(requestedSuit, suitEra, out SuitStats stats)) return;
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
            // Apply civilian HP plus the equipped suit bonus.
            int newMaxHP = bal.baseHP + stats.bonusHP;

            state.villagers[vid].maxHP = newMaxHP;
            state.villagers[vid].hp = newMaxHP;
        }
        public static int GetRespawnCost(SimulationState state, int playerID, int additionalPaidRespawns = 0)
        {
            int paidRespawns = (int)System.Math.Min(int.MaxValue,
                (long)state.players[playerID].paidRespawns + additionalPaidRespawns);
            int reductionPercent = InfirmaryCostReductionPercent(state, playerID);
            return bal.PaidRespawnCost(paidRespawns, reductionPercent);
        }

        private static void ProcessRespawnCommand(SimulationState state, GameCommand command, TickEventLog log)
        {
            int vid = command.villagerID;
            if (vid < 0 || vid >= state.villagers.Length) return;
            VillagerData villager = state.villagers[vid];
            if (villager.ownerID != command.playerID) return;
            if (villager.state != VillagerState.Dead) return;
            if (villager.isConsumed) return;
            int finalCost = GetRespawnCost(state, command.playerID);
            if (state.players[command.playerID].food < finalCost) return;
            // Apply
            state.players[command.playerID].food -= finalCost;
            if (state.players[command.playerID].paidRespawns < int.MaxValue)
                state.players[command.playerID].paidRespawns++;
            GameSimulation.ResetToCore(state, vid, bal, log, paid: true);
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

        /// <summary>
        /// Each working Infirmary worker takes its Infirmary era's percentage off
        /// the respawn cost; the percentages add.
        /// </summary>
        private static int InfirmaryCostReductionPercent(SimulationState state, int playerID)
        {
            int percent = 0;
            for (int node = 0; node < state.nodes.Length; node++)
                percent += GameSimulation.CountInfirmaryWorkers(state, node, playerID) *
                    bal.GetDistrictStats(DistrictType.Infirmary, state.nodes[node].districtEra).respawnCostReductionPercent;
            return percent;
        }
    }
}
