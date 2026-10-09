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
        private static int HashNodeExtension(int hash, int index, int tag, int value)
        {
            if (value == 0) return hash;
            unchecked { return ((hash * 31 + tag) * 31 + index) * 31 + value; }
        }
        public static int ComputeHash(SimulationState state)
        {
            unchecked
            {
                int hash = 17;

                hash = hash * 31 + state.tickCount;
                hash = hash * 31 + (state.gameOver ? 1 : 0);
                hash = hash * 31 + state.winnerID;
                hash = hash * 31 + state.defaultLinkWeight;
                hash = hash * 31 + state.boardHash;

                // Players
                for (int i = 0; i < state.players.Length; i++)
                {
                    hash = hash * 31 + state.players[i].playerID;
                    hash = hash * 31 + state.players[i].coreNodeID;
                    hash = hash * 31 + state.players[i].food;
                    hash = hash * 31 + state.players[i].materials;
                    hash = hash * 31 + state.players[i].metal;
                    hash = hash * 31 + state.players[i].breachCount;
                    if (state.players[i].recruitCount != 0)
                    {
                        hash = hash * 31 + 2010;
                        hash = hash * 31 + i;
                        hash = hash * 31 + state.players[i].recruitCount;
                    }
                    // The new counter starts at zero; retain existing neutral hash paths.
                    if (state.players[i].paidRespawns != 0)
                    {
                        hash = hash * 31 + 2002;
                        hash = hash * 31 + state.players[i].paidRespawns;
                    }
                    // Omit neutral v2 fields so feature-off matches retain v1 hashes.
                    // Tags distinguish bar progress from the derived candidate ID.
                    if (state.players[i].breachBar != 0)
                    {
                        hash = hash * 31 + 2000;
                        hash = hash * 31 + state.players[i].breachBar;
                    }
                    if (state.players[i].nextBreacherID != -1)
                    {
                        hash = hash * 31 + 2001;
                        hash = hash * 31 + state.players[i].nextBreacherID;
                    }
                    if (state.players[i].draftedSuits != null)
                    {
                        hash = hash * 31 + state.players[i].draftedSuits.Length;
                        for (int s = 0; s < state.players[i].draftedSuits.Length; s++)
                            hash = hash * 31 + state.players[i].draftedSuits[s];
                    }
                    else hash = hash * 31 + 0;

                    if (state.players[i].draftedDistricts != null)
                    {
                        hash = hash * 31 + state.players[i].draftedDistricts.Length;
                        for (int n = 0; n < state.players[i].draftedDistricts.Length; n++)
                            hash = hash * 31 + state.players[i].draftedDistricts[n];
                    }
                    else hash = hash * 31 + 0;

                    hash = HashEras(hash, state.players[i].suitEras, 0);
                    hash = HashEras(hash, state.players[i].districtEras, 1000);
                }

                // Nodes (mutable gameplay fields only)
                for (int i = 0; i < state.nodes.Length; i++)
                {
                    hash = hash * 31 + state.nodes[i].nodeID;
                    hash = hash * 31 + state.nodes[i].claimBar;
                    hash = hash * 31 + state.nodes[i].ownerID;
                    hash = hash * 31 + state.nodes[i].materialAllocation;
                    hash = hash * 31 + (int)state.nodes[i].districtType;
                    hash = hash * 31 + (int)state.nodes[i].upgradeCategory;
                    hash = hash * 31 + (int)state.nodes[i].baseDistrictType;
                    hash = hash * 31 + (int)state.nodes[i].terrain;
                    hash = hash * 31 + (int)state.nodes[i].structureKind;
                    hash = hash * 31 + state.nodes[i].structureHP;
                    hash = HashNodeExtension(hash, i, 2020, state.nodes[i].bankFood);
                    hash = HashNodeExtension(hash, i, 2021, state.nodes[i].bankMaterials);
                    hash = HashNodeExtension(hash, i, 2022, state.nodes[i].bankMetal);
                    hash = HashNodeExtension(hash, i, 2023, state.nodes[i].minionProductionRemaining);
                    hash = HashNodeExtension(hash, i, 2024, state.nodes[i].storehouseNextResource);
                    hash = HashNodeExtension(hash, i, 2025, state.nodes[i].storehouseInitialised ? 1 : 0);
                    hash = HashNodeExtension(hash, i, 2026, state.nodes[i].collectProgress);
                    hash = HashNodeExtension(hash, i, 2027, state.nodes[i].collectRequested ? 1 : 0);
                    if (state.nodes[i].districtEra != 0)
                        hash = hash * 31 + state.nodes[i].districtEra;
                    if (state.nodes[i].recruitReadyTick != 0)
                    {
                        hash = hash * 31 + 4010;
                        hash = hash * 31 + i;
                        hash = hash * 31 + state.nodes[i].recruitReadyTick;
                    }
                    if (state.nodes[i].fortressLevel != 0)
                    {
                        hash = hash * 31 + 4013;
                        hash = hash * 31 + i;
                        hash = hash * 31 + state.nodes[i].fortressLevel;
                    }
                    if (state.nodes[i].townPaidMask != 0)
                    {
                        hash = hash * 31 + 4012;
                        hash = hash * 31 + i;
                        hash = hash * 31 + state.nodes[i].townPaidMask;
                    }
                    if (state.nodes[i].autoRecruit)
                    {
                        hash = hash * 31 + 4011;
                        hash = hash * 31 + i;
                        hash = hash * 31 + 1;
                    }
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

        /// <summary>
        /// Era fields are hashed only where they are not 0. Every era-0 match
        /// then hashes exactly as it did before eras existed, so the pinned
        /// baselines and older match logs still verify, while any era a peer
        /// disagrees on still changes the hash. Index and value both go in, so
        /// the same era on a different type hashes differently; the table offset
        /// keeps a suit era apart from a district era at the same index.
        /// </summary>
        private static int HashEras(int hash, int[] eras, int tableOffset)
        {
            unchecked
            {
                if (eras == null) return hash;
                for (int i = 0; i < eras.Length; i++)
                {
                    if (eras[i] == 0) continue;
                    hash = hash * 31 + tableOffset + i;
                    hash = hash * 31 + eras[i];
                }
                return hash;
            }
        }
    }
}
