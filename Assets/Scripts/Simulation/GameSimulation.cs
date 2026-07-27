namespace NodeWar.Simulation
{
    public static class GameSimulation
    {
        /// <summary>
        /// Advances the simulation by one tick. Called at fixed rate (10hz).
        /// Phase 2: only handles movement.
        /// </summary>
        public static void SimulateTick(SimulationState state)
        {
            state.tickCount++;

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

            // Increment progress
            v.moveProgress++;

            // Has this villager arrived at the next node in the path?
            if (v.moveProgress >= v.moveSpeedTicks)
            {
                // Advance to next node in path
                v.previousNodeID = v.movePath[v.movePathIndex];
                v.movePathIndex++;
                v.moveProgress = 0;

                // Update current node
                v.currentNodeID = v.movePath[v.movePathIndex];

                // Check if we've reached the final node
                if (v.movePathIndex >= v.movePath.Length - 1)
                {
                    // Arrived at destination
                    v.state = VillagerState.Idle;
                    v.targetNodeID = -1;
                    v.movePath = new int[0];
                    v.movePathIndex = 0;
                    v.moveProgress = 0;
                }
            }

            // Write back (struct copy)
            state.villagers[villagerIndex] = v;
        }
    }
}