namespace NodeWar.Simulation
{
    [System.Serializable]
    public struct SuitStats
    {
        public SuitType suitType;
        public int bonusHP; // added to baseHP (can be negative)
        public int attackDamage;
        public int moveSpeedTicks;
        public int attackCooldownMax;
        public int foodCost;
        public int materialCost;
        public int fightPriority;
    }

    [System.Serializable]
    public struct GameBalanceData
    {
        public int ticksPerSecond;

        public int baseClaimPerTick;
        public int decrementMultiplier;
        public int claimThreshold;
        public int maxClaimersPerNode;

        public int respawnTicks;
        public int healIntervalTicks;
        public int breachThreshold;

        public int foodProductionTicks;
        public int materialProductionTicks;
        public int metalProductionTicks;
        public int marketFoodProductionTicks;
        public int marketMaterialProductionTicks;
        public int maxWorkersPerNode;
        public int maxVillagersPerPlayer;

        public int respawnCostFood;

        public int baseHP;
        public int baseAttackDamage;
        public int baseMoveSpeedTicks;
        public int baseAttackCooldownMax;

        public SuitStats[] suitStats;

        public int bonusVillagersOnVillageClaim;

        public int shrineHealIntervalTicks;

        public int rampartDecrementMultiplier;
        public int rampartDamageReduction;
        public int rampartMaxHPBonus;

        public int watchtowerClaimNumerator;
        public int watchtowerClaimDenominator;

        public int sanctuaryRespawnBoostPerWorker;
        public int sanctuaryRespawnCostReductionPercent;

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
                foodProductionTicks = 30,
                materialProductionTicks = 40,
                metalProductionTicks = 50,
                marketFoodProductionTicks = 45,
                marketMaterialProductionTicks = 60,
                maxWorkersPerNode = 2,
                maxVillagersPerPlayer = 25,
                respawnCostFood = 1,
                baseHP = 5,
                baseAttackDamage = 1,
                baseMoveSpeedTicks = 4,
                baseAttackCooldownMax = 20,
                suitStats = null,
                bonusVillagersOnVillageClaim = 2,
                shrineHealIntervalTicks = 20,
                rampartDecrementMultiplier = 2,
                rampartDamageReduction = 1,
                rampartMaxHPBonus = 1,
                watchtowerClaimNumerator = 3,
                watchtowerClaimDenominator = 2,
                sanctuaryRespawnBoostPerWorker = 1,
                sanctuaryRespawnCostReductionPercent = 25
            };
        }

        public SuitStats GetSuitStats(SuitType type)
        {
            if (suitStats == null) return default;

            for (int i = 0; i < suitStats.Length; i++)
            {
                if (suitStats[i].suitType == type) return suitStats[i];
            }
            return default;
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
