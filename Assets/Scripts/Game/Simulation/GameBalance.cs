using UnityEngine;

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

    [CreateAssetMenu(fileName = "GameBalance", menuName = "NodeWar/Game Balance")]
    public class GameBalance : ScriptableObject
    {
        [Header("Tick Rate")]
        public int ticksPerSecond = 10;

        [Header("Claiming")]
        public int baseClaimPerTick = 17;
        public int decrementMultiplier = 4;
        public int claimThreshold = 10000;
        public int maxClaimersPerNode = 4;

        [Header("Combat")]
        public int respawnTicks = 50;
        public int healIntervalTicks = 30;
        public int breachThreshold = 3;

        [Header("Production")]
        public int foodProductionTicks = 30;
        public int materialProductionTicks = 40;
        public int metalProductionTicks = 50;
        public int marketFoodProductionTicks = 45;
        public int marketMaterialProductionTicks = 60;
        public int maxWorkersPerNode = 2;
        public int maxVillagersPerPlayer = 25;

        [Header("Costs")]
        public int respawnCostFood = 1;

        [Header("Villager Base Stats")]
        public int baseHP = 5;
        public int baseAttackDamage = 1;
        public int baseMoveSpeedTicks = 4;
        public int baseAttackCooldownMax = 20;

        [Header("Suit Stats (configure in Inspector)")]
        public SuitStats[] suitStats;

        [Header("Village Bonus")]
        public int bonusVillagersOnVillageClaim = 2;

        [Header("Node: Shrine")]
        public int shrineHealIntervalTicks = 20;

        [Header("Node: Rampart")]
        public int rampartDecrementMultiplier = 2;
        public int rampartDamageReduction = 1;
        public int rampartMaxHPBonus = 1;

        [Header("Node: Watchtower")]
        public int watchtowerClaimNumerator = 3;
        public int watchtowerClaimDenominator = 2;

        [Header("Node: Sanctuary")]
        public int sanctuaryRespawnBoostPerWorker = 1;
        public int sanctuaryRespawnCostReductionPercent = 25;

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