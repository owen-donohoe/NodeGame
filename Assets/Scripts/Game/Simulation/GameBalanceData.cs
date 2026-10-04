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
    ///   Village            bonusVillagersOnClaim
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
        public int bonusVillagersOnClaim;
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

        public int respawnTicks;
        public int healIntervalTicks;
        public int breachThreshold;

        public int maxWorkersPerNode;
        public int maxVillagersPerPlayer;

        public int respawnCostFood;

        public int baseHP;
        public int baseAttackDamage;
        public int baseMoveSpeedTicks;
        public int baseAttackCooldownMax;

        public SuitStats[] suitStats;

        /// <summary>Per (district, era). See <see cref="DistrictStats"/>.</summary>
        public DistrictStats[] districtStats;

        public static GameBalanceData Default()
        {
            return new GameBalanceData
            {
                ticksPerSecond = 10,
                baseClaimPerTick = 17,
                decrementMultiplier = 4,
                claimThreshold = 10000,
                maxClaimersPerNode = 4,
                respawnTicks = 50,
                healIntervalTicks = 30,
                breachThreshold = 3,
                maxWorkersPerNode = 2,
                maxVillagersPerPlayer = 25,
                respawnCostFood = 1,
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
                    respawnCostReductionPercent = sanctuaryCostReductionPercent }
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
            switch (district)
            {
                case DistrictType.Camp:
                    return suit == SuitType.Warrior || suit == SuitType.Scout;

                case DistrictType.Barracks:
                    return suit == SuitType.Warrior || suit == SuitType.Guardian ||
                           suit == SuitType.Berserker || suit == SuitType.Scout;

                case DistrictType.Arsenal:
                    return suit == SuitType.Warrior || suit == SuitType.Guardian ||
                           suit == SuitType.Scout;

                case DistrictType.Sanctuary:
                    return suit == SuitType.Medic;

                default:
                    return false;
            }
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

        public static NodeSlotType GetSlotTypeForDistrict(DistrictType district)
        {
            switch (district)
            {
                case DistrictType.Camp:
                case DistrictType.Barracks:
                case DistrictType.Arsenal:
                    return NodeSlotType.Army;

                case DistrictType.Shrine:
                case DistrictType.Sanctuary:
                    return NodeSlotType.Healing;

                case DistrictType.Watchtower:
                case DistrictType.Rampart:
                    return NodeSlotType.Affect;

                case DistrictType.Market:
                    return NodeSlotType.ResourceSpecial;

                default:
                    return NodeSlotType.Fixed;
            }
        }
    }
}
