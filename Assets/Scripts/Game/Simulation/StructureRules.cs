namespace NodeWar.Simulation
{
    /// <summary>One attack verb for every structure; Medic keeps its civilian claim role.</summary>
    public static class StructureRules
    {
        public static bool CanAttack(SuitType suit) => suit == SuitType.Warrior ||
            suit == SuitType.Guardian || suit == SuitType.Scout || suit == SuitType.Berserker;

        public static int MaxHP(NodeData node, GameBalanceData balance) =>
            node.structureKind == StructureKind.Fortification
                ? balance.GetDistrictStats(DistrictType.Fortress, node.districtEra).fortificationHP
                : node.structureKind == StructureKind.Minion ? balance.minionHP : 0;

        private static bool Eligible(SimulationState state, VillagerData v)
        {
            if (!CanAttack(v.suit) || v.hp <= 0 || v.isConsumed || v.state == VillagerState.Dead ||
                v.state == VillagerState.Moving || v.state == VillagerState.Breaching ||
                (v.targetNodeID >= 0 && v.targetNodeID != v.currentNodeID)) return false;
            NodeData node = state.nodes[v.currentNodeID];
            return node.districtType != DistrictType.Core && node.ownerID == 1 - v.ownerID &&
                node.structureKind != StructureKind.None && node.structureHP > 0 &&
                !NodeActionRules.HasLivingEnemyAtNode(state, v.ownerID, v.currentNodeID);
        }

        // Fighting survivors may be selected at post-combat resume. The damage pass
        // separately excludes Fighting, so a kill cannot also damage a structure.
        public static bool IsSelectedAttacker(SimulationState state, int villagerIndex, GameBalanceData balance)
        {
            VillagerData candidate = state.villagers[villagerIndex];
            if (!balance.StructureTuningValid() || !Eligible(state, candidate)) return false;
            int rank = 0;
            for (int i = 0; i < state.villagers.Length; i++)
            {
                VillagerData other = state.villagers[i];
                if (other.currentNodeID != candidate.currentNodeID || !Eligible(state, other)) continue;
                if (other.villagerID < candidate.villagerID ||
                    (other.villagerID == candidate.villagerID && i < villagerIndex)) rank++;
            }
            return rank < balance.maxStructureAttackersPerNode;
        }

        public static NodeData Destroy(NodeData node)
        {
            // D3 will pay raider loot before calling this clearing helper.
            // Never leave bank contents on a bare node (bank > 0 implies Minion).
            node.bankFood = node.bankMaterials = node.bankMetal = 0;
            node.minionProductionRemaining = 0;
            node.storehouseNextResource = 0;
            node.structureKind = StructureKind.None;
            node.structureHP = 0;
            node.fortressLevel = 0;
            node.autoRecruit = false;
            return node;
        }
    }
}
