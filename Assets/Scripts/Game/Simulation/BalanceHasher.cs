namespace NodeWar.Simulation
{
    /// <summary>
    /// A deterministic fingerprint of the balance a match is played with. Peers
    /// compare it in the handshake (as the ContentHash), because two players on
    /// the same simulation with different numbers still desync.
    ///
    /// Balance is read-only tuning, not <see cref="SimulationState"/>, so
    /// <see cref="SimulationStateHasher"/> never sees it. This is the hash that
    /// does.
    ///
    /// Every field of <see cref="GameBalanceData"/>, <see cref="SuitStats"/> and
    /// <see cref="DistrictStats"/> goes in, in declared order. A test sets each field in turn and requires
    /// the hash to move, so a new field left out of here fails it.
    /// </summary>
    public static class BalanceHasher
    {
        public static int Hash(GameBalanceData b)
        {
            unchecked
            {
                int hash = 17;

                hash = hash * 31 + b.ticksPerSecond;
                hash = hash * 31 + b.baseClaimPerTick;
                hash = hash * 31 + b.decrementMultiplier;
                hash = hash * 31 + b.claimThreshold;
                hash = hash * 31 + b.maxClaimersPerNode;
                hash = hash * 31 + b.respawnTicks;
                hash = hash * 31 + b.healIntervalTicks;
                hash = hash * 31 + b.breachThreshold;
                hash = hash * 31 + b.maxWorkersPerNode;
                hash = hash * 31 + b.maxVillagersPerPlayer;
                hash = hash * 31 + b.respawnCostFood;
                hash = hash * 31 + b.baseHP;
                hash = hash * 31 + b.baseAttackDamage;
                hash = hash * 31 + b.baseMoveSpeedTicks;
                hash = hash * 31 + b.baseAttackCooldownMax;

                // Array order matters: TryGetSuitStats takes the first match.
                if (b.suitStats == null)
                {
                    hash = hash * 31 - 1;
                }
                else
                {
                    hash = hash * 31 + b.suitStats.Length;
                    for (int i = 0; i < b.suitStats.Length; i++)
                    {
                        SuitStats s = b.suitStats[i];
                        hash = hash * 31 + (int)s.suitType;
                        hash = hash * 31 + s.bonusHP;
                        hash = hash * 31 + s.attackDamage;
                        hash = hash * 31 + s.moveSpeedTicks;
                        hash = hash * 31 + s.attackCooldownMax;
                        hash = hash * 31 + s.foodCost;
                        hash = hash * 31 + s.materialCost;
                        hash = hash * 31 + s.fightPriority;
                        hash = hash * 31 + s.era;
                    }
                }

                // Order matters here too: GetDistrictStats takes the first match.
                if (b.districtStats == null)
                {
                    hash = hash * 31 - 1;
                }
                else
                {
                    hash = hash * 31 + b.districtStats.Length;
                    for (int i = 0; i < b.districtStats.Length; i++)
                    {
                        DistrictStats d = b.districtStats[i];
                        hash = hash * 31 + (int)d.districtType;
                        hash = hash * 31 + d.era;
                        hash = hash * 31 + d.productionTicks;
                        hash = hash * 31 + d.secondaryProductionTicks;
                        hash = hash * 31 + d.bonusVillagersOnClaim;
                        hash = hash * 31 + d.healIntervalTicks;
                        hash = hash * 31 + d.claimDecrementMultiplier;
                        hash = hash * 31 + d.damageReduction;
                        hash = hash * 31 + d.maxHPBonus;
                        hash = hash * 31 + d.claimRateNumerator;
                        hash = hash * 31 + d.claimRateDenominator;
                        hash = hash * 31 + d.respawnBoostPerWorker;
                        hash = hash * 31 + d.respawnCostReductionPercent;
                    }
                }

                return hash;
            }
        }
    }
}
