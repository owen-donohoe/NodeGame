namespace NodeWar.Simulation
{
    public static class CommandProcessor
    {
        private const int SOLDIER_COST_FOOD = 2;
        private const int SOLDIER_COST_MATERIAL = 1;
        private const int RESPAWN_COST_FOOD = 1;

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

            int[] path = Pathfinding.FindPath(state, villager.currentNodeID, command.targetNodeID);
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
            if (state.players[command.playerID].food < SOLDIER_COST_FOOD) return;
            if (state.players[command.playerID].materials < SOLDIER_COST_MATERIAL) return;

            // All valid — apply
            state.players[command.playerID].food -= SOLDIER_COST_FOOD;
            state.players[command.playerID].materials -= SOLDIER_COST_MATERIAL;

            state.villagers[vid].suit = SuitType.Soldier;
            state.villagers[vid].attackDamage = 2;
            state.villagers[vid].moveSpeedTicks = 5;
            state.villagers[vid].attackCooldownMax = 10;
            state.villagers[vid].attackCooldownRemaining = 10;
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
            if (state.players[command.playerID].food < RESPAWN_COST_FOOD) return;

            // All valid — apply
            state.players[command.playerID].food -= RESPAWN_COST_FOOD;

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
            state.villagers[vid].attackDamage = 1;
            state.villagers[vid].moveSpeedTicks = 4;
            state.villagers[vid].attackCooldownMax = 20;
            state.villagers[vid].attackCooldownRemaining = 20;
            state.villagers[vid].combatTargetID = -1;
            state.villagers[vid].respawnTicksRemaining = 0;
            state.villagers[vid].productionTicksRemaining = 0;
            state.villagers[vid].productionTicksMax = 0;
        }
    }
}