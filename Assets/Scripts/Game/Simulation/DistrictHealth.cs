namespace NodeWar.Simulation
{
    /// <summary>Shared passive-effect health gate, using the node's district era.</summary>
    public static class DistrictHealth
    {
        public static bool Healthy(SimulationState state, NodeData node)
        {
            int max = GameSimulation.Balance.GetDistrictStats(node.districtType, node.districtEra).healthMax;
            return max == 0 || node.districtHealth == max;
        }
    }
}
