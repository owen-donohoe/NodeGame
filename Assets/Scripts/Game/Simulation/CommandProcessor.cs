namespace NodeWar.Simulation
{
    public static class CommandProcessor
    {
        private static GameBalance bal;

        public static void SetBalance(GameBalance balance)
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

            // Ownership check
            if (villager.ownerID != command.playerID) return;

            // Must be alive and not consumed
            if (villager.state == VillagerState.Dead) return;
            if (villager.isConsumed) return;

            // Must be Idle (not moving, fighting, working, claiming)
            if (villager.state != VillagerState.Idle) return;

            // Already a soldier — can't equip again
            if (villager.suit == SuitType.Soldier) return;

            // Must be on a Barracks owned by this player
            int nodeID = villager.currentNodeID;
            if (state.nodes[nodeID].districtType != DistrictType.Barracks) return;
            if (state.nodes[nodeID].ownerID != command.playerID) return;

            // Resource check
            if (state.players[command.playerID].food < bal.soldierCostFood) return;
            if (state.players[command.playerID].materials < bal.soldierCostMaterial) return;

            // All valid - apply
            state.players[command.playerID].food -= bal.soldierCostFood;
            state.players[command.playerID].materials -= bal.soldierCostMaterial;

            state.villagers[vid].suit = SuitType.Soldier;
            state.villagers[vid].attackDamage = bal.soldierAttackDamage;
            state.villagers[vid].moveSpeedTicks = bal.soldierMoveSpeedTicks;
            state.villagers[vid].attackCooldownMax = bal.soldierAttackCooldownMax;
            state.villagers[vid].attackCooldownRemaining = bal.soldierAttackCooldownMax;
        }

        private static void ProcessRespawnCommand(SimulationState state, GameCommand command)
        {
            int vid = command.villagerID;
            if (vid < 0 || vid >= state.villagers.Length) return;

            VillagerData villager = state.villagers[vid];

            // Ownership check
            if (villager.ownerID != command.playerID) return;

            // Must be dead
            if (villager.state != VillagerState.Dead) return;

            // Cannot respawn consumed villagers
            if (villager.isConsumed) return;

            // Resource check
            if (state.players[command.playerID].food < bal.respawnCostFood) return;

            // All valid - apply
            state.players[command.playerID].food -= bal.respawnCostFood;

            int coreNode = state.players[command.playerID].coreNodeID;

            state.villagers[vid].state = VillagerState.Idle;
            state.villagers[vid].currentNodeID = coreNode;
            state.villagers[vid].previousNodeID = coreNode;
            state.villagers[vid].targetNodeID = -1;
            state.villagers[vid].movePath = new int[0];
            state.villagers[vid].movePathIndex = 0;
            state.villagers[vid].moveProgress = 0;
            state.villagers[vid].hp = state.villagers[vid].maxHP;
            state.villagers[vid].suit = SuitType.None;
            state.villagers[vid].attackDamage = bal.baseAttackDamage;
            state.villagers[vid].moveSpeedTicks = bal.baseMoveSpeedTicks;
            state.villagers[vid].attackCooldownMax = bal.baseAttackCooldownMax;
            state.villagers[vid].attackCooldownRemaining = bal.baseAttackCooldownMax;
            state.villagers[vid].combatTargetID = -1;
            state.villagers[vid].respawnTicksRemaining = 0;
            state.villagers[vid].productionTicksRemaining = 0;
            state.villagers[vid].productionTicksMax = 0;
        }
    }
}