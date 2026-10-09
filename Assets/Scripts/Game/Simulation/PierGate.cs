namespace NodeWar.Simulation
{
    /// <summary>A Pier gate is a position rule, shared by every departure path.</summary>
    public static class PierGate
    {
        public static bool IsEnemyPier(SimulationState state, VillagerData v)
        {
            NodeData node = state.nodes[v.currentNodeID];
            return node.districtType == DistrictType.Pier && node.ownerID == 1 - v.ownerID;
        }

        public static bool BlocksDeparture(SimulationState state, VillagerData v, int nextNode) =>
            v.state != VillagerState.Moving && IsEnemyPier(state, v) && nextNode != v.previousNodeID;

        internal static bool HasClaimerSlot(SimulationState state, VillagerData v, int cap)
        {
            int ahead = 0;
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData other = state.villagers[i];
                if (other.currentNodeID != v.currentNodeID || other.ownerID != v.ownerID ||
                    other.villagerID >= v.villagerID || other.isConsumed || other.hp <= 0) continue;
                if (other.state == VillagerState.Idle || other.state == VillagerState.Claiming || other.state == VillagerState.Working || other.state == VillagerState.Fighting)
                    ahead++;
            }
            return ahead < cap;
        }
    }
}
