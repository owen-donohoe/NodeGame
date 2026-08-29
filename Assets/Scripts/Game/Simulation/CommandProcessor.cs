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
        private static void ProcessMoveCommand(SimulationState state, GameCommand command)
        {
            VillagerData villager = state.villagers[command.villagerID];

            if (villager.ownerID != command.playerID) return;
            if (villager.state == VillagerState.Dead) return;
            if (villager.isConsumed) return;
            if (villager.currentNodeID == command.targetNodeID) return;

            int[] path = Pathfinding.FindPath(state, villager.ownerID, villager.currentNodeID, command.targetNodeID);

            if (path.Length < 2) return;

            state.villagers[command.villagerID].movePath = path;
            state.villagers[command.villagerID].movePathIndex = 0;
            state.villagers[command.villagerID].targetNodeID = command.targetNodeID;
            state.villagers[command.villagerID].moveProgress = 0;
            state.villagers[command.villagerID].state = VillagerState.Moving;
            state.villagers[command.villagerID].combatTargetID = -1;
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