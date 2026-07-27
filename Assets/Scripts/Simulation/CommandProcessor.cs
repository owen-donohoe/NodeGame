namespace NodeWar.Simulation
{
    public static class CommandProcessor
    {
        /// <summary>
        /// Processes a move command: computes BFS path and sets villager into Moving state.
        /// </summary>
        public static void ProcessCommand(SimulationState state, GameCommand command)
        {
            if (command.type != CommandType.Move) return;

            // Validate ownership
            VillagerData villager = state.villagers[command.villagerID];
            if (villager.ownerID != command.playerID) return;
            if (villager.state == VillagerState.Dead) return;

            // Can't move to the node you're already on
            if (villager.currentNodeID == command.targetNodeID) return;

            // Find path
            int[] path = Pathfinding.FindPath(state, villager.currentNodeID, command.targetNodeID);
            if (path.Length < 2) return; // No valid path

            // Apply to state
            state.villagers[command.villagerID].movePath = path;
            state.villagers[command.villagerID].movePathIndex = 0;
            state.villagers[command.villagerID].targetNodeID = command.targetNodeID;
            state.villagers[command.villagerID].moveProgress = 0;
            state.villagers[command.villagerID].state = VillagerState.Moving;
        }
    }
}