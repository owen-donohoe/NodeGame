namespace NodeWar.Simulation
{
    public enum CollectState { Empty, Neutral, Locked, StorageFull, Waiting, Collecting }
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

        // A locked owner's request is accepted; the production pass pauses it.
        public static bool CanCollect(SimulationState state, GameCommand command)
        {
            if ((command.value != 0 && command.value != 1) || command.villagerID != -1 ||
                command.playerID < 0 || command.playerID > 1 || command.playerID >= state.players.Length ||
                command.targetNodeID < 0 || command.targetNodeID >= state.nodes.Length) return false;
            NodeData node = state.nodes[command.targetNodeID];
            return node.ownerID == command.playerID && Total(node) > 0;
        }

        private static bool PoolHasRoom(int value, int cap) => value < (cap > 0 ? cap : int.MaxValue);

        private static int CollectibleResource(SimulationState state, NodeData node, GameBalanceData balance)
        {
            PlayerData player = state.players[node.ownerID];
            if (node.bankFood > 0 && PoolHasRoom(player.food, balance.foodCap)) return 0;
            if (node.bankMaterials > 0 && PoolHasRoom(player.materials, balance.materialsCap)) return 1;
            if (node.bankMetal > 0 && PoolHasRoom(player.metal, balance.metalCap)) return 2;
            return -1;
        }

        /// <summary>Read-only presentation query using the collection pass's exact pool check.</summary>
        public static CollectState CollectionState(SimulationState state, NodeData node, GameBalanceData balance)
        {
            if (node.ownerID < 0) return CollectState.Neutral;
            if (Locked(state, node, balance)) return CollectState.Locked;
            if (Total(node) == 0) return CollectState.Empty;
            if (CollectibleResource(state, node, balance) < 0) return CollectState.StorageFull;
            return node.collectRequested || HasStationaryCollector(state, node) ? CollectState.Collecting : CollectState.Waiting;
        }

        public static NodeData TickCollection(SimulationState state, NodeData node, GameBalanceData balance)
        {
            if (Total(node) == 0)
            {
                node.collectProgress = 0;
                node.collectRequested = false;
                return node;
            }
            if (!node.collectRequested && !HasStationaryCollector(state, node))
            {
                node.collectProgress = 0;
                return node;
            }
            if (Locked(state, node, balance)) return node;
            int resource = CollectibleResource(state, node, balance);
            if (resource < 0) return node;
            // One shared dwell clock, independent of production tempo.
            if (balance.collectProgressPerTick <= 0 || balance.collectProgressPerUnit <= 0) return node;
            node.collectProgress += balance.collectProgressPerTick;
            if (node.collectProgress >= balance.collectProgressPerUnit)
            {
                node.collectProgress -= balance.collectProgressPerUnit;
                switch (resource)
                {
                    case 0: node.bankFood--; state.players[node.ownerID].food++; break;
                    case 1: node.bankMaterials--; state.players[node.ownerID].materials++; break;
                    case 2: node.bankMetal--; state.players[node.ownerID].metal++; break;
                }
                if (Total(node) == 0)
                {
                    node.collectProgress = 0;
                    node.collectRequested = false;
                }
            }
            return node;
        }

        public static int ProductionTicks(NodeData node, GameBalanceData balance) =>
            node.districtType == DistrictType.Storehouse ? balance.GetDistrictStats(node.districtType, node.districtEra).productionTicks : 0;

        public static bool CanProduce(SimulationState state, NodeData node, GameBalanceData balance) =>
            node.districtType == DistrictType.Storehouse && node.ownerID >= 0 && DistrictHealth.Healthy(state, node) &&
            balance.BankTuningValid() && ProductionTicks(node, balance) > 0 && Total(node) < balance.bankCapacity;

        // Ordinary human workers retain the existing capacity; banks consume no slot.
        public static bool HasWorkerSlot(SimulationState state, int index, GameBalanceData balance)
        {
            VillagerData candidate = state.villagers[index];
            if (candidate.state == VillagerState.Working) return true;
            int workers = 0;
            for (int i = 0; i < state.villagers.Length; i++)
                if (state.villagers[i].currentNodeID == candidate.currentNodeID && state.villagers[i].ownerID == candidate.ownerID &&
                    state.villagers[i].state == VillagerState.Working) workers++;
            return workers < balance.maxWorkersPerNode;
        }
        private static int AddLoot(int value, int amount, int cap)
        {
            long sum = (long)value + amount;
            return (int)System.Math.Min(cap > 0 ? cap : int.MaxValue, sum);
        }

        // Full capture pays the bank before its contents are cleared.
        public static void PayBank(SimulationState state, NodeData node, int playerID, GameBalanceData balance)
        {
            state.players[playerID].food = AddLoot(state.players[playerID].food, node.bankFood, balance.foodCap);
            state.players[playerID].materials = AddLoot(state.players[playerID].materials, node.bankMaterials, balance.materialsCap);
            state.players[playerID].metal = AddLoot(state.players[playerID].metal, node.bankMetal, balance.metalCap);
        }
    }
}
