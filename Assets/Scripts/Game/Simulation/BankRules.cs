namespace NodeWar.Simulation
{
    /// <summary>Shared bank eligibility, lock, production and worker definitions.</summary>
    public static class BankRules
    {
        public static long Total(NodeData node) => (long)node.bankFood + node.bankMaterials + node.bankMetal;

        public static bool Locked(SimulationState state, NodeData node, GameBalanceData balance) =>
            node.ownerID < 0 || NodeActionRules.HasLivingEnemyAtNode(state, node.ownerID, node.nodeID) ||
            (node.ownerID == 0 ? node.claimBar < balance.claimThreshold : node.claimBar > -balance.claimThreshold);

        public static bool HasStationaryCollector(SimulationState state, NodeData node)
        {
            if (node.ownerID < 0) return false;
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID == node.ownerID && v.currentNodeID == node.nodeID && v.hp > 0 && !v.isConsumed &&
                    (v.state == VillagerState.Idle || v.state == VillagerState.Working)) return true;
            }
            return false;
        }

        public static bool MinionDistrict(DistrictType type) => type == DistrictType.Farm || type == DistrictType.Mine ||
            type == DistrictType.Forge || type == DistrictType.Storehouse;

        public static int ProductionTicks(NodeData node, GameBalanceData balance) =>
            MinionDistrict(node.districtType) ? balance.GetDistrictStats(node.districtType, node.districtEra).productionTicks : 0;

        public static bool CanInstallMinion(SimulationState state, GameBalanceData balance, GameCommand command)
        {
            if (command.value != 0 || command.villagerID != -1 || command.playerID < 0 || command.playerID > 1 ||
                command.playerID >= state.players.Length || command.targetNodeID < 0 || command.targetNodeID >= state.nodes.Length ||
                (!balance.BankTuningValid() || balance.maxWorkersPerNode < 1)) return false;
            NodeData node = state.nodes[command.targetNodeID];
            return node.ownerID == command.playerID && MinionDistrict(node.districtType) &&
                node.structureKind == StructureKind.None && Total(node) == 0 && ProductionTicks(node, balance) > 0 &&
                !Locked(state, node, balance) && state.players[command.playerID].metal >= balance.minionMetalCost;
        }

        public static NodeData CreateMinion(NodeData node, GameBalanceData balance)
        {
            node.structureKind = StructureKind.Minion;
            node.structureHP = balance.minionHP;
            node.minionProductionRemaining = ProductionTicks(node, balance);
            node.storehouseNextResource = 0;
            node.bankFood = node.bankMaterials = node.bankMetal = 0;
            return node;
        }

        public static bool CanProduce(SimulationState state, NodeData node, GameBalanceData balance) =>
            node.structureKind == StructureKind.Minion && node.structureHP > 0 && node.ownerID >= 0 &&
            balance.BankTuningValid() && ProductionTicks(node, balance) > 0 && Total(node) < balance.bankCapacity;

        public static int WorkerCapacity(NodeData node, GameBalanceData balance) =>
            System.Math.Max(0, balance.maxWorkersPerNode - (node.structureKind == StructureKind.Minion ? 1 : 0));

        // Rank candidates by ID, irrespective of array storage order. Both arrival
        // and the per-tick worker update use this same capacity and ranking.
        public static bool HasWorkerSlot(SimulationState state, int index, GameBalanceData balance)
        {
            VillagerData candidate = state.villagers[index];
            if (state.nodes[candidate.currentNodeID].structureKind != StructureKind.Minion)
            {
                if (candidate.state == VillagerState.Working) return true;
                int workers = 0;
                for (int i = 0; i < state.villagers.Length; i++)
                    if (state.villagers[i].currentNodeID == candidate.currentNodeID && state.villagers[i].ownerID == candidate.ownerID &&
                        state.villagers[i].state == VillagerState.Working) workers++;
                return workers < WorkerCapacity(state.nodes[candidate.currentNodeID], balance);
            }
            int rank = 0;
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData v = state.villagers[i];
                if (v.ownerID != candidate.ownerID || v.currentNodeID != candidate.currentNodeID || v.hp <= 0 || v.isConsumed ||
                    GameBalanceData.IsCombatSuit(v.suit) ||
                    (v.state != VillagerState.Idle && v.state != VillagerState.Working && v.state != VillagerState.Claiming) ||
                    (v.targetNodeID >= 0 && v.targetNodeID != v.currentNodeID)) continue;
                if (v.villagerID < candidate.villagerID || (v.villagerID == candidate.villagerID && i < index)) rank++;
            }
            return rank < WorkerCapacity(state.nodes[candidate.currentNodeID], balance);
        }

        public static void DemoteSurplusWorkers(SimulationState state, int nodeID, GameBalanceData balance)
        {
            for (int i = 0; i < state.villagers.Length; i++)
                if (state.villagers[i].currentNodeID == nodeID && state.villagers[i].state == VillagerState.Working &&
                    !HasWorkerSlot(state, i, balance))
                {
                    state.villagers[i].state = VillagerState.Idle;
                    state.villagers[i].productionTicksRemaining = state.villagers[i].productionTicksMax = 0;
                }
        }

        private static int AddLoot(int value, int amount, int cap)
        {
            long sum = (long)value + amount;
            return (int)System.Math.Min(cap > 0 ? cap : int.MaxValue, sum);
        }

        // D3's destruction payout can use this exact transfer before Destroy.
        public static void PayBank(SimulationState state, NodeData node, int playerID, GameBalanceData balance)
        {
            state.players[playerID].food = AddLoot(state.players[playerID].food, node.bankFood, balance.foodCap);
            state.players[playerID].materials = AddLoot(state.players[playerID].materials, node.bankMaterials, balance.materialsCap);
            state.players[playerID].metal = AddLoot(state.players[playerID].metal, node.bankMetal, balance.metalCap);
        }
    }
}
