namespace NodeWar.Simulation
{
    /// <summary>
    /// Computes a deterministic integer hash of the full SimulationState.
    /// Used for desync detection in lockstep networking.
    /// Called every 50 ticks — both machines compare hashes to verify determinism.
    ///
    /// Rules:
    /// - Must include every field that can diverge between machines.
    /// - Must NOT include view-only data (node grid position is a View-layer concern).
    /// - Must be deterministic: same state = same hash, always.
    /// - Order of field hashing must be fixed (array index order).
    /// </summary>
    public static class SimulationStateHasher
    {
        public static int ComputeHash(SimulationState state)
        {
            unchecked
            {
                int hash = 17;

                hash = hash * 31 + state.tickCount;
                hash = hash * 31 + (state.gameOver ? 1 : 0);
                hash = hash * 31 + state.winnerID;
                hash = hash * 31 + state.defaultEdgeWeight;

                // Players
                for (int i = 0; i < state.players.Length; i++)
                {
                    hash = hash * 31 + state.players[i].playerID;
                    hash = hash * 31 + state.players[i].coreNodeID;
                    hash = hash * 31 + state.players[i].food;
                    hash = hash * 31 + state.players[i].materials;
                    hash = hash * 31 + state.players[i].metal;
                    hash = hash * 31 + state.players[i].breachCount;
                    if (state.players[i].draftedSuits != null)
                    {
                        hash = hash * 31 + state.players[i].draftedSuits.Length;
                        for (int s = 0; s < state.players[i].draftedSuits.Length; s++)
                            hash = hash * 31 + state.players[i].draftedSuits[s];
                    }
                    else hash = hash * 31 + 0;

                    if (state.players[i].draftedNodes != null)
                    {
                        hash = hash * 31 + state.players[i].draftedNodes.Length;
                        for (int n = 0; n < state.players[i].draftedNodes.Length; n++)
                            hash = hash * 31 + state.players[i].draftedNodes[n];
                    }
                    else hash = hash * 31 + 0;
                }

                // Nodes (mutable gameplay fields only)
                for (int i = 0; i < state.nodes.Length; i++)
                {
                    hash = hash * 31 + state.nodes[i].nodeID;
                    hash = hash * 31 + state.nodes[i].claimBar;
                    hash = hash * 31 + state.nodes[i].ownerID;
                    hash = hash * 31 + state.nodes[i].materialAllocation;
                    hash = hash * 31 + (int)state.nodes[i].districtType;
                    hash = hash * 31 + (int)state.nodes[i].slotType;
                    hash = hash * 31 + (int)state.nodes[i].baseDistrictType;
                }

                // Villagers (all mutable fields)
                for (int i = 0; i < state.villagers.Length; i++)
                {
                    VillagerData v = state.villagers[i];
                    hash = hash * 31 + v.villagerID;
                    hash = hash * 31 + v.ownerID;
                    hash = hash * 31 + v.currentNodeID;
                    hash = hash * 31 + v.targetNodeID;
                    hash = hash * 31 + v.movePathIndex;
                    hash = hash * 31 + v.moveProgress;
                    hash = hash * 31 + v.previousNodeID;
                    hash = hash * 31 + (int)v.state;
                    hash = hash * 31 + (int)v.suit;
                    hash = hash * 31 + v.hp;
                    hash = hash * 31 + v.maxHP;
                    hash = hash * 31 + v.attackDamage;
                    hash = hash * 31 + v.moveSpeedTicks;
                    hash = hash * 31 + v.respawnTicksRemaining;
                    hash = hash * 31 + v.attackCooldownRemaining;
                    hash = hash * 31 + v.attackCooldownMax;
                    hash = hash * 31 + v.combatTargetID;
                    hash = hash * 31 + v.fightPriority;
                    hash = hash * 31 + (v.isConsumed ? 1 : 0);
                    hash = hash * 31 + v.productionTicksRemaining;
                    hash = hash * 31 + v.productionTicksMax;
                    hash = hash * 31 + (v.hasRampartBonus ? 1 : 0);

                    // movePath contents
                    if (v.movePath != null)
                    {
                        hash = hash * 31 + v.movePath.Length;
                        for (int p = 0; p < v.movePath.Length; p++)
                        {
                            hash = hash * 31 + v.movePath[p];
                        }
                    }
                    else
                    {
                        hash = hash * 31 + 0;
                    }
                }

                return hash;
            }
        }
    }
}