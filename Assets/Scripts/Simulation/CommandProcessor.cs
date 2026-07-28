namespace NodeWar.Simulation
{
    public static class CommandProcessor
    {
        public static void ProcessCommand(SimulationState state, GameCommand command)
        {
            if (command.type != CommandType.Move) return;

            // Validate ownership
            VillagerData villager = state.villagers[command.villagerID];
            if (villager.ownerID != command.playerID) return;
            if (villager.state == VillagerState.Dead) return;
            if (villager.isConsumed) return;

            // Can't move to the node you're already on
            if (villager.currentNodeID == command.targetNodeID) return;

            // Find path
            int[] path = Pathfinding.FindPath(state, villager.currentNodeID, command.targetNodeID);
            if (path.Length < 2) return;

            // Apply to state — this also allows re-pathing a Fighting villager
            // (interrupts current fight, starts moving toward new target)
            state.villagers[command.villagerID].movePath = path;
            state.villagers[command.villagerID].movePathIndex = 0;
            state.villagers[command.villagerID].targetNodeID = command.targetNodeID;
            state.villagers[command.villagerID].moveProgress = 0;
            state.villagers[command.villagerID].state = VillagerState.Moving;
            state.villagers[command.villagerID].combatTargetID = -1;
        }
    }
}