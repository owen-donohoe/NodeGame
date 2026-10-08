namespace NodeWar.Simulation
{
    [System.Serializable]
    public struct SuitStats
    {
        public SuitType suitType;

        /// <summary>
        /// Which era's variant of the suit this is (0 = the first arena's).
        /// A lookup for an era with no entry falls back to era 0.
        /// </summary>
        public int era;
        public int bonusHP; // added to baseHP (can be negative)
        public int attackDamage;
        public int moveSpeedTicks;
        public int attackCooldownMax;
        public int foodCost;
        public int materialCost;
        public int fightPriority;
    }

    /// <summary>
    /// One era's variant of a district: every number the simulation reads that
    /// belongs to that district rather than to the game as a whole. A field a
    /// district does not use stays 0. Each district type fills only its own:
    ///   Farm, Mine, Forge  productionTicks
    ///   Market             productionTicks (food), secondaryProductionTicks (materials)
    ///   Town               townBonusVillagers (Village bonusVillagersOnClaim is historical)
    ///   Shrine             healIntervalTicks
    ///   Rampart            claimDecrementMultiplier, damageReduction, maxHPBonus
    ///   Watchtower         claimRateNumerator / claimRateDenominator
    ///   Sanctuary          respawnBoostPerWorker, respawnCostReductionPercent
    /// </summary>
    [System.Serializable]
    public struct DistrictStats
    {
        public DistrictType districtType;
        public int era;

        public int productionTicks;
        public int secondaryProductionTicks;
        public int bonusVillagersOnClaim; // Historical JSON field; inactive on Village.
        public int townBonusVillagers;
        public int healIntervalTicks;
        public int claimDecrementMultiplier;
        public int damageReduction;
        public int maxHPBonus;
        public int claimRateNumerator;
        public int claimRateDenominator;
        public int respawnBoostPerWorker;
        public int respawnCostReductionPercent;
    }

    [System.Serializable]
    public struct GameBalanceData
    {
        /// <summary>Arenas 0-5; arena N plays era N's variants.</summary>
        public const int EraCount = 6;

        public int ticksPerSecond;

        public int baseClaimPerTick;
        public int decrementMultiplier;
        public int claimThreshold;
        public int maxClaimersPerNode;
        public int captureBonusPercentPerStep;
        public int captureBonusMaxSteps;

        public int respawnTicks;
        public int healIntervalTicks;
        public int breachThreshold;

        // Missing serialized fields disable their feature; Default() opts in.
        public int[] tempoStageTicks;
        public int[] tempoClaimPercent;
        public int[] tempoRespawnPercent;
        public int[] tempoProductionPercent;
        public int[] suddenDeathTicks;
        public int[] suddenDeathThresholds;
        public int breachBarMax;
        public int[] breachSwarmRate;
        public int breachBarDecayPerTick;

        public int maxWorkersPerNode;
        public int maxVillagersPerPlayer;

        public int respawnCostFood;
        public int recruitBaseCost;
        public int recruitCostPerRecruit;

        /// <summary>Resource ceilings; zero or negative means uncapped.</summary>
        public int foodCap;
        public int materialsCap;
        public int metalCap;

        public int baseHP;
        public int baseAttackDamage;
        public int baseMoveSpeedTicks;
        public int baseAttackCooldownMax;

        public SuitStats[] suitStats;

        /// <summary>Per (district, era). See <see cref="DistrictStats"/>.</summary>
        public DistrictStats[] districtStats;

        private static bool IncreasingTicks(int[] ticks)
        {
            if (ticks == null) return true;
            int previous = 0;
            for (int i = 0; i < ticks.Length; i++)
            {
                if (ticks[i] <= previous) return false;
                previous = ticks[i];
            }
            return true;
        }

        internal bool TempoAxisValid(int[] pct)
        {
            if (tempoStageTicks == null || tempoStageTicks.Length == 0)
                return pct == null || pct.Length == 0;
            if (!IncreasingTicks(tempoStageTicks) || pct == null || pct.Length != tempoStageTicks.Length)
                return false;
            for (int i = 0; i < pct.Length; i++)
                if (pct[i] <= 0) return false;
            return true;
        }

        // A single completion per tick requires each duration to cover the largest
        // possible decrement. Zero durations denote districts that produce nothing.
        public bool ProductionTempoValid()
        {
            if (!TempoAxisValid(tempoProductionPercent)) return false;
            if (tempoProductionPercent == null || tempoProductionPercent.Length == 0) return true;
            long largest = 1;
            for (int i = 0; i < tempoProductionPercent.Length; i++)
            {
                long decrement = ((long)tempoProductionPercent[i] + 99) / 100;
                if (decrement > largest) largest = decrement;
            }
            if (districtStats != null)
                for (int i = 0; i < districtStats.Length; i++)
                {
                    DistrictStats d = districtStats[i];
                    if (d.productionTicks != 0 && (d.productionTicks < 2 || d.productionTicks < largest)) return false;
                    if (d.secondaryProductionTicks != 0 && (d.secondaryProductionTicks < 2 || d.secondaryProductionTicks < largest)) return false;
                }
            return true;
        }

        public bool BreachBarEnabled()
        {
            if (breachBarMax <= 0 || breachBarDecayPerTick < 0 || breachSwarmRate == null || breachSwarmRate.Length == 0)
                return false;
            for (int i = 0; i < breachSwarmRate.Length; i++)
                if (breachSwarmRate[i] <= 0) return false;
            return true;
        }

        public bool SuddenDeathValid()
        {
            if (suddenDeathTicks == null || suddenDeathTicks.Length == 0)
                return suddenDeathThresholds == null || suddenDeathThresholds.Length == 0;
            if (!IncreasingTicks(suddenDeathTicks) || suddenDeathThresholds == null ||
                suddenDeathTicks.Length != suddenDeathThresholds.Length) return false;
            int previous = breachThreshold;
            for (int i = 0; i < suddenDeathThresholds.Length; i++)
            {
                int threshold = suddenDeathThresholds[i];
                if (threshold <= 0 || threshold >= previous) return false;
                previous = threshold;
            }
            return true;
        }

        public bool TempoAndBreachValid(out string reason)
        {
            if (!TempoAxisValid(tempoClaimPercent) || !TempoAxisValid(tempoRespawnPercent) || !ProductionTempoValid())
            { reason = "Invalid tempo schedule, percentages or production durations."; return false; }
            if (!SuddenDeathValid())
            { reason = "Invalid sudden-death schedule or thresholds."; return false; }
            if (breachBarMax < 0 || breachBarDecayPerTick < 0 || (breachBarMax > 0 && !BreachBarEnabled()))
            { reason = "Invalid breach bar, swarm rates or decay."; return false; }
            reason = null;
            return true;
        }

        public int TempoPercent(int[] pct, int tick)
        {
            if (!TempoAxisValid(pct) || pct == null) return 100;
            for (int i = pct.Length - 1; i >= 0; i--)
                if (tick >= tempoStageTicks[i]) return pct[i];
            return 100;
        }

        // C(t) integrates ticks 1..t inclusively, with one division after summing.
        public long ScaledTicks(int[] pct, int tick)
        {
            if (tick <= 0) return 0;
            if (!TempoAxisValid(pct) || pct == null || pct.Length == 0) return tick;
            long sum = 0;
            long start = 1;
            int percent = 100;
            for (int i = 0; i < pct.Length; i++)
            {
                long end = (long)tempoStageTicks[i] - 1;
                if (end > tick) end = tick;
                if (end >= start) sum += (end - start + 1) * percent;
                if (tempoStageTicks[i] > tick) return sum / 100;
                start = tempoStageTicks[i];
                percent = pct[i];
            }
            sum += ((long)tick - start + 1) * percent;
            return sum / 100;
        }

        public int TimerDecrement(int[] pct, int tick)
        {
            if (tick <= 0) return 0;
            return (int)(ScaledTicks(pct, tick) - ScaledTicks(pct, tick - 1));
        }

        public int BreachThresholdAt(int tick)
        {
            if (!BreachBarEnabled() || !SuddenDeathValid() || suddenDeathTicks == null) return breachThreshold;
            for (int i = suddenDeathTicks.Length - 1; i >= 0; i--)
                if (tick >= suddenDeathTicks[i]) return suddenDeathThresholds[i];
            return breachThreshold;
        }

        /// <summary>
        /// Escalate first, then subtract the integer-floor Infirmary discount.
        /// Preserve the one-food minimum; saturate unrepresentable prices.
        /// Shared arithmetic for command validation and read-only UI pricing.
        /// </summary>
        public int PaidRespawnCost(int paidRespawns, int reductionPercent)
        {
            long cost = (long)respawnCostFood * (System.Math.Max(0, paidRespawns) + 1L);
            int percent = System.Math.Max(0, System.Math.Min(100, reductionPercent));
            // Split the multiplication to keep even the largest base/counter safe.
            cost -= cost / 100 * percent + cost % 100 * percent / 100;
            if (cost < 1) return 1;
            return cost > int.MaxValue ? int.MaxValue : (int)cost;
        }

        public static int ClampResource(int value, int cap)
        {
            return cap > 0 && value > cap ? cap : value;
        }

        public static bool HasResourceRoom(int value, int cap)
        {
            return cap <= 0 || value < cap;
        }

        /// <summary>A full stock wastes the payout; nonpositive caps keep legacy arithmetic.</summary>
        public static int AddResource(int value, int cap)
        {
            return cap > 0 && value >= cap ? cap : unchecked(value + 1);
        }

        public bool CoreRulesValid(out string reason)
        {
            if (districtStats != null)
                for (int i = 0; i < districtStats.Length; i++)
                {
                    DistrictStats d = districtStats[i];
                    if (d.districtType == DistrictType.Infirmary &&
                        (d.healIntervalTicks <= 0 || d.respawnBoostPerWorker < 0 ||
                         d.respawnCostReductionPercent < 0 || d.respawnCostReductionPercent > 100))
                    { reason = "Invalid Infirmary interval, boost or discount."; return false; }
                }
            if (districtStats != null)
                for (int i = 0; i < districtStats.Length; i++)
                    if (districtStats[i].townBonusVillagers < 0)
                    {
                        reason = "Town bonus must be nonnegative.";
                        return false;
                    }
            if (captureBonusPercentPerStep < 0 || captureBonusMaxSteps < 0)
            {
                reason = "Capture bonus step and cap must be nonnegative.";
                return false;
            }
            try
            {
                checked
                {
                    long frontier = 100L + (long)captureBonusPercentPerStep * captureBonusMaxSteps;
                    int maxTempo = 100;
                    if (tempoClaimPercent != null)
                        for (int i = 0; i < tempoClaimPercent.Length; i++)
                            if (tempoClaimPercent[i] > maxTempo) maxTempo = tempoClaimPercent[i];
                    // Validate both ordinary and opposing-lean contributions.
                    long claim = (long)baseClaimPerTick * 4 * System.Math.Max(1, decrementMultiplier);
                    claim = claim * frontier / 100;
                    claim = claim * maxTempo / 100;
                    long restore = (long)baseClaimPerTick * 4 * maxTempo / 100;
                    if (breachSwarmRate != null)
                        for (int i = 0; i < breachSwarmRate.Length; i++)
                        {
                            long breach = (long)breachSwarmRate[i] * frontier / 100;
                        }
                }
            }
            catch (System.OverflowException)
            {
                reason = "Core rule percentage products exceed the integer range.";
                return false;
            }
            reason = null;
            return true;
        }

        /// <summary>Price is also the cooldown in seconds, using the pre-recruit count.</summary>
        public bool TryRecruitCostAndCooldown(int count, int tick, out int cost, out int readyTick)
        {
            cost = 0;
            readyTick = 0;
            if (count < 0 || recruitBaseCost <= 0 || recruitCostPerRecruit <= 0 || ticksPerSecond <= 0)
                return false;
            try
            {
                checked
                {
                    long price = (long)recruitBaseCost + (long)recruitCostPerRecruit * count;
                    long duration = price * ticksPerSecond;
                    long ready = tick + duration;
                    if (price > int.MaxValue || duration > int.MaxValue || ready < int.MinValue || ready > int.MaxValue)
                        return false;
                    cost = (int)price;
                    readyTick = (int)ready;
                    return true;
                }
            }
            catch (System.OverflowException) { return false; }
        }

        public static GameBalanceData Default()
        {
            return new GameBalanceData
            {
                ticksPerSecond = 10,
                baseClaimPerTick = 17,
                captureBonusPercentPerStep = 25,
                captureBonusMaxSteps = 2,
                decrementMultiplier = 4,
                claimThreshold = 10000,
                maxClaimersPerNode = 4,
                respawnTicks = 50,
                healIntervalTicks = 30,
                breachThreshold = 3,
                tempoStageTicks = new[] { 1200, 1800 },
                tempoClaimPercent = new[] { 150, 200 },
                tempoRespawnPercent = new[] { 80, 67 },
                tempoProductionPercent = new[] { 110, 125 },
                suddenDeathTicks = new[] { 2400 },
                suddenDeathThresholds = new[] { 1 },
                breachBarMax = 4000,
                breachSwarmRate = new[] { 50, 83, 108, 125 },
                breachBarDecayPerTick = 200,
                maxWorkersPerNode = 2,
                maxVillagersPerPlayer = 25,
                respawnCostFood = 1,
                recruitBaseCost = 6,
                recruitCostPerRecruit = 3,
                foodCap = 30,
                materialsCap = 30,
                metalCap = 10,
                baseHP = 5,
                baseAttackDamage = 1,
                baseMoveSpeedTicks = 4,
                baseAttackCooldownMax = 20,
                suitStats = null,
                districtStats = UniformDistrictStats(
                    farmTicks: 30, mineTicks: 40, forgeTicks: 50, marketFoodTicks: 45, marketMaterialTicks: 60,
                    villageBonus: 2, shrineHealInterval: 20,
                    rampartDecrement: 2, rampartDamageReduction: 1, rampartMaxHP: 1,
                    watchtowerNumerator: 3, watchtowerDenominator: 2,
                    sanctuaryBoost: 1, sanctuaryCostReductionPercent: 25),
            };
        }

        /// <summary>
        /// Every district that has numbers of its own, at every era, all eras
        /// playing the same. Eras differ once someone tunes them.
        /// </summary>
        public static DistrictStats[] UniformDistrictStats(
            int farmTicks, int mineTicks, int forgeTicks, int marketFoodTicks, int marketMaterialTicks,
            int villageBonus, int shrineHealInterval,
            int rampartDecrement, int rampartDamageReduction, int rampartMaxHP,
            int watchtowerNumerator, int watchtowerDenominator,
            int sanctuaryBoost, int sanctuaryCostReductionPercent)
        {
            DistrictStats[] template =
            {
                new DistrictStats { districtType = DistrictType.Farm, productionTicks = farmTicks },
                new DistrictStats { districtType = DistrictType.Mine, productionTicks = mineTicks },
                new DistrictStats { districtType = DistrictType.Forge, productionTicks = forgeTicks },
                new DistrictStats { districtType = DistrictType.Market, productionTicks = marketFoodTicks,
                    secondaryProductionTicks = marketMaterialTicks },
                new DistrictStats { districtType = DistrictType.Village, bonusVillagersOnClaim = villageBonus },
                new DistrictStats { districtType = DistrictType.Shrine, healIntervalTicks = shrineHealInterval },
                new DistrictStats { districtType = DistrictType.Rampart, claimDecrementMultiplier = rampartDecrement,
                    damageReduction = rampartDamageReduction, maxHPBonus = rampartMaxHP },
                new DistrictStats { districtType = DistrictType.Watchtower, claimRateNumerator = watchtowerNumerator,
                    claimRateDenominator = watchtowerDenominator },
                new DistrictStats { districtType = DistrictType.Sanctuary, respawnBoostPerWorker = sanctuaryBoost,
                    respawnCostReductionPercent = sanctuaryCostReductionPercent },
                new DistrictStats { districtType = DistrictType.Town, townBonusVillagers = 2 },
                new DistrictStats { districtType = DistrictType.Barracks },
                new DistrictStats { districtType = DistrictType.Infirmary, healIntervalTicks = 10, respawnBoostPerWorker = 1, respawnCostReductionPercent = 20 },
                new DistrictStats { districtType = DistrictType.Fortress }
            };

            DistrictStats[] all = new DistrictStats[template.Length * EraCount];
            for (int d = 0; d < template.Length; d++)
            {
                for (int era = 0; era < EraCount; era++)
                {
                    DistrictStats entry = template[d];
                    entry.era = era;
                    all[d * EraCount + era] = entry;
                }
            }
            return all;
        }

        /// <summary>
        /// The suit table with a copy of each era-0 entry for every later era
        /// that has none, placed after the suit's own entries.
        /// </summary>
        public static SuitStats[] WithEveryEra(SuitStats[] suits)
        {
            if (suits == null) return null;
            var all = new System.Collections.Generic.List<SuitStats>();
            for (int i = 0; i < suits.Length; i++)
            {
                all.Add(suits[i]);
                if (suits[i].era != 0) continue;
                for (int era = 1; era < EraCount; era++)
                {
                    bool present = false;
                    for (int j = 0; j < suits.Length; j++)
                        if (suits[j].suitType == suits[i].suitType && suits[j].era == era) present = true;
                    if (present) continue;
                    SuitStats copy = suits[i];
                    copy.era = era;
                    all.Add(copy);
                }
            }
            return all.ToArray();
        }

        public SuitStats GetSuitStats(SuitType type)
        {
            return GetSuitStats(type, 0);
        }

        /// <summary>The max HP a villager's Rampart bonus added; 0 without one.</summary>
        public int RampartBonusHP(VillagerData v)
        {
            return v.hasRampartBonus ? GetDistrictStats(DistrictType.Rampart, v.rampartBonusEra).maxHPBonus : 0;
        }

        public SuitStats GetSuitStats(SuitType type, int era)
        {
            TryGetSuitStats(type, era, out SuitStats stats);
            return stats;
        }

        public bool TryGetSuitStats(SuitType type, out SuitStats stats)
        {
            return TryGetSuitStats(type, 0, out stats);
        }

        /// <summary>
        /// The first entry for the suit at that era, else the first at era 0.
        /// First-match, like every table lookup here, so array order decides a
        /// duplicate and the balance hash covers the order.
        /// </summary>
        public bool TryGetSuitStats(SuitType type, int era, out SuitStats stats)
        {
            if (suitStats != null)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    int wanted = pass == 0 ? era : 0;
                    if (pass == 1 && era == 0) break;
                    for (int i = 0; i < suitStats.Length; i++)
                    {
                        if (suitStats[i].suitType == type && suitStats[i].era == wanted)
                        {
                            stats = suitStats[i];
                            return true;
                        }
                    }
                }
            }
            stats = default;
            return false;
        }

        /// <summary>
        /// The district's stats at that era, else at era 0, else all zero: a
        /// district with no entry produces nothing and grants nothing.
        /// </summary>
        public DistrictStats GetDistrictStats(DistrictType type, int era)
        {
            if (districtStats != null)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    int wanted = pass == 0 ? era : 0;
                    if (pass == 1 && era == 0) break;
                    for (int i = 0; i < districtStats.Length; i++)
                    {
                        if (districtStats[i].districtType == type && districtStats[i].era == wanted)
                            return districtStats[i];
                    }
                }
            }
            return new DistrictStats { districtType = type, era = era };
        }

        public bool CanEquipSuitAtNode(SuitType suit, DistrictType district)
        {
            return district == DistrictType.Barracks && IsCombatSuit(suit);
        }

        public static bool IsCombatSuit(SuitType suit)
        {
            switch (suit)
            {
                case SuitType.Warrior:
                case SuitType.Guardian:
                case SuitType.Scout:
                case SuitType.Berserker:
                case SuitType.Medic:
                    return true;

                default:
                    return false;
            }
        }

        public static DistrictUpgradeCategory GetUpgradeCategoryForDistrict(DistrictType district)
        {
            switch (district)
            {
                case DistrictType.Camp:
                case DistrictType.Barracks:
                case DistrictType.Arsenal:
                    return DistrictUpgradeCategory.Army;

                case DistrictType.Shrine:
                case DistrictType.Sanctuary:
                    return DistrictUpgradeCategory.Healing;

                case DistrictType.Watchtower:
                case DistrictType.Rampart:
                    return DistrictUpgradeCategory.Affect;

                case DistrictType.Market:
                    return DistrictUpgradeCategory.ResourceSpecial;

                default:
                    return DistrictUpgradeCategory.Fixed;
            }
        }
    }
}
