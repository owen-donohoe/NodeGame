namespace NodeWar.Simulation
{
    public enum RecruitRefusal
    {
        None,
        InvalidPlayer,
        InvalidNode,
        NotVillage,
        NotOwned,
        EnemyPresent,
        Cooldown,
        InvalidCost,
        CostAboveFoodCap,
        InsufficientFood,
        PopulationCap
    }

    /// <summary>Read-only eligibility shared by node commands, automatic production and UI.</summary>
    public static class NodeActionRules
    {
        private static RecruitRefusal ValidateVillage(SimulationState state, int playerID, int nodeID)
        {
            if (playerID < 0 || playerID > 1 || playerID >= state.players.Length)
                return RecruitRefusal.InvalidPlayer;
            if (nodeID < 0 || nodeID >= state.nodes.Length) return RecruitRefusal.InvalidNode;
            if (state.nodes[nodeID].districtType != DistrictType.Village) return RecruitRefusal.NotVillage;
            if (state.nodes[nodeID].ownerID != playerID) return RecruitRefusal.NotOwned;
            return RecruitRefusal.None;
        }

        public static bool CanRecruit(SimulationState state, GameBalanceData balance, int playerID, int nodeID,
            out RecruitRefusal refusal)
        {
            refusal = ValidateVillage(state, playerID, nodeID);
            if (refusal != RecruitRefusal.None) return false;
            if (HasLivingEnemyAtNode(state, playerID, nodeID)) refusal = RecruitRefusal.EnemyPresent;
            else if (state.nodes[nodeID].recruitReadyTick > state.tickCount) refusal = RecruitRefusal.Cooldown;
            else if (!balance.TryRecruitCostAndCooldown(state.players[playerID].recruitCount, state.tickCount,
                out int cost, out _)) refusal = RecruitRefusal.InvalidCost;
            else if (balance.foodCap > 0 && cost > balance.foodCap) refusal = RecruitRefusal.CostAboveFoodCap;
            else if (state.players[playerID].food < cost) refusal = RecruitRefusal.InsufficientFood;
            else if (CountPopulation(state, playerID) >= balance.maxVillagersPerPlayer) refusal = RecruitRefusal.PopulationCap;
            return refusal == RecruitRefusal.None;
        }

        public static bool CanSetAutoRecruit(SimulationState state, int playerID, int nodeID, int value)
        {
            return (value == 0 || value == 1) && ValidateVillage(state, playerID, nodeID) == RecruitRefusal.None;
        }

        public static bool CanUpgradeFortress(SimulationState state, GameBalanceData balance, int playerID,
            int nodeID, int currency, int villagerID = -1)
        {
            if (villagerID != -1 || (currency != 0 && currency != 1) || playerID < 0 || playerID > 1 ||
                playerID >= state.players.Length || nodeID < 0 || nodeID >= state.nodes.Length) return false;
            NodeData node = state.nodes[nodeID];
            if (node.districtType != DistrictType.Fortress || node.ownerID != playerID ||
                node.fortressLevel < 0 || node.fortressLevel >= 3 || HasLivingEnemyAtNode(state, playerID, nodeID)) return false;
            DistrictStats stats = balance.GetDistrictStats(DistrictType.Fortress, node.districtEra);
            if (!GameBalanceData.FortressStatsValid(stats)) return false;
            int cost = (currency == 0 ? stats.fortressMaterialsCosts : stats.fortressMetalCosts)[node.fortressLevel + 1];
            return (currency == 0 ? state.players[playerID].materials : state.players[playerID].metal) >= cost;
        }

        public static int CountPopulation(SimulationState state, int playerID)
        {
            int count = 0;
            for (int i = 0; i < state.villagers.Length; i++)
                if (state.villagers[i].ownerID == playerID && !state.villagers[i].isConsumed) count++;
            return count;
        }

        public static bool HasLivingEnemyAtNode(SimulationState state, int playerID, int nodeID)
        {
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID == 1 - playerID && v.currentNodeID == nodeID &&
                    v.state != VillagerState.Dead && !v.isConsumed) return true;
            }
            return false;
        }
    }
}
